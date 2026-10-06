#include "plv/tool_models.hpp"

#include <algorithm>
#include <cmath>

namespace plv {

BuretteProfile BuretteProfile::researchDefault() {
    return {"burette-50ml-research-v1", 0.60, 0.00030, 9.80665, 0.006, 5e-8};
}

MaterialState ToolParcelState::rinse(const MaterialState& rinseParcel, double residualVolumeM3) {
    if (!rinseParcel.isFiniteNonNegative() || !std::isfinite(residualVolumeM3) || residualVolumeM3 < 0.0) {
        return {};
    }
    MaterialState combined = tip;
    combined.add(rinseParcel);
    const auto retained = combined.fraction(std::min(residualVolumeM3, combined.referenceVolumeM3));
    MaterialState waste = combined;
    if (!waste.subtract(retained)) {
        return {};
    }
    tip = retained;
    return waste;
}

MaterialState ToolParcelState::landInFlight() {
    MaterialState landed = inFlight;
    inFlight = {};
    return landed;
}

CommandOutcome validateGeometrySample(
    const GeometryCaptureSample& sample,
    const std::string& requiredFrame,
    const std::string& requiredProfile,
    std::uint64_t requiredRevision) {
    if (sample.coordinateFrame != requiredFrame || sample.profileId != requiredProfile ||
        sample.profileRevision != requiredRevision) {
        return {false, "InvalidGeometryIdentity", "coordinate frame, profile, or revision mismatch"};
    }
    double normSquared = 0.0;
    for (const double value : sample.orientation) {
        if (!std::isfinite(value)) {
            return {false, "InvalidOrientation", "orientation is non-finite"};
        }
        normSquared += value * value;
    }
    if (std::abs(normSquared - 1.0) > 1e-6) {
        return {false, "InvalidOrientation", "orientation quaternion is not unit length"};
    }
    double captureSum = 0.0;
    for (const double fraction : sample.captureFractions) {
        if (!std::isfinite(fraction) || fraction < 0.0 || fraction > 1.0) {
            return {false, "InvalidCapture", "capture fraction is outside [0,1]"};
        }
        captureSum += fraction;
    }
    if (captureSum > 1.0 + 1e-12) {
        return {false, "InvalidCapture", "capture fractions exceed unity"};
    }
    return {true, "Accepted", ""};
}

double simulateBuretteDelivery(
    const BuretteProfile& profile,
    double initialVolumeM3,
    double actuator01,
    double durationS,
    double transportTickS) {
    if (!std::isfinite(initialVolumeM3) || initialVolumeM3 <= 0.0 ||
        !std::isfinite(actuator01) || actuator01 < 0.0 || actuator01 > 1.0 ||
        !std::isfinite(durationS) || durationS < 0.0 ||
        !std::isfinite(transportTickS) || transportTickS <= 0.0 ||
        profile.dischargeCoefficient <= 0.0 || profile.maxApertureRadiusM <= 0.0 ||
        profile.internalRadiusM <= 0.0 || profile.gravityMPerS2 <= 0.0) {
        return 0.0;
    }
    constexpr double pi = 3.14159265358979323846;
    constexpr double integrationQuantumS = 0.001;
    const double columnAreaM2 = pi * profile.internalRadiusM * profile.internalRadiusM;
    const double apertureAreaM2 = pi * profile.maxApertureRadiusM * profile.maxApertureRadiusM * actuator01;
    double remaining = initialVolumeM3;
    double elapsed = 0.0;
    while (elapsed + 1e-15 < durationS && remaining > 0.0) {
        const double outer = std::min(transportTickS, durationS - elapsed);
        double within = 0.0;
        while (within + 1e-15 < outer && remaining > 0.0) {
            const double step = std::min(integrationQuantumS, outer - within);
            const double headM = remaining / columnAreaM2;
            const double flowM3PerS = profile.dischargeCoefficient * apertureAreaM2 *
                                      std::sqrt(2.0 * profile.gravityMPerS2 * headM);
            remaining -= std::min(remaining, flowM3PerS * step);
            within += step;
        }
        elapsed += outer;
    }
    return initialVolumeM3 - remaining;
}

}  // namespace plv
