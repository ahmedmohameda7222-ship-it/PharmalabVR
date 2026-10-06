#include "plv/abi.h"

#include "plv/state_executor.hpp"
#include "json.hpp"

#include <cmath>
#include <cstring>
#include <deque>
#include <limits>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>

namespace {
using json = nlohmann::json;

struct Context {
    explicit Context(std::string branch) : executor(std::move(branch)) {}
    plv::StateExecutor executor;
    std::deque<std::string> events;
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

json snapshotJson(const plv::SessionSnapshot& snapshot) {
    json vessels = json::array();
    for (const auto& item : snapshot.vessels) {
        const auto& vessel = item.second;
        vessels.push_back({
            {"id", vessel.id},
            {"capacityM3", vessel.capacityM3},
            {"materialRevision", std::to_string(vessel.materialRevision)},
            {"inventory", {
                {"solventWaterKg", vessel.inventory.solventWaterKg},
                {"sodiumMol", vessel.inventory.sodiumMol},
                {"chlorideMol", vessel.inventory.chlorideMol},
                {"acetateMol", vessel.inventory.acetateMol},
                {"researchAdditiveVolumeM3", vessel.inventory.referenceVolumeM3}
            }}
        });
    }
    return {
        {"schemaVersion", 1},
        {"branchId", snapshot.branchId},
        {"eventSequence", std::to_string(snapshot.eventSequence)},
        {"simulationTimeS", snapshot.simulationTimeS},
        {"vessels", vessels}
    };
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
        const auto sequence = parsed.at("commandSequence").get<std::string>();
        const auto type = parsed.at("type").get<std::string>();
        if (type != "Pause" && type != "Continue") {
            return PLV_INVALID_ARGUMENT;
        }
        json outcome = {
            {"schemaVersion", 1},
            {"branchId", parsed.at("branchId")},
            {"commandSequence", sequence},
            {"type", "CommandOutcome"},
            {"accepted", true},
            {"code", "Accepted"}
        };
        std::lock_guard<std::mutex> lock(context->mutex);
        if (context->events.size() >= 256U) {
            return PLV_BUSY;
        }
        context->events.push_back(outcome.dump());
        return PLV_OK;
    } catch (const json::exception&) {
        return PLV_INVALID_ARGUMENT;
    } catch (...) {
        return PLV_INTERNAL_ERROR;
    }
}

std::int32_t plv_input_batch(std::uint64_t handle, const char* samples, std::uint32_t size) {
    if (!getContext(handle)) {
        return PLV_INVALID_HANDLE;
    }
    if (!validInput(samples, size)) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        const auto parsed = json::parse(samples, samples + size);
        return parsed.is_array() ? PLV_OK : PLV_INVALID_ARGUMENT;
    } catch (...) {
        return PLV_INVALID_ARGUMENT;
    }
}

std::int32_t plv_step(std::uint64_t handle, double delta_s, std::uint64_t) {
    if (!getContext(handle)) {
        return PLV_INVALID_HANDLE;
    }
    return std::isfinite(delta_s) && delta_s >= 0.0 ? PLV_OK : PLV_INVALID_ARGUMENT;
}

std::int32_t plv_snapshot(std::uint64_t handle, char* out, std::uint32_t capacity, std::uint32_t* required) {
    const auto context = getContext(handle);
    if (!context) {
        return PLV_INVALID_HANDLE;
    }
    try {
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
    return plv_snapshot(handle, out, capacity, required);
}

std::int32_t plv_import(const char* input, std::uint32_t size, std::uint64_t* new_handle) {
    if (!validInput(input, size) || new_handle == nullptr) {
        return PLV_INVALID_ARGUMENT;
    }
    try {
        const auto parsed = json::parse(input, input + size);
        json config = {{"schemaVersion", parsed.at("schemaVersion")}, {"branchId", parsed.at("branchId")}};
        const auto serialized = config.dump();
        return plv_create(serialized.data(), static_cast<std::uint32_t>(serialized.size()), new_handle);
    } catch (...) {
        *new_handle = 0U;
        return PLV_INVALID_ARGUMENT;
    }
}

}  // extern "C"
