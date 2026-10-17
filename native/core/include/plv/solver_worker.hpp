#pragma once

#include "plv/scheduler.hpp"
#include "plv/solver.hpp"
#include "plv/types.hpp"

#include <condition_variable>
#include <chrono>
#include <cstddef>
#include <deque>
#include <functional>
#include <mutex>
#include <optional>
#include <thread>

namespace plv {

struct SolverJob {
    ObservationRequest observation;
    SolveRequest solve;
};

struct SolverCompletion {
    ObservationRequest observation;
    SolveResult result;
};

class SolverWorker {
public:
    using SolveFunction = std::function<SolveResult(const SolveRequest&)>;

    SolverWorker(std::size_t capacity, SolveFunction solve);
    ~SolverWorker();
    SolverWorker(const SolverWorker&) = delete;
    SolverWorker& operator=(const SolverWorker&) = delete;

    CommandOutcome submit(SolverJob job);
    std::optional<SolverCompletion> poll();
    std::size_t outstanding() const;
    bool stalled(std::chrono::milliseconds threshold = std::chrono::seconds(2)) const;
    // Returns false while an engine call is still using the worker context.
    // A later call can complete shutdown after that call returns.
    bool requestStop();

private:
    void run();

    const std::size_t capacity_;
    SolveFunction solve_;
    mutable std::mutex mutex_;
    std::condition_variable available_;
    std::deque<SolverJob> pending_;
    std::deque<SolverCompletion> completed_;
    std::size_t outstanding_ = 0;
    bool stopping_ = false;
    bool solving_ = false;
    std::chrono::steady_clock::time_point solveStarted_{};
    std::thread thread_;
};

}  // namespace plv
