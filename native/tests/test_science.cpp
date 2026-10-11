#include "doctest.h"
#include "plv/ideal_reference.hpp"
#include "plv/solver.hpp"
#include "plv/sha256.hpp"
#include "IPhreeqc.hpp"

#include <cmath>
#include <filesystem>
#include <fstream>
#include <iostream>
#include <stdexcept>
#include <vector>

using namespace plv;

namespace {
std::string minteqDatabasePath() {
    if (std::filesystem::is_regular_file(PLV_MINTEQ_DATABASE)) return PLV_MINTEQ_DATABASE;
    const auto besideExecutable = std::filesystem::current_path() / "minteq.v4.dat";
    return besideExecutable.string();
}
}  // namespace

TEST_CASE("S01 ideal charge root handles water strong acid and strong base") {
    const auto water = solveIdealAqueous({});
    CHECK(water.pH == doctest::Approx(7.0).epsilon(1e-10));
    const auto acid = solveIdealAqueous({0.0, 0.1, 0.0});
    CHECK(acid.pH == doctest::Approx(1.0).epsilon(1e-10));
    const auto base = solveIdealAqueous({0.1, 0.0, 0.0});
    CHECK(base.pH == doctest::Approx(13.0).epsilon(1e-10));
    CHECK(std::abs(base.chargeResidualMolPerL) < 1e-12);
}

TEST_CASE("S01 acetate total participates in one full charge balance") {
    const auto aceticAcid = solveIdealAqueous({0.0, 0.0, 0.1});
    CHECK(aceticAcid.pH == doctest::Approx(2.8753).epsilon(0.001));
    CHECK(std::abs(aceticAcid.chargeResidualMolPerL) < 1e-12);
}

TEST_CASE("S05 invalid scientific inputs reject rather than fabricate an observation") {
    IdealAqueousInput invalid;
    invalid.sodiumMolPerL = -1.0;
    CHECK_THROWS_AS(solveIdealAqueous(invalid), std::invalid_argument);
}

TEST_CASE("S02 pinned IPhreeqc engine solves water strong acid and strong base") {
    IPhreeqcAdapter adapter(minteqDatabasePath(), "minteq.v4.dat-pinned-package-bytes");
    REQUIRE(adapter.initialized());
    const auto water = adapter.solve({0.01, 1e-5, 0.0, 0.0, 0.0, 25.0});
    const auto acid = adapter.solve({0.01, 1e-5, 0.0, 1e-3, 0.0, 25.0});
    const auto base = adapter.solve({0.01, 1e-5, 1e-3, 0.0, 0.0, 25.0});
    INFO("water error: " << water.error);
    INFO("acid error: " << acid.error);
    INFO("base error: " << base.error);
    REQUIRE(water.succeeded);
    REQUIRE(acid.succeeded);
    REQUIRE(base.succeeded);
    CHECK(water.pH == doctest::Approx(7.0).epsilon(0.02));
    CHECK(acid.pH == doctest::Approx(1.0).epsilon(0.20));
    CHECK(base.pH == doctest::Approx(13.0).epsilon(0.03));
    CHECK_FALSE(water.engineVersion.empty());
    CHECK(water.databaseIdentity == "minteq.v4.dat-pinned-package-bytes");
}

TEST_CASE("S03 solver result does not depend on diverse prior request") {
    IPhreeqcAdapter adapter(minteqDatabasePath(), "minteq");
    REQUIRE(adapter.initialized());
    const SolveRequest acid{0.01, 1e-5, 0.0, 1e-7, 0.0, 25.0};
    const auto first = adapter.solve(acid);
    const auto intervening = adapter.solve({0.01, 1e-5, 1e-6, 0.0, 0.0, 25.0});
    INFO("intervening error: " << intervening.error);
    REQUIRE(intervening.succeeded);
    const auto second = adapter.solve(acid);
    REQUIRE(first.succeeded);
    REQUIRE(second.succeeded);
    CHECK(second.pH == doctest::Approx(first.pH).epsilon(1e-12));
}

TEST_CASE("S03 IPhreeqc preserves represented pools using actual solvent molality") {
    IPhreeqcAdapter adapter(minteqDatabasePath(), "minteq");
    REQUIRE(adapter.initialized());
    const SolveRequest mixed{0.0125, 9e-6, 4e-4, 2.5e-4, 3e-4, 25.0};

    const auto result = adapter.solve(mixed);
    INFO("mixed solve error: " << result.error);
    REQUIRE(result.succeeded);
    CHECK(result.solventWaterKg == doctest::Approx(mixed.solventWaterKg).epsilon(1e-10));
    CHECK(result.sodiumMol == doctest::Approx(mixed.sodiumMol).epsilon(1e-9));
    CHECK(result.chlorideMol == doctest::Approx(mixed.chlorideMol).epsilon(1e-9));
    CHECK(result.acetateMol == doctest::Approx(mixed.acetateMol).epsilon(1e-9));
    CHECK(std::isfinite(result.ionicStrengthMolPerKg));
    CHECK(result.ionicStrengthMolPerKg >= 0.0);
    CHECK(std::abs(result.chargeBalance) <= 1e-10);
}

TEST_CASE("S05 engine adapter rejects invalid requests and missing database") {
    IPhreeqcAdapter adapter(minteqDatabasePath(), "minteq");
    REQUIRE(adapter.initialized());
    const auto invalid = adapter.solve({0.0, 0.0, -1.0, 0.0, 0.0, 25.0});
    CHECK_FALSE(invalid.succeeded);
    CHECK_FALSE(invalid.error.empty());
    IPhreeqcAdapter missing("definitely-missing-database.dat", "missing");
    CHECK_FALSE(missing.initialized());
    CHECK_FALSE(missing.solve({0.01, 1e-5, 0.0, 0.0, 0.0, 25.0}).succeeded);
}

TEST_CASE("P04_02 pinned IPhreeqc database rejects changed bytes before loading") {
    CHECK(sha256Hex("") == "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855");
    CHECK(sha256Hex("abc") == "BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD");
    constexpr auto pinned = "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7";
    IPhreeqcAdapter original(minteqDatabasePath(), pinned);
    INFO("pinned load error: " << original.solve({0.01, 1e-5, 0.0, 0.0, 0.0, 25.0}).error);
    REQUIRE(original.initialized());
    const auto changedPath = std::filesystem::temp_directory_path() / "plv-p04-02-changed-minteq.dat";
    {
        std::ifstream source(minteqDatabasePath(), std::ios::binary);
        std::ofstream changed(changedPath, std::ios::binary | std::ios::trunc);
        changed << source.rdbuf() << '\n';
    }
    IPhreeqcAdapter changed(changedPath.string(), pinned);
    CHECK_FALSE(changed.initialized());
    CHECK_FALSE(changed.solve({0.01, 1e-5, 0.0, 0.0, 0.0, 25.0}).succeeded);
    std::filesystem::remove(changedPath);
}

TEST_CASE("P04_03 unsupported temperature and overflowing molality never enter the engine") {
    IPhreeqcAdapter adapter(minteqDatabasePath(),
        "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7");
    REQUIRE(adapter.initialized());
    auto unsupportedTemperature = adapter.solve({0.01, 1e-5, 0.0, 0.0, 0.0, 37.0});
    CHECK_FALSE(unsupportedTemperature.succeeded);
    CHECK(unsupportedTemperature.error == "invalid or incomplete solve request");
    auto overflowingMolality = adapter.solve({1e-300, 1e-5, 1e100, 0.0, 0.0, 25.0});
    CHECK_FALSE(overflowingMolality.succeeded);
    CHECK(overflowingMolality.error == "invalid or incomplete solve request");
    auto outsideProfile = adapter.solve({0.01, 1e-5, 0.02, 0.0, 0.0, 25.0});
    CHECK_FALSE(outsideProfile.succeeded);
    CHECK(outsideProfile.error == "invalid or incomplete solve request");
    auto boundary = adapter.solve({0.01, 1e-5, 0.01, 0.01, 0.0, 25.0});
    REQUIRE(boundary.succeeded);
    CHECK(std::isfinite(boundary.pH));
}

TEST_CASE("P04_03 prior acid base and acetate cells do not change a clean immutable request") {
    constexpr auto pinned = "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7";
    const SolveRequest target{0.025, 2.5e-5, 0.0005, 0.0003, 0.0002, 25.0};
    IPhreeqcAdapter clean(minteqDatabasePath(), pinned);
    IPhreeqcAdapter reused(minteqDatabasePath(), pinned);
    REQUIRE(clean.initialized());
    REQUIRE(reused.initialized());
    const auto baseline = clean.solve(target);
    REQUIRE(baseline.succeeded);
    REQUIRE(reused.solve({0.025, 2.5e-5, 0.0, 0.0025, 0.0, 25.0}).succeeded);
    REQUIRE(reused.solve({0.025, 2.5e-5, 0.0025, 0.0, 0.0, 25.0}).succeeded);
    REQUIRE(reused.solve({0.025, 2.5e-5, 0.0, 0.0, 0.0025, 25.0}).succeeded);
    const auto rejected = reused.solve({0.025, 2.5e-5, -1.0, 0.0, 0.0, 25.0});
    CHECK_FALSE(rejected.succeeded);
    CHECK_FALSE(rejected.error.empty());
    const auto after = reused.solve(target);
    REQUIRE(after.succeeded);
    CHECK(after.pH == doctest::Approx(baseline.pH).epsilon(1e-10));
    CHECK(after.sodiumMol == doctest::Approx(baseline.sodiumMol).epsilon(1e-10));
    CHECK(after.chlorideMol == doctest::Approx(baseline.chlorideMol).epsilon(1e-10));
    CHECK(after.acetateMol == doctest::Approx(baseline.acetateMol).epsilon(1e-10));
    CHECK(after.chargeBalance == doctest::Approx(baseline.chargeBalance).epsilon(1e-10));
}

TEST_CASE("P04_03 six supported aqueous stock pool and charge gates use real IPhreeqc") {
    IPhreeqcAdapter adapter(minteqDatabasePath(),
        "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7");
    REQUIRE(adapter.initialized());
    const std::vector<SolveRequest> stocks{
        {0.025, 2.5e-5, 0.0, 0.0, 0.0, 25.0},
        {0.025, 2.5e-5, 0.0, 0.0025, 0.0, 25.0},
        {0.025, 2.5e-5, 0.0025, 0.0, 0.0, 25.0},
        {0.025, 2.5e-5, 0.0025, 0.0025, 0.0, 25.0},
        {0.025, 2.5e-5, 0.0, 0.0, 0.0025, 25.0},
        {0.025, 2.5e-5, 0.0025, 0.0, 0.0025, 25.0},
    };
    for (const auto& stock : stocks) {
        const auto result = adapter.solve(stock);
        INFO("stock pools Na=" << stock.sodiumMol << " Cl=" << stock.chlorideMol
             << " acetate=" << stock.acetateMol << " error=" << result.error);
        REQUIRE(result.succeeded);
        CHECK(std::isfinite(result.pH));
        CHECK(std::isfinite(result.ionicStrengthMolPerKg));
        CHECK(std::abs(result.solventWaterKg - stock.solventWaterKg) <=
              1e-12 + 1e-9 * stock.solventWaterKg);
        CHECK(std::abs(result.sodiumMol - stock.sodiumMol) <= 1e-12 + 1e-9 * stock.sodiumMol);
        CHECK(std::abs(result.chlorideMol - stock.chlorideMol) <= 1e-12 + 1e-9 * stock.chlorideMol);
        CHECK(std::abs(result.acetateMol - stock.acetateMol) <= 1e-12 + 1e-9 * stock.acetateMol);
        CHECK(std::abs(result.chargeBalance) <=
              1e-12 + 1e-9 * (stock.sodiumMol + stock.chlorideMol + stock.acetateMol));
    }
}

TEST_CASE("P04_03 ideal and minteq activity assumptions are not mislabeled equivalent") {
    IPhreeqcAdapter adapter(minteqDatabasePath(),
        "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7");
    REQUIRE(adapter.initialized());
    const auto engine = adapter.solve({0.025, 2.5e-5, 0.0, 0.0025, 0.0, 25.0});
    REQUIRE(engine.succeeded);
    const auto ideal = solveIdealAqueous({0.0, 0.1, 0.0});
    CHECK(std::abs(engine.pH - ideal.pH) > 0.001);
    std::cout << "P04_03 different assumptions raw: idealPH=" << ideal.pH
              << " minteqPH=" << engine.pH << " deltaPH=" << engine.pH - ideal.pH << '\n';
}

TEST_CASE("P04_03 adapter matches independently assembled IPhreeqc input under the same assumptions") {
    IPhreeqc direct;
    REQUIRE(direct.LoadDatabase(minteqDatabasePath().c_str()) == 0);
    const char* input =
        "SOLUTION 1\n"
        "temp 25\n"
        "units mol/kgw\n"
        "pH 7 charge\n"
        "Na 0.02\n"
        "Cl 0.012\n"
        "Acetate 0.008\n"
        "-water 0.025\n"
        "SELECTED_OUTPUT 1\n"
        "-reset false\n"
        "-high_precision true\n"
        "-pH true\n"
        "END\n";
    REQUIRE(direct.RunString(input) == 0);
    REQUIRE(direct.GetSelectedOutputRowCount() >= 2);
    double directPH = 0.0;
    bool foundPH = false;
    for (int column = 0; column < direct.GetSelectedOutputColumnCount(); ++column) {
        VAR heading;
        VarInit(&heading);
        const auto headingStatus = direct.GetSelectedOutputValue(0, column, &heading);
        const bool isPH = headingStatus == VR_OK && heading.type == TT_STRING &&
                          heading.sVal != nullptr && std::string(heading.sVal) == "pH";
        VarClear(&heading);
        if (!isPH) continue;
        VAR value;
        VarInit(&value);
        const auto valueStatus = direct.GetSelectedOutputValue(direct.GetSelectedOutputRowCount() - 1, column, &value);
        foundPH = valueStatus == VR_OK && value.type == TT_DOUBLE;
        if (foundPH) directPH = value.dVal;
        VarClear(&value);
        break;
    }
    REQUIRE(foundPH);
    IPhreeqcAdapter adapter(minteqDatabasePath(),
        "AB0A8F7C7375E1BD997990F4BC3A9AF497516F10E57F4AAA3CEFB55DCCAC5AE7");
    REQUIRE(adapter.initialized());
    const auto result = adapter.solve({0.025, 2.5e-5, 0.0005, 0.0003, 0.0002, 25.0});
    REQUIRE(result.succeeded);
    CHECK(std::abs(result.pH - directPH) <= 0.001);
    std::cout << "P04_03 matched IPhreeqc assumptions raw: directPH=" << directPH
              << " adapterPH=" << result.pH << " deltaPH=" << result.pH - directPH << '\n';
}

TEST_CASE("P04_03 real engine initialization failure stays bounded and cannot publish a result") {
    const auto invalidPath = std::filesystem::temp_directory_path() / "plv-p04-03-invalid-database.dat";
    {
        std::ofstream invalid(invalidPath, std::ios::binary | std::ios::trunc);
        for (int line = 0; line < 256; ++line) invalid << "NOT_A_PHREEQC_DATABASE_KEYWORD\n";
    }
    IPhreeqcAdapter invalid(invalidPath.string(), "invalid-test-database");
    CHECK_FALSE(invalid.initialized());
    const auto result = invalid.solve({0.025, 2.5e-5, 0.0, 0.0, 0.0, 25.0});
    CHECK_FALSE(result.succeeded);
    CHECK_FALSE(result.error.empty());
    CHECK(result.error.size() <= 1024U);
    std::filesystem::remove(invalidPath);
}
