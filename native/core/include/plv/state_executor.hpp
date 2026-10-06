#pragma once

#include "plv/types.hpp"

#include <string>
#include <unordered_map>

namespace plv {

class StateExecutor {
public:
    explicit StateExecutor(std::string branchId);

    CommandOutcome createVessel(const std::string& id, double capacityM3);
    CommandOutcome createSink(const std::string& id);
    CommandOutcome prepareStock(
        const std::string& vesselId,
        StockKind kind,
        double concentrationMolPerL,
        double referenceVolumeM3);
    CommandOutcome transferFixed(
        const std::string& sourceId,
        const std::string& receiverId,
        const std::string& spillSinkId,
        double requestedVolumeM3,
        std::uint64_t expectedSourceRevision,
        std::uint64_t expectedReceiverRevision);

    const VesselState& vessel(const std::string& id) const;
    const MaterialState& sink(const std::string& id) const;
    MaterialState totalLedger() const;
    SessionSnapshot snapshot() const;
    CommandOutcome restore(const SessionSnapshot& snapshot);
    void advanceTime(double deltaS);
    void setPaused(bool paused);
    bool paused() const;

private:
    std::string branchId_;
    std::uint64_t eventSequence_ = 0;
    double simulationTimeS_ = 0.0;
    bool paused_ = false;
    std::unordered_map<std::string, VesselState> vessels_;
    std::unordered_map<std::string, MaterialState> sinks_;
};

bool isValidId(const std::string& value);

}  // namespace plv
