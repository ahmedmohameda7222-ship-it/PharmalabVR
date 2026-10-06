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
    scheduler.markMaterialRevision("B", 2);
    CHECK(scheduler.observation("A", "pH")->freshness == ObservationFreshness::Current);
    scheduler.markMaterialRevision("A", 2);
    CHECK(scheduler.observation("A", "pH")->freshness == ObservationFreshness::Stale);
}

TEST_CASE("O02 older completion never replaces newer per-observable result") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 8});
    REQUIRE(scheduler.offer({"A", "pH", 1, 0, 0.0}).accepted);
    auto oldRequest = scheduler.takeNext();
    REQUIRE(oldRequest.has_value());
    REQUIRE(scheduler.offer({"A", "pH", 2, 1, 0.0}).accepted);
    auto newRequest = scheduler.takeNext();
    REQUIRE(newRequest.has_value());
    scheduler.complete(*newRequest, {ObservationSupport::Supported, ObservationMaturity::Research, true, 8.0});
    scheduler.complete(*oldRequest, {ObservationSupport::Supported, ObservationMaturity::Research, true, 3.0});
    CHECK(scheduler.observation("A", "pH")->solvedRevision == 2);
    CHECK(scheduler.observation("A", "pH")->value == doctest::Approx(8.0));
}

TEST_CASE("O03 bounded pending slots remain round-robin fair") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 2});
    REQUIRE(scheduler.offer({"A", "pH", 1, 0, 0.0}).accepted);
    REQUIRE(scheduler.offer({"B", "pH", 1, 0, 0.0}).accepted);
    CHECK_FALSE(scheduler.offer({"C", "pH", 1, 0, 0.0}).accepted);
    CHECK(scheduler.takeNext()->vesselId == "A");
    CHECK(scheduler.takeNext()->vesselId == "B");
}

TEST_CASE("O04 O05 precommit budgets count gross circulation and hold before violation") {
    ObservationScheduler scheduler({2e-7, 500000000ULL, 8});
    scheduler.markSolved("A", 4, 1000000000ULL);
    CHECK(scheduler.admitTransport("A", 1e-7, 1200000000ULL).accepted);
    scheduler.recordGrossTransport("A", 1e-7);
    CHECK_FALSE(scheduler.admitTransport("A", 1.1e-7, 1250000000ULL).accepted);
    CHECK_FALSE(scheduler.admitTransport("A", 0.0, 1600000000ULL).accepted);
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
