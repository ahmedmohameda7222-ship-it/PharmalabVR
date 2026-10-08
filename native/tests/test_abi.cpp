#include "doctest.h"
#include "plv/abi.h"
#include "json.hpp"

#include <array>
#include <cstdint>
#include <string>

namespace {
using json = nlohmann::json;

std::string read_text(std::uint64_t handle, bool exported = false) {
    std::uint32_t required = 0;
    const auto sizing = exported ? plv_export(handle, nullptr, 0, &required)
                                 : plv_snapshot(handle, nullptr, 0, &required);
    REQUIRE(sizing == PLV_BUFFER_TOO_SMALL);
    std::string value(required, '\0');
    const auto copied = exported ? plv_export(handle, value.data(), required, &required)
                                 : plv_snapshot(handle, value.data(), required, &required);
    REQUIRE(copied == PLV_OK);
    value.pop_back();
    return value;
}

void submit(std::uint64_t handle, const json& command) {
    const auto value = command.dump();
    REQUIRE(plv_submit(handle, value.data(), static_cast<std::uint32_t>(value.size())) == PLV_OK);
}

json poll(std::uint64_t handle) {
    std::uint32_t required = 0;
    REQUIRE(plv_poll(handle, nullptr, 0, &required) == PLV_BUFFER_TOO_SMALL);
    std::string value(required, '\0');
    REQUIRE(plv_poll(handle, value.data(), required, &required) == PLV_OK);
    value.pop_back();
    return json::parse(value);
}

json command(const std::string& sequence, const std::string& type, json payload = json::object(), json revisions = json::object()) {
    return {{"schemaVersion", 1}, {"branchId", "branch-1"}, {"commandSequence", sequence},
            {"type", type}, {"expectedMaterialRevisions", revisions}, {"payload", payload}};
}
}  // namespace

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

TEST_CASE("A02 discrete ABI commands mutate the native authority and duplicate safely") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);

    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 2e-5}}));
    CHECK(poll(handle)["accepted"].get<bool>());
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-5}}));
    CHECK(poll(handle)["accepted"].get<bool>());
    submit(handle, command("3", "CreateSink", {{"id", "spill"}}));
    CHECK(poll(handle)["accepted"].get<bool>());
    submit(handle, command("4", "PrepareStock", {{"vesselId", "source"}, {"stockKind", "SodiumChloride"},
                                                   {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}}));
    CHECK(poll(handle)["accepted"].get<bool>());
    const auto transfer = command("5", "TransferFixed",
                                  {{"sourceId", "source"}, {"receiverId", "receiver"}, {"spillSinkId", "spill"},
                                   {"requestedVolumeM3", 2e-6}},
                                  {{"source", "1"}, {"receiver", "0"}});
    submit(handle, transfer);
    CHECK(poll(handle)["accepted"].get<bool>());
    submit(handle, transfer);
    CHECK(poll(handle)["code"] == "Accepted");

    const auto snapshot = json::parse(read_text(handle));
    const auto source = snapshot["vessels"][0]["id"] == "source" ? snapshot["vessels"][0] : snapshot["vessels"][1];
    const auto receiver = snapshot["vessels"][0]["id"] == "receiver" ? snapshot["vessels"][0] : snapshot["vessels"][1];
    CHECK(source["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(8e-6));
    CHECK(receiver["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(2e-6));

    auto conflict = transfer;
    conflict["payload"]["requestedVolumeM3"] = 1e-6;
    submit(handle, conflict);
    CHECK(poll(handle)["code"] == "CommandIdentityConflict");
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("R01 ABI export imports complete state into a fresh validated context") {
    std::uint64_t original = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &original) == PLV_OK);
    submit(original, command("1", "CreateVessel", {{"id", "stock"}, {"capacityM3", 2e-5}}));
    poll(original);
    submit(original, command("2", "PrepareStock", {{"vesselId", "stock"}, {"stockKind", "HydrochloricAcid"},
                                                     {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}}));
    poll(original);
    const auto exported = read_text(original, true);

    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    CHECK(json::parse(read_text(imported)) == json::parse(read_text(original)));
    CHECK(imported != original);
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(original) == PLV_OK);
}

TEST_CASE("R05 export preserves command identities and the next ordered sequence") {
    std::uint64_t original = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &original) == PLV_OK);
    const auto first = command("1", "CreateVessel", {{"id", "stock"}, {"capacityM3", 2e-5}});
    const auto second = command("2", "PrepareStock", {{"vesselId", "stock"}, {"stockKind", "HydrochloricAcid"},
                                                        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}});
    submit(original, first);
    CHECK(poll(original)["accepted"].get<bool>());
    submit(original, second);
    CHECK(poll(original)["accepted"].get<bool>());

    const auto exported = json::parse(read_text(original, true));
    CHECK(exported["nextCommandSequence"] == "3");
    REQUIRE(exported["commandReceipts"].is_array());
    CHECK(exported["commandReceipts"].size() == 2U);

    const auto serialized = exported.dump();
    std::uint64_t imported = 0;
    REQUIRE(plv_import(serialized.data(), static_cast<std::uint32_t>(serialized.size()), &imported) == PLV_OK);

    submit(imported, second);
    CHECK(poll(imported)["code"] == "Accepted");
    auto conflictingSecond = second;
    conflictingSecond["payload"]["concentrationMolPerL"] = 0.2;
    submit(imported, conflictingSecond);
    CHECK(poll(imported)["code"] == "CommandIdentityConflict");
    submit(imported, command("3", "Pause"));
    CHECK(poll(imported)["accepted"].get<bool>());
    CHECK(json::parse(read_text(imported))["paused"].get<bool>());

    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(original) == PLV_OK);
}

TEST_CASE("S05 import rejects unknown material pools instead of silently discarding them") {
    std::uint64_t original = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &original) == PLV_OK);
    submit(original, command("1", "CreateVessel", {{"id", "stock"}, {"capacityM3", 2e-5}}));
    REQUIRE(poll(original)["accepted"].get<bool>());

    auto exported = json::parse(read_text(original, true));
    exported["vessels"][0]["inventory"]["carbonateMol"] = 0.001;
    const auto serialized = exported.dump();
    std::uint64_t imported = 99;
    CHECK(plv_import(serialized.data(), static_cast<std::uint32_t>(serialized.size()), &imported) == PLV_INVALID_ARGUMENT);
    CHECK(imported == 0U);

    CHECK(plv_destroy(original) == PLV_OK);
}

TEST_CASE("A04 input batches validate geometry atomically and reject stale samples") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    const std::string valid = R"([{"toolId":"tool-1","sampleSequence":"1","captureMonotonicNs":"100","positionMetres":[0,1,2],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.5,"geometryProfileHash":"profile-1"}])";
    CHECK(plv_input_batch(handle, valid.data(), static_cast<std::uint32_t>(valid.size())) == PLV_OK);
    CHECK(plv_input_batch(handle, valid.data(), static_cast<std::uint32_t>(valid.size())) == PLV_INVALID_ARGUMENT);
    const std::string invalid = R"([{"toolId":"tool-2","sampleSequence":"1","captureMonotonicNs":"101","positionMetres":[0,1,2],"rotation":[0,0,0,2],"trackingValid":true,"actuator01":0.5,"geometryProfileHash":"profile-1"}])";
    CHECK(plv_input_batch(handle, invalid.data(), static_cast<std::uint32_t>(invalid.size())) == PLV_INVALID_ARGUMENT);
    const std::string corrected = R"([{"toolId":"tool-2","sampleSequence":"1","captureMonotonicNs":"101","positionMetres":[0,1,2],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.5,"geometryProfileHash":"profile-1"}])";
    CHECK(plv_input_batch(handle, corrected.data(), static_cast<std::uint32_t>(corrected.size())) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}
