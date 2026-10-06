#include "plv/journal.hpp"

#include <limits>

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
