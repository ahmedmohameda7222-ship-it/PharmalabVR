#include "plv/abi.h"

#include "plv/state_executor.hpp"
#include "plv/tool_models.hpp"
#include "plv/scheduler.hpp"
#include "plv/solver_worker.hpp"
#include "plv/solver.hpp"
#include "json.hpp"

#include <algorithm>
#include <array>
#include <atomic>
#include <cmath>
#include <cstring>
#include <deque>
#include <functional>
#include <limits>
#include <memory>
#include <mutex>
#include <optional>
#include <sstream>
#include <string>
#include <thread>
#include <unordered_map>
#include <unordered_set>
#include <vector>

namespace {
using json = nlohmann::json;

struct Context {
    struct ToolInput {
        std::uint64_t sampleSequence = 0;
        std::uint64_t captureMonotonicNs = 0;
        double actuator01 = 0.0;
        bool trackingValid = false;
        std::vector<plv::CaptureFraction> captures;
    };

    struct ToolState {
        std::string sourceInventoryId;
        std::string overflowSinkId;
        std::string coordinateFrame;
        std::string geometryProfileHash;
        std::uint64_t profileRevision = 0;
        std::uint64_t toolRevision = 1;
        std::uint64_t actuatorRevision = 0;
        std::uint64_t inventoryRevision = 0;
        double actuator01 = 0.0;
        plv::MaterialState tipInventory;
        plv::MaterialState residualInventory;
        plv::MaterialState inFlightInventory;
        std::optional<ToolInput> latestInput;
    };

    struct CheckpointState {
        plv::SessionSnapshot snapshot;
        std::unordered_map<std::string, ToolState> tools;
        std::string mode;
    };

    explicit Context(std::string branch, std::string databasePath = {}, std::string databaseIdentity = {})
        : executor(std::move(branch)), scheduler({5e-8, 100'000'000ULL, 16U}),
          databasePath(std::move(databasePath)), databaseIdentity(std::move(databaseIdentity)) {
        if (!this->databasePath.empty()) {
            worker = std::make_unique<plv::SolverWorker>(16U,
                [path = this->databasePath, identity = this->databaseIdentity,
                 engine = std::shared_ptr<plv::IPhreeqcAdapter>{}](const plv::SolveRequest& request) mutable {
                    if (!engine) engine = std::make_shared<plv::IPhreeqcAdapter>(path, identity);
                    return engine->solve(request);
                });
        }
    }
    plv::StateExecutor executor;
    plv::ObservationScheduler scheduler;
    std::unique_ptr<plv::SolverWorker> worker;
    std::string databasePath;
    std::string databaseIdentity;
    std::unordered_map<std::string, plv::SolveResult> scienceResults;
    std::deque<std::string> events;
    std::unordered_map<std::uint64_t, std::pair<std::string, std::string>> receipts;
    std::unordered_map<std::string, std::uint64_t> inputSequences;
    std::unordered_map<std::string, ToolState> tools;
    std::unordered_map<std::string, CheckpointState> checkpoints;
    std::uint64_t nextCommandSequence = 1;
    std::string mode = "Desktop";
    bool holdActive = false;
    std::string holdReason;
    bool recoveryReady = false;
    std::uint64_t lastMonotonicNowNs = 0;
    std::thread::id ownerThread = std::this_thread::get_id();
    std::atomic<bool> active{true};
    std::mutex mutex;
};

void dispatchScience(Context& context) {
    if (!context.worker) return;
    while (context.worker->outstanding() < 16U) {
        auto request = context.scheduler.takeNext();
        if (!request) break;
        const auto snapshot = context.executor.snapshot();
        const auto vessel = snapshot.vessels.find(request->vesselId);
        if (vessel == snapshot.vessels.end() || vessel->second.inventory.referenceVolumeM3 <= 0.0) continue;
        const auto& material = vessel->second.inventory;
        const auto accepted = context.worker->submit({*request,
            {material.solventWaterKg, material.referenceVolumeM3, material.sodiumMol,
             material.chlorideMol, material.acetateMol, 25.0}});
        if (!accepted.accepted) break;
    }
}

void offerChangedScience(Context& context, const plv::SessionSnapshot& before, std::uint64_t simulationNowNs) {
    if (!context.worker) return;
    const auto after = context.executor.snapshot();
    for (const auto& [id, vessel] : after.vessels) {
        const auto old = before.vessels.find(id);
        if (old != before.vessels.end() && old->second.materialRevision == vessel.materialRevision) continue;
        context.scheduler.markMaterialRevision(id, vessel.materialRevision, simulationNowNs);
        if (vessel.inventory.referenceVolumeM3 > 0.0) {
            context.scheduler.offer({id, "pH", vessel.materialRevision, simulationNowNs, 0.0});
        }
    }
    dispatchScience(context);
}

void collectScience(Context& context, std::uint64_t simulationNowNs) {
    if (!context.worker) return;
    while (const auto completion = context.worker->poll()) {
        const auto& request = completion->observation;
        const auto& result = completion->result;
        context.scheduler.complete(request, {plv::ObservationSupport::Supported,
            plv::ObservationMaturity::Research, result.succeeded, result.pH});
        const auto* record = context.scheduler.observation(request.vesselId, request.observable);
        if (record != nullptr && record->solvedRevision == request.materialRevision) {
            context.scienceResults.insert_or_assign(request.vesselId, result);
            if (record->freshness == plv::ObservationFreshness::Current) {
                context.scheduler.markSolved(request.vesselId, request.materialRevision, simulationNowNs);
            }
        }
    }
    dispatchScience(context);
}

std::mutex registryMutex;
std::unordered_map<std::uint64_t, std::shared_ptr<Context>> contexts;
std::uint64_t nextHandle = 1;
constexpr std::uint32_t maxInputSize = 16U * 1024U * 1024U;
constexpr std::size_t exportMutationReserve = 64U * 1024U;
constexpr std::size_t maxTools = 64U;
// One active session plus at most three contexts retiring a live solver call.
constexpr std::size_t maxSessionContexts = 4U;
constexpr std::uint64_t inputStaleCutoffNs = 100'000'000ULL;
constexpr double transportTickS = 0.020;

bool freshNeutralInputs(const Context& context, std::uint64_t nowNs) {
    for (const auto& [toolId, tool] : context.tools) {
        const auto& input = tool.latestInput;
        if (!input || !input->trackingValid || input->actuator01 > 1e-9 ||
            input->captureMonotonicNs > nowNs ||
            nowNs - input->captureMonotonicNs > inputStaleCutoffNs) return false;
    }
    return true;
}

std::shared_ptr<Context> getContext(std::uint64_t handle) {
    std::lock_guard<std::mutex> lock(registryMutex);
    const auto found = contexts.find(handle);
    return found == contexts.end() ? nullptr : found->second;
}

int copyString(const std::string& value, char* out, std::uint32_t capacity, std::uint32_t* required) {
    if (required == nullptr || value.size() >= std::numeric_limits<std::uint32_t>::max()) {
        return PLV_INVALID_ARGUMENT;
    }
    *required = static_cast<std::uint32_t>(value.size() + 1U);
    if (out == nullptr || capacity < *required) {
        return PLV_BUFFER_TOO_SMALL;
    }
    std::memcpy(out, value.c_str(), *required);
    return PLV_OK;
}

bool validInput(const char* input, std::uint32_t size) {
    return input != nullptr && size > 0U && size <= maxInputSize && std::memchr(input, '\0', size) == nullptr;
}

json materialJson(const plv::MaterialState& material) {
    return {
        {"solventWaterKg", material.solventWaterKg},
        {"sodiumMol", material.sodiumMol},
        {"chlorideMol", material.chlorideMol},
        {"acetateMol", material.acetateMol},
        {"researchAdditiveVolumeM3", material.referenceVolumeM3},
        {"preparationId", material.preparationId},
        {"provenanceId", material.provenanceId}
    };
}

json toolJson(const std::string& id, const Context::ToolState& tool) {
    return {
        {"id", id},
        {"sourceInventoryId", tool.sourceInventoryId},
        {"overflowSinkId", tool.overflowSinkId},
        {"coordinateFrame", tool.coordinateFrame},
        {"geometryProfileHash", tool.geometryProfileHash},
        {"profileRevision", std::to_string(tool.profileRevision)},
        {"toolRevision", std::to_string(tool.toolRevision)},
        {"actuatorRevision", std::to_string(tool.actuatorRevision)},
        {"inventoryRevision", std::to_string(tool.inventoryRevision)},
        {"actuator01", tool.actuator01},
        {"tipInventory", materialJson(tool.tipInventory)},
        {"residualInventory", materialJson(tool.residualInventory)},
        {"inFlightInventory", materialJson(tool.inFlightInventory)}
    };
}

plv::MaterialState parseMaterial(const json& value) {
    if (!value.is_object()) throw std::invalid_argument("material must be an object");
    static constexpr std::array<const char*, 7> allowedKeys{
        "solventWaterKg", "sodiumMol", "chlorideMol", "acetateMol",
        "researchAdditiveVolumeM3", "preparationId", "provenanceId"};
    for (const auto& item : value.items()) {
        if (std::find(allowedKeys.begin(), allowedKeys.end(), item.key()) == allowedKeys.end()) {
            throw std::invalid_argument("unknown material pool or field");
        }
    }
    plv::MaterialState material;
    material.solventWaterKg = value.at("solventWaterKg").get<double>();
    material.sodiumMol = value.at("sodiumMol").get<double>();
    material.chlorideMol = value.at("chlorideMol").get<double>();
    material.acetateMol = value.at("acetateMol").get<double>();
    material.referenceVolumeM3 = value.at("researchAdditiveVolumeM3").get<double>();
    material.preparationId = value.value("preparationId", "");
    material.provenanceId = value.value("provenanceId", "");
    return material;
}

json snapshotJson(const plv::SessionSnapshot& snapshot) {
    json vessels = json::array();
    std::vector<std::string> vesselIds;
    vesselIds.reserve(snapshot.vessels.size());
    for (const auto& item : snapshot.vessels) vesselIds.push_back(item.first);
    std::sort(vesselIds.begin(), vesselIds.end());
    for (const auto& id : vesselIds) {
        const auto& vessel = snapshot.vessels.at(id);
        vessels.push_back({
            {"id", vessel.id},
            {"capacityM3", vessel.capacityM3},
            {"materialRevision", std::to_string(vessel.materialRevision)},
            {"inventory", materialJson(vessel.inventory)}
        });
    }
    json sinks = json::array();
    std::vector<std::string> sinkIds;
    sinkIds.reserve(snapshot.sinks.size());
    for (const auto& item : snapshot.sinks) sinkIds.push_back(item.first);
    std::sort(sinkIds.begin(), sinkIds.end());
    for (const auto& id : sinkIds) sinks.push_back({{"id", id}, {"inventory", materialJson(snapshot.sinks.at(id))}});
    return {
        {"schemaVersion", 1},
        {"branchId", snapshot.branchId},
        {"eventSequence", std::to_string(snapshot.eventSequence)},
        {"simulationTimeS", snapshot.simulationTimeS},
        {"paused", snapshot.paused},
        {"vessels", vessels},
        {"sinks", sinks}
    };
}

json contextSnapshotJson(const Context& context) {
    const auto authoritative = context.executor.snapshot();
    auto result = snapshotJson(authoritative);
    result["mode"] = context.mode;
    result["hold"] = {
        {"active", context.holdActive},
        {"reason", context.holdReason},
        {"recoveryReady", context.recoveryReady}
    };
    result["tools"] = json::array();
    std::vector<std::string> toolIds;
    toolIds.reserve(context.tools.size());
    for (const auto& item : context.tools) toolIds.push_back(item.first);
    std::sort(toolIds.begin(), toolIds.end());
    for (const auto& id : toolIds) {
        result["tools"].push_back(toolJson(id, context.tools.at(id)));
    }
    auto ledger = context.executor.totalLedger();
    for (const auto& id : toolIds) {
        const auto& tool = context.tools.at(id);
        ledger.add(tool.tipInventory);
        ledger.add(tool.residualInventory);
        ledger.add(tool.inFlightInventory);
    }
    result["materialLedger"] = materialJson(ledger);
    result["inputWatermarks"] = json::array();
    for (const auto& id : toolIds) {
        const auto found = context.inputSequences.find(id);
        result["inputWatermarks"].push_back({
            {"toolId", id}, {"sampleSequence", std::to_string(found == context.inputSequences.end() ? 0U : found->second)}
        });
    }
    result["observations"] = json::array();
    std::vector<std::string> vesselIds;
    for (const auto& item : authoritative.vessels) vesselIds.push_back(item.first);
    std::sort(vesselIds.begin(), vesselIds.end());
    for (const auto& id : vesselIds) {
        const auto& vessel = authoritative.vessels.at(id);
        const auto* record = context.scheduler.observation(id, "pH");
        const auto solved = context.scienceResults.find(id);
        json observation = {
            {"vesselId", id}, {"observableId", "pH"}, {"unit", "pH"},
            {"maturity", "Research"}, {"modelId", "IPhreeqc-minteq-v4-research"},
            {"databaseIdentity", context.databaseIdentity},
            {"asOfMaterialRevision", record ? std::to_string(record->solvedRevision) : "0"},
            {"support", vessel.inventory.referenceVolumeM3 <= 0.0 ? "Unsupported" : "Supported"},
            {"freshness", "Absent"}, {"computationState", "Pending"}
        };
        if (vessel.inventory.referenceVolumeM3 <= 0.0) {
            observation["computationState"] = "Absent";
        } else if (!context.worker) {
            observation["computationState"] = "Failed";
            observation["error"] = "ScientificDatabaseUnavailable";
        } else if (record != nullptr) {
            observation["freshness"] = record->freshness == plv::ObservationFreshness::Current ? "Current" :
                record->freshness == plv::ObservationFreshness::Stale ? "Stale" : "Pending";
            observation["computationState"] = record->freshness == plv::ObservationFreshness::Pending
                ? "Pending" : record->succeeded ? "Ready" : "Failed";
            if (record->succeeded && std::isfinite(record->value)) observation["value"] = record->value;
            if (solved != context.scienceResults.end()) {
                observation["engineVersion"] = solved->second.engineVersion;
                observation["databaseIdentity"] = solved->second.databaseIdentity;
                if (!solved->second.error.empty()) observation["error"] = solved->second.error;
            }
        }
        result["observations"].push_back(std::move(observation));
    }
    return result;
}

json exportJson(const Context& context) {
    auto result = contextSnapshotJson(context);
    result["scientificPackage"] = {{"path", context.databasePath}, {"identity", context.databaseIdentity}};
    result["nextCommandSequence"] = std::to_string(context.nextCommandSequence);
    result["commandReceipts"] = json::array();
    std::vector<std::uint64_t> sequences;
    sequences.reserve(context.receipts.size());
    for (const auto& receipt : context.receipts) sequences.push_back(receipt.first);
    std::sort(sequences.begin(), sequences.end());
    for (const auto sequence : sequences) {
        const auto& receipt = context.receipts.at(sequence);
        result["commandReceipts"].push_back({
            {"commandSequence", std::to_string(sequence)},
            {"canonicalCommand", json::parse(receipt.first)},
            {"terminalOutcome", json::parse(receipt.second)}
        });
    }
    result["inputSequences"] = json::object();
    for (const auto& item : context.inputSequences) {
        result["inputSequences"][item.first] = std::to_string(item.second);
    }
    result["checkpoints"] = json::array();
    std::vector<std::string> checkpointIds;
    checkpointIds.reserve(context.checkpoints.size());
    for (const auto& item : context.checkpoints) checkpointIds.push_back(item.first);
    std::sort(checkpointIds.begin(), checkpointIds.end());
    for (const auto& checkpointId : checkpointIds) {
        const auto& checkpoint = context.checkpoints.at(checkpointId);
        json tools = json::array();
        std::vector<std::string> ids;
        ids.reserve(checkpoint.tools.size());
        for (const auto& item : checkpoint.tools) ids.push_back(item.first);
        std::sort(ids.begin(), ids.end());
        for (const auto& id : ids) tools.push_back(toolJson(id, checkpoint.tools.at(id)));
        result["checkpoints"].push_back({
            {"checkpointId", checkpointId},
            {"snapshot", snapshotJson(checkpoint.snapshot)},
            {"tools", std::move(tools)},
            {"mode", checkpoint.mode}
        });
    }
    return result;
}

std::optional<std::uint64_t> decimalSequence(const std::string& value) {
    if (value.empty() || value.size() > 20 || (value.size() > 1 && value.front() == '0')) return std::nullopt;
    std::uint64_t result = 0;
    for (const char character : value) {
        if (character < '0' || character > '9') return std::nullopt;
        const auto digit = static_cast<std::uint64_t>(character - '0');
        if (result > (std::numeric_limits<std::uint64_t>::max() - digit) / 10U) return std::nullopt;
        result = result * 10U + digit;
    }
    return result;
}

plv::StockKind stockKind(const std::string& value) {
    if (value == "Water") return plv::StockKind::Water;
    if (value == "HydrochloricAcid") return plv::StockKind::HydrochloricAcid;
    if (value == "SodiumHydroxide") return plv::StockKind::SodiumHydroxide;
    if (value == "SodiumChloride") return plv::StockKind::SodiumChloride;
    if (value == "AceticAcid") return plv::StockKind::AceticAcid;
    if (value == "SodiumAcetate") return plv::StockKind::SodiumAcetate;
    throw std::invalid_argument("unknown stock kind");
}

std::optional<plv::TransferQuantityBasis> transferQuantityBasis(const std::string& value) {
    if (value == "WaterMassKg") return plv::TransferQuantityBasis::WaterMassKg;
    if (value == "LiquidVolumeM3") return plv::TransferQuantityBasis::LiquidVolumeM3;
    return std::nullopt;
}

std::uint64_t revision(const json& revisions, const std::string& id) {
    const auto value = revisions.at(id).get<std::string>();
    const auto parsed = decimalSequence(value);
    if (!parsed) throw std::invalid_argument("invalid revision");
    return *parsed;
}

json outcomeJson(const json& command, const plv::CommandOutcome& outcome) {
    return {
        {"schemaVersion", 1}, {"branchId", command.at("branchId")},
        {"commandSequence", command.at("commandSequence")}, {"type", "CommandOutcome"},
        {"accepted", outcome.accepted}, {"code", outcome.code}, {"message", outcome.message}
    };
}

plv::SessionSnapshot parseSnapshot(const json& parsed) {
    if (parsed.at("schemaVersion").get<int>() != 1 || !parsed.at("vessels").is_array() ||
        !parsed.at("sinks").is_array()) {
        throw std::invalid_argument("invalid snapshot schema");
    }
    plv::SessionSnapshot snapshot;
    snapshot.branchId = parsed.at("branchId").get<std::string>();
    const auto eventSequence = decimalSequence(parsed.at("eventSequence").get<std::string>());
    if (!eventSequence) throw std::invalid_argument("invalid event sequence");
    snapshot.eventSequence = *eventSequence;
    snapshot.simulationTimeS = parsed.at("simulationTimeS").get<double>();
    snapshot.paused = parsed.value("paused", false);
    for (const auto& item : parsed.at("vessels")) {
        plv::VesselState vessel;
        vessel.id = item.at("id").get<std::string>();
        vessel.capacityM3 = item.at("capacityM3").get<double>();
        const auto materialRevision = decimalSequence(item.at("materialRevision").get<std::string>());
        if (!materialRevision) throw std::invalid_argument("invalid material revision");
        vessel.materialRevision = *materialRevision;
        vessel.inventory = parseMaterial(item.at("inventory"));
        if (!snapshot.vessels.emplace(vessel.id, vessel).second) throw std::invalid_argument("duplicate vessel");
    }
    for (const auto& item : parsed.at("sinks")) {
        const auto id = item.at("id").get<std::string>();
        if (!snapshot.sinks.emplace(id, parseMaterial(item.at("inventory"))).second) {
            throw std::invalid_argument("duplicate sink");
        }
    }
    return snapshot;
}
}  // namespace

extern "C" {

std::uint32_t plv_abi_version(void) {
    return 1U;
}

std::int32_t plv_create(const char* config, std::uint32_t size, std::uint64_t* handle) {
    if (handle == nullptr) {
        return PLV_INVALID_ARGUMENT;
    }
    *handle = 0U;
    if (!validInput(config, size)) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        const auto parsed = json::parse(config, config + size);
        if (!parsed.contains("schemaVersion") || !parsed["schemaVersion"].is_number_integer()) {
            return PLV_INVALID_ARGUMENT;
        }
        if (parsed["schemaVersion"].get<int>() != 1) {
            return PLV_UNSUPPORTED_VERSION;
        }
        const auto branch = parsed.at("branchId").get<std::string>();
        const auto databasePath = parsed.value("databasePath", std::string{});
        const auto databaseIdentity = parsed.value("databaseIdentity", std::string{});
        if ((!databasePath.empty() && databaseIdentity.empty()) ||
            (databasePath.empty() && !databaseIdentity.empty()) ||
            databasePath.size() > 1024U || databaseIdentity.size() > 128U) return PLV_INVALID_ARGUMENT;
        const auto initialMode = parsed.value("initialMode", "Desktop");
        if (initialMode != "Desktop" && initialMode != "VR") return PLV_INVALID_ARGUMENT;
        std::lock_guard<std::mutex> lock(registryMutex);
        if (contexts.size() >= maxSessionContexts) return PLV_BUSY;
        for (const auto& [existingHandle, existing] : contexts)
            if (existing->active.load()) return PLV_BUSY;
        auto context = std::make_shared<Context>(branch, databasePath, databaseIdentity);
        context->mode = initialMode;
        const auto assigned = nextHandle++;
        contexts.emplace(assigned, std::move(context));
        *handle = assigned;
        return PLV_OK;
    } catch (const json::exception&) {
        return PLV_INVALID_ARGUMENT;
    } catch (const std::invalid_argument&) {
        return PLV_INVALID_ARGUMENT;
    } catch (...) {
        return PLV_INTERNAL_ERROR;
    }
}

std::int32_t plv_destroy(std::uint64_t handle) {
    std::shared_ptr<Context> retired;
    {
        std::lock_guard<std::mutex> lock(registryMutex);
        const auto found = contexts.find(handle);
        if (found == contexts.end()) return PLV_INVALID_HANDLE;
        std::lock_guard<std::mutex> sessionLock(found->second->mutex);
        if (found->second->worker && !found->second->worker->requestStop()) {
            found->second->active.store(false);
            return PLV_BUSY;
        }
        retired = std::move(found->second);
        contexts.erase(found);
    }
    // Join only after the current engine call has returned, outside registry locks.
    retired.reset();
    return PLV_OK;
}

std::int32_t plv_submit(std::uint64_t handle, const char* command, std::uint32_t size) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    if (!context->active.load() || context->ownerThread != std::this_thread::get_id()) return PLV_BUSY;
    if (!validInput(command, size)) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        const auto parsed = json::parse(command, command + size);
        if (parsed.value("schemaVersion", 0) != 1) {
            return PLV_UNSUPPORTED_VERSION;
        }
        std::lock_guard<std::mutex> lock(context->mutex);
        if (!context->active.load()) return PLV_BUSY;
        if (parsed.at("branchId").get<std::string>() != context->executor.snapshot().branchId) {
            return PLV_INVALID_ARGUMENT;
        }
        const auto sequenceText = parsed.at("commandSequence").get<std::string>();
        const auto sequence = decimalSequence(sequenceText);
        if (!sequence) return PLV_INVALID_ARGUMENT;
        const auto type = parsed.at("type").get<std::string>();
        if (context->events.size() >= 256U) {
            return PLV_BUSY;
        }
        const auto canonical = parsed.dump();
        const auto existing = context->receipts.find(*sequence);
        if (existing != context->receipts.end()) {
            if (existing->second.first == canonical) {
                context->events.push_back(existing->second.second);
            } else {
                context->events.push_back(outcomeJson(parsed, {false, "CommandIdentityConflict", "identity payload differs"}).dump());
            }
            return PLV_OK;
        }
        if (*sequence != context->nextCommandSequence) {
            const auto code = *sequence > context->nextCommandSequence ? "CommandSequenceGap" : "CommandIdentityUnknown";
            context->events.push_back(outcomeJson(parsed, {false, code, "command is not the next ordered identity"}).dump());
            return PLV_OK;
        }

        // A command identity is durable only if the resulting session can be imported.
        // Reserve room for its receipt and the bounded state mutation before dispatch.
        const auto serializedSize = exportJson(*context).dump().size();
        if (canonical.size() > maxInputSize - exportMutationReserve ||
            serializedSize > maxInputSize - exportMutationReserve - canonical.size()) {
            return PLV_BUSY;
        }

        const auto beforeCommand = context->executor.snapshot();
        const auto& payload = parsed.at("payload");
        const auto& revisions = parsed.at("expectedMaterialRevisions");
        plv::CommandOutcome outcome;
        if (context->holdActive &&
            (type == "CreateVessel" || type == "CreateSink" || type == "PrepareStock" ||
             type == "TransferFixed" || type == "DisposeContents" || type == "RinseTool")) {
            context->recoveryReady = false;
            outcome = {false, "SessionHeld", "material operations require explicit recovery"};
        } else if (type == "CreateVessel") {
            outcome = context->executor.createVessel(payload.at("id").get<std::string>(), payload.at("capacityM3").get<double>());
        } else if (type == "CreateSink") {
            outcome = context->executor.createSink(payload.at("id").get<std::string>());
        } else if (type == "PrepareStock") {
            const auto vesselId = payload.at("vesselId").get<std::string>();
            const auto snapshot = context->executor.snapshot();
            const auto vessel = snapshot.vessels.find(vesselId);
            if (!revisions.is_object() || revisions.size() != 1U || !revisions.contains(vesselId) ||
                vessel == snapshot.vessels.end() ||
                revision(revisions, vesselId) != vessel->second.materialRevision) {
                outcome = {false, "StaleRevision", "stock preparation revision does not match touched inventory"};
            } else {
                outcome = context->executor.prepareStock(
                    vesselId, stockKind(payload.at("stockKind").get<std::string>()),
                    payload.at("concentrationMolPerL").get<double>(), payload.at("referenceVolumeM3").get<double>());
            }
        } else if (type == "TransferFixed") {
            const auto sourceId = payload.at("sourceInventoryId").get<std::string>();
            const auto basis = transferQuantityBasis(payload.at("quantity").at("basis").get<std::string>());
            if (payload.at("sourceRegion").get<std::string>() != "Homogeneous" ||
                payload.at("selection").get<std::string>() != "HomogeneousAqueousLiquid" || !basis ||
                !payload.at("captureFractions").is_array()) {
                outcome = {false, "InvalidTransfer", "unsupported source region, selection, or quantity basis"};
            } else {
                std::vector<plv::CaptureFraction> captures;
                std::unordered_map<std::string, std::uint64_t> expected;
                expected.emplace(sourceId, revision(revisions, sourceId));
                for (const auto& capture : payload.at("captureFractions")) {
                    const auto destinationId = capture.at("destinationInventoryId").get<std::string>();
                    captures.push_back({destinationId, capture.at("fraction").get<double>()});
                    expected.emplace(destinationId, revision(revisions, destinationId));
                }
                outcome = context->executor.transferFixed(
                    {sourceId,
                     *basis,
                     payload.at("quantity").at("value").get<double>(),
                     std::move(captures),
                     payload.at("overflowSinkId").get<std::string>()},
                    expected);
            }
        } else if (type == "PlaceTool") {
            const auto toolId = payload.at("toolId").get<std::string>();
            const auto sourceId = payload.at("sourceInventoryId").get<std::string>();
            const auto sinkId = payload.at("overflowSinkId").get<std::string>();
            const auto frame = payload.at("coordinateFrame").get<std::string>();
            const auto profile = payload.at("geometryProfileHash").get<std::string>();
            const auto profileRevision = decimalSequence(payload.at("profileRevision").get<std::string>());
            const auto snapshot = context->executor.snapshot();
            if (!plv::isValidId(toolId) || !profileRevision || *profileRevision == 0U || frame != "lab" ||
                profile != plv::BuretteProfile::researchDefault().id || snapshot.vessels.count(sourceId) == 0U ||
                snapshot.sinks.count(sinkId) == 0U) {
                outcome = {false, "InvalidToolPlacement", "tool identity, profile, source, sink, or frame is invalid"};
            } else if (context->tools.count(toolId) != 0U) {
                outcome = {false, "ToolIdentityConflict", "tool identity already exists"};
            } else if (context->tools.size() >= maxTools) {
                outcome = {false, "ToolLimitReached", "tool inventory limit reached"};
            } else {
                context->tools.emplace(toolId, Context::ToolState{
                    sourceId, sinkId, frame, profile, *profileRevision, 1U, 0U, 0U, 0.0, {}, {}, {}, std::nullopt});
                outcome = {true, "Accepted", ""};
            }
        } else if (type == "SetActuator") {
            const auto toolId = payload.at("toolId").get<std::string>();
            const auto expectedActuatorRevision = decimalSequence(payload.at("expectedActuatorRevision").get<std::string>());
            const double actuator = payload.at("actuator01").get<double>();
            const auto found = context->tools.find(toolId);
            if (found == context->tools.end() || !expectedActuatorRevision ||
                *expectedActuatorRevision != found->second.actuatorRevision) {
                outcome = {false, "StaleActuatorRevision", "tool actuator revision changed"};
            } else if (!std::isfinite(actuator) || actuator < 0.0 || actuator > 1.0) {
                outcome = {false, "InvalidActuator", "actuator must be finite and within [0,1]"};
            } else {
                found->second.actuator01 = actuator;
                ++found->second.actuatorRevision;
                found->second.latestInput.reset();
                outcome = {true, "Accepted", ""};
            }
        } else if (type == "DisposeContents") {
            const auto sourceId = payload.at("sourceInventoryId").get<std::string>();
            const auto sinkId = payload.at("sinkInventoryId").get<std::string>();
            const auto snapshot = context->executor.snapshot();
            const auto source = snapshot.vessels.find(sourceId);
            if (source == snapshot.vessels.end() || snapshot.sinks.count(sinkId) == 0U) {
                outcome = {false, "InvalidDisposal", "source vessel or sink is not registered"};
            } else if (source->second.inventory.referenceVolumeM3 <= 0.0) {
                outcome = {false, "AlreadyEmpty", "source inventory is empty"};
            } else {
                outcome = context->executor.transferFixed(
                    {sourceId, plv::TransferQuantityBasis::LiquidVolumeM3,
                     source->second.inventory.referenceVolumeM3, {}, sinkId},
                    {{sourceId, revision(revisions, sourceId)}});
            }
        } else if (type == "RinseTool") {
            const auto toolId = payload.at("toolId").get<std::string>();
            const auto sourceId = payload.at("rinseSourceInventoryId").get<std::string>();
            const auto sinkId = payload.at("wasteSinkId").get<std::string>();
            const auto expectedInventoryRevision = decimalSequence(payload.at("expectedInventoryRevision").get<std::string>());
            const auto basis = transferQuantityBasis(payload.at("quantity").at("basis").get<std::string>());
            const auto found = context->tools.find(toolId);
            if (found == context->tools.end() || !expectedInventoryRevision ||
                *expectedInventoryRevision != found->second.inventoryRevision) {
                outcome = {false, "StaleInventoryRevision", "tool rinse inventory revision changed"};
            } else if (!basis) {
                outcome = {false, "InvalidRinse", "unsupported rinse quantity basis"};
            } else if (found->second.inFlightInventory.referenceVolumeM3 > 0.0) {
                outcome = {false, "InFlightOutstanding", "land the in-flight parcel before rinsing"};
            } else {
                auto staged = context->executor;
                const auto beforeRinse = staged.snapshot();
                outcome = staged.transferFixed(
                    {sourceId, *basis, payload.at("quantity").at("value").get<double>(), {}, sinkId},
                    {{sourceId, revision(revisions, sourceId)}});
                if (outcome.accepted) {
                    auto afterRinse = staged.snapshot();
                    const auto& sourceBefore = beforeRinse.vessels.at(sourceId).inventory;
                    const auto& sourceAfter = afterRinse.vessels.at(sourceId).inventory;
                    const auto rinseVolumeM3 = sourceBefore.referenceVolumeM3 - sourceAfter.referenceVolumeM3;
                    auto combined = sourceBefore.fraction(rinseVolumeM3);
                    combined.add(found->second.tipInventory);
                    combined.add(found->second.residualInventory);
                    const auto retained = combined.fraction(std::min(
                        plv::BuretteProfile::researchDefault().dropReferenceVolumeM3,
                        combined.referenceVolumeM3));
                    auto& waste = afterRinse.sinks.at(sinkId);
                    waste.add(found->second.tipInventory);
                    waste.add(found->second.residualInventory);
                    if (!waste.subtract(retained) || !staged.restore(afterRinse).accepted) {
                        outcome = {false, "RinseLedgerError", "rinse cannot preserve the represented material ledger"};
                    } else {
                        context->executor = std::move(staged);
                        found->second.tipInventory = retained;
                        found->second.residualInventory = {};
                        ++found->second.inventoryRevision;
                        found->second.actuator01 = 0.0;
                        found->second.latestInput.reset();
                    }
                }
            }
        } else if (type == "CreateCheckpoint") {
            const auto checkpointId = payload.at("checkpointId").get<std::string>();
            if (!plv::isValidId(checkpointId)) {
                outcome = {false, "InvalidCheckpoint", "checkpoint identity is invalid"};
            } else if (context->checkpoints.size() >= 16U && context->checkpoints.count(checkpointId) == 0U) {
                outcome = {false, "CheckpointLimitReached", "checkpoint limit reached"};
            } else {
                context->checkpoints.insert_or_assign(
                    checkpointId,
                    Context::CheckpointState{context->executor.snapshot(), context->tools, context->mode});
                outcome = {true, "Accepted", ""};
            }
        } else if (type == "RestartCheckpoint") {
            const auto checkpointId = payload.at("checkpointId").get<std::string>();
            const auto found = context->checkpoints.find(checkpointId);
            if (found == context->checkpoints.end()) {
                outcome = {false, "CheckpointNotFound", "checkpoint identity is unknown"};
            } else {
                auto restartSnapshot = found->second.snapshot;
                restartSnapshot.eventSequence = context->executor.snapshot().eventSequence + 1U;
                outcome = context->executor.restore(restartSnapshot);
                if (outcome.accepted) {
                    context->tools = found->second.tools;
                    for (auto& item : context->tools) item.second.latestInput.reset();
                    context->mode = found->second.mode;
                    context->holdActive = true;
                    context->holdReason = "CheckpointRestart";
                    context->recoveryReady = context->tools.empty();
                }
            }
        } else if (type == "BeginModeChange") {
            const auto mode = payload.at("mode").get<std::string>();
            if (mode != "Desktop" && mode != "VR") {
                outcome = {false, "InvalidMode", "mode must be Desktop or VR"};
            } else {
                context->mode = mode;
                context->holdActive = true;
                context->holdReason = "ModeChange";
                context->recoveryReady = context->tools.empty();
                for (auto& item : context->tools) item.second.latestInput.reset();
                outcome = {true, "Accepted", ""};
            }
        } else if (type == "Recenter") {
            context->holdActive = true;
            context->holdReason = "Recenter";
            context->recoveryReady = false;
            for (auto& item : context->tools) item.second.latestInput.reset();
            outcome = {true, "Accepted", ""};
        } else if (type == "BeginInputHold") {
            const auto reason = payload.at("reason").get<std::string>();
            if (reason != "FocusLoss" && reason != "TrackingLoss") {
                outcome = {false, "InvalidHoldReason", "unsupported input hold reason"};
            } else {
                context->holdActive = true;
                context->holdReason = reason;
                context->recoveryReady = false;
                for (auto& item : context->tools) item.second.latestInput.reset();
                outcome = {true, "Accepted", ""};
            }
        } else if (type == "Pause") {
            context->executor.setPaused(true);
            outcome = {true, "Accepted", ""};
        } else if (type == "Continue") {
            const auto clock = payload.contains("monotonicNowNs") && payload.at("monotonicNowNs").is_string()
                                   ? decimalSequence(payload.at("monotonicNowNs").get<std::string>())
                                   : std::nullopt;
            if (context->holdActive && (!context->recoveryReady ||
                (!context->tools.empty() && (!clock || *clock < context->lastMonotonicNowNs ||
                                              !freshNeutralInputs(*context, *clock))))) {
                outcome = {false, "RecoveryNotReady", "neutral fresh tracked baselines are required"};
            } else {
                context->holdActive = false;
                context->holdReason.clear();
                context->recoveryReady = false;
                for (auto& item : context->tools) item.second.latestInput.reset();
                context->executor.setPaused(false);
                outcome = {true, "Accepted", ""};
            }
        } else {
            outcome = {false, "UnsupportedCommand", "command type is not implemented"};
        }
        if (outcome.accepted && context->holdActive &&
            (type == "CreateVessel" || type == "PrepareStock" || type == "TransferFixed" ||
             type == "PlaceTool" || type == "SetActuator" || type == "DisposeContents" ||
             type == "RinseTool")) context->recoveryReady = false;
        if (outcome.accepted) {
            const auto nowNs = static_cast<std::uint64_t>(
                std::max(0.0, context->executor.snapshot().simulationTimeS) * 1'000'000'000.0);
            offerChangedScience(*context, beforeCommand, nowNs);
        }
        const auto serializedOutcome = outcomeJson(parsed, outcome).dump();
        context->receipts.emplace(*sequence, std::make_pair(canonical, serializedOutcome));
        ++context->nextCommandSequence;
        context->events.push_back(serializedOutcome);
        return PLV_OK;
    } catch (const json::exception&) {
        return PLV_INVALID_ARGUMENT;
    } catch (const std::invalid_argument&) {
        return PLV_INVALID_ARGUMENT;
    } catch (...) {
        return PLV_INTERNAL_ERROR;
    }
}

std::int32_t plv_input_batch(std::uint64_t handle, const char* samples, std::uint32_t size) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    if (!context->active.load() || context->ownerThread != std::this_thread::get_id()) return PLV_BUSY;
    if (!validInput(samples, size)) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        const auto parsed = json::parse(samples, samples + size);
        if (!parsed.is_array() || parsed.size() > 128U) return PLV_INVALID_ARGUMENT;
        std::lock_guard<std::mutex> lock(context->mutex);
        if (!context->active.load()) return PLV_BUSY;
        auto proposedSequences = context->inputSequences;
        auto proposedTools = context->tools;
        const auto snapshot = context->executor.snapshot();
        for (const auto& sample : parsed) {
            const auto toolId = sample.at("toolId").get<std::string>();
            const auto profile = sample.at("geometryProfileHash").get<std::string>();
            const auto frame = sample.at("coordinateFrame").get<std::string>();
            const auto sequence = decimalSequence(sample.at("sampleSequence").get<std::string>());
            const auto timestamp = decimalSequence(sample.at("captureMonotonicNs").get<std::string>());
            const auto profileRevision = decimalSequence(sample.at("profileRevision").get<std::string>());
            const auto toolRevision = decimalSequence(sample.at("toolRevision").get<std::string>());
            const auto& position = sample.at("positionMetres");
            const auto& rotation = sample.at("rotation");
            const auto& captureFractions = sample.at("captureFractions");
            const double actuator = sample.at("actuator01").get<double>();
            const auto registered = proposedTools.find(toolId);
            if (!plv::isValidId(toolId) || registered == proposedTools.end() || !sequence || !timestamp ||
                !profileRevision || !toolRevision || frame != registered->second.coordinateFrame ||
                profile != registered->second.geometryProfileHash || *profileRevision != registered->second.profileRevision ||
                *toolRevision != registered->second.toolRevision ||
                !position.is_array() || position.size() != 3U || !rotation.is_array() || rotation.size() != 4U ||
                !captureFractions.is_array() || captureFractions.size() > plv::kMaxVesselInventories ||
                !sample.at("trackingValid").is_boolean() || !std::isfinite(actuator) || actuator < 0.0 || actuator > 1.0) {
                return PLV_INVALID_ARGUMENT;
            }
            double normSquared = 0.0;
            for (const auto& coordinate : position) if (!coordinate.is_number() || !std::isfinite(coordinate.get<double>())) return PLV_INVALID_ARGUMENT;
            for (const auto& coordinate : rotation) {
                if (!coordinate.is_number() || !std::isfinite(coordinate.get<double>())) return PLV_INVALID_ARGUMENT;
                const double value = coordinate.get<double>();
                normSquared += value * value;
            }
            if (std::abs(normSquared - 1.0) > 1e-6 || *sequence <= proposedSequences[toolId]) return PLV_INVALID_ARGUMENT;

            std::vector<plv::CaptureFraction> captures;
            std::unordered_set<std::string> destinations;
            double captureSum = 0.0;
            for (const auto& capture : captureFractions) {
                const auto destinationId = capture.at("destinationInventoryId").get<std::string>();
                const double fraction = capture.at("fraction").get<double>();
                if (!plv::isValidId(destinationId) || destinationId == registered->second.sourceInventoryId ||
                    snapshot.vessels.count(destinationId) == 0U || !destinations.insert(destinationId).second ||
                    !std::isfinite(fraction) || fraction < 0.0 || fraction > 1.0) {
                    return PLV_INVALID_ARGUMENT;
                }
                captureSum += fraction;
                if (!std::isfinite(captureSum) || captureSum > 1.0 + 1e-12) return PLV_INVALID_ARGUMENT;
                captures.push_back({destinationId, fraction});
            }
            proposedSequences[toolId] = *sequence;
            registered->second.latestInput = Context::ToolInput{
                *sequence,
                *timestamp,
                actuator,
                sample.at("trackingValid").get<bool>(),
                std::move(captures)};
        }
        context->inputSequences = std::move(proposedSequences);
        context->tools = std::move(proposedTools);
        if (context->holdActive) context->recoveryReady = context->tools.empty();
        return PLV_OK;
    } catch (...) {
        return PLV_INVALID_ARGUMENT;
    }
}

std::int32_t plv_step(std::uint64_t handle, double delta_s, std::uint64_t monotonic_now_ns) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    if (!context->active.load() || context->ownerThread != std::this_thread::get_id()) return PLV_BUSY;
    if (!std::isfinite(delta_s) || delta_s < 0.0) return PLV_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> lock(context->mutex);
    if (!context->active.load()) return PLV_BUSY;
    if (monotonic_now_ns < context->lastMonotonicNowNs) {
        context->holdActive = true;
        context->holdReason = "TimeDiscontinuity";
        context->recoveryReady = false;
        for (auto& item : context->tools) item.second.latestInput.reset();
        return PLV_OK;
    }
    context->lastMonotonicNowNs = monotonic_now_ns;
    const auto simulationNowNs = static_cast<std::uint64_t>(
        std::max(0.0, context->executor.snapshot().simulationTimeS) * 1'000'000'000.0);
    collectScience(*context, simulationNowNs);
    if (context->holdActive) {
        bool ready = freshNeutralInputs(*context, monotonic_now_ns);
        if (context->holdReason == "Compute" && context->worker) {
            const auto snapshot = context->executor.snapshot();
            for (const auto& [id, vessel] : snapshot.vessels) {
                if (vessel.inventory.referenceVolumeM3 <= 0.0) continue;
                const auto* observation = context->scheduler.observation(id, "pH");
                if (observation == nullptr ||
                    observation->freshness != plv::ObservationFreshness::Current ||
                    observation->solvedRevision != vessel.materialRevision) ready = false;
            }
        }
        context->recoveryReady = ready;
        return PLV_OK;
    }
    if (delta_s > transportTickS * 2.0 + 1e-12) {
        context->holdActive = true;
        context->holdReason = "TimeDiscontinuity";
        context->recoveryReady = false;
        for (auto& item : context->tools) item.second.latestInput.reset();
        return PLV_OK;
    }
    if (context->executor.paused()) return PLV_OK;

    std::vector<std::string> toolIds;
    toolIds.reserve(context->tools.size());
    for (const auto& item : context->tools) toolIds.push_back(item.first);
    std::sort(toolIds.begin(), toolIds.end());

    std::size_t activeToolCount = 0U;
    for (const auto& toolId : toolIds) {
        const auto& tool = context->tools.at(toolId);
        if (!tool.latestInput || tool.latestInput->actuator01 <= 0.0) continue;
        const auto& input = *tool.latestInput;
        if (!input.trackingValid || input.captureMonotonicNs > monotonic_now_ns ||
            monotonic_now_ns - input.captureMonotonicNs > inputStaleCutoffNs) {
            context->holdActive = true;
            context->holdReason = "Input";
            context->recoveryReady = false;
            for (auto& item : context->tools) item.second.latestInput.reset();
            return PLV_OK;
        }
        ++activeToolCount;
    }
    if (context->events.size() + activeToolCount > 256U) return PLV_BUSY;

    // Stage the complete tick so a later tool or science budget cannot leave
    // earlier transfers committed at an unchanged simulation time.
    const auto beforeTick = context->executor.snapshot();
    auto stagedExecutor = context->executor;
    std::unordered_map<std::string, double> grossByVessel;
    std::vector<std::string> stagedEvents;
    stagedEvents.reserve(activeToolCount);
    for (const auto& toolId : toolIds) {
        const auto& tool = context->tools.at(toolId);
        if (!tool.latestInput || !tool.latestInput->trackingValid || tool.latestInput->actuator01 <= 0.0 || delta_s == 0.0) {
            continue;
        }
        const auto snapshot = stagedExecutor.snapshot();
        const auto source = snapshot.vessels.find(tool.sourceInventoryId);
        if (source == snapshot.vessels.end() || source->second.inventory.referenceVolumeM3 <= 0.0) continue;
        const double requestedVolumeM3 = plv::simulateBuretteDelivery(
            plv::BuretteProfile::researchDefault(),
            source->second.inventory.referenceVolumeM3,
            tool.latestInput->actuator01,
            delta_s,
            transportTickS);
        if (requestedVolumeM3 <= 0.0) continue;

        std::unordered_map<std::string, std::uint64_t> revisions;
        revisions.emplace(tool.sourceInventoryId, source->second.materialRevision);
        for (const auto& capture : tool.latestInput->captures) {
            const auto destination = snapshot.vessels.find(capture.destinationInventoryId);
            if (destination == snapshot.vessels.end()) return PLV_INTERNAL_ERROR;
            revisions.emplace(capture.destinationInventoryId, destination->second.materialRevision);
        }
        const auto outcome = stagedExecutor.transferFixed(
            {tool.sourceInventoryId,
             plv::TransferQuantityBasis::LiquidVolumeM3,
             requestedVolumeM3,
             tool.latestInput->captures,
             tool.overflowSinkId},
            revisions);
        if (!outcome.accepted) return PLV_INTERNAL_ERROR;
        const auto committed = stagedExecutor.snapshot();
        grossByVessel[tool.sourceInventoryId] += requestedVolumeM3;
        for (const auto& capture : tool.latestInput->captures) {
            const auto& before = snapshot.vessels.at(capture.destinationInventoryId);
            const auto& after = committed.vessels.at(capture.destinationInventoryId);
            grossByVessel[capture.destinationInventoryId] +=
                std::max(0.0, after.inventory.referenceVolumeM3 - before.inventory.referenceVolumeM3);
        }
        json captured = json::array();
        for (const auto& capture : tool.latestInput->captures) {
            const auto& before = snapshot.vessels.at(capture.destinationInventoryId);
            const auto& after = committed.vessels.at(capture.destinationInventoryId);
            captured.push_back({
                {"destinationInventoryId", capture.destinationInventoryId},
                {"quantityM3", after.inventory.referenceVolumeM3 - before.inventory.referenceVolumeM3},
                {"materialRevision", std::to_string(after.materialRevision)}
            });
        }
        const double spilledQuantityM3 =
            committed.sinks.at(tool.overflowSinkId).referenceVolumeM3 -
            snapshot.sinks.at(tool.overflowSinkId).referenceVolumeM3;
        stagedEvents.push_back(json({
            {"schemaVersion", 1},
            {"type", "LiveTransferCommitted"},
            {"branchId", committed.branchId},
            {"eventSequence", std::to_string(committed.eventSequence)},
            {"toolId", toolId},
            {"sampleSequence", std::to_string(tool.latestInput->sampleSequence)},
            {"quantityM3", requestedVolumeM3},
            {"sourceInventoryId", tool.sourceInventoryId},
            {"sourceMaterialRevision", std::to_string(committed.vessels.at(tool.sourceInventoryId).materialRevision)},
            {"captured", std::move(captured)},
            {"overflowSinkId", tool.overflowSinkId},
            {"spilledQuantityM3", spilledQuantityM3}
        }).dump());
    }
    const auto proposedTimeNs = simulationNowNs + static_cast<std::uint64_t>(delta_s * 1'000'000'000.0);
    if (context->worker) {
        for (const auto& [vesselId, grossVolume] : grossByVessel) {
            if (grossVolume <= 0.0) continue;
            const auto before = beforeTick.vessels.find(vesselId);
            if (before != beforeTick.vessels.end() && before->second.inventory.referenceVolumeM3 <= 0.0) continue;
            if (!context->scheduler.admitTransport(vesselId, grossVolume, proposedTimeNs).accepted) {
                context->holdActive = true;
                context->holdReason = "Compute";
                context->recoveryReady = false;
                for (auto& item : context->tools) item.second.latestInput.reset();
                return PLV_OK;
            }
        }
    }
    stagedExecutor.advanceTime(delta_s);
    context->executor = std::move(stagedExecutor);
    if (context->worker) {
        for (const auto& [vesselId, grossVolume] : grossByVessel)
            context->scheduler.recordGrossTransport(vesselId, grossVolume, simulationNowNs);
        offerChangedScience(*context, beforeTick, proposedTimeNs);
    }
    for (auto& event : stagedEvents) context->events.push_back(std::move(event));
    return PLV_OK;
}

std::int32_t plv_snapshot(std::uint64_t handle, char* out, std::uint32_t capacity, std::uint32_t* required) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    try {
        std::lock_guard<std::mutex> lock(context->mutex);
        return copyString(contextSnapshotJson(*context).dump(), out, capacity, required);
    } catch (...) {
        return PLV_INTERNAL_ERROR;
    }
}

std::int32_t plv_poll(std::uint64_t handle, char* out, std::uint32_t capacity, std::uint32_t* required) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    if (required == nullptr) {
        return PLV_INVALID_ARGUMENT;
    }
    std::lock_guard<std::mutex> lock(context->mutex);
    if (context->events.empty()) {
        *required = 0U;
        return PLV_NO_EVENT;
    }
    const auto result = copyString(context->events.front(), out, capacity, required);
    if (result == PLV_OK) {
        context->events.pop_front();
    }
    return result;
}

std::int32_t plv_export(std::uint64_t handle, char* out, std::uint32_t capacity, std::uint32_t* required) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    try {
        std::lock_guard<std::mutex> lock(context->mutex);
        return copyString(exportJson(*context).dump(), out, capacity, required);
    } catch (...) {
        return PLV_INTERNAL_ERROR;
    }
}

std::int32_t plv_import(const char* input, std::uint32_t size, std::uint64_t* new_handle) {
    if (!validInput(input, size) || new_handle == nullptr) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        *new_handle = 0U;
        const auto parsed = json::parse(input, input + size);
        const auto snapshot = parseSnapshot(parsed);
        std::string databasePath;
        std::string databaseIdentity;
        if (parsed.contains("scientificPackage")) {
            const auto& package = parsed.at("scientificPackage");
            databasePath = package.at("path").get<std::string>();
            databaseIdentity = package.at("identity").get<std::string>();
            if ((!databasePath.empty() && databaseIdentity.empty()) ||
                (databasePath.empty() && !databaseIdentity.empty()) ||
                databasePath.size() > 1024U || databaseIdentity.size() > 128U) return PLV_INVALID_ARGUMENT;
        }
        auto context = std::make_shared<Context>(snapshot.branchId, databasePath, databaseIdentity);
        const auto outcome = context->executor.restore(snapshot);
        if (!outcome.accepted) return PLV_INVALID_ARGUMENT;
        const auto parseTools = [](const json& tools, const plv::SessionSnapshot& ownerSnapshot) {
            std::unordered_map<std::string, Context::ToolState> result;
            if (!tools.is_array() || tools.size() > maxTools) throw std::invalid_argument("invalid tools");
            for (const auto& item : tools) {
                const auto id = item.at("id").get<std::string>();
                const auto sourceId = item.at("sourceInventoryId").get<std::string>();
                const auto sinkId = item.at("overflowSinkId").get<std::string>();
                const auto frame = item.at("coordinateFrame").get<std::string>();
                const auto profile = item.at("geometryProfileHash").get<std::string>();
                const auto profileRevision = decimalSequence(item.at("profileRevision").get<std::string>());
                const auto toolRevision = decimalSequence(item.at("toolRevision").get<std::string>());
                const auto actuatorRevision = decimalSequence(item.at("actuatorRevision").get<std::string>());
                const auto inventoryRevision = decimalSequence(item.at("inventoryRevision").get<std::string>());
                const double actuator = item.at("actuator01").get<double>();
                const auto tip = parseMaterial(item.at("tipInventory"));
                const auto residual = parseMaterial(item.at("residualInventory"));
                const auto inFlight = parseMaterial(item.at("inFlightInventory"));
                if (!plv::isValidId(id) || !profileRevision || !toolRevision || *profileRevision == 0U ||
                    !actuatorRevision || !inventoryRevision || *toolRevision == 0U ||
                    !std::isfinite(actuator) || actuator < 0.0 || actuator > 1.0 ||
                    !tip.isFiniteNonNegative() || !residual.isFiniteNonNegative() || !inFlight.isFiniteNonNegative() ||
                    frame != "lab" || profile != plv::BuretteProfile::researchDefault().id ||
                    ownerSnapshot.vessels.count(sourceId) == 0U || ownerSnapshot.sinks.count(sinkId) == 0U ||
                    !result.emplace(id, Context::ToolState{
                        sourceId, sinkId, frame, profile, *profileRevision, *toolRevision, *actuatorRevision,
                        *inventoryRevision, actuator,
                        tip, residual, inFlight, std::nullopt}).second) {
                    throw std::invalid_argument("invalid tool");
                }
            }
            return result;
        };
        context->tools = parseTools(parsed.at("tools"), snapshot);
        context->mode = parsed.at("mode").get<std::string>();
        if (context->mode != "Desktop" && context->mode != "VR") return PLV_INVALID_ARGUMENT;
        const auto& hold = parsed.at("hold");
        if (!hold.is_object() || !hold.at("active").is_boolean() || !hold.at("reason").is_string() ||
            !hold.at("recoveryReady").is_boolean()) {
            return PLV_INVALID_ARGUMENT;
        }
        context->holdActive = true;
        context->holdReason = "SessionLoad";
        context->recoveryReady = false;
        const auto nextSequence = decimalSequence(parsed.at("nextCommandSequence").get<std::string>());
        if (!nextSequence || *nextSequence == 0U || !parsed.at("commandReceipts").is_array() ||
            !parsed.at("inputSequences").is_object()) {
            return PLV_INVALID_ARGUMENT;
        }
        for (const auto& item : parsed.at("commandReceipts")) {
            const auto sequence = decimalSequence(item.at("commandSequence").get<std::string>());
            if (!sequence || *sequence == 0U || *sequence >= *nextSequence) return PLV_INVALID_ARGUMENT;
            const auto& command = item.at("canonicalCommand");
            const auto& terminalOutcome = item.at("terminalOutcome");
            if (!command.is_object() || !terminalOutcome.is_object() ||
                command.value("schemaVersion", 0) != 1 || terminalOutcome.value("schemaVersion", 0) != 1 ||
                command.at("branchId").get<std::string>() != snapshot.branchId ||
                terminalOutcome.at("branchId").get<std::string>() != snapshot.branchId ||
                command.at("commandSequence").get<std::string>() != std::to_string(*sequence) ||
                terminalOutcome.at("commandSequence").get<std::string>() != std::to_string(*sequence) ||
                !context->receipts.emplace(*sequence, std::make_pair(command.dump(), terminalOutcome.dump())).second) {
                return PLV_INVALID_ARGUMENT;
            }
        }
        if (context->receipts.size() != *nextSequence - 1U) return PLV_INVALID_ARGUMENT;
        for (const auto& item : parsed.at("inputSequences").items()) {
            const auto sequence = decimalSequence(item.value().get<std::string>());
            if (!plv::isValidId(item.key()) || !sequence || context->tools.count(item.key()) == 0U) {
                return PLV_INVALID_ARGUMENT;
            }
            context->inputSequences.emplace(item.key(), *sequence);
        }
        const auto& checkpoints = parsed.at("checkpoints");
        if (!checkpoints.is_array() || checkpoints.size() > 16U) return PLV_INVALID_ARGUMENT;
        for (const auto& item : checkpoints) {
            const auto checkpointId = item.at("checkpointId").get<std::string>();
            const auto checkpointSnapshot = parseSnapshot(item.at("snapshot"));
            const auto checkpointMode = item.at("mode").get<std::string>();
            if (!plv::isValidId(checkpointId) || checkpointSnapshot.branchId != snapshot.branchId ||
                (checkpointMode != "Desktop" && checkpointMode != "VR") ||
                !context->checkpoints.emplace(
                    checkpointId,
                    Context::CheckpointState{
                        checkpointSnapshot, parseTools(item.at("tools"), checkpointSnapshot), checkpointMode}).second) {
                return PLV_INVALID_ARGUMENT;
            }
        }
        context->nextCommandSequence = *nextSequence;
        if (context->worker) {
            const plv::SessionSnapshot empty;
            offerChangedScience(*context, empty, 0U);
        }
        std::lock_guard<std::mutex> lock(registryMutex);
        if (contexts.size() >= maxSessionContexts) return PLV_BUSY;
        for (const auto& [existingHandle, existing] : contexts) {
            if (existing->active.load() && existing->ownerThread != std::this_thread::get_id()) return PLV_BUSY;
        }
        for (const auto& [existingHandle, existing] : contexts) {
            if (existing->active.load() && existing->worker) existing->worker->requestStop();
            existing->active.store(false);
        }
        const auto assigned = nextHandle++;
        contexts.emplace(assigned, std::move(context));
        *new_handle = assigned;
        return PLV_OK;
    } catch (...) {
        *new_handle = 0U;
        return PLV_INVALID_ARGUMENT;
    }
}

}  // extern "C"
