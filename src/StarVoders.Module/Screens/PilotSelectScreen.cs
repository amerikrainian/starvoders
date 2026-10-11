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
    /// Choosing the pilot for a new run (<c>CharacterSelectionScene</c>, <see cref="MechSelectionController"/>),
    /// two levels on one screen. The classes: one entry per mech panel in order (its <c>TitlePanelHolder</c>
    /// number), Gunner, then the two others, which read LOCKED with their condition while their lock
    /// shows (the third's checklist: each named class "done" or "not done", by its check or X); Enter on
    /// an open one is its panel's click (<c>OnPressGunnerPanel</c>...); Random pilot (a random class's
    /// random pilot, shown, nothing started) and Back (to the main menu). A class's pilots
    /// (<see cref="MechSelectionController.CurrentPilotSelectionController"/>): the pilot ("Roxy, 1/4", Left and Right turning as the arrows do); the panel shown (by
    /// its CanvasGroup: open = complexity counted from its "=" glyphs, description, victories,
    /// the starter deck and artifact; locked = LOCKED and its condition or checklist); Go! while shown
    /// (difficulty next); the class's Guide; Back to the classes; all one stop, the class's level and
    /// experience on top of it, focus starting on the pilot (the game's arrow buttons are Left and
    /// Right on the pilot). Escape is the shown level's
    /// Back. While the game slides between the levels (<c>IsTransitioning</c>) the screen is empty.
    /// </summary>
    public sealed class PilotSelectScreen : Screen
    {
        public override string Key => "pilots";
        public override string ScreenName => GameLoc.Get("Pilots_Misc");

        private static MechSelectionController Controller => GameScenes.Controller<MechSelectionController>();

        // The classes by their panel's number: the game's own order.
        private static readonly string[] Panels = { "GunnerPanel", "MeleePanel", "MysticPanel" };

        public override bool IsActive() => Controller != null;

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => Back());
        }

        private static void Back()
        {
            var c = Controller;
            if (c == null || c.IsTransitioning) return;
            try
            {
                var pilots = Pilots(c);
                if (pilots != null) pilots.OnPressReturnToMechScene();
                else c.OnPressBackButton();
            }
            catch (Exception e) { CoreLog.Warning("Pilots: going back failed: " + e.Message); }
        }

        private static PilotSelectionController Pilots(MechSelectionController c)
        {
            try { return c.IsInPilotSelection() ? c.CurrentPilotSelectionController : null; }
            catch { return null; }
        }

        // The level shown changes the whole screen: focus starts over on it.
        private bool _wasPilots;

        public override void OnUpdate()
        {
            var c = Controller;
            if (c == null || c.IsTransitioning) return;
            bool pilots = Pilots(c) != null;
            if (pilots != _wasPilots)
            {
                // The pilot, not the stop's first item (the class's level above it).
                if (pilots) Navigation.Active?.FocusNode(PilotId);
                else Navigation.Active?.FocusStop("pilots:classes");
            }
            _wasPilots = pilots;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsTransitioning) return;
            var pilots = Pilots(c);
            if (pilots != null) BuildPilots(b, pilots);
            else BuildClasses(b, c);
        }

        // ---- the classes ----

        private static void BuildClasses(GraphBuilder b, MechSelectionController c)
        {
            b.BeginStop("pilots:classes");
            b.PushContext(null);
            var screen = c.MeleeLockImage != null ? c.MeleeLockImage.transform.parent.parent : null;
            for (int i = 0; i < Panels.Length; i++)
            {
                var panel = screen != null ? screen.Find(Panels[i]) : null;
                if (panel == null || !panel.gameObject.activeInHierarchy) continue;
                b.AddItem(ControlId.Structural("pilots:class" + i), Class(c, panel, i));
            }
            b.PopContext();

            // Random and Back follow the classes in the same stop, unnumbered.
            b.PushContext(null, positions: false);
            if (ViewNodes.IsShown(c.RandomButtonView))
                b.AddItem(ControlId.Structural("pilots:random"), ViewNodes.Button(c.RandomButtonView, () => Strings.PilotsRandom));
            if (ViewNodes.IsShown(c.BackButtonView))
                b.AddItem(ControlId.Structural("pilots:back"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Back));
            b.PopContext();
        }

        // A class: its title, or LOCKED with what unlocks it while its lock shows.
        private static NodeVtable Class(MechSelectionController c, Transform panel, int index)
        {
            var lockIcon = index == 1 ? c.MeleeLockImage : index == 2 ? c.MysticLockImage : null;
            Func<bool> locked = () => lockIcon != null && lockIcon.gameObject.activeInHierarchy;
            TMP_Text title = null;
            var holder = panel.Find("TitlePanelHolder");
            if (holder != null)
                foreach (var t in holder.GetComponentsInChildren<TMP_Text>(true))
                    if (t.name == "TitleText") { title = t; break; }
            Func<string> label = () => locked() ? GameLoc.Get("LockedTitle_Misc") : GameText.Of(title);
            Func<string> condition = () => locked() ? LockText(c, index) : null;
            var vt = GameNodes.Button(label, () =>
            {
                try
                {
                    if (index == 0) c.OnPressGunnerPanel();
                    else if (index == 1) c.OnPressMeleePanel();
                    else c.OnPressMysticPanel();
                }
                catch (Exception e) { CoreLog.Warning("Pilots: opening a class failed: " + e.Message); }
            }, () => !locked() && !c.IsTransitioning);
            vt.Announcements = new List<NodeAnnouncement>(vt.Announcements) { GameNodes.TooltipPart(condition) };
            return vt;
        }

        // "Win on Invasion difficulty to unlock"; the third class's adds its checklist ("Gunner, not
        // done"), each class by its check or X.
        private static string LockText(MechSelectionController c, int index)
        {
            var icon = index == 1 ? c.MeleeLockImage : c.MysticLockImage;
            if (icon == null) return null;
            var texts = icon.GetComponentsInChildren<TMP_Text>(false);
            if (texts.Length == 0) return null;
            var parts = new List<string> { GameText.Of(texts[0]) };
            if (index == 2)
            {
                parts.Add(Checked(GameText.Of(c.MysticLockGunnerX != null ? FirstText(c.MysticLockGunnerX.transform.parent) : null), c.MysticLockGunnerCheck));
                parts.Add(Checked(GameText.Of(c.MysticStingerCheckboxText), c.MysticLockMeleeCheck));
            }
            parts.RemoveAll(string.IsNullOrEmpty);
            return string.Join(", ", parts);
        }

        private static TMP_Text FirstText(Transform t) => t != null ? t.GetComponentInChildren<TMP_Text>(false) : null;

        private static string Checked(string name, GameObject check)
        {
            if (string.IsNullOrEmpty(name)) return null;
            bool done = check != null && check.activeInHierarchy;
            return Strings.FightLabelled(name, done ? Strings.PilotsDone : Strings.PilotsNotDone);
        }

        // ---- a class's pilots ----

        private static void BuildPilots(GraphBuilder b, PilotSelectionController p)
        {
            var view = p.PilotView;
            b.BeginStop("pilots:pilot");
            b.PushContext(null, positions: false);
            // The class's level and experience on top (the panel beside the pilot, the Guide under it);
            // focus starts on the pilot below it.
            if (ViewNodes.IsShown(p.PilotXPView))
                b.AddItem(ControlId.Structural("pilots:level"), GameNodes.Text(() => Level(p)));
            b.AddItem(PilotId, Pilot(p));
            b.SetStart(PilotId);
            int i = 0;
            foreach (var line in PanelLines(view))
            {
                string text = line;
                b.AddItem(ControlId.Structural("pilots:line" + i++), GameNodes.Text(() => text));
            }
            if (Shown(view?.PilotPanelCanvasGroup))
            {
                if (ViewNodes.IsShown(p.DeckButtonView))
                    b.AddItem(ControlId.Structural("pilots:deck"), ViewNodes.Button(p.DeckButtonView, () => GameLoc.Get("Deck_Misc")));
                var artifact = view.PilotSelectArtifactView;
                if (ViewNodes.IsShown(artifact) && artifact.ArtifactModel != null)
                    b.AddItem(ControlId.Structural("pilots:artifact"), CardNodes.Artifact(artifact.ArtifactModel, artifact));
            }
            // Go! for an open pilot only (the game hides it from a locked one by sliding it off screen),
            // then the class's guide (the guide scene, on its page; beside the level panel, not in it)
            // and Back. The game's pilot arrows are Left/Right on the pilot's line.
            if (ViewNodes.IsShown(p.GoButtonView) && Shown(view?.PilotPanelCanvasGroup))
                b.AddItem(ControlId.Structural("pilots:go"), ViewNodes.Button(p.GoButtonView));
            var guideButton = p.transform.Find("MechPanel/Mediator/GuideButton/GuideButton")?.GetComponent<AViewItem>();
            if (ViewNodes.IsShown(guideButton)) b.AddItem(ControlId.Structural("pilots:guide"), ViewNodes.Button(guideButton));
            if (ViewNodes.IsShown(p.BackButtonView))
                b.AddItem(ControlId.Structural("pilots:pilotback"), GameNodes.Button(() => Strings.Get("bind.ui.back"), Back));
            b.PopContext();
        }

        private static readonly ControlId PilotId = ControlId.Structural("pilots:name");

        private static bool Shown(CanvasGroup group) => group != null && group.gameObject.activeInHierarchy && group.alpha > 0.05f;

        // "Gunner, lv 0, Exp: 10/500" (the level panel's own words), or its "max" line once reached.
        private static string Level(PilotSelectionController p)
        {
            var level = p.PilotXPView != null ? p.PilotXPView.TryCast<MechLevelView>() : null;
            if (level == null) return null;
            var parts = new List<string> { GameText.Of(level.ClassTitleText) };
            var diamond = level.LevelDiamond != null ? level.LevelDiamond.GetComponentsInChildren<TMP_Text>(false) : null;
            if (diamond != null && diamond.Length > 0)
            {
                var d = new List<string>();
                foreach (var t in diamond) { string s = GameText.Of(t); if (!string.IsNullOrEmpty(s)) d.Add(s); }
                parts.Add(string.Join(" ", d));
            }
            if (level.ExpTextHolder != null && level.ExpTextHolder.activeInHierarchy)
                parts.Add(Joined(level.ExpTextHolder.transform, ""));
            if (level.MaxReachedHolder != null && level.MaxReachedHolder.activeInHierarchy)
                parts.Add(Joined(level.MaxReachedHolder.transform, " "));
            parts.RemoveAll(string.IsNullOrEmpty);
            return string.Join(", ", parts);
        }

        // A holder's shown texts joined ("Exp:" "10" "/" "500": "Exp: 10/500").
        private static string Joined(Transform holder, string between)
        {
            var parts = new List<string>();
            foreach (var t in holder.GetComponentsInChildren<TMP_Text>(false)) { string s = GameText.Of(t); if (!string.IsNullOrEmpty(s)) parts.Add(s); }
            if (parts.Count == 0) return null;
            string first = parts[0];
            parts.RemoveAt(0);
            return parts.Count > 0 ? first + " " + string.Join(between, parts) : first;
        }

        // "Roxy, 1/4"; Left and Right turn to the neighbouring pilot, as the arrows.
        private static NodeVtable Pilot(PilotSelectionController p)
        {
            Func<string> label = () =>
            {
                var view = p.PilotView;
                if (view == null) return null;
                string name = GameText.Of(view.PilotNameText);
                // The counter as the game counts it ("2/4"): each panel (open, locked) draws its own,
                // and the one fading in still shows the previous pilot's for a moment.
                string pip = Position(p);
                return name != null && pip != null ? name + ", " + pip : name ?? pip;
            };
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(label, live: true, kind: AnnouncementKinds.Label) },
                SearchText = label,
                OnAdjust = (sign, large) =>
                {
                    try { if (sign > 0) p.OnPressRightArrow(); else p.OnPressLeftArrow(); }
                    catch (Exception e) { CoreLog.Warning("Pilots: turning the pilot failed: " + e.Message); }
                },
            };
        }

        // "3/4": the current pilot's place in the class's list.
        private static string Position(PilotSelectionController p)
        {
            var pilots = p.PlayerDataSOs;
            var current = p.CurrentPlayerData;
            if (pilots == null || current == null) return null;
            for (int i = 0; i < pilots.Count; i++)
                if (pilots[i] != null && pilots[i].Pointer == current.Pointer) return (i + 1) + "/" + pilots.Count;
            return null;
        }

        // The shown panel: open = "Complexity: 2", the description's lines, the victories; locked =
        // LOCKED, the condition, a checklist of pilots ("Roxy: done").
        private static List<string> PanelLines(PilotView view)
        {
            var lines = new List<string>();
            if (view == null) return lines;
            if (Shown(view.PilotPanelCanvasGroup))
            {
                string complexity = view.PilotComplexityText != null ? view.PilotComplexityText.text : null;
                if (!string.IsNullOrEmpty(complexity))
                {
                    int level = 0;
                    foreach (char ch in complexity) if (ch == '=') level++;
                    string word = GameText.Clean(complexity.Replace("=", "")).Trim();
                    lines.Add(level > 0 ? Strings.FightLabelled(word, level.ToString()) : word);
                }
                AddLines(lines, view.PilotDescriptionText);
                if (view.VictoryTrackerGameObject != null && view.VictoryTrackerGameObject.activeInHierarchy)
                    AddLines(lines, view.VictoryTrackerText);
            }
            else if (Shown(view.LockedPanelCanvasGroup))
            {
                lines.Add(GameLoc.Get("LockedTitle_Misc"));
                var texts = new[] { view.LockedPilot1Text, view.LockedPilot2Text, view.LockedPilot3Text, view.LockedPilot4Text };
                var checks = new[] { view.LockedPilot1Check, view.LockedPilot2Check, view.LockedPilot3Check, view.LockedPilot4Check };
                bool checklist = false;
                foreach (var t in texts) if (t != null && t.gameObject.activeInHierarchy) checklist = true;
                AddLines(lines, checklist ? view.LockedPilotPanelDescriptionText : view.LockedDescriptionText);
                for (int i = 0; i < texts.Length; i++)
                    if (texts[i] != null && texts[i].gameObject.activeInHierarchy)
                        lines.Add(Checked(GameText.Of(texts[i]), checks[i]));
            }
            lines.RemoveAll(string.IsNullOrEmpty);
            return lines;
        }

        private static void AddLines(List<string> lines, TMP_Text tmp)
        {
            if (tmp == null || !tmp.gameObject.activeInHierarchy || string.IsNullOrEmpty(tmp.text)) return;
            foreach (var raw in tmp.text.Split('\n'))
            {
                string line = GameText.Clean(raw)?.Trim();
                if (!string.IsNullOrEmpty(line)) lines.Add(line);
            }
        }
    }
}
