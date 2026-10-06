#include "plv/solver.hpp"

#include "IPhreeqc.hpp"

#include <cmath>
#include <iomanip>
#include <limits>
#include <mutex>
#include <sstream>
#include <string>

namespace plv {

class IPhreeqcAdapter::Implementation {
public:
    Implementation(std::string databasePath, std::string identity)
        : databaseIdentity(std::move(identity)) {
        engine.SetErrorStringOn(true);
        engine.SetOutputStringOn(false);
        engine.SetSelectedOutputStringOn(false);
        const int errors = engine.LoadDatabase(databasePath.c_str());
        ready = errors == 0;
        if (!ready) {
            const char* message = engine.GetErrorString();
            initializationError = message == nullptr ? "IPhreeqc database load failed" : message;
        }
    }

    IPhreeqc engine;
    std::mutex mutex;
    std::string databaseIdentity;
    std::string initializationError;
    bool ready = false;
};

namespace {
bool validRequest(const SolveRequest& request) {
    const double values[] = {request.solventWaterKg, request.referenceVolumeM3, request.sodiumMol,
                             request.chlorideMol, request.acetateMol, request.temperatureC};
    for (const double value : values) {
        if (!std::isfinite(value)) return false;
    }
    return request.solventWaterKg > 0.0 && request.referenceVolumeM3 > 0.0 &&
           request.sodiumMol >= 0.0 && request.chlorideMol >= 0.0 && request.acetateMol >= 0.0 &&
           request.temperatureC >= 0.0 && request.temperatureC <= 100.0;
}

bool selectedDouble(IPhreeqc& engine, const std::string& heading, double& output) {
    const int columns = engine.GetSelectedOutputColumnCount();
    const int rows = engine.GetSelectedOutputRowCount();
    if (columns <= 0 || rows < 2) return false;
    for (int column = 0; column < columns; ++column) {
        VAR header;
        VarInit(&header);
        if (engine.GetSelectedOutputValue(0, column, &header) != VR_OK) {
            VarClear(&header);
            continue;
        }
        const bool match = header.type == TT_STRING && header.sVal != nullptr && heading == header.sVal;
        VarClear(&header);
        if (!match) continue;
        VAR value;
        VarInit(&value);
        const auto status = engine.GetSelectedOutputValue(rows - 1, column, &value);
        if (status == VR_OK && (value.type == TT_DOUBLE || value.type == TT_LONG)) {
            output = value.type == TT_DOUBLE ? value.dVal : static_cast<double>(value.lVal);
            VarClear(&value);
            return std::isfinite(output);
        }
        VarClear(&value);
        return false;
    }
    return false;
}

std::string selectedHeadings(IPhreeqc& engine) {
    std::ostringstream headings;
    bool first = true;
    for (int column = 0; column < engine.GetSelectedOutputColumnCount(); ++column) {
        VAR header;
        VarInit(&header);
        if (engine.GetSelectedOutputValue(0, column, &header) == VR_OK && header.type == TT_STRING && header.sVal != nullptr) {
            if (!first) headings << ',';
            headings << header.sVal;
            first = false;
        }
        VarClear(&header);
    }
    return headings.str();
}
}  // namespace

IPhreeqcAdapter::IPhreeqcAdapter(std::string databasePath, std::string databaseIdentity)
    : implementation_(std::make_unique<Implementation>(std::move(databasePath), std::move(databaseIdentity))) {}

IPhreeqcAdapter::~IPhreeqcAdapter() = default;

bool IPhreeqcAdapter::initialized() const { return implementation_->ready; }

SolveResult IPhreeqcAdapter::solve(const SolveRequest& request) {
    SolveResult result;
    result.engineVersion = IPhreeqc::GetVersionString();
    result.databaseIdentity = implementation_->databaseIdentity;
    if (!implementation_->ready) {
        result.error = implementation_->initializationError;
        return result;
    }
    if (!validRequest(request)) {
        result.error = "invalid or incomplete solve request";
        return result;
    }

    const double volumeL = request.referenceVolumeM3 * 1000.0;
    std::ostringstream input;
    input << std::setprecision(17)
          << "DELETE\n-all\nEND\n"
          << "SOLUTION 1\n"
          << "temp " << request.temperatureC << "\n"
          << "units mol/L\n"
          << "pH 7 charge\n"
          << "Na " << request.sodiumMol / volumeL << "\n"
          << "Cl " << request.chlorideMol / volumeL << "\n"
          << "Acetate " << request.acetateMol / volumeL << "\n"
          << "-water " << request.solventWaterKg << "\n"
          << "SELECTED_OUTPUT 1\n"
          << "-reset false\n"
          << "-pH true\n"
          << "-charge_balance true\n"
          << "END\n";

    std::lock_guard<std::mutex> lock(implementation_->mutex);
    const int errors = implementation_->engine.RunString(input.str().c_str());
    if (errors != 0) {
        const char* message = implementation_->engine.GetErrorString();
        result.error = message == nullptr ? "IPhreeqc solve failed" : message;
        return result;
    }
    if (!selectedDouble(implementation_->engine, "pH", result.pH)) {
        result.error = "selected output did not contain a finite named pH column; headings=" + selectedHeadings(implementation_->engine);
        return result;
    }
    if (!selectedDouble(implementation_->engine, "charge", result.chargeBalance) &&
        !selectedDouble(implementation_->engine, "charge_balance", result.chargeBalance)) {
        result.error = "selected output did not contain a finite named charge-balance column; headings=" + selectedHeadings(implementation_->engine);
        return result;
    }
    result.succeeded = true;
    return result;
}

}  // namespace plv
