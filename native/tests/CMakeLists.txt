add_executable(plv_tests
    test_main.cpp
    test_abi.cpp
    test_material.cpp
    test_science.cpp
    test_scheduler.cpp
    test_persistence.cpp
    test_tools.cpp
    test_transport.cpp
)
target_include_directories(plv_tests PRIVATE
    ${PROJECT_SOURCE_DIR}/native/core/include
    ${PROJECT_SOURCE_DIR}/third_party/doctest
    ${PROJECT_SOURCE_DIR}/third_party/nlohmann
)
target_link_libraries(plv_tests PRIVATE pharmalab_core plv_core)
target_compile_definitions(plv_tests PRIVATE PLV_MINTEQ_DATABASE="${CMAKE_SOURCE_DIR}/third_party/iphreeqc-source/database/minteq.v4.dat")
if(WIN32)
    add_custom_command(TARGET plv_tests POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different
            $<TARGET_FILE:pharmalab_core>
            $<TARGET_FILE_DIR:plv_tests>
    )
endif()
add_test(NAME plv_tests COMMAND plv_tests)
