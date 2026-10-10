#include "plv/solver_worker.hpp"

#include <exception>
#include <utility>

namespace plv {

SolverWorker::SolverWorker(std::size_t capacity, SolveFunction solve)
    : capacity_(capacity), solve_(std::move(solve)), thread_(&SolverWorker::run, this) {}

SolverWorker::~SolverWorker() {
    requestStop();
    if (thread_.joinable()) thread_.join();
}

bool SolverWorker::requestStop() {
    std::lock_guard<std::mutex> lock(mutex_);
    stopping_ = true;
    pending_.clear();
    available_.notify_one();
    return !solving_;
}

bool SolverWorker::stalled(std::chrono::milliseconds threshold) const {
    std::lock_guard<std::mutex> lock(mutex_);
    return solving_ && std::chrono::steady_clock::now() - solveStarted_ > threshold;
}

CommandOutcome SolverWorker::submit(SolverJob job) {
    std::lock_guard<std::mutex> lock(mutex_);
    if (stopping_ || !solve_) return {false, "SolverUnavailable", "solver worker is unavailable"};
    if (capacity_ == 0U || outstanding_ >= capacity_) {
        return {false, "SolverQueueFull", "solver request capacity is exhausted"};
    }
    pending_.push_back(std::move(job));
    ++outstanding_;
    available_.notify_one();
    return {true, "Accepted", ""};
}

std::optional<SolverCompletion> SolverWorker::poll() {
    std::lock_guard<std::mutex> lock(mutex_);
    if (completed_.empty()) return std::nullopt;
    auto result = std::move(completed_.front());
    completed_.pop_front();
    --outstanding_;
    return result;
}

std::size_t SolverWorker::outstanding() const {
    std::lock_guard<std::mutex> lock(mutex_);
    return outstanding_;
}

void SolverWorker::run() {
    for (;;) {
        SolverJob job;
        {
            std::unique_lock<std::mutex> lock(mutex_);
            available_.wait(lock, [this] { return stopping_ || !pending_.empty(); });
            if (stopping_ && pending_.empty()) {
                // The callback owns the IPhreeqc adapter and its global registry
                // context. Release it on the sole solver thread after all calls.
                solve_ = {};
                return;
            }
            job = std::move(pending_.front());
            pending_.pop_front();
            solving_ = true;
            solveStarted_ = std::chrono::steady_clock::now();
        }

        SolveResult result;
        try {
            result = solve_(job.solve);
        } catch (const std::exception& error) {
            result.error = error.what();
        } catch (...) {
            result.error = "solver callback failed";
        }

        {
            std::lock_guard<std::mutex> lock(mutex_);
            completed_.push_back({std::move(job.observation), std::move(result)});
            solving_ = false;
        }
    }
}

}  // namespace plv
