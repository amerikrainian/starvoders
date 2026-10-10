# StarVoders

A [StarVaders](https://store.steampowered.com/app/2097570/) mod that makes the game playable by
blind users.

## Install

Download `StarVodersInstaller.exe` from the
[latest release](https://github.com/amerikrainian/starvoders/releases) and run it. It finds
your Steam install, downloads the mod, and can later update, repair, or uninstall it. Manual
alternative: extract the release zip over the game folder (the one holding `StarVaders.exe`).

The first launch after installing generates the loader's interop assemblies and can take a few
minutes; the mod says "StarVoders loaded" once the game is up.

## Keys

Press F1, anywhere. It lists the keys that do something right where you are; Enter on a row runs
that key for you.

The basics are a screen reader's: arrows move, Tab and Shift+Tab cycle control groups, Enter
activates, Escape backs out, and typing letters searches the focused group. Ctrl+Shift+A hands
control back to the game and takes it again. Ctrl+Shift+M opens the mod menu. Ctrl+Left and
Ctrl+Right switch review buffers, Ctrl+Up and Ctrl+Down step through one.

## Building from source

```
dotnet build StarVoders.slnx -c Debug                                # builds and deploys into the game
dotnet test src/StarVoders.Tests/StarVoders.Tests.csproj       # unit tests
```

Run `setup-bepinex.ps1` once first, then launch the game once through Steam so the interop
assemblies exist. A release is `build_release.ps1` (the mod zip), `build-installer.ps1` (the
installer exe) and `create-release.ps1 vX.Y.Z` (the GitHub release from a pushed tag and the
CHANGELOG section).

Runs on [BepInEx 6](https://github.com/BepInEx/BepInEx) (vendored); speech via
[Prism](https://github.com/ethindp/prism). The architecture, tooling and installer come from
[Guildrun Access](https://github.com/amerikrainian/guildrun-access); the installer is adapted from
the [Non-Visual Calculus](https://github.com/rashadnaqeeb/NonVisualCalculus) installer by Rashad
Naqeeb (MIT).
