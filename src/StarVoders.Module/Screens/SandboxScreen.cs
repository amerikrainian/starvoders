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
    /// Sandbox settings (<c>SandboxSceneAdditive</c>, <see cref="SandboxSceneController"/>, the
    /// options' Extras page's SANDBOX SETTINGS), titled by its TitleText. One stop of the panel's
    /// intro line, then the rows of its scrolled list in the game's order, each under its section
    /// (a header with one text: Difficulty, Chrono Token, Pack System, Encounters). A header with a
    /// column title (<c>Banned (1)</c>: "Pack", "Act 1 Boss") opens a ban list: its <c>PackDesc</c>
    /// rule is a line, its checkboxes read banned or allowed (checked = in
    /// <c>DisabledPacks</c>/<c>DisabledBosses</c>); a locked one reads only the game's LOCKED. Then
    /// Reset to Default (a confirm prompt) and Apply and Exit (<c>SaveAndExit</c>, which is also
    /// the game's cancel, so Escape): changes are kept only by it. Layer 36 over the options;
    /// empty once leaving.
    /// </summary>
    public sealed class SandboxScreen : Screen
    {
        public override string Key => "sandbox";
        public override int Layer => 36;
        public override bool Exclusive => true;
        public override object InitialFocusStop => "sandbox:rows";

        private static readonly string[] ActionNames = { "ResetButton", "ApplyButton" };

        private static SandboxSceneController Controller => GameScenes.Controller<SandboxSceneController>();

        public override bool IsActive() => Controller != null;

        public override string ScreenName
        {
            get
            {
                var c = Controller;
                var title = c != null && c.MainPanel != null ? c.MainPanel.Find("Holder/TitleText") : null;
                return (title != null ? GameText.Of(title.GetComponent<TMP_Text>()) : null) ?? Strings.ScreenSettings;
            }
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ =>
            {
                var apply = Bottom(Controller, "ApplyButton");
                if (ViewNodes.IsShown(apply)) ViewNodes.Click(apply);
            });
        }

        public override void Build(GraphBuilder b)
        {
            var c = Controller;
            if (c == null || c.IsEndingScene || c.MainPanel == null) return;

            b.BeginStop("sandbox:rows");
            b.PushContext(null);
            var intro = c.MainPanel.Find("Holder/Text (TMP)");
            string introText = intro != null ? GameText.Of(intro.GetComponent<TMP_Text>()) : null;
            if (introText != null)
                b.AddItem(ControlId.Structural("sandbox:intro"), GameNodes.Text(() => introText.Replace("\n", " ").Trim()));

            var list = c.MainPanel.Find("Holder/ExtrasPanel/ScrollRect/VerticalLayoutGroup");
            if (list != null) AddList(b, list);
            b.PopContext();

            b.BeginStop("sandbox:actions");
            b.PushContext(null);
            foreach (var name in ActionNames)
            {
                var item = Bottom(c, name);
                if (ViewNodes.IsShown(item)) b.AddItem(ControlId.Structural("sandbox:" + name), Pressed(item));
            }
            b.PopContext();
        }

        // The list's children in order: a row, a section title, or a ban list's header (its rule,
        // its column title). Sections and ban lists are contexts, closed by the next header.
        private static void AddList(GraphBuilder b, Transform list)
        {
            bool inSection = false, inBanList = false;
            for (int i = 0; i < list.childCount; i++)
            {
                var child = list.GetChild(i);
                if (!child.gameObject.activeInHierarchy) continue;
                var row = child.GetComponent<OptionView>();
                if (row != null)
                {
                    AddRow(b, row, inBanList);
                    continue;
                }

                string column = TextOf(child.Find("Banned (1)"));
                string rule = TextOf(child.Find("PackDesc"));
                if (inBanList) { b.PopContext(); inBanList = false; }
                if (column == null && rule == null)
                {
                    string title = null;
                    foreach (var tmp in child.GetComponentsInChildren<TMP_Text>(false))
                        if ((title = GameText.Of(tmp)) != null) break;
                    if (title == null) continue;
                    if (inSection) b.PopContext();
                    b.PushContext(title);
                    inSection = true;
                    continue;
                }
                if (rule != null)
                    b.AddItem(ControlId.Structural("sandbox:rule" + i), GameNodes.Text(() => rule.Replace("\n", " ").Trim()));
                if (column != null)
                {
                    b.PushContext(column);
                    inBanList = true;
                }
            }
            if (inBanList) b.PopContext();
            if (inSection) b.PopContext();
        }

        private static void AddRow(GraphBuilder b, OptionView row, bool banList)
        {
            var id = ControlId.Structural("sandbox:row:" + row.gameObject.name);
            var toggle = row.TryCast<OptionToggle>();
            if (toggle != null)
            {
                b.AddItem(id, banList
                    ? OptionNodes.Toggle(toggle, () => Strings.StateBanned, () => Strings.StateAllowed)
                    : OptionNodes.Toggle(toggle));
                return;
            }
            var button = row.TryCast<OptionButton>();
            if (button != null) { b.AddItem(id, OptionNodes.Button(button)); return; }
            CoreLog.Warning("Sandbox: unread row " + row.gameObject.name);
        }

        private static string TextOf(Transform t)
        {
            return t != null && t.gameObject.activeInHierarchy ? GameText.Of(t.GetComponent<TMP_Text>()) : null;
        }

        private static AViewItem Bottom(SandboxSceneController c, string name)
        {
            var t = c != null && c.MainPanel != null ? c.MainPanel.Find(name) : null;
            return t != null ? t.GetComponent<AViewItem>() : null;
        }

        private static NodeVtable Pressed(AViewItem item)
        {
            var pressed = item.TryCast<PressedButtonView>();
            var vt = pressed != null
                ? GameNodes.Button(() => ViewNodes.Label(item), () => ViewNodes.Click(item),
                    () => ViewNodes.IsShown(item) && pressed.ButtonEnabled)
                : ViewNodes.Button(item);
            vt.OnFocus = () => ViewNodes.Hover(item);
            return vt;
        }
    }
}
