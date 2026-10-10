#include "doctest.h"
#include "plv/solver_worker.hpp"

#include <chrono>
#include <condition_variable>
#include <mutex>
#include <memory>
#include <thread>

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
