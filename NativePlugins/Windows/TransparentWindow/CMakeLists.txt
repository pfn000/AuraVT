cmake_minimum_required(VERSION 3.20)
project(AuraVT_TransparentWindow)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Output a DLL
add_library(TransparentWindow SHARED dllmain.cpp)

# Link Win32 libraries
target_link_libraries(TransparentWindow
    user32
    dwmapi
)

# Optimizations matching Unity IL2CPP release build
target_compile_options(TransparentWindow PRIVATE
    $<$<CONFIG:Release>:/O2 /GL>
    $<$<CONFIG:Debug>:/Od /Zi>
)

# Copy to Unity Plugins folder after build
set(UNITY_PLUGIN_DIR "${CMAKE_SOURCE_DIR}/../../../UnityProject/Assets/AuraVT/Plugins/Windows")
add_custom_command(TARGET TransparentWindow POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E make_directory "${UNITY_PLUGIN_DIR}"
    COMMAND ${CMAKE_COMMAND} -E copy
        "$<TARGET_FILE:TransparentWindow>"
        "${UNITY_PLUGIN_DIR}/TransparentWindow.dll"
    COMMENT "Copying DLL to Unity Plugins/Windows"
)
