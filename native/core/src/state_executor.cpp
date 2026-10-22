#include "plv/state_executor.hpp"

#include <algorithm>
#include <cmath>
#include <regex>
#include <stdexcept>
#include <unordered_set>
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
    if (!isValidId(id) || !std::isfinite(capacityM3) || capacityM3 <= 0.0 ||
        vessels_.count(id) != 0 || sinks_.count(id) != 0) {
        return rejected("InvalidVessel", "invalid or duplicate vessel");
    }
    if (vessels_.size() >= kMaxVesselInventories) {
        return rejected("VesselLimitReached", "vessel inventory limit reached");
    }
    vessels_.emplace(id, VesselState{id, capacityM3, {}, 0});
    ++eventSequence_;
    return accepted();
}

CommandOutcome StateExecutor::createSink(const std::string& id) {
    if (!isValidId(id) || sinks_.count(id) != 0 || vessels_.count(id) != 0) {
        return rejected("InvalidSink", "invalid or duplicate sink");
    }
    if (sinks_.size() >= kMaxSinks) {
        return rejected("SinkLimitReached", "sink limit reached");
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
        found->second.materialRevision != 0U || found->second.inventory.referenceVolumeM3 != 0.0 ||
        volumeM3 > found->second.capacityM3) {
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
    return transferFixed(
        {sourceId,
         TransferQuantityBasis::LiquidVolumeM3,
         requestedVolumeM3,
         {{receiverId, 1.0}},
         spillSinkId},
        {{sourceId, expectedSourceRevision}, {receiverId, expectedReceiverRevision}});
}

CommandOutcome StateExecutor::transferFixed(
    const TransferFixedRequest& request,
    const std::unordered_map<std::string, std::uint64_t>& expectedMaterialRevisions) {
    const auto source = vessels_.find(request.sourceInventoryId);
    const auto overflowSink = sinks_.find(request.overflowSinkId);
    if (source == vessels_.end() || overflowSink == sinks_.end() ||
        !std::isfinite(request.quantityValue) || request.quantityValue <= 0.0) {
        return rejected("InvalidTransfer", "invalid transfer participants or quantity");
    }

    std::vector<CaptureFraction> captures = request.captureFractions;
    std::sort(captures.begin(), captures.end(), [](const auto& left, const auto& right) {
        return left.destinationInventoryId < right.destinationInventoryId;
    });
    std::unordered_set<std::string> destinationIds;
    double fractionSum = 0.0;
    for (const auto& capture : captures) {
        if (!isValidId(capture.destinationInventoryId) ||
            capture.destinationInventoryId == request.sourceInventoryId ||
            !std::isfinite(capture.fraction) || capture.fraction < 0.0 ||
            vessels_.count(capture.destinationInventoryId) == 0 ||
            !destinationIds.insert(capture.destinationInventoryId).second) {
            return rejected("InvalidTransfer", "invalid or duplicate capture destination");
        }
        fractionSum += capture.fraction;
        if (!std::isfinite(fractionSum) || fractionSum > 1.0 + 1e-12) {
            return rejected("InvalidTransfer", "capture fractions exceed one");
        }
    }

    if (expectedMaterialRevisions.size() != destinationIds.size() + 1U) {
        return rejected("StaleRevision", "expected revisions do not match touched inventories");
    }
    const auto sourceRevision = expectedMaterialRevisions.find(request.sourceInventoryId);
    if (sourceRevision == expectedMaterialRevisions.end() ||
        sourceRevision->second != source->second.materialRevision) {
        return rejected("StaleRevision", "source material revision changed");
    }
    for (const auto& destinationId : destinationIds) {
        const auto expected = expectedMaterialRevisions.find(destinationId);
        if (expected == expectedMaterialRevisions.end() ||
            expected->second != vessels_.at(destinationId).materialRevision) {
            return rejected("StaleRevision", "destination material revision changed");
        }
    }

    double requestedVolumeM3 = request.quantityValue;
    if (request.quantityBasis == TransferQuantityBasis::WaterMassKg) {
        const double availableWaterKg = source->second.inventory.solventWaterKg;
        if (availableWaterKg <= 0.0 || request.quantityValue > availableWaterKg + 1e-15) {
            return rejected("InsufficientSource", "fixed transfer exceeds source water inventory");
        }
        requestedVolumeM3 = source->second.inventory.referenceVolumeM3 *
                            (request.quantityValue / availableWaterKg);
    }
    if (!std::isfinite(requestedVolumeM3) || requestedVolumeM3 <= 0.0 ||
        requestedVolumeM3 > source->second.inventory.referenceVolumeM3 + 1e-15) {
        return rejected("InsufficientSource", "fixed transfer exceeds source inventory");
    }

    const auto parcel = source->second.inventory.fraction(requestedVolumeM3);
    auto vesselsAfter = vessels_;
    auto sinksAfter = sinks_;
    auto remaining = parcel;
    if (!vesselsAfter.at(request.sourceInventoryId).inventory.subtract(parcel)) {
        return rejected("InternalConservation", "source debit failed");
    }

    std::unordered_set<std::string> changedDestinations;
    for (const auto& capture : captures) {
        auto& destination = vesselsAfter.at(capture.destinationInventoryId);
        const double requestedCaptureM3 = parcel.referenceVolumeM3 * capture.fraction;
        const double freeCapacityM3 = std::max(
            0.0,
            destination.capacityM3 - destination.inventory.referenceVolumeM3);
        const double capturedVolumeM3 = std::min(requestedCaptureM3, freeCapacityM3);
        const auto captured = parcel.fraction(capturedVolumeM3);
        if (!remaining.subtract(captured)) {
            return rejected("InternalConservation", "parcel split failed");
        }
        destination.inventory.add(captured);
        if (captured.referenceVolumeM3 > 0.0) changedDestinations.insert(capture.destinationInventoryId);
    }
    sinksAfter.at(request.overflowSinkId).add(remaining);

    for (const auto& destinationId : destinationIds) {
        const auto& destination = vesselsAfter.at(destinationId);
        if (!destination.inventory.isFiniteNonNegative() ||
            destination.inventory.referenceVolumeM3 > destination.capacityM3 + 1e-15) {
            return rejected("InternalConservation", "post-transfer destination validation failed");
        }
    }
    if (!vesselsAfter.at(request.sourceInventoryId).inventory.isFiniteNonNegative() ||
        !sinksAfter.at(request.overflowSinkId).isFiniteNonNegative()) {
        return rejected("InternalConservation", "post-transfer ledger validation failed");
    }

    vessels_ = std::move(vesselsAfter);
    sinks_ = std::move(sinksAfter);
    ++vessels_.at(request.sourceInventoryId).materialRevision;
    for (const auto& destinationId : changedDestinations) {
        ++vessels_.at(destinationId).materialRevision;
    }
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
    return SessionSnapshot{branchId_, eventSequence_, simulationTimeS_, paused_, vessels_, sinks_};
}

CommandOutcome StateExecutor::restore(const SessionSnapshot& snapshot) {
    if (snapshot.branchId != branchId_ || !std::isfinite(snapshot.simulationTimeS) || snapshot.simulationTimeS < 0.0 ||
        snapshot.vessels.size() > kMaxVesselInventories || snapshot.sinks.size() > kMaxSinks) {
        return rejected("InvalidSnapshot", "snapshot identity or limits invalid");
    }
    for (const auto& entry : snapshot.vessels) {
        const auto& vessel = entry.second;
        if (entry.first != vessel.id || !isValidId(vessel.id) || !std::isfinite(vessel.capacityM3) ||
            vessel.capacityM3 <= 0.0 || !vessel.inventory.isFiniteNonNegative() ||
            vessel.inventory.referenceVolumeM3 > vessel.capacityM3 + 1e-15) {
            return rejected("InvalidSnapshot", "invalid vessel in snapshot");
        }
    }
    for (const auto& entry : snapshot.sinks) {
        if (!isValidId(entry.first) || !entry.second.isFiniteNonNegative() || snapshot.vessels.count(entry.first) != 0) {
            return rejected("InvalidSnapshot", "invalid sink in snapshot");
        }
    }
    eventSequence_ = snapshot.eventSequence;
    simulationTimeS_ = snapshot.simulationTimeS;
    paused_ = snapshot.paused;
    vessels_ = snapshot.vessels;
    sinks_ = snapshot.sinks;
    return accepted();
}

void StateExecutor::advanceTime(double deltaS) {
    if (!paused_ && std::isfinite(deltaS) && deltaS >= 0.0) {
        simulationTimeS_ += deltaS;
    }
}

void StateExecutor::setPaused(bool paused) { paused_ = paused; }
bool StateExecutor::paused() const { return paused_; }

}  // namespace plv
