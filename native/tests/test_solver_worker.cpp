#include "doctest.h"
#include "plv/solver_worker.hpp"

#include <chrono>
#include <condition_variable>
#include <mutex>

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
