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
    /// One stop: the three difficulties as radio buttons ("First Contact, selected", its description
    /// lines after; a locked one LOCKED and disabled, with "Unlocked by beating previous difficulty");
    /// Challenges when the game shows it; Go! (the run starts); Back. Enter on a difficulty only
    /// selects it (<c>OnLevel1Pressed(allowStart: false)</c>): the game's own click on the selected
    /// one starts the run, which a second Enter must not do by surprise; Go! starts it. The seed field
    /// is the mouse's (hidden here) and not read. Escape is <see cref="DifficultySelectionController.OnBackPressed"/>;
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
            b.PushContext(null);
            var panels = new[] { c.Difficulty1Panel, c.Difficulty2Panel, c.Difficulty3Panel };
            for (int i = 0; i < panels.Length; i++)
                if (ViewNodes.IsShown(panels[i])) b.AddItem(ControlId.Structural("difficulty:" + i), Difficulty(c, panels[i], i));
            b.PopContext();
            b.PushContext(null, positions: false);
            if (ViewNodes.IsShown(c.CustomButton)) b.AddItem(ControlId.Structural("difficulty:challenges"), ViewNodes.Button(c.CustomButton));
            if (c.GoButtonShown && ViewNodes.IsShown(c.GoButton)) b.AddItem(ControlId.Structural("difficulty:go"), ViewNodes.Button(c.GoButton));
            if (ViewNodes.IsShown(c.BackButton))
                b.AddItem(ControlId.Structural("difficulty:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), () =>
                {
                    try { if (!c.IsTransitioning) c.OnBackPressed(); }
                    catch (Exception e) { CoreLog.Warning("Difficulty: going back failed: " + e.Message); }
                }));
            b.PopContext();
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
