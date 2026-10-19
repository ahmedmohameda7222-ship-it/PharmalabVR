#include "doctest.h"
#include "plv/journal.hpp"
#include <filesystem>
#include <stdexcept>
#include <chrono>
#include <fstream>
#include <iterator>
#include <string>

TEST_CASE("P11_01 checkpoint paths reject relative working-directory traversal") {
    CHECK_THROWS_AS(plv::DurableCheckpointStore("./state.ckpt", 1024),
                    std::invalid_argument);
    CHECK_THROWS_AS(plv::DurableCheckpointStore("../other/state.ckpt", 1024),
                    std::invalid_argument);
    CHECK_THROWS_AS(plv::DurableCheckpointStore("sessions/state.ckpt", 1024),
                    std::invalid_argument);
    const auto absolute = (std::filesystem::temp_directory_path() /
                           "plv-checkpoint-absolute.ckpt").string();
    CHECK_NOTHROW(plv::DurableCheckpointStore(absolute, 1024));
    CHECK_THROWS_AS(plv::DurableCheckpointStore(
        std::filesystem::temp_directory_path().string(), 1024),
        std::invalid_argument);
    CHECK_THROWS_AS(plv::DurableCheckpointStore("/", 1024),
                    std::invalid_argument);

}

TEST_CASE("P11_01 disk WAL refuses relative and directory destinations") {
    CHECK_THROWS_AS(plv::DurableJournal("./receipts.wal", 256),
                    std::invalid_argument);
    CHECK_THROWS_AS(plv::DurableJournal("../other/receipts.wal", 256),
                    std::invalid_argument);
    CHECK_THROWS_AS(plv::DurableJournal(
        std::filesystem::temp_directory_path().string(), 256),
        std::invalid_argument);
    const auto fullPath = (std::filesystem::temp_directory_path() /
                           "plv-journal-absolute.wal").string();
    CHECK_NOTHROW(plv::DurableJournal(fullPath, 256));
}

#ifndef _WIN32
TEST_CASE("P11_01 disk WAL must not follow a symlink to another persisted file") {
    namespace fs = std::filesystem;
    const auto directory = fs::temp_directory_path() /
        ("plv-wal-symlink-" + std::to_string(
          std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(directory));
    const auto target = directory / "protected.txt";
    const auto wal = directory / "receipts.wal";
    {
        std::ofstream stream(target, std::ios::binary);
        REQUIRE(stream.is_open());
        stream << "do-not-corrupt";
    }
    REQUIRE_NOTHROW(fs::create_symlink(target, wal));
    CHECK_THROWS_AS(plv::DurableJournal(wal.string(), 256, 2048),
                    std::invalid_argument);
    REQUIRE(fs::remove(wal));
    plv::DurableJournal constructedBeforeLink(wal.string(), 256, 2048);
    REQUIRE_NOTHROW(fs::create_symlink(target, wal));
    CHECK_THROWS_AS(constructedBeforeLink.recover(), std::runtime_error);
    const auto result = constructedBeforeLink.append("must-not-append");
    CHECK_FALSE(result.accepted);
    std::ifstream read(target, std::ios::binary);
    REQUIRE(read.is_open());
    std::string contents((std::istreambuf_iterator<char>(read)),
                         std::istreambuf_iterator<char>());
    CHECK(contents == "do-not-corrupt");
    fs::remove_all(directory);
}

TEST_CASE("P11_01 checkpoint stage symlink must never truncate unrelated file") {
    namespace fs = std::filesystem;
    const auto base = fs::temp_directory_path() /
        ("plv-ckpt-stage-symlink-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(base));
    const auto protectedFile = base / "unrelated-binary";
    {
        std::ofstream output(protectedFile, std::ios::binary);
        REQUIRE(output.is_open());
        output << "must-remain-byte-identical";
    }
    const auto path = (base / "session.ckpt").string();
    REQUIRE_NOTHROW(fs::create_symlink(protectedFile, path + ".slot0.tmp"));
    plv::DurableCheckpointStore store(path, 1024);
    const auto outcome = store.write("authenticated-checkpoint-bytes");
    CHECK_FALSE(outcome.accepted);
    std::ifstream stream(protectedFile, std::ios::binary);
    REQUIRE(stream.is_open());
    const std::string recovered((std::istreambuf_iterator<char>(stream)),
                                std::istreambuf_iterator<char>());
    CHECK(recovered == "must-remain-byte-identical");
    fs::remove_all(base);
}

TEST_CASE("P11_01 checkpoint load rejects a valid slot aliased by symlink") {
    namespace fs = std::filesystem;
    const auto base = fs::temp_directory_path() /
        ("plv-ckpt-read-symlink-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(base));
    REQUIRE(fs::create_directories(base / "foreign"));
    REQUIRE(fs::create_directories(base / "local"));
    const auto foreignPath = (base / "foreign" / "session.ckpt").string();
    const auto localPath = (base / "local" / "session.ckpt").string();
    plv::DurableCheckpointStore foreign(foreignPath, 1024);
    REQUIRE(foreign.write("foreign-native-snapshot").accepted);
    REQUIRE_NOTHROW(fs::create_symlink(
        foreignPath + ".slot0", localPath + ".slot0"));
    plv::DurableCheckpointStore local(localPath, 1024);
    CHECK_THROWS_AS(local.read(), std::runtime_error);
    fs::remove_all(base);
}


TEST_CASE("P11_01 checkpoint stage hardlink must never truncate unrelated file") {
    namespace fs = std::filesystem;
    const auto base = fs::temp_directory_path() /
        ("plv-ckpt-stage-hardlink-" + std::to_string(
            std::chrono::steady_clock::now().time_since_epoch().count()));
    REQUIRE(fs::create_directories(base));
    const auto protectedFile = base / "protected-science-file";
    {
        std::ofstream output(protectedFile, std::ios::binary);
        REQUIRE(output.is_open());
        output << "this-data-must-not-change";
    }
    const auto path = (base / "session.ckpt").string();
    REQUIRE_NOTHROW(fs::create_hard_link(protectedFile, path + ".slot0.tmp"));
    REQUIRE(fs::hard_link_count(protectedFile) == 2);
    plv::DurableCheckpointStore checkpoint(path, 1024);
    const auto result = checkpoint.write("different-payload");
    CHECK_FALSE(result.accepted);
    std::ifstream input(protectedFile, std::ios::binary);
    REQUIRE(input.is_open());
    const std::string actual((std::istreambuf_iterator<char>(input)),
                             std::istreambuf_iterator<char>());
    CHECK(actual == "this-data-must-not-change");
    fs::remove_all(base);
}

#endif
