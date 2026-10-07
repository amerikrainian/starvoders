# build.ps1 - Build StarVaders Access and deploy it into the game. Deploy itself is the host project's
# Debug post-build target (plugin + Core + Module into BepInEx\plugins\StarVadersAccess, prism.dll next
# to StarVaders.exe); this script checks BepInEx is set up and runs the build. Close the game first for a
# HOST change (its DLL is locked while the game runs); Core/Module changes deploy fine with the game
# running and take effect on POST /reload or F6.

param(
    [switch]$Help,
    [switch]$ModuleOnly
)

if ($Help) {
    Write-Host "Usage: .\build.ps1 [-ModuleOnly] [-Help]"
    Write-Host "  Builds the solution and deploys the mod into the game folder."
    Write-Host "  -ModuleOnly builds just the reloadable Core + Module (the hot loop)."
    Write-Host "  Run setup-bepinex.ps1 once first, then launch the game once through Steam."
    exit 0
}

$ErrorActionPreference = "Stop"

$Game = $env:STARVADERS_DIR
if (-not $Game) { $Game = "C:\Program Files (x86)\Steam\steamapps\common\StarVaders" }
if (-not (Test-Path "$Game\StarVaders.exe")) {
    Write-Host "ERROR: StarVaders not found at: $Game (set STARVADERS_DIR)" -ForegroundColor Red
    exit 1
}
if (-not (Test-Path "$Game\BepInEx\core\BepInEx.Core.dll")) {
    Write-Host "ERROR: BepInEx is not installed at $Game\BepInEx. Run setup-bepinex.ps1 first." -ForegroundColor Red
    exit 1
}
if (-not (Test-Path "$Game\BepInEx\interop")) {
    Write-Host "ERROR: $Game\BepInEx\interop does not exist. Launch the game once through Steam" -ForegroundColor Red
    Write-Host "(steam.exe -applaunch 2097570) and wait for the main menu, then re-run this." -ForegroundColor Red
    exit 1
}

if ($ModuleOnly) {
    dotnet build "$PSScriptRoot\src\StarVadersAccess.Module\StarVadersAccess.Module.csproj" -c Debug
} else {
    dotnet build "$PSScriptRoot\StarVadersAccess.slnx" -c Debug
}
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build FAILED." -ForegroundColor Red
    exit 1
}
Write-Host ""
Write-Host "Done. Launch StarVaders through Steam and listen for the startup line, or POST /reload if it is running." -ForegroundColor Cyan
