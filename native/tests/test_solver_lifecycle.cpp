#include "doctest.h"
#include "plv/solver.hpp"

#include <algorithm>
#include <chrono>
#include <cmath>
#include <cstdint>
#include <fstream>
#include <iostream>
#include <string>

#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#include <psapi.h>
#else
#include <unistd.h>
#endif

namespace {
std::uint64_t residentBytes() {
#ifdef _WIN32
    PROCESS_MEMORY_COUNTERS counters{};
    if (!GetProcessMemoryInfo(GetCurrentProcess(), &counters, sizeof(counters))) return 0;
    return static_cast<std::uint64_t>(counters.WorkingSetSize);
#else
    std::ifstream stat("/proc/self/statm");
    std::uint64_t totalPages = 0;
    std::uint64_t residentPages = 0;
    if (!(stat >> totalPages >> residentPages)) return 0;
    return residentPages * static_cast<std::uint64_t>(sysconf(_SC_PAGESIZE));
#endif
}
}  // namespace

TEST_CASE("P04_03 repeated real IPhreeqc contexts report raw init solve and resident memory") {
    constexpr auto pinned = "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7";
    const plv::SolveRequest request{0.025, 2.5e-5, 0.0, 0.0025, 0.0, 25.0};
    const auto beforeBytes = residentBytes();
    std::chrono::microseconds maxInit{};
    std::chrono::microseconds maxSolve{};
    std::string reportedVersion;
    for (int iteration = 0; iteration < 8; ++iteration) {
        const auto begin = std::chrono::steady_clock::now();
        plv::IPhreeqcAdapter engine(PLV_MINTEQ_DATABASE, pinned);
        const auto initialized = std::chrono::steady_clock::now();
        REQUIRE(engine.initialized());
        const auto result = engine.solve(request);
        const auto solved = std::chrono::steady_clock::now();
        REQUIRE(result.succeeded);
        REQUIRE(std::isfinite(result.pH));
        CHECK(result.databaseIdentity == pinned);
        CHECK_FALSE(result.engineVersion.empty());
        reportedVersion = result.engineVersion;
        maxInit = std::max(maxInit, std::chrono::duration_cast<std::chrono::microseconds>(initialized - begin));
        maxSolve = std::max(maxSolve, std::chrono::duration_cast<std::chrono::microseconds>(solved - initialized));
    }
    const auto afterBytes = residentBytes();
    std::cout << "P04_03 lifecycle raw: contexts=8 maxInitUs=" << maxInit.count()
              << " maxSolveUs=" << maxSolve.count() << " residentBeforeBytes=" << beforeBytes
              << " residentAfterBytes=" << afterBytes << " reportedEngineVersion=" << reportedVersion
              << " databaseSha256=" << pinned << '\n';
    CHECK(beforeBytes > 0U);
    CHECK(afterBytes > 0U);
}
