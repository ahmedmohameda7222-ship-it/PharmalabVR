#include "doctest.h"
#include "plv/abi.h"
#include "json.hpp"

#include <array>
#include <cstdint>
#include <string>
#include <chrono>
#include <thread>
#include <atomic>

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

TEST_CASE("P01_02 only one mutable session and its owner thread may submit") {
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    std::uint64_t original = 0;
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &original) == PLV_OK);
    std::uint64_t second = 0;
    CHECK(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &second) == PLV_BUSY);
    CHECK(second == 0U);
    const auto create = command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}}).dump();
    std::atomic<int> foreignResult{PLV_OK};
    std::atomic<int> foreignStep{PLV_OK};
    std::atomic<int> foreignInput{PLV_OK};
    std::thread foreign([&] {
        foreignResult = plv_submit(original, create.data(), static_cast<std::uint32_t>(create.size()));
        foreignStep = plv_step(original, 0.02, 1'000'000'000ULL);
        const std::string emptyBatch = "[]";
        foreignInput = plv_input_batch(original, emptyBatch.data(), static_cast<std::uint32_t>(emptyBatch.size()));
    });
    foreign.join();
    CHECK(foreignResult == PLV_BUSY);
    CHECK(foreignStep == PLV_BUSY);
    CHECK(foreignInput == PLV_BUSY);
    CHECK(json::parse(read_text(original))["vessels"].empty());
    const auto exported = read_text(original, true);
    std::uint64_t replacement = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &replacement) == PLV_OK);
    CHECK(plv_submit(original, create.data(), static_cast<std::uint32_t>(create.size())) == PLV_BUSY);
    CHECK(plv_submit(replacement, create.data(), static_cast<std::uint32_t>(create.size())) == PLV_OK);
    CHECK(poll(replacement)["accepted"].get<bool>());
    std::uint64_t third = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &third) == PLV_OK);
    std::uint64_t fourth = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &fourth) == PLV_OK);
    std::uint64_t fifth = 0;
    CHECK(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &fifth) == PLV_BUSY);
    CHECK(fifth == 0U);
    CHECK(plv_submit(third, create.data(), static_cast<std::uint32_t>(create.size())) == PLV_BUSY);
    CHECK(plv_submit(fourth, create.data(), static_cast<std::uint32_t>(create.size())) == PLV_OK);
    CHECK(poll(fourth)["accepted"].get<bool>());
    CHECK(plv_destroy(fourth) == PLV_OK);
    CHECK(plv_destroy(third) == PLV_OK);
    CHECK(plv_destroy(replacement) == PLV_OK);
    if (second != 0U) CHECK(plv_destroy(second) == PLV_OK);
    CHECK(plv_destroy(original) == PLV_OK);
}

TEST_CASE("P01_02 repeated real engine create import and destroy releases handles") {
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE}, {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    for (int cycle = 0; cycle < 3; ++cycle) {
        std::uint64_t original = 0;
        REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &original) == PLV_OK);
        submit(original, command("1", "CreateVessel", {{"id", "acid"}, {"capacityM3", 2e-5}}));
        REQUIRE(poll(original)["accepted"].get<bool>());
        submit(original, command("2", "PrepareStock",
                                 {{"vesselId", "acid"}, {"stockKind", "HydrochloricAcid"},
                                  {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}}, {{"acid", "0"}}));
        REQUIRE(poll(original)["accepted"].get<bool>());
        bool current = false;
        for (int attempt = 0; attempt < 200 && !current; ++attempt) {
            REQUIRE(plv_step(original, 0.0, 1'000'000'000ULL) == PLV_OK);
            const auto snapshot = json::parse(read_text(original));
            for (const auto& observation : snapshot["observations"])
                current |= observation.value("vesselId", "") == "acid" && observation.value("freshness", "") == "Current";
            if (!current) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        REQUIRE(current);
        const auto exported = read_text(original, true);
        std::uint64_t imported = 0;
        REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
        CHECK(plv_destroy(original) == PLV_OK);
        bool importedCurrent = false;
        for (int attempt = 0; attempt < 200 && !importedCurrent; ++attempt) {
            REQUIRE(plv_step(imported, 0.0, 1'000'000'000ULL) == PLV_OK);
            const auto snapshot = json::parse(read_text(imported));
            for (const auto& observation : snapshot["observations"])
                importedCurrent |= observation.value("vesselId", "") == "acid" && observation.value("freshness", "") == "Current";
            if (!importedCurrent) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        CHECK(importedCurrent);
        CHECK(plv_destroy(imported) == PLV_OK);
        CHECK(plv_destroy(imported) == PLV_INVALID_HANDLE);
    }
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
                                                   {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}},
                           {{"source", "0"}}));
    CHECK(poll(handle)["accepted"].get<bool>());
    const auto transfer = command("5", "TransferFixed",
                                  {{"sourceInventoryId", "source"}, {"sourceRegion", "Homogeneous"},
                                   {"selection", "HomogeneousAqueousLiquid"},
                                   {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 2e-6}}},
                                   {"captureFractions", {{{"destinationInventoryId", "receiver"}, {"fraction", 1.0}}}},
                                   {"overflowSinkId", "spill"}},
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
    conflict["payload"]["quantity"]["value"] = 1e-6;
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
                                                     {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}},
                             {{"stock", "0"}}));
    poll(original);
    const auto exported = read_text(original, true);

    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    const auto restored = json::parse(read_text(imported));
    const auto previous = json::parse(read_text(original));
    CHECK(restored["vessels"] == previous["vessels"]);
    CHECK(restored["sinks"] == previous["sinks"]);
    CHECK(restored["branchId"] == previous["branchId"]);
    CHECK(restored["hold"]["active"].get<bool>());
    CHECK(restored["hold"]["reason"] == "SessionLoad");
    CHECK(imported != original);
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(original) == PLV_OK);
}

TEST_CASE("C01 PrepareStock requires current revision and setup-only eligibility") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PrepareStock",
                           {{"vesselId", "source"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 4e-6}},
                           {{"source", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("5", "TransferFixed",
                           {{"sourceInventoryId", "source"}, {"sourceRegion", "Homogeneous"},
                            {"selection", "HomogeneousAqueousLiquid"},
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 4e-6}}},
                            {"captureFractions", {{{"destinationInventoryId", "receiver"}, {"fraction", 1.0}}}},
                            {"overflowSinkId", "spill"}},
                           {{"source", "1"}, {"receiver", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());

    const auto refillPayload = json{{"vesselId", "source"}, {"stockKind", "Water"},
                                    {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 4e-6}};
    submit(handle, command("6", "PrepareStock", refillPayload, {{"source", "0"}}));
    CHECK(poll(handle)["code"] == "StaleRevision");
    submit(handle, command("7", "PrepareStock", refillPayload, {{"source", "2"}}));
    CHECK(poll(handle)["code"] == "InvalidPreparation");
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("R05 export preserves command identities and the next ordered sequence") {
    std::uint64_t original = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &original) == PLV_OK);
    const auto first = command("1", "CreateVessel", {{"id", "stock"}, {"capacityM3", 2e-5}});
    const auto second = command("2", "PrepareStock", {{"vesselId", "stock"}, {"stockKind", "HydrochloricAcid"},
                                                        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}},
                                {{"stock", "0"}});
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
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PlaceTool",
                           {{"toolId", "tool-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const std::string valid = R"([{"toolId":"tool-1","sampleSequence":"1","captureMonotonicNs":"100","positionMetres":[0,1,2],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.5,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    CHECK(plv_input_batch(handle, valid.data(), static_cast<std::uint32_t>(valid.size())) == PLV_OK);
    CHECK(plv_input_batch(handle, valid.data(), static_cast<std::uint32_t>(valid.size())) == PLV_INVALID_ARGUMENT);
    const std::string invalid = R"([{"toolId":"tool-1","sampleSequence":"2","captureMonotonicNs":"101","positionMetres":[0,1,2],"rotation":[0,0,0,2],"trackingValid":true,"actuator01":0.5,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    CHECK(plv_input_batch(handle, invalid.data(), static_cast<std::uint32_t>(invalid.size())) == PLV_INVALID_ARGUMENT);
    const std::string corrected = R"([{"toolId":"tool-1","sampleSequence":"2","captureMonotonicNs":"101","positionMetres":[0,1,2],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.5,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    CHECK(plv_input_batch(handle, corrected.data(), static_cast<std::uint32_t>(corrected.size())) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("T02 live ABI input commits bounded conserved delivery on a transport tick") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PrepareStock",
                           {{"vesselId", "source"}, {"stockKind", "SodiumChloride"},
                            {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 4e-6}},
                           {{"source", "0"}})); poll(handle);
    submit(handle, command("5", "PlaceTool",
                           {{"toolId", "burette-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());

    const std::string sample = R"([{"toolId":"burette-1","sampleSequence":"1","captureMonotonicNs":"1000000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":1.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":0.75}]}])";
    REQUIRE(plv_input_batch(handle, sample.data(), static_cast<std::uint32_t>(sample.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1010000000) == PLV_OK);

    const auto snapshot = json::parse(read_text(handle));
    const auto& vessels = snapshot["vessels"];
    const auto source = vessels[0]["id"] == "source" ? vessels[0] : vessels[1];
    const auto receiver = vessels[0]["id"] == "receiver" ? vessels[0] : vessels[1];
    const double sourceVolume = source["inventory"]["researchAdditiveVolumeM3"].get<double>();
    const double receiverVolume = receiver["inventory"]["researchAdditiveVolumeM3"].get<double>();
    const double spillVolume = snapshot["sinks"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>();
    CHECK(sourceVolume < 4e-6);
    CHECK(sourceVolume >= 0.0);
    CHECK(receiverVolume > 0.0);
    CHECK(spillVolume > 0.0);
    CHECK(sourceVolume + receiverVolume + spillVolume == doctest::Approx(4e-6).epsilon(1e-12));
    CHECK(snapshot["hold"]["active"].get<bool>() == false);
    const auto exported = read_text(handle, true);
    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    auto importedSnapshot = json::parse(read_text(imported));
    CHECK(importedSnapshot["hold"]["reason"] == "SessionLoad");
    importedSnapshot["hold"] = snapshot["hold"];
    CHECK(importedSnapshot == snapshot);
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("H05 transport backlog enters time-discontinuity hold without catch-up pour") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("3", "PrepareStock",
                           {{"vesselId", "source"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 4e-6}},
                           {{"source", "0"}})); poll(handle);
    const auto before = json::parse(read_text(handle));
    REQUIRE(plv_step(handle, 0.061, 1000000000) == PLV_OK);
    const auto after = json::parse(read_text(handle));
    CHECK(after["simulationTimeS"] == before["simulationTimeS"]);
    CHECK(after["vessels"] == before["vessels"]);
    CHECK(after["hold"]["active"].get<bool>());
    CHECK(after["hold"]["reason"] == "TimeDiscontinuity");
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("H02 held session requires neutral fresh baseline and explicit Continue") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PrepareStock",
                           {{"vesselId", "source"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 4e-6}},
                           {{"source", "0"}})); poll(handle);
    submit(handle, command("5", "PlaceTool",
                           {{"toolId", "burette-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}})); poll(handle);

    REQUIRE(plv_step(handle, 0.061, 1000000000) == PLV_OK);
    submit(handle, command("6", "Continue"));
    CHECK_FALSE(poll(handle)["accepted"].get<bool>());

    const std::string neutral = R"([{"toolId":"burette-1","sampleSequence":"1","captureMonotonicNs":"1010000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    REQUIRE(plv_input_batch(handle, neutral.data(), static_cast<std::uint32_t>(neutral.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.0, 1020000000) == PLV_OK);
    CHECK(json::parse(read_text(handle))["hold"]["recoveryReady"].get<bool>());
    submit(handle, command("7", "Continue"));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto before = json::parse(read_text(handle));
    REQUIRE(plv_step(handle, 0.02, 1040000000) == PLV_OK);
    const auto after = json::parse(read_text(handle));
    CHECK_FALSE(after["hold"]["active"].get<bool>());
    CHECK(after["vessels"] == before["vessels"]);
    CHECK(after["simulationTimeS"].get<double>() == doctest::Approx(0.02));
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("H02 replacement open sample invalidates held recovery readiness before Continue") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("3", "PlaceTool",
                           {{"toolId", "burette-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}})); poll(handle);
    REQUIRE(plv_step(handle, 0.061, 1000000000) == PLV_OK);
    auto sample = json::parse(R"({"toolId":"burette-1","sampleSequence":"50","captureMonotonicNs":"1010000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[]})");
    auto batch = json::array({sample}).dump();
    REQUIRE(plv_input_batch(handle, batch.data(), static_cast<std::uint32_t>(batch.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.0, 1020000000) == PLV_OK);
    REQUIRE(json::parse(read_text(handle))["hold"]["recoveryReady"].get<bool>());
    CHECK(json::parse(read_text(handle))["inputWatermarks"][0]["sampleSequence"] == "50");
    sample["sampleSequence"] = "51";
    sample["actuator01"] = 1.0;
    batch = json::array({sample}).dump();
    REQUIRE(plv_input_batch(handle, batch.data(), static_cast<std::uint32_t>(batch.size())) == PLV_OK);
    submit(handle, command("4", "Continue"));
    CHECK_FALSE(poll(handle)["accepted"].get<bool>());
    CHECK(json::parse(read_text(handle))["hold"]["active"].get<bool>());
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("H02 focus loss enters native hold until an explicit fresh neutral Continue") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("3", "PlaceTool",
                           {{"toolId", "burette-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}})); poll(handle);
    submit(handle, command("4", "BeginInputHold", {{"reason", "FocusLoss"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    CHECK(json::parse(read_text(handle))["hold"]["reason"] == "FocusLoss");
    submit(handle, command("5", "Continue"));
    CHECK_FALSE(poll(handle)["accepted"].get<bool>());
    const std::string neutral = R"([{"toolId":"burette-1","sampleSequence":"1","captureMonotonicNs":"1010000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[]}])";
    REQUIRE(plv_input_batch(handle, neutral.data(), static_cast<std::uint32_t>(neutral.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.0, 1020000000) == PLV_OK);
    submit(handle, command("6", "Continue"));
    CHECK(poll(handle)["accepted"].get<bool>());
    CHECK_FALSE(json::parse(read_text(handle))["hold"]["active"].get<bool>());
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("H02 imported flowing session requires fresh neutral input before Continue") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("3", "PlaceTool",
                           {{"toolId", "burette-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}})); poll(handle);
    const auto exported = read_text(handle, true);
    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    auto snapshot = json::parse(read_text(imported));
    CHECK(snapshot["hold"]["reason"] == "SessionLoad");
    submit(imported, command("4", "Continue"));
    CHECK_FALSE(poll(imported)["accepted"].get<bool>());
    const std::string neutral = R"([{"toolId":"burette-1","sampleSequence":"1","captureMonotonicNs":"1010000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[]}])";
    REQUIRE(plv_input_batch(imported, neutral.data(), static_cast<std::uint32_t>(neutral.size())) == PLV_OK);
    REQUIRE(plv_step(imported, 0.0, 1020000000) == PLV_OK);
    submit(imported, command("5", "Continue"));
    CHECK(poll(imported)["accepted"].get<bool>());
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("T02 long live pour emits ordered allocated events without filling the native queue") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 0.00005}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 0.00005}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PrepareStock",
                           {{"vesselId", "source"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 0.00004}},
                           {{"source", "0"}})); poll(handle);
    submit(handle, command("5", "PlaceTool",
                           {{"toolId", "burette-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}})); poll(handle);
    auto sample = json::parse(R"({"toolId":"burette-1","sampleSequence":"1","captureMonotonicNs":"1000000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":1.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":0.75}]})");
    std::uint64_t priorEventSequence = 0;
    double allocatedM3 = 0.0;
    for (std::uint64_t tick = 0; tick < 1001; ++tick) {
        sample["sampleSequence"] = std::to_string(tick + 1);
        sample["captureMonotonicNs"] = std::to_string(1000000000ULL + tick * 20000000ULL);
        const auto batch = json::array({sample}).dump();
        REQUIRE(plv_input_batch(handle, batch.data(), static_cast<std::uint32_t>(batch.size())) == PLV_OK);
        REQUIRE(plv_step(handle, 0.02, 1000000000ULL + tick * 20000000ULL) == PLV_OK);
        const auto event = poll(handle);
        REQUIRE(event["type"] == "LiveTransferCommitted");
        CHECK(event["branchId"] == "branch-1");
        const auto eventSequence = std::stoull(event["eventSequence"].get<std::string>());
        CHECK(eventSequence > priorEventSequence);
        priorEventSequence = eventSequence;
        const double captured = event["captured"][0]["quantityM3"].get<double>();
        const double spilled = event["spilledQuantityM3"].get<double>();
        CHECK(captured + spilled == doctest::Approx(event["quantityM3"].get<double>()).epsilon(1e-9));
        allocatedM3 += captured + spilled;
    }
    CHECK(allocatedM3 > 0.0);
    CHECK_FALSE(json::parse(read_text(handle))["hold"]["active"].get<bool>());
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("L02 accepted command history remains importable at the session size boundary") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    const json payload = {{"note", std::string(6U * 1024U * 1024U, 'x')}};
    for (int sequence = 1; sequence <= 2; ++sequence) {
        const auto value = command(std::to_string(sequence), "Pause", payload).dump();
        REQUIRE(plv_submit(handle, value.data(), static_cast<std::uint32_t>(value.size())) == PLV_OK);
        REQUIRE(poll(handle)["accepted"].get<bool>());
    }
    const auto value = command("3", "Pause", payload).dump();
    CHECK(plv_submit(handle, value.data(), static_cast<std::uint32_t>(value.size())) == PLV_BUSY);
    const auto exported = read_text(handle, true);
    CHECK(exported.size() <= 16U * 1024U * 1024U);
    CHECK(json::parse(exported)["nextCommandSequence"] == "3");
    std::uint64_t imported = 0;
    CHECK(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    if (imported != 0) CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("S01 product ABI publishes current pinned IPhreeqc acid observation") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE}, {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "acid"}, {"capacityM3", 0.00002}})); poll(handle);
    submit(handle, command("2", "PrepareStock",
                           {{"vesselId", "acid"}, {"stockKind", "HydrochloricAcid"},
                            {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 0.00001}},
                           {{"acid", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto pending = json::parse(read_text(handle));
    for (const auto& observation : pending.at("observations")) {
        if (observation.at("vesselId") != "acid") continue;
        CHECK(observation.at("freshness") == "Pending");
        CHECK(observation.at("computationState") == "Pending");
    }
    bool current = false;
    for (int attempt = 0; attempt < 200 && !current; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1000000000ULL) == PLV_OK);
        const auto snapshot = json::parse(read_text(handle));
        for (const auto& observation : snapshot.at("observations")) {
            if (observation.at("vesselId") != "acid" || observation.at("observableId") != "pH" ||
                observation.at("freshness") != "Current") continue;
            CHECK(observation.at("support") == "Supported");
            CHECK(observation.at("maturity") == "Research");
            CHECK(observation.at("asOfMaterialRevision") == "1");
            CHECK(observation.at("databaseIdentity") == "minteq.v4.dat-pinned-package-bytes");
            CHECK(observation.at("value").get<double>() == doctest::Approx(1.0).epsilon(0.2));
            current = true;
        }
        if (!current) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    CHECK(current);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P01_01 ten tool science denial is an atomic transport tick") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE}, {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 5e-5}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("2", "CreateSink", {{"id", "spill"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("3", "PrepareStock",
                           {{"vesselId", "source"}, {"stockKind", "SodiumChloride"},
                            {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 5e-5}},
                           {{"source", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    bool current = false;
    for (int attempt = 0; attempt < 200 && !current; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
        const auto snapshot = json::parse(read_text(handle));
        for (const auto& observation : snapshot["observations"])
            current |= observation.value("vesselId", "") == "source" && observation.value("freshness", "") == "Current";
        if (!current) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(current);
    json samples = json::array();
    for (int i = 0; i < 10; ++i) {
        const auto id = "tool-" + std::to_string(i);
        submit(handle, command(std::to_string(4 + i), "PlaceTool",
                               {{"toolId", id}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                                {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                                {"profileRevision", "1"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        samples.push_back({{"toolId", id}, {"sampleSequence", "1"}, {"captureMonotonicNs", "1000000000"},
                           {"positionMetres", {0, 1, 0}}, {"rotation", {0, 0, 0, 1}},
                           {"trackingValid", true}, {"actuator01", 1.0}, {"coordinateFrame", "lab"},
                           {"geometryProfileHash", "burette-50ml-research-v1"}, {"profileRevision", "1"},
                           {"toolRevision", "1"}, {"captureFractions", json::array()}});
    }
    const auto input = samples.dump();
    REQUIRE(plv_input_batch(handle, input.data(), static_cast<std::uint32_t>(input.size())) == PLV_OK);
    const auto before = json::parse(read_text(handle));
    REQUIRE(plv_step(handle, 0.02, 1'010'000'000ULL) == PLV_OK);
    const auto after = json::parse(read_text(handle));
    REQUIRE(after["hold"]["reason"] == "Compute");
    CHECK(after["simulationTimeS"] == before["simulationTimeS"]);
    CHECK(after["eventSequence"] == before["eventSequence"]);
    CHECK(after["vessels"] == before["vessels"]);
    CHECK(after["sinks"] == before["sinks"]);
    std::uint32_t required = 0;
    CHECK(plv_poll(handle, nullptr, 0, &required) == PLV_NO_EVENT);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P01_01 admitted disjoint tools advance simulation once") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateSink", {{"id", "spill"}})); REQUIRE(poll(handle)["accepted"].get<bool>());
    json samples = json::array();
    for (int i = 0; i < 2; ++i) {
        const auto id = "source-" + std::to_string(i);
        const auto tool = "tool-" + std::to_string(i);
        submit(handle, command(std::to_string(2 + i * 3), "CreateVessel", {{"id", id}, {"capacityM3", 5e-5}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        submit(handle, command(std::to_string(3 + i * 3), "PrepareStock",
                               {{"vesselId", id}, {"stockKind", "SodiumChloride"},
                                {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 5e-5}}, {{id, "0"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        submit(handle, command(std::to_string(4 + i * 3), "PlaceTool",
                               {{"toolId", tool}, {"sourceInventoryId", id}, {"overflowSinkId", "spill"},
                                {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                                {"profileRevision", "1"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        samples.push_back({{"toolId", tool}, {"sampleSequence", "1"}, {"captureMonotonicNs", "1000000000"},
                           {"positionMetres", {0, 1, 0}}, {"rotation", {0, 0, 0, 1}},
                           {"trackingValid", true}, {"actuator01", 1.0}, {"coordinateFrame", "lab"},
                           {"geometryProfileHash", "burette-50ml-research-v1"}, {"profileRevision", "1"},
                           {"toolRevision", "1"}, {"captureFractions", json::array()}});
    }
    const auto input = samples.dump();
    REQUIRE(plv_input_batch(handle, input.data(), static_cast<std::uint32_t>(input.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1'010'000'000ULL) == PLV_OK);
    const auto after = json::parse(read_text(handle));
    CHECK(after["simulationTimeS"].get<double>() == doctest::Approx(0.02));
    CHECK(after["eventSequence"] == "7");
    CHECK(after["sinks"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>() > 0.0);
    CHECK(poll(handle)["type"] == "LiveTransferCommitted");
    CHECK(poll(handle)["type"] == "LiveTransferCommitted");
    std::uint32_t required = 0;
    CHECK(plv_poll(handle, nullptr, 0, &required) == PLV_NO_EVENT);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("S01 imported product session resumes pinned chemistry instead of losing its solver") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE}, {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "acid"}, {"capacityM3", 0.00002}})); poll(handle);
    submit(handle, command("2", "PrepareStock",
                           {{"vesselId", "acid"}, {"stockKind", "HydrochloricAcid"},
                            {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 0.00001}},
                           {{"acid", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto exported = read_text(handle, true);
    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    bool current = false;
    std::string lastObservation;
    for (int attempt = 0; attempt < 200 && !current; ++attempt) {
        REQUIRE(plv_step(imported, 0.0, 1000000000ULL) == PLV_OK);
        const auto snapshot = json::parse(read_text(imported));
        lastObservation = snapshot.at("observations").dump();
        for (const auto& observation : snapshot.at("observations")) {
            if (observation.at("vesselId") != "acid" || observation.at("freshness") != "Current") continue;
            CHECK(observation.at("databaseIdentity") == "minteq.v4.dat-pinned-package-bytes");
            CHECK(observation.at("value").get<double>() == doctest::Approx(1.0).epsilon(0.2));
            current = true;
        }
        if (!current) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    INFO(lastObservation);
    CHECK(current);
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("S01 product session solves acid base acetate and mixed inventories from their materials") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE}, {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    const std::array<std::pair<std::string, std::string>, 4> stocks{{
        {"acid", "HydrochloricAcid"}, {"base", "SodiumHydroxide"},
        {"acetate", "SodiumAcetate"}, {"mixed", "AceticAcid"}}};
    std::uint64_t sequence = 1;
    for (const auto& [id, kind] : stocks) {
        submit(handle, command(std::to_string(sequence++), "CreateVessel", {{"id", id}, {"capacityM3", 0.00002}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        submit(handle, command(std::to_string(sequence++), "PrepareStock",
                               {{"vesselId", id}, {"stockKind", kind},
                                {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 0.00001}},
                               {{id, "0"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
    }
    std::unordered_map<std::string, double> current;
    for (int attempt = 0; attempt < 300 && current.size() < stocks.size(); ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1000000000ULL) == PLV_OK);
        const auto snapshot = json::parse(read_text(handle));
        for (const auto& observation : snapshot.at("observations")) {
            if (observation.at("freshness") != "Current") continue;
            REQUIRE(observation.at("asOfMaterialRevision") == "1");
            REQUIRE(observation.at("databaseIdentity") == "minteq.v4.dat-pinned-package-bytes");
            current[observation.at("vesselId").get<std::string>()] = observation.at("value").get<double>();
        }
        if (current.size() < stocks.size()) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(current.size() == stocks.size());
    CHECK(current.at("acid") < current.at("mixed"));
    CHECK(current.at("mixed") < current.at("acetate"));
    CHECK(current.at("acetate") < current.at("base"));
    submit(handle, command(std::to_string(sequence++), "CreateSink", {{"id", "spill"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command(std::to_string(sequence++), "TransferFixed",
                           {{"sourceInventoryId", "mixed"}, {"sourceRegion", "Homogeneous"},
                            {"selection", "HomogeneousAqueousLiquid"},
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 5e-6}}},
                            {"captureFractions", {{{"destinationInventoryId", "acetate"}, {"fraction", 1.0}}}},
                            {"overflowSinkId", "spill"}},
                           {{"mixed", "1"}, {"acetate", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    bool mixedCurrent = false;
    for (int attempt = 0; attempt < 300 && !mixedCurrent; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1000000000ULL) == PLV_OK);
        const auto snapshot = json::parse(read_text(handle));
        for (const auto& observation : snapshot.at("observations")) {
            if (observation.at("vesselId") != "acetate" || observation.at("freshness") != "Current" ||
                observation.at("asOfMaterialRevision") != "2") continue;
            CHECK(observation.at("value").get<double>() < current.at("acetate"));
            mixedCurrent = true;
        }
        if (!mixedCurrent) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    CHECK(mixedCurrent);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("C02 remaining frozen commands mutate atomically and checkpoint restart restores state") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1","initialMode":"Desktop"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "rinse"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "waste"}})); poll(handle);
    submit(handle, command("4", "PrepareStock",
                           {{"vesselId", "source"}, {"stockKind", "SodiumChloride"},
                            {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 4e-6}},
                           {{"source", "0"}})); poll(handle);
    submit(handle, command("5", "PrepareStock",
                           {{"vesselId", "rinse"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 3e-6}},
                           {{"rinse", "0"}})); poll(handle);
    submit(handle, command("6", "PlaceTool",
                           {{"toolId", "burette-1"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "waste"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());

    submit(handle, command("7", "SetActuator", {{"toolId", "burette-1"}, {"actuator01", 0.25}, {"expectedActuatorRevision", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    auto snapshot = json::parse(read_text(handle));
    CHECK(snapshot["tools"][0]["actuator01"].get<double>() == doctest::Approx(0.25));
    CHECK(snapshot["tools"][0]["toolRevision"] == "1");
    CHECK(snapshot["tools"][0]["actuatorRevision"] == "1");

    submit(handle, command("8", "CreateCheckpoint", {{"checkpointId", "before-disposal"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("9", "DisposeContents",
                           {{"sourceInventoryId", "source"}, {"sinkInventoryId", "waste"}},
                           {{"source", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    snapshot = json::parse(read_text(handle));
    const auto disposedSource = snapshot["vessels"][0]["id"] == "source" ? snapshot["vessels"][0] : snapshot["vessels"][1];
    CHECK(disposedSource["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(0.0));
    const auto disposedEventSequence = std::stoull(snapshot["eventSequence"].get<std::string>());

    submit(handle, command("10", "RestartCheckpoint", {{"checkpointId", "before-disposal"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    snapshot = json::parse(read_text(handle));
    const auto restoredSource = snapshot["vessels"][0]["id"] == "source" ? snapshot["vessels"][0] : snapshot["vessels"][1];
    CHECK(restoredSource["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(4e-6));
    CHECK(std::stoull(snapshot["eventSequence"].get<std::string>()) > disposedEventSequence);

    submit(handle, command("11", "RinseTool",
                           {{"toolId", "burette-1"}, {"rinseSourceInventoryId", "rinse"},
                            {"wasteSinkId", "waste"},
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-6}}},
                            {"expectedInventoryRevision", "0"}},
                           {{"rinse", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    snapshot = json::parse(read_text(handle));
    const auto rinsed = snapshot["vessels"][0]["id"] == "rinse" ? snapshot["vessels"][0] : snapshot["vessels"][1];
    CHECK(rinsed["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(2e-6));
    CHECK(snapshot["tools"][0]["toolRevision"] == "1");
    CHECK(snapshot["tools"][0]["inventoryRevision"] == "1");

    submit(handle, command("12", "BeginModeChange", {{"mode", "VR"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    snapshot = json::parse(read_text(handle));
    CHECK(snapshot["mode"] == "VR");
    CHECK(snapshot["hold"]["active"].get<bool>());
    CHECK(snapshot["hold"]["reason"] == "ModeChange");

    const auto exported = read_text(handle, true);
    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    submit(imported, command("13", "RestartCheckpoint", {{"checkpointId", "before-disposal"}}));
    REQUIRE(poll(imported)["accepted"].get<bool>());
    const auto importedSnapshot = json::parse(read_text(imported));
    const auto importedSource = importedSnapshot["vessels"][0]["id"] == "source"
                                    ? importedSnapshot["vessels"][0]
                                    : importedSnapshot["vessels"][1];
    CHECK(importedSource["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(4e-6));
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}
