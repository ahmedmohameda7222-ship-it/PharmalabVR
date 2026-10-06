#pragma once

#include "plv/material.hpp"
#include "plv/types.hpp"

#include <array>
#include <cstdint>
#include <string>
#include <vector>

namespace plv {

struct BuretteProfile {
    std::string id;
    double dischargeCoefficient = 0.60;
    double maxApertureRadiusM = 0.00030;
    double gravityMPerS2 = 9.80665;
    double internalRadiusM = 0.006;
    double dropReferenceVolumeM3 = 5e-8;

    static BuretteProfile researchDefault();
};

struct ToolParcelState {
    MaterialState tip;
    MaterialState inFlight;

    MaterialState rinse(const MaterialState& rinseParcel, double residualVolumeM3);
    MaterialState landInFlight();
};

struct GeometryCaptureSample {
    std::string coordinateFrame;
    std::string profileId;
    std::uint64_t profileRevision = 0;
    std::array<double, 4> orientation{};
    std::vector<double> captureFractions;
};

CommandOutcome validateGeometrySample(
    const GeometryCaptureSample& sample,
    const std::string& requiredFrame,
    const std::string& requiredProfile,
    std::uint64_t requiredRevision);

double simulateBuretteDelivery(
    const BuretteProfile& profile,
    double initialVolumeM3,
    double actuator01,
    double durationS,
    double transportTickS);

}  // namespace plv
