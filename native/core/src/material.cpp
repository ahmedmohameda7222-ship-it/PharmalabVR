#include "plv/material.hpp"

#include <algorithm>
#include <cmath>

namespace plv {

namespace {
bool valid(double value) {
    return std::isfinite(value) && value >= 0.0;
}

void clampTiny(double& value) {
    if (std::abs(value) < 1e-18) {
        value = 0.0;
    }
}
}  // namespace

MaterialState MaterialState::preparedStock(
    const std::string& preparation,
    StockKind kind,
    double concentrationMolPerL,
    double volumeM3) {
    MaterialState state;
    if (!valid(concentrationMolPerL) || !valid(volumeM3)) {
        return state;
    }
    state.solventWaterKg = volumeM3 * 1000.0;
    state.referenceVolumeM3 = volumeM3;
    state.preparationId = preparation;
    state.provenanceId = "aqueous-six-research-v1";
    const double amountMol = concentrationMolPerL * volumeM3 * 1000.0;
    switch (kind) {
        case StockKind::Water:
            break;
        case StockKind::HydrochloricAcid:
            state.chlorideMol = amountMol;
            break;
        case StockKind::SodiumHydroxide:
            state.sodiumMol = amountMol;
            break;
        case StockKind::SodiumChloride:
            state.sodiumMol = amountMol;
            state.chlorideMol = amountMol;
            break;
        case StockKind::AceticAcid:
            state.acetateMol = amountMol;
            break;
        case StockKind::SodiumAcetate:
            state.sodiumMol = amountMol;
            state.acetateMol = amountMol;
            break;
    }
    return state;
}

bool MaterialState::isFiniteNonNegative() const {
    return valid(solventWaterKg) && valid(sodiumMol) && valid(chlorideMol) &&
           valid(acetateMol) && valid(referenceVolumeM3);
}

MaterialState MaterialState::fraction(double requestedVolumeM3) const {
    MaterialState result;
    if (!valid(requestedVolumeM3) || referenceVolumeM3 <= 0.0 || requestedVolumeM3 <= 0.0) {
        return result;
    }
    const double ratio = std::min(1.0, requestedVolumeM3 / referenceVolumeM3);
    result.solventWaterKg = solventWaterKg * ratio;
    result.sodiumMol = sodiumMol * ratio;
    result.chlorideMol = chlorideMol * ratio;
    result.acetateMol = acetateMol * ratio;
    result.referenceVolumeM3 = referenceVolumeM3 * ratio;
    result.preparationId = preparationId;
    result.provenanceId = provenanceId;
    return result;
}

void MaterialState::add(const MaterialState& other) {
    solventWaterKg += other.solventWaterKg;
    sodiumMol += other.sodiumMol;
    chlorideMol += other.chlorideMol;
    acetateMol += other.acetateMol;
    referenceVolumeM3 += other.referenceVolumeM3;
    if (preparationId.empty()) {
        preparationId = other.preparationId;
    } else if (!other.preparationId.empty() && preparationId != other.preparationId) {
        preparationId = "mixture";
    }
    if (provenanceId.empty()) {
        provenanceId = other.provenanceId;
    }
}

bool MaterialState::subtract(const MaterialState& other) {
    constexpr double tolerance = 1e-15;
    if (other.solventWaterKg > solventWaterKg + tolerance ||
        other.sodiumMol > sodiumMol + tolerance ||
        other.chlorideMol > chlorideMol + tolerance ||
        other.acetateMol > acetateMol + tolerance ||
        other.referenceVolumeM3 > referenceVolumeM3 + tolerance) {
        return false;
    }
    solventWaterKg -= other.solventWaterKg;
    sodiumMol -= other.sodiumMol;
    chlorideMol -= other.chlorideMol;
    acetateMol -= other.acetateMol;
    referenceVolumeM3 -= other.referenceVolumeM3;
    clampTiny(solventWaterKg);
    clampTiny(sodiumMol);
    clampTiny(chlorideMol);
    clampTiny(acetateMol);
    clampTiny(referenceVolumeM3);
    return true;
}

}  // namespace plv
