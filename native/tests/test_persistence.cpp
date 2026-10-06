#include "doctest.h"
#include "plv/journal.hpp"

using namespace plv;

TEST_CASE("R03 journal truncation recovers only complete checksummed records") {
    Journal journal(1024);
    REQUIRE(journal.append("transfer-1").accepted);
    REQUIRE(journal.append("transfer-2").accepted);
    auto bytes = journal.bytes();
    bytes.resize(bytes.size() - 3);
    const auto recovered = Journal::recover(bytes, 1024);
    REQUIRE(recovered.records.size() == 1);
    CHECK(recovered.records[0] == "transfer-1");
    CHECK(recovered.truncatedTail);
}

TEST_CASE("R02 corrupt and oversized journal records fail closed") {
    Journal journal(32);
    CHECK_FALSE(journal.append(std::string(33, 'x')).accepted);
    REQUIRE(journal.append("valid").accepted);
    auto bytes = journal.bytes();
    bytes.back() ^= 0x01;
    const auto recovered = Journal::recover(bytes, 32);
    CHECK(recovered.records.empty());
    CHECK(recovered.corruptTail);
}

TEST_CASE("R05 receipt identities survive memory cache eviction through durable journal") {
    ReceiptIdentityIndex identities(2);
    CHECK(identities.accept("branch", 1, "hash-a"));
    CHECK(identities.accept("branch", 2, "hash-b"));
    CHECK(identities.accept("branch", 3, "hash-c"));
    CHECK_FALSE(identities.accept("branch", 1, "hash-a"));
    CHECK_FALSE(identities.accept("branch", 1, "different"));
    CHECK(identities.cachedCount() <= 2);
}
