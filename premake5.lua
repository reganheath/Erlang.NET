-- premake5.lua
defines { "TRACE" }

workspace "Erlang.NET"
    configurations { "Debug", "Release" }
    platforms { "AnyCPU" }

project "test"
    location "src/test"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "src/test/**.cs", "src/test/**.Config" }
    links { "System", "System.Configuration", "deps/log4net", "Erlang.NET" }

    filter "configurations:Debug"
        defines { "DEBUG" }
        symbols "On"

    filter "configurations:Release"
        optimize "On"

project "Erlang.NET"
    location "src/Erlang.NET"
    kind "SharedLib"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "src/Erlang.NET/**.cs", "src/Erlang.NET/**.Config" }
    links { "System", "System.Configuration", "deps/log4net" }

    filter "configurations:Debug"
        defines { "DEBUG" }
        symbols "On"

    filter "configurations:Release"
        optimize "On"

project "echo"
    location "src/echo"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "src/echo/**.cs", "src/echo/**.Config" }
    links { "System", "System.Configuration", "deps/log4net", "Erlang.NET" }

    filter "configurations:Debug"
        defines { "DEBUG" }
        symbols "On"

    filter "configurations:Release"
        optimize "On"

project "epmd"
    location "src/epmd"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "src/epmd/**.cs", "src/epmd/**.Config" }
    links { "System", "System.Configuration", "System.Configuration.Install", "System.ServiceProcess", "deps/log4net", "Erlang.NET" }

    filter "configurations:Debug"
        defines { "DEBUG" }
        symbols "On"

    filter "configurations:Release"
        optimize "On"

project "ping"
    location "src/ping"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "src/ping/**.cs", "src/ping/**.Config" }
    links { "System", "System.Configuration", "deps/log4net", "Erlang.NET" }

    filter "configurations:Debug"
        defines { "DEBUG" }
        symbols "On"

    filter "configurations:Release"
        optimize "On"
