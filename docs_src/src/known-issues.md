# Troubleshooting And Known Issues

## No Speech

If the mod does not speak:

1. Wait. The first launch after installing, and after every game update, takes minutes of silence while the loader generates its assemblies.
2. Confirm `winhttp.dll`, `doorstop_config.ini`, `prism.dll`, `dotnet`, and `BepInEx` sit next to `StarVaders.exe`, the mod is at `BepInEx\plugins\StarVoders`, and its patcher at `BepInEx\patchers\StarVoders`.
3. Launch through Steam.
4. Check the BepInEx log:

   ```text
   C:\Program Files (x86)\Steam\steamapps\common\StarVaders\BepInEx\LogOutput.log
   ```

5. Look for `StarVoders`. Loader problems are at the top of the file.

The log is truncated on every launch, so whatever is in it is from the current session. Attach it to any bug report.

## The Game Crashes About Ten Seconds In

The patcher is missing. The mod loader crashes this game about ten seconds after launch, with or without the mod; `BepInEx\patchers\StarVoders` is what prevents it. Reinstall, or run the installer's repair. If you replace or update BepInEx yourself, keep that folder.

## Shift+Tab Opens Something Else

That is the Steam overlay. Turn it off; see [Installation](installation.md#turn-off-the-steam-overlay-first).

## The Keyboard Stopped Working

You pressed Ctrl+Shift+A, which hands the keyboard to the game. Press it again.

## Mouse Only

Typing is not supported yet: the run seed field and the Compendium search cannot be used.
