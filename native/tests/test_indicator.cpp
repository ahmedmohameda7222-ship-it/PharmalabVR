#include "doctest.h"
#include "plv/indicator_model.hpp"

using namespace plv;

TEST_CASE("I01 indicator without sourced preparation stays unsupported") {
    const auto observation = observeIndicator({"phenolphthalein", 1e-6, 1e-4, 9.4, ""}, 9.4);
    CHECK(observation.support == IndicatorSupport::Unsupported);
    CHECK_FALSE(observation.reason.empty());
}

TEST_CASE("I02 theoretical fractions use quantity solvent and identified pKa without claiming color") {
    const IndicatorPreparation preparation{"synthetic-indicator", 2e-6, 2e-4, 7.0, "synthetic-test-only"};
    const auto acidic = observeIndicator(preparation, 6.0);
    const auto midpoint = observeIndicator(preparation, 7.0);
    const auto basic = observeIndicator(preparation, 8.0);
    REQUIRE(acidic.support == IndicatorSupport::Approximate);
    CHECK(acidic.acidFraction > midpoint.acidFraction);
    CHECK(midpoint.acidFraction == doctest::Approx(0.5));
    CHECK(basic.baseFraction > midpoint.baseFraction);
    CHECK(midpoint.indicatorMol == doctest::Approx(2e-6));
    CHECK(midpoint.solventKg == doctest::Approx(2e-4));
    CHECK_FALSE(midpoint.hasValidatedOpticalColor);
}

TEST_CASE("I03 multiple preparations remain separate and cannot become last-added color") {
    const auto observations = observeIndicators({
        {"indicator-a", 1e-6, 1e-4, 4.0, "source-a"},
        {"indicator-b", 3e-6, 2e-4, 9.0, "source-b"}}, 7.0);
    REQUIRE(observations.size() == 2);
    CHECK(observations[0].indicatorId == "indicator-a");
    CHECK(observations[1].indicatorId == "indicator-b");
    CHECK(observations[0].indicatorMol + observations[1].indicatorMol == doctest::Approx(4e-6));
}
