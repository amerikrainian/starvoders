using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The difficulty for a new run (<c>DifficultySelectionSceneAdditive</c>,
    /// <see cref="DifficultySelectionController"/>, over the pilot choice), titled "Select Difficulty".
    /// One stop: the three difficulties as a row of radio buttons ("First Contact, selected", its description
    /// lines after; a locked one LOCKED and disabled, with "Unlocked by beating previous difficulty");
    /// Challenges when the game shows it (not hovered either: that too deselects); Go! (the run starts; "disabled" while no difficulty is selected,
    /// and never hovered: the game's keyboard hover on it deselects the difficulty); Back. Enter on a difficulty only
    /// selects it (<c>OnLevel1Pressed(allowStart: false)</c>): the game's own click on the selected
    /// one starts the run, which a second Enter must not do by surprise; Go! starts it. The seed sits
    /// above the difficulties in the same stop (focus still lands on them), edited the moment focus
    /// lands on it (<see cref="TextEdit"/>; Up or Down ends the edit and moves on). Offered once Challenges are unlocked:
    /// the game shows it then, in mouse mode only (<c>HandleDeviceChanged</c>), so the mod shows it
    /// for the edit; Go! reads it. Escape is <see cref="DifficultySelectionController.OnBackPressed"/>;
    /// empty while it transitions.
    /// </summary>
    public sealed class DifficultyScreen : Screen
    {
        public override string Key => "difficulty";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override string ScreenName => GameLoc.Get("SelectDifficulty_Misc");

        private static DifficultySelectionController Controller => GameScenes.Controller<DifficultySelectionController>();

        public override bool IsActive() => Controller != null;

        // The selected difficulty's index (0-2), -1 when none.
        private static int SelectedIndex(DifficultySelectionController c)
        {
            var selected = c.SelectedChallengeData;
            if (selected == null) return -1;
            var challenges = new[] { c.Level1Challenge, c.Level2Challenge, c.Level3Challenge };
            for (int i = 0; i < challenges.Length; i++)
                if (challenges[i] != null && challenges[i].Pointer == selected.Pointer) return i;
            return -1;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller;
                if (c == null || c.IsTransitioning) return;
                try { c.OnBackPressed(); }
                catch (Exception e) { CoreLog.Warning("Difficulty: going back failed: " + e.Message); }
            });
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsTransitioning) return;
            b.BeginStop("difficulty");
            // The seed at the top, as drawn, in the same stop: edited on landing, Down leaves it.
            if (SeedAvailable(c))
            {
                b.PushContext(null, positions: false);
                b.AddItem(SeedId, TextEdit.Node(c.SeedInput, prepare: () => ShowSeed(c)));
                b.PopContext();
            }
            b.PushContext(null);
            // The difficulties side by side, as drawn: a row (Left/Right), coming up into it on the
            // selected one.
            var panels = new[] { c.Difficulty1Panel, c.Difficulty2Panel, c.Difficulty3Panel };
            var shown = new List<int>();
            for (int i = 0; i < panels.Length; i++) if (ViewNodes.IsShown(panels[i])) shown.Add(i);
            if (shown.Count > 0)
            {
                b.StartRow("difficulty:levels", () => Math.Max(0, shown.IndexOf(SelectedIndex(c))));
                foreach (int i in shown) b.AddItem(ControlId.Structural("difficulty:" + i), Difficulty(c, panels[i], i));
                b.EndRow();
                // The landing: the difficulties (the selected one wins), not the seed above them.
                b.SetStart(ControlId.Structural("difficulty:" + shown[0]));
            }
            b.PopContext();
            b.PushContext(null, positions: false);
            // Challenges and Go! are not hovered: the game's keyboard hover on either clears the
            // selected difficulty (and with it Go!).
            if (ViewNodes.IsShown(c.CustomButton))
            {
                var challenges = c.CustomButton;
                b.AddItem(ControlId.Structural("difficulty:challenges"), GameNodes.Button(() => ViewNodes.Label(challenges),
                    () => ViewNodes.Click(challenges), () => ViewNodes.IsShown(challenges)));
            }
            // Go! starts the run with the selected difficulty, refusing silently without one.
            if (c.GoButtonShown && ViewNodes.IsShown(c.GoButton))
            {
                var go = c.GoButton;
                b.AddItem(ControlId.Structural("difficulty:go"), GameNodes.Button(() => ViewNodes.Label(go),
                    () => ViewNodes.Click(go), () => ViewNodes.IsShown(go) && c.SelectedChallengeData != null));
            }
            if (ViewNodes.IsShown(c.BackButton))
                b.AddItem(ControlId.Structural("difficulty:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), () =>
                {
                    try { if (!c.IsTransitioning) c.OnBackPressed(); }
                    catch (Exception e) { CoreLog.Warning("Difficulty: going back failed: " + e.Message); }
                }));
            b.PopContext();
        }

        // ---- the seed ----

        private static readonly ControlId SeedId = ControlId.Structural("difficulty:seed");
        private readonly TextEdit.OnLanding _seed = new TextEdit.OnLanding();

        private static bool SeedFocused => Equals(Navigation.FocusedNode?.Id, SeedId);

        public override bool AllowsTypeahead => !SeedFocused;

        // The game offers the seed once Challenges are unlocked (its HandleDeviceChanged).
        private static bool SeedAvailable(DifficultySelectionController c)
        {
            try { return c.SeedInput != null && !UnlockManager.IsLocked(MiscUnlockName.Challenges); }
            catch (Exception e) { CoreLog.Warning("Difficulty: reading the seed's unlock failed: " + e.Message); return false; }
        }

        // Hidden outside mouse mode: shown for the edit.
        private static void ShowSeed(DifficultySelectionController c)
        {
            try { if (c?.SeedInput != null && !c.SeedInput.gameObject.activeSelf) c.SeedInput.gameObject.SetActive(true); }
            catch (Exception e) { CoreLog.Warning("Difficulty: showing the seed failed: " + e.Message); }
        }

        public override void OnPop() => TextEdit.Stop();

        public override void OnUpdate()
        {
            var c = Controller;
            _seed.Tick(SeedFocused, c?.SeedInput, () => ShowSeed(c));
        }


        private static NodeVtable Difficulty(DifficultySelectionController c, AViewItem panel, int index)
        {
            Func<bool> locked = () =>
            {
                try { return index == 1 ? c.IsDifficulty2Locked() : index == 2 && c.IsDifficulty3Locked(); }
                catch { return false; }
            };
            Func<string> title = () => Text(panel, "TitlePanel");
            Func<string> label = () => locked() ? GameLoc.Get("LockedTitle_Misc") + ", " + title() : title();
            Func<string> description = () =>
            {
                if (locked())
                {
                    var lockText = index == 1 ? c.Diffuculty2LockText : c.Diffuculty3LockText;
                    return Clean(lockText, " "); // one sentence drawn over two lines
                }
                return Text(panel, "DescriptionPanel");
            };
            var challenge = index == 0 ? c.Level1Challenge : index == 1 ? c.Level2Challenge : c.Level3Challenge;
            Func<bool> selected = () => c.SelectedChallengeData != null && challenge != null && c.SelectedChallengeData.Pointer == challenge.Pointer;
            return new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    GameNodes.SelectedPart(selected),
                    GameNodes.DisabledPart(() => !locked()),
                    GameNodes.TooltipPart(description),
                },
                SearchText = title,
                OnFocus = () => ViewNodes.Hover(panel),
                OnActivate = () =>
                {
                    if (locked() || c.IsTransitioning) return;
                    try
                    {
                        if (index == 0) c.OnLevel1Pressed(false);
                        else if (index == 1) c.OnLevel2Pressed(false);
                        else c.OnLevel3Pressed(false);
                    }
                    catch (Exception e) { CoreLog.Warning("Difficulty: selecting failed: " + e.Message); }
                },
            };
        }

        // The panel's text under the named part (its title, its description), lines joined.
        private static string Text(AViewItem panel, string part)
        {
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp.transform.parent != null && tmp.transform.parent.name.StartsWith(part, StringComparison.Ordinal))
                    return Clean(tmp);
            return null;
        }

        // The text's lines cleaned and joined: a description's bullets by commas (the dashes dropped),
        // a sentence wrapped over lines by a space.
        private static string Clean(TMP_Text tmp, string between = ", ")
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) return null;
            var lines = new List<string>();
            foreach (var raw in tmp.text.Split('\n'))
            {
                string line = GameText.Clean(raw)?.Trim().TrimStart('-').Trim();
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }
            return lines.Count > 0 ? string.Join(between, lines) : null;
        }
    }
}
