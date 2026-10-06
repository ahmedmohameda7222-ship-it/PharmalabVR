add_executable(plv_tests
    test_main.cpp
    test_abi.cpp
    test_material.cpp
    test_transport.cpp
)
target_include_directories(plv_tests PRIVATE
    ${PROJECT_SOURCE_DIR}/native/core/include
    ${PROJECT_SOURCE_DIR}/third_party/doctest
)
target_link_libraries(plv_tests PRIVATE pharmalab_core plv_core)
add_test(NAME plv_tests COMMAND plv_tests)
