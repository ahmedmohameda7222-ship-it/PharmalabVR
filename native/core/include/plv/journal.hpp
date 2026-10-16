#pragma once

#include "plv/types.hpp"

#include <cstddef>
#include <cstdint>
#include <deque>
#include <memory>
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

// Disk-backed, bounded append-only record stream. The owning Native session
// must enforce a single writer; no simultaneous cross-process append is assumed.
// A torn trailing record is discarded at the next append, while CRC corruption
// in a complete record fails closed. A successful append flushes through the OS
// file descriptor before reporting acceptance.
class DurableJournal {
public:
    explicit DurableJournal(std::string path, std::size_t maxRecordBytes,
                            std::size_t maxTotalBytes = 32U * 1024U * 1024U);
    JournalRecovery recover() const;
    CommandOutcome append(const std::string& payload);

private:
    std::string path_;
    std::size_t maxRecordBytes_;
    std::size_t maxTotalBytes_;
};

class ReceiptIdentityIndex {
public:
    explicit ReceiptIdentityIndex(std::size_t cacheLimit);
    // Optional disk-backed identity history; cache size is capped at 4096.
    // Every acceptance is durably journaled before entering working memory.
    ReceiptIdentityIndex(std::size_t cacheLimit, std::string journalPath);
    bool accept(const std::string& branchId, std::uint64_t sequence, const std::string& payloadHash);
    std::size_t cachedCount() const;

private:
    std::size_t cacheLimit_;
    std::deque<std::string> cacheOrder_;
    std::unordered_map<std::string, std::string> durableIdentities_;
    std::unique_ptr<DurableJournal> durableJournal_;
};

}  // namespace plv
