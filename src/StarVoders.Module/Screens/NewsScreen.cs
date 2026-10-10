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
    /// News (<c>NewsSceneAdditive</c>, <see cref="NewsSceneController"/>, over the main menu): the latest
    /// update's summary, one stop read top to bottom as drawn: its title, each shown paragraph line by line (the
    /// update list's section titles and bullets each a line), the signature, the two link buttons ("See
    /// ALL News", the Dicevaders page: the game's clicks open them in the browser), the footnote, Back.
    /// Escape is <see cref="NewsSceneController.OnExitPressed"/>, which fades out unguarded: once left,
    /// the screen stands down for that controller so a second press cannot start a second fade.
    /// </summary>
    public sealed class NewsScreen : Screen
    {
        public override string Key => "news";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override string ScreenName => GameLoc.Get("News_Misc");

        private static NewsSceneController Controller => GameScenes.Controller<NewsSceneController>();
        private static IntPtr _left;

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && c.Pointer != _left && c.MainPanel != null && c.MainPanel.gameObject.activeInHierarchy;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Leave());
        }

        private static void Leave()
        {
            var c = Controller;
            if (c == null || c.Pointer == _left) return;
            _left = c.Pointer;
            try { c.OnExitPressed(); }
            catch (Exception e) { CoreLog.Warning("News: leaving failed: " + e.Message); }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            var panel = c.MainPanel.Find("BackgroundPanel") ?? c.MainPanel;
            b.BeginStop("news");
            b.PushContext(null, positions: false);
            int i = 0;
            var area = panel.Find("NewsArea");
            if (area != null)
                foreach (var tmp in TopToBottom(area))
                    foreach (var line in Lines(tmp))
                    {
                        string text = line;
                        b.AddItem(ControlId.Structural("news:line" + i++), GameNodes.Text(() => text));
                    }
            foreach (var name in new[] { "SteamButton", "Dicevadersbutton" })
            {
                var button = panel.Find(name)?.GetComponent<AViewItem>();
                if (ViewNodes.IsShown(button)) b.AddItem(ControlId.Structural("news:" + name), ViewNodes.Button(button));
            }
            var note = panel.Find("BottomText")?.GetComponent<TMP_Text>();
            if (note != null && note.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("news:note"), GameNodes.Text(() => GameText.Of(note)));
            var back = panel.Find("BackButtons/BackButton")?.GetComponent<AViewItem>();
            if (ViewNodes.IsShown(back))
                b.AddItem(ControlId.Structural("news:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Leave));
            b.PopContext();
        }

        // The area's texts as drawn, top first: the hierarchy has the signature second, drawn last.
        private static List<TMP_Text> TopToBottom(Transform area)
        {
            var texts = new List<TMP_Text>(area.GetComponentsInChildren<TMP_Text>(false));
            texts.Sort((a, b) => b.transform.position.y.CompareTo(a.transform.position.y));
            return texts;
        }

        // A text's lines, each cleaned of its markup, the blank ones dropped: the update list is one
        // text with a line per section title and bullet.
        private static IEnumerable<string> Lines(TMP_Text tmp)
        {
            if (tmp == null || string.IsNullOrEmpty(tmp.text)) yield break;
            foreach (var raw in tmp.text.Split('\n'))
            {
                string line = GameText.Clean(raw)?.Trim();
                if (!string.IsNullOrEmpty(line)) yield return line;
            }
        }
    }
}
