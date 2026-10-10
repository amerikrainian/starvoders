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
    /// The Album (<c>PilotGlossarySceneAdditive</c>, <see cref="PilotGlossaryController"/>): one
    /// pilot's page at a time. One stop: the pilot ("Roxy, 1/12": the name is drawn as an image, so
    /// the pilot's own name from <see cref="PilotGlossaryController.Pilots"/>; Left and Right turn the
    /// page as the Up and Down buttons do, both wrapping); a locked page's lock text ("LOCKED, Win
    /// with pilot to unlock": the placeholder page under it is never read); an open page's subtitle,
    /// quote, description, stats ("Current Win Streak: 3") and its Memories (Enter plays one, as
    /// its click; a locked one reads LOCKED); the previous and next buttons. Then the pages' buttons
    /// and Back. Escape is <see cref="PilotGlossaryController.ExitScene"/>.
    /// </summary>
    public sealed class AlbumScreen : Screen
    {
        public override string Key => "album";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override string ScreenName => GameLoc.Get("Album_Misc");

        private static PilotGlossaryController Controller => GameScenes.Controller<PilotGlossaryController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var c = Controller;
                if (c == null || c.IsEndingScene) return;
                try { c.ExitScene(); }
                catch (Exception e) { CoreLog.Warning("Album: leaving failed: " + e.Message); }
            });
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;
            b.BeginStop("album:page");
            b.PushContext(null, positions: false);
            b.AddItem(ControlId.Structural("album:pilot"), Pilot(c));
            int i = 0;
            foreach (var line in PageLines(c))
            {
                string text = line;
                b.AddItem(ControlId.Structural("album:line" + i++), GameNodes.Text(() => text));
            }
            if (!Locked(c) && c.MemoryButtons != null)
            {
                b.PushContext(GameLoc.Get("Memories_Misc"));
                b.StartRow("album:memories");
                for (int m = 0; m < c.MemoryButtons.Count; m++)
                {
                    var button = c.MemoryButtons[m];
                    if (ViewNodes.IsShown(button)) b.AddItem(ControlId.Structural("album:memory" + m), Memory(c, button, m));
                }
                b.EndRow();
                b.PopContext();
            }
            var up = GameScenes.Item(c, "UpButton");
            if (ViewNodes.IsShown(up)) b.AddItem(ControlId.Structural("album:prev"), ViewNodes.Button(up, () => Strings.AlbumPrevious));
            var down = GameScenes.Item(c, "DownButton");
            if (ViewNodes.IsShown(down)) b.AddItem(ControlId.Structural("album:next"), ViewNodes.Button(down, () => Strings.AlbumNext));
            b.PopContext();
            GlossaryNodes.AddBottom(b, c, "album");
        }

        private static bool Locked(PilotGlossaryController c) => c.LockedObject != null && c.LockedObject.activeInHierarchy;

        // "Roxy, 1/12"; Left and Right turn the page.
        private static NodeVtable Pilot(PilotGlossaryController c)
        {
            Func<string> label = () =>
            {
                string name = null;
                try
                {
                    var pilots = c.Pilots;
                    int i = c._currentPilotIndex;
                    if (pilots != null && i >= 0 && i < pilots.Count) name = pilots[i].ToString();
                }
                catch (Exception e) { CoreLog.Warning("Album: reading the pilot failed: " + e.Message); }
                string number = GameText.Of(c.PilotNumber);
                return name != null && number != null ? name + ", " + number : name ?? number;
            };
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(label, live: true, kind: AnnouncementKinds.Label) },
                SearchText = label,
                OnAdjust = (sign, large) =>
                {
                    try { if (sign > 0) c.OnDownButtonPressed(); else c.OnUpButtonPressed(); }
                    catch (Exception e) { CoreLog.Warning("Album: turning the page failed: " + e.Message); }
                },
            };
        }

        // The page as shown: locked, its lock texts (lone question marks dropped); open, the
        // subtitle, quote, description, then the stats, each label with its answer.
        private static List<string> PageLines(PilotGlossaryController c)
        {
            var lines = new List<string>();
            if (Locked(c))
            {
                foreach (var tmp in c.LockedObject.GetComponentsInChildren<TMP_Text>(false))
                {
                    string t = GameText.Of(tmp);
                    if (!string.IsNullOrWhiteSpace(t) && t.Trim('?', ' ').Length > 0) lines.Add(t);
                }
                return lines;
            }
            Add(lines, GameText.Of(c.PilotSubtitle));
            Add(lines, GameText.Of(c.PilotQuote));
            Add(lines, GameText.Of(c.PilotDescription));
            Add(lines, Answer(c.DifficultyAnswer));
            Add(lines, Answer(c.WinstreakAnswer));
            Add(lines, Pair(c.CustomQuestion, c.CustomAnswer));
            return lines;
        }

        // A stat's answer with its question, the text it sits under ("Current Win Streak: 3").
        private static string Answer(TMP_Text answer)
        {
            var parent = answer != null && answer.transform.parent != null ? answer.transform.parent.GetComponent<TMP_Text>() : null;
            return Pair(parent, answer);
        }

        private static string Pair(TMP_Text question, TMP_Text answer)
        {
            string q = question != null && question.gameObject.activeInHierarchy ? GameText.Of(question) : null;
            string a = answer != null && answer.gameObject.activeInHierarchy ? GameText.Of(answer) : null;
            if (a == null) return q;
            return q != null ? q.TrimEnd() + " " + a : a;
        }

        private static void Add(List<string> lines, string text)
        {
            if (!string.IsNullOrWhiteSpace(text)) lines.Add(text);
        }

        // A memory: its number, LOCKED and dead while its lock shows; Enter plays it.
        private static NodeVtable Memory(PilotGlossaryController c, PressedButtonView button, int index)
        {
            Func<bool> locked = () =>
            {
                var locks = c.MemoryButtonLocks;
                return locks != null && index < locks.Count && locks[index] != null && locks[index].activeInHierarchy;
            };
            Func<string> label = () => locked() ? GameLoc.Get("LockedTitle_Misc") : ViewNodes.Label(button);
            return ViewNodes.Button(button, label, enabled: () => !locked());
        }
    }
}
