#include "doctest.h"
#include "plv/journal.hpp"
#include <filesystem>
#include <stdexcept>

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
