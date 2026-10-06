set(PLV_CORE_SOURCES
    core/src/ideal_reference.cpp
    core/src/indicator_model.cpp
    core/src/iphreeqc_adapter.cpp
    core/src/journal.cpp
    core/src/material.cpp
    core/src/mixing_model.cpp
    core/src/scheduler.cpp
    core/src/solver_worker.cpp
    core/src/state_executor.cpp
    core/src/tool_models.cpp
    core/src/transport.cpp
)

add_library(plv_core STATIC ${PLV_CORE_SOURCES})
target_include_directories(plv_core PUBLIC core/include)
target_link_libraries(plv_core PUBLIC IPhreeqc)
find_package(Threads REQUIRED)
target_link_libraries(plv_core PUBLIC Threads::Threads)

add_library(pharmalab_core SHARED core/src/abi.cpp)
target_include_directories(pharmalab_core PUBLIC core/include PRIVATE ${PROJECT_SOURCE_DIR}/third_party/nlohmann)
target_link_libraries(pharmalab_core PRIVATE plv_core)
target_compile_definitions(pharmalab_core PRIVATE PLV_BUILD_SHARED)
if(MSVC)
    target_compile_options(plv_core PRIVATE /W4 /permissive-)
    target_compile_options(pharmalab_core PRIVATE /W4 /permissive-)
else()
    target_compile_options(plv_core PRIVATE -Wall -Wextra -Wpedantic -Werror)
    target_compile_options(pharmalab_core PRIVATE -Wall -Wextra -Wpedantic -Werror)
endif()

if(BUILD_TESTING)
    add_subdirectory(tests)
endif()
