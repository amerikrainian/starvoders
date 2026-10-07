# StarVadersAccess — Accessibility Mod for StarVaders

Screen-reader accessibility mod for blind players. Port of the guildrun_access architecture
(`C:\Users\CRNTJR3\cs\guildrun_access`): host/module hot reload, the engine-free key-graph Core, Prism
speech, the dev HTTP server, buffers, key help, the Rust installer. Speech is the sole interface: a
silent failure is invisible to the player, so every catch logs, and nothing caches game state.

## Game facts
- **Engine**: Unity **2020.3.49f1**, **IL2CPP** x64, metadata **v27** (il2cpp 27.1), unobfuscated
  (types and members; no ACTk, no string encryption). Steam app **2097570**, game version 2.0.5 at
  build 22588692. Install: `C:\Program Files (x86)\Steam\steamapps\common\StarVaders`
  (`Directory.Build.props`: registry, `STARVADERS_DIR`, `-p:GameDir=`, or a gitignored
  `Directory.Build.local.props`). Ships both Steam and GOG Galaxy SDKs; audio is FMOD.
- **Game code**: almost all in `ScriptAssembly.dll` (namespace `StarVaders` plus the global
  namespace; the PixelCrushers Dialogue System is compiled into it too), small `Assembly-CSharp`.
  **No DI container**: one `StarVaders.SceneController` subclass per scene (`BootupController`,
  `MainMenuSceneController`, `TutorialSelectionController`, `EncounterController`...), additive
  scenes for overlays (`TutorialSelectionSceneAdditive`), a static `DataManager` holding player,
  run and settings data, a few `Instance` singletons (`HoverCursorManager`).
- **Widgets**: NOT uGUI selectables. Interactive elements are `AViewItem` MonoBehaviours (119
  subclasses: `ButtonView`, `MainMenuButtonView`, `OutlinedButtonView`, `PressedButtonView`,
  `CardView`, `TileView`, `Option*`...) with explicit `LeftNav/RightNav/UpNav/DownNav`; clickable
  ones implement `IClickable.OnClick`, which is where the game's click path ends. Text is TMP.
  Descriptions often sit BESIDE a button in its panel, not under it (the tutorial choice).
- **Input**: **new Input System only** (`activeInputHandler = 1`): `UnityEngine.Input` THROWS. Each
  scene has an `InputHandler` (its `PlayerInput`, hover state, `OnHoverViewItemByKey`) and a
  per-scene `*KeyInputController`; the game has full keyboard/controller navigation of its own,
  which we suppress in favour of the key graph (its focus jumps and layouts are not screen-reader
  shaped).
- **Localization**: Unity Localization, eleven locales: `de en es fr ja ko pt-BR ru tr zh-Hans zh-TW`
  (plus a pseudo-locale also coded `en`). `lang/<code>.txt` per locale, `LangFilesTests` checks them.
- **Saves**: `%USERPROFILE%\AppData\LocalLow\Pengonauts\StarVaders\saves` (encrypted:
  `DataManager.EncryptSavesEnabled`). Unity's log: `...\Pengonauts\StarVaders\Player.log`.

## The BepInEx crash and the patcher (read before touching BepInEx)
BepInEx 6 be.788 (vendored) **crashes this game with zero plugins**, ~10 s in. Root cause (dump +
disassembly, 2026-10-07): Il2CppInterop's `GenericMethod_GetMethod_Hook.FindTargetMethod` (unchanged
upstream since 2023, so a newer BepInEx does not help) follows `il2cpp_object_get_virtual_method` ->
`Object::GetVirtualMethod` -> its last jump target, the shim at RVA 0x3849C0; on Unity 2020 with a
one-target shim it rescans past the ret and takes the SECOND target. Here GetMethod (0x384A00)
directly follows the shim, so the pick is GetMethod's first call, a pool allocator (0x389570,
`Alloc(pool, 0x50)`); the `(gmethod, bool)` detour passes 0x50 as `true` = 1 and the heap corrupts
(crashes in `memset`, or `Class::Init` filling a vtable with garbage during URP's
`VolumeManager.ReloadBaseTypes`). **`src/StarVadersAccess.Patcher`** (a preloader patcher in
`BepInEx\patchers\StarVadersAccess`; patchers' `Initialize` runs before Il2CppInterop installs its
hooks on the first type registration) Harmony-postfixes `FindTargetMethod` and corrects exactly that
layout; the log line "Il2CppInterop picked 0x..., a call inside 0x...; hooking 0x..." confirms it.
It must ship in every release zip (`build_release.ps1` does). Worth an upstream PR to Il2CppInterop.

## Decompiled reference (gitignored `game/`; never commit it)
- `game/il2cppdump/dump.cs` (Il2CppDumper, `C:\tools\Il2CppDumper`), `DummyDll/`, `script.json`
  (method addresses: crash RVAs resolve against it), `il2cpp.h`.
- `game/analysis/types.tsv`, `version.json`; `game/porting-assessment.md` (what ports from guildrun).
- Regenerate after a game update: `uv run python tools/python/dump_game.py --keep --check`.

## Tools (`tools/python/`, standard library; `uv run python tools/python/<tool>.py`)
Ported from guildrun, retargeted in `gamelib.py` (app id, folder, `GAME_ASSEMBLY_PREFIXES =
ScriptAssembly, Assembly-CSharp, PixelCrushers, PowerTools`, `SVA_DEV_URL`/`SVA_DEV_PORT`):
`show.py Type`, `holders.py Type`, `census.py --report`, `disasm.py Type.Method --calls` (with
`uv run --with capstone`), `dump_game.py`, `dump_diff.py`, `check_members.py`, and `dev.py`
(`launch`, `kill`, `reload`, `nav`, `input ui.down`, `speech --tail N`, `log --grep X`, `gui`,
`eval file.cs`, `typeinfo`, `actions`, `module`, `screenshot`). Crash dumps: `procdump64 -ma -e -w
StarVaders.exe <dir>` before launching, then `cdb -z <dmp> -c ".ecxr; kn 40; q"`; map frames with
`script.json` (managed) or the hook addresses Il2CppInterop logs at Debug level (runtime).

## Build & deploy
`dotnet build StarVadersAccess.slnx` (or `.\build.ps1`) builds all six projects and deploys host +
Contracts + Roslyn deps + Core + Module to `<game>\BepInEx\plugins\StarVadersAccess\`, the patcher to
`<game>\BepInEx\patchers\StarVadersAccess\`, `prism.dll` next to `StarVaders.exe`. A host, Contracts
or patcher change needs the game closed; Core/Module: `dotnet build
src/StarVadersAccess.Module/StarVadersAccess.Module.csproj` then `dev.py reload` (or F6 in game).
First-time setup: `.\setup-bepinex.ps1`, launch once through Steam (interop generates), build.
Launch: `dev.py launch` (Steam `-applaunch 2097570`, waits for the dev server). Tests:
`dotnet test src/StarVadersAccess.Tests/StarVadersAccess.Tests.csproj` (Core + Contracts, no game).
Release: `build_release.ps1`, `build-installer.ps1`, `create-release.ps1 vX.Y.Z` (guildrun's flow;
version in `Directory.Build.props` alone, notes from `CHANGELOG.md`).

## Architecture (guildrun's, see its CLAUDE.md for the long form)
- **Contracts** (netstandard2.0, permanent), **host** `StarVadersAccess` (net6.0, permanent: Plugin,
  Prism, `HostPump`, `ModuleLoader`, dev server), **Core** (netstandard2.0, reloadable, engine-free,
  unit-tested: key graph, navigator, buffers, input manager, screens, strings), **Module** (net6.0,
  reloadable, engine-coupled: day-to-day work), **Patcher** (net6.0, preloader, permanent), **Tests**.
- Not ported from guildrun yet: audio cues, `TextEdit`/`NumberEdit` (legacy-input text fields),
  the update checker (wire it once the GitHub repo has releases), `ScreenAudit`, `BufferKeys`'
  guildrun buffer names (hero, quest... are placeholders; the ui buffer is the only one).

## Input (the port's one real redesign)
Guildrun reads keys from legacy `Input` and silences the game by disabling the Input System
keyboard. Legacy input throws here, so `Module/Input/OsKeys` reads the OS (`GetAsyncKeyState`, once
per frame, only while the game window is foreground) and `KeyboardBinding` keeps its `KeyCode`
identity (saved bindings, key names) on top; `FocusMode` still disables the Input System keyboard
and the EventSystem's navigation. Keypad Enter shares VK_RETURN with Enter and is not bound
separately. Host F6 uses the same OS read.

## Screens and game access
- `Module/Interop/GameScenes`: the live scene controllers and input handlers, rebuilt only on
  Unity's sceneLoaded/sceneUnloaded; `Controller<T>()`, `Item(controller, name)` (a scene's view
  items by object name, gathered once per controller, keyed by native pointer: a proxy is a fresh
  object per cast, so never compare proxies by reference), `InputFor(gameObject)` (the handler of
  that object's scene: an additive overlay has its own). No per-frame scene scans.
- `Module/UI/ViewNodes`: `Button(item, label, describeFrom)` over `AViewItem`: Enter is
  `IClickable.OnClick`, focus calls the item's scene `InputHandler.OnHoverViewItemByKey` so the
  game's cursor, highlight and tooltip follow; labels are live TMP text (one-letter key prompts
  skipped); `describeFrom` reads a panel's other texts as the spoken description.
- Screens so far: `BootScreen` (title: `BootupController.OnPressAnything`, up while
  `IsFinishedLoadingGame && !IsEndingScene`), `MainMenuScreen` (the game's `VerticalViewItemList`,
  then profile, language dropdown, News/Discord/Credits by mod words), `TutorialSelectionScreen`
  (layer 10, exclusive; Escape is `OnClickBack`), `SettingsScreen` (below), plus guildrun's
  `HelpScreen` and `ModMenuScreen`.
- Options (`SettingsMenuScene`, additive): stops = tabs (`Tab (1)`..`(5)`, click -> `OnPressTab`;
  tabs carry a selected part and the row `entry` is `_currentTab`, since landing on a tab selects
  it), the page (`Panels[_currentTab]`, its top-level view items), and the bottom buttons found
  under `MainPanel` by name (the Graphics page has its own `ApplyButton`: never look these up
  scene-wide). Escape = Apply & Exit (the game wires `OnCancelEvent` to `ApplyButton.OnClick`).
  `UI/OptionNodes` drives rows through their own `OnClick`/`OnLeftPressed`/`OnRightPressed`;
  choosers (`OptionChooser`) are combo boxes in guildrun's dropdown pattern (Enter pushes a
  `ChoiceSubmenuScreen` of the options, a pick sets `_currentOptionIndex` + `UpdateText`, which is
  all the game's arrows do; option text = the key, or `LocalizeSynchronous(key + "_Misc")` when
  `IsLocalized`; no `StateText`, so Enter does not echo the value before the list);
  label = `OptionView.Description`; a tooltip only where the "?" (`TooltipTrigger`) is active:
  several Graphics rows have `HasTooltip` set with the "?" hidden and copied text. Volumes are 0-1
  (read as percent), animation speeds 0-4 whole. Key binding rows are read only (rebinding listens
  on the keyboard focus mode disables, and the game's bindings matter only with focus mode off).

## Dev driver (Debug only; `SVA_NO_DEV=1` disables, `SVA_DEV_PORT` sets the port, default 8772)
Guildrun's server unchanged: `/input`, `/nav`, `/gui`, `/speech`, `/log`, `/eval` (Roslyn, every
interop proxy referenced), `/wait`, `/typeinfo`, `/screenshot`, `/reload`, `/module`, `/health`.
`SVA_NO_SPEECH=1` skips Prism. **Never press a control that starts a tutorial or a run, or changes
the save, through the driver without asking**: verify screens by reading them.

## Keys
F1 key help, arrows, Tab/Shift+Tab, Enter, Backspace (secondary), Escape, Home/End, Alt+Up/Down
sections, type-ahead letters, Ctrl+arrows buffers, Ctrl+Shift+A focus mode, Ctrl+Shift+M mod menu.
