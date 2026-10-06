#include "doctest.h"
#include "plv/ideal_reference.hpp"
#include "plv/solver.hpp"

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

TEST_CASE("S02 pinned IPhreeqc engine solves water strong acid and strong base") {
    IPhreeqcAdapter adapter(PLV_MINTEQ_DATABASE, "minteq.v4.dat-pinned-package-bytes");
    REQUIRE(adapter.initialized());
    const auto water = adapter.solve({0.01, 1e-5, 0.0, 0.0, 0.0, 25.0});
    const auto acid = adapter.solve({0.01, 1e-5, 0.0, 1e-6, 0.0, 25.0});
    const auto base = adapter.solve({0.01, 1e-5, 1e-6, 0.0, 0.0, 25.0});
    REQUIRE(water.succeeded);
    REQUIRE(acid.succeeded);
    REQUIRE(base.succeeded);
    CHECK(water.pH == doctest::Approx(7.0).epsilon(0.02));
    CHECK(acid.pH == doctest::Approx(1.0).epsilon(0.20));
    CHECK(base.pH == doctest::Approx(13.0).epsilon(0.03));
    CHECK_FALSE(water.engineVersion.empty());
    CHECK(water.databaseIdentity == "minteq.v4.dat-pinned-package-bytes");
}

TEST_CASE("S03 solver result does not depend on diverse prior request") {
    IPhreeqcAdapter adapter(PLV_MINTEQ_DATABASE, "minteq");
    REQUIRE(adapter.initialized());
    const SolveRequest acid{0.01, 1e-5, 0.0, 1e-7, 0.0, 25.0};
    const auto first = adapter.solve(acid);
    REQUIRE(adapter.solve({0.01, 1e-5, 1e-6, 0.0, 0.0, 25.0}).succeeded);
    const auto second = adapter.solve(acid);
    REQUIRE(first.succeeded);
    REQUIRE(second.succeeded);
    CHECK(second.pH == doctest::Approx(first.pH).epsilon(1e-12));
}

TEST_CASE("S05 engine adapter rejects invalid requests and missing database") {
    IPhreeqcAdapter adapter(PLV_MINTEQ_DATABASE, "minteq");
    REQUIRE(adapter.initialized());
    const auto invalid = adapter.solve({0.0, 0.0, -1.0, 0.0, 0.0, 25.0});
    CHECK_FALSE(invalid.succeeded);
    CHECK_FALSE(invalid.error.empty());
    IPhreeqcAdapter missing("definitely-missing-database.dat", "missing");
    CHECK_FALSE(missing.initialized());
    CHECK_FALSE(missing.solve({0.01, 1e-5, 0.0, 0.0, 0.0, 25.0}).succeeded);
}
