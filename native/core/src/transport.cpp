#include "plv/state_executor.hpp"

// Transport commits are implemented by StateExecutor so source debit,
// receiver credit, overflow routing, revisions, and the event sequence share
// one atomic authority. Live calibrated tool flow layers on this contract.
