using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The run's encounter choice (<c>GlobeScene</c>, "Choose an Encounter"). Stops: the tutorial tip
    /// while one shows (Enter dismisses it, as its click does); then one stop of rows, top to bottom:
    /// the act timeline, the run status (Doom, Stars, Deck), the encounters, Proceed, then Reroll under it (with
    /// the Chrono Tokens left). Each encounter reads "title, [selected,] invaders: names, reward[,
    /// bonus, curse...]", Enter clicking it as the mouse does (selecting it; on the selected one,
    /// proceeding into the fight), its tooltips and its invaders' descriptions in the ui buffer.
    /// Focus starts on the encounters (the selected one, else the first).
    /// </summary>
    public sealed class GlobeScreen : Screen
    {
        public override string Key => "globe";
        public override IReadOnlyList<InputCategory> InputCategories => RunKeys.Categories;

        private static GlobeController Controller => GameScenes.Controller<GlobeController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && !c.IsEndingScene;
        }

        // The scene's heading ("Choose an Encounter"), the Canvas root's TitleText.
        public override string ScreenName
        {
            get
            {
                var c = Controller;
                if (c == null) return null;
                foreach (var root in c.gameObject.scene.GetRootGameObjects())
                {
                    if (root.name != "Canvas") continue;
                    var title = root.transform.Find("TitleText");
                    if (title != null) return GameText.Of(title.GetComponent<TMP_Text>());
                }
                return null;
            }
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            var tip = Tips.Current;
            if (tip != null && tip.gameObject.scene.handle == c.gameObject.scene.handle && Tips.Text(tip) != null)
            {
                b.BeginStop("globe:tip");
                b.AddItem(ControlId.Structural("globe:tip"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => Tips.Text(tip)) },
                    SearchText = () => Tips.Text(tip),
                    OnActivate = () => Tips.Continue(tip),
                });
            }

            b.BeginStop("globe:run");
            CampaignNodes.Strips(b, c, "globe");

            var shown = new List<int>();
            var panels = c.CurrentRoomChoicePanels;
            if (panels != null)
                for (int i = 0; i < panels.Count; i++)
                    if (ViewNodes.IsShown(panels[i])) shown.Add(i);
            if (shown.Count > 0)
            {
                b.PushContext(null);
                b.StartRow();
                foreach (int i in shown)
                    b.AddItem(ControlId.Structural("globe:room" + i), Encounter(panels[i]));
                b.EndRow();
                b.PopContext();
                // Landing: the first encounter, or the selected one (the stop's selected member wins).
                b.SetStart(ControlId.Structural("globe:room" + shown[0]));
            }

            // Proceed, then Reroll under it: each its own row.
            b.PushContext(null, positions: false);
            var proceed = c.ProceedButton;
            if (ViewNodes.IsShown(proceed))
                b.AddItem(ControlId.Structural("globe:proceed"), ViewNodes.Button(proceed, enabled: () => proceed.ButtonEnabled));
            CampaignNodes.Reroll(b, c.RerollButton, "globe:reroll");
            b.PopContext();
        }

        private static NodeVtable Encounter(RoomChoicePanel panel)
        {
            var encounter = panel.TryCast<EncounterRoomChoicePanel>();
            Func<string> title = () => Title(panel);
            var vt = new NodeVtable
            {
                ControlType = ControlTypes.Button,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(title),
                    GameNodes.SelectedPart(() => panel._selected),
                    new NodeAnnouncement(() => Summary(panel, encounter), kind: AnnouncementKinds.Value),
                },
                SearchText = title,
                // The game's click: it selects the encounter, and on the one already selected it is
                // Proceed (the fight starts).
                OnActivate = () => ViewNodes.Click(panel),
                OnFocus = () => ViewNodes.Hover(panel),
                Details = () => Details(panel, encounter),
            };
            return vt;
        }

        // The drawn title. A Void Zone shows its special title over the plain one; the Holes! and
        // Cloudy panels are prefabs of their own that hide TitleText (it keeps the placeholder
        // "Abandoned =") and draw the name in "TitleText (1)", not wired as SpecialTitleText.
        private static string Title(RoomChoicePanel panel)
        {
            if (panel.SpecialTitle != null && panel.SpecialTitle.activeInHierarchy && GameText.Of(panel.SpecialTitleText) != null)
                return GameText.Of(panel.SpecialTitleText);
            if (panel.TitleText != null && panel.TitleText.gameObject.activeInHierarchy)
                return GameText.Of(panel.TitleText);
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
                if (tmp.name.StartsWith("TitleText") && GameText.Of(tmp) != null) return GameText.Of(tmp);
            return GameText.Of(panel.TitleText);
        }

        // The special tiles a room lays out at the start of its fight (its OnStartTaskList's
        // HolesEventSpecificTask, CloudyEventSpecificTask), which the panel names but never explains.
        private static TileStatus? RoomTiles(RoomChoicePanel panel)
        {
            var room = panel.RoomModel;
            if (room == null) return null;
            if (room.TryCast<AbandonedSmartRoomModel>() != null) return TileStatus.Hole;
            if (room.TryCast<CloudySmartRoomModel>() != null) return TileStatus.Cloud;
            return null;
        }

        // What the panel draws besides its title: the invaders, the reward, a bonus or curse line, a
        // difficulty or curse modifier, the danger mark.
        private static string Summary(RoomChoicePanel panel, EncounterRoomChoicePanel encounter)
        {
            var parts = new List<string>();
            if (encounter != null)
            {
                var names = new List<string>();
                foreach (var enemy in Enemies(encounter))
                {
                    var name = enemy.EnemyID.EnemyName;
                    names.Add(GameLoc.EnemyName(name) ?? name.ToString());
                }
                if (names.Count > 0)
                {
                    string label = CampaignNodes.Tooltip(Trigger(encounter, "Enemies")).Title;
                    parts.Add((label != null ? label + ": " : "") + string.Join(", ", names));
                }
                // The reward by its tooltip's title ("Card Reward"), plainer than the caption ("Card").
                string reward = CampaignNodes.Tooltip(Trigger(encounter, "Reward")).Title;
                if (reward != null) parts.Add(reward);
                else Add(parts, encounter.RewardText);
                Add(parts, encounter.SpecialRewardText);
                Add(parts, encounter.ArtifactSpecialRewardText);
            }
            if (panel.BonusObject != null && panel.BonusObject.activeInHierarchy) Add(parts, panel.BonusText);
            if (panel.CurseObject != null && panel.CurseObject.activeInHierarchy) Add(parts, panel.CurseText);
            if (panel.DifficultyModObject != null && panel.DifficultyModObject.activeInHierarchy)
            {
                string mod = Artifact(panel.DifficultyModArtifact);
                if (mod != null) parts.Add(mod);
            }
            if (panel.CurseModArtifact != null && ViewNodes.IsShown(panel.CurseModArtifact))
            {
                string mod = Artifact(panel.CurseModArtifact);
                if (mod != null) parts.Add(mod);
            }
            foreach (var trigger in panel.GetComponentsInChildren<StarVaders.TooltipTrigger>(false))
                if (trigger != null && trigger.gameObject.name.StartsWith("Image"))
                {
                    string danger = CampaignNodes.Tooltip(trigger).Title;
                    if (danger != null) parts.Add(danger);
                }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        // The ui buffer: each invader with its description and what its more info links, then every
        // tooltip the panel shows.
        private static IEnumerable<string> Details(RoomChoicePanel panel, EncounterRoomChoicePanel encounter)
        {
            var lines = new List<string>();
            var tiles = RoomTiles(panel);
            if (tiles.HasValue)
            {
                string d = Fight.FightBoard.StatusDescription(tiles.Value);
                string n = Fight.FightBoard.StatusName(tiles.Value);
                lines.Add(d != null ? Strings.FightLabelled(n, d) : n);
            }
            if (encounter != null)
                foreach (var enemy in Enemies(encounter))
                {
                    var name = enemy.EnemyID.EnemyName;
                    string n = GameLoc.EnemyName(name) ?? name.ToString();
                    // No encounter yet: the game strips a counter's "([#] left)" here.
                    string d = GameLoc.EntityDescription(enemy, name + "_EntityDesc", null);
                    string line = d != null ? n + ": " + d : n;
                    if (!lines.Contains(line)) lines.Add(line);
                    // What its more info links: keywords, and the cards it adds (the Pixie's Shimmer).
                    foreach (var info in Fight.Fight.EntityInfoLines(enemy))
                        if (!lines.Contains(info)) lines.Add(info);
                }
            foreach (var view in new[] { panel.DifficultyModArtifact, panel.CurseModArtifact })
            {
                if (view == null || !ViewNodes.IsShown(view)) continue;
                var model = view.ArtifactModel;
                if (model == null) continue;
                string n = GameLoc.ArtifactName(model.ArtifactName);
                string d = Fight.Fight.ArtifactDescription(model);
                if (n != null) lines.Add(d != null ? n + ": " + d : n);
            }
            foreach (var trigger in panel.GetComponentsInChildren<StarVaders.TooltipTrigger>(false))
            {
                string line = CampaignNodes.TooltipLine(trigger);
                if (line != null && !lines.Contains(line)) lines.Add(line);
            }
            return lines;
        }

        private static IEnumerable<EnemyEntityModel> Enemies(EncounterRoomChoicePanel encounter)
        {
            var holders = encounter.EnemyHolders;
            if (holders == null) yield break;
            foreach (var holder in holders)
            {
                if (holder == null || !holder.gameObject.activeInHierarchy) continue;
                var model = holder.EntityModel?.TryCast<EnemyEntityModel>();
                if (model != null) yield return model;
            }
        }

        private static StarVaders.TooltipTrigger Trigger(UnityEngine.Component root, string child)
        {
            var t = root.transform.Find(child);
            return t != null ? t.GetComponent<StarVaders.TooltipTrigger>() : null;
        }

        private static string Artifact(DeckArtifactView view)
        {
            var model = view != null ? view.ArtifactModel : null;
            return model != null ? GameLoc.ArtifactName(model.ArtifactName) : null;
        }

        private static void Add(List<string> parts, TMP_Text tmp)
        {
            if (tmp == null || !tmp.gameObject.activeInHierarchy) return;
            string text = GameText.Of(tmp);
            if (text != null) parts.Add(text);
        }
    }
}
