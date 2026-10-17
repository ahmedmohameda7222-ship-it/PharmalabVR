#include "plv/ideal_reference.hpp"

#include <cmath>
#include <stdexcept>

namespace plv {

namespace {
double residual(const IdealAqueousInput& input, double logHydrogen) {
    const double hydrogen = std::pow(10.0, logHydrogen);
    return hydrogen + input.sodiumMolPerL - input.chlorideMolPerL -
           input.acetateTotalMolPerL * input.ka / (input.ka + hydrogen) -
           input.kw / hydrogen;
}
}  // namespace

IdealAqueousResult solveIdealAqueous(const IdealAqueousInput& input) {
    if (!std::isfinite(input.sodiumMolPerL) || input.sodiumMolPerL < 0.0 ||
        !std::isfinite(input.chlorideMolPerL) || input.chlorideMolPerL < 0.0 ||
        !std::isfinite(input.acetateTotalMolPerL) || input.acetateTotalMolPerL < 0.0 ||
        !std::isfinite(input.ka) || input.ka <= 0.0 || !std::isfinite(input.kw) || input.kw <= 0.0) {
        throw std::invalid_argument("ideal aqueous input outside restricted finite domain");
    }
    double low = -16.0;
    double high = 1.0;
    double lowResidual = residual(input, low);
    const double highResidual = residual(input, high);
    if (lowResidual * highResidual > 0.0) {
        throw std::runtime_error("charge-balance root not bracketed");
    }
    for (int iteration = 0; iteration < 200; ++iteration) {
        const double middle = (low + high) * 0.5;
        const double middleResidual = residual(input, middle);
        if (middleResidual == 0.0) {
            low = middle;
            high = middle;
            break;
        }
        if ((lowResidual < 0.0) == (middleResidual < 0.0)) {
            low = middle;
            lowResidual = middleResidual;
        } else {
            high = middle;
        }
    }
    const double logHydrogen = (low + high) * 0.5;
    const double hydrogen = std::pow(10.0, logHydrogen);
    return {-logHydrogen, hydrogen, residual(input, logHydrogen)};
}

}  // namespace plv
