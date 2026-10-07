using System;
using System.Collections.Generic;
using System.Globalization;
using StarVaders;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using TMPro;

namespace StarVadersAccess.Module.UI
{
    /// <summary>
    /// Node factories over the game's option rows (<see cref="OptionView"/> and its kinds), the
    /// settings screen's widgets. Every change goes through the row's own handlers
    /// (<c>OnClick</c>, <c>OnLeftPressed</c>, <c>OnRightPressed</c>), as the game's keyboard does, so
    /// its settings logic (applying volumes, resolution choices, cosmetics) runs unchanged. A row's
    /// caption is its <c>Description</c> text (the game's name for the row's label); its tooltip, the
    /// text of the "?" beside it, is read only where the row shows that "?": the
    /// others carry the prefab's placeholder text.
    /// </summary>
    internal static class OptionNodes
    {
        /// <summary>The row's caption.</summary>
        public static string Label(OptionView row)
        {
            var d = row != null ? row.Description : null;
            return d != null && !string.IsNullOrWhiteSpace(d.text) ? d.text : ViewNodes.Label(row);
        }

        /// <summary>The row's tooltip body, or null when the row shows no "?". The "?" is the test,
        /// not <c>HasTooltip</c>: several Graphics rows set it with their "?" hidden and a tooltip
        /// copied from another row.</summary>
        public static string Tooltip(OptionView row)
        {
            var trigger = row != null ? row.TooltipTrigger : null;
            if (trigger == null || !trigger.gameObject.activeInHierarchy) return null;
            string body = null;
            foreach (var tmp in trigger.GetComponentsInChildren<TMP_Text>(true))
                if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text)) body = tmp.text.Trim();
            return body; // the last text: the first is the tooltip's title
        }

        /// <summary>A tab of the strip: "label, tab[, selected]"; landing on it shows its page, as a
        /// tab does. The selected part is what Tab-stop entry lands on when the strip has no
        /// remembered position (a fresh render, a reload): landing elsewhere would switch the page.</summary>
        public static NodeVtable Tab(AViewItem tab, Func<bool> selected, Action select)
        {
            Func<string> label = () => ViewNodes.Label(tab);
            Action pick = () => { if (!selected()) select(); };
            return new NodeVtable
            {
                ControlType = ControlTypes.Tab,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label), GameNodes.SelectedPart(selected) },
                SearchText = label,
                OnActivate = pick,
                OnFocus = () => { ViewNodes.Hover(tab); pick(); },
            };
        }

        /// <summary>A toggle row: "label, toggle, on/off[, disabled]"; Enter is the row's click.</summary>
        public static NodeVtable Toggle(OptionToggle row)
        {
            Func<string> label = () => Label(row);
            Func<string> state = () => row.Toggle != null && row.Toggle.isOn ? Strings.StateOn : Strings.StateOff;
            return Row(row, ControlTypes.Toggle, label, state, () => !row._isLocked, onActivate: () => row.OnClick());
        }

        /// <summary>A slider row: "label, slider, value"; Left and Right are the row's own steps
        /// (five at a time with the large step). A 0 to 1 slider (the volumes) reads as a percent,
        /// a whole-number one (the animation speeds) as its number.</summary>
        public static NodeVtable Slider(OptionSlider row)
        {
            Func<string> label = () => Label(row);
            Func<string> value = () => SliderText(row);
            var vt = Row(row, ControlTypes.Slider, label, value, null, onActivate: null);
            vt.OnAdjust = (sign, large) => Step(sign, large, row.OnRightPressed, row.OnLeftPressed);
            return vt;
        }

        /// <summary>A chooser row (resolution, screen type, skins): "label, combo box, current";
        /// Left and Right step through the choices with the row's own arrows, Enter steps forward.</summary>
        public static NodeVtable Chooser(OptionChooser row)
        {
            Func<string> label = () => Label(row);
            Func<string> value = () => row.CurrentOptionText != null ? row.CurrentOptionText.text : null;
            var vt = Row(row, ControlTypes.ComboBox, label, value, null, onActivate: () => row.OnRightPressed());
            vt.OnAdjust = (sign, large) => Step(sign, false, row.OnRightPressed, row.OnLeftPressed);
            return vt;
        }

        /// <summary>A button row: "label, button"; Enter is the row's click.</summary>
        public static NodeVtable Button(OptionButton row)
        {
            Func<string> label = () => Label(row);
            return Row(row, ControlTypes.Button, label, null, null, onActivate: () => row.OnClick());
        }

        /// <summary>A key binding row, read only: "action, key binding, key". The game's bindings
        /// only matter while the mod has handed the keyboard back (Ctrl+Shift+A), and rebinding
        /// listens on the keyboard focus mode switches off, so it is not offered.</summary>
        public static NodeVtable Keybind(OptionKeybind row)
        {
            Func<string> label = () => Label(row);
            Func<string> key = () =>
            {
                var prompt = row.MainKeyprompt;
                var text = prompt != null ? prompt._keyText : null;
                return text != null && !string.IsNullOrWhiteSpace(text.text) ? text.text : null;
            };
            return Row(row, ControlTypes.KeyBinding, label, key, null, onActivate: null);
        }

        private static NodeVtable Row(OptionView row, ControlType type, Func<string> label, Func<string> value,
            Func<bool> enabled, Action onActivate)
        {
            var parts = new List<NodeAnnouncement> { GameNodes.LabelPart(label) };
            if (value != null) parts.Add(new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value));
            parts.Add(GameNodes.DisabledPart(enabled));
            parts.Add(GameNodes.TooltipPart(() => Tooltip(row)));
            return new NodeVtable
            {
                ControlType = type,
                Announcements = parts,
                SearchText = label,
                StateText = value,
                OnFocus = () => ViewNodes.Hover(row),
                OnActivate = onActivate == null ? null : () =>
                {
                    if (enabled != null && !enabled()) return;
                    onActivate();
                },
            };
        }

        private static void Step(int sign, bool large, Action right, Action left)
        {
            int n = large ? 5 : 1;
            for (int i = 0; i < n; i++) (sign > 0 ? right : left)();
        }

        private static string SliderText(OptionSlider row)
        {
            var s = row.Slider;
            if (s == null) return null;
            if (s.wholeNumbers || row.isIntIncrement) return ((int)Math.Round(s.value)).ToString(CultureInfo.InvariantCulture);
            if (s.minValue == 0f && s.maxValue == 1f) return Strings.Percent((int)Math.Round(s.value * 100f));
            return Math.Round(s.value, 2).ToString(CultureInfo.InvariantCulture);
        }
    }
}
