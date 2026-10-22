#pragma once

#include <string>
#include <string_view>

namespace plv {
std::string sha256Hex(std::string_view bytes);
}
