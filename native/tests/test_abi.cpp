#include "doctest.h"
#include "plv/abi.h"
#include "json.hpp"

#include <array>
#include <cstdint>
#include <string>
#include <chrono>
#include <thread>
#include <atomic>
#include <cmath>
#include <unordered_map>

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
    const auto pause = command("1", "Pause").dump();
    CHECK(plv_submit(replacement, pause.data(), static_cast<std::uint32_t>(pause.size())) == PLV_OK);
    CHECK(poll(replacement)["accepted"].get<bool>());
    std::uint64_t third = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &third) == PLV_OK);
    std::uint64_t fourth = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &fourth) == PLV_OK);
    std::uint64_t fifth = 0;
    CHECK(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &fifth) == PLV_BUSY);
    CHECK(fifth == 0U);
    CHECK(plv_submit(third, create.data(), static_cast<std::uint32_t>(create.size())) == PLV_BUSY);
    CHECK(plv_submit(fourth, pause.data(), static_cast<std::uint32_t>(pause.size())) == PLV_OK);
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

TEST_CASE("P02_03 rinse transfers contaminated tip parcel into waste and retains modeled residual") {
    std::uint64_t original = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &original) == PLV_OK);
    submit(original, command("1", "CreateVessel", {{"id", "acid"}, {"capacityM3", 2e-5}})); REQUIRE(poll(original)["accepted"]);
    submit(original, command("2", "CreateVessel", {{"id", "rinse"}, {"capacityM3", 2e-5}})); REQUIRE(poll(original)["accepted"]);
    submit(original, command("3", "CreateSink", {{"id", "waste"}})); REQUIRE(poll(original)["accepted"]);
    submit(original, command("4", "PrepareStock", {{"vesselId", "acid"}, {"stockKind", "HydrochloricAcid"},
        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-5}}, {{"acid", "0"}})); REQUIRE(poll(original)["accepted"]);
    submit(original, command("5", "PrepareStock", {{"vesselId", "rinse"}, {"stockKind", "Water"},
        {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 1e-5}}, {{"rinse", "0"}})); REQUIRE(poll(original)["accepted"]);
    submit(original, command("6", "PlaceTool", {{"toolId", "tool"}, {"sourceInventoryId", "acid"},
        {"overflowSinkId", "waste"}, {"coordinateFrame", "lab"},
        {"geometryProfileHash", "burette-50ml-research-v1"}, {"profileRevision", "1"}})); REQUIRE(poll(original)["accepted"]);

    // A controlled, ledger-balanced imported parcel exercises the rinse path while
    // live tip filling is implemented separately. Import must never create matter.
    auto saved = json::parse(read_text(original, true));
    for (auto& vessel : saved["vessels"]) {
        if (vessel["id"] != "acid") continue;
        vessel["inventory"]["solventWaterKg"] = 0.009;
        vessel["inventory"]["chlorideMol"] = 0.0009;
        vessel["inventory"]["researchAdditiveVolumeM3"] = 9e-6;
    }
    auto& tip = saved["tools"][0]["tipInventory"];
    tip["solventWaterKg"] = 0.001;
    tip["chlorideMol"] = 0.0001;
    tip["researchAdditiveVolumeM3"] = 1e-6;
    tip["preparationId"] = "acid";
    tip["provenanceId"] = "aqueous-six-research-v1";
    const auto serialized = saved.dump();
    std::uint64_t imported = 0;
    REQUIRE(plv_import(serialized.data(), static_cast<std::uint32_t>(serialized.size()), &imported) == PLV_OK);
    const auto ledgerBefore = json::parse(read_text(imported))["materialLedger"];
    CHECK(std::abs(ledgerBefore["chlorideMol"].get<double>() - 0.001) < 1e-12);
    CHECK(std::abs(ledgerBefore["researchAdditiveVolumeM3"].get<double>() - 20e-6) < 1e-12);
    const std::string neutral = R"([{"toolId":"tool","sampleSequence":"1","captureMonotonicNs":"1010000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[]}])";
    REQUIRE(plv_input_batch(imported, neutral.data(), static_cast<std::uint32_t>(neutral.size())) == PLV_OK);
    REQUIRE(plv_step(imported, 0.0, 1020000000) == PLV_OK);
    submit(imported, command("7", "Continue", {{"monotonicNowNs", "1020000000"}}));
    REQUIRE(poll(imported)["accepted"]);
    submit(imported, command("8", "RinseTool", {{"toolId", "tool"}, {"rinseSourceInventoryId", "rinse"},
        {"wasteSinkId", "waste"}, {"expectedInventoryRevision", "0"},
        {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-6}}}}, {{"rinse", "1"}}));
    REQUIRE(poll(imported)["accepted"]);
    const auto after = json::parse(read_text(imported));
    CHECK(std::abs(after["materialLedger"]["chlorideMol"].get<double>() - 0.001) < 1e-12);
    CHECK(std::abs(after["materialLedger"]["solventWaterKg"].get<double>() - 0.020) < 1e-12);
    const auto retained = after["tools"][0]["tipInventory"];
    CHECK(std::abs(retained["researchAdditiveVolumeM3"].get<double>() - 5e-8) < 1e-12);
    double chlorideTotal = retained["chlorideMol"].get<double>();
    double waterTotal = retained["solventWaterKg"].get<double>();
    double volumeTotal = retained["researchAdditiveVolumeM3"].get<double>();
    for (const auto& vessel : after["vessels"]) {
        chlorideTotal += vessel["inventory"]["chlorideMol"].get<double>();
        waterTotal += vessel["inventory"]["solventWaterKg"].get<double>();
        volumeTotal += vessel["inventory"]["researchAdditiveVolumeM3"].get<double>();
    }
    for (const auto& sink : after["sinks"]) {
        chlorideTotal += sink["inventory"]["chlorideMol"].get<double>();
        waterTotal += sink["inventory"]["solventWaterKg"].get<double>();
        volumeTotal += sink["inventory"]["researchAdditiveVolumeM3"].get<double>();
    }
    CHECK(std::abs(chlorideTotal - 0.001) < 1e-12);
    CHECK(std::abs(waterTotal - 0.020) < 1e-12);
    CHECK(std::abs(volumeTotal - 20e-6) < 1e-12);
    CHECK(after["sinks"][0]["inventory"]["chlorideMol"].get<double>() > 0.0);
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(original) == PLV_OK);
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

TEST_CASE("P03_02 PrepareStock cannot create fresh material after the lab leaves setup") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "initial"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "PrepareStock", {{"vesselId", "initial"}, {"stockKind", "Water"},
                           {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 1e-6}}, {{"initial", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("3", "Continue"));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("4", "CreateVessel", {{"id", "late"}, {"capacityM3", 1e-5}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto before = json::parse(read_text(handle));
    submit(handle, command("5", "PrepareStock", {{"vesselId", "late"}, {"stockKind", "SodiumHydroxide"},
                           {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-6}}, {{"late", "0"}}));
    CHECK(poll(handle)["code"] == "PreparationClosed");
    const auto after = json::parse(read_text(handle));
    CHECK(after["materialLedger"] == before["materialLedger"]);
    CHECK(after["vessels"] == before["vessels"]);
    auto exported = json::parse(read_text(handle, true));
    exported["setupClosed"] = false;
    const auto forged = exported.dump();
    std::uint64_t rejectedHandle = 0;
    CHECK(plv_import(forged.data(), static_cast<std::uint32_t>(forged.size()), &rejectedHandle) == PLV_INVALID_ARGUMENT);
    CHECK(rejectedHandle == 0U);
    exported.erase("setupClosed");
    const auto legacy = exported.dump();
    std::uint64_t legacyImported = 0;
    REQUIRE(plv_import(legacy.data(), static_cast<std::uint32_t>(legacy.size()), &legacyImported) == PLV_OK);
    CHECK(json::parse(read_text(legacyImported))["setupClosed"] == true);
    CHECK(plv_destroy(legacyImported) == PLV_OK);
    exported["setupClosed"] = true;
    const auto saved = exported.dump();
    std::uint64_t imported = 0;
    REQUIRE(plv_import(saved.data(), static_cast<std::uint32_t>(saved.size()), &imported) == PLV_OK);
    CHECK(json::parse(read_text(imported))["setupClosed"] == true);
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);

    std::uint64_t stepped = 0;
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &stepped) == PLV_OK);
    submit(stepped, command("1", "CreateVessel", {{"id", "late"}, {"capacityM3", 1e-5}})); poll(stepped);
    REQUIRE(plv_step(stepped, 0.02, 1'000'000'000ULL) == PLV_OK);
    CHECK(json::parse(read_text(stepped))["setupClosed"] == true);
    submit(stepped, command("2", "PrepareStock", {{"vesselId", "late"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 1e-6}}, {{"late", "0"}}));
    CHECK(poll(stepped)["code"] == "PreparationClosed");
    CHECK(plv_destroy(stepped) == PLV_OK);
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
    submit(handle, command("6", "CreateVessel", {{"id", "z-rinse"}, {"capacityM3", 2e-6}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("7", "PrepareStock",
                           {{"vesselId", "z-rinse"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 1e-6}},
                           {{"z-rinse", "0"}}));
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
    const auto& tool = snapshot["tools"][0];
    const double tipVolume = tool["tipInventory"]["researchAdditiveVolumeM3"].get<double>();
    const double flightVolume = tool["inFlightInventory"]["researchAdditiveVolumeM3"].get<double>();
    CHECK(tipVolume > 0.0);
    CHECK(flightVolume > 0.0);
    CHECK(sourceVolume + receiverVolume + spillVolume + tipVolume + flightVolume ==
          doctest::Approx(4e-6).epsilon(1e-12));
    CHECK(snapshot["materialLedger"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(5e-6));
    CHECK(snapshot["hold"]["active"].get<bool>() == false);
    const auto exported = read_text(handle, true);
    const std::string closed = R"([{"toolId":"burette-1","sampleSequence":"2","captureMonotonicNs":"1020000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":0.75}]}])";
    REQUIRE(plv_input_batch(handle, closed.data(), static_cast<std::uint32_t>(closed.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1030000000) == PLV_OK);
    const auto closedSnapshot = json::parse(read_text(handle));
    CHECK(closedSnapshot["tools"][0]["inFlightInventory"]["researchAdditiveVolumeM3"].get<double>() ==
          doctest::Approx(0.0));
    CHECK(closedSnapshot["tools"][0]["tipInventory"]["researchAdditiveVolumeM3"].get<double>() ==
          doctest::Approx(tipVolume));
    const auto eventAfterLand = closedSnapshot["eventSequence"];
    const auto sinkAfterLand = closedSnapshot["sinks"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>();
    CHECK(sinkAfterLand > spillVolume);
    const auto closedReceiver = closedSnapshot["vessels"][0]["id"] == "receiver"
                                    ? closedSnapshot["vessels"][0] : closedSnapshot["vessels"][1];
    CHECK(closedReceiver["inventory"]["researchAdditiveVolumeM3"].get<double>() > receiverVolume);
    REQUIRE(plv_step(handle, 0.02, 1050000000) == PLV_OK);
    const auto repeated = json::parse(read_text(handle));
    CHECK(repeated["eventSequence"] == eventAfterLand);
    CHECK(repeated["sinks"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>() ==
          doctest::Approx(sinkAfterLand));
    CHECK(poll(handle)["type"] == "LiveTransferCommitted");
    CHECK(poll(handle)["type"] == "InFlightLanded");
    const auto beforeRinse = json::parse(read_text(handle));
    submit(handle, command("8", "RinseTool",
                           {{"toolId", "burette-1"}, {"rinseSourceInventoryId", "z-rinse"},
                            {"wasteSinkId", "spill"},
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-6}}},
                            {"expectedInventoryRevision", repeated["tools"][0]["inventoryRevision"]}},
                           {{"z-rinse", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto afterRinse = json::parse(read_text(handle));
    CHECK(afterRinse["sinks"][0]["inventory"]["chlorideMol"].get<double>() >
          beforeRinse["sinks"][0]["inventory"]["chlorideMol"].get<double>());
    CHECK(afterRinse["materialLedger"]["chlorideMol"].get<double>() ==
          doctest::Approx(beforeRinse["materialLedger"]["chlorideMol"].get<double>()));
    CHECK(afterRinse["materialLedger"]["researchAdditiveVolumeM3"].get<double>() ==
          doctest::Approx(beforeRinse["materialLedger"]["researchAdditiveVolumeM3"].get<double>()));
    CHECK(plv_destroy(handle) == PLV_OK);
    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    auto importedSnapshot = json::parse(read_text(imported));
    CHECK(importedSnapshot["hold"]["reason"] == "SessionLoad");
    importedSnapshot["hold"] = snapshot["hold"];
    CHECK(importedSnapshot == snapshot);
    CHECK(plv_destroy(imported) == PLV_OK);
}

TEST_CASE("P03_02 retained tip contaminates the next stock delivered through the live tool") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PrepareStock", {{"vesselId", "source"}, {"stockKind", "HydrochloricAcid"},
                           {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 4e-6}}, {{"source", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("5", "PlaceTool", {{"toolId", "tool"}, {"sourceInventoryId", "source"},
                           {"overflowSinkId", "spill"}, {"coordinateFrame", "lab"},
                           {"geometryProfileHash", "burette-50ml-research-v1"}, {"profileRevision", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("6", "CreateVessel", {{"id", "z-base-stock"}, {"capacityM3", 1e-5}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("7", "PrepareStock", {{"vesselId", "z-base-stock"}, {"stockKind", "SodiumHydroxide"},
                           {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 4e-6}},
                           {{"z-base-stock", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const std::string acidInput = R"([{"toolId":"tool","sampleSequence":"1","captureMonotonicNs":"1000000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":1.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    REQUIRE(plv_input_batch(handle, acidInput.data(), static_cast<std::uint32_t>(acidInput.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1'010'000'000ULL) == PLV_OK);
    REQUIRE(poll(handle)["type"] == "LiveTransferCommitted");
    const std::string closedInput = R"([{"toolId":"tool","sampleSequence":"2","captureMonotonicNs":"1020000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    REQUIRE(plv_input_batch(handle, closedInput.data(), static_cast<std::uint32_t>(closedInput.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1'030'000'000ULL) == PLV_OK);
    REQUIRE(poll(handle)["type"] == "InFlightLanded");
    const auto beforeSwitch = json::parse(read_text(handle));
    const auto& beforeSource = beforeSwitch["vessels"][0]["id"] == "source" ?
        beforeSwitch["vessels"][0] : beforeSwitch["vessels"][1];
    const auto& beforeReceiver = beforeSwitch["vessels"][0]["id"] == "receiver" ?
        beforeSwitch["vessels"][0] : beforeSwitch["vessels"][1];
    const double chlorideBefore = beforeReceiver["inventory"]["chlorideMol"].get<double>();
    const double tipChloride = beforeSwitch["tools"][0]["tipInventory"]["chlorideMol"].get<double>();
    REQUIRE(tipChloride > 0.0);
    submit(handle, command("8", "DisposeContents", {{"sourceInventoryId", "source"},
                           {"sinkInventoryId", "spill"}}, {{"source", beforeSource["materialRevision"]}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto emptied = json::parse(read_text(handle));
    const auto& emptySource = emptied["vessels"][0]["id"] == "source" ?
        emptied["vessels"][0] : emptied["vessels"][1];
    submit(handle, command("9", "TransferFixed", {{"sourceInventoryId", "z-base-stock"},
                           {"sourceRegion", "Homogeneous"}, {"selection", "HomogeneousAqueousLiquid"},
                           {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 4e-6}}},
                           {"captureFractions", json::array({{{"destinationInventoryId", "source"}, {"fraction", 1.0}}})},
                           {"overflowSinkId", "spill"}},
                           {{"z-base-stock", "1"}, {"source", emptySource["materialRevision"]}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto beforeBase = json::parse(read_text(handle));
    const std::string baseInput = R"([{"toolId":"tool","sampleSequence":"3","captureMonotonicNs":"1040000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":1.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    REQUIRE(plv_input_batch(handle, baseInput.data(), static_cast<std::uint32_t>(baseInput.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1'050'000'000ULL) == PLV_OK);
    REQUIRE(poll(handle)["type"] == "LiveTransferCommitted");
    const auto afterBase = json::parse(read_text(handle));
    const auto& receiver = afterBase["vessels"][0]["id"] == "receiver" ?
        afterBase["vessels"][0] : afterBase["vessels"][1];
    CHECK(receiver["inventory"]["chlorideMol"].get<double>() > chlorideBefore);
    CHECK(receiver["inventory"]["sodiumMol"].get<double>() > beforeReceiver["inventory"]["sodiumMol"].get<double>());
    for (const char* pool : {"chlorideMol", "sodiumMol", "acetateMol", "solventWaterKg",
                             "researchAdditiveVolumeM3"}) {
        CHECK(afterBase["materialLedger"][pool].get<double>() ==
              doctest::Approx(beforeBase["materialLedger"][pool].get<double>()).epsilon(1e-12));
    }
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P02_03 full receiver and exhausted source preserve the live tool ledger") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-10}})); poll(handle);
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
    const std::string sample = R"([{"toolId":"burette-1","sampleSequence":"1","captureMonotonicNs":"1000000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":1.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[{"destinationInventoryId":"receiver","fraction":1.0}]}])";
    REQUIRE(plv_input_batch(handle, sample.data(), static_cast<std::uint32_t>(sample.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1'010'000'000ULL) == PLV_OK);
    const auto afterPour = json::parse(read_text(handle));
    const auto receiver = afterPour["vessels"][0]["id"] == "receiver"
                              ? afterPour["vessels"][0] : afterPour["vessels"][1];
    const auto source = afterPour["vessels"][0]["id"] == "source"
                            ? afterPour["vessels"][0] : afterPour["vessels"][1];
    CHECK(receiver["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(1e-10));
    CHECK(afterPour["sinks"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>() > 0.0);
    CHECK(afterPour["materialLedger"]["chlorideMol"].get<double>() == doctest::Approx(4e-4));
    CHECK(poll(handle)["type"] == "LiveTransferCommitted");
    submit(handle, command("6", "DisposeContents",
                           {{"sourceInventoryId", "source"}, {"sinkInventoryId", "spill"}},
                           {{"source", source["materialRevision"]}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto empty = json::parse(read_text(handle));
    REQUIRE(plv_step(handle, 0.02, 1'030'000'000ULL) == PLV_OK);
    const auto afterLanding = json::parse(read_text(handle));
    const auto landedSource = afterLanding["vessels"][0]["id"] == "source"
                                  ? afterLanding["vessels"][0] : afterLanding["vessels"][1];
    CHECK(landedSource["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(0.0));
    CHECK(afterLanding["tools"][0]["inFlightInventory"]["researchAdditiveVolumeM3"].get<double>() ==
          doctest::Approx(0.0));
    CHECK(afterLanding["materialLedger"]["chlorideMol"].get<double>() ==
          doctest::Approx(empty["materialLedger"]["chlorideMol"].get<double>()));
    CHECK(afterLanding["materialLedger"]["researchAdditiveVolumeM3"].get<double>() ==
          doctest::Approx(empty["materialLedger"]["researchAdditiveVolumeM3"].get<double>()));
    CHECK(poll(handle)["type"] == "InFlightLanded");
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
    submit(handle, command("7", "Continue", {{"monotonicNowNs", "1020000000"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto before = json::parse(read_text(handle));
    REQUIRE(plv_step(handle, 0.02, 1040000000) == PLV_OK);
    const auto after = json::parse(read_text(handle));
    CHECK_FALSE(after["hold"]["active"].get<bool>());
    CHECK(after["vessels"] == before["vessels"]);
    CHECK(after["simulationTimeS"].get<double>() == doctest::Approx(0.02));
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P01_03 Continue expires readiness and SetActuator invalidates it") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("3", "PlaceTool",
                           {{"toolId", "tool"}, {"sourceInventoryId", "source"}, {"overflowSinkId", "spill"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("4", "BeginInputHold", {{"reason", "FocusLoss"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    auto sample = json::parse(R"({"toolId":"tool","sampleSequence":"1","captureMonotonicNs":"1000000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[]})");
    auto input = json::array({sample}).dump();
    REQUIRE(plv_input_batch(handle, input.data(), static_cast<std::uint32_t>(input.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.0, 1'010'000'000ULL) == PLV_OK);
    REQUIRE(json::parse(read_text(handle))["hold"]["recoveryReady"].get<bool>());
    submit(handle, command("5", "Continue", {{"monotonicNowNs", "1200000000"}}));
    REQUIRE_FALSE(poll(handle)["accepted"].get<bool>());
    CHECK(json::parse(read_text(handle))["hold"]["active"].get<bool>());
    sample["sampleSequence"] = "2";
    sample["captureMonotonicNs"] = "1200000000";
    input = json::array({sample}).dump();
    REQUIRE(plv_input_batch(handle, input.data(), static_cast<std::uint32_t>(input.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.0, 1'210'000'000ULL) == PLV_OK);
    REQUIRE(json::parse(read_text(handle))["hold"]["recoveryReady"].get<bool>());
    submit(handle, command("6", "SetActuator",
                           {{"toolId", "tool"}, {"actuator01", 0.0}, {"expectedActuatorRevision", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("7", "Continue", {{"monotonicNowNs", "1210000000"}}));
    CHECK_FALSE(poll(handle)["accepted"].get<bool>());
    CHECK(json::parse(read_text(handle))["hold"]["active"].get<bool>());
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P01_03 rinse attempt during hold cannot change material or reuse readiness") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "rinse"}, {"capacityM3", 1e-5}})); poll(handle);
    submit(handle, command("2", "CreateSink", {{"id", "waste"}})); poll(handle);
    submit(handle, command("3", "PrepareStock",
                           {{"vesselId", "rinse"}, {"stockKind", "Water"},
                            {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 4e-6}}, {{"rinse", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("4", "PlaceTool",
                           {{"toolId", "tool"}, {"sourceInventoryId", "rinse"}, {"overflowSinkId", "waste"},
                            {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
                            {"profileRevision", "1"}})); REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("5", "BeginInputHold", {{"reason", "FocusLoss"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const std::string neutral = R"([{"toolId":"tool","sampleSequence":"1","captureMonotonicNs":"1000000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[]}])";
    REQUIRE(plv_input_batch(handle, neutral.data(), static_cast<std::uint32_t>(neutral.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.0, 1'010'000'000ULL) == PLV_OK);
    REQUIRE(json::parse(read_text(handle))["hold"]["recoveryReady"].get<bool>());
    const auto before = json::parse(read_text(handle));
    submit(handle, command("6", "RinseTool",
                           {{"toolId", "tool"}, {"rinseSourceInventoryId", "rinse"}, {"wasteSinkId", "waste"},
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-6}}},
                            {"expectedInventoryRevision", "0"}}, {{"rinse", "1"}}));
    CHECK_FALSE(poll(handle)["accepted"].get<bool>());
    const auto after = json::parse(read_text(handle));
    CHECK(after["vessels"] == before["vessels"]);
    CHECK(after["sinks"] == before["sinks"]);
    CHECK(after["simulationTimeS"] == before["simulationTimeS"]);
    submit(handle, command("7", "Continue", {{"monotonicNowNs", "1010000000"}}));
    CHECK_FALSE(poll(handle)["accepted"].get<bool>());
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
    submit(handle, command("6", "Continue", {{"monotonicNowNs", "1020000000"}}));
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
    submit(imported, command("5", "Continue", {{"monotonicNowNs", "1020000000"}}));
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
        if (tick > 0) {
            const auto landed = poll(handle);
            REQUIRE(landed["type"] == "InFlightLanded");
            const auto landingSequence = std::stoull(landed["eventSequence"].get<std::string>());
            CHECK(landingSequence > priorEventSequence);
            priorEventSequence = landingSequence;
            const double landedCaptured = landed["captured"][0]["quantityM3"].get<double>();
            const double landedSpilled = landed["spilledQuantityM3"].get<double>();
            CHECK(landedCaptured + landedSpilled ==
                  doctest::Approx(landed["quantityM3"].get<double>()).epsilon(1e-9));
            allocatedM3 += landedCaptured + landedSpilled;
        }
        const auto event = poll(handle);
        REQUIRE(event["type"] == "LiveTransferCommitted");
        CHECK(event["branchId"] == "branch-1");
        const auto eventSequence = std::stoull(event["eventSequence"].get<std::string>());
        CHECK(eventSequence > priorEventSequence);
        priorEventSequence = eventSequence;
        const double captured = event["captured"][0]["quantityM3"].get<double>();
        const double spilled = event["spilledQuantityM3"].get<double>();
        CHECK(captured + spilled == doctest::Approx(event["quantityM3"].get<double>()).epsilon(1e-9));
        CHECK(event["sourceDebitM3"].get<double>() ==
              doctest::Approx(event["quantityM3"].get<double>() +
                              event["tipRetainedM3"].get<double>() +
                              event["inFlightReservedM3"].get<double>()).epsilon(1e-9));
        allocatedM3 += captured + spilled;
    }
    CHECK(allocatedM3 > 0.0);
    CHECK_FALSE(json::parse(read_text(handle))["hold"]["active"].get<bool>());
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P03_01 closing valve after moving receiver spills the pending drop exactly once") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 0.00005}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 0.00005}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PrepareStock", {{"vesselId", "source"}, {"stockKind", "Water"},
        {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 0.00004}}, {{"source", "0"}})); poll(handle);
    submit(handle, command("5", "PlaceTool", {{"toolId", "burette-1"}, {"sourceInventoryId", "source"},
        {"overflowSinkId", "spill"}, {"coordinateFrame", "lab"},
        {"geometryProfileHash", "burette-50ml-research-v1"}, {"profileRevision", "1"}})); poll(handle);
    json sample = {{"toolId", "burette-1"}, {"sampleSequence", "1"},
        {"captureMonotonicNs", "1000000000"}, {"positionMetres", {0, 1, 0}},
        {"rotation", {0, 0, 0, 1}}, {"trackingValid", true}, {"actuator01", 1.0},
        {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
        {"profileRevision", "1"}, {"toolRevision", "1"},
        {"captureFractions", json::array({{{"destinationInventoryId", "receiver"}, {"fraction", 1.0}}})}};
    auto batch = json::array({sample}).dump();
    REQUIRE(plv_input_batch(handle, batch.data(), static_cast<std::uint32_t>(batch.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1'000'000'000ULL) == PLV_OK);
    const auto issued = poll(handle);
    REQUIRE(issued["type"] == "LiveTransferCommitted");
    const auto beforeLanding = json::parse(read_text(handle));
    const double pending = beforeLanding["tools"][0]["inFlightInventory"]
        ["researchAdditiveVolumeM3"].get<double>();
    REQUIRE(pending > 0.0);
    const auto parcelId = beforeLanding["tools"][0]["inFlightParcelId"].get<std::string>();
    REQUIRE_FALSE(parcelId.empty());
    CHECK(issued["inFlightParcelId"] == parcelId);
    sample["sampleSequence"] = "2";
    sample["captureMonotonicNs"] = "1020000000";
    sample["actuator01"] = 0.0;
    sample["captureFractions"] = json::array();
    batch = json::array({sample}).dump();
    REQUIRE(plv_input_batch(handle, batch.data(), static_cast<std::uint32_t>(batch.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.02, 1'020'000'000ULL) == PLV_OK);
    const auto landed = poll(handle);
    CHECK(landed["type"] == "InFlightLanded");
    CHECK(landed["parcelId"] == parcelId);
    CHECK(landed["spilledQuantityM3"].get<double>() == doctest::Approx(pending));
    CHECK(landed["captured"].empty());
    const auto afterLanding = json::parse(read_text(handle));
    CHECK(afterLanding["tools"][0]["inFlightParcelId"] == "");
    CHECK(afterLanding["vessels"] == beforeLanding["vessels"]);
    CHECK(afterLanding["sinks"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>() ==
        doctest::Approx(beforeLanding["sinks"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>() + pending));
    REQUIRE(plv_step(handle, 0.02, 1'040'000'000ULL) == PLV_OK);
    std::uint32_t required = 0;
    CHECK(plv_poll(handle, nullptr, 0, &required) == PLV_NO_EVENT);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P03_01 imported in-flight parcel requires its committed identity") {
    std::uint64_t live = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &live) == PLV_OK);
    submit(live, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 5e-5}})); poll(live);
    submit(live, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 5e-5}})); poll(live);
    submit(live, command("3", "CreateSink", {{"id", "spill"}})); poll(live);
    submit(live, command("4", "PrepareStock", {{"vesselId", "source"}, {"stockKind", "Water"},
        {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 4e-5}}, {{"source", "0"}})); poll(live);
    submit(live, command("5", "PlaceTool", {{"toolId", "burette-1"}, {"sourceInventoryId", "source"},
        {"overflowSinkId", "spill"}, {"coordinateFrame", "lab"},
        {"geometryProfileHash", "burette-50ml-research-v1"}, {"profileRevision", "1"}})); poll(live);
    const json sample = {{{"toolId", "burette-1"}, {"sampleSequence", "1"},
        {"captureMonotonicNs", "1000000000"}, {"positionMetres", {0, 1, 0}},
        {"rotation", {0, 0, 0, 1}}, {"trackingValid", true}, {"actuator01", 1.0},
        {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
        {"profileRevision", "1"}, {"toolRevision", "1"},
        {"captureFractions", json::array({{{"destinationInventoryId", "receiver"}, {"fraction", 1.0}}})}}};
    const auto encoded = sample.dump();
    REQUIRE(plv_input_batch(live, encoded.data(), static_cast<std::uint32_t>(encoded.size())) == PLV_OK);
    REQUIRE(plv_step(live, 0.02, 1'000'000'000ULL) == PLV_OK);
    const auto issued = poll(live);
    REQUIRE(issued["type"] == "LiveTransferCommitted");
    const auto exported = json::parse(read_text(live, true));
    REQUIRE(exported["tools"].size() == 1U);
    const auto originalId = exported["tools"][0]["inFlightParcelId"].get<std::string>();
    REQUIRE_FALSE(originalId.empty());
    REQUIRE(exported["tools"][0]["inFlightInventory"]["researchAdditiveVolumeM3"].get<double>() > 0.0);
    CHECK(originalId == issued["inFlightParcelId"]);

    const auto importStatus = [](const json& candidate) {
        const auto content = candidate.dump();
        std::uint64_t imported = 0;
        const auto result = plv_import(content.data(), static_cast<std::uint32_t>(content.size()), &imported);
        if (result == PLV_OK) CHECK(plv_destroy(imported) == PLV_OK);
        else CHECK(imported == 0U);
        return result;
    };
    CHECK(importStatus(exported) == PLV_OK);

    auto anonymous = exported;
    anonymous["tools"][0]["inFlightParcelId"] = "";
    CHECK(importStatus(anonymous) == PLV_INVALID_ARGUMENT);

    auto wrongBranch = exported;
    wrongBranch["tools"][0]["inFlightParcelId"] = "other-branch/7/burette-1";
    CHECK(importStatus(wrongBranch) == PLV_INVALID_ARGUMENT);

    auto wrongTool = exported;
    wrongTool["tools"][0]["inFlightParcelId"] = "branch-1/7/other-tool";
    CHECK(importStatus(wrongTool) == PLV_INVALID_ARGUMENT);

    auto futureSequence = exported;
    futureSequence["tools"][0]["inFlightParcelId"] = "branch-1/999999/burette-1";
    CHECK(importStatus(futureSequence) == PLV_INVALID_ARGUMENT);

    CHECK(json::parse(read_text(live, true)) == exported);
    CHECK(plv_destroy(live) == PLV_OK);
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

TEST_CASE("P05_01 fixed ten ml transfer is denied before material commit") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE},
                         {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 0.02}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 0.02}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "waste"}})); poll(handle);
    submit(handle, command("4", "PrepareStock", {{"vesselId", "source"}, {"stockKind", "HydrochloricAcid"},
                             {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 0.01}}, {{"source", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    bool current = false;
    for (int attempt = 0; attempt < 300 && !current; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
        const auto state = json::parse(read_text(handle));
        for (const auto& item : state["observations"])
            if (item.value("vesselId", "") == "source" && item.value("freshness", "") == "Current")
                current = true;
        if (!current) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(current);
    const auto before = json::parse(read_text(handle));
    submit(handle, command("5", "TransferFixed",
        {{"sourceInventoryId", "source"}, {"sourceRegion", "Homogeneous"},
         {"selection", "HomogeneousAqueousLiquid"},
         {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 0.01}}},
         {"captureFractions", json::array({{{"destinationInventoryId", "receiver"}, {"fraction", 1.0}}})},
         {"overflowSinkId", "waste"}}, {{"source", "1"}, {"receiver", "0"}}));
    const auto result = poll(handle);
    CHECK_FALSE(result["accepted"].get<bool>());
    CHECK(result["code"] == "ObservationVolumeHold");
    const auto after = json::parse(read_text(handle));
    CHECK(after["vessels"] == before["vessels"]);
    CHECK(after["sinks"] == before["sinks"]);
    CHECK(after["eventSequence"] == before["eventSequence"]);
    std::int32_t destroyed = PLV_BUSY;
    for (int i = 0; i < 200 && destroyed == PLV_BUSY; ++i) {
        destroyed = plv_destroy(handle);
        if (destroyed == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    CHECK(destroyed == PLV_OK);
}

TEST_CASE("P05_01 product Precision and Bulk profiles admit different fixed proposals and survive export") {
    for (const std::string profile : {"Precision", "Bulk"}) {
        std::uint64_t handle = 0;
        const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                             {"databasePath", PLV_MINTEQ_DATABASE},
                             {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"},
                             {"scienceBudgetMode", profile}};
        const auto encoded = config.dump();
        REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
        submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 2e-6}})); poll(handle);
        submit(handle, command("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 2e-6}})); poll(handle);
        submit(handle, command("3", "CreateSink", {{"id", "waste"}})); poll(handle);
        submit(handle, command("4", "PrepareStock", {{"vesselId", "source"}, {"stockKind", "HydrochloricAcid"},
                                 {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-6}}, {{"source", "0"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        bool current = false;
        for (int attempt = 0; attempt < 300 && !current; ++attempt) {
            REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
            const auto state = json::parse(read_text(handle));
            for (const auto& item : state["observations"])
                if (item.value("vesselId", "") == "source" && item.value("freshness", "") == "Current")
                    current = true;
            if (!current) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        REQUIRE(current);
        CHECK(json::parse(read_text(handle))["scienceBudgetMode"] == profile);
        CHECK(json::parse(read_text(handle, true))["scienceBudgetMode"] == profile);
        submit(handle, command("5", "TransferFixed",
            {{"sourceInventoryId", "source"}, {"sourceRegion", "Homogeneous"},
             {"selection", "HomogeneousAqueousLiquid"},
             {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 2e-7}}},
             {"captureFractions", json::array({{{"destinationInventoryId", "receiver"}, {"fraction", 1.0}}})},
             {"overflowSinkId", "waste"}}, {{"source", "1"}, {"receiver", "0"}}));
        const auto result = poll(handle);
        CHECK(result["accepted"] == (profile == "Bulk"));
        const auto state = json::parse(read_text(handle));
        CHECK(state["vessels"][0]["materialRevision"] == (profile == "Bulk" ? "1" : "0"));
        const auto exported = read_text(handle, true);
        std::uint64_t imported = 0;
        REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
        CHECK(json::parse(read_text(imported))["scienceBudgetMode"] == profile);
        std::int32_t importedDestroyed = PLV_BUSY;
        for (int i = 0; i < 200 && importedDestroyed == PLV_BUSY; ++i) {
            importedDestroyed = plv_destroy(imported);
            if (importedDestroyed == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        CHECK(importedDestroyed == PLV_OK);
        std::int32_t destroyed = PLV_BUSY;
        for (int i = 0; i < 200 && destroyed == PLV_BUSY; ++i) {
            destroyed = plv_destroy(handle);
            if (destroyed == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        CHECK(destroyed == PLV_OK);
    }
}

TEST_CASE("P05_01 rinse and disposal share precommit admission with fixed transfer") {
    for (const bool rinse : {false, true}) {
        std::uint64_t handle = 0;
        const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                             {"databasePath", PLV_MINTEQ_DATABASE},
                             {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
        const auto encoded = config.dump();
        REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
        submit(handle, command("1", "CreateVessel", {{"id", "source"}, {"capacityM3", 2e-6}})); poll(handle);
        submit(handle, command("2", "CreateSink", {{"id", "waste"}})); poll(handle);
        submit(handle, command("3", "PrepareStock", {{"vesselId", "source"}, {"stockKind", "Water"},
                                 {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 1e-6}}, {{"source", "0"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        if (rinse) {
            submit(handle, command("4", "PlaceTool", {{"toolId", "tool"}, {"sourceInventoryId", "source"},
                {"overflowSinkId", "waste"}, {"coordinateFrame", "lab"},
                {"geometryProfileHash", "burette-50ml-research-v1"}, {"profileRevision", "1"}}));
            REQUIRE(poll(handle)["accepted"].get<bool>());
        }
        const auto before = json::parse(read_text(handle));
        if (rinse) {
            submit(handle, command("5", "RinseTool", {{"toolId", "tool"},
                {"rinseSourceInventoryId", "source"}, {"wasteSinkId", "waste"},
                {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-6}}},
                {"expectedInventoryRevision", "0"}}, {{"source", "1"}}));
        } else {
            submit(handle, command("4", "DisposeContents",
                {{"sourceInventoryId", "source"}, {"sinkInventoryId", "waste"}}, {{"source", "1"}}));
        }
        const auto result = poll(handle);
        CHECK_FALSE(result["accepted"].get<bool>());
        CHECK(result["code"] == "ObservationRequired");
        const auto after = json::parse(read_text(handle));
        CHECK(after["vessels"] == before["vessels"]);
        CHECK(after["sinks"] == before["sinks"]);
        CHECK(after["tools"] == before["tools"]);
        CHECK(after["eventSequence"] == before["eventSequence"]);
        std::int32_t destroyed = PLV_BUSY;
        for (int i = 0; i < 200 && destroyed == PLV_BUSY; ++i) {
            destroyed = plv_destroy(handle);
            if (destroyed == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        CHECK(destroyed == PLV_OK);
    }
}

TEST_CASE("P05_01 opposite fixed transfers cannot hide gross circulation behind zero net volume") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE},
                         {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    for (const auto& [id, sequence] : {std::pair{"a", "1"}, std::pair{"b", "3"}}) {
        submit(handle, command(sequence, "CreateVessel", {{"id", id}, {"capacityM3", 2e-6}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
        const auto prepareSequence = std::to_string(std::stoi(sequence) + 1);
        submit(handle, command(prepareSequence, "PrepareStock",
            {{"vesselId", id}, {"stockKind", "HydrochloricAcid"},
             {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-6}}, {{id, "0"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
    }
    submit(handle, command("5", "CreateSink", {{"id", "waste"}})); REQUIRE(poll(handle)["accepted"].get<bool>());
    bool bothCurrent = false;
    for (int attempt = 0; attempt < 300 && !bothCurrent; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
        const auto state = json::parse(read_text(handle));
        std::size_t current = 0;
        for (const auto& item : state["observations"])
            if (item.value("freshness", "") == "Current") ++current;
        bothCurrent = current == 2;
        if (!bothCurrent) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(bothCurrent);
    const auto transfer = [&](const std::string& sequence, const std::string& from,
                              const std::string& to, const std::string& revision) {
        submit(handle, command(sequence, "TransferFixed",
            {{"sourceInventoryId", from}, {"sourceRegion", "Homogeneous"},
             {"selection", "HomogeneousAqueousLiquid"},
             {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 3e-8}}},
             {"captureFractions", json::array({{{"destinationInventoryId", to}, {"fraction", 1.0}}})},
             {"overflowSinkId", "waste"}}, {{from, revision}, {to, revision}}));
        return poll(handle);
    };
    REQUIRE(transfer("6", "a", "b", "1")["accepted"].get<bool>());
    const auto beforeReverse = json::parse(read_text(handle));
    const auto reverse = transfer("7", "b", "a", "2");
    CHECK_FALSE(reverse["accepted"].get<bool>());
    CHECK(reverse["code"] == "ObservationVolumeHold");
    const auto afterReverse = json::parse(read_text(handle));
    CHECK(afterReverse["vessels"] == beforeReverse["vessels"]);
    CHECK(afterReverse["eventSequence"] == beforeReverse["eventSequence"]);
    std::int32_t destroyed = PLV_BUSY;
    for (int i = 0; i < 200 && destroyed == PLV_BUSY; ++i) {
        destroyed = plv_destroy(handle);
        if (destroyed == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    CHECK(destroyed == PLV_OK);
}

TEST_CASE("P05_02 real multi-vessel science scopes freshness and preserves committed history") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE},
                         {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    std::uint64_t sequence = 1;
    for (const std::string id : {"a", "b", "unrelated"}) {
        submit(handle, command(std::to_string(sequence++), "CreateVessel",
            {{"id", id}, {"capacityM3", 2e-6}})); REQUIRE(poll(handle)["accepted"].get<bool>());
        submit(handle, command(std::to_string(sequence++), "PrepareStock",
            {{"vesselId", id}, {"stockKind", "HydrochloricAcid"},
             {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 1e-6}}, {{id, "0"}}));
        REQUIRE(poll(handle)["accepted"].get<bool>());
    }
    submit(handle, command(std::to_string(sequence++), "CreateSink", {{"id", "waste"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto observations = [](const json& state) {
        std::unordered_map<std::string, json> byVessel;
        for (const auto& item : state["observations"])
            byVessel.emplace(item.at("vesselId").get<std::string>(), item);
        return byVessel;
    };
    bool allCurrent = false;
    for (int attempt = 0; attempt < 300 && !allCurrent; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
        const auto items = observations(json::parse(read_text(handle)));
        allCurrent = items.size() == 3U;
        for (const auto& [id, item] : items) allCurrent &= item.value("freshness", "") == "Current";
        if (!allCurrent) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    REQUIRE(allCurrent);
    const auto before = json::parse(read_text(handle));
    const auto unrelatedPH = observations(before).at("unrelated").at("value");
    submit(handle, command(std::to_string(sequence++), "TransferFixed",
        {{"sourceInventoryId", "a"}, {"sourceRegion", "Homogeneous"},
         {"selection", "HomogeneousAqueousLiquid"},
         {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-8}}},
         {"captureFractions", json::array({{{"destinationInventoryId", "b"}, {"fraction", 1.0}}})},
         {"overflowSinkId", "waste"}}, {{"a", "1"}, {"b", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto afterCommit = json::parse(read_text(handle));
    CHECK(std::stoull(afterCommit["eventSequence"].get<std::string>()) ==
          std::stoull(before["eventSequence"].get<std::string>()) + 1U);
    CHECK(observations(afterCommit).at("unrelated").at("freshness") == "Current");
    CHECK(observations(afterCommit).at("unrelated").at("value") == unrelatedPH);
    bool changedCurrent = false;
    for (int attempt = 0; attempt < 300 && !changedCurrent; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
        const auto state = json::parse(read_text(handle));
        const auto items = observations(state);
        changedCurrent = items.at("a").at("freshness") == "Current" &&
                         items.at("b").at("freshness") == "Current" &&
                         items.at("a").at("asOfMaterialRevision") == "2" &&
                         items.at("b").at("asOfMaterialRevision") == "2";
        CHECK(items.at("unrelated").at("freshness") == "Current");
        CHECK(state["eventSequence"] == afterCommit["eventSequence"]);
        if (!changedCurrent) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    CHECK(changedCurrent);
    const auto beforeUnsupported = json::parse(read_text(handle));
    submit(handle, command(std::to_string(sequence++), "KineticSolve",
        {{"vesselId", "a"}, {"durationSeconds", 1.0}}, {{"a", "2"}}));
    const auto unsupported = poll(handle);
    CHECK_FALSE(unsupported["accepted"].get<bool>());
    CHECK(unsupported["code"] == "UnsupportedCommand");
    CHECK(json::parse(read_text(handle))["eventSequence"] == beforeUnsupported["eventSequence"]);
    std::int32_t destroyed = PLV_BUSY;
    for (int i = 0; i < 200 && destroyed == PLV_BUSY; ++i) {
        destroyed = plv_destroy(handle);
        if (destroyed == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    CHECK(destroyed == PLV_OK);
}

TEST_CASE("P05_03 product snapshot reports the live worker stall diagnostic") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE},
                         {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    const auto snapshot = json::parse(read_text(handle));
    REQUIRE(snapshot.contains("solverWallStalled"));
    CHECK(snapshot["solverWallStalled"] == false);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P04_01 checkpoint rollback recomputes real IPhreeqc for restored dependency") {
    std::uint64_t handle = 0;
    const json config = {{"schemaVersion", 1}, {"branchId", "branch-1"},
                         {"databasePath", PLV_MINTEQ_DATABASE},
                         {"databaseIdentity", "minteq.v4.dat-pinned-package-bytes"}};
    const auto encoded = config.dump();
    REQUIRE(plv_create(encoded.data(), static_cast<std::uint32_t>(encoded.size()), &handle) == PLV_OK);
    submit(handle, command("1", "CreateVessel", {{"id", "acid"}, {"capacityM3", 2e-5}})); poll(handle);
    submit(handle, command("2", "CreateVessel", {{"id", "base"}, {"capacityM3", 2e-5}})); poll(handle);
    submit(handle, command("3", "CreateSink", {{"id", "spill"}})); poll(handle);
    submit(handle, command("4", "PrepareStock",
                           {{"vesselId", "acid"}, {"stockKind", "HydrochloricAcid"},
                            {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 4e-8}},
                           {{"acid", "0"}})); poll(handle);
    submit(handle, command("5", "PrepareStock",
                           {{"vesselId", "base"}, {"stockKind", "SodiumHydroxide"},
                            {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 9e-8}},
                           {{"base", "0"}})); poll(handle);
    const auto observation = [](const json& snapshot) {
        for (const auto& item : snapshot.at("observations"))
            if (item.at("vesselId") == "acid") return item;
        return json();
    };
    const auto waitCurrent = [&]() {
        json state;
        for (int attempt = 0; attempt < 250; ++attempt) {
            REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
            state = observation(json::parse(read_text(handle)));
            if (state.value("freshness", "") == "Current" && state.value("computationState", "") == "Ready")
                return state;
            std::this_thread::sleep_for(std::chrono::milliseconds(10));
        }
        return state;
    };
    const auto before = waitCurrent();
    REQUIRE(before["freshness"] == "Current");
    REQUIRE(before["asOfMaterialRevision"] == "1");
    bool baseCurrent = false;
    for (int attempt = 0; attempt < 250 && !baseCurrent; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
        const auto state = json::parse(read_text(handle));
        for (const auto& item : state["observations"])
            if (item.value("vesselId", "") == "base" && item.value("freshness", "") == "Current")
                baseCurrent = true;
        if (!baseCurrent) std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
    REQUIRE(baseCurrent);
    const double acidPH = before["value"].get<double>();
    submit(handle, command("6", "CreateCheckpoint", {{"checkpointId", "acid-state"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("7", "TransferFixed",
                           {{"sourceInventoryId", "base"}, {"sourceRegion", "Homogeneous"},
                            {"selection", "HomogeneousAqueousLiquid"},
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 4e-8}}},
                            {"captureFractions", json::array({{{"destinationInventoryId", "acid"}, {"fraction", 1.0}}})},
                            {"overflowSinkId", "spill"}},
                           {{"base", "1"}, {"acid", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto changed = waitCurrent();
    REQUIRE(changed["asOfMaterialRevision"] == "2");
    REQUIRE(changed["value"].get<double>() > acidPH + 5.0);
    submit(handle, command("8", "RestartCheckpoint", {{"checkpointId", "acid-state"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto restored = observation(json::parse(read_text(handle)));
    CHECK_FALSE((restored.value("freshness", "") == "Current" &&
                 restored.value("asOfMaterialRevision", "") != "1"));
    CHECK(json::parse(read_text(handle))["scienceDependencyGeneration"] == "2");
    const auto recomputed = waitCurrent();
    CHECK(recomputed["asOfMaterialRevision"] == "1");
    // Restart invalidates the observation budgets of BOTH vessels. A Current
    // acid result alone does not imply that the restored base has a solved
    // boundary, so a second transfer must await both actual solver receipts.
    baseCurrent = false;
    for (int attempt = 0; attempt < 250 && !baseCurrent; ++attempt) {
        REQUIRE(plv_step(handle, 0.0, 1'000'000'000ULL) == PLV_OK);
        const auto state = json::parse(read_text(handle));
        for (const auto& item : state["observations"])
            if (item.value("vesselId", "") == "base" &&
                item.value("freshness", "") == "Current" &&
                item.value("computationState", "") == "Ready" &&
                item.value("asOfMaterialRevision", "") == "1")
                baseCurrent = true;
        if (!baseCurrent) std::this_thread::sleep_for(std::chrono::milliseconds(10));
    }
    REQUIRE_MESSAGE(baseCurrent, "Restored base observation must be Current before a second transfer.");
    CHECK(recomputed["freshness"] == "Current");
    CHECK(recomputed["value"].get<double>() == doctest::Approx(acidPH).epsilon(1e-7));
    submit(handle, command("9", "Continue"));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("10", "TransferFixed",
                           {{"sourceInventoryId", "base"}, {"sourceRegion", "Homogeneous"},
                            {"selection", "HomogeneousAqueousLiquid"},
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 4e-8}}},
                            {"captureFractions", json::array({{{"destinationInventoryId", "acid"}, {"fraction", 1.0}}})},
                            {"overflowSinkId", "spill"}},
                           {{"base", "1"}, {"acid", "1"}}));
    const auto secondTransferOutcome = poll(handle);
    INFO("P04_01 second transfer after rollback: " << secondTransferOutcome.dump());
    REQUIRE(secondTransferOutcome["accepted"].get<bool>());
    submit(handle, command("11", "RestartCheckpoint", {{"checkpointId", "acid-state"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    const auto pendingRestore = json::parse(read_text(handle));
    CHECK(pendingRestore["scienceDependencyGeneration"] == "3");
    const auto pendingAcid = observation(pendingRestore);
    CHECK(pendingAcid.value("freshness", "") != "Current");
    const auto afterOldCompletion = waitCurrent();
    CHECK(afterOldCompletion["asOfMaterialRevision"] == "1");
    CHECK(afterOldCompletion["value"].get<double>() == doctest::Approx(acidPH).epsilon(1e-7));
    std::int32_t destroyStatus = PLV_BUSY;
    for (int attempt = 0; attempt < 200 && destroyStatus == PLV_BUSY; ++attempt) {
        destroyStatus = plv_destroy(handle);
        if (destroyStatus == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
    }
    CHECK(destroyStatus == PLV_OK);
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
    CHECK(after["materialLedger"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(1e-4));
    CHECK(after["materialLedger"]["sodiumMol"].get<double>() == doctest::Approx(0.01));
    CHECK(after["materialLedger"]["chlorideMol"].get<double>() == doctest::Approx(0.01));
    for (const auto& tool : after["tools"]) {
        CHECK(tool["tipInventory"]["researchAdditiveVolumeM3"].get<double>() > 0.0);
        CHECK(tool["inFlightInventory"]["researchAdditiveVolumeM3"].get<double>() > 0.0);
    }
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
    for (const auto session : {imported, handle}) {
        std::int32_t destroyed = PLV_BUSY;
        for (int i = 0; i < 200 && destroyed == PLV_BUSY; ++i) {
            destroyed = plv_destroy(session);
            if (destroyed == PLV_BUSY) std::this_thread::sleep_for(std::chrono::milliseconds(5));
        }
        CHECK(destroyed == PLV_OK);
    }
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
                            {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 5e-8}}},
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

    const std::string neutralAfterRestart = R"([{"toolId":"burette-1","sampleSequence":"1","captureMonotonicNs":"1000000000","positionMetres":[0,1,0],"rotation":[0,0,0,1],"trackingValid":true,"actuator01":0.0,"coordinateFrame":"lab","geometryProfileHash":"burette-50ml-research-v1","profileRevision":"1","toolRevision":"1","captureFractions":[]}])";
    REQUIRE(plv_input_batch(handle, neutralAfterRestart.data(), static_cast<std::uint32_t>(neutralAfterRestart.size())) == PLV_OK);
    REQUIRE(plv_step(handle, 0.0, 1'010'000'000ULL) == PLV_OK);
    submit(handle, command("11", "Continue", {{"monotonicNowNs", "1010000000"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("12", "RinseTool",
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

    submit(handle, command("13", "BeginModeChange", {{"mode", "VR"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    snapshot = json::parse(read_text(handle));
    CHECK(snapshot["mode"] == "VR");
    CHECK(snapshot["hold"]["active"].get<bool>());
    CHECK(snapshot["hold"]["reason"] == "ModeChange");

    const auto exported = read_text(handle, true);
    std::uint64_t imported = 0;
    REQUIRE(plv_import(exported.data(), static_cast<std::uint32_t>(exported.size()), &imported) == PLV_OK);
    submit(imported, command("14", "RestartCheckpoint", {{"checkpointId", "before-disposal"}}));
    REQUIRE(poll(imported)["accepted"].get<bool>());
    const auto importedSnapshot = json::parse(read_text(imported));
    const auto importedSource = importedSnapshot["vessels"][0]["id"] == "source"
                                    ? importedSnapshot["vessels"][0]
                                    : importedSnapshot["vessels"][1];
    CHECK(importedSource["inventory"]["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(4e-6));
    CHECK(plv_destroy(imported) == PLV_OK);
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P03_02 switch prepared pipette source carries prior chloride into new base aliquot") {
    std::uint64_t h = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1","scienceBudgetMode":"Bulk"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &h) == PLV_OK);
    const auto issue = [&](const std::string& sequence, const std::string& type,
                           const json& payload, const json& expected = json::object()) {
        submit(h, command(sequence, type, payload, expected));
        return poll(h);
    };
    CHECK(issue("1", "CreateVessel", {{"id", "acidBottle"}, {"capacityM3", 1e-5}})["accepted"] == true);
    CHECK(issue("2", "CreateVessel", {{"id", "baseBottle"}, {"capacityM3", 1e-5}})["accepted"] == true);
    CHECK(issue("3", "CreateVessel", {{"id", "acidReceiver"}, {"capacityM3", 1e-5}})["accepted"] == true);
    CHECK(issue("4", "CreateVessel", {{"id", "mixedReceiver"}, {"capacityM3", 1e-5}})["accepted"] == true);
    CHECK(issue("5", "CreateSink", {{"id", "waste"}})["accepted"] == true);
    CHECK(issue("6", "PrepareStock", {{"vesselId", "acidBottle"}, {"stockKind", "HydrochloricAcid"},
        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 5e-6}}, {{"acidBottle", "0"}})["accepted"] == true);
    CHECK(issue("7", "PrepareStock", {{"vesselId", "baseBottle"}, {"stockKind", "SodiumHydroxide"},
        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 5e-6}}, {{"baseBottle", "0"}})["accepted"] == true);
    CHECK(issue("8", "PlaceTool", {{"toolId", "pipette"}, {"sourceInventoryId", "acidBottle"},
        {"overflowSinkId", "waste"}, {"coordinateFrame", "lab"},
        {"geometryProfileHash", "pipette-25ml-research-v1"}, {"profileRevision", "1"}})["accepted"] == true);
    const auto before = json::parse(read_text(h));
    CHECK(issue("9", "AspiratePipette", {{"toolId", "pipette"},
        {"expectedInventoryRevision", "0"}, {"quantityM3", 2e-7}}, {{"acidBottle", "1"}})["accepted"] == true);
    CHECK(issue("10", "DispensePipette", {{"toolId", "pipette"},
        {"expectedInventoryRevision", "1"}, {"destinationInventoryId", "acidReceiver"},
        {"quantityM3", 1e-7}}, {{"acidReceiver", "0"}})["accepted"] == true);

    const auto changed = issue("11", "SwitchPipetteSource", {{"toolId", "pipette"},
        {"sourceInventoryId", "baseBottle"}, {"expectedInventoryRevision", "2"}},
        {{"baseBottle", "1"}});
    CHECK(changed["accepted"] == true);
    const auto switched = json::parse(read_text(h));
    if (!switched["tools"].empty()) {
        CHECK(switched["tools"][0]["sourceInventoryId"] == "baseBottle");
        CHECK(switched["tools"][0]["tipInventory"]["chlorideMol"].get<double>() > 0.0);
    }
    CHECK(issue("12", "AspiratePipette", {{"toolId", "pipette"},
        {"expectedInventoryRevision", "3"}, {"quantityM3", 1e-7}}, {{"baseBottle", "1"}})["accepted"] == true);
    CHECK(issue("13", "DispensePipette", {{"toolId", "pipette"},
        {"expectedInventoryRevision", "4"}, {"destinationInventoryId", "mixedReceiver"},
        {"quantityM3", 1e-7}}, {{"mixedReceiver", "0"}})["accepted"] == true);
    const auto after = json::parse(read_text(h));
    for (const auto& vessel : after["vessels"])
        if (vessel["id"] == "mixedReceiver") {
            CHECK(vessel["inventory"]["sodiumMol"].get<double>() > 0.0);
            CHECK(vessel["inventory"]["chlorideMol"].get<double>() > 0.0);
            CHECK(vessel["inventory"]["researchAdditiveVolumeM3"].get<double>() ==
                  doctest::Approx(1e-7));
        }
    for (const auto& key : {"researchAdditiveVolumeM3", "sodiumMol", "chlorideMol", "solventWaterKg"})
        CHECK(after["materialLedger"][key].get<double>() ==
              doctest::Approx(before["materialLedger"][key].get<double>()).epsilon(1e-10));

    const auto bad = issue("14", "SwitchPipetteSource", {{"toolId", "pipette"},
        {"sourceInventoryId", "missingBottle"}, {"expectedInventoryRevision", "5"}});
    CHECK(bad["accepted"] == false);
    CHECK(bad["code"] == "InvalidPipetteSource");
    CHECK(json::parse(read_text(h))["materialLedger"] == after["materialLedger"]);
    CHECK(plv_destroy(h) == PLV_OK);
}

TEST_CASE("P03_02 registered 100ml stock bottle pours through revised Native inventories without valve emulation") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    const auto issue = [&](const std::string& sequence, const std::string& type,
                           json payload, json revisions = json::object()) {
        submit(handle, command(sequence, type, std::move(payload), std::move(revisions)));
        return poll(handle);
    };
    CHECK(issue("1", "CreateVessel", {{"id", "bottle-stock"}, {"capacityM3", 1e-4}})["accepted"] == true);
    CHECK(issue("2", "CreateVessel", {{"id", "too-large"}, {"capacityM3", 2e-4}})["accepted"] == true);
    CHECK(issue("3", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-4}})["accepted"] == true);
    CHECK(issue("4", "CreateSink", {{"id", "waste"}})["accepted"] == true);
    CHECK(issue("5", "PrepareStock", {{"vesselId", "bottle-stock"}, {"stockKind", "SodiumChloride"},
        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 2e-6}}, {{"bottle-stock", "0"}})["accepted"] == true);
    const auto original = json::parse(read_text(handle));
    const auto bad = issue("6", "PlaceTool", {{"toolId", "oversize-bottle"},
        {"sourceInventoryId", "too-large"}, {"overflowSinkId", "waste"},
        {"coordinateFrame", "lab"}, {"geometryProfileHash", "bottle-100ml-research-v1"},
        {"profileRevision", "1"}});
    CHECK(bad["accepted"] == false);
    CHECK(json::parse(read_text(handle))["materialLedger"] == original["materialLedger"]);

    const auto placed = issue("7", "PlaceTool", {{"toolId", "bottle-1"},
        {"sourceInventoryId", "bottle-stock"}, {"overflowSinkId", "waste"},
        {"coordinateFrame", "lab"}, {"geometryProfileHash", "bottle-100ml-research-v1"},
        {"profileRevision", "1"}});
    CHECK(placed["accepted"] == true);
    if (placed["accepted"] == true) {
        const auto actuator = issue("8", "SetActuator", {{"toolId", "bottle-1"},
            {"expectedActuatorRevision", "0"}, {"actuator01", 1.0}});
        CHECK(actuator["accepted"] == false);
        CHECK(actuator["code"] == "BottleRequiresMeasuredPour");

        const auto poured = issue("9", "PourBottle", {{"toolId", "bottle-1"},
            {"destinationInventoryId", "receiver"}, {"quantityM3", 2e-8},
            {"expectedInventoryRevision", "0"}},
            {{"bottle-stock", "1"}, {"receiver", "0"}});
        CHECK(poured["accepted"] == true);
        const auto after = json::parse(read_text(handle));
        CHECK(after["tools"][0]["geometryProfileHash"] == "bottle-100ml-research-v1");
        CHECK(after["vessels"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>() ==
              doctest::Approx(1.96e-6).epsilon(1e-10));
        CHECK(after["vessels"][1]["inventory"]["chlorideMol"].get<double>() > 0.0);
        CHECK(after["tools"][0]["inventoryRevision"] == "1");
        CHECK(after["materialLedger"] == original["materialLedger"]);

        const auto stale = issue("10", "PourBottle", {{"toolId", "bottle-1"},
            {"destinationInventoryId", "receiver"}, {"quantityM3", 2e-8},
            {"expectedInventoryRevision", "0"}},
            {{"bottle-stock", "2"}, {"receiver", "1"}});
        CHECK(stale["accepted"] == false);
        CHECK(stale["code"] == "StaleInventoryRevision");
        CHECK(json::parse(read_text(handle))["materialLedger"] == original["materialLedger"]);
    }
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P03_02 distinct 25 ml pipette aspirates aliquot and dispenses conserved material") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1","scienceBudgetMode":"Bulk"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    const auto issue = [&](const std::string& sequence, const std::string& type,
                           const json& payload, const json& expected = json::object()) {
        submit(handle, command(sequence, type, payload, expected));
        return poll(handle);
    };
    CHECK(issue("1", "CreateVessel", {{"id", "bottle"}, {"capacityM3", 5e-5}})["accepted"] == true);
    CHECK(issue("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 5e-5}})["accepted"] == true);
    CHECK(issue("3", "CreateVessel", {{"id", "rinse"}, {"capacityM3", 5e-5}})["accepted"] == true);
    CHECK(issue("4", "CreateSink", {{"id", "waste"}})["accepted"] == true);
    CHECK(issue("5", "PrepareStock", {{"vesselId", "bottle"}, {"stockKind", "HydrochloricAcid"},
        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 4e-5}}, {{"bottle", "0"}})["accepted"] == true);
    CHECK(issue("6", "PrepareStock", {{"vesselId", "rinse"}, {"stockKind", "Water"},
        {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 2e-5}}, {{"rinse", "0"}})["accepted"] == true);
    const auto placed = issue("7", "PlaceTool", {{"toolId", "pipette"},
        {"sourceInventoryId", "bottle"}, {"overflowSinkId", "waste"},
        {"coordinateFrame", "lab"}, {"geometryProfileHash", "pipette-25ml-research-v1"},
        {"profileRevision", "1"}});
    CHECK(placed["accepted"] == true);
    const auto before = json::parse(read_text(handle));
    const auto actuator = issue("8", "SetActuator", {{"toolId", "pipette"},
        {"expectedActuatorRevision", "0"}, {"actuator01", 1.0}});
    CHECK(actuator["accepted"] == false);
    CHECK(actuator["code"] == "PipetteRequiresMeteredAction");
    const auto filled = issue("9", "AspiratePipette", {{"toolId", "pipette"},
        {"expectedInventoryRevision", "0"}, {"quantityM3", 3e-7}}, {{"bottle", "1"}});
    CHECK(filled["accepted"] == true);
    const auto full = json::parse(read_text(handle));
    if (!full["tools"].empty()) {
        CHECK(full["tools"][0]["tipInventory"]["researchAdditiveVolumeM3"].get<double>() ==
              doctest::Approx(3e-7));
        CHECK(full["vessels"][0]["inventory"]["researchAdditiveVolumeM3"].get<double>() ==
              doctest::Approx(3.97e-5));
    }
    const auto overflow = issue("10", "AspiratePipette", {{"toolId", "pipette"},
        {"expectedInventoryRevision", "1"}, {"quantityM3", 2.6e-5}}, {{"bottle", "2"}});
    CHECK(overflow["accepted"] == false);
    CHECK(overflow["code"] == "PipetteCapacityExceeded");
    const auto delivered = issue("11", "DispensePipette", {{"toolId", "pipette"},
        {"expectedInventoryRevision", "1"}, {"destinationInventoryId", "receiver"},
        {"quantityM3", 1e-7}}, {{"receiver", "0"}});
    CHECK(delivered["accepted"] == true);
    const auto after = json::parse(read_text(handle));
    if (!after["tools"].empty()) {
        CHECK(after["tools"][0]["tipInventory"]["researchAdditiveVolumeM3"].get<double>() ==
              doctest::Approx(2e-7));
        CHECK(after["tools"][0]["tipInventory"]["chlorideMol"].get<double>() > 0.0);
        for (const auto& vessel : after["vessels"])
            if (vessel["id"] == "receiver") {
                CHECK(vessel["inventory"]["researchAdditiveVolumeM3"].get<double>() ==
                      doctest::Approx(1e-7));
                CHECK(vessel["inventory"]["chlorideMol"].get<double>() > 0.0);
            }
    }
    for (const auto& key : {"researchAdditiveVolumeM3", "chlorideMol", "solventWaterKg"})
        CHECK(after["materialLedger"][key].get<double>() ==
              doctest::Approx(before["materialLedger"][key].get<double>()).epsilon(1e-10));
    const auto rinsed = issue("12", "RinseTool", {{"toolId", "pipette"},
        {"rinseSourceInventoryId", "rinse"}, {"wasteSinkId", "waste"},
        {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-7}}},
        {"expectedInventoryRevision", "2"}}, {{"rinse", "1"}});
    CHECK(rinsed["accepted"] == true);
    const auto cleaned = json::parse(read_text(handle));
    if (!cleaned["tools"].empty()) {
        CHECK(cleaned["tools"][0]["tipInventory"]["researchAdditiveVolumeM3"].get<double>() ==
              doctest::Approx(1e-7));
        CHECK(cleaned["sinks"][0]["inventory"]["chlorideMol"].get<double>() > 0.0);
    }
    for (const auto& key : {"researchAdditiveVolumeM3", "chlorideMol", "solventWaterKg"})
        CHECK(cleaned["materialLedger"][key].get<double>() ==
              doctest::Approx(before["materialLedger"][key].get<double>()).epsilon(1e-10));
    CHECK(plv_destroy(handle) == PLV_OK);
}

TEST_CASE("P03_02 rinse rejects solute-bearing stock without debiting the material ledger") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"branch-1"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);

    submit(handle, command("1", "CreateVessel", {{"id", "stock"}, {"capacityM3", 1e-5}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("2", "CreateVessel", {{"id", "rinse"}, {"capacityM3", 1e-5}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("3", "CreateSink", {{"id", "waste"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("4", "PrepareStock",
        {{"vesselId", "stock"}, {"stockKind", "SodiumChloride"},
         {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 4e-6}}, {{"stock", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("5", "PrepareStock",
        {{"vesselId", "rinse"}, {"stockKind", "Water"},
         {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 4e-6}}, {{"rinse", "0"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());
    submit(handle, command("6", "PlaceTool",
        {{"toolId", "burette"}, {"sourceInventoryId", "stock"}, {"overflowSinkId", "waste"},
         {"coordinateFrame", "lab"}, {"geometryProfileHash", "burette-50ml-research-v1"},
         {"profileRevision", "1"}}));
    REQUIRE(poll(handle)["accepted"].get<bool>());

    const auto before = json::parse(read_text(handle));
    submit(handle, command("7", "RinseTool",
        {{"toolId", "burette"}, {"rinseSourceInventoryId", "stock"}, {"wasteSinkId", "waste"},
         {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-6}}},
         {"expectedInventoryRevision", "0"}}, {{"stock", "1"}}));
    const auto denied = poll(handle);
    CHECK_FALSE(denied["accepted"].get<bool>());
    CHECK(denied["code"] == "InvalidRinseSource");
    const auto afterDenied = json::parse(read_text(handle));
    CHECK(afterDenied["vessels"] == before["vessels"]);
    CHECK(afterDenied["sinks"] == before["sinks"]);
    CHECK(afterDenied["tools"] == before["tools"]);
    CHECK(afterDenied["materialLedger"] == before["materialLedger"]);

    submit(handle, command("8", "RinseTool",
        {{"toolId", "burette"}, {"rinseSourceInventoryId", "rinse"}, {"wasteSinkId", "waste"},
         {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-6}}},
         {"expectedInventoryRevision", "0"}}, {{"rinse", "1"}}));
    CHECK(poll(handle)["accepted"].get<bool>());
    const auto afterClean = json::parse(read_text(handle));
    CHECK(afterClean["materialLedger"]["researchAdditiveVolumeM3"].get<double>() ==
          doctest::Approx(before["materialLedger"]["researchAdditiveVolumeM3"].get<double>()).epsilon(1e-12));
    CHECK(afterClean["materialLedger"]["sodiumMol"].get<double>() ==
          doctest::Approx(before["materialLedger"]["sodiumMol"].get<double>()).epsilon(1e-12));
    const auto retained = afterClean["tools"][0]["tipInventory"];
    CHECK(retained["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(5e-8));
    CHECK(retained["sodiumMol"].get<double>() == doctest::Approx(0.0));
    CHECK(afterClean["sinks"][0]["inventory"]["sodiumMol"].get<double>() == doctest::Approx(0.0));
    CHECK(plv_destroy(handle) == PLV_OK);
}


TEST_CASE("P03_02 bottle film is conserved through measured pours and real water rinse") {
    std::uint64_t handle = 0;
    const std::string config = R"({"schemaVersion":1,"branchId":"bottle-film","scienceBudgetMode":"Bulk"})";
    REQUIRE(plv_create(config.data(), static_cast<std::uint32_t>(config.size()), &handle) == PLV_OK);
    const auto issue = [&](const std::string& sequence, const std::string& type,
                           json payload, json expected = json::object()) {
        submit(handle, command(sequence, type, std::move(payload), std::move(expected)));
        return poll(handle);
    };
    REQUIRE(issue("1", "CreateVessel", {{"id", "stock"}, {"capacityM3", 1e-4}})["accepted"] == true);
    REQUIRE(issue("2", "CreateVessel", {{"id", "receiver"}, {"capacityM3", 1e-4}})["accepted"] == true);
    REQUIRE(issue("3", "CreateVessel", {{"id", "clean-water"}, {"capacityM3", 1e-4}})["accepted"] == true);
    REQUIRE(issue("4", "CreateSink", {{"id", "waste"}})["accepted"] == true);
    REQUIRE(issue("5", "PrepareStock", {{"vesselId", "stock"}, {"stockKind", "SodiumChloride"},
        {"concentrationMolPerL", 0.1}, {"referenceVolumeM3", 2e-6}}, {{"stock", "0"}})["accepted"] == true);
    REQUIRE(issue("6", "PrepareStock", {{"vesselId", "clean-water"}, {"stockKind", "Water"},
        {"concentrationMolPerL", 0.0}, {"referenceVolumeM3", 1e-6}}, {{"clean-water", "0"}})["accepted"] == true);
    REQUIRE(issue("7", "PlaceTool", {{"toolId", "bottle"}, {"sourceInventoryId", "stock"},
        {"overflowSinkId", "waste"}, {"coordinateFrame", "lab"},
        {"geometryProfileHash", "bottle-100ml-research-v1"}, {"profileRevision", "1"}})["accepted"] == true);
    const auto initial = json::parse(read_text(handle));
    REQUIRE(issue("8", "PourBottle", {{"toolId", "bottle"},
        {"destinationInventoryId", "receiver"}, {"quantityM3", 2e-8},
        {"expectedInventoryRevision", "0"}}, {{"stock", "1"}, {"receiver", "0"}})["accepted"] == true);
    const auto afterPour = json::parse(read_text(handle));
    const auto oldFilm = afterPour["tools"][0]["tipInventory"];
    CHECK(oldFilm["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(2e-8));
    CHECK(oldFilm["chlorideMol"].get<double>() > 0.0);
    const auto volumeOf = [](const json& snapshot, const std::string& id) {
        for (const auto& vessel : snapshot["vessels"])
            if (vessel["id"] == id) return vessel["inventory"]["researchAdditiveVolumeM3"].get<double>();
        return -1.0;
    };
    CHECK(volumeOf(afterPour, "stock") == doctest::Approx(1.96e-6));
    CHECK(volumeOf(afterPour, "receiver") == doctest::Approx(2e-8));
    CHECK(afterPour["materialLedger"] == initial["materialLedger"]);

    const auto denied = issue("9", "RinseTool", {{"toolId", "bottle"},
        {"rinseSourceInventoryId", "stock"}, {"wasteSinkId", "waste"},
        {"expectedInventoryRevision", "1"},
        {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-7}}}}, {{"stock", "2"}});
    CHECK(denied["accepted"] == false);
    CHECK(denied["code"] == "InvalidRinseSource");
    CHECK(json::parse(read_text(handle))["materialLedger"] == initial["materialLedger"]);

    const auto washed = issue("10", "RinseTool", {{"toolId", "bottle"},
        {"rinseSourceInventoryId", "clean-water"}, {"wasteSinkId", "waste"},
        {"expectedInventoryRevision", "1"},
        {"quantity", {{"basis", "LiquidVolumeM3"}, {"value", 1e-7}}}}, {{"clean-water", "1"}});
    REQUIRE(washed["accepted"] == true);
    const auto afterWash = json::parse(read_text(handle));
    const auto newFilm = afterWash["tools"][0]["tipInventory"];
    CHECK(newFilm["researchAdditiveVolumeM3"].get<double>() == doctest::Approx(2e-8));
    CHECK(newFilm["chlorideMol"].get<double>() > 0.0);
    CHECK(newFilm["chlorideMol"].get<double>() < oldFilm["chlorideMol"].get<double>());
    CHECK(afterWash["sinks"][0]["inventory"]["chlorideMol"].get<double>() > 0.0);
    CHECK(afterWash["materialLedger"] == initial["materialLedger"]);
    CHECK(afterWash["tools"][0]["inventoryRevision"] == "2");
    CHECK(plv_destroy(handle) == PLV_OK);
}
