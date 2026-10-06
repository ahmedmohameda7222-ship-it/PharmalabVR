#pragma once

#include "plv/material.hpp"

#include <cstdint>
#include <string>
#include <unordered_map>

namespace plv {

struct CommandOutcome {
    bool accepted = false;
    std::string code;
    std::string message;
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
    std::unordered_map<std::string, VesselState> vessels;
};

}  // namespace plv
