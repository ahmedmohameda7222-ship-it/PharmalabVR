#pragma once

namespace plv {

struct IdealAqueousInput {
    double sodiumMolPerL = 0.0;
    double chlorideMolPerL = 0.0;
    double acetateTotalMolPerL = 0.0;
    double ka = 1.8e-5;
    double kw = 1.0e-14;
};

struct IdealAqueousResult {
    double pH = 0.0;
    double hydrogenMolPerL = 0.0;
    double chargeResidualMolPerL = 0.0;
};

IdealAqueousResult solveIdealAqueous(const IdealAqueousInput& input);

}  // namespace plv
