#include "plv/indicator_model.hpp"

#include <cmath>

namespace plv {

IndicatorObservation observeIndicator(const IndicatorPreparation& preparation, double pH) {
    IndicatorObservation result;
    result.indicatorId = preparation.id;
    result.indicatorMol = preparation.indicatorMol;
    result.solventKg = preparation.solventKg;
    if (preparation.id.empty() || preparation.provenance.empty() || !std::isfinite(preparation.indicatorMol) ||
        !std::isfinite(preparation.solventKg) || !std::isfinite(preparation.pKa) || !std::isfinite(pH) ||
        preparation.indicatorMol <= 0.0 || preparation.solventKg < 0.0 || preparation.pKa < -2.0 ||
        preparation.pKa > 16.0 || pH < -2.0 || pH > 16.0) {
        result.reason = "identified quantity, solvent, finite pKa, and provenance are required";
        return result;
    }
    const double ratio = std::pow(10.0, pH - preparation.pKa);
    result.acidFraction = 1.0 / (1.0 + ratio);
    result.baseFraction = ratio / (1.0 + ratio);
    result.support = IndicatorSupport::Approximate;
    result.reason = "theoretical Henderson-Hasselbalch fraction only; no validated optical color";
    return result;
}

std::vector<IndicatorObservation> observeIndicators(
    const std::vector<IndicatorPreparation>& preparations,
    double pH) {
    std::vector<IndicatorObservation> results;
    results.reserve(preparations.size());
    for (const auto& preparation : preparations) results.push_back(observeIndicator(preparation, pH));
    return results;
}

}  // namespace plv
