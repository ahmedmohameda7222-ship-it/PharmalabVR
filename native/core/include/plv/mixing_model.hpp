#pragma once

#include <string>
#include <vector>

namespace plv {

struct MixingRegion {
    double fraction = 0.0;
    double referenceVolumeM3 = 0.0;
};

struct MixingResult {
    bool supported = false;
    std::string model;
    std::string reason;
    std::vector<MixingRegion> regions;
};

class MixingModel {
public:
    static MixingResult homogeneous(double referenceVolumeM3);
    static MixingResult localResearch(double referenceVolumeM3, const std::string& tracerCalibrationId);
};

}  // namespace plv
