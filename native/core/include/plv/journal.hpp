#pragma once

#include "plv/types.hpp"

#include <cstddef>
#include <cstdint>
#include <deque>
#include <string>
#include <unordered_map>
#include <vector>

namespace plv {

struct JournalRecovery {
    std::vector<std::string> records;
    bool truncatedTail = false;
    bool corruptTail = false;
    std::size_t validBytes = 0;
};

class Journal {
public:
    explicit Journal(std::size_t maxRecordBytes);
    CommandOutcome append(const std::string& payload);
    const std::vector<std::uint8_t>& bytes() const;
    static JournalRecovery recover(const std::vector<std::uint8_t>& bytes, std::size_t maxRecordBytes);

private:
    std::size_t maxRecordBytes_;
    std::vector<std::uint8_t> bytes_;
};

class ReceiptIdentityIndex {
public:
    explicit ReceiptIdentityIndex(std::size_t cacheLimit);
    bool accept(const std::string& branchId, std::uint64_t sequence, const std::string& payloadHash);
    std::size_t cachedCount() const;

private:
    std::size_t cacheLimit_;
    std::deque<std::string> cacheOrder_;
    std::unordered_map<std::string, std::string> durableIdentities_;
};

}  // namespace plv
