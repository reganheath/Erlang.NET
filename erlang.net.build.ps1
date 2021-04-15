param(
    [switch]$rebuild
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$name = "Erlang.NET"
$MSBuild = Resolve-MSBuild

task . Compile

task Premake -If { $rebuild -or !(Test-Path "$name.sln") } {
    .\premake5.exe vs2019
}

task Compile Premake, {
    if ($rebuild) { $action = 'Rebuild' }
    else { $action = 'Build' }
    RunMSBuild $action Debug
    RunMSBuild $action Release
}

task Clean {
    RunMSBuild Clean Debug
    RunMSBuild Clean Release
    Get-ChildItem .\ -Filter obj -Directory -Recurse | ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
    Get-ChildItem .\ -Filter bin -Directory -Recurse | ForEach-Object { Remove-Item $_.FullName -Recurse -Force -ErrorAction SilentlyContinue }
}

task InstallEpmd {
    $InstallUtil = FindInstallUtil
    & $InstallUtil .\bin\Release\epmd.exe
}

task UninstallEpmd {
    $InstallUtil = FindInstallUtil
    & $InstallUtil /u .\bin\Release\epmd.exe
}

function RunMSBuild()
{
    param(
        $action,
        $configuration
    )
    Write-Host "$action $configuration" -ForegroundColor Yellow
    exec { & $MSBuild "$name.sln" "-t:$action" "-clp:ErrorsOnly" -m -nologo "-p:Configuration=$configuration" }
}

function FindInstallUtil()
{
    $SearchPath = "$env:windir\Microsoft.NET\Framework\"
    $Result = (Get-Childitem -Path $SearchPath -Recurse -force -ErrorAction SilentlyContinue -include InstallUtil.exe) | Sort-Object -Descending | Select-Object -First 1
    if ($null -eq $Result) {
        Write-Error "Cannot locate InstallUtil.exe in $SearchPath"
    }
    return $Result
}
