#include "doctest.h"
#include "plv/state_executor.hpp"

using namespace plv;

TEST_CASE("M01 prepared stocks contain analytical pools without pH authority") {
    const auto hcl = MaterialState::preparedStock("hcl-0.1M", StockKind::HydrochloricAcid, 0.1, 0.000010);
    CHECK(hcl.solventWaterKg == doctest::Approx(0.010));
    CHECK(hcl.chlorideMol == doctest::Approx(0.001));
    CHECK(hcl.sodiumMol == doctest::Approx(0.0));
    CHECK(hcl.acetateMol == doctest::Approx(0.0));
    CHECK(hcl.referenceVolumeM3 == doctest::Approx(0.000010));
}

TEST_CASE("C01 stale touched revision rejects atomically while unrelated changes do not") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.000020).accepted);
    REQUIRE(executor.createVessel("receiver", 0.000020).accepted);
    REQUIRE(executor.createVessel("unrelated", 0.000020).accepted);
    REQUIRE(executor.prepareStock("source", StockKind::HydrochloricAcid, 0.1, 0.000010).accepted);
    const auto expected_source = executor.vessel("source").materialRevision;
    REQUIRE(executor.prepareStock("unrelated", StockKind::SodiumChloride, 0.1, 0.000001).accepted);

    const auto accepted = executor.transferFixed("source", "receiver", "spill", 0.000001, expected_source, 0);
    CHECK(accepted.accepted);
    const auto source_after = executor.vessel("source").inventory.referenceVolumeM3;
    const auto stale = executor.transferFixed("source", "receiver", "spill", 0.000001, expected_source, 1);
    CHECK_FALSE(stale.accepted);
    CHECK(executor.vessel("source").inventory.referenceVolumeM3 == doctest::Approx(source_after));
}

TEST_CASE("M04 empty material never divides by zero") {
    MaterialState empty;
    CHECK(empty.fraction(0.5).referenceVolumeM3 == doctest::Approx(0.0));
    CHECK(empty.isFiniteNonNegative());
}
