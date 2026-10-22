#include "doctest.h"
#include "plv/state_executor.hpp"

#include <unordered_map>

using namespace plv;

TEST_CASE("T01 sequential captures allocate seven millilitres and spill three") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.000020).accepted);
    REQUIRE(executor.createVessel("receiver", 0.000007).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::SodiumChloride, 0.1, 0.000010).accepted);

    auto source_revision = executor.vessel("source").materialRevision;
    auto receiver_revision = executor.vessel("receiver").materialRevision;
    REQUIRE(executor.transferFixed("source", "receiver", "spill", 0.000005, source_revision, receiver_revision).accepted);
    source_revision = executor.vessel("source").materialRevision;
    receiver_revision = executor.vessel("receiver").materialRevision;
    REQUIRE(executor.transferFixed("source", "receiver", "spill", 0.000005, source_revision, receiver_revision).accepted);

    CHECK(executor.vessel("source").inventory.referenceVolumeM3 == doctest::Approx(0.0));
    CHECK(executor.vessel("receiver").inventory.referenceVolumeM3 == doctest::Approx(0.000007));
    CHECK(executor.sink("spill").referenceVolumeM3 == doctest::Approx(0.000003));
    CHECK(executor.totalLedger().sodiumMol == doctest::Approx(0.001));
    CHECK(executor.totalLedger().chlorideMol == doctest::Approx(0.001));
}

TEST_CASE("T02 oversized fixed transfer rejects without negative inventory") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.000010).accepted);
    REQUIRE(executor.createVessel("receiver", 0.000010).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::Water, 0.0, 0.000004).accepted);
    const auto before = executor.totalLedger();
    const auto result = executor.transferFixed("source", "receiver", "spill", 0.000005, executor.vessel("source").materialRevision, 0);
    CHECK_FALSE(result.accepted);
    CHECK(executor.totalLedger().referenceVolumeM3 == doctest::Approx(before.referenceVolumeM3));
}

TEST_CASE("T03 overflow preserves proportional represented pools") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.000010).accepted);
    REQUIRE(executor.createVessel("receiver", 0.000002).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::SodiumAcetate, 0.1, 0.000005).accepted);
    REQUIRE(executor.transferFixed("source", "receiver", "spill", 0.000005, executor.vessel("source").materialRevision, 0).accepted);
    CHECK(executor.vessel("receiver").inventory.sodiumMol == doctest::Approx(0.0002));
    CHECK(executor.sink("spill").acetateMol == doctest::Approx(0.0003));
}

TEST_CASE("T03 multiple capture fractions and uncaptured overflow conserve the parcel") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.000020).accepted);
    REQUIRE(executor.createVessel("receiver-a", 0.000010).accepted);
    REQUIRE(executor.createVessel("receiver-b", 0.000010).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::SodiumChloride, 0.1, 0.000010).accepted);

    const TransferFixedRequest request{
        "source",
        TransferQuantityBasis::LiquidVolumeM3,
        0.000010,
        {{"receiver-b", 0.50}, {"receiver-a", 0.25}},
        "spill"};
    const std::unordered_map<std::string, std::uint64_t> revisions{
        {"source", 1}, {"receiver-a", 0}, {"receiver-b", 0}};

    REQUIRE(executor.transferFixed(request, revisions).accepted);
    CHECK(executor.vessel("receiver-a").inventory.referenceVolumeM3 == doctest::Approx(0.0000025));
    CHECK(executor.vessel("receiver-b").inventory.referenceVolumeM3 == doctest::Approx(0.0000050));
    CHECK(executor.sink("spill").referenceVolumeM3 == doctest::Approx(0.0000025));
    CHECK(executor.totalLedger().sodiumMol == doctest::Approx(0.001));
    CHECK(executor.totalLedger().chlorideMol == doctest::Approx(0.001));
}

TEST_CASE("T07 capture fractions above one reject atomically") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.000020).accepted);
    REQUIRE(executor.createVessel("receiver-a", 0.000010).accepted);
    REQUIRE(executor.createVessel("receiver-b", 0.000010).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::Water, 0.0, 0.000010).accepted);
    const auto before = executor.snapshot();

    const TransferFixedRequest request{
        "source",
        TransferQuantityBasis::WaterMassKg,
        0.002,
        {{"receiver-a", 0.60}, {"receiver-b", 0.41}},
        "spill"};
    const std::unordered_map<std::string, std::uint64_t> revisions{
        {"source", 1}, {"receiver-a", 0}, {"receiver-b", 0}};

    const auto outcome = executor.transferFixed(request, revisions);
    CHECK_FALSE(outcome.accepted);
    CHECK(executor.snapshot().eventSequence == before.eventSequence);
    CHECK(executor.vessel("source").inventory.referenceVolumeM3 ==
          doctest::Approx(before.vessels.at("source").inventory.referenceVolumeM3));
}

TEST_CASE("T02 WaterMassKg quantity uses authoritative solvent mass") {
    StateExecutor executor("branch-1");
    REQUIRE(executor.createVessel("source", 0.000010).accepted);
    REQUIRE(executor.createVessel("receiver", 0.000010).accepted);
    REQUIRE(executor.createSink("spill").accepted);
    REQUIRE(executor.prepareStock("source", StockKind::Water, 0.0, 0.000005).accepted);
    const TransferFixedRequest request{
        "source",
        TransferQuantityBasis::WaterMassKg,
        0.002,
        {{"receiver", 1.0}},
        "spill"};

    REQUIRE(executor.transferFixed(request, {{"source", 1}, {"receiver", 0}}).accepted);
    CHECK(executor.vessel("source").inventory.solventWaterKg == doctest::Approx(0.003));
    CHECK(executor.vessel("source").inventory.referenceVolumeM3 == doctest::Approx(0.000003));
    CHECK(executor.vessel("receiver").inventory.solventWaterKg == doctest::Approx(0.002));
    CHECK(executor.vessel("receiver").inventory.referenceVolumeM3 == doctest::Approx(0.000002));
    CHECK(executor.sink("spill").referenceVolumeM3 == doctest::Approx(0.0));
}
