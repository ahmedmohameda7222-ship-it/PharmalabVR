#pragma once

#include <memory>
#include <string>

namespace plv {

struct SolveRequest {
    double solventWaterKg = 0.0;
    double referenceVolumeM3 = 0.0;
    double sodiumMol = 0.0;
    double chlorideMol = 0.0;
    double acetateMol = 0.0;
    double temperatureC = 25.0;
};

struct SolveResult {
    bool succeeded = false;
    double pH = 0.0;
    double chargeBalance = 0.0;
    std::string error;
    std::string engineVersion;
    std::string databaseIdentity;
};

class IPhreeqcAdapter {
public:
    IPhreeqcAdapter(std::string databasePath, std::string databaseIdentity);
    ~IPhreeqcAdapter();
    IPhreeqcAdapter(const IPhreeqcAdapter&) = delete;
    IPhreeqcAdapter& operator=(const IPhreeqcAdapter&) = delete;

    bool initialized() const;
    SolveResult solve(const SolveRequest& request);

private:
    class Implementation;
    std::unique_ptr<Implementation> implementation_;
};

}  // namespace plv
