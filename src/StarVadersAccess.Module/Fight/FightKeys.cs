using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Input;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Input;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.Screens;
using StarVadersAccess.Module.UI;
using UnityEngine;

namespace StarVadersAccess.Module.Fight
{
    /// <summary>
    /// The fight's keys (the Game category, live while the fight screen is): digits play the nth
    /// card; single letters read one thing (C coordinates, T turn, D Doom, H Heat, O objective, M the
    /// mech) and are silent where there is nothing to read; Shift+M goes to the mech; [ and ] cycle
    /// the board's categories (invaders, mech, bombs and items, pickups, danger zones; only those
    /// with something in them), comma and period step through the current one in reading order
    /// (top row first), moving the cursor; Alt+Backspace jumps back to where a jump left from; E ends
    /// the turn; Shift+arrows skip cells that read the same.
    /// </summary>
    internal static class FightKeys
    {
        public static void Register()
        {
            for (int n = 1; n <= 10; n++)
            {
                int card = n;
                InputManager.Register("fight.card" + card, "Play card " + card, InputCategory.Game, () => EncounterScreen.PlayCard(card))
                    .AddBinding(new KeyboardBinding(card == 10 ? KeyCode.Alpha0 : KeyCode.Alpha0 + card));
            }
            InputManager.Register("fight.skip.up", "Skip up", InputCategory.Game, () => EncounterScreen.Skip(0, 1))
                .AddBinding(new KeyboardBinding(KeyCode.UpArrow, shift: true)).Repeating();
            InputManager.Register("fight.skip.down", "Skip down", InputCategory.Game, () => EncounterScreen.Skip(0, -1))
                .AddBinding(new KeyboardBinding(KeyCode.DownArrow, shift: true)).Repeating();
            InputManager.Register("fight.skip.left", "Skip left", InputCategory.Game, () => EncounterScreen.Skip(-1, 0))
                .AddBinding(new KeyboardBinding(KeyCode.LeftArrow, shift: true)).Repeating();
            InputManager.Register("fight.skip.right", "Skip right", InputCategory.Game, () => EncounterScreen.Skip(1, 0))
                .AddBinding(new KeyboardBinding(KeyCode.RightArrow, shift: true)).Repeating();

            Read("fight.coords", "Coordinates", KeyCode.C, Coords);
            Read("fight.turn", "Turn", KeyCode.T, EncounterScreen.TurnText);
            Read("fight.doom", "Doom", KeyCode.D, () => Visible(Fight.View?.DoomView) ? EncounterScreen.Figure("DoomTitle_Misc", Fight.View.DoomView.DoomText) : null);
            Read("fight.heat", "Heat", KeyCode.H, () => Visible(Fight.View?.HeatView) ? EncounterScreen.Figure("HeatTitle_Misc", Fight.View.HeatView._heatValueText) : null);
            Read("fight.objective", "Objective", KeyCode.O, EncounterScreen.Objective);
            Read("fight.mech", "Mech", KeyCode.M, MechLine);
            InputManager.Register("fight.mech.jump", "Go to the mech", InputCategory.Game, () => JumpTo(Mech()))
                .AddBinding(new KeyboardBinding(KeyCode.M, shift: true));
            InputManager.Register("fight.cat.next", "Next category", InputCategory.Game, () => Category(1))
                .AddBinding(new KeyboardBinding(KeyCode.RightBracket));
            InputManager.Register("fight.cat.prev", "Previous category", InputCategory.Game, () => Category(-1))
                .AddBinding(new KeyboardBinding(KeyCode.LeftBracket));
            InputManager.Register("fight.item.next", "Next in category", InputCategory.Game, () => Item(1))
                .AddBinding(new KeyboardBinding(KeyCode.Period)).Repeating();
            InputManager.Register("fight.item.prev", "Previous in category", InputCategory.Game, () => Item(-1))
                .AddBinding(new KeyboardBinding(KeyCode.Comma)).Repeating();
            InputManager.Register("fight.jumpback", "Jump back", InputCategory.Game, JumpBack)
                .AddBinding(new KeyboardBinding(KeyCode.Backspace, alt: true));
            InputManager.Register("fight.endturn", "End turn", InputCategory.Game, EndTurn)
                .AddBinding(new KeyboardBinding(KeyCode.E));
        }

        // A read key: speaks its line, interrupting; nothing when the line is null.
        private static void Read(string key, string label, KeyCode code, Func<string> line)
        {
            InputManager.Register(key, label, InputCategory.Game, () =>
            {
                string text = null;
                try { text = line(); }
                catch (Exception e) { CoreLog.Warning("FightKeys: " + key + " failed: " + e.Message); }
                if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt: true);
            }).AddBinding(new KeyboardBinding(code));
        }

        private static bool Visible(Component view)
        {
            if (view == null || !view.gameObject.activeInHierarchy) return false;
            var group = view.GetComponent<CanvasGroup>();
            return group == null || group.alpha > 0.05f;
        }

        private static string Coords()
        {
            var at = EncounterScreen.FocusedTile();
            return at.HasValue ? FightBoard.CoordText(at.Value.X, at.Value.Y) : null;
        }

        private static (int X, int Y)? Mech()
        {
            var board = FightBoard.Read();
            if (board == null) return null;
            for (int y = 0; y < board.Rows; y++)
                for (int x = 0; x < board.Columns; x++)
                    if (board.At(x, y)?.TryCast<PlayerEntityModel>() != null) return (x, y);
            return null;
        }

        // "mech, 3, 2[, shielded...]".
        private static string MechLine()
        {
            var at = Mech();
            var board = FightBoard.Read();
            if (at == null || board == null) return null;
            return board.Describe(board.At(at.Value.X, at.Value.Y)) + ", " + FightBoard.CoordText(at.Value.X, at.Value.Y);
        }

        // ---- jumps ----

        private static (int X, int Y)? _back;

        /// <summary>Move the board cursor to the tile (spoken as a move), remembering where it was.</summary>
        public static void JumpTo((int X, int Y)? tile)
        {
            if (tile == null) return;
            var from = EncounterScreen.FocusedTile();
            if (from.HasValue && from.Value.Equals(tile.Value)) { Navigation.AnnounceCurrent(); return; }
            if (Navigation.Active?.MoveTo(EncounterScreen.TileId(tile.Value.X, tile.Value.Y)) == true)
                _back = from;
        }

        private static void JumpBack()
        {
            if (_back == null) return;
            var target = _back;
            JumpTo(target);
        }

        // ---- categories ----

        private static readonly string[] Categories = { "invaders", "mech", "items", "pickups", "danger" };
        private static int _category = -1;
        private static int _item = -1;

        // The tiles of a category, in reading order: top row first, left to right.
        private static List<(int X, int Y)> Members(FightBoard board, string category)
        {
            var tiles = new List<(int, int)>();
            for (int y = board.Rows - 1; y >= 0; y--)
                for (int x = 0; x < board.Columns; x++)
                {
                    var e = board.At(x, y);
                    bool member;
                    switch (category)
                    {
                        case "invaders": member = e?.TryCast<EnemyEntityModel>() != null; break;
                        case "mech": member = e?.TryCast<PlayerEntityModel>() != null; break;
                        case "items": member = e?.TryCast<ItemEntityModel>() != null; break;
                        case "pickups": member = board.Pickup(x, y) != null; break;
                        default: member = board.Danger(x, y) != null; break;
                    }
                    if (member) tiles.Add((x, y));
                }
            return tiles;
        }

        // [ and ]: the next category with something in it, wrapping; "Invaders, 2".
        private static void Category(int step)
        {
            var board = FightBoard.Read();
            if (board == null) return;
            int n = Categories.Length;
            int start = _category >= 0 ? _category : (step > 0 ? -1 : n);
            for (int i = 1; i <= n; i++)
            {
                int c = ((start + step * i) % n + n) % n;
                var members = Members(board, Categories[c]);
                if (members.Count == 0) continue;
                _category = c;
                _item = -1;
                Speech.Say(Strings.FightCategoryLine(Strings.FightCategory(Categories[c]), members.Count), interrupt: true);
                return;
            }
            Speech.Say(Strings.FightNothing, interrupt: true);
        }

        // Comma and period: the previous or next member of the category, wrapping, moving the cursor.
        private static void Item(int step)
        {
            var board = FightBoard.Read();
            if (board == null) return;
            if (_category < 0 || Members(board, Categories[_category]).Count == 0)
            {
                _category = -1;
                for (int c = 0; c < Categories.Length && _category < 0; c++)
                    if (Members(board, Categories[c]).Count > 0) _category = c;
                _item = -1;
                if (_category < 0) { Speech.Say(Strings.FightNothing, interrupt: true); return; }
            }
            var members = Members(board, Categories[_category]);
            _item = _item < 0 ? (step > 0 ? 0 : members.Count - 1) : ((_item + step) % members.Count + members.Count) % members.Count;
            JumpTo(members[_item]);
        }

        // ---- end turn ----

        private static void EndTurn()
        {
            var end = Fight.View?.EndTurnView;
            if (end == null || !end.gameObject.activeInHierarchy) return;
            if (!end.ButtonEnabled)
            {
                Speech.Say(Strings.FightLabelled(GameLoc.Get("EndTurnTitle_Misc") ?? "End Turn", Strings.StateDisabled), interrupt: true);
                return;
            }
            Fight.Click(end);
        }
    }
}
