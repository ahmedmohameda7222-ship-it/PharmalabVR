#include "doctest.h"
#include "plv/journal.hpp"

#include <chrono>
#include <filesystem>
#include <fstream>
#include <string>
#include <vector>

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

TEST_CASE("R03 disk journal restarts recover ordered fsynced records") {
    namespace fs = std::filesystem;
    const auto directory = fs::temp_directory_path() /
        ("plv-durable-journal-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(directory));
    const auto path = directory / "session.wal";
    {
        DurableJournal first(path.string(), 256, 2048);
        CHECK(first.append("cmd-1").accepted);
        CHECK(first.append("cmd-2").accepted);
    }
    {
        DurableJournal reopened(path.string(), 256, 2048);
        const auto recovered = reopened.recover();
        REQUIRE(recovered.records.size() == 2);
        CHECK(recovered.records[0] == "cmd-1");
        CHECK(recovered.records[1] == "cmd-2");
        CHECK_FALSE(recovered.truncatedTail);
        CHECK_FALSE(recovered.corruptTail);
        CHECK(reopened.append("cmd-3").accepted);
        CHECK(reopened.recover().records.size() == 3);
    }
    fs::remove_all(directory);
}

TEST_CASE("R03 interrupted disk journal tail discards only unconfirmed bytes") {
    namespace fs = std::filesystem;
    const auto directory = fs::temp_directory_path() /
        ("plv-durable-truncate-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(directory));
    const auto path = directory / "session.wal";
    DurableJournal first(path.string(), 256, 2048);
    REQUIRE(first.append("committed-1").accepted);
    REQUIRE(first.append("uncommitted-2").accepted);
    fs::resize_file(path, fs::file_size(path) - 4);
    {
        DurableJournal restarted(path.string(), 256, 2048);
        const auto before = restarted.recover();
        REQUIRE(before.records.size() == 1);
        CHECK(before.records[0] == "committed-1");
        CHECK(before.truncatedTail);
        CHECK(restarted.append("committed-3").accepted);
        const auto after = restarted.recover();
        REQUIRE(after.records.size() == 2);
        CHECK(after.records[0] == "committed-1");
        CHECK(after.records[1] == "committed-3");
        CHECK_FALSE(after.truncatedTail);
    }
    fs::remove_all(directory);
}

TEST_CASE("R02 disk journal rejects CRC corruption and over-capacity append without write") {
    namespace fs = std::filesystem;
    const auto directory = fs::temp_directory_path() /
        ("plv-durable-corrupt-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(directory));
    const auto path = directory / "session.wal";
    DurableJournal disk(path.string(), 32, 64);
    REQUIRE(disk.append("ok").accepted);
    const auto originalSize = fs::file_size(path);
    CHECK_FALSE(disk.append(std::string(60, 'x')).accepted);
    CHECK(fs::file_size(path) == originalSize);
    {
        std::fstream stream(path, std::ios::in | std::ios::out | std::ios::binary);
        REQUIRE(stream.is_open());
        stream.seekp(-1, std::ios::end);
        stream.put('X');
    }
    const auto corruptedSize = fs::file_size(path);
    const auto corrupt = disk.recover();
    CHECK(corrupt.corruptTail);
    const auto result = disk.append("must-reject");
    CHECK_FALSE(result.accepted);
    CHECK(result.code == "JournalCorrupt");
    CHECK(fs::file_size(path) == corruptedSize);
    fs::remove_all(directory);
}

TEST_CASE("R05 durable Native receipt identities reject replays across process restarts") {
    namespace fs = std::filesystem;
    const auto directory = fs::temp_directory_path() /
        ("plv-receipt-index-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(directory));
    const auto path = directory / "receipts.wal";
    {
        ReceiptIdentityIndex first(2, path.string());
        CHECK(first.accept("branch-A", 1, std::string(64, 'a')));
        CHECK(first.accept("branch-A", 2, std::string(64, 'b')));
        CHECK(first.accept("branch-A", 3, std::string(64, 'c')));
        CHECK(first.cachedCount() <= 2);
    }
    {
        ReceiptIdentityIndex restarted(2, path.string());
        const auto originalLength = fs::file_size(path);
        CHECK_FALSE(restarted.accept("branch-A", 1, std::string(64, 'a')));
        CHECK_FALSE(restarted.accept("branch-A", 1, std::string(64, 'f')));
        CHECK(fs::file_size(path) == originalLength);
        CHECK(restarted.accept("branch-B", 1, std::string(64, 'a')));
        CHECK(restarted.accept("branch-A", 4, std::string(64, 'd')));
        CHECK(restarted.cachedCount() <= 2);
    }
    {
        ReceiptIdentityIndex secondRestart(2, path.string());
        CHECK_FALSE(secondRestart.accept("branch-B", 1, std::string(64, 'a')));
        CHECK_FALSE(secondRestart.accept("branch-A", 4, std::string(64, 'd')));
    }
    fs::remove_all(directory);
}

TEST_CASE("R05 torn receipt does not turn into a durable replay identity") {
    namespace fs = std::filesystem;
    const auto directory = fs::temp_directory_path() /
        ("plv-receipt-torn-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(directory));
    const auto path = directory / "receipts.wal";
    {
        ReceiptIdentityIndex first(2, path.string());
        CHECK(first.accept("branch-A", 1, std::string(64, 'a')));
        CHECK(first.accept("branch-A", 2, std::string(64, 'b')));
    }
    fs::resize_file(path, fs::file_size(path) - 3);
    {
        ReceiptIdentityIndex recovered(2, path.string());
        CHECK_FALSE(recovered.accept("branch-A", 1, std::string(64, 'a')));
        CHECK(recovered.accept("branch-A", 2, std::string(64, 'b')));
    }
    {
        ReceiptIdentityIndex afterRepair(2, path.string());
        CHECK_FALSE(afterRepair.accept("branch-A", 2, std::string(64, 'b')));
    }
    fs::remove_all(directory);
}

TEST_CASE("R04 Native dirty checkpoint requires earlier of wall or simulation threshold") {
    DirtyCheckpointPolicy policy(30'000'000'000ULL, 30.0);
    CHECK_FALSE(policy.dirty());
    CHECK_FALSE(policy.needsFlush(10'000'000'000ULL, 5.0, false));
    policy.markDirty(10'000'000'000ULL, 5.0);
    CHECK(policy.dirty());
    policy.markDirty(35'000'000'000ULL, 30.0);
    CHECK_FALSE(policy.needsFlush(39'000'000'000ULL, 34.9, false));
    CHECK(policy.needsFlush(40'000'000'000ULL, 34.9, false));
    // R01/R04: a failed disk flush may not clear dirty state or defer retry.
    policy.acknowledgeFlush(false);
    CHECK(policy.dirty());
    CHECK(policy.needsFlush(40'000'000'001ULL, 34.9, false));
    policy.acknowledgeFlush(true);
    CHECK_FALSE(policy.dirty());
    CHECK_FALSE(policy.needsFlush(41'000'000'000ULL, 36.0, false));
    policy.markDirty(100'000'000'000ULL, 20.0);
    CHECK_FALSE(policy.needsFlush(101'000'000'000ULL, 49.9, false));
    CHECK(policy.needsFlush(101'000'000'000ULL, 50.0, false));
}

TEST_CASE("R04 dirty Native state flushes before pause and on time discontinuity") {
    DirtyCheckpointPolicy policy(30'000'000'000ULL, 30.0);
    policy.markDirty(50'000'000'000ULL, 12.0);
    CHECK(policy.needsFlush(50'000'000'001ULL, 12.001, true));
    CHECK(policy.needsFlush(49'000'000'999ULL, 12.001, false));
    CHECK(policy.needsFlush(50'000'000'001ULL, 11.99, false));
    CHECK(policy.dirty());
    policy.acknowledgeFlush(true);
    CHECK_FALSE(policy.needsFlush(60'000'000'000ULL, 42.0, true));
}

TEST_CASE("P11_01 Native two-slot checkpoint retains previous committed generation") {
    namespace fs = std::filesystem;
    const auto dir = fs::temp_directory_path() /
        ("plv-checkpoint-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(dir));
    const auto path = (dir / "state.ckpt").string();
    {
        DurableCheckpointStore checkpoint(path, 4096);
        CHECK_FALSE(checkpoint.read().has_value());
        REQUIRE(checkpoint.write(R"({"generation":1,"value":"first"})").accepted);
        CHECK(checkpoint.read().value() == R"({"generation":1,"value":"first"})");
        REQUIRE(checkpoint.write(R"({"generation":2,"value":"second"})").accepted);
        CHECK(checkpoint.read().value() == R"({"generation":2,"value":"second"})");
        // A rejected oversized write must not retire a previously confirmed slot.
        CHECK_FALSE(checkpoint.write(std::string(4097, 'x')).accepted);
        CHECK(checkpoint.read().value() == R"({"generation":2,"value":"second"})");
    }
    {
        DurableCheckpointStore restarted(path, 4096);
        REQUIRE(restarted.read().has_value());
        CHECK(restarted.read().value() == R"({"generation":2,"value":"second"})");
        // Corrupt the newest slot; only the independently validated older
        // generation is allowed as a recorded recovery candidate.
        auto newest = fs::path(path + ".slot1");
        REQUIRE(fs::exists(newest));
        std::fstream damaged(newest, std::ios::in | std::ios::out | std::ios::binary);
        REQUIRE(damaged.is_open());
        damaged.seekp(-1, std::ios::end);
        damaged.put('!');
        damaged.close();
        CHECK(restarted.read().value() == R"({"generation":1,"value":"first"})");
        REQUIRE(restarted.write(R"({"generation":3,"value":"third"})").accepted);
        CHECK(restarted.read().value() == R"({"generation":3,"value":"third"})");
    }
    fs::remove_all(dir);
}

TEST_CASE("P11_01 both damaged checkpoint slots fail closed") {
    namespace fs = std::filesystem;
    const auto dir = fs::temp_directory_path() /
        ("plv-checkpoint-bad-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(dir));
    const auto path = (dir / "state.ckpt").string();
    DurableCheckpointStore checkpoint(path, 4096);
    REQUIRE(checkpoint.write(R"({"generation":1})").accepted);
    REQUIRE(checkpoint.write(R"({"generation":2})").accepted);
    for (const auto& suffix : {".slot0", ".slot1"}) {
        std::ofstream damaged(path + suffix, std::ios::binary | std::ios::trunc);
        damaged << "corrupted";
    }
    CHECK_THROWS_AS(checkpoint.read(), std::runtime_error);
    CHECK_FALSE(checkpoint.write(R"({"generation":3})").accepted);
    fs::remove_all(dir);
}
