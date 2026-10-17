#pragma once

#include "plv/material.hpp"

#include <cstdint>
#include <string>
#include <unordered_map>
#include <vector>

namespace plv {

struct CommandOutcome {
    bool accepted = false;
    std::string code;
    std::string message;
};

enum class TransferQuantityBasis { WaterMassKg, LiquidVolumeM3 };

struct CaptureFraction {
    std::string destinationInventoryId;
    double fraction = 0.0;
};

struct TransferFixedRequest {
    std::string sourceInventoryId;
    TransferQuantityBasis quantityBasis = TransferQuantityBasis::LiquidVolumeM3;
    double quantityValue = 0.0;
    std::vector<CaptureFraction> captureFractions;
    std::string overflowSinkId;
};

struct VesselState {
    std::string id;
    double capacityM3 = 0.0;
    MaterialState inventory;
    std::uint64_t materialRevision = 0;
};

struct SessionSnapshot {
    std::string branchId;
    std::uint64_t eventSequence = 0;
    double simulationTimeS = 0.0;
    bool paused = false;
    std::unordered_map<std::string, VesselState> vessels;
    std::unordered_map<std::string, MaterialState> sinks;
};

}  // namespace plv
