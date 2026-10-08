using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.Readers;
using StarVadersAccess.Module.UI;
using TMPro;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// The run's encounter choice (<c>GlobeScene</c>, "Choose an Encounter"). Stops: the tutorial tip
    /// while one shows (Enter dismisses it, as its click does); the encounters, each "title,
    /// [selected,] invaders: names, reward[, bonus, curse...]", Enter clicking it as the mouse does
    /// (selecting it; on the selected one, proceeding into the fight),
    /// its tooltips and its invaders' descriptions in the ui buffer; Proceed and Reroll (with the
    /// Chrono Tokens left); the run status (Doom, Stars, Deck); the act timeline. Focus starts on
    /// the encounters.
    /// </summary>
    public sealed class GlobeScreen : Screen
    {
        public override string Key => "globe";
        public override object InitialFocusStop => "globe:encounters";

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

            var tip = TipManager.CurrentTip;
            if (tip != null && tip._isShowing && tip.gameObject.scene.handle == c.gameObject.scene.handle && PopupReader.TipText(tip) != null)
            {
                b.BeginStop("globe:tip");
                b.AddItem(ControlId.Structural("globe:tip"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => PopupReader.TipText(tip)) },
                    SearchText = () => PopupReader.TipText(tip),
                    // A finished tip box's click is Tip.Destroy; an unfinished one's only finishes the
                    // typing, which the player has no use for.
                    OnActivate = () => tip.Destroy(true),
                });
            }

            b.BeginStop("globe:encounters");
            b.PushContext(null, Strings.RoleList);
            var panels = c.CurrentRoomChoicePanels;
            if (panels != null)
                for (int i = 0; i < panels.Count; i++)
                {
                    var panel = panels[i];
                    if (!ViewNodes.IsShown(panel)) continue;
                    b.AddItem(ControlId.Structural("globe:room" + i), Encounter(panel));
                }
            b.PopContext();

            b.BeginStop("globe:actions");
            b.PushContext(null, Strings.RoleList);
            var proceed = c.ProceedButton;
            if (ViewNodes.IsShown(proceed))
                b.AddItem(ControlId.Structural("globe:proceed"), ViewNodes.Button(proceed, enabled: () => proceed.ButtonEnabled));
            var reroll = c.RerollButton;
            if (ViewNodes.IsShown(reroll))
            {
                var vt = ViewNodes.Button(reroll, () => GameText.Of(reroll.transform.Find("RerollText")?.GetComponent<TMP_Text>()),
                    enabled: () => reroll.ButtonEnabled);
                Func<string> left = () => RerollsLeft(reroll);
                vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
                {
                    new NodeAnnouncement(left, live: true, kind: AnnouncementKinds.Value),
                };
                b.AddItem(ControlId.Structural("globe:reroll"), vt);
            }
            b.PopContext();

            CampaignNodes.Status(b, c, "globe");
            CampaignNodes.Map(b, c, "globe");
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

        private static NodeVtable Encounter(RoomChoicePanel panel)
        {
            var encounter = panel.TryCast<EncounterRoomChoicePanel>();
            Func<string> title = () =>
                panel.SpecialTitle != null && panel.SpecialTitle.activeInHierarchy && GameText.Of(panel.SpecialTitleText) != null
                    ? GameText.Of(panel.SpecialTitleText) : GameText.Of(panel.TitleText);
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

        // What the panel draws besides its title: the invaders, the reward, a bonus or curse line, a
        // difficulty or curse modifier, the danger mark.
        private static string Summary(RoomChoicePanel panel, EncounterRoomChoicePanel encounter)
        {
            var parts = new List<string>();
            if (encounter != null)
            {
                var names = new List<string>();
                foreach (var name in Enemies(encounter)) names.Add(GameLoc.EnemyName(name) ?? name.ToString());
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

        // The ui buffer: each invader with its description, then every tooltip the panel shows.
        private static IEnumerable<string> Details(RoomChoicePanel panel, EncounterRoomChoicePanel encounter)
        {
            var lines = new List<string>();
            if (encounter != null)
                foreach (var name in Enemies(encounter))
                {
                    string n = GameLoc.EnemyName(name) ?? name.ToString();
                    string d = GameLoc.EnemyDescription(name);
                    string line = d != null ? n + ": " + d : n;
                    if (!lines.Contains(line)) lines.Add(line);
                }
            foreach (var view in new[] { panel.DifficultyModArtifact, panel.CurseModArtifact })
            {
                if (view == null || !ViewNodes.IsShown(view)) continue;
                var model = view.ArtifactModel;
                if (model == null) continue;
                string n = GameLoc.ArtifactName(model.ArtifactName);
                string d = GameLoc.ArtifactDescription(model.ArtifactName);
                if (n != null) lines.Add(d != null ? n + ": " + d : n);
            }
            foreach (var trigger in panel.GetComponentsInChildren<StarVaders.TooltipTrigger>(false))
            {
                string line = CampaignNodes.TooltipLine(trigger);
                if (line != null && !lines.Contains(line)) lines.Add(line);
            }
            return lines;
        }

        private static IEnumerable<EnemyName> Enemies(EncounterRoomChoicePanel encounter)
        {
            var holders = encounter.EnemyHolders;
            if (holders == null) yield break;
            foreach (var holder in holders)
            {
                if (holder == null || !holder.gameObject.activeInHierarchy) continue;
                var model = holder.EntityModel?.TryCast<EnemyEntityModel>();
                if (model != null) yield return model.EnemyID.EnemyName;
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
