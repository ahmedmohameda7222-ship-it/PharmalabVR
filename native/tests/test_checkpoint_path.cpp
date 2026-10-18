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
#endif
