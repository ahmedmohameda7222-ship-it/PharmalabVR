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
}
