cmake_minimum_required(VERSION 3.20)
project(imagesharp_aom_benchmark LANGUAGES C CXX)

# Link the explicitly supplied current-main Release reference build. This adapter is benchmark-only;
# it is never referenced by ImageSharp or copied by the production project.
set(AOM_SOURCE_DIRECTORY "" CACHE PATH "Verified official libaom source directory")
set(AOM_BUILD_DIRECTORY "" CACHE PATH "Matching optimized Release libaom build directory")
add_library(imagesharp_aom_benchmark SHARED aom_benchmark.c)
target_include_directories(imagesharp_aom_benchmark PRIVATE "${AOM_SOURCE_DIRECTORY}")
find_library(AOM_LIBRARY NAMES aom PATHS "${AOM_BUILD_DIRECTORY}" NO_DEFAULT_PATH REQUIRED)
target_link_libraries(imagesharp_aom_benchmark PRIVATE "${AOM_LIBRARY}")
set_target_properties(imagesharp_aom_benchmark PROPERTIES C_STANDARD 11 LINKER_LANGUAGE CXX)
