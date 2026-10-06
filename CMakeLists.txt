cmake_minimum_required(VERSION 3.24)
project(PharmaLabVR VERSION 0.1.0 LANGUAGES C CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_CXX_EXTENSIONS OFF)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)

include(CTest)
set(IPHREEQC_ENABLE_MODULE OFF CACHE BOOL "Disable legacy Fortran wrappers" FORCE)
add_subdirectory(third_party/iphreeqc-source EXCLUDE_FROM_ALL)
add_subdirectory(native)
