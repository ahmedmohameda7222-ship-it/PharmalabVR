#include "plv/abi.h"

#include "plv/state_executor.hpp"
#include "json.hpp"

#include <algorithm>
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
#include <unordered_map>
#include <vector>

namespace {
using json = nlohmann::json;

struct Context {
    explicit Context(std::string branch) : executor(std::move(branch)) {}
    plv::StateExecutor executor;
    std::deque<std::string> events;
    std::unordered_map<std::uint64_t, std::pair<std::string, std::string>> receipts;
    std::unordered_map<std::string, std::uint64_t> inputSequences;
    std::uint64_t nextCommandSequence = 1;
    std::mutex mutex;
};

std::mutex registryMutex;
std::unordered_map<std::uint64_t, std::shared_ptr<Context>> contexts;
std::uint64_t nextHandle = 1;
constexpr std::uint32_t maxInputSize = 16U * 1024U * 1024U;

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

plv::MaterialState parseMaterial(const json& value) {
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

json exportJson(const Context& context) {
    auto result = snapshotJson(context.executor.snapshot());
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
        auto context = std::make_shared<Context>(branch);
        std::lock_guard<std::mutex> lock(registryMutex);
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
    std::lock_guard<std::mutex> lock(registryMutex);
    return contexts.erase(handle) == 1U ? PLV_OK : PLV_INVALID_HANDLE;
}

std::int32_t plv_submit(std::uint64_t handle, const char* command, std::uint32_t size) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    if (!validInput(command, size)) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        const auto parsed = json::parse(command, command + size);
        if (parsed.value("schemaVersion", 0) != 1) {
            return PLV_UNSUPPORTED_VERSION;
        }
        if (parsed.at("branchId").get<std::string>() != context->executor.snapshot().branchId) {
            return PLV_INVALID_ARGUMENT;
        }
        const auto sequenceText = parsed.at("commandSequence").get<std::string>();
        const auto sequence = decimalSequence(sequenceText);
        if (!sequence) return PLV_INVALID_ARGUMENT;
        const auto type = parsed.at("type").get<std::string>();
        std::lock_guard<std::mutex> lock(context->mutex);
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

        const auto& payload = parsed.at("payload");
        const auto& revisions = parsed.at("expectedMaterialRevisions");
        plv::CommandOutcome outcome;
        if (type == "CreateVessel") {
            outcome = context->executor.createVessel(payload.at("id").get<std::string>(), payload.at("capacityM3").get<double>());
        } else if (type == "CreateSink") {
            outcome = context->executor.createSink(payload.at("id").get<std::string>());
        } else if (type == "PrepareStock") {
            outcome = context->executor.prepareStock(
                payload.at("vesselId").get<std::string>(), stockKind(payload.at("stockKind").get<std::string>()),
                payload.at("concentrationMolPerL").get<double>(), payload.at("referenceVolumeM3").get<double>());
        } else if (type == "TransferFixed") {
            const auto sourceId = payload.at("sourceId").get<std::string>();
            const auto receiverId = payload.at("receiverId").get<std::string>();
            outcome = context->executor.transferFixed(
                sourceId, receiverId, payload.at("spillSinkId").get<std::string>(),
                payload.at("requestedVolumeM3").get<double>(), revision(revisions, sourceId), revision(revisions, receiverId));
        } else if (type == "Pause") {
            context->executor.setPaused(true);
            outcome = {true, "Accepted", ""};
        } else if (type == "Continue") {
            context->executor.setPaused(false);
            outcome = {true, "Accepted", ""};
        } else {
            outcome = {false, "UnsupportedCommand", "command type is not implemented"};
        }
        const auto serializedOutcome = outcomeJson(parsed, outcome).dump();
        context->receipts.emplace(*sequence, std::make_pair(canonical, serializedOutcome));
        ++context->nextCommandSequence;
        context->events.push_back(serializedOutcome);
        return PLV_OK;
    } catch (const json::exception&) {
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
    if (!validInput(samples, size)) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        const auto parsed = json::parse(samples, samples + size);
        if (!parsed.is_array() || parsed.size() > 128U) return PLV_INVALID_ARGUMENT;
        std::lock_guard<std::mutex> lock(context->mutex);
        auto proposedSequences = context->inputSequences;
        for (const auto& sample : parsed) {
            const auto toolId = sample.at("toolId").get<std::string>();
            const auto profile = sample.at("geometryProfileHash").get<std::string>();
            const auto sequence = decimalSequence(sample.at("sampleSequence").get<std::string>());
            const auto timestamp = decimalSequence(sample.at("captureMonotonicNs").get<std::string>());
            const auto& position = sample.at("positionMetres");
            const auto& rotation = sample.at("rotation");
            const double actuator = sample.at("actuator01").get<double>();
            if (!plv::isValidId(toolId) || profile.empty() || profile.size() > 128U || !sequence || !timestamp ||
                !position.is_array() || position.size() != 3U || !rotation.is_array() || rotation.size() != 4U ||
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
            proposedSequences[toolId] = *sequence;
        }
        context->inputSequences = std::move(proposedSequences);
        return PLV_OK;
    } catch (...) {
        return PLV_INVALID_ARGUMENT;
    }
}

std::int32_t plv_step(std::uint64_t handle, double delta_s, std::uint64_t) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    if (!std::isfinite(delta_s) || delta_s < 0.0) return PLV_INVALID_ARGUMENT;
    std::lock_guard<std::mutex> lock(context->mutex);
    context->executor.advanceTime(delta_s);
    return PLV_OK;
}

std::int32_t plv_snapshot(std::uint64_t handle, char* out, std::uint32_t capacity, std::uint32_t* required) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    try {
        std::lock_guard<std::mutex> lock(context->mutex);
        return copyString(snapshotJson(context->executor.snapshot()).dump(), out, capacity, required);
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
        auto context = std::make_shared<Context>(snapshot.branchId);
        const auto outcome = context->executor.restore(snapshot);
        if (!outcome.accepted) return PLV_INVALID_ARGUMENT;
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
            if (!plv::isValidId(item.key()) || !sequence) return PLV_INVALID_ARGUMENT;
            context->inputSequences.emplace(item.key(), *sequence);
        }
        context->nextCommandSequence = *nextSequence;
        std::lock_guard<std::mutex> lock(registryMutex);
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
