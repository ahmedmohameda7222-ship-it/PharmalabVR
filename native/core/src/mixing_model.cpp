#include "plv/mixing_model.hpp"

#include <cmath>

namespace plv {

MixingResult MixingModel::homogeneous(double referenceVolumeM3) {
    if (!std::isfinite(referenceVolumeM3) || referenceVolumeM3 < 0.0) {
        return {false, "homogeneous", "invalid reference volume", {}};
    }
    return {true, "homogeneous", "single-region supported profile", {{1.0, referenceVolumeM3}}};
}

MixingResult MixingModel::localResearch(double referenceVolumeM3, const std::string& tracerCalibrationId) {
    if (!std::isfinite(referenceVolumeM3) || referenceVolumeM3 < 0.0) {
        return {false, "local-research", "invalid reference volume", {}};
    }
    if (tracerCalibrationId.empty()) {
        return {false, "local-research", "local mixing requires independently supplied tracer calibration", {}};
    }
    return {false, "local-research", "calibrated local provider is not installed", {}};
}

}  // namespace plv
