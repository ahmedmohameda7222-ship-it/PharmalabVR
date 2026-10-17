#pragma once

#include <cstdint>
#include <string>

namespace plv {

enum class StockKind {
    Water,
    HydrochloricAcid,
    SodiumHydroxide,
    SodiumChloride,
    AceticAcid,
    SodiumAcetate
};

struct MaterialState {
    double solventWaterKg = 0.0;
    double sodiumMol = 0.0;
    double chlorideMol = 0.0;
    double acetateMol = 0.0;
    double referenceVolumeM3 = 0.0;
    std::string preparationId;
    std::string provenanceId;

    static MaterialState preparedStock(
        const std::string& preparationId,
        StockKind kind,
        double concentrationMolPerL,
        double referenceVolumeM3);

    bool isFiniteNonNegative() const;
    MaterialState fraction(double requestedVolumeM3) const;
    void add(const MaterialState& other);
    bool subtract(const MaterialState& other);
};

}  // namespace plv
