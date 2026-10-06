#include "doctest.h"
#include "plv/abi.h"

#include <array>
#include <cstdint>
#include <string>

TEST_CASE("A01 ABI version and handle lifecycle") {
    CHECK(plv_abi_version() == 1U);
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    CHECK(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    CHECK(handle != 0U);
    CHECK(plv_destroy(handle) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_INVALID_HANDLE);
}

TEST_CASE("A01 unsupported schema does not create a handle") {
    std::uint64_t handle = 99;
    const std::string config = R"({"schemaVersion":2,"branchId":"branch-1"})";
    CHECK(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_UNSUPPORTED_VERSION);
    CHECK(handle == 0U);
}

TEST_CASE("A03 snapshot sizing includes NUL and poll sizing is non-consuming") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);

    std::uint32_t required = 0;
    CHECK(plv_snapshot(handle, nullptr, 0, &required) == PLV_BUFFER_TOO_SMALL);
    CHECK(required > 1U);
    std::string snapshot(required, '\0');
    CHECK(plv_snapshot(handle, snapshot.data(), required, &required) == PLV_OK);
    CHECK(snapshot.back() == '\0');

    const std::string command = R"({"schemaVersion":1,"branchId":"branch-1","commandSequence":"1","type":"Pause","expectedMaterialRevisions":{},"payload":{}})";
    REQUIRE(plv_submit(handle, command.data(), static_cast<std::uint32_t>(command.size())) == PLV_OK);
    std::uint32_t event_size = 0;
    CHECK(plv_poll(handle, nullptr, 0, &event_size) == PLV_BUFFER_TOO_SMALL);
    std::array<char, 2> too_small{};
    CHECK(plv_poll(handle, too_small.data(), static_cast<std::uint32_t>(too_small.size()), &event_size) == PLV_BUFFER_TOO_SMALL);
    std::string event(event_size, '\0');
    CHECK(plv_poll(handle, event.data(), event_size, &event_size) == PLV_OK);
    CHECK(plv_poll(handle, nullptr, 0, &event_size) == PLV_NO_EVENT);
    CHECK(event_size == 0U);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("A04 malformed and non-finite JSON inputs reject") {
    std::uint64_t handle = 0;
    const std::string malformed = "{";
    CHECK(plv_create(malformed.data(), static_cast<std::uint32_t>(malformed.size()), &handle) == PLV_INVALID_ARGUMENT);
    CHECK(handle == 0U);
}
