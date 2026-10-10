#include "doctest.h"
#include "plv/mixing_model.hpp"

using namespace plv;

TEST_CASE("homogeneous profile exposes one explicit region") {
    const auto result = MixingModel::homogeneous(0.0001);
    CHECK(result.supported);
    REQUIRE(result.regions.size() == 1);
    CHECK(result.regions[0].fraction == doctest::Approx(1.0));
    CHECK(result.regions[0].referenceVolumeM3 == doctest::Approx(0.0001));
}

TEST_CASE("local mixing remains unsupported without tracer calibration") {
    const auto result = MixingModel::localResearch(0.0001, "");
    CHECK_FALSE(result.supported);
    CHECK_FALSE(result.reason.empty());
    CHECK(result.regions.empty());
}
