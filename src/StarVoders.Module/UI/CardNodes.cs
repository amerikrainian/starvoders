using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.Screens;
using FightState = StarVoders.Module.Fight.Fight;

namespace StarVoders.Module.UI
{
    /// <summary>
    /// A card or an artifact shown outside the hand (the card viewer, the rewards): "name, upgrade,
    /// cost, traits, description, upgrade's description" with the keyword explanations in the ui buffer and on Backspace; focus hovers
    /// its view as the mouse does, Enter is its click.
    /// </summary>
    internal static class CardNodes
    {
        /// <summary>The views as the game draws them in rows: top row first, each left to right. A view
        /// starts a new row when it sits lower than the row's first by more than half its own drawn
        /// height (a rack, a grid; one row when they share a line).</summary>
        public static List<List<T>> DrawnRows<T>(IEnumerable<T> views) where T : UnityEngine.Component
        {
            var sorted = new List<T>(views);
            sorted.Sort((x, y) => y.transform.position.y.CompareTo(x.transform.position.y));
            var rows = new List<List<T>>();
            float rowY = 0f;
            foreach (var view in sorted)
            {
                float y = view.transform.position.y;
                if (rows.Count == 0 || rowY - y > HalfHeight(view))
                {
                    rows.Add(new List<T>());
                    rowY = y;
                }
                rows[rows.Count - 1].Add(view);
            }
            foreach (var row in rows) row.Sort((x, y) => x.transform.position.x.CompareTo(y.transform.position.x));
            return rows;
        }

        // Half the view's drawn height in world units; a small default for a view without a rect.
        private static float HalfHeight(UnityEngine.Component view)
        {
            var rect = view.transform.TryCast<UnityEngine.RectTransform>();
            float h = rect != null ? rect.rect.height * Math.Abs(rect.lossyScale.y) : 0f;
            return h > 0f ? h / 2f : 0.01f;
        }

        /// <summary>The views as horizontal strips (<see cref="DrawnRows"/>), one row each; rows share
        /// <paramref name="rowKey"/>, so Up and Down keep the column. Returns the first id, the landing,
        /// or null when there were none.</summary>
        public static ControlId Strips<T>(GraphBuilder b, IEnumerable<T> views, string rowKey,
            Func<T, ControlId> id, Func<T, NodeVtable> node) where T : UnityEngine.Component
        {
            ControlId first = null;
            foreach (var row in DrawnRows(views))
            {
                b.StartRow(rowKey);
                foreach (var view in row)
                {
                    var key = id(view);
                    b.AddItem(key, node(view));
                    first = first ?? key;
                }
                b.EndRow();
            }
            return first;
        }

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
                    new NodeAnnouncement(() => EncounterScreen.Cost(card, view), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => FightState.CardTraits(card), kind: AnnouncementKinds.Value),
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

        /// <summary>An artifact as its panel shows it: "name, trait, cooldown (a spell's), description",
        /// its more info in the ui buffer and on Backspace. Enter is its click only when
        /// <paramref name="clickable"/> (a reward; the viewer's artifacts do nothing). A fight's rack
        /// passes <paramref name="cooldown"/> false: the panel's figure is the spell's base cooldown.
        /// Focus hovers <paramref name="view"/> unless <paramref name="focus"/> says otherwise.</summary>
        public static NodeVtable Artifact(ArtifactModel model, AViewItem view, bool clickable = false, bool cooldown = true,
            Action focus = null)
        {
            Func<string> name = () => model != null ? GameLoc.ArtifactName(model.ArtifactName) : null;
            var parts = new List<NodeAnnouncement>
            {
                GameNodes.LabelPart(name),
                new NodeAnnouncement(() => FightState.ArtifactTrait(model), kind: AnnouncementKinds.Value),
            };
            if (cooldown) parts.Add(new NodeAnnouncement(() => FightState.ArtifactCooldown(model), kind: AnnouncementKinds.Value));
            parts.Add(GameNodes.TooltipPart(() => FightState.ArtifactDescription(model)));
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = parts,
                SearchText = name,
                OnFocus = focus ?? (view != null ? () => ViewNodes.Hover(view) : (Action)null),
                OnActivate = clickable ? () => ViewNodes.Click(view) : (Action)null,
                Details = () => FightState.ArtifactInfoLines(model),
                OnSecondary = () =>
                {
                    var lines = FightState.ArtifactInfoLines(model);
                    Speech.Say(lines.Count > 0 ? string.Join(" ", lines) : Strings.NoDetails, interrupt: true);
                },
            };
        }
    }
}
