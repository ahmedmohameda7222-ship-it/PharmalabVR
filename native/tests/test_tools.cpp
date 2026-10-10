#include "doctest.h"
#include "plv/tool_models.hpp"

#include <algorithm>
#include <array>
#include <cmath>

using namespace plv;

TEST_CASE("T04 rinse moves contamination through represented tip inventory") {
    ToolParcelState tool;
    tool.tip = MaterialState::preparedStock("acid", StockKind::HydrochloricAcid, 0.1, 1e-7);
    const auto rinse = MaterialState::preparedStock("rinse", StockKind::Water, 0.0, 2e-7);
    const auto waste = tool.rinse(rinse, 5e-8);

    CHECK(waste.referenceVolumeM3 == doctest::Approx(2.5e-7));
    CHECK(waste.chlorideMol > 0.0);
    CHECK(tool.tip.referenceVolumeM3 == doctest::Approx(5e-8));
    CHECK(tool.tip.chlorideMol > 0.0);
}

TEST_CASE("T05 valve close preserves and lands one in-flight parcel") {
    ToolParcelState tool;
    tool.inFlight = MaterialState::preparedStock("salt", StockKind::SodiumChloride, 0.1, 5e-8);
    const auto landed = tool.landInFlight();
    CHECK(landed.referenceVolumeM3 == doctest::Approx(5e-8));
    CHECK(tool.inFlight.referenceVolumeM3 == doctest::Approx(0.0));
    CHECK(tool.landInFlight().referenceVolumeM3 == doctest::Approx(0.0));
}

TEST_CASE("T07 calibrated geometry rejects invalid frame quaternion profile and fractions") {
    GeometryCaptureSample sample{"lab", "burette-50ml-research-v1", 9, {0.0, 0.0, 0.0, 1.0}, {0.7, 0.3}};
    CHECK(validateGeometrySample(sample, "lab", "burette-50ml-research-v1", 9).accepted);
    sample.captureFractions = {0.8, 0.3};
    CHECK_FALSE(validateGeometrySample(sample, "lab", "burette-50ml-research-v1", 9).accepted);
    sample.captureFractions = {1.0};
    sample.orientation = {0.0, 0.0, 0.0, 2.0};
    CHECK_FALSE(validateGeometrySample(sample, "lab", "burette-50ml-research-v1", 9).accepted);
}

TEST_CASE("T08 research burette trace converges for 10 20 and 40 millisecond ticks") {
    const auto profile = BuretteProfile::researchDefault();
    const auto reference = simulateBuretteDelivery(profile, 4e-5, 0.75, 1.0, 0.001);
    for (const double tick : std::array<double, 3>{0.010, 0.020, 0.040}) {
        const auto delivered = simulateBuretteDelivery(profile, 4e-5, 0.75, 1.0, tick);
        const double tolerance = std::max(1e-8, 0.005 * reference);
        CHECK(std::abs(delivered - reference) <= tolerance);
    }
}
