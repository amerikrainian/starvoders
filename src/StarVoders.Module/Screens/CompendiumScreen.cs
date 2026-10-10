using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using UnityEngine;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The Compendium (<c>GlossarySceneAdditive</c>, <see cref="GlossaryController"/>, over the main
    /// menu). Stops: the class tabs (Gunner, Neutral, the locked ones read "LOCKED"; landing on one
    /// shows it, as its click); the count panel ("21/115, 18% found", Enter switching to the victory
    /// count and back, as the panel's arrows) and the Filters button; while the filter panel is open,
    /// its buttons under their section titles, each on or off; the items in the order shown: the
    /// class's artifacts and cards, then each pilot's group under its title ("Roxy Unique"); then
    /// Bestiary, Album, Trophies and Back. An item reads as <see cref="CardNodes"/> does, or the game's
    /// "LOCKED" or "UNDISCOVERED" with its explanation when it is one (its name is hidden, as the
    /// game hides it); in the victory view, "Victory" on the ones won with. An item's click is never
    /// bound: <see cref="DeckCardView.OnClick"/> is the shop's card removal. The search bar is the
    /// mouse's only (<see cref="GlossaryController.HandleSearchBarVisibility"/>) and not read. Escape
    /// closes the filter panel, else leaves (<see cref="GlossaryController.ExitScene"/>).
    /// </summary>
    public sealed class CompendiumScreen : Screen
    {
        public override string Key => "compendium";
        public override int Layer => 35;
        public override bool Exclusive => true;
        public override object InitialFocusStop => TabsStop;
        public override string ScreenName => GameLoc.Get("Glossary_Misc");

        private const string TabsStop = "compendium:tabs";
        private const string HeaderStop = "compendium:header";
        private const string FiltersStop = "compendium:filters";

        private static GlossaryController Controller => GameScenes.Controller<GlossaryController>();

        // Up until the scene is gone, empty once leaving (see GlossaryNodes): dropping it at
        // the press would land on the screen beneath, then land there again after the scene change.
        public override bool IsActive() => Controller != null;

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;

            b.BeginStop(TabsStop);
            b.PushContext(null);
            b.StartRow(TabsStop);
            if (c.ClassButtons != null)
                foreach (var tab in c.ClassButtons)
                    if (ViewNodes.IsShown(tab)) b.AddItem(ControlId.Structural("compendium:tab:" + tab.gameObject.name), Tab(tab));
            b.EndRow();
            b.PopContext();

            b.BeginStop(HeaderStop);
            b.PushContext(null, positions: false);
            b.AddItem(ControlId.Structural("compendium:count"), Count(c));
            var filters = GameScenes.Item(c, "FilterToggleButton");
            if (ViewNodes.IsShown(filters)) b.AddItem(ControlId.Structural("compendium:filtertoggle"), FilterToggle(c, filters));
            b.PopContext();

            if (c.IsFilterPanelActive) BuildFilters(b, c);

            BuildItems(b, c);

            GlossaryNodes.AddBottom(b, c, "compendium");
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Back());
        }

        private static void Back()
        {
            var c = Controller;
            if (c == null || c.IsEndingScene) return;
            try
            {
                if (c.IsFilterPanelActive) c.OnPressFilterButton();
                else c.ExitScene();
            }
            catch (Exception e) { CoreLog.Warning("Compendium: going back failed: " + e.Message); }
        }

        // The filter panel opening takes the focus to its buttons; closing it, back to the Filters button.
        private bool _wasFiltering;

        public override void OnUpdate()
        {
            var c = Controller;
            bool filtering = c != null && c.IsFilterPanelActive;
            if (filtering != _wasFiltering)
            {
                if (filtering) Navigation.Active?.FocusStop(FiltersStop);
                else if (c != null) Navigation.Active?.MoveTo(ControlId.Structural("compendium:filtertoggle"));
            }
            _wasFiltering = filtering;
        }

        // ---- the tabs ----

        // A class tab: landing on it shows the class, as its click; a locked one ("? ? ? ? ?") reads
        // the game's LOCKED and is never clicked (its click only shakes it).
        private static NodeVtable Tab(GlossaryClassButton tab)
        {
            Func<string> label = () => tab.IsDisabled() ? GameLoc.Get("LockedTitle_Misc") : ViewNodes.Label(tab);
            Action pick = () => { if (!tab.IsDisabled() && !tab._selected) ViewNodes.Click(tab); };
            return new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                IsSelected = () => tab._selected,
                SearchText = label,
                OnActivate = pick,
                OnFocus = () => { ViewNodes.Hover(tab); pick(); },
            };
        }

        // ---- the count panel and the filters ----

        // The panel's figures ("21/115, 18% found", or the victories in the victory view); Enter is
        // its arrow, switching between the two.
        private static NodeVtable Count(GlossaryController c)
        {
            Func<string> text = () => CountText(c);
            var vt = GameNodes.Button(text, () =>
            {
                var arrow = GameScenes.Item(c, "ArrowRightButton");
                if (ViewNodes.IsShown(arrow)) ViewNodes.Click(arrow);
                else c.OnToggleVictoryViewMode();
            });
            // Named for what it counts; the figures live: Enter swaps them in place, and the new ones
            // are the answer.
            var parts = new List<NodeAnnouncement>(vt.Announcements);
            parts[0] = GameNodes.LabelPart(() => Scope(c));
            parts.Insert(1, new NodeAnnouncement(text, live: true, kind: AnnouncementKinds.Value));
            vt.Announcements = parts;
            vt.SearchText = () => Scope(c);
            return vt;
        }

        // What the panel counts: the items shown, so the class (every class with the filter's Show
        // ALL classes) and the kind the Type filter leaves ("Gunner cards and artifacts").
        private static string Scope(GlossaryController c)
        {
            string scope = null;
            try
            {
                var all = c.FilterPanel != null ? c.FilterPanel.ShowAllButton : null;
                if (all != null && all.IsSelected) scope = Strings.CompendiumAllClasses;
                else if (c.ClassButtons != null)
                    foreach (var tab in c.ClassButtons)
                        if (tab != null && tab._selected) scope = ViewNodes.Label(tab);
            }
            catch (Exception e) { CoreLog.Warning("Compendium: reading the class shown failed: " + e.Message); }
            string kind = Strings.CompendiumKind(c.CardFilter, c.ArtifactFilter);
            return scope != null ? Strings.CompendiumScope(scope, kind) : kind;
        }

        private static string CountText(GlossaryController c)
        {
            var panel = c.IsVictoryViewMode ? c.CompletionPanelObject : c.DiscoveredPanelObject;
            if (panel == null || !panel.activeInHierarchy) panel = c.CompletionSquareBackground;
            if (panel == null) return null;
            var parts = new List<string>();
            foreach (var tmp in panel.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                string t = GameText.Of(tmp);
                if (string.IsNullOrWhiteSpace(t)) continue;
                t = t.Replace(" /", "/").Replace("/ ", "/").Trim();
                if (!parts.Contains(t)) parts.Add(t);
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        // "Filters, button[, on]": on while any filter the panel shows as on is set.
        private static NodeVtable FilterToggle(GlossaryController c, AViewItem button)
        {
            var vt = ViewNodes.Button(button);
            vt.Announcements = new List<NodeAnnouncement>(vt.Announcements)
            {
                new NodeAnnouncement(() => FilterOn(c) ? Strings.StateOn : null, live: true, kind: AnnouncementKinds.Value),
            };
            return vt;
        }

        private static bool FilterOn(GlossaryController c)
        {
            try { return c.FilterPanel != null && c.FilterPanel.IsFilterOn(); }
            catch { return false; }
        }

        // The panel's sections in order, each its title then its buttons; Clear All last.
        private static void BuildFilters(GraphBuilder b, GlossaryController c)
        {
            var panel = c.FilterPanel != null ? c.FilterPanel.transform : null;
            if (panel == null) return;
            b.BeginStop(FiltersStop);
            for (int i = 0; i < panel.childCount; i++)
            {
                var section = panel.GetChild(i);
                if (!section.gameObject.activeInHierarchy) continue;
                var own = section.GetComponent<FilterButton>();
                if (own != null)
                {
                    // Clear All; the mock and the "Filters" caption are buttons too, but no filters.
                    if (section.name == "Clear")
                    {
                        b.PushContext(null);
                        b.AddItem(ControlId.Structural("compendium:filter:Clear"), ViewNodes.Button(own));
                        b.PopContext();
                    }
                    continue;
                }
                var title = section.Find("Title")?.GetComponent<TMPro.TMP_Text>();
                b.PushContext(title != null ? GameText.Of(title) : null);
                foreach (var button in section.GetComponentsInChildren<FilterButton>(false))
                    b.AddItem(ControlId.Structural("compendium:filter:" + section.name + "/" + button.gameObject.name), Filter(button));
                b.PopContext();
            }
        }

        // "Rare, toggle, on/off[, disabled]"; Enter is its click.
        private static NodeVtable Filter(FilterButton button)
        {
            Func<string> label = () => ViewNodes.Label(button);
            Func<string> state = () => button.IsSelected ? Strings.StateOn : Strings.StateOff;
            return new NodeVtable
            {
                ControlType = ControlTypes.Toggle,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(label),
                    new NodeAnnouncement(state, live: true, kind: AnnouncementKinds.Value),
                    GameNodes.DisabledPart(() => !button.IsDisabled),
                },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(button),
                OnActivate = () => { if (!button.IsDisabled) ViewNodes.Click(button); },
                StateText = state,
            };
        }

        // ---- the items ----

        // In the order shown: the scroll list's grids (the class's artifacts, then its cards), then
        // each pilot group's under its title.
        private static void BuildItems(GraphBuilder b, GlossaryController c)
        {
            var list = c.verticalLayoutGroup;
            if (list == null) return;
            b.BeginStop("compendium:items");
            b.PushContext(null);
            for (int i = 0; i < list.childCount; i++)
            {
                var child = list.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var group = child.GetComponent<GlossaryPilotGroup>();
                if (group == null) { AddGrid(b, child); continue; }
                b.PushContext(group.textMeshProUGUI != null ? GameText.Of(group.textMeshProUGUI) : null);
                for (int j = 0; j < child.childCount; j++)
                    if (child.GetChild(j).gameObject.activeInHierarchy) AddGrid(b, child.GetChild(j));
                b.PopContext();
            }
            b.PopContext();
        }

        private static void AddGrid(GraphBuilder b, Transform grid)
        {
            for (int i = 0; i < grid.childCount; i++)
            {
                var child = grid.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var card = child.GetComponent<DeckCardView>();
                if (card != null && card.CardModel != null) { b.AddItem(ControlId.Structural("compendium:item:" + card.GetInstanceID()), Card(card)); continue; }
                var artifact = child.GetComponent<DeckArtifactView>();
                if (artifact != null && artifact.ArtifactModel != null) b.AddItem(ControlId.Structural("compendium:item:" + artifact.GetInstanceID()), Artifact(artifact));
            }
        }

        private static NodeVtable Card(DeckCardView view)
        {
            if (view.IsLockedMode || view.IsUndiscoveredMode) return Hidden(view, view.IsLockedMode, view._StarObject);
            var vt = CardNodes.Card(view.CardModel, view);
            vt.OnActivate = null;
            return WithVictory(vt, view._StarObject);
        }

        private static NodeVtable Artifact(DeckArtifactView view)
        {
            if (view.IsLockedMode || view.IsUndiscoveredMode) return Hidden(view, view.IsLockedMode, view._StarObject);
            return WithVictory(CardNodes.Artifact(view.ArtifactModel, view), view._StarObject);
        }

        // A locked or undiscovered item, as the game shows it: its title and explanation, no name.
        private static NodeVtable Hidden(AViewItem view, bool locked, CanvasGroup star)
        {
            string key = locked ? "Locked" : "Undiscovered";
            Func<string> label = () => GameLoc.Get(key + "Title_Misc");
            Func<string> description = () => GameLoc.Get(key + "Desc_Misc");
            return WithVictory(new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label), GameNodes.TooltipPart(description) },
                SearchText = label,
                OnFocus = () => ViewNodes.Hover(view),
                Details = () => GameNodes.Lines(description()),
            }, star);
        }

        // In the victory view, the game's star on an item won with: "Victory" after its name, the
        // sticker's explanation first in its details.
        private static NodeVtable WithVictory(NodeVtable vt, CanvasGroup star)
        {
            Func<bool> won = () => star != null && star.gameObject.activeInHierarchy && star.alpha > 0.05f;
            var parts = new List<NodeAnnouncement>(vt.Announcements);
            parts.Insert(1, new NodeAnnouncement(() => won() ? GameLoc.Get("VictoryTitle_Misc") : null, kind: AnnouncementKinds.Value));
            vt.Announcements = parts;
            var details = vt.Details;
            vt.Details = () =>
            {
                var lines = new List<string>();
                if (won()) lines.AddRange(GameNodes.Lines(GameLoc.Get("VictoryStickerDescription_Misc")));
                if (details != null) lines.AddRange(details());
                return lines;
            };
            return vt;
        }
    }
}
