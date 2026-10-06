set(PLV_CORE_SOURCES
    core/src/abi.cpp
    core/src/material.cpp
    core/src/state_executor.cpp
    core/src/transport.cpp
)

add_library(pharmalab_core SHARED ${PLV_CORE_SOURCES})
target_include_directories(pharmalab_core
    PUBLIC core/include
    PRIVATE ${PROJECT_SOURCE_DIR}/third_party/nlohmann
)
target_compile_definitions(pharmalab_core PRIVATE PLV_BUILD_SHARED)
if(MSVC)
    target_compile_options(pharmalab_core PRIVATE /W4 /permissive-)
else()
    target_compile_options(pharmalab_core PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

if(BUILD_TESTING)
    add_subdirectory(tests)
endif()
