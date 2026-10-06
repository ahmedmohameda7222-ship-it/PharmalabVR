#include "doctest.h"
#include "plv/state_executor.hpp"

using namespace plv;

TEST_CASE("T01 sequential captures allocate seven millilitres and spill three") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.020).accepted);
    REQUIRE(executor.createVessel("receiver", 0.007).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::SodiumChloride, 0.1, 0.010).accepted);

    auto source_revision = executor.vessel("source").materialRevision;
    auto receiver_revision = executor.vessel("receiver").materialRevision;
    REQUIRE(executor.transferFixed("source", "receiver", "spill", 0.005, source_revision, receiver_revision).accepted);
    source_revision = executor.vessel("source").materialRevision;
    receiver_revision = executor.vessel("receiver").materialRevision;
    REQUIRE(executor.transferFixed("source", "receiver", "spill", 0.005, source_revision, receiver_revision).accepted);

    CHECK(executor.vessel("source").inventory.referenceVolumeM3 == doctest::Approx(0.0));
    CHECK(executor.vessel("receiver").inventory.referenceVolumeM3 == doctest::Approx(0.007));
    CHECK(executor.sink("spill").referenceVolumeM3 == doctest::Approx(0.003));
    CHECK(executor.totalLedger().sodiumMol == doctest::Approx(0.001));
    CHECK(executor.totalLedger().chlorideMol == doctest::Approx(0.001));
}

TEST_CASE("T02 oversized fixed transfer rejects without negative inventory") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.010).accepted);
    REQUIRE(executor.createVessel("receiver", 0.010).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::Water, 0.0, 0.004).accepted);
    const auto before = executor.totalLedger();
    const auto result = executor.transferFixed("source", "receiver", "spill", 0.005, executor.vessel("source").materialRevision, 0);
    CHECK_FALSE(result.accepted);
    CHECK(executor.totalLedger().referenceVolumeM3 == doctest::Approx(before.referenceVolumeM3));
}

TEST_CASE("T03 overflow preserves proportional represented pools") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.010).accepted);
    REQUIRE(executor.createVessel("receiver", 0.002).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::SodiumAcetate, 0.1, 0.005).accepted);
    REQUIRE(executor.transferFixed("source", "receiver", "spill", 0.005, executor.vessel("source").materialRevision, 0).accepted);
    CHECK(executor.vessel("receiver").inventory.sodiumMol == doctest::Approx(0.0002));
    CHECK(executor.sink("spill").acetateMol == doctest::Approx(0.0003));
}
