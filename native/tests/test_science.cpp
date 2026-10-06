#include "doctest.h"
#include "plv/ideal_reference.hpp"

#include <cmath>
#include <stdexcept>

using namespace plv;

TEST_CASE("S01 ideal charge root handles water strong acid and strong base") {
    const auto water = solveIdealAqueous({});
    CHECK(water.pH == doctest::Approx(7.0).epsilon(1e-10));
    const auto acid = solveIdealAqueous({0.0, 0.1, 0.0});
    CHECK(acid.pH == doctest::Approx(1.0).epsilon(1e-10));
    const auto base = solveIdealAqueous({0.1, 0.0, 0.0});
    CHECK(base.pH == doctest::Approx(13.0).epsilon(1e-10));
    CHECK(std::abs(base.chargeResidualMolPerL) < 1e-12);
}

TEST_CASE("S01 acetate total participates in one full charge balance") {
    const auto aceticAcid = solveIdealAqueous({0.0, 0.0, 0.1});
    CHECK(aceticAcid.pH == doctest::Approx(2.8753).epsilon(0.001));
    CHECK(std::abs(aceticAcid.chargeResidualMolPerL) < 1e-12);
}

TEST_CASE("S05 invalid scientific inputs reject rather than fabricate an observation") {
    IdealAqueousInput invalid;
    invalid.sodiumMolPerL = -1.0;
    CHECK_THROWS_AS(solveIdealAqueous(invalid), std::invalid_argument);
}
