#include "plv/state_executor.hpp"

#include <cmath>
#include <regex>
#include <stdexcept>
#include <utility>

namespace plv {

namespace {
CommandOutcome accepted() {
    return {true, "Accepted", ""};
}

CommandOutcome rejected(const std::string& code, const std::string& message) {
    return {false, code, message};
}
}  // namespace

bool isValidId(const std::string& value) {
    static const std::regex pattern("^[A-Za-z0-9._-]{1,64}$");
    return std::regex_match(value, pattern);
}

StateExecutor::StateExecutor(std::string branchId) : branchId_(std::move(branchId)) {
    if (!isValidId(branchId_)) {
        throw std::invalid_argument("invalid branch id");
    }
}

CommandOutcome StateExecutor::createVessel(const std::string& id, double capacityM3) {
    if (!isValidId(id) || !std::isfinite(capacityM3) || capacityM3 <= 0.0 || vessels_.count(id) != 0) {
        return rejected("InvalidVessel", "invalid or duplicate vessel");
    }
    vessels_.emplace(id, VesselState{id, capacityM3, {}, 0});
    ++eventSequence_;
    return accepted();
}

CommandOutcome StateExecutor::createSink(const std::string& id) {
    if (!isValidId(id) || sinks_.count(id) != 0 || vessels_.count(id) != 0) {
        return rejected("InvalidSink", "invalid or duplicate sink");
    }
    sinks_.emplace(id, MaterialState{});
    ++eventSequence_;
    return accepted();
}

CommandOutcome StateExecutor::prepareStock(
    const std::string& vesselId,
    StockKind kind,
    double concentrationMolPerL,
    double volumeM3) {
    auto found = vessels_.find(vesselId);
    if (found == vessels_.end() || !std::isfinite(volumeM3) || volumeM3 < 0.0 ||
        !std::isfinite(concentrationMolPerL) || concentrationMolPerL < 0.0 ||
        found->second.inventory.referenceVolumeM3 != 0.0 || volumeM3 > found->second.capacityM3) {
        return rejected("InvalidPreparation", "stock preparation rejected");
    }
    auto prepared = MaterialState::preparedStock(vesselId, kind, concentrationMolPerL, volumeM3);
    if (!prepared.isFiniteNonNegative()) {
        return rejected("InvalidPreparation", "non-finite stock preparation");
    }
    found->second.inventory = prepared;
    ++found->second.materialRevision;
    ++eventSequence_;
    return accepted();
}

CommandOutcome StateExecutor::transferFixed(
    const std::string& sourceId,
    const std::string& receiverId,
    const std::string& spillSinkId,
    double requestedVolumeM3,
    std::uint64_t expectedSourceRevision,
    std::uint64_t expectedReceiverRevision) {
    auto source = vessels_.find(sourceId);
    auto receiver = vessels_.find(receiverId);
    auto spill = sinks_.find(spillSinkId);
    if (source == vessels_.end() || receiver == vessels_.end() || spill == sinks_.end() ||
        source == receiver || !std::isfinite(requestedVolumeM3) || requestedVolumeM3 <= 0.0) {
        return rejected("InvalidTransfer", "invalid transfer participants or quantity");
    }
    if (source->second.materialRevision != expectedSourceRevision ||
        receiver->second.materialRevision != expectedReceiverRevision) {
        return rejected("StaleRevision", "touched material revision changed");
    }
    if (requestedVolumeM3 > source->second.inventory.referenceVolumeM3 + 1e-15) {
        return rejected("InsufficientSource", "fixed transfer exceeds source inventory");
    }

    const auto parcel = source->second.inventory.fraction(requestedVolumeM3);
    const double freeCapacity = std::max(
        0.0,
        receiver->second.capacityM3 - receiver->second.inventory.referenceVolumeM3);
    const double capturedVolume = std::min(parcel.referenceVolumeM3, freeCapacity);
    const auto captured = parcel.fraction(capturedVolume);
    auto overflow = parcel;
    if (!overflow.subtract(captured)) {
        return rejected("InternalConservation", "parcel split failed");
    }

    auto sourceAfter = source->second.inventory;
    auto receiverAfter = receiver->second.inventory;
    auto spillAfter = spill->second;
    if (!sourceAfter.subtract(parcel)) {
        return rejected("InternalConservation", "source debit failed");
    }
    receiverAfter.add(captured);
    spillAfter.add(overflow);
    if (!sourceAfter.isFiniteNonNegative() || !receiverAfter.isFiniteNonNegative() ||
        !spillAfter.isFiniteNonNegative() || receiverAfter.referenceVolumeM3 > receiver->second.capacityM3 + 1e-15) {
        return rejected("InternalConservation", "post-transfer validation failed");
    }

    source->second.inventory = sourceAfter;
    receiver->second.inventory = receiverAfter;
    spill->second = spillAfter;
    ++source->second.materialRevision;
    ++receiver->second.materialRevision;
    ++eventSequence_;
    return accepted();
}

const VesselState& StateExecutor::vessel(const std::string& id) const {
    return vessels_.at(id);
}

const MaterialState& StateExecutor::sink(const std::string& id) const {
    return sinks_.at(id);
}

MaterialState StateExecutor::totalLedger() const {
    MaterialState total;
    for (const auto& entry : vessels_) {
        total.add(entry.second.inventory);
    }
    for (const auto& entry : sinks_) {
        total.add(entry.second);
    }
    return total;
}

SessionSnapshot StateExecutor::snapshot() const {
    return SessionSnapshot{branchId_, eventSequence_, 0.0, vessels_};
}

}  // namespace plv
