#include "plv/scheduler.hpp"
#include "plv/state_executor.hpp"

#include <cmath>
#include <utility>

namespace plv {

namespace {
CommandOutcome accept() { return {true, "Accepted", ""}; }
CommandOutcome reject(const std::string& code, const std::string& message) { return {false, code, message}; }
}  // namespace

ObservationScheduler::ObservationScheduler(SchedulerLimits limits) : limits_(limits) {}

std::string ObservationScheduler::key(const std::string& vesselId, const std::string& observable) {
    return vesselId + "\x1f" + observable;
}

CommandOutcome ObservationScheduler::offer(ObservationRequest request) {
    if (!isValidId(request.vesselId) || !isValidId(request.observable) ||
        !std::isfinite(request.grossVolumeSinceSolveM3) || request.grossVolumeSinceSolveM3 < 0.0) {
        return reject("InvalidObservationRequest", "invalid observation request");
    }
    if (pending_.size() >= limits_.maxPendingRequests) {
        return reject("ObservationQueueFull", "pending observation queue is full");
    }
    pending_.push_back(std::move(request));
    return accept();
}

std::optional<ObservationRequest> ObservationScheduler::takeNext() {
    if (pending_.empty()) {
        return std::nullopt;
    }
    auto request = pending_.front();
    pending_.pop_front();
    observations_[key(request.vesselId, request.observable)].freshness = ObservationFreshness::Pending;
    return request;
}

void ObservationScheduler::complete(const ObservationRequest& request, const ObservationResultInput& result) {
    auto& record = observations_[key(request.vesselId, request.observable)];
    if (request.materialRevision < record.solvedRevision) {
        return;
    }
    record.support = result.support;
    record.maturity = result.maturity;
    record.succeeded = result.succeeded;
    record.solvedRevision = request.materialRevision;
    record.value = result.value;
    record.freshness = result.succeeded ? ObservationFreshness::Current : ObservationFreshness::Stale;
}

const ObservationRecord* ObservationScheduler::observation(
    const std::string& vesselId,
    const std::string& observable) const {
    const auto found = observations_.find(key(vesselId, observable));
    return found == observations_.end() ? nullptr : &found->second;
}

void ObservationScheduler::markMaterialRevision(const std::string& vesselId, std::uint64_t revision) {
    const auto prefix = vesselId + "\x1f";
    for (auto& entry : observations_) {
        if (entry.first.compare(0, prefix.size(), prefix) == 0 && entry.second.solvedRevision < revision) {
            entry.second.freshness = ObservationFreshness::Stale;
        }
    }
}

void ObservationScheduler::markSolved(
    const std::string& vesselId,
    std::uint64_t revision,
    std::uint64_t nowNs) {
    budgets_[vesselId] = {revision, nowNs, 0.0, true};
}

CommandOutcome ObservationScheduler::admitTransport(
    const std::string& vesselId,
    double proposedGrossVolumeM3,
    std::uint64_t nowNs) const {
    if (!std::isfinite(proposedGrossVolumeM3) || proposedGrossVolumeM3 < 0.0) {
        return reject("InvalidTransportBudget", "invalid gross transport volume");
    }
    const auto found = budgets_.find(vesselId);
    if (found == budgets_.end() || !found->second.initialized) {
        return reject("ObservationRequired", "no solved observation budget boundary");
    }
    const auto& state = found->second;
    if (nowNs < state.lastSolvedNs || nowNs - state.lastSolvedNs > limits_.maxObservationAgeNs) {
        return reject("ObservationAgeHold", "transport would exceed observation age budget");
    }
    if (state.grossVolumeM3 + proposedGrossVolumeM3 > limits_.maxGrossVolumeBetweenSolvesM3 + 1e-18) {
        return reject("ObservationVolumeHold", "transport would exceed gross volume budget");
    }
    return accept();
}

void ObservationScheduler::recordGrossTransport(const std::string& vesselId, double grossVolumeM3) {
    if (std::isfinite(grossVolumeM3) && grossVolumeM3 > 0.0) {
        budgets_[vesselId].grossVolumeM3 += grossVolumeM3;
    }
}

void HoldState::enter(HoldCause cause) {
    cause_ = cause;
    neutral_ = false;
    baselineFresh_ = false;
    continueRequested_ = false;
}

void HoldState::observeNeutralInput(bool neutral) { neutral_ = neutral; }
void HoldState::refreshBaseline() { baselineFresh_ = true; }
void HoldState::requestContinue() { continueRequested_ = true; }
bool HoldState::canContinue() const {
    return active() && neutral_ && baselineFresh_ && continueRequested_;
}
bool HoldState::release() {
    if (!canContinue()) {
        return false;
    }
    cause_ = HoldCause::None;
    neutral_ = false;
    baselineFresh_ = false;
    continueRequested_ = false;
    return true;
}
bool HoldState::active() const { return cause_ != HoldCause::None; }
HoldCause HoldState::cause() const { return cause_; }

}  // namespace plv
