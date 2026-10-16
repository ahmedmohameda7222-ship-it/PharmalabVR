#include "plv/journal.hpp"
#include "plv/sha256.hpp"

#include <charconv>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <limits>
#include <stdexcept>
#include <string>
#include <utility>

#ifdef _WIN32
#include <io.h>
#else
#include <fcntl.h>
#include <unistd.h>
#endif

namespace plv {
namespace {

constexpr const char* magic = "PLV-CKPT-V1";
constexpr std::size_t headerReserve = 256U;

struct CheckpointSlot {
    bool present = false;
    bool valid = false;
    std::uint64_t generation = 0;
    std::string bytes;
};

bool parseDecimal(const std::string& value, std::uint64_t& parsed) {
    if (value.empty() || (value.size() > 1U && value[0] == '0')) return false;
    const auto result = std::from_chars(value.data(), value.data() + value.size(), parsed);
    return result.ec == std::errc() && result.ptr == value.data() + value.size();
}

CheckpointSlot loadSlot(const std::string& base, unsigned index, std::size_t maximum) {
    namespace fs = std::filesystem;
    const fs::path path(base + ".slot" + std::to_string(index));
    CheckpointSlot result;
    std::error_code error;
    result.present = fs::exists(path, error);
    if (error) throw std::runtime_error("checkpoint slot could not be inspected");
    if (!result.present) return result;
    const auto size = fs::file_size(path, error);
    if (error) throw std::runtime_error("checkpoint slot could not be measured");
    if (size > maximum + headerReserve) return result;
    std::ifstream file(path, std::ios::binary);
    if (!file) throw std::runtime_error("checkpoint slot could not be opened");
    std::string text(static_cast<std::size_t>(size), '\0');
    if (!text.empty() && !file.read(text.data(), static_cast<std::streamsize>(text.size())))
        throw std::runtime_error("checkpoint slot read failed");
    const auto e1 = text.find('\n');
    const auto e2 = e1 == std::string::npos ? e1 : text.find('\n', e1 + 1);
    const auto e3 = e2 == std::string::npos ? e2 : text.find('\n', e2 + 1);
    const auto e4 = e3 == std::string::npos ? e3 : text.find('\n', e3 + 1);
    if (e1 == std::string::npos || e2 == std::string::npos ||
        e3 == std::string::npos || e4 == std::string::npos ||
        text.substr(0, e1) != magic) return result;

    std::uint64_t generation = 0;
    std::uint64_t length = 0;
    if (!parseDecimal(text.substr(e1 + 1, e2 - e1 - 1), generation) ||
        !parseDecimal(text.substr(e2 + 1, e3 - e2 - 1), length) ||
        generation == 0 || (generation - 1U) % 2U != index ||
        length == 0 || length > maximum || text.size() - (e4 + 1) != length)
        return result;
    const auto prefix = text.substr(0, e3 + 1);
    const auto payload = text.substr(e4 + 1);
    if (text.substr(e3 + 1, e4 - e3 - 1) != sha256Hex(prefix + payload))
        return result;
    result.valid = true;
    result.generation = generation;
    result.bytes = payload;
    return result;
}

std::optional<CheckpointSlot> latestSlot(const std::string& base, std::size_t max) {
    const auto zero = loadSlot(base, 0U, max);
    const auto one = loadSlot(base, 1U, max);
    if (!zero.valid && !one.valid) {
        if (zero.present || one.present)
            throw std::runtime_error("no valid checkpoint generation exists");
        return std::nullopt;
    }
    if (zero.valid && one.valid && zero.generation == one.generation)
        throw std::runtime_error("checkpoint generations are ambiguous");
    if (!one.valid || (zero.valid && zero.generation > one.generation))
        return zero;
    return one;
}

bool flushFile(std::FILE* file) {
    if (std::fflush(file) != 0) return false;
#ifdef _WIN32
    return _commit(_fileno(file)) == 0;
#else
    return ::fsync(::fileno(file)) == 0;
#endif
}

bool flushDirectory(const std::filesystem::path& directory) {
#ifdef _WIN32
    // Windows file contents are _commit'ed. Directory metadata flush is not
    // asserted by this source implementation; device power-loss needs proof.
    (void)directory;
    return true;
#else
    const int descriptor = ::open(directory.c_str(), O_RDONLY | O_DIRECTORY);
    if (descriptor < 0) return false;
    const bool flushed = ::fsync(descriptor) == 0;
    const bool closed = ::close(descriptor) == 0;
    return flushed && closed;
#endif
}

}  // namespace

DurableCheckpointStore::DurableCheckpointStore(std::string path, std::size_t maxPayloadBytes)
    : path_(std::move(path)), maxPayloadBytes_(maxPayloadBytes) {
    if (path_.empty() || path_.find('\0') != std::string::npos ||
        std::filesystem::path(path_).parent_path().empty() ||
        maxPayloadBytes_ == 0 ||
        maxPayloadBytes_ > std::numeric_limits<std::size_t>::max() - headerReserve)
        throw std::invalid_argument("invalid checkpoint path or bounds");
}

std::optional<std::string> DurableCheckpointStore::read() const {
    const auto found = latestSlot(path_, maxPayloadBytes_);
    if (!found) return std::nullopt;
    return found->bytes;
}

CommandOutcome DurableCheckpointStore::write(const std::string& nativeExport) {
    if (nativeExport.empty() || nativeExport.size() > maxPayloadBytes_)
        return {false, "CheckpointLimit", "serialized Native checkpoint is empty or oversized"};

    namespace fs = std::filesystem;
    fs::path temporary;
    try {
        const auto previous = latestSlot(path_, maxPayloadBytes_);
        if (previous && previous->generation == std::numeric_limits<std::uint64_t>::max())
            return {false, "CheckpointLimit", "checkpoint generation exhausted"};
        const auto generation = previous ? previous->generation + 1U : 1U;
        const auto index = static_cast<unsigned>((generation - 1U) % 2U);
        const fs::path target(path_ + ".slot" + std::to_string(index));
        temporary = fs::path(target.string() + ".tmp");
        std::error_code ec;
        if (!fs::is_directory(target.parent_path(), ec) || ec)
            return {false, "CheckpointWriteFailed", "checkpoint parent directory is unavailable"};

        const auto prefix = std::string(magic) + "\n" + std::to_string(generation) +
                            "\n" + std::to_string(nativeExport.size()) + "\n";
        const auto framed = prefix + sha256Hex(prefix + nativeExport) + "\n" + nativeExport;
        std::FILE* file = std::fopen(temporary.string().c_str(), "wb");
        if (!file) return {false, "CheckpointWriteFailed", "checkpoint stage cannot be opened"};
        bool completed = std::fwrite(framed.data(), 1, framed.size(), file) == framed.size();
        if (completed) completed = flushFile(file);
        if (std::fclose(file) != 0) completed = false;
        if (!completed) {
            fs::remove(temporary, ec);
            return {false, "CheckpointWriteFailed", "checkpoint stage write/flush failed"};
        }
        // Only the INACTIVE slot may be replaced. The latest confirmed
        // generation remains untouched until the next durable slot exists.
        if (fs::exists(target, ec)) {
            if (ec || !fs::remove(target, ec) || ec) {
                fs::remove(temporary, ec);
                return {false, "CheckpointWriteFailed", "inactive checkpoint slot cannot be retired"};
            }
        }
        if (ec) return {false, "CheckpointWriteFailed", "checkpoint target cannot be inspected"};
        fs::rename(temporary, target, ec);
        if (ec || !flushDirectory(target.parent_path()))
            return {false, "CheckpointWriteFailed", "checkpoint commit could not be confirmed"};
        return {true, "Accepted", ""};
    } catch (const std::exception&) {
        std::error_code cleanup;
        if (!temporary.empty()) fs::remove(temporary, cleanup);
        return {false, "CheckpointWriteFailed", "checkpoint filesystem operation failed"};
    }
}
}  // namespace plv
