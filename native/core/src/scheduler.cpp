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
    const auto vesselId = request.vesselId;
    const bool replacesPending = pendingByVessel_.count(vesselId) != 0;
    if (!replacesPending && pendingByVessel_.size() >= limits_.maxPendingRequests) {
        return reject("ObservationQueueFull", "pending observation queue is full");
    }
    markMaterialRevision(vesselId, request.materialRevision, request.submittedNs);
    pendingByVessel_.insert_or_assign(vesselId, std::move(request));
    if (inFlightVessels_.count(vesselId) == 0 && queuedVessels_.insert(vesselId).second) {
        readyVessels_.push_back(vesselId);
    }
    return accept();
}

std::optional<ObservationRequest> ObservationScheduler::takeNext() {
    if (readyVessels_.empty()) {
        return std::nullopt;
    }
    const auto vesselId = readyVessels_.front();
    readyVessels_.pop_front();
    queuedVessels_.erase(vesselId);
    const auto found = pendingByVessel_.find(vesselId);
    if (found == pendingByVessel_.end()) return std::nullopt;
    auto request = std::move(found->second);
    pendingByVessel_.erase(found);
    inFlightVessels_.insert(vesselId);
    observations_[key(request.vesselId, request.observable)].freshness = ObservationFreshness::Pending;
    return request;
}

void ObservationScheduler::complete(const ObservationRequest& request, const ObservationResultInput& result) {
    inFlightVessels_.erase(request.vesselId);
    if (pendingByVessel_.count(request.vesselId) != 0 && queuedVessels_.insert(request.vesselId).second) {
        readyVessels_.push_back(request.vesselId);
    }
    auto& record = observations_[key(request.vesselId, request.observable)];
    if (request.materialRevision < record.solvedRevision) {
        return;
    }
    record.support = result.support;
    record.maturity = result.maturity;
    record.succeeded = result.succeeded;
    record.solvedRevision = request.materialRevision;
    record.value = result.value;
    const auto authoritative = authoritativeRevisions_.find(request.vesselId);
    const bool matchesAuthoritative = authoritative == authoritativeRevisions_.end() ||
                                      authoritative->second == request.materialRevision;
    record.freshness = result.succeeded && matchesAuthoritative
                           ? ObservationFreshness::Current
                           : ObservationFreshness::Stale;
}

const ObservationRecord* ObservationScheduler::observation(
    const std::string& vesselId,
    const std::string& observable) const {
    const auto found = observations_.find(key(vesselId, observable));
    return found == observations_.end() ? nullptr : &found->second;
}

void ObservationScheduler::markMaterialRevision(
    const std::string& vesselId,
    std::uint64_t revision,
    std::uint64_t nowNs) {
    auto& authoritative = authoritativeRevisions_[vesselId];
    if (revision <= authoritative) return;
    authoritative = revision;
    const auto prefix = vesselId + "\x1f";
    for (auto& entry : observations_) {
        if (entry.first.compare(0, prefix.size(), prefix) == 0 && entry.second.solvedRevision < revision) {
            entry.second.freshness = ObservationFreshness::Stale;
        }
    }
    auto budget = budgets_.find(vesselId);
    if (budget != budgets_.end() && budget->second.initialized && revision > budget->second.revision) {
        budget->second.revision = revision;
        if (!budget->second.oldestUnresolvedChangeNs) {
            budget->second.oldestUnresolvedChangeNs = nowNs;
        }
    }
}

void ObservationScheduler::markSolved(
    const std::string& vesselId,
    std::uint64_t revision,
    std::uint64_t nowNs) {
    authoritativeRevisions_[vesselId] = revision;
    budgets_[vesselId] = {revision, nowNs, 0.0, true, std::nullopt};
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
    if (state.oldestUnresolvedChangeNs) {
        if (nowNs < *state.oldestUnresolvedChangeNs ||
            nowNs - *state.oldestUnresolvedChangeNs > limits_.maxObservationAgeNs) {
            return reject("ObservationAgeHold", "transport would exceed observation age budget");
        }
    }
    if (state.grossVolumeM3 + proposedGrossVolumeM3 > limits_.maxGrossVolumeBetweenSolvesM3 + 1e-18) {
        return reject("ObservationVolumeHold", "transport would exceed gross volume budget");
    }
    return accept();
}

void ObservationScheduler::recordGrossTransport(
    const std::string& vesselId,
    double grossVolumeM3,
    std::uint64_t nowNs) {
    if (std::isfinite(grossVolumeM3) && grossVolumeM3 > 0.0) {
        auto& state = budgets_[vesselId];
        state.grossVolumeM3 += grossVolumeM3;
        if (!state.oldestUnresolvedChangeNs) state.oldestUnresolvedChangeNs = nowNs;
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
