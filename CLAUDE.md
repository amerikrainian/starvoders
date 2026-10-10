# StarVoders — Accessibility Mod for StarVaders

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
`VolumeManager.ReloadBaseTypes`). **`src/StarVoders.Patcher`** (a preloader patcher in
`BepInEx\patchers\StarVoders`; patchers' `Initialize` runs before Il2CppInterop installs its
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
ScriptAssembly, Assembly-CSharp, PixelCrushers, PowerTools`, `SVO_DEV_URL`/`SVO_DEV_PORT`):
`show.py Type`, `holders.py Type`, `census.py --report`, `disasm.py Type.Method --calls` (with
`uv run --with capstone`), `dump_game.py`, `dump_diff.py`, `check_members.py`, and `dev.py`
(`launch`, `kill`, `reload`, `nav`, `input ui.down`, `speech --tail N`, `log --grep X`, `gui`,
`eval file.cs`, `typeinfo`, `actions`, `module`, `screenshot`). Crash dumps: `procdump64 -ma -e -w
StarVaders.exe <dir>` before launching, then `cdb -z <dmp> -c ".ecxr; kn 40; q"`; map frames with
`script.json` (managed) or the hook addresses Il2CppInterop logs at Debug level (runtime).

## Build & deploy
`dotnet build StarVoders.slnx` (or `.\build.ps1`) builds all six projects and deploys host +
Contracts + Roslyn deps + Core + Module to `<game>\BepInEx\plugins\StarVoders\`, the patcher to
`<game>\BepInEx\patchers\StarVoders\`, `prism.dll` next to `StarVaders.exe`. A host, Contracts
or patcher change needs the game closed; Core/Module: `dotnet build
src/StarVoders.Module/StarVoders.Module.csproj` then `dev.py reload` (or F6 in game).
First-time setup: `.\setup-bepinex.ps1`, launch once through Steam (interop generates), build.
Launch: `dev.py launch` (Steam `-applaunch 2097570`, waits for the dev server). Tests:
`dotnet test src/StarVoders.Tests/StarVoders.Tests.csproj` (Core + Contracts, no game).
Release: `build_release.ps1`, `build-installer.ps1`, `create-release.ps1 vX.Y.Z` (guildrun's flow;
version in `Directory.Build.props` alone, notes from `CHANGELOG.md`).

## Architecture (guildrun's, see its CLAUDE.md for the long form)
- **Contracts** (netstandard2.0, permanent), **host** `StarVoders` (net6.0, permanent: Plugin,
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
  (layer 10, exclusive; Escape is `OnClickBack`), `SettingsScreen` (below), `CutsceneScreen`,
  `ConfirmPromptScreen`, `GlobeScreen`, `TipScreen`, `EncounterScreen` (all below),
  `EncounterWinScreen` (`EncounterWinSceneAdditive`: "Victory", the stars earned, the loot box
  `Box (1)` whose click is `GoToRewardScene`, up while `!IsEndingScene`; opening it leaves the
  fight: never through the driver), `RewardScreen` (`CardRewardScene`, `RewardController` for
  cards, upgrades and artifacts: title `TitleText`, the shown `CardRewardBoards` and
  `ArtifactRewardBoards` left to right by x, Enter takes one (`AddRewardToDeck`: never through
  the driver), Skip = `ProceedButton`, Reroll and the run status from `UI/CampaignNodes`; card and
  artifact nodes shared with the deck viewer in `UI/CardNodes`), `PauseScreen`
  (`PauseMenuSceneAdditive`, layer 30, exclusive (what it opens sits above it: Options, the Compendium
  pages and the guide 35, the deck viewer 36, the confirm prompt 37; a Compendium page leaving stays up, empty, until
  its scene unloads, else the pause menu is announced twice around the scene change): "Paused", its buttons by object name, the run's
  difficulty, Deck and Modifiers, the map via `CampaignNodes.Map(panel)`; Escape = `OnResumePressed`;
  Save & Exit and Skip Tutorial/Abandon Run: never through the driver), `WorkshopScreen`
  (`WorkshopScene`, `ShopController`, named by `ShopTitle_Misc`: one stop of the wares, cards
  top row first then artifacts, each "name, 8900 Stars" (the drawn price; the model's is a
  hundredth), "Sale!, was 5700" (price on `SaleText`, `BuyButtonText` crossed out), "Bundle!, 2
  copies" (bought twice), "disabled" while the price is drawn red (`Text.color !=
  originalColor`); then Remove Card, Restock, the tip jar (`MinFundraiserPanel` figures), Gacha and
  red button when shown, Exit; buying, tipping, Restock and Exit: never through the driver; the
  unlock canvas is not read yet), `CompendiumScreen` (`GlossarySceneAdditive`, `GlossaryController`,
  layer 35, exclusive: class tabs (`ClassButtons`, locked = `IsDisabled()` read "LOCKED", landing
  shows the class), the count panel (Enter = `ArrowRightButton`, the victory view), Filters
  (`IsFilterPanelActive`: `FilterPanel`'s sections by their `Title`, `FilterButton.IsSelected`), the
  items by `verticalLayoutGroup` child order with `GlossaryPilotGroup` titles; `IsLockedMode`/
  `IsUndiscoveredMode` items read only the game's LOCKED/UNDISCOVERED; never bind a
  `DeckCardView` click there (it is the shop's removal path); search is mouse-only; Escape closes
  the filters, else `ExitScene`; the count is labelled by class and the Type filter: it counts what
  is shown), the other Compendium pages (each its own additive scene, layer 35, exclusive; their
  bottom row and Back in `UI/GlossaryNodes`; Escape = `ExitScene`): `BestiaryScreen`
  (`InvaderGlossaryController`: act tabs (`_currentTab` indexes `InvaderGroups`, the shown group's
  Canvas enabled), Advanced Invaders `ToggleButton`, boss `SimpleChooser`; each `InvaderGlossaryPanel`'s
  holders by `EntityModel` (`Unknown` = not met: the game's `Unknown_EntityTitle`), its lore from
  `RevealedObject` or "Lore hidden, 39/50 defeated" from `RequirementText`; `HiddenObject`'s text is
  alien script, never read), `AlbumScreen` (`PilotGlossaryController`, one pilot a page, name from
  `Pilots[_currentPilotIndex]` (drawn as an image), Left/Right = `OnDown/OnUpButtonPressed`; a
  locked page reads only `LockedObject`, the page under it is placeholder Lorem ipsum; Memories
  play cutscenes: never through the driver), `TrophiesScreen` (`AchievementGlossaryController`:
  completion, Global Stats rows as label + child figures, fave card and artifact, `_achBlocks` by
  their shown side; a counter's `LeftCounter` is a hidden template, read the active one),
  `NewsScreen` (`NewsSceneAdditive`, `NewsSceneController`, layer 10, exclusive: `MainPanel`'s
  `NewsArea` texts ordered by drawn height (the signature is second in the hierarchy, drawn last),
  split into lines; the two link buttons open the browser; `OnExitPressed` fades unguarded, so the
  screen stands down for that controller once left), `CreditsScreen` (`CreditsScene`, a full scene,
  `CreditsController`, also after the ending: the scroll as a list in `ScrollingPanel` hierarchy order;
  a title pairs with the childless text drawn nearest below it (the nesting is uneven: Fund Manager
  holds the next roles), multi-line name lists joined by commas; Toggle Speed, Back =
  `OnBackButtonPressed`, down while `IsTransitioning`), `GuideScreen` (`GuideSceneAdditive`, the pause
  menu's Show Guide, layer 35: tabs from `LeftSide` (General/Gunner/Stinger/Keeper = pages 0-3, the
  `*Locked` twins read LOCKED, never clicked), `GuidePages[currentPageIndex]`'s prose = the TMPs named
  `Line1..4` (the rest label pictures: a mock HeatView, "New!", "Will whiff!"), a demo artifact;
  `LeaveScene`, empty while `IsEndingScene` as the Compendium pages), plus guildrun's
  `HelpScreen` and `ModMenuScreen`.
- Cutscenes (`OpeningScene` = Full Tutorial's start, `EndingScene`): a `PlayableDirector` that
  pauses on `CutsceneDialogueBubble`s; the live one is `_dialogueBubbleHandler._currBubble`
  (`CutsceneDialogueHelper`). One node per bubble, "speaker: line"; the game draws no names, so the
  speaker is the Dialogue System actor ("Display Name" field, else `Name`; never localized):
  `ActivateNextDialogue` opens `_dialogueBubbles[_currIndex - 1]` with entry `_currIndex -
  _dialogueDatabaseIndexOffset` then increments `_currIndex`, so after it the shown bubble is
  `[_currIndex - 2]`, entry `_currIndex - 1 - offset`; a `LocKeyOverride` bubble (Roxy's 13 "wait"s,
  shown unpaused) takes its actor from the key's last `_` segment. The bubble's click only finishes
  the typewriter while any shown bubble's `TextAnimator._maxVisibleCharacters` is short of
  `textInfo.characterCount`, so Enter `SkipTypewriter` + `ShowAllCharacters` first, then clicks:
  one press always advances. Escape = `OnSkipCutscene` (the confirm prompt). Clicking the LAST
  line, or YES on the skip, starts the tutorial run.
- Confirm prompt (`ConfirmPromptSceneAdditive`, `ConfirmPromptController`, statics
  `LocalizationKeyToShow`/`ActionToPerform`/`IsProceedOnly`): title + the setup's message name
  the dialog context, then `NormalSetup` YES/NO (`YesButton`, `NoButton`) or `ProceedOnlySetup`
  `OKButton`; focus lands on `ViewItemToStartAt` (NO). Escape clicks NO (or OK). Yes/No guard only
  on `SceneLoader.IsTransitioning` and the prompt fades before unloading, so once answered the
  screen stays up with no nodes until the scene goes (standing down hands a second Enter to the
  screen beneath).
- Encounter choice (`GlobeScene`, `GlobeController : CampaignSceneController`): stops tip (while
  `TipManager.CurrentTip` shows in this scene; Enter = `Tip.Destroy(true)`, what a finished tip
  box's click does), encounters (`CurrentRoomChoicePanels`; invaders by `EnemyEntityModel.EnemyID
  .EnemyName`, reward by its tooltip title; invader descriptions and every tooltip in the ui
  buffer), Proceed/Reroll (`ButtonEnabled`), then `UI/CampaignNodes` (Doom, Stars, Deck; the act
  timeline: kind from the active `Pip`/`StarPip`/`BossPip` child named by the map tooltip's
  legend, cleared = `XPip.enabled`, current = first not cleared). **Clicking the encounter that is
  already selected is `ProceedButton.OnClick` (the fight starts)**, as in the game, and the
  tutorial preselects its one encounter: never Enter on an encounter through the driver.
- Game strings: `Interop/GameLoc` (`<EnemyName>_EntityTitle`/`_EntityDesc`,
  `<ArtifactName>_ArtiTitle`/`_ArtiDesc` through `LocalizeSynchronous`; the async
  `LocalizationUtils.Localize*` helpers build those keys), `UI/GameText` (rich text, Text
  Animator tags, `<sprite name=X>` read as X). `CampaignNodes.Tooltip`: a game `TooltipTrigger`'s
  `toolTipObject` texts (first = title).
- `Readers/PopupReader` (module tick, components via `GameScenes.Components<T>`, gathered once per
  scene change): the tip box text when a new `CurrentTip` shows, `ActOverlay`'s act and Chrono
  Token overlays on their fade-in (test: `o.StartCoroutine(o.View_StartActOverlay(1))`,
  `View_StartReplenishText()`), and `SpeechBubble`s under a `SpeechBubbleManager` ("Roxy: line"
  when the `SpeechName` has a `Name_` prefix; test: `CreateSpeechBubble(SpeechName.Roxy_Excited,
  1, 0, 0, 3, false)`).
- Tips (`UI/Tips`): `TipManager.CurrentTip` while `_isShowing`; it waits for a click when its box
  has `TipBox.LineGlow` (or a `ClickToContinueLine`), and then `TipScreen` (layer 40, exclusive)
  holds it, Enter = `Tips.Continue` (`box.OnClick` + `_isFinished` if still typing, then the static
  `TipManager.ClickTipBox`); other tips are the popup reader's.
  Between chained tips the fight is uncovered for a moment: `EncounterScreen.HoldsArrival` (Core
  `Screen.HoldsArrival`) keeps its name and landing until `FightEvents.Settled` and no tip is
  `Tips.Coming` (`IsCreatingTip` or `TipQueue`); test chains with two `TipManager.Tip` calls (the
  second queues), never `AddTipTask` (it builds the tutorial's tasks: draws, triggers).
- The fight (`EncounterScene`, `Fight/*`, `EncounterScreen`): every press is the game's click,
  `EncounterInputHandler.OnHoverViewItemByKey(item)` then `ClickViewItems()` (cards, buttons, and a
  running selection's answer alike). Model: `EncounterController.EncounterModel` (`GridModel`:
  `GridShape`, not fixed: 5x7 and 5x9 seen, so read it, `Coord` 0-based with y UP; `ModelItemDict` holds every model, dead entities
  kept; an entity's coord via `GetEntityCoord(model.ID.Cast<EntityID>())`). Spoken tiles are
  "x, y" 1-based, row 1 at the bottom. Stops: Targets (while `SelectionController.IsSelecting`:
  `EncounterView.ViewsItems` filtered by `IsSatisfied`, under `ScreenView.SelectionPrompt`; focus
  jumps there; Escape = `SelectionController.Cancel` + preview/display board/hover resets; a card
  that needs no target still asks for the `PlayAreaView`, read "Play"), Board (rows top first,
  shared row key, lands on the mech; cell = target mark, coords, zone on crossing, contents),
  Hand (`GetCardsInHandButNotBeingPlayed`, `CardPlayView.GetCardView`; text from
  `<CardName>_CardTitle` and `_CardDesc` through `LocalizationUtils.GetModifiedDescriptions`),
  Rack, Status. Labels are game strings (`*_Misc` keys in the "Everything" table: search it with
  `StringDatabase.GetTable("Everything").SharedData.Entries`). Keys in `Fight/FightKeys` (Game
  category): digits, Shift+arrows skip, C T D H O M, Shift+M, `[ ] , .` categories (danger zones
  step by region, "5, 1 to 5, 5, targeted by Shooter 2", merged by `Core/Fight/TileRanges` as the
  targets events are), Alt+Backspace, E; Backspace reads details (`*_InfoLine` keywords).
- Fight events (`Fight/FightEvents`, Core `Fight/FightDiff` with tests): snapshot at settle
  points (task engine idle 3 frames, or waiting on the player: a selection or a click tip, else
  an enemy turn's changes would wait for the next card to resolve), diff, speak each event as its own queued entry,
  append to the uncapped combat buffer. Ticked before `ScreenManager` so an event
  precedes the landing it causes. Invaders numbered within their kind by `EnemyID.Number` order.
- Choosing a target (`Fight.ReadTargets`: the selection's targets sorted into tiles, `PlayAreaView`,
  `DirectionSelectionArrow`s, others): a selection with grid targets is answered ON THE BOARD (its
  prompt said, focus to the board or kept there with the game's hover synced; Enter on a cell =
  the tile, else the play area from any cell, else the arrow toward the cell from the selection's
  `BaseCoord` or the mech, else "not a target"; "target" marks valid tiles and the cells next to
  the origin in a valid direction; a "targets" category). Only selections without grid targets
  (cards) get the Targets list stop. Direction selections are untested (no such card yet).
- Strike marks (`Fight.StrikeTiles`): tiles whose `TileView._targetPreviewRenderer` (or `Front`) is
  enabled, set by `EnableTargetTile` from `StrikeTileEffectTask` and kin in preview mode, and by
  delayed artillery. A card's preview runs on hover only in mouse mode (`OnViewItemHoverEnter`
  gates on `DeviceManager.IsMouse`/`IsInKeyboardAndMouseMode`), but `SelectionTask` previews as
  its selection opens in any mode (Flamethrower: the cone is its pre-selection task, the burn its
  post). Cells say "strike"; a selection's opening says "Strikes <ranges>; hits <names>" once,
  waiting up to 30 frames for the marks. The card's cost and tasks wait for the pick, so a first,
  fully cancellable selection backs out with nothing spent.
- Fight figures: read `EncounterModel.Values[EncounterValue.Doom/Heat/MaxHeat/Power/...]`, never
  the panels: hovering End Turn or a card PREVIEWS on them (Doom panel "5/5" with the model at 3).
  Doom's maximum is the panel's "/5" (a preview leaves it alone).
- Card viewer (`DeckViewSceneAdditive`, `DeckViewSceneController`, statics `Type` (`DeckSceneType`:
  RemoveCard, PlayerData, PileView, Glossary, StarterDeck) and `PileToShow`): `DeckViewScreen`,
  title by the pile its first card is in (no title drawn), `DeckCardView`s from `_cardGrid` and
  `_cardPilotUniqueGrid`, artifacts, Back its own stop; Escape = `ExitScene`. In RemoveCard mode a
  card's click removes it: never Enter on a card there through the driver.
- Options (`SettingsMenuScene`, additive): stops = tabs (`Tab (1)`..`(5)`, click -> `OnPressTab`;
  tabs carry `NodeVtable.IsSelected` (unspoken: landing selects a tab anyway) and the row `entry` is `_currentTab`, since landing on a tab selects
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

## Dev driver (Debug only; `SVO_NO_DEV=1` disables, `SVO_DEV_PORT` sets the port, default 8772)
Guildrun's server unchanged: `/input`, `/nav`, `/gui`, `/speech`, `/log`, `/eval` (Roslyn, every
interop proxy referenced), `/wait`, `/typeinfo`, `/screenshot`, `/reload`, `/module`, `/health`.
`SVO_NO_SPEECH=1` skips Prism. Roslyn 4.8 wants System.Collections.Immutable and
System.Reflection.Metadata 7.0, the game's runtime has 6.0: `CSharpEvaluator.InstallDependencyResolver`
(Default context `Resolving`) binds them to 6.0 before the LoadFrom handler tries the plugin folder's
7.0 copies and throws on the first eval or two. **Never press a control that starts a tutorial or a run, or changes
the save, through the driver without asking**: verify screens by reading them.

## Keys
F1 key help, arrows, Tab/Shift+Tab, Enter, Backspace (secondary), Escape (on a screen with no
Back of its own, `Navigation.BackFallback` = `UI/Pause`: the scene's `PauseButtonView` click, as the
game's Menu key; the fight's Escape cancels a selection first), Home/End, Alt+Up/Down
sections, type-ahead letters, Ctrl+arrows buffers, Ctrl+Shift+A focus mode, Ctrl+Shift+M mod menu.
