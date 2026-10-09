using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using TMPro;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// The pause menu (<c>PauseMenuSceneAdditive</c>, <see cref="PauseMenuController"/>), opened by
    /// Escape on a run's screens (<see cref="Pause"/>). Named "Paused" (the game's word). Stops: its
    /// buttons (Resume, Options, Compendium, Save &amp; Exit, Skip Tutorial or Abandon Run, Show Guide),
    /// each the game's click; the run (difficulty, Deck, Modifiers); the act timeline. Escape resumes.
    /// </summary>
    public sealed class PauseScreen : Screen
    {
        public override string Key => "pause";
        public override int Layer => 30;
        public override bool Exclusive => true;

        private static PauseMenuController Controller => GameScenes.Controller<PauseMenuController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override string ScreenName => GameLoc.Get("Paused_Misc");

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Resume());
        }

        private static void Resume()
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            try { c.OnResumePressed(); }
            catch (Exception e) { CoreLog.Warning("Pause: resuming failed: " + e.Message); }
        }

        private static readonly string[] Buttons = { "ResumeButton", "OptionsButton", "GlossaryButton", "ExitButton", "AbandonRun", "Guide" };

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            b.BeginStop("pause:menu");
            b.PushContext(null);
            foreach (var name in Buttons)
            {
                var item = GameScenes.Item(c, name);
                if (ViewNodes.IsShown(item)) b.AddItem(ControlId.Structural("pause:" + name), ViewNodes.Button(item));
            }
            b.PopContext();

            b.BeginStop("pause:run");
            b.PushContext(null, positions: false);
            var difficulty = c.DifficultyText;
            if (difficulty != null && difficulty.gameObject.activeInHierarchy)
            {
                Func<string> text = () => Labelled(GameLoc.Get("DifficultyHeader_Misc"), GameText.Of(difficulty));
                b.AddItem(ControlId.Structural("pause:difficulty"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(text) },
                    SearchText = text,
                });
            }
            var deck = GameScenes.Item(c, "Deck");
            if (ViewNodes.IsShown(deck))
                b.AddItem(ControlId.Structural("pause:deck"), ViewNodes.Button(deck, () => GameText.Of(c.DeckTooltipTitleText) ?? ViewNodes.Label(deck)));
            var mods = c.ModifiersButtonObject;
            if (ViewNodes.IsShown(mods))
            {
                var (title, lines) = TooltipTexts(mods);
                b.AddItem(ControlId.Structural("pause:modifiers"), ViewNodes.Button(mods, () => title ?? GameLoc.Get("Modifiers_Misc"),
                    enabled: () => !mods._isLocked));
            }
            b.PopContext();

            CampaignNodes.Map(b, c.MapPanel, "pause");
        }

        private static string Labelled(string label, string value) =>
            value == null ? null : label == null ? value : Strings.FightLabelled(label, value);

        // The button's own tooltip (hidden until hovered): its title and description lines.
        private static (string Title, List<string> Lines) TooltipTexts(OutlinedButtonView button)
        {
            var lines = new List<string>();
            string title = null;
            if (button.ToolTip == null) return (null, lines);
            foreach (var tmp in button.ToolTip.GetComponentsInChildren<TMP_Text>(true))
            {
                if (tmp.name == "Text") continue; // the one-letter key prompt
                string text = GameText.Of(tmp);
                if (text == null) continue;
                if (title == null) title = text;
                else lines.Add(text);
            }
            return (title, lines);
        }
    }
}
