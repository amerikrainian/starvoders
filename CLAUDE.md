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
Player docs: mdbook in `docs_src` (`mdbook build docs_src`; `book/` gitignored), in guildrun's tone:
how things work and the non-obvious keys, not each screen's readout. `build_release.ps1` ships the
rendered book as `StarVodersDocs`; `.github/workflows/build-docs.yml` publishes it to GitHub Pages on
a push to main touching `docs_src` or `CHANGELOG.md` (the change log page includes `CHANGELOG.md`).

## Architecture (guildrun's, see its CLAUDE.md for the long form)
- **Contracts** (netstandard2.0, permanent), **host** `StarVoders` (net6.0, permanent: Plugin,
  Prism, `HostPump`, `ModuleLoader`, dev server), **Core** (netstandard2.0, reloadable, engine-free,
  unit-tested: key graph, navigator, buffers, input manager, screens, strings), **Module** (net6.0,
  reloadable, engine-coupled: day-to-day work), **Patcher** (net6.0, preloader, permanent), **Tests**.
- Not ported from guildrun yet: audio cues, `NumberEdit` (the mod's own number entry),
  `ScreenAudit`, `BufferKeys`' guildrun buffer names (hero, quest... are placeholders).
- Buffers (`UI/Buffers`, sources in `Fight/FightBuffers`): ui (the focused control), hand (each card
  as its focus reads it: `GraphAnnouncer.LeafText` over `EncounterScreen.Card`), player (in a fight:
  Doom, `Fight.Resources`, bombs, Summons, the mech line, Stars; between fights the portrait's Doom and
  Stars, `CampaignNodes.StatusLines`), enemies (invaders, described + tile, top row first), combat. Only
  ui follows focus; a focus change rewinds them all (combat to its newest) and keeps review where it is.

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
  then profile, language dropdown, News/Discord/Credits by mod words; Resume is followed by the run's
  delete icon, `ResumeButton/Background/Delet/ButtonTemplate` = `DeleteRunButtonPressed`, read "Delete
  Current Run", which asks first), `PilotSelectScreen` (`CharacterSelectionScene`,
  `MechSelectionController`: the classes by panel (`GunnerPanel`/`MeleePanel`/`MysticPanel`, locked
  while `MeleeLockImage`/`MysticLockImage` show, with their condition and checklist), Random, Back; in a
  class (`IsInPilotSelection`, `CurrentPilotSelectionController`) the pilot "Roxy, 1/4" with Left/Right
  = the arrows, the panel by CanvasGroup alpha (open: complexity = count of "=" glyphs, description,
  starter deck and artifact, Go!; locked: its condition or checklist and its own `SelectPip`; Go! is
  slid off screen, not faded, so it is offered only with the open panel), the class level, Guide;
  empty while `IsTransitioning`), `DifficultyScreen` (`DifficultySelectionSceneAdditive`, layer 10:
  radio buttons; Enter = `OnLevelNPressed(false)`, select only: the game's click on the selected one
  starts the run; Go! = `OnStartPressed`, never through the driver; Go! and Challenges are never
  hovered: the game's keyboard hover on either clears the selected difficulty; the seed: see Text fields),
  `ChallengesScreen` (`ChallengesSceneAdditive`, `ChallengesSceneController`, the difficulty's Challenges,
  layer 11, one stop as the difficulties: `ChallengeTextLineList` as a row of radio buttons by title,
  entry = the selected (`_selectedImage`), `IsLocked` reads LOCKED; Enter = its `OnClick`
  (`OnClickChallenge`: select only); then the selected one's `CustomStartObject` ("Custom Loadout, Deck":
  `_deckButton` = the deck viewer), the `ModVerticalLayoutGroup` lines, mod artifacts, its trophy by
  sprite (`ObtainedTrophy` = the game's Completed, `TrueEnding`/`UltimateEndingObtainedTrophy` add the
  ending), the pilot by `PilotTitleImage` sprite, Total Completed, Start (a run: never through the
  driver), Back = `OnClickBack`; empty while `IsEndingScene`),
  `ProfilesScreen` (`SaveProfileSceneAdditive`, `SaveProfileSceneController`, layer 10: the page's three
  `SaveProfileGroup` slots, a saved one followed by its `_deleteButton` "Delete File 1", page arrows, Back
  = `OnClickBack`; switching, creating and deleting profiles: never through the driver),
  `MoreInfoScreen` (`MoreInfoSceneAdditive`, `MoreInfoSceneController`, layer 38, over anything: the
  `_currentDisplayBoardPanel`'s texts, the keyword lines under `_moreInfoLineParent`, the linked cards
  and invaders `MoreInfoObjs` (Enter = their click, nested), Back while `PreviousObjects` is non-empty,
  Close = `ExitScene(0)`; test: a pilot's `PilotSelectArtifactView.OnClick()`),
  `ModifiersScreen` (`CheckModifiersSceneAdditive`, the pause menu's MODIFIERS, layer 35: the pilot by
  its name sprite (`T_RoxyName`), the lines under `ModVerticalLayoutGroup`, `ArtifactModHolder`'s
  artifacts, Back = `OnClickBack`), `QuickGuideScreen` (`QuickGuideSceneAdditive`, the tutorial
  choice's Quick Guide, a new profile's: `Pages[_currentPage]`, "1/3" with Left/Right =
  `OnClickNext/PreviousPage`, prose by `UI/GuidePages` (shared with the guide), Next/Previous/Back/START
  by name; START begins a run, never through the driver; leaving unloads it before the tutorial choice
  reloads, so it is held, empty, until `TutorialSelectionController` is back, 2 s at most),
  `TutorialSelectionScreen`
  (layer 10, exclusive; Escape is `OnClickBack`), `SettingsScreen` (below), `SandboxScreen`
  (`SandboxSceneAdditive`, `SandboxSceneController`, the Extras page's SANDBOX SETTINGS, layer 36,
  exclusive: the intro line, then the `VerticalLayoutGroup`'s rows in order under contexts from its
  headers (one text = a section; `Banned (1)` = a ban list's column title, `PackDesc` its rule as a
  line); ban-list checkboxes read banned/allowed (checked = `DisabledPacks`/`DisabledBosses`), locked
  ones only the game's LOCKED; Reset to Default (confirm prompt), Apply & Exit = `SaveAndExit`, also
  the game's cancel, so Escape; empty while `IsEndingScene`), `CutsceneScreen`,
  `ConfirmPromptScreen`, `GlobeScreen`, `TipScreen`, `EncounterScreen` (all below),
  `EncounterWinScreen` (`EncounterWinSceneAdditive`: "Victory", the stars earned, the loot box
  `Box (1)` whose click is `GoToRewardScene`, up while `!IsEndingScene`; opening it leaves the
  fight: never through the driver), `RewardScreen` (`CardRewardScene`, `RewardController` for
  cards, upgrades and artifacts: title `TitleText`, the shown `CardRewardBoards` and
  `ArtifactRewardBoards` left to right by x (a strip each), Enter takes one (`AddRewardToDeck`: never through
  the driver), Skip = `ProceedButton`, Reroll and the run status from `UI/CampaignNodes`; card and
  artifact nodes shared with the deck viewer in `UI/CardNodes`), `PauseScreen`
  (`PauseMenuSceneAdditive`, layer 30, exclusive (what it opens sits above it: Options, the Compendium
  pages, the guide and Modifiers 35, the deck viewer 36, the confirm prompt 37; a Compendium page leaving stays up, empty, until
  its scene unloads, else the pause menu is announced twice around the scene change; the pause menu
  itself likewise stays up, empty, once `IsEndingScene`, until its scene unloads: Abandon Run keeps it
  loaded under the run end, else the fight beneath is announced first): "Paused", its buttons by object name, the run's
  difficulty, Deck and Modifiers, the map via `CampaignNodes.Map(panel)`; Escape = `OnResumePressed`;
  Save & Exit and Skip Tutorial/Abandon Run: never through the driver), `WorkshopScreen`
  (`WorkshopScene`, `ShopController`, named by `ShopTitle_Misc`: one stop of the wares, the cards
  rack as strips, a row each as drawn (`CardNodes.Strips`/`DrawnRows`: a view lower than its row's
  first by over half its drawn height starts a row; rows share a key, so Up/Down keep the column),
  then the artifacts' strip (`CardNodes.CardsLabel`/`ArtifactsLabel`: the game's `FilterCards_Misc`/
  `FilterArtifacts_Misc` words as contexts, only when both kinds show; the rewards and the card
  viewer do the same), each "name, 8900 Stars" (the drawn price; the model's is a
  hundredth), "Sale!, was 5700" (price on `SaleText`, `BuyButtonText` crossed out), "Bundle!, 2
  copies" (bought twice), "disabled" while the price is drawn red (`Text.color !=
  originalColor`); then Remove Card, Restock, the tip jar (`MinFundraiserPanel` figures), Gacha and
  red button when shown, Exit; buying, tipping, Restock and Exit: never through the driver),
  `MinUnlockScreen` (the full fundraiser's `MinUnlockCanvas`, `ShopController.currentUnlockCanvas`, layer
  15, exclusive: a dialog named by `UnlockTitle`, the card/artifact board's model as the rewards read it
  (Enter = more info), `MiscUnlockText`, Take/Skip or Continue; holds until opaque, 3 s at most; down
  once the canvas `IsEndingScene`; test: load `WorkshopScene` from the encounter choice with
  `SceneLoader.LoadSceneWithTransition`, tip the jar full, click it), `PackmasterScreen` (Bud's Factory, `PackmasterScene`,
  `PackmasterController`, a `CampaignSceneController`: the belt's `CardViews` (one strip) under "Pick 2 Cards", Enter =
  the card's click `OnPressSelectCardButton` (toggles `_isSelected`, "selected" live), Proceed (enabled at
  2; adds them and leaves: never through the driver), Reroll; the `PackViews` (a strip under Reroll, in the main stop, top first) by `<Pack>_Misc` and
  `<Pack>_Desc_Misc` (drawn only as pictures), Enter = their click (the Compendium via
  `GlossaryTransitionSceneAdditive`), the frame's PACK SYSTEM tooltip; status, map; holds its arrival
  until every shown card is `_interactable` (they ride in on the belt), 3 s at most),
  `SpecialRewardScreen` (`SpecialRewardScene`, `SpecialRewardController`: title from `TextesObject`, the
  `SpecialRewardDescription` over `_rewardViewItems` (card/artifact reward boards as one strip, display only), Take All =
  `AcceptRewardViewItem`, Skip = base `ProceedButton` ("+1000", the star glyph "=" dropped), Reroll; status,
  map; holds until the box has opened; taking/skipping: never through the driver; the kind is the static
  `CurrentSpecialReward` but the offer follows the run's room data: test by setting it and
  `SceneLoader.LoadSceneWithTransition(SpecialRewardScene)` from the encounter choice), `DailyRunScreen`
  (`DailyRunSceneAdditive`, `DailyRunController`, layer 10, exclusive, over the main menu once every pilot
  is unlocked (`SettingsSceneController.UnlockPilots()` asks first through the confirm prompt; the main
  menu shows the button from its next load): the pilot by the static `PlayerData.PilotName` + subtitle,
  `ModVerticalLayoutGroup`'s lines, the pilot and mod artifacts, the deck as a row (Left/Right, under
  "Deck"), Start (a run: never through the driver),
  Back, the reset countdown, Total Completed by `StreakNumber`; all one stop),
  `CompendiumScreen` (`GlossarySceneAdditive`, `GlossaryController`,
  layer 35, exclusive: class tabs (`ClassButtons`, locked = `IsDisabled()` read "LOCKED", landing
  shows the class), the count panel (Enter = `ArrowRightButton`, the victory view), Filters
  (`IsFilterPanelActive`: `FilterPanel`'s sections by their `Title`, `FilterButton.IsSelected`), the
  items by `verticalLayoutGroup` child order with `GlossaryPilotGroup` titles; `IsLockedMode`/
  `IsUndiscoveredMode` items read only the game's LOCKED/UNDISCOVERED; never bind a
  `DeckCardView` click there (it is the shop's removal path); the search: see Text fields; Escape closes
  the filters, else `ExitScene`; the count is labelled by class and the Type filter: it counts what
  is shown; its figures are left out while `isShowingCoroutine` (the game's `ResetCountPanel` "???", "??% found"),
  the live part says them when they come), the other Compendium pages (each its own additive scene, layer 35, exclusive; their
  bottom row and Back in `UI/GlossaryNodes`; Escape = `ExitScene`): `BestiaryScreen`
  (`InvaderGlossaryController`: act tabs (`_currentTab` indexes `InvaderGroups`, the shown group's
  Canvas enabled), Advanced Invaders `ToggleButton`, boss `SimpleChooser`; each `InvaderGlossaryPanel`'s
  holders by `EntityModel` (`Unknown` = not met: the game's `Unknown_EntityTitle`), its lore from
  `RevealedObject` or "Lore hidden, 39/50 defeated" from `RequirementText`; `HiddenObject`'s text is
  alien script, never read), `AlbumScreen` (`PilotGlossaryController`, one pilot a page, name from
  `Pilots[_currentPilotIndex]` (drawn as an image), Left/Right = `OnDown/OnUpButtonPressed`; a
  locked page reads only `LockedObject`; stats pair each `... Answer` text with the question beside it of the
  same name less " Answer"; Memories by the digit each draws (`MemoryButtonNumbers`: one character, which
  `ViewNodes.Label` skips as a key prompt), LOCKED while `MemoryButtonLocks` shows, the page under it is placeholder Lorem ipsum; Memories
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
  `LeaveScene`, empty while `IsEndingScene` as the Compendium pages), `DialogueScreen` (layer 20,
  exclusive: any Dialogue System conversation, `CutsceneConvManager.PlayConversation`: the Chaos God, pilot
  backstories, Min; the one `DialogueManager.standardDialogueUI` in DontDestroyOnLoad; up while
  `isConversationActive`, empty between lines; the line = the open subtitle panel whose `currentSubtitle`
  is the state's entry, "speaker: line" (drawn `portraitName`, else `speakerInfo.Name`, actor "None" =
  narration), id by conversation:entry; Enter finishes the typewriter then clicks `continueButton` (a
  `DialogueButtonView`; its listener `OnFastForward` only finishes a typing line and ignores clicks while
  the panel animates); responses = the open menu panel's `ViewItemUIResponseButton.dialogueButtonView`s,
  focus jumps to them, the line said first if focus never reached it; Escape in the scenes that are all
  conversation, the Rift (`RiftSceneController`, after a won run) and a pilot's backstory
  (`PilotBackstorySceneController`, also the Album's memories), is their `OnSkipCutscene` (the confirm prompt's
  SKIP CUTSCENE?), elsewhere its fallback; backstory tested from the Album, the Rift not (it leads on into the
  Chaos God scene); only the Chaos God's one-line
  conversation seen live), `ChaosGodScreen` (`ChaosGodScene` after the conversation: the `UnlockCanvas`
  dialog, "Timeline Divergence", its `ChaosUnlocks` left to right by x (the list is Mid, Left, Right),
  hover text + name, Proceed = `ProceedButton`, leaves the scene: never through the driver; the game
  switches Proceed on before the panel and icons fade in, so the screen holds its arrival (declares
  nothing) until the canvas and every named or mid-fade icon reach full alpha, 3 s at most;
  `ShowCampaignUnlocks(LevelUp(...))` pairs the list with icons by index and the list is never stored,
  so `ChaosGodScreen.Names` Harmony-prefixes `ChaosUnlock.InitializeEnemy`/`InitializeRoom` to record
  the name: `<Enemy>_EntityTitle`, rooms `BossTitle_<Room>_Misc` or Cloudy/Holes!/Void Zone
  `*Encounter_Misc`; panel not yet seen live, names tested by calling the initializers on the hidden
  icons), `ChaosGlobeScreen` (`ChaosGlobeScene`, `ChaosGlobeController`, after the final Chaos God
  conversation: its one `Button`, read by the drawn `RegularTitle`/`UltimateTitle` ("The Overseer"), Enter =
  the click (`OnChaosGodPressed`, the final fight); its `MapPanel` sits under the switched-off
  `CampaignPortrait`, so no timeline; no Back; down once `IsEndingScene`; test: `/eval`
  `SceneLoader.LoadSceneWithTransition(SceneName.ChaosGlobeScene)`), `AchievementScreen` (layer 45, exclusive, mod-made: "Achievement unlocked, dialog", the
  game popup's title and description, OK = `OK_Misc`, Escape too, queued; fed by `PopupReader`: the
  popup blocks under `AchievementPopupManager.Instance._popupHolder` (DontDestroyOnLoad) are slid in
  and out and never destroyed, and lose their `AchievementBlock` component, so new = a new child
  pointer, those present at module load taken as seen; after a dismissal it stays up, empty, while the game
  owes more (`_popupQueue`, or ones it dequeued not yet shown: it dequeues, then spawns the block, whose texts
  come later), 8 s at most, so the screen beneath is not announced between queued popups; `AddToPopupQueue`
  earns too (the popup coroutine calls `PlatformManager.EarnAchievement`): re-queue only earned ones; never `AddToPopupQueue` to test, it earns the
  Steam achievement: clone a block under the holder), `UnlockCardScreen` (the run end's Unlock:
  `CampaignEndController._currUnlockCanvas`, layer 15, exclusive, a dialog named by the card's title;
  kinds = the active `Single`/`Set`/`Misc`/`PackUnlockCanvas`; the pilot by `UnlockCardScreen.Pilots`, a
  Harmony prefix on `PilotFrontPortraitView.SetPortraitView` (no localized pilot names; + the game's
  `<Pilot>_Subtitle_Misc`), set/pack card and artifact views as their drawn texts (untested), the
  description lines, OK = `OK_Misc` = the card's `OnClick` (next card or back), Escape too; holds
  until faded in and a description line has text (it arrives by callback), 3 s at most;
  `Initialize` takes `First` of `LevelUnlocks`, else `AchievementUnlocks`, and REMOVES it, so the
  lists hold the cards still to come, not the one shown; the prefab's portrait is Min/Max), plus guildrun's
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
- Special rewards on an encounter ("Gain 2 copies of a random LEGENDARY card", "Gain the following
  SPECIAL artifact:" + the drawn `ArtifactSpecialView`): the game has no reward tooltip for them
  (`ToolTipTitle_SpecialReward_Misc` is missing), so the Reward trigger keeps the prefab's placeholder
  "DECK (10 cards, 3 artifacts)": skipped; the special line is read instead ("…: Cosmic Dust", its
  description in the buffer). The status row's Deck reads "Deck, 12 cards, 2 artifacts" from
  `DataManager.PlayerData` (the game's title "DECK (12 card, 2 artifact)" shouts and has no plural); Stars
  is `GameText.Calm`ed ("STARS" -> "Stars"). A curse's "+ PERMANENT" (`CurseText`) is read after its
  curse modifier ("Dark Channel, Permanent"): `SetCurseMod` alone shows it (with `IsCursed`,
  `CurseArtifactName` and the modifier's icon; `SetCurse` is never called), so the two always pair.
- Encounter choice (`GlobeScene`, `GlobeController : CampaignSceneController`): stops tip (while
  `TipManager.CurrentTip` shows in this scene; Enter = `Tip.Destroy(true)`, what a finished tip
  box's click does), then ONE stop of horizontal rows, top to bottom: the act timeline and the
  status (`UI/CampaignNodes.Strips`: `Map`/`Status` with `row: true`; every run screen (rewards,
  Workshop, Bud's Factory, special rewards) opens its main stop with them, landing by `SetStart` on
  its first item, and the pause menu's run stop with the timeline; timeline kind from the active `Pip`/`StarPip`/`BossPip` child named by the map
  tooltip's legend, cleared = `XPip.enabled`, current = first not cleared, Up into the row lands
  on it), the encounters (`CurrentRoomChoicePanels`, the landing: selected, else first; invaders
  by `EnemyEntityModel.EnemyID.EnemyName`, reward by its tooltip title; invader descriptions, each
  followed by its more-info links (`Fight.EntityInfoLines`: keywords and the `MoreInfoCardNames`/items/
  invaders it names, the Pixie's Shimmer; the fight's cell details and the Bestiary use it too), and
  every tooltip in the ui buffer; the secret quest after them, while offered: `_instantiatedSecretQuestPanel`,
  the Apocalypse final quest's offer (shown on the act's last encounter choice of a run with the `Finale`
  run mod when the quest level is the act number), read as its `TakeButton` "Accept Strange Offer, Dark
  Channel, Permanent, The LINK" (curse with the drawn "+ PERMANENT", key), descriptions in the ui buffer,
  Enter = the button (`OnAcceptPressed`: both join the run); the panel's own click only selects it, so it
  is not offered; test: instantiate `SecretQuestPanel` under `ChoicePanelHoldersSet2[1]`, `Initialize(Random,
  globe)`, set `_instantiatedSecretQuestPanel`), Proceed, then Reroll under it (`ButtonEnabled`). **Clicking the encounter that is
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
  "x y" 1-based, row 1 at the bottom. Stops: Targets (while `SelectionController.IsSelecting`:
  `EncounterView.ViewsItems` filtered by `IsSatisfied`, under `ScreenView.SelectionPrompt`; focus
  jumps there; Escape = `SelectionController.Cancel` + preview/display board/hover resets; a card
  that needs no target still asks for the `PlayAreaView`, read "Play"), Board (rows top first,
  shared row key, lands on the mech; cell = target mark, contents, coords, zone on crossing ("mech, 3 2", "Hole, 4 2");
  tile statuses by the game's `<Status>Title_Misc`/`TileStatus_Misc`, which exist only for Cloud, Hole, Cracked:
  `FightBoard.StatusName` gives TrueHole (the Overseer fight's column: `IsMoveableCoord` refuses it, a push
  carries the entity across) the mod's "Rift", Nebula (strikes do nothing there) and Vortex (a cut boss) mod
  words, DarkCracked Cracked's name and no description),
  Hand (in the Board's stop, a strip under the bottom row, `strictColumns`: card n under column n,
  Down from a column with no card or Up from a card past the last column stays; keyed apart while a
  target is chosen on the board, so no arrow reaches a card; a choice begun from the hand moves focus
  to the last cell, else the mech; Alt+H jumps to card 1, Home/End = first/last card;
  `GetCardsInHandButNotBeingPlayed`, `CardPlayView.GetCardView`; text from
  `<CardName>_CardTitle` and `_CardDesc` through `LocalizationUtils.GetModifiedDescriptions`;
  play modifiers (`CardPlayModel.PlayCardModifiers`, never in the model's `HeatCost`) as the art
  draws them: the cost icons, a `Minus` icon is a point taken off (only those = Free), the `_repeatText`
  "x3", Purge/Flow added = modded vs default card args; test: `AddPlayCardModTask` reusing a live
  mod's `StartCondition`, then `RemovePlayCardModTask`; Burnt/Frozen are traits (7, 12), read with
  the rest; the rarity after the traits, on every card node and pickup (`Fight.CardRarity`: More Info's set,
  Starter/Common/Rare/Legendary by `Rarity*_Misc`, plus Cosmic, the mod's word (the art's icon,
  `CardImageView.SetRarityIcon`, marks it; Starter's only with the `AcademyPack`); Created/Junk/
  Trader... are shown nowhere, so silent; More Info's own `RarityText` is a panel line; artifacts the same, after the trait, `Fight.ArtifactRarity`:
  Starter/Common/Legendary/Special, Modifier/Created silent); "not enough Power" = the game's `CardView.WillUnpowered` (darkened icons), "spends Battery"
  = `PlayCardCostTask`'s rule (the mark shows only in its preview); not read: the keep mark (only
  the deck viewer draws it), the autoplay and freeze previews (they need the game's hover preview,
  mouse mode only), the burn warning. A Gunner's cards all cost Heat (`BaseEnergyCost`): test Power by
  setting a hand card's `PowerCost`, `Values[Power/PowerCell]`, `UpdateView()`, then restore),
  Spells (its own stop, for a Keeper: `ManaView.SpellViews`, not in `ArtifactViewDict`, then "Empty slot"
  up to `PlayerData.spellSlots` (3, Grimoire +2; the panel draws empty frames, `HasEmptySpellSlotCondition`
  and the Spell Reward's `RewardFilter` compare against it): buttons, Enter = the click (cast), "Cooldown: N, disabled" while
  `CooldownObject` shows (its text is stale while hidden), refused when `Used`/`Destroyed`; test:
  Iris starts with Conjure), Rack (End Turn; the Transformer's Draw, `PowerView.DrawButton`, shown
  when the run has the artifact (`PowerView.Initialize`), once a turn = `DrawButtonLock`; test:
  `TaskEngine.QueueTask(new CreateArtifactTask(ArtifactName.Transformer))`), Status. The class energy: the game initializes all three panels (Heat 0/1, Power 2/3, Mana 4/5) and
  shows one (`GetActiveEnergyView` takes the first shown); `Fight.Resources` reads each shown: Heat,
  Power + Battery (`PowerCell`, while `BatteryMockView` shows; Power over max becomes Battery, spent when
  Power runs out), Mana + its gain ("+3 / Turn", `_manaRegenText`). Labels are game strings (`*_Misc` keys in the "Everything" table: search it with
  `StringDatabase.GetTable("Everything").SharedData.Entries`). Keys in `Fight/FightKeys` (Game
  category): digits, Shift+digits (the card as the hand buffer reads it; `GameKeyBinding` with `shift`), Shift+arrows skip, C T, Ctrl+D H O M B (Doom: a Run-category key (`UI/RunKeys`, with Ctrl+S Stars), live in any screen declaring `RunKeys.Categories`, i.e. the run's screens and the overlays over a run scene, from `DataManager.PlayerData` between fights (Stars: `starbucksAmount` is a hundredth of the drawn figure); the energy (`Fight.Resources`), objective, mech, bombs: `GridModel.PlayerBombList` of the game's constant 3, plus Summons `PlayerPuppetList` over `PuppetLimitView._maxPuppets` for a Keeper or while one is out; the bomb counter popup is spoken only during a target choice, once), Shift+M, Alt+H (the hand), I J K L as the arrows, U O N M the board's diagonals (silent at its edge), `[ ] , .` categories (Shocked = any entity `IsShocked`, after Invaders; Summons = the tiles of `PlayerPuppetList`'s entities, `PuppetItem`s,
  left out of "Bombs and items"; danger zones
  step by region, "5 1 to 5 5, targeted by Shooter 2", merged by `Core/Fight/TileRanges` as the
  targets events are), Alt+Backspace; the game's own fight keys are `Input/GameKeyBinding`s, the keys
  of the game's action (`PlayerInput.actions` of the fight's handler: one asset, the main menu loads
  the saved rebinds into it, `effectivePath` carries them), so its Options rebinding carries over:
  digits (`SelectCard1..0`), E (`EndTurn`), A/S (`Deck`/`Discard`: the pile's click), R (`Reroll`:
  Rewind), F (`MoreInfo`: Backspace's details); T stays ours (the game's guide). Backspace reads details (`*_InfoLine` keywords; on a pickup
  the whole card as the hand reads it, from `GridModel.PickupMap`'s `CardID` via `CardPlayView.GetCardView`;
  test pickups: `QueueTask(new CreateRandomCardsAsPickupNearbyTask())`).
- Fight events (`Fight/FightEvents`, Core `Fight/FightDiff` with tests): snapshot at settle
  points (task engine idle 3 frames, or waiting on the player: a selection or a click tip, else
  an enemy turn's changes would wait for the next card to resolve; End Turn to the next turn is ONE busy stretch,
  so `FightEvents.Phases`, a prefix on `TaskEngine.LogTaskExecution` (the game's per-task log call in
  `ProcessTask`, live engine only, not preview), also snapshots as `EnemyPhaseTask`, each
  `ProcessEnemyPhaseActionTask`/`ProcessEnemyStartTurnActionTask` (each invader's action, chronological) and
  `StartTurnTask` start, the phase taken from the task (the model leaves the player's turn at the discard): end-of-turn
  effects, then "Invader Turn" and what the invaders did, then the new turn; the combo is caught before
  `StartTurnTask` resets it to 0), diff, speak each event as its own queued entry,
  append to the uncapped combat buffer. Snapshot energy: Heat, Power, Battery, Mana (not the regen);
  pickups by card id: "Pickup: Dash appears at 4 7", "Pickup: Dash destroyed" when gone and not drawn.
  Every pile by card id: a card in no pile and not in the hand before, now in the draw or discard pile, is
  "Poof! added to Draw Pile" (the piles' `DrawPileTitle_Misc`/`DiscardPileTitle_Misc`; the purge pile has no
  title, so purged and pickup cards are tracked only so leaving them is not news). A spell whose
  `CooldownObject` stops showing is "Conjure ready" (`fight.ready`).
  Doom channelling: `GridModel.IsChannelingDoom(id)` (the game's "WARNING! +1 Doom"); the invader's
  `Charge` is then the Doom it adds, so it reads "channelling 1 Doom" instead of "charge 1" ("channelling
  Doom" at charge 0: Doom Ritual's pick), events on start/stop, details `TileDoom_InfoLine` (in the doom
  rows) or `AttackDoom_InfoLine`. Artifacts gained or lost mid-fight are events (`ArtifactViewDict` by view
  pointer); an artifact the game greys out (`ArtifactView.UsedObject`: a once-a-turn effect spent) reads
  the game's "Used" (`UsedArtifact_Misc`) first on its node, no event (it would fire every turn). Overheating says the game's "Overheat!" when `OverheatTask` starts (the task-log prefix); the
  Heat sink is the Heat maximum ("Heat 0/5" after Fire Starter). Booby Traps (Kaia's artifact) name every
  pickup "Booby Trap" (the word before the colon of `BoobyTrap_InfoLine`) and add that line to its details.
  Immunity (`AEntityModel.IsInvulnerable`, the Mothership's comes and goes) has no keyword: read "Immune"
  (`FloatTextImmune_Misc` without its "!") in the entity's state and its gain ("Mothership: Immune") and
  loss, details `Invulnerable_InfoLine`; test by setting the flag on an invader through `/eval`. Ticked before `ScreenManager` so an event
  precedes the landing it causes. Invaders numbered within their spoken name by kind, then `EnemyID.Number` (the Overseer fight's three Chaotic
  Detritus kinds count as one), except one boss's parts drawn as a single creature (`FightBoard.OneCreature`: Mega-Blob's
  core and pieces, Nessie's head and body), read unnumbered; Kraken tentacle segments by tentacle, from the base to
  the tip ("Left Kraken Tentacle 4"), from `KrakenCoreModel.Left/Middle/RightTentacleEntities` (boxed `EnemyID`s
  behind the `EntityID` interface: `il2cpp_object_unbox`, never the proxy's `ToString`). An entity gone from the board but in
  `GridModel.OffGridEntities` (a Queen Bee in its Hive) "left the board", grouped as the destroyed are; one
  removed by `DissipateEntityTask` "flew away" (its `_flyAway`: a Fallen Angel, a Chrono Bird) or "dissipated"
  (a Void Rift), one on the tile of a `WormEatTask` "eaten" (Nessie's Apples): both caught by the task-log prefix,
  the id unboxed from the task's `Args`, kept for the fight (`FightSnapshot.Taken`). Tile statuses are in the
  snapshot: a change reads by range per status, "4 8 now Hole", "1 4 no longer Cloud" (a Cloud used up blocking a
  strike); test with `AddTileStatusTileEffectTask(boxed Coord, TileStatus.Cloud)` then a strike there. A Fallen
  Angel's or Chrono Bird's charge reads as its countdown, "flies away next turn" (`FightBoard.Countdown`: gone as the
  charge would reach 2), its change an event (`EntitySnap.Note`).
  `GameText.Clean` drops space just inside brackets (`GetModifiedDescriptions` pads them: "( 3 left )").
- The Keeper (Mystic) has no entity of its own: `GridModel.PlayerID` stands for the Summon it haunts
  (`CurrentPuppet`), `GetPlayerCoord` is that Summon's tile, and `ModelItemDict` has no player. So the
  board's `PlayerTile` comes from `GetPlayerCoord` (while `IsEntityOnGrid(PlayerID)`), the cell is the
  Summon's, read "Sol, haunted" (details add `Haunt_InfoLine`), and Ctrl+M, Shift+M, the Mech category and
  the landing use that tile; with no Summon the landing is the bottom-left cell. A Summon's once-a-turn
  Activate (Enter on its cell: the game's click) is ready while it has the hidden `EntityTrait.Charged`
  ("Activate ready"); recharge cards add it back (`AddTraitTask`). The summon counter popup ("1/3") takes
  `Summon_Keyword` (`PopupReader.Labelled`). Test Keeper mechanics in a Xenn run: Sol & Luna, Align,
  Implode, Astral Call.
- Choosing a target (`Fight.ReadTargets`: the selection's targets sorted into tiles, `PlayAreaView`,
  `DirectionSelectionArrow`s, others): a selection with grid targets is answered ON THE BOARD (its
  prompt said, focus to the board or kept there with the game's hover synced; Enter on a cell =
  the tile, else the play area from any cell, else the arrow toward the cell from the selection's
  `BaseCoord` or the mech, else "not a target"; "target" marks valid tiles and the cells next to
  the origin in a valid direction; a "targets" category). A selection whose targets are all
  cards of the hand (`TargetSet.InHand`: Exhaust's "Choose a card to purge"; the game outlines them
  in place, it draws no list) is answered IN THE HAND: prompt said, focus to the first target card,
  targets read "target, <card>", Enter on another card or on a cell says "not a target", Escape
  cancels (tested with `CreateCardTask(Il2CppSystem.Enum.ToObject(Il2CppType.Of<CardName>(), ...),
  Pile: Hand, rarity: new Il2CppSystem.Nullable<Rarity>())`; the rarity must not be null). Every other
  non-grid target gets the Targets list stop (before the board), also beside grid targets: every Jab
  variant (Jab, Power, Quick, Flicker, Left Foot, Spirit, Jab'racadabra) is ONE selection taking an
  adjacent entity's tile OR the `EncounterView.ExtraSelectionPanel` ("Draw 1 Card",
  `Selection.ExtraSelectionChoice`); the prompt names the extras ("Choose an entity, or Draw 1 Card")
  and focus stays on the board (with no entity adjacent, the list alone, focused). Direction selections (Discharge, Kunai's throw, a Summon's push, Pusher) are answered on the board: the cells
  next to the origin read "target"; the origin is `BaseCoord`, a `Coord` or an `AValue` to `Get()` (Pusher's
  is its first pick, a `TargetValue`), else the mech. An arrow whose cell is off the board (the game offers
  pushing into the wall: it strikes the entity) goes to the Targets list, named with the prompt. A card can chain selections (Kunai: a hand card, then a direction;
  Nether Lash: two entities): `EncounterScreen` tracks the game's `CurrentSelection` pointer
  (`Fight.SelectionId`), so each step is announced and focused as a new one.
  Repeats: a repeat's selection (`Selection.IsRepeat`; Flamethrower x2's second burn, not its first) shows
  Skip Repeats (`EncounterView.CancelRepeatsSelectionPanel`, click = `SelectionController.Cancel(true)`), and
  while it shows the game's Cancel key IS that button (`EncounterKeyInputController.OnCancel`); a plain
  `Cancel(false)` just starts the same repeat again, so a repeat with nothing to choose softlocked the mod's
  Escape. The button is the rack's first item; a selection the game found empty (`!HadValidChoices`, set as
  it starts) says its prompt + "no choices" and focus goes to Skip Repeats. Test: the reported save
  (`profile_0.zip`), Flamethrower x2 with Shift as the only other card.
- Strike marks (`Fight.StrikeTiles`): tiles whose `TileView._targetPreviewRenderer` (or `Front`) is
  enabled, set by `EnableTargetTile` from `StrikeTileEffectTask` and kin in preview mode, and by
  delayed artillery. A card's preview runs on hover only in mouse mode (`OnViewItemHoverEnter`
  gates on `DeviceManager.IsMouse`/`IsInKeyboardAndMouseMode`), but `SelectionTask` previews as
  its selection opens in any mode (Flamethrower: the cone is its pre-selection task, the burn its
  post). Cells say "strike"; during a selection "Strikes <ranges>; hits <names>" is said whenever
  the marks settle (3 frames unchanged) on a set other than the last said: hovering a target
  re-previews its strike (no mouse gate on that path), so Titan Rocket's area follows the cursor;
  settling on no marks forgets the last set. The card's cost and tasks wait for the pick, so a first,
  fully cancellable selection backs out with nothing spent.
- The Overseer (`Fight/Overseer`, the final fight, `ChaosBossModel`): never on the grid. Phase 1: the
  Hands of Time (shielded = closed; strike the shield off, then the open hand: the Overseer loses 1, the hand
  re-shields and turns Immune; their health is a hit counter from 99, so `FightBoard.ShownHealth` hides it,
  numbering is by spoken name so the three Chaotic Detritus kinds count as one) and a Rift column; health =
  the drawn `ChaosAct1HealthBarView` (`Phase1HP` / `MaxPhase1HP`), hidden after phase 1. Phase 2: Chaos
  Swarms fill the board; phase 3: Chaos Hearts and Detritus (`Phase3HP`, 4, is drawn nowhere, so not read).
  Status item and the enemies buffer's first line ("The Overseer 5/6", else "The Overseer, phase 2");
  snapshot `Boss`/`BossPhase` events; the phase from `ChaosBossTransitionToPhaseNTask` (the task-log
  prefix), but 1 whenever the bar shows (Rewind). Name: `ChaosGodGlossaryTitle_Misc` (ultimate:
  `ChaosGodUltimateEnding_Misc`). The game has no Overseer objective and leaves "Defeat Kraken!" on its
  screen: not read (`EncounterScreen.Objective` is null there); the screen is named by the Overseer and
  Ctrl+O reads its status item. Test: from an encounter choice, `SceneLoader.LoadSceneWithTransition(ChaosGlobeScene)`, then
  `ChaosGlobeController.OnChaosGodPressed()` (from inside a fight it resumes that fight instead); strike with
  `StrikeTileEffectTask` (box the `Coord` with `il2cpp_value_box`), set `Phase1HP` to skip ahead, Rewind to
  return. **Never leave this fight through the driver: abandoning it counts as a won run (achievements).**
- Boss panels (`Fight/BossCounters`), made by the boss's view at runtime beside the board: the Grandmaster's
  hourglass (`HourglassInstance.NumberText` = its charge, so the board drops the Grandmaster's "charge N":
  `FightBoard.ShownCharge`), the Hive's timer (`QueenBeeCoreView.CounterInstance._currHoneyTimer`: pips that
  fill as time runs out, enabled and scaled up = filled, read "The Hive 1/4"; its number text is hidden), the
  Hemophage's health bar (`ClonerView.HealthbarInstance`, "Hemophage 12/25"; its health events are the
  panel's, `BossCounters.ReadsHealth`), the Kraken Heart while drawn above the board (`KrakenCoreModel.TheoreticalPosition`,
  a row lower each turn, until it comes onto the board: "Kraken Heart, 3 rows above the board"). Titles/descriptions `GrandmasterHourglass*`, `QueenBeeCounter*` (its
  "[#]" = `GetAllEnemiesWithName(QueenBee)`), `ClonerHealthbar*` `_Misc`. Status items after the turn, the
  enemies buffer's first lines, snapshot `Counters` events. Test: from an encounter choice,
  `RoomFactory.CreateRoomModel(<BossRoomChoiceGeneratorDataSO in CurrentCampaign.Act1RoomGeneratorList>
  .BossRooms[...].RoomLevel1, globe.Random, false, new List<EnemyName>(), new List<RewardName>())`, set
  `DataManager.CurrentRoomModel`, `LoadSceneWithTransition(EncounterScene)`; from inside a fight it resumes the
  saved one, so abandon (a defeat) and start a run each time.
- A lost fight: `LoseEncounterTask` loads `CampaignLoseTransitionAdditive` (scene 30) straight to the run
  end; nothing in the code loads `EncounterLoseSceneAdditive` (16, `EncounterLoseController` Rewind/Lose) by
  a fixed number, so it is treated as unused. Test a loss with
  `EncounterController.TaskEngine.QueueTask(new LoseEncounterTask())` (it ends the run: back up the save).
- Fight figures: read `EncounterModel.Values[EncounterValue.Doom/Heat/MaxHeat/Power/...]`, never
  the panels: hovering End Turn or a card PREVIEWS on them (Doom panel "5/5" with the model at 3).
  Doom's maximum is the panel's "/5" (a preview leaves it alone).
- Card viewer (`DeckViewSceneAdditive`, `DeckViewSceneController`, statics `Type` (`DeckSceneType`:
  RemoveCard, PlayerData, PileView, Glossary, StarterDeck) and `PileToShow`): `DeckViewScreen`,
  title by the pile its first card is in (no title drawn), `DeckCardView`s from `_cardGrid` and
  `_cardPilotUniqueGrid` (each grid as strips, a row each as drawn, Up/Down keeping the column), artifacts (strips too; the game draws them above the cards; the final quest's relic first, drawn apart in
  `ChaosQuestObject`/`ChaosQuestArtifactView`; the starter deck's is the pilot's
  starter artifact), a Keeper's spells (`SpellViews`, apart from the artifact grids; a row under "Spells",
  then "Empty slot" up to the drawn frame, `KeeperObjects3`/`KeeperObjects5`), Back last in the same stop (outside the cards' positions); Escape = `ExitScene`. The game creates the artifacts first and
  the cards over the next frames, so the screen holds its arrival until the card count holds 0.15 s
  (an empty pile 0.5 s, 1.5 s at most), else focus lands on the artifact. In RemoveCard mode a
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
  (read as percent), animation speeds 0-4 whole. Key binding rows rebind (`Screens/KeyCaptureScreen`, a
  child of the options): the game's `UIRebindActionBehaviour.StartRebindProcess` (`OptionKeybind.OnClick`)
  is Unity's interactive rebind, listening on the Input System keyboard focus mode disables, so the
  screen waits for every key up, lends the keyboard (`FocusMode.LendKeyboard`), clicks, stands the mod
  down (`CapturesRawInput`) while the row's `waitingText` shows, takes the keyboard back and waits for
  every key up again (else Enter or the new key reaches the rebind or the mod). The game excludes arrows
  and the mouse, Escape cancels; a Harmony prefix on `RebindingOperation.Start`, armed for that click,
  also excludes the modifiers AND the synthetic `shift`/`ctrl`/`alt`/`anyKey` (Alt alone binds anyKey,
  then alt). After: the row reads its key (the prompt's TMP while shown; some keys, Enter, are drawn as a
  picture with the TMP hidden and stale, so then the key is named from the binding), then "C is also Coordinates" for a bare mod key it shares
  (the game swaps a key another of its actions had; that is not announced). The game saves bindings on Apply
  and on every tab press (`SaveManager.SaveBindings`): a test rebind is on disk, so put the key back
  (`RemoveAllBindingOverrides`, then `SaveBindings` of the asset's json).

## Text fields
`UI/TextEdit` (guildrun's, keys from `OsKeys`): the game's own `TMP_InputField` edits, selected and
activated as a click does; TextMesh Pro reads Unity's IMGUI event queue, which focus mode leaves
alone, so typing, the caret, selection, Ctrl+A and the clipboard are the game's. The mod's keys stand
down meanwhile (`InputManager.TextFieldFocused`, until the ending key comes up; type-ahead too).
Each step is echoed from the field's state before and after (`Core/UI/EditEcho`). Enter ends the edit with focus resting on the field (Enter there edits again, with no word of its
own: the field selecting its text is echoed, "123 selected"); Escape ends it silently and runs the
screen's Back, as on any control (the text kept); Tab and Shift+Tab end it and move on, and so do
Up and Down in a one-line field; focus leaving any other way stops it. A field is edited the moment focus lands on it, taking focus once every key is up (a held arrow
would move its caret) (`TextEdit.OnLanding`, ticked from the screen's
`OnUpdate`; `editOnLanding: false` would wait for Enter); Up and Down leave a one-line field, so
one can share a stop with other controls (the seed); `TextEdit.Node` reads "Insert Seed, edit, 123" (name = the
placeholder, dots dropped; value not live: the echo says each step). Fields: the Compendium search
(`GlossaryController.InputField`; the game shows its bar only in mouse mode, `HandleSearchBarVisibility`
at start and on a device change, so the mod shows `_searchBar` for the edit; `onEndEdit` runs
`OnSearch`, so the search applies when the edit ends; `OnSearch` REBUILDS every item (new objects,
new ids), so: an edit with the text unchanged keeps that one listener off for the whole edit
(`quietWhenUnchanged`, `TextEdit.HoldQuiet`: the field ends its own edit when it loses the game's selection, a
hover elsewhere, before the mod sees focus go; back on the moment the text changes, and at the end; `SetPersistentListenerState`, restored to RuntimeOnly: 2020 has no
getter), else Tab past the field would land on an item the rebuild destroys and focus would fall
back to the search, which edits again; a changed search left by Tab takes the Tab's move
(`TextEdit.Ended` with `EditExit`, `TextEdit.MoveTaken`), waits for `isShowingCoroutine` up then
down (5 s at most, the search not edited meanwhile) and focuses the items stop, or re-reads the
search when nothing was found), the difficulty seed (`SeedInput`, above the difficulties in their stop, the landing still a
difficulty by `SetStart`; offered
once `MiscUnlockName.Challenges` is unlocked; whole numbers, 12 digits; Go! reads it).
`OptionInputField` rows are not in the Options scene; the F2 bug report form is not covered.
Test with real keys: a scratch script bringing the game forward (`AttachThreadInput` +
`SetForegroundWindow`) and `keybd_event` (End/Home need the extended-key flag, else they read as
the numpad's); the driver's `/input` goes to the mod, not the field.

## Dev driver (Debug only; `SVO_NO_DEV=1` disables, `SVO_DEV_PORT` sets the port, default 8772)
Guildrun's server unchanged: `/input`, `/nav`, `/gui`, `/speech`, `/log`, `/eval` (Roslyn, every
interop proxy referenced), `/wait`, `/typeinfo`, `/screenshot`, `/reload`, `/module`, `/health`.
`SVO_NO_SPEECH=1` skips Prism. Launch update check (`Module/UpdateChecker`, `Core/UpdateCheck`, from
guildrun): GitHub `releases/latest` on a pool thread, "update X available" only when strictly newer;
404 (no release yet), up to date, no tag, offline: silent, a log line. `SVO_UPDATE_URL` replaces the
URL (set it in the game with `Environment.SetEnvironmentVariable` through `/eval`, then `reload`; e.g.
guildrun-access's latest, or a local `http.server` serving `{"tag_name":"v0.0.1"}`). Roslyn 4.8 wants System.Collections.Immutable and
System.Reflection.Metadata 7.0, the game's runtime has 6.0: `CSharpEvaluator.InstallDependencyResolver`
(Default context `Resolving`) binds them to 6.0 before the LoadFrom handler tries the plugin folder's
7.0 copies and throws on the first eval or two. **Never press a control that starts a tutorial or a run, or changes
the save, through the driver without asking**: verify screens by reading them.

## Rebinding the mod's keys
`Core/Input/KeyBindings` (tested): the registered bindings are the defaults (`Snapshot` after
`RegisterInput`), the store's `key_bindings` string (`mod.menu=keyboard:M|ctrl,shift keyboard:F10;...`,
one entry per action differing from its defaults, unknown actions kept) is `Load`ed over them, every
change saves at once. Rebindable = every default binding's type has a deserializer (so the `game`
`GameKeyBinding`s, the game's Options' keys, are not). Conflicts within a category or with Global are
refused (`InputBinding.Combos`, a game key's current keys included); other categories only overlap
and are named. `Protected` (ui.up/down/activate/back, mod.menu) keep one key. UI: the mod menu's All
keys = the categories (`CategoryKeysScreen`), an action's keys (`ActionKeysScreen`), the capture
(`BindKeyScreen`: `CapturesRawInput`, every key up, `OsKeys.FirstPressed` of a non-modifier with the
held modifiers, bare Escape cancels, every key up again before closing). Test with real keys (the
scratch sender: pass the sequence as ONE comma-separated string; `powershell -File` does not split
arrays).

## Keys
F1 key help, arrows, Tab/Shift+Tab, Enter, Backspace (secondary), Escape (on a screen with no
Back of its own, `Navigation.BackFallback` = `UI/Pause`: the scene's `PauseButtonView` click, as the
game's Menu key; the fight's Escape cancels a selection first), Home/End, Alt+Up/Down
sections, type-ahead letters, Ctrl+arrows buffers, Ctrl+Shift+A focus mode, Ctrl+Shift+M mod menu.
