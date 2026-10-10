using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Fight;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using UnityEngine;
using FightState = StarVadersAccess.Module.Fight.Fight;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// The Bestiary (<c>InvaderGlossarySceneAdditive</c>, <see cref="InvaderGlossaryController"/>).
    /// Stops: the act tabs (Act 1-3, Chaos read LOCKED while disabled, Other; landing on one shows it,
    /// as its click: <c>OnPressTab</c>); the Advanced Invaders toggle and the boss level chooser
    /// (Left and Right step it, Enter is its click); the shown act's panels in order, each its
    /// invaders and items (one entry per kind: a Swarmer panel draws three) by the game's name and
    /// description, "Unknown" for one not met yet, then its lore: the revealed title and text, or
    /// the defeats still needed (<see cref="InvaderGlossaryPanel.RequirementText"/>: the hidden side
    /// is the game's alien script, never read); then the pages' buttons and Back. The act shown is
    /// the group whose Canvas is enabled (the tab switches it), which <c>_currentTab</c> indexes.
    /// Escape is <see cref="InvaderGlossaryController.ExitScene"/>.
    /// </summary>
    public sealed class BestiaryScreen : Screen
    {
        public override string Key => "bestiary";
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override object InitialFocusStop => TabsStop;
        public override string ScreenName => GameLoc.Get("Bestiary_Misc");

        private const string TabsStop = "bestiary:tabs";

        private static InvaderGlossaryController Controller => GameScenes.Controller<InvaderGlossaryController>();

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
                catch (Exception e) { CoreLog.Warning("Bestiary: leaving failed: " + e.Message); }
            });
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            b.BeginStop(TabsStop);
            b.PushContext(null);
            b.StartRow(TabsStop);
            if (c.ClassButtons != null)
                for (int i = 0; i < c.ClassButtons.Count; i++)
                {
                    var tab = c.ClassButtons[i];
                    if (ViewNodes.IsShown(tab)) b.AddItem(ControlId.Structural("bestiary:tab" + i), Tab(c, tab, i));
                }
            b.EndRow();
            b.PopContext();

            b.BeginStop("bestiary:options");
            b.PushContext(null, positions: false);
            if (ViewNodes.IsShown(c.AdvancedInvadersToggle)) b.AddItem(ControlId.Structural("bestiary:advanced"), Advanced(c.AdvancedInvadersToggle));
            if (ViewNodes.IsShown(c.BossLevelChooser)) b.AddItem(ControlId.Structural("bestiary:bosses"), Chooser(c.BossLevelChooser));
            b.PopContext();

            BuildPanels(b, c);
            GlossaryNodes.AddBottom(b, c, "bestiary");
        }

        // ---- the tabs and options ----

        private static NodeVtable Tab(InvaderGlossaryController c, GlossaryClassButton tab, int index)
        {
            Func<string> label = () => tab.IsDisabled() ? GameLoc.Get("LockedTitle_Misc") : ViewNodes.Label(tab);
            Action pick = () => { if (!tab.IsDisabled() && c._currentTab != index) ViewNodes.Click(tab); };
            return new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                IsSelected = () => c._currentTab == index,
                SearchText = label,
                OnActivate = pick,
                OnFocus = () => { ViewNodes.Hover(tab); pick(); },
            };
        }

        // "Advanced Invaders, toggle, on/off": the panels then show the advanced kinds.
        private static NodeVtable Advanced(ToggleButton toggle)
        {
            Func<string> label = () => GameLoc.Get("AdvancedInvaders_Misc");
            Func<string> state = () => toggle.IsSelected ? Strings.StateOn : Strings.StateOff;
            return new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(state, live: true, kind: AnnouncementKinds.Value),
                    GameNodes.DisabledPart(() => !toggle.IsDisabled),
                },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(toggle),
                OnActivate = () => { if (!toggle.IsDisabled) ViewNodes.Click(toggle); },
                StateText = state,
            };
        }

        // The boss level ("Simple Bosses"): its own text is its value; Left and Right are its arrows,
        // Enter its click.
        private static NodeVtable Chooser(SimpleChooser chooser)
        {
            Func<string> value = () => GameText.Of(chooser.CurrentOptionText);
            return new NodeVtable
            {
                ControlType = ControlTypes.ComboBox,
                Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Label) },
                SearchText = value,
                OnFocus = () => ViewNodes.Hover(chooser),
                OnActivate = () => chooser.OnClick(),
                OnAdjust = (sign, large) => { if (sign > 0) chooser.OnRightPressed(); else chooser.OnLeftPressed(); },
                StateText = value,
            };
        }

        // ---- the panels ----

        private static void BuildPanels(GraphBuilder b, InvaderGlossaryController c)
        {
            var groups = c.InvaderGroups;
            int tab = c._currentTab;
            if (groups == null || tab < 0 || tab >= groups.Count || groups[tab] == null) return;
            var group = groups[tab].transform;
            var shown = new HashSet<IntPtr>();
            foreach (var panel in c.InvaderPanels)
                if (panel != null && panel.gameObject.activeInHierarchy && panel.transform.IsChildOf(group)) shown.Add(panel.Pointer);

            b.BeginStop("bestiary:panels");
            b.PushContext(null);
            // The panels in the order drawn: the group's own hierarchy order.
            foreach (var panel in group.GetComponentsInChildren<InvaderGlossaryPanel>(false))
            {
                if (panel == null || !shown.Contains(panel.Pointer)) continue;
                AddPanel(b, panel);
            }
            b.PopContext();
        }

        private static void AddPanel(GraphBuilder b, InvaderGlossaryPanel panel)
        {
            string id = "bestiary:panel:" + panel.GetInstanceID();
            var seen = new HashSet<string>();
            if (panel.InvaderHolders != null)
                foreach (var holder in panel.InvaderHolders)
                {
                    if (!ViewNodes.IsShown(holder) || holder.EntityModel == null) continue;
                    var enemy = holder.EntityModel.TryCast<EnemyEntityModel>();
                    string kind = enemy != null ? enemy.EnemyID.EnemyName.ToString() : holder.gameObject.name;
                    if (!seen.Add(kind)) continue;
                    b.AddItem(ControlId.Structural(id + ":" + kind), Entity(holder, holder.EntityModel));
                }
            if (panel.ItemHolders != null)
                foreach (var holder in panel.ItemHolders)
                {
                    if (!ViewNodes.IsShown(holder) || holder.EntityModel == null) continue;
                    var item = holder.EntityModel.TryCast<ItemEntityModel>();
                    string kind = item != null ? item.ItemID.ItemName.ToString() : holder.gameObject.name;
                    if (!seen.Add("item:" + kind)) continue;
                    b.AddItem(ControlId.Structural(id + ":item:" + kind), Entity(holder, holder.EntityModel));
                }
            string lore = Lore(panel);
            if (lore != null) b.AddItem(ControlId.Structural(id + ":lore"), GameNodes.Text(() => Lore(panel)));
        }

        // An invader or item as its info panel shows it: name, description; "Unknown, You have not
        // encountered this invader yet." for one not met (the game's own Unknown entry).
        private static NodeVtable Entity(AViewItem holder, AEntityModel entity)
        {
            var enemy = entity.TryCast<EnemyEntityModel>();
            var item = entity.TryCast<ItemEntityModel>();
            string key = enemy != null ? enemy.EnemyID.EnemyName.ToString() : item != null ? item.ItemID.ItemName.ToString() : null;
            Func<string> name = () => key != null ? GameLoc.Get(key + "_EntityTitle") ?? key : null;
            Func<string> description = () => key != null ? GameLoc.EntityDescription(entity, key + "_EntityDesc", null) : null;
            Func<IEnumerable<string>> details = () =>
            {
                var lines = new List<string>(FightBoard.TraitLines(entity));
                foreach (var line in FightState.InfoLines(entity.MoreInfoWordNames))
                    if (!lines.Contains(line)) lines.Add(line);
                return lines;
            };
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(name), GameNodes.TooltipPart(description) },
                SearchText = name,
                OnFocus = () => ViewNodes.Hover(holder),
                Details = details,
                OnSecondary = () =>
                {
                    var lines = new List<string>(details());
                    Speech.Say(lines.Count > 0 ? string.Join(" ", lines) : Strings.NoDetails, interrupt: true);
                },
            };
        }

        // The panel's lore: "Migo: The versatile Migo..." once revealed; until then the defeats it
        // takes ("Lore hidden, 39/50 defeated"), never the alien script drawn in its place.
        private static string Lore(InvaderGlossaryPanel panel)
        {
            try
            {
                var revealed = panel.RevealedObject;
                if (revealed != null && revealed.activeInHierarchy)
                {
                    var parts = new List<string>();
                    foreach (var tmp in revealed.GetComponentsInChildren<TMPro.TMP_Text>(false))
                    {
                        string t = GameText.Of(tmp);
                        if (!string.IsNullOrWhiteSpace(t)) parts.Add(t);
                    }
                    return parts.Count > 0 ? string.Join(": ", parts) : null;
                }
                var hidden = panel.HiddenObject;
                if (hidden != null && hidden.activeInHierarchy && panel.RequirementText != null)
                {
                    string req = GameText.Of(panel.RequirementText)?.Trim('[', ']', ' ');
                    if (!string.IsNullOrEmpty(req)) return Strings.BestiaryLoreHidden(req);
                }
            }
            catch (Exception e) { CoreLog.Warning("Bestiary: reading a panel's lore failed: " + e.Message); }
            return null;
        }
    }
}
