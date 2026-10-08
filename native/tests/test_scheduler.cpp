#include "doctest.h"
#include "plv/scheduler.hpp"

using namespace plv;

TEST_CASE("O01 unrelated revisions do not stale vessel observations") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 8});
    REQUIRE(scheduler.offer({"A", "pH", 1, 0, 0.0}).accepted);
    const auto request = scheduler.takeNext();
    REQUIRE(request.has_value());
    scheduler.complete(*request, {ObservationSupport::Supported, ObservationMaturity::Research, true, 7.0});
    CHECK(scheduler.observation("A", "pH")->freshness == ObservationFreshness::Current);
    scheduler.markMaterialRevision("B", 2, 10);
    CHECK(scheduler.observation("A", "pH")->freshness == ObservationFreshness::Current);
    scheduler.markMaterialRevision("A", 2, 20);
    CHECK(scheduler.observation("A", "pH")->freshness == ObservationFreshness::Stale);
}

TEST_CASE("O02 older completion never replaces newer per-observable result") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 8});
    REQUIRE(scheduler.offer({"A", "pH", 1, 0, 0.0}).accepted);
    auto oldRequest = scheduler.takeNext();
    REQUIRE(oldRequest.has_value());
    REQUIRE(scheduler.offer({"A", "pH", 2, 1, 0.0}).accepted);
    scheduler.complete(*oldRequest, {ObservationSupport::Supported, ObservationMaturity::Research, true, 3.0});
    auto newRequest = scheduler.takeNext();
    REQUIRE(newRequest.has_value());
    scheduler.complete(*newRequest, {ObservationSupport::Supported, ObservationMaturity::Research, true, 8.0});
    scheduler.complete(*oldRequest, {ObservationSupport::Supported, ObservationMaturity::Research, true, 3.0});
    CHECK(scheduler.observation("A", "pH")->solvedRevision == 2);
    CHECK(scheduler.observation("A", "pH")->value == doctest::Approx(8.0));
}

TEST_CASE("O02 completion behind the authoritative material revision remains stale") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 8});
    REQUIRE(scheduler.offer({"A", "pH", 2, 10, 0.0}).accepted);
    const auto request = scheduler.takeNext();
    REQUIRE(request.has_value());

    scheduler.markMaterialRevision("A", 3, 20);
    scheduler.complete(*request, {ObservationSupport::Supported, ObservationMaturity::Research, true, 7.2});

    const auto* observation = scheduler.observation("A", "pH");
    REQUIRE(observation != nullptr);
    CHECK(observation->solvedRevision == 2);
    CHECK(observation->freshness == ObservationFreshness::Stale);
}

TEST_CASE("O03 bounded pending slots remain round-robin fair") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 2});
    REQUIRE(scheduler.offer({"A", "pH", 1, 0, 0.0}).accepted);
    REQUIRE(scheduler.offer({"B", "pH", 1, 0, 0.0}).accepted);
    CHECK_FALSE(scheduler.offer({"C", "pH", 1, 0, 0.0}).accepted);
    CHECK(scheduler.takeNext()->vesselId == "A");
    CHECK(scheduler.takeNext()->vesselId == "B");
}

TEST_CASE("O03 each vessel has one replaceable pending slot without starving peers") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 2});
    REQUIRE(scheduler.offer({"A", "pH", 1, 10, 0.0}).accepted);
    const auto inFlightA = scheduler.takeNext();
    REQUIRE(inFlightA.has_value());

    REQUIRE(scheduler.offer({"A", "pH", 2, 20, 0.0}).accepted);
    REQUIRE(scheduler.offer({"A", "pH", 3, 30, 0.0}).accepted);
    REQUIRE(scheduler.offer({"B", "pH", 1, 40, 0.0}).accepted);
    CHECK_FALSE(scheduler.offer({"C", "pH", 1, 50, 0.0}).accepted);

    scheduler.complete(*inFlightA, {ObservationSupport::Supported, ObservationMaturity::Research, true, 7.0});
    const auto next = scheduler.takeNext();
    REQUIRE(next.has_value());
    CHECK(next->vesselId == "B");
    scheduler.complete(*next, {ObservationSupport::Supported, ObservationMaturity::Research, true, 7.0});

    const auto coalescedA = scheduler.takeNext();
    REQUIRE(coalescedA.has_value());
    CHECK(coalescedA->vesselId == "A");
    CHECK(coalescedA->materialRevision == 3);
}

TEST_CASE("O04 O05 precommit budgets count gross circulation and hold before violation") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 8});
    scheduler.markSolved("A", 4, 1000000000ULL);
    CHECK(scheduler.admitTransport("A", 1e-7, 1200000000ULL).accepted);
    scheduler.recordGrossTransport("A", 1e-7, 1200000000ULL);
    CHECK_FALSE(scheduler.admitTransport("A", 1.1e-7, 1250000000ULL).accepted);
    CHECK_FALSE(scheduler.admitTransport("A", 0.0, 1800000000ULL).accepted);
}

TEST_CASE("O05 an idle unchanged observation does not age before the first dependency change") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 8});
    scheduler.markSolved("A", 4, 1000000000ULL);

    CHECK(scheduler.admitTransport("A", 1e-7, 10000000000ULL).accepted);
    scheduler.recordGrossTransport("A", 1e-7, 10000000000ULL);
    CHECK(scheduler.admitTransport("A", 0.0, 10400000000ULL).accepted);
    CHECK_FALSE(scheduler.admitTransport("A", 0.0, 10600000000ULL).accepted);
}

TEST_CASE("H02 H05 resume requires neutral fresh baseline and explicit continue") {
    HoldState hold;
    hold.enter(HoldCause::TimeDiscontinuity);
    CHECK_FALSE(hold.canContinue());
    hold.observeNeutralInput(true);
    hold.refreshBaseline();
    CHECK_FALSE(hold.canContinue());
    hold.requestContinue();
    CHECK(hold.canContinue());
    hold.release();
    CHECK_FALSE(hold.active());
}
