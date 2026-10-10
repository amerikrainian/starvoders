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
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The game's more-info popup (<c>MoreInfoSceneAdditive</c>, <see cref="MoreInfoSceneController"/>):
    /// what a card, artifact or invader's "more info" shows, over whatever opened it (a pilot's starter
    /// artifact's click, the shop's unlocks, the game's own more-info key). One stop: the item's panel
    /// (<c>_currentDisplayBoardPanel</c>: title, traits, description, each shown text), a line per keyword
    /// it uses ("Overheat: ..."), the cards and invaders it names (<c>MoreInfoObjs</c>; Enter opens theirs
    /// nested, as their click), Back while nested (<see cref="MoreInfoSceneController.PreviousObjects"/>),
    /// Close. Escape is Back while nested, else Close. Layer 38: over the deck viewer and the confirm
    /// prompt, under a tip.
    /// </summary>
    public sealed class MoreInfoScreen : Screen
    {
        public override string Key => "moreinfo";
        public override int Layer => 38;
        public override bool Exclusive => true;

        private static MoreInfoSceneController Controller => GameScenes.Controller<MoreInfoSceneController>();

        public override bool IsActive() => Controller != null;

        public override string ScreenName => Title(Controller);

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                if (Nested()) Back();
                else Close();
            });
        }

        private static bool Nested()
        {
            var previous = MoreInfoSceneController.PreviousObjects;
            return previous != null && previous.Count > 0;
        }

        private static void Back()
        {
            try { Controller?.BackButtonPressed(); }
            catch (Exception e) { CoreLog.Warning("More info: going back failed: " + e.Message); }
        }

        private static void Close()
        {
            try { Controller?.ExitScene(0f); }
            catch (Exception e) { CoreLog.Warning("More info: closing failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c._canvasGroup == null || c._canvasGroup.alpha < 0.05f) return;
            b.BeginStop("moreinfo");
            b.PushContext(null, positions: false);
            int i = 0;
            var panel = c._currentDisplayBoardPanel;
            if (panel != null)
                foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                    AddText(b, "moreinfo:panel" + i++, tmp);
            if (c._moreInfoLineParent != null)
                foreach (var tmp in c._moreInfoLineParent.GetComponentsInChildren<TMP_Text>(false))
                    AddText(b, "moreinfo:line" + i++, tmp);
            if (c.MoreInfoObjs != null)
                foreach (var obj in c.MoreInfoObjs)
                    if (ViewNodes.IsShown(obj))
                        b.AddItem(ControlId.Structural("moreinfo:obj" + obj.GetInstanceID()), ViewNodes.Button(obj, () => ObjectLabel(obj)));
            if (c._backButton != null && c._backButton.activeInHierarchy)
                b.AddItem(ControlId.Structural("moreinfo:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Back));
            if (ViewNodes.IsShown(c._closeButton))
                b.AddItem(ControlId.Structural("moreinfo:close"), GameNodes.Button(() => Strings.ModClose, Close));
            b.PopContext();
        }

        // A shown text as a line; key prompts (one letter) skipped.
        private static void AddText(GraphBuilder b, string id, TMP_Text tmp)
        {
            string text = GameText.Of(tmp);
            if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 2) return;
            string line = text.Replace("\n", " ").Trim();
            b.AddItem(ControlId.Structural(id), GameNodes.Text(() => line));
        }

        // A linked card or invader: its name as drawn (the first text of two letters or more).
        private static string ObjectLabel(AViewItem obj)
        {
            foreach (var tmp in obj.GetComponentsInChildren<TMP_Text>(false))
            {
                string t = GameText.Of(tmp);
                if (!string.IsNullOrWhiteSpace(t) && t.Trim().Length > 1) return t.Trim();
            }
            return ViewNodes.Label(obj);
        }

        // The item's title: the panel's TitleText.
        private static string Title(MoreInfoSceneController c)
        {
            var panel = c != null ? c._currentDisplayBoardPanel : null;
            if (panel == null) return null;
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp.name == "TitleText") return GameText.Of(tmp);
            return null;
        }
    }
}
