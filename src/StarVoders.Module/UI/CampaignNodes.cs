using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using TMPro;
using UnityEngine;

namespace StarVoders.Module.UI
{
    /// <summary>
    /// The run's status, shared by every scene a <see cref="CampaignSceneController"/> runs (the
    /// encounter choice, the shops, the rewards): the portrait's Doom, Stars and Deck, and the act
    /// timeline. Every word is the game's own: the captions it draws, else its tooltips' titles.
    /// </summary>
    internal static class CampaignNodes
    {
        /// <summary>A game tooltip's title and its description lines (the texts of its object).</summary>
        public static (string Title, List<string> Lines) Tooltip(StarVaders.TooltipTrigger trigger)
        {
            var lines = new List<string>();
            string title = null;
            var obj = trigger != null ? trigger.toolTipObject : null;
            if (obj == null) return (null, lines);
            foreach (var tmp in obj.GetComponentsInChildren<TMP_Text>(true))
            {
                string text = GameText.Of(tmp);
                if (text == null) continue;
                if (title == null) title = text;
                else lines.Add(text);
            }
            return (title, lines);
        }

        /// <summary>"title: description", or the title alone, or null.</summary>
        public static string TooltipLine(StarVaders.TooltipTrigger trigger)
        {
            var (title, lines) = Tooltip(trigger);
            if (title == null) return null;
            return lines.Count == 0 ? title : title + ": " + string.Join(" ", lines);
        }

        private static T Under<T>(Component root, string path) where T : Component
        {
            var t = root != null ? root.transform.Find(path) : null;
            return t != null ? t.GetComponent<T>() : null;
        }

        /// <summary>The Reroll button (its caption, "disabled" without tokens) with the Chrono Tokens
        /// left, read live ("3 of 3"), and the <paramref name="description"/> its hover panel shows,
        /// if any (the workshop's Restock).</summary>
        public static void Reroll(GraphBuilder b, RerollButton reroll, string id, Func<string> description = null)
        {
            if (!ViewNodes.IsShown(reroll)) return;
            var vt = ViewNodes.Button(reroll, () => GameText.Of(reroll.transform.Find("RerollText")?.GetComponent<TMP_Text>()),
                enabled: () => reroll.ButtonEnabled);
            Func<string> left = () => RerollsLeft(reroll);
            vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
            {
                new NodeAnnouncement(left, live: true, kind: AnnouncementKinds.Value),
            };
            if (description != null) vt.Announcements = new List<NodeAnnouncement>(vt.Announcements) { GameNodes.TooltipPart(description) };
            b.AddItem(ControlId.Structural(id), vt);
        }

        // "3 of 3": the tokens left over the most, as the button draws them ("3", "/3").
        private static string RerollsLeft(RerollButton reroll)
        {
            if (reroll.NumberObject == null || !reroll.NumberObject.activeInHierarchy) return null;
            string left = GameText.Of(reroll.NumberText);
            string max = GameText.Of(reroll.MaxNumberText)?.TrimStart('/').Trim();
            if (left == null) return null;
            return int.TryParse(left, out int l) && int.TryParse(max, out int m) ? Strings.Position(l, m) : left;
        }

        /// <summary>Doom, Stars and Deck, one stop; with <paramref name="row"/>, a row of the current
        /// stop instead.</summary>
        public static void Status(GraphBuilder b, CampaignSceneController c, string key, bool row = false)
        {
            var portrait = c.CampaignPortraitView;
            if (portrait == null || !portrait.gameObject.activeInHierarchy) return;
            // Short: no positions, the descriptions in the ui buffer and on Backspace.
            if (!row) b.BeginStop(key + ":status");
            b.PushContext(null, positions: false);
            if (row) b.StartRow();

            var doomTip = DoomTip(portrait);
            b.AddItem(ControlId.Structural(key + ":doom"), Stat(() => DoomLabel(portrait), () => GameText.Of(portrait.DoomText), doomTip));

            var stars = StarsTip(portrait);
            b.AddItem(ControlId.Structural(key + ":stars"), Stat(
                () => Tooltip(stars).Title, () => Number(portrait.StarbucksText), stars));

            var deck = c.DeckView;
            if (ViewNodes.IsShown(deck))
                b.AddItem(ControlId.Structural(key + ":deck"),
                    ViewNodes.Button(deck, () => GameText.Of(portrait.DeckTooltipTitleText) ?? GameText.Of(portrait.DeckNumberText)));
            if (row) b.EndRow();
            b.PopContext();
        }

        /// <summary>"Doom 2/5" and "Stars 7500" as the portrait shows them, for the player buffer
        /// between fights: empty while no run scene draws the portrait.</summary>
        public static List<string> StatusLines()
        {
            var lines = new List<string>();
            var portrait = GameScenes.Controller<CampaignSceneController>()?.CampaignPortraitView;
            if (portrait == null || !portrait.gameObject.activeInHierarchy) return lines;
            void Add(string label, string value)
            {
                if (value == null) return;
                lines.Add(label != null ? label + " " + value : value);
            }
            Add(DoomLabel(portrait), GameText.Of(portrait.DoomText));
            Add(Tooltip(StarsTip(portrait)).Title, Number(portrait.StarbucksText));
            return lines;
        }

        private static StarVaders.TooltipTrigger DoomTip(CampaignPortraitView portrait) =>
            Under<StarVaders.TooltipTrigger>(portrait, "CanvasGroup/Portrait/DoomTooltipTrigger");

        private static StarVaders.TooltipTrigger StarsTip(CampaignPortraitView portrait) =>
            Under<StarVaders.TooltipTrigger>(portrait, "CanvasGroup/Portrait/Stardust");

        private static string DoomLabel(CampaignPortraitView portrait) =>
            GameText.Of(Under<TMP_Text>(portrait, "CanvasGroup/Portrait/TitleText")) ?? Tooltip(DoomTip(portrait)).Title;

        // A read-only figure: "label, value"; its tooltip's description in the ui buffer and on Backspace.
        private static NodeVtable Stat(Func<string> label, Func<string> value, StarVaders.TooltipTrigger tip)
        {
            Func<string> description = () =>
            {
                var lines = Tooltip(tip).Lines;
                return lines.Count > 0 ? string.Join(" ", lines) : null;
            };
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value),
                },
                SearchText = label,
                Details = () => GameNodes.Lines(description()),
                OnSecondary = () =>
                {
                    string text = description();
                    Core.Speech.Say(string.IsNullOrEmpty(text) ? Strings.NoDetails : text, interrupt: true);
                },
            };
        }

        // The Stars counter draws leading zeros dimmed ("<alpha=#60>00</color>7500"): the number.
        private static string Number(TMP_Text tmp)
        {
            string text = GameText.Of(tmp);
            if (text == null) return null;
            string trimmed = text.TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        /// <summary>The act timeline, one stop: its nodes in order, each "kind[, cleared | current]",
        /// the boss node with the boss preview's line. The kinds are the map tooltip's own legend
        /// (combat, non-combat special, boss, in that order, then its summary line). A cleared node
        /// draws its cross; the current one is the first not crossed out. With <paramref name="row"/>,
        /// a row of the current stop instead.</summary>
        public static void Map(GraphBuilder b, AViewItem panel, string key, bool row = false)
        {
            if (!ViewNodes.IsShown(panel)) return;
            var map = panel.GetComponentInChildren<MapView>();
            if (map == null || map.nodes == null) return;
            var tip = Under<StarVaders.TooltipTrigger>(panel, "MapTooltipTrigger");
            var act = Under<TMP_Text>(panel, "CurrentAct");

            bool any = false;
            foreach (var n in map.nodes) any |= n != null;
            if (!any) return; // an empty row cannot be built
            if (!row) b.BeginStop(key + ":map");
            var (title, legend) = Tooltip(tip);
            string actText = GameText.Of(act);
            b.PushContext(actText != null && title != null ? title + ", " + actText : title ?? actText);
            int current = -1, currentItem = 0, items = 0;
            for (int i = 0; i < map.nodes.Count && current < 0; i++)
            {
                var node = map.nodes[i];
                if (node == null) continue;
                if (!Cleared(node)) { current = i; currentItem = items; }
                items++;
            }
            // Moving into the row lands on the current stop.
            if (row) b.StartRow(entry: () => currentItem);
            for (int i = 0; i < map.nodes.Count; i++)
            {
                var node = map.nodes[i];
                if (node == null) continue;
                bool cleared = Cleared(node);
                int kind = Kind(node);
                bool isCurrent = i == current;
                string word = kind >= 0 && kind < legend.Count ? legend[kind] : null;
                var boss = kind == 2 ? map.BossPreview : null;
                b.AddItem(ControlId.Structural(key + ":map" + i), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement>
                    {
                        GameNodes.LabelPart(() => word ?? node.gameObject.name),
                        new NodeAnnouncement(() => cleared ? Strings.MapCleared : isCurrent ? Strings.MapCurrent : null,
                            kind: AnnouncementKinds.Value),
                        GameNodes.TooltipPart(() => boss != null ? GameText.Of(boss.BossTitle) : null),
                    },
                    SearchText = () => word,
                });
            }
            if (row) b.EndRow();
            b.PopContext();
        }

        private static bool Cleared(MapNode node) => node.XPip != null && node.XPip.enabled && node.XPip.gameObject.activeSelf;

        // 0 combat, 1 non-combat special, 2 boss: which pip the node draws.
        private static int Kind(MapNode node)
        {
            var t = node.transform;
            if (Shown(t, "BossPip")) return 2;
            if (Shown(t, "StarPip")) return 1;
            if (Shown(t, "Pip")) return 0;
            return -1;
        }

        private static bool Shown(Transform t, string child)
        {
            var c = t.Find(child);
            return c != null && c.gameObject.activeSelf;
        }
    }
}
