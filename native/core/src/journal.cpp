#include "plv/journal.hpp"

#include <limits>
#include <cstdio>
#include <filesystem>
#include <fstream>
#include <stdexcept>
#include <system_error>
#include <vector>

#ifdef _WIN32
#include <io.h>
#else
#include <unistd.h>
#endif

namespace plv {

namespace {
std::uint32_t crc32(const std::uint8_t* data, std::size_t size) {
    std::uint32_t crc = 0xffffffffU;
    for (std::size_t i = 0; i < size; ++i) {
        crc ^= data[i];
        for (int bit = 0; bit < 8; ++bit) {
            crc = (crc >> 1U) ^ (0xedb88320U & (0U - (crc & 1U)));
        }
    }
    return ~crc;
}

void writeU32(std::vector<std::uint8_t>& output, std::uint32_t value) {
    for (unsigned shift = 0; shift < 32; shift += 8) {
        output.push_back(static_cast<std::uint8_t>((value >> shift) & 0xffU));
    }
}

std::uint32_t readU32(const std::vector<std::uint8_t>& input, std::size_t offset) {
    std::uint32_t value = 0;
    for (unsigned shift = 0; shift < 32; shift += 8) {
        value |= static_cast<std::uint32_t>(input[offset++]) << shift;
    }
    return value;
}
}  // namespace

Journal::Journal(std::size_t maxRecordBytes) : maxRecordBytes_(maxRecordBytes) {}

CommandOutcome Journal::append(const std::string& payload) {
    if (payload.empty() || payload.size() > maxRecordBytes_ ||
        payload.size() > std::numeric_limits<std::uint32_t>::max()) {
        return {false, "JournalRecordLimit", "journal record is empty or oversized"};
    }
    writeU32(bytes_, static_cast<std::uint32_t>(payload.size()));
    writeU32(bytes_, crc32(reinterpret_cast<const std::uint8_t*>(payload.data()), payload.size()));
    bytes_.insert(bytes_.end(), payload.begin(), payload.end());
    return {true, "Accepted", ""};
}

const std::vector<std::uint8_t>& Journal::bytes() const { return bytes_; }

JournalRecovery Journal::recover(const std::vector<std::uint8_t>& bytes, std::size_t maxRecordBytes) {
    JournalRecovery result;
    std::size_t offset = 0;
    while (offset < bytes.size()) {
        if (bytes.size() - offset < 8) {
            result.truncatedTail = true;
            break;
        }
        const auto length = readU32(bytes, offset);
        const auto expectedCrc = readU32(bytes, offset + 4);
        if (length == 0 || length > maxRecordBytes) {
            result.corruptTail = true;
            break;
        }
        if (bytes.size() - offset - 8 < length) {
            result.truncatedTail = true;
            break;
        }
        const auto* payload = bytes.data() + offset + 8;
        if (crc32(payload, length) != expectedCrc) {
            result.corruptTail = true;
            break;
        }
        result.records.emplace_back(reinterpret_cast<const char*>(payload), length);
        offset += 8 + length;
        result.validBytes = offset;
    }
    return result;
}


DurableJournal::DurableJournal(
    std::string path, std::size_t maxRecordBytes, std::size_t maxTotalBytes)
    : path_(std::move(path)), maxRecordBytes_(maxRecordBytes),
      maxTotalBytes_(maxTotalBytes) {
    if (path_.empty() || maxRecordBytes_ == 0 || maxTotalBytes_ < 9 ||
        maxRecordBytes_ > maxTotalBytes_ - 8) {
        throw std::invalid_argument("invalid disk journal path or size bounds");
    }
}

JournalRecovery DurableJournal::recover() const {
    namespace fs = std::filesystem;
    std::error_code error;
    const bool exists = fs::exists(path_, error);
    if (error) throw std::runtime_error("disk journal presence check failed");
    if (!exists) return {};
    const auto length = fs::file_size(path_, error);
    if (error || length > maxTotalBytes_ ||
        length > static_cast<std::uintmax_t>(
            std::numeric_limits<std::streamsize>::max())) {
        throw std::runtime_error("disk journal unreadable or size limit exceeded");
    }
    std::ifstream stream(path_, std::ios::binary);
    if (!stream) throw std::runtime_error("disk journal cannot be opened");
    std::vector<std::uint8_t> bytes(static_cast<std::size_t>(length));
    if (!bytes.empty() &&
        !stream.read(reinterpret_cast<char*>(bytes.data()),
                     static_cast<std::streamsize>(bytes.size()))) {
        throw std::runtime_error("disk journal read was incomplete");
    }
    return Journal::recover(bytes, maxRecordBytes_);
}

CommandOutcome DurableJournal::append(const std::string& payload) {
    Journal frame(maxRecordBytes_);
    const auto accepted = frame.append(payload);
    if (!accepted.accepted) return accepted;
    try {
        const auto recovered = recover();
        if (recovered.corruptTail) {
            return {false, "JournalCorrupt",
                    "complete journal record has invalid length or CRC"};
        }
        if (recovered.validBytes > maxTotalBytes_ ||
            frame.bytes().size() > maxTotalBytes_ - recovered.validBytes) {
            return {false, "JournalTotalLimit", "disk journal size bound reached"};
        }
        // Recovery never interprets a trailing fragment as an accepted command.
        // Drop only the unconfirmed tail; retain the complete CRC-checked prefix.
        if (recovered.truncatedTail) {
            std::error_code error;
            std::filesystem::resize_file(path_, recovered.validBytes, error);
            if (error) return {false, "JournalWriteFailed", "cannot discard torn tail"};
        }
        std::FILE* file = std::fopen(path_.c_str(), "ab");
        if (file == nullptr) {
            return {false, "JournalWriteFailed", "cannot open disk journal for append"};
        }
        const auto& bytes = frame.bytes();
        bool written = std::fwrite(bytes.data(), 1, bytes.size(), file) == bytes.size();
        if (written) written = std::fflush(file) == 0;
        if (written) {
#ifdef _WIN32
            written = _commit(_fileno(file)) == 0;
#else
            written = ::fsync(::fileno(file)) == 0;
#endif
        }
        if (std::fclose(file) != 0) written = false;
        if (!written) {
            // The attempted record is not confirmed. Recovery will check
            // actual bytes at restart before any receipt may be accepted.
            return {false, "JournalWriteFailed", "journal write or durable flush failed"};
        }
        return {true, "Accepted", ""};
    } catch (const std::exception&) {
        return {false, "JournalWriteFailed", "journal storage is unavailable"};
    }
}

ReceiptIdentityIndex::ReceiptIdentityIndex(std::size_t cacheLimit) : cacheLimit_(cacheLimit) {}

bool ReceiptIdentityIndex::accept(
    const std::string& branchId,
    std::uint64_t sequence,
    const std::string& payloadHash) {
    const auto identity = branchId + ":" + std::to_string(sequence);
    if (durableIdentities_.count(identity) != 0) {
        return false;
    }
    durableIdentities_.emplace(identity, payloadHash);
    if (cacheLimit_ > 0) {
        cacheOrder_.push_back(identity);
        while (cacheOrder_.size() > cacheLimit_) {
            cacheOrder_.pop_front();
        }
    }
    return true;
}

std::size_t ReceiptIdentityIndex::cachedCount() const { return cacheOrder_.size(); }

}  // namespace plv
