-- premake5.lua
defines { "TRACE" }

workspace "Erlang.NET"
    configurations { "Debug", "Release" }

    nuget { "Nito.AsyncEx:5.0.0" }

    filter "configurations:Debug"
        defines { "DEBUG" }
        symbols "On"

    filter "configurations:Release"
        optimize "On"

project "test"
    location "test"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "test/**.cs", "test/**.Config" }
    links { "System", "System.Configuration", "deps/log4net", "Erlang.NET" }

project "Erlang.NET"
    location "Erlang.NET"
    kind "SharedLib"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "Erlang.NET/**.cs", "Erlang.NET/**.Config" }
    links { "System", "System.Configuration", "deps/log4net" }

project "echo"
    location "echo"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "echo/**.cs", "echo/**.Config" }
    links { "System", "System.Configuration", "deps/log4net", "Erlang.NET" }

project "epmd"
    location "epmd"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "epmd/**.cs", "epmd/**.Config" }
    links { "System", "System.Configuration", "System.Configuration.Install", "System.ServiceProcess", "deps/log4net", "Erlang.NET" }

project "ping"
    location "ping"
    kind "ConsoleApp"
    language "C#"
    targetdir "bin/%{cfg.buildcfg}"
    objdir "obj/%{prj.name}"

    files { "ping/**.cs", "ping/**.Config" }
    links { "System", "System.Configuration", "deps/log4net", "Erlang.NET" }
