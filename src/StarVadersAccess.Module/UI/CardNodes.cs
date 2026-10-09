using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.Screens;
using FightState = StarVadersAccess.Module.Fight.Fight;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// A card or an artifact shown outside the hand (the card viewer, the rewards): "name, upgrade,
    /// cost, description, upgrade's description" with the keyword explanations in the ui buffer and on Backspace; focus hovers
    /// its view as the mouse does, Enter is its click.
    /// </summary>
    internal static class CardNodes
    {
        public static NodeVtable Card(CardModel card, AViewItem view)
        {
            Func<string> name = () => FightState.CardName(card);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    new NodeAnnouncement(() => FightState.CardUpgrade(card), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => EncounterScreen.Cost(card), kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => FightState.CardDescription(card)),
                    GameNodes.TooltipPart(() => FightState.CardUpgradeDescription(card)),
                },
                SearchText = name,
                OnFocus = () => ViewNodes.Hover(view),
                OnActivate = () => ViewNodes.Click(view),
                Details = () => FightState.CardInfoLines(card),
                OnSecondary = () =>
                {
                    var lines = FightState.CardInfoLines(card);
                    Speech.Say(lines.Count > 0 ? string.Join(" ", lines) : Strings.NoDetails, interrupt: true);
                },
            };
        }

        /// <summary>An artifact: "name, description"; Enter is its click only when
        /// <paramref name="clickable"/> (a reward; the viewer's artifacts do nothing).</summary>
        public static NodeVtable Artifact(ArtifactModel model, AViewItem view, bool clickable = false)
        {
            Func<string> name = () => model != null ? GameLoc.ArtifactName(model.ArtifactName) : null;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    GameNodes.TooltipPart(() => model != null ? GameLoc.ArtifactDescription(model.ArtifactName) : null),
                },
                SearchText = name,
                OnFocus = () => ViewNodes.Hover(view),
                OnActivate = clickable ? () => ViewNodes.Click(view) : (Action)null,
            };
        }
    }
}
