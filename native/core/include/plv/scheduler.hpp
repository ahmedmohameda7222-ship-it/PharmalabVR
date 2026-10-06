#pragma once

#include "plv/types.hpp"

#include <cstddef>
#include <cstdint>
#include <deque>
#include <optional>
#include <string>
#include <unordered_map>

namespace plv {

enum class ObservationSupport { Supported, Unsupported };
enum class ObservationMaturity { Research, Published };
enum class ObservationFreshness { Current, Stale, Pending };

struct ObservationRequest {
    std::string vesselId;
    std::string observable;
    std::uint64_t materialRevision = 0;
    std::uint64_t submittedNs = 0;
    double grossVolumeSinceSolveM3 = 0.0;
};

struct ObservationResultInput {
    ObservationSupport support = ObservationSupport::Unsupported;
    ObservationMaturity maturity = ObservationMaturity::Research;
    bool succeeded = false;
    double value = 0.0;
};

struct ObservationRecord {
    ObservationSupport support = ObservationSupport::Unsupported;
    ObservationMaturity maturity = ObservationMaturity::Research;
    ObservationFreshness freshness = ObservationFreshness::Pending;
    bool succeeded = false;
    std::uint64_t solvedRevision = 0;
    double value = 0.0;
};

struct SchedulerLimits {
    double maxGrossVolumeBetweenSolvesM3 = 0.0;
    std::uint64_t maxObservationAgeNs = 0;
    std::size_t maxPendingRequests = 0;
};

class ObservationScheduler {
public:
    explicit ObservationScheduler(SchedulerLimits limits);

    CommandOutcome offer(ObservationRequest request);
    std::optional<ObservationRequest> takeNext();
    void complete(const ObservationRequest& request, const ObservationResultInput& result);
    const ObservationRecord* observation(const std::string& vesselId, const std::string& observable) const;
    void markMaterialRevision(const std::string& vesselId, std::uint64_t revision);
    void markSolved(const std::string& vesselId, std::uint64_t revision, std::uint64_t nowNs);
    CommandOutcome admitTransport(const std::string& vesselId, double proposedGrossVolumeM3, std::uint64_t nowNs) const;
    void recordGrossTransport(const std::string& vesselId, double grossVolumeM3);

private:
    struct BudgetState {
        std::uint64_t revision = 0;
        std::uint64_t lastSolvedNs = 0;
        double grossVolumeM3 = 0.0;
        bool initialized = false;
    };

    static std::string key(const std::string& vesselId, const std::string& observable);
    SchedulerLimits limits_;
    std::deque<ObservationRequest> pending_;
    std::unordered_map<std::string, ObservationRecord> observations_;
    std::unordered_map<std::string, BudgetState> budgets_;
};

enum class HoldCause { None, Compute, Tracking, Focus, TimeDiscontinuity, StaleInput };

class HoldState {
public:
    void enter(HoldCause cause);
    void observeNeutralInput(bool neutral);
    void refreshBaseline();
    void requestContinue();
    bool canContinue() const;
    bool release();
    bool active() const;
    HoldCause cause() const;

private:
    HoldCause cause_ = HoldCause::None;
    bool neutral_ = false;
    bool baselineFresh_ = false;
    bool continueRequested_ = false;
};

}  // namespace plv
