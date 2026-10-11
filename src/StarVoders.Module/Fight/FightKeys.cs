using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Fight;
using StarVoders.Core.Input;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Input;
using StarVoders.Module.Interop;
using StarVoders.Module.Screens;
using StarVoders.Module.UI;
using UnityEngine;

namespace StarVoders.Module.Fight
{
    /// <summary>
    /// The fight's keys (the Game category, live while the fight screen is): digits play the nth
    /// card; read keys speak one thing (C coordinates, T turn, Ctrl+D Doom, Ctrl+H Heat, Ctrl+O
    /// objective, Ctrl+M the mech, Ctrl+B the bombs) and are silent where there is nothing to read; Shift+M goes to the mech; [ and ] cycle
    /// the board's categories (while choosing a target, the cells that take it; invaders, mech,
    /// bombs and items, pickups, danger zones; only those with something in them), comma and period step through the current one in reading order
    /// (top row first), moving the cursor; danger zones step by region, each attacker's tiles merged
    /// as its targets are announced ("5, 1 to 5, 5, targeted by Shooter 2", the cursor to its first
    /// tile); Alt+Backspace jumps back to where a jump left from; E ends
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
            Read("fight.doom", "Doom", KeyCode.D, ctrl: true, line: () => Visible(Fight.View?.DoomView) ? Fight.Figure("Doom") : null);
            Read("fight.heat", "Heat", KeyCode.H, ctrl: true, line: () => Visible(Fight.View?.HeatView) ? Fight.Figure("Heat") : null);
            Read("fight.objective", "Objective", KeyCode.O, ctrl: true, line: EncounterScreen.Objective);
            Read("fight.mech", "Mech", KeyCode.M, ctrl: true, line: MechLine);
            Read("fight.bombs", "Bombs", KeyCode.B, ctrl: true, line: Fight.Bombs);
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
        private static void Read(string key, string label, KeyCode code, Func<string> line, bool ctrl = false)
        {
            InputManager.Register(key, label, InputCategory.Game, () =>
            {
                string text = null;
                try { text = line(); }
                catch (Exception e) { CoreLog.Warning("FightKeys: " + key + " failed: " + e.Message); }
                if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt: true);
            }).AddBinding(new KeyboardBinding(code, ctrl: ctrl));
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

        /// <summary>Move the board cursor to the tile (spoken as a move, unless <paramref name="speak"/>
        /// is false: the caller said something else), remembering where it was.</summary>
        public static void JumpTo((int X, int Y)? tile, bool speak = true)
        {
            if (tile == null) return;
            var from = EncounterScreen.FocusedTile();
            if (from.HasValue && from.Value.Equals(tile.Value)) { if (speak) Navigation.AnnounceCurrent(); return; }
            if (Navigation.Active?.MoveTo(EncounterScreen.TileId(tile.Value.X, tile.Value.Y), speak) == true)
                _back = from;
        }

        private static void JumpBack()
        {
            if (_back == null) return;
            var target = _back;
            JumpTo(target);
        }

        // ---- categories ----

        private static readonly string[] Categories = { "targets", "invaders", "mech", "items", "pickups", "danger" };
        private static int _category = -1;
        private static int _item = -1;

        // A category member: the tile the cursor goes to, and for a danger region its line (null:
        // the move reads the tile).
        private struct Member
        {
            public (int X, int Y) Tile;
            public string Line;
        }

        // The members of a category, in reading order: top row first, left to right. Danger zones
        // are regions: per attacker (in the order their tiles first appear), its tiles as shapes.
        private static List<Member> Members(FightBoard board, string category)
        {
            if (category == "danger") return DangerRegions(board);
            var tiles = new List<Member>();
            for (int y = board.Rows - 1; y >= 0; y--)
                for (int x = 0; x < board.Columns; x++)
                {
                    var e = board.At(x, y);
                    bool member;
                    switch (category)
                    {
                        case "targets": member = EncounterScreen.IsTarget(x, y); break;
                        case "invaders": member = e?.TryCast<EnemyEntityModel>() != null; break;
                        case "mech": member = e?.TryCast<PlayerEntityModel>() != null; break;
                        case "items": member = e?.TryCast<ItemEntityModel>() != null; break;
                        default: member = board.Pickup(x, y) != null; break;
                    }
                    if (member) tiles.Add(new Member { Tile = (x, y) });
                }
            return tiles;
        }

        private static List<Member> DangerRegions(FightBoard board)
        {
            var byWho = new List<KeyValuePair<string, List<(int X, int Y)>>>();
            for (int y = board.Rows - 1; y >= 0; y--)
                for (int x = 0; x < board.Columns; x++)
                    foreach (var who in board.Attackers(x, y))
                    {
                        var tiles = byWho.Find(kv => kv.Key == who).Value;
                        if (tiles == null) { tiles = new List<(int X, int Y)>(); byWho.Add(new KeyValuePair<string, List<(int X, int Y)>>(who, tiles)); }
                        tiles.Add((x + 1, y + 1));
                    }
            // The same region from several attackers (two Shooters on one column) is one member.
            var regions = new List<(TileRanges.Range Range, List<string> Who)>();
            foreach (var kv in byWho)
                foreach (var range in TileRanges.Ranges(kv.Value, board.Columns, board.Rows))
                {
                    int same = regions.FindIndex(r => r.Range.Text == range.Text);
                    if (same >= 0) regions[same].Who.Add(kv.Key);
                    else regions.Add((range, new List<string> { kv.Key }));
                }
            string label = GameLoc.Get("DangerZoneText_Misc") ?? "Danger Zone";
            var members = new List<Member>();
            foreach (var (range, who) in regions)
            {
                who.Remove(label); // the game's label stands in only when nobody is named
                members.Add(new Member
                {
                    Tile = (range.Start.X - 1, range.Start.Y - 1),
                    Line = range.Text + ", " + (who.Count > 0 ? Strings.FightTargeted(string.Join(", ", who)) : label),
                });
            }
            return members;
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
            var m = members[_item];
            if (m.Line == null) { JumpTo(m.Tile); return; }
            Speech.Say(m.Line, interrupt: true);
            JumpTo(m.Tile, speak: false);
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
