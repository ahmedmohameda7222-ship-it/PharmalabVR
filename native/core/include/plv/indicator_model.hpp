#pragma once

#include <string>
#include <vector>

namespace plv {

enum class IndicatorSupport { Unsupported, Approximate };

struct IndicatorPreparation {
    std::string id;
    double indicatorMol = 0.0;
    double solventKg = 0.0;
    double pKa = 0.0;
    std::string provenance;
};

struct IndicatorObservation {
    std::string indicatorId;
    IndicatorSupport support = IndicatorSupport::Unsupported;
    double indicatorMol = 0.0;
    double solventKg = 0.0;
    double acidFraction = 0.0;
    double baseFraction = 0.0;
    bool hasValidatedOpticalColor = false;
    std::string reason;
};

IndicatorObservation observeIndicator(const IndicatorPreparation& preparation, double pH);
std::vector<IndicatorObservation> observeIndicators(const std::vector<IndicatorPreparation>& preparations, double pH);

}  // namespace plv
