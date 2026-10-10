using System.Collections.Generic;
using StarVaders;
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
    /// The options (<see cref="SettingsSceneController"/>, an additive scene over the main menu or a
    /// run): the tab strip (Gameplay, Sound, Graphics, Controls, Extras) as one Tab-stop, the shown
    /// page's rows as the next, and Apply and Exit, Reset to Default, Skip Tutorial as the last. A
    /// page is <c>Panels[_currentTab]</c>; its rows are the view items under it that sit in no other
    /// view item (a chooser's arrows and a key row's set button are parts of their row), in the
    /// game's order. Escape is what the game wires its cancel to: Apply and Exit.
    /// </summary>
    public sealed class SettingsScreen : Screen
    {
        public override string Key => "settings";
        // The panel's own title ("Options"), else the mod's word.
        public override string ScreenName
        {
            get
            {
                var title = Controller?.MainPanel?.Find("TitleText");
                var tmp = title != null ? title.GetComponent<TMPro.TMP_Text>() : null;
                return tmp != null && !string.IsNullOrWhiteSpace(tmp.text) ? tmp.text : Strings.ScreenSettings;
            }
        }
        public override int Layer => 10;
        public override bool Exclusive => true;
        public override object InitialFocusStop => "tabs";

        private static readonly string[] TabNames = { "Tab (1)", "Tab (2)", "Tab (3)", "Tab (4)", "Tab (5)" };
        private static readonly string[] ActionNames = { "ApplyButton", "ResetButton", "SkipButton" };

        private static SettingsSceneController Controller => GameScenes.Controller<SettingsSceneController>();

        public override bool IsActive()
        {
            var c = Controller;
            return c != null && c.MainPanel != null && c.MainPanel.gameObject.activeInHierarchy;
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null) return;

            b.BeginStop("tabs");
            b.PushContext(null);
            b.StartRow(entry: () => c._currentTab);
            for (int i = 0; i < TabNames.Length; i++)
            {
                int index = i;
                var tab = GameScenes.Item(c, TabNames[i]);
                if (!ViewNodes.IsShown(tab)) continue;
                b.AddItem(ControlId.Structural("settings:tab" + i),
                    OptionNodes.Tab(tab, () => c._currentTab == index, () => ViewNodes.Click(tab)));
            }
            b.EndRow();
            b.PopContext();

            b.BeginStop("page");
            b.PushContext(null);
            var panels = c.Panels;
            int current = c._currentTab;
            var page = panels != null && current >= 0 && current < panels.Count ? panels[current] : null;
            if (page != null && page.activeInHierarchy)
                foreach (var item in page.GetComponentsInChildren<AViewItem>(false))
                    if (IsRow(item, page.transform)) AddRow(b, item);
            b.PopContext();

            b.BeginStop("actions");
            b.PushContext(null);
            foreach (var name in ActionNames)
            {
                var item = Bottom(c, name);
                if (ViewNodes.IsShown(item)) b.AddItem(ControlId.Structural("settings:" + name), PressedButton(item));
            }
            b.PopContext();
        }

        // A button along the panel's bottom, by name under the panel itself: the Graphics page has
        // an ApplyButton of its own, so a scene-wide name lookup would find the wrong one.
        private static AViewItem Bottom(SettingsSceneController c, string name)
        {
            var t = c != null && c.MainPanel != null ? c.MainPanel.Find(name) : null;
            return t != null ? t.GetComponent<AViewItem>() : null;
        }

        // A row of the page: a shown view item that is no part of another (an arrow, a set button,
        // the "?" tooltip trigger) and no input blocker.
        private static bool IsRow(AViewItem item, Transform page)
        {
            if (!ViewNodes.IsShown(item) || item.TryCast<TooltipTrigger>() != null || item.TryCast<BlockerViewItem>() != null)
                return false;
            for (var t = item.transform.parent; t != null && t != page; t = t.parent)
                if (t.GetComponent<AViewItem>() != null) return false;
            return true;
        }

        private static void AddRow(GraphBuilder b, AViewItem item)
        {
            var id = ControlId.Structural("settings:row:" + item.gameObject.name);
            var toggle = item.TryCast<OptionToggle>();
            if (toggle != null) { b.AddItem(id, OptionNodes.Toggle(toggle)); return; }
            var slider = item.TryCast<OptionSlider>();
            if (slider != null) { b.AddItem(id, OptionNodes.Slider(slider)); return; }
            var chooser = item.TryCast<OptionChooser>();
            if (chooser != null) { b.AddItem(id, OptionNodes.Chooser(chooser)); return; }
            var keybind = item.TryCast<OptionKeybind>();
            if (keybind != null) { b.AddItem(id, OptionNodes.Keybind(keybind)); return; }
            var button = item.TryCast<OptionButton>();
            if (button != null) { b.AddItem(id, OptionNodes.Button(button)); return; }
            b.AddItem(id, PressedButton(item)); // Apply (graphics), Reset (controls)
        }

        // A pressed button (Apply and Exit, the pages' Apply and Reset): disabled while the game
        // has it switched off.
        private static NodeVtable PressedButton(AViewItem item)
        {
            var pressed = item.TryCast<PressedButtonView>();
            var vt = ViewNodes.Button(item);
            if (pressed != null)
                vt = GameNodes.Button(() => ViewNodes.Label(item), () => ViewNodes.Click(item),
                    () => ViewNodes.IsShown(item) && pressed.ButtonEnabled);
            vt.OnFocus = () => ViewNodes.Hover(item);
            return vt;
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var apply = Bottom(Controller, "ApplyButton");
                if (ViewNodes.IsShown(apply)) ViewNodes.Click(apply);
            });
        }
    }
}
