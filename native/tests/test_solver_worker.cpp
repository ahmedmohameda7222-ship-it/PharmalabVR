#include <ostream>
#include "doctest.h"
#include "plv/solver_worker.hpp"

#include <chrono>
#include <condition_variable>
#include <mutex>
#include <memory>
#include <thread>
#include <vector>

using namespace plv;

TEST_CASE("O03 worker keeps solve execution outside queue locks and bounds all outstanding work") {
    std::mutex gateMutex;
    std::condition_variable gateChanged;
    bool entered = false;
    bool release = false;
    SolverWorker worker(2, [&](const SolveRequest&) {
        std::unique_lock<std::mutex> lock(gateMutex);
        entered = true;
        gateChanged.notify_one();
        gateChanged.wait(lock, [&] { return release; });
        SolveResult result;
        result.succeeded = true;
        result.pH = 7.0;
        return result;
    });

    REQUIRE(worker.submit({{"A", "pH", 1, 10, 0.0}, {}}).accepted);
    {
        std::unique_lock<std::mutex> lock(gateMutex);
        REQUIRE(gateChanged.wait_for(lock, std::chrono::seconds(2), [&] { return entered; }));
    }
    CHECK(worker.submit({{"B", "pH", 1, 11, 0.0}, {}}).accepted);
    CHECK_FALSE(worker.submit({{"C", "pH", 1, 12, 0.0}, {}}).accepted);
    CHECK(worker.outstanding() == 2U);
    CHECK_FALSE(worker.poll().has_value());

    {
        std::lock_guard<std::mutex> lock(gateMutex);
        release = true;
    }
    gateChanged.notify_one();

    std::optional<SolverCompletion> first;
    for (int attempt = 0; attempt < 200 && !first; ++attempt) {
        first = worker.poll();
        if (!first) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(first.has_value());
    CHECK(first->observation.vesselId == "A");
    CHECK(first->result.succeeded);
}

TEST_CASE("P05_02 worker replaces a vessel's pending equilibrium without losing peer order") {
    std::mutex gateMutex;
    std::condition_variable gateChanged;
    bool entered = false;
    bool release = false;
    SolverWorker worker(3, [&](const SolveRequest&) {
        std::unique_lock<std::mutex> lock(gateMutex);
        if (!entered) {
            entered = true;
            gateChanged.notify_one();
            gateChanged.wait(lock, [&] { return release; });
        }
        SolveResult result;
        result.succeeded = true;
        result.pH = 7.0;
        return result;
    });
    REQUIRE(worker.submit({{"A", "pH", 1, 1, 0.0}, {}}).accepted);
    {
        std::unique_lock<std::mutex> lock(gateMutex);
        REQUIRE(gateChanged.wait_for(lock, std::chrono::seconds(2), [&] { return entered; }));
    }
    REQUIRE(worker.submit({{"B", "pH", 1, 2, 0.0}, {}}).accepted);
    REQUIRE(worker.submit({{"C", "pH", 1, 3, 0.0}, {}}).accepted);
    const auto replacement = worker.submit({{"B", "pH", 2, 4, 0.0}, {}});
    CHECK(replacement.accepted);
    const auto stale = worker.submit({{"B", "pH", 1, 5, 0.0}, {}});
    CHECK_FALSE(stale.accepted);
    CHECK(stale.code == "StaleObservationRequest");
    CHECK(worker.outstanding() == 3U);
    {
        std::lock_guard<std::mutex> lock(gateMutex);
        release = true;
    }
    gateChanged.notify_one();
    std::vector<SolverCompletion> results;
    for (int attempt = 0; attempt < 200 && results.size() < 3U; ++attempt) {
        if (auto result = worker.poll()) results.push_back(std::move(*result));
        else std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(results.size() == 3U);
    CHECK(results[0].observation.vesselId == "A");
    CHECK(results[1].observation.vesselId == "B");
    CHECK(results[1].observation.materialRevision == 2U);
    CHECK(results[2].observation.vesselId == "C");
}

TEST_CASE("P05_02 restored dependency generation replaces older pending work despite lower revision") {
    std::mutex gateMutex;
    std::condition_variable gateChanged;
    bool entered = false;
    bool release = false;
    SolverWorker worker(2, [&](const SolveRequest&) {
        std::unique_lock<std::mutex> lock(gateMutex);
        if (!entered) {
            entered = true;
            gateChanged.notify_one();
            gateChanged.wait(lock, [&] { return release; });
        }
        SolveResult result;
        result.succeeded = true;
        return result;
    });
    ObservationRequest active{"other", "pH", 2, 1, 0.0};
    active.dependencyGeneration = 1;
    ObservationRequest oldPending{"acid", "pH", 2, 2, 0.0};
    oldPending.dependencyGeneration = 1;
    ObservationRequest restored{"acid", "pH", 1, 3, 0.0};
    restored.dependencyGeneration = 2;
    REQUIRE(worker.submit({active, {}}).accepted);
    {
        std::unique_lock<std::mutex> lock(gateMutex);
        REQUIRE(gateChanged.wait_for(lock, std::chrono::seconds(2), [&] { return entered; }));
    }
    REQUIRE(worker.submit({oldPending, {}}).accepted);
    CHECK(worker.submit({restored, {}}).accepted);
    {
        std::lock_guard<std::mutex> lock(gateMutex);
        release = true;
    }
    gateChanged.notify_one();
    std::vector<SolverCompletion> results;
    for (int attempt = 0; attempt < 200 && results.size() < 2U; ++attempt) {
        if (auto result = worker.poll()) results.push_back(std::move(*result));
        else std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(results.size() == 2U);
    CHECK(results[1].observation.vesselId == "acid");
    CHECK(results[1].observation.materialRevision == 1U);
    CHECK(results[1].observation.dependencyGeneration == 2U);
}

TEST_CASE("P01_02 solver callback and owned engine are destroyed on worker thread") {
    std::thread::id solveThread;
    std::thread::id destructionThread;
    bool destroyed = false;
    auto engine = std::shared_ptr<int>(new int(1), [&](int* value) {
        destructionThread = std::this_thread::get_id();
        destroyed = true;
        delete value;
    });
    {
        SolverWorker worker(1, [engine, &solveThread](const SolveRequest&) {
            solveThread = std::this_thread::get_id();
            SolveResult result;
            result.succeeded = true;
            return result;
        });
        engine.reset();
        REQUIRE(worker.submit({{"source", "pH", 1, 0, 0.0}, {}}).accepted);
        bool complete = false;
        for (int attempt = 0; attempt < 200 && !complete; ++attempt) {
            complete = worker.poll().has_value();
            if (!complete) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        REQUIRE(complete);
    }
    CHECK(destroyed);
    CHECK(solveThread != std::this_thread::get_id());
    CHECK(destructionThread == solveThread);
}

TEST_CASE("P01_02 blocked solve reports busy shutdown without detaching its context") {
    std::mutex gateMutex;
    std::condition_variable gateChanged;
    bool entered = false;
    bool release = false;
    SolverWorker worker(2, [&](const SolveRequest&) {
        std::unique_lock<std::mutex> lock(gateMutex);
        entered = true;
        gateChanged.notify_one();
        gateChanged.wait(lock, [&] { return release; });
        SolveResult result;
        result.succeeded = true;
        return result;
    });
    REQUIRE(worker.submit({{"source", "pH", 1, 0, 0.0}, {}}).accepted);
    {
        std::unique_lock<std::mutex> lock(gateMutex);
        REQUIRE(gateChanged.wait_for(lock, std::chrono::seconds(2), [&] { return entered; }));
    }
    std::this_thread::sleep_for(std::chrono::milliseconds(15));
    CHECK(worker.stalled(std::chrono::milliseconds(10)));
    CHECK_FALSE(worker.requestStop());
    CHECK_FALSE(worker.submit({{"other", "pH", 1, 0, 0.0}, {}}).accepted);
    {
        std::lock_guard<std::mutex> lock(gateMutex);
        release = true;
    }
    gateChanged.notify_one();
    for (int attempt = 0; attempt < 200 && !worker.requestStop(); ++attempt)
        std::this_thread::sleep_for(std::chrono::milliseconds(5));
    CHECK(worker.requestStop());
}

TEST_CASE("P05_03 stopping a blocked solver discards queued work from its bounded accounting") {
    std::mutex gateMutex;
    std::condition_variable gateChanged;
    bool entered = false;
    bool release = false;
    SolverWorker worker(2, [&](const SolveRequest&) {
        std::unique_lock<std::mutex> lock(gateMutex);
        entered = true;
        gateChanged.notify_one();
        gateChanged.wait(lock, [&] { return release; });
        SolveResult result;
        result.succeeded = true;
        return result;
    });
    REQUIRE(worker.submit({{"A", "pH", 1, 1, 0.0}, {}}).accepted);
    {
        std::unique_lock<std::mutex> lock(gateMutex);
        REQUIRE(gateChanged.wait_for(lock, std::chrono::seconds(2), [&] { return entered; }));
    }
    REQUIRE(worker.submit({{"B", "pH", 1, 2, 0.0}, {}}).accepted);
    CHECK(worker.outstanding() == 2U);
    CHECK_FALSE(worker.requestStop());
    CHECK(worker.outstanding() == 1U);
    {
        std::lock_guard<std::mutex> lock(gateMutex);
        release = true;
    }
    gateChanged.notify_one();
    std::optional<SolverCompletion> completion;
    for (int attempt = 0; attempt < 200 && !completion; ++attempt) {
        completion = worker.poll();
        if (!completion) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(completion.has_value());
    CHECK(completion->observation.vesselId == "A");
    CHECK(worker.outstanding() == 0U);
}

TEST_CASE("P05_03 default two second wall diagnostic does not replace a blocked worker") {
    std::mutex gateMutex;
    std::condition_variable gateChanged;
    bool entered = false;
    bool release = false;
    SolverWorker worker(1, [&](const SolveRequest&) {
        std::unique_lock<std::mutex> lock(gateMutex);
        entered = true;
        gateChanged.notify_one();
        gateChanged.wait(lock, [&] { return release; });
        return SolveResult{};
    });
    REQUIRE(worker.submit({{"A", "pH", 1, 1, 0.0}, {}}).accepted);
    {
        std::unique_lock<std::mutex> lock(gateMutex);
        REQUIRE(gateChanged.wait_for(lock, std::chrono::seconds(2), [&] { return entered; }));
    }
    CHECK_FALSE(worker.stalled());
    CHECK_FALSE(worker.submit({{"B", "pH", 1, 2, 0.0}, {}}).accepted);
    std::this_thread::sleep_for(std::chrono::milliseconds(2100));
    CHECK(worker.stalled());
    CHECK_FALSE(worker.requestStop());
    CHECK(worker.outstanding() == 1U);
    {
        std::lock_guard<std::mutex> lock(gateMutex);
        release = true;
    }
    gateChanged.notify_one();
}
