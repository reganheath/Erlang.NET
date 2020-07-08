param(
    [switch]$rebuild
)

$ErrorActionPreference = "Stop"
$InformationPreference = "Continue"
$name = "Erlang.NET"
$MSBuild = Resolve-MSBuild

task . Compile

task Premake -If { !(Test-Path "$name.sln") } {
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

function RunMSBuild()
{
    param(
        $action,
        $configuration
    )
    Write-Host "$action $configuration" -ForegroundColor Yellow
    exec { & $MSBuild "$name.sln" "-t:$action" "-clp:ErrorsOnly" -m -nologo "-p:Configuration=$configuration" }
}

# task FindVA -If ($null -eq $script:vadir) {
#     $script:vadir = (Get-ChildItem ${env:ProgramFiles(x86)} -Filter VoiceAttack -Directory).FullName
#     if ($null -eq $script:vadir) {
#         $script:vadir = (Get-ChildItem $env:ProgramFiles -Filter VoiceAttack -Directory).FullName
#     }
#     if ($null -eq $script:vadir) {
#         Write-Error "Failed to locate the VoiceAttack installation folder"
#     }
#     Write-Host "Voice Attack found in $script:vadir"
# }

# task InstallVAPlug VAPlug, FindVA, {
#     $target = "$script:vadir\Apps\VAEDSrv"
#     mkdir "$target" -ErrorAction SilentlyContinue | Out-Null
#     Write-Host "Copying plugin to $target"
#     $files = @('ErlEI.dll', 'ErlEiCS.dll', 'VAEDSrv.dll')
#     $files | ForEach-Object { Copy-Item "$priv\$_" $target }
# }