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
    /// card, Shift with them reads it as the hand buffer does; I J K L move as the arrows, U O N M
    /// diagonally on the board; read keys speak one thing (C coordinates, T turn, Ctrl+D Doom and Ctrl+S
    /// Stars (Run keys: live anywhere in a run), Ctrl+H the energy (Heat, Power and Battery, or Mana and its gain), Ctrl+O
    /// objective, Ctrl+M the mech, Ctrl+B the bombs and a Keeper's Summons) and are silent where there is nothing to read; Shift+M goes to the mech, Alt+H to the hand (under the grid; Home and End there
    /// are its first and last card); [ and ] cycle
    /// the board's categories (while choosing a target, the cells that take it; invaders, mech,
    /// Summons (a Keeper's puppets), bombs and items, pickups, danger zones; only those with something in them), comma and period step through the current one in reading order
    /// (top row first), moving the cursor; danger zones step by region, each attacker's tiles merged
    /// as its targets are announced ("5, 1 to 5, 5, targeted by Shooter 2", the cursor to its first
    /// tile); Alt+Backspace jumps back to where a jump left from; Shift+arrows skip cells that read
    /// the same; Home and End go to the row's ends, Page Up and Page Down to the column's.
    /// The game's own fight keys are its actions' keys, read from the game (<see cref="GameKeyBinding"/>,
    /// so its rebinding carries over): the card digits, E ends the turn, A and S open the draw and
    /// discard piles, R rewinds, F is Backspace's details (the game's more info).
    /// </summary>
    internal static class FightKeys
    {
        // The fight's action asset (the scene's PlayerInput): what the game's own fight keys listen to.
        private static StarVaders.InputHandler Handler() => Fight.Controller?.EncounterInputHandler;

        private static GameKeyBinding GameKey(string action, KeyCode defaultKey, bool shift = false) =>
            new GameKeyBinding(action, defaultKey, Handler, shift);

        public static void Register()
        {
            for (int n = 1; n <= 10; n++)
            {
                int card = n, digit = n % 10;
                InputManager.Register("fight.card" + card, "Play card " + card, InputCategory.Game, () => EncounterScreen.PlayCard(card))
                    .AddBinding(GameKey("SelectCard" + digit, KeyCode.Alpha0 + digit));
                InputManager.Register("fight.cardinfo" + card, "Read card " + card, InputCategory.Game, () => ReadCard(card))
                    .AddBinding(GameKey("SelectCard" + digit, KeyCode.Alpha0 + digit, shift: true));
            }
            InputManager.Register("fight.skip.up", "Skip up", InputCategory.Game, () => EncounterScreen.Skip(0, 1))
                .AddBinding(new KeyboardBinding(KeyCode.UpArrow, shift: true)).Repeating();
            InputManager.Register("fight.skip.down", "Skip down", InputCategory.Game, () => EncounterScreen.Skip(0, -1))
                .AddBinding(new KeyboardBinding(KeyCode.DownArrow, shift: true)).Repeating();
            InputManager.Register("fight.skip.left", "Skip left", InputCategory.Game, () => EncounterScreen.Skip(-1, 0))
                .AddBinding(new KeyboardBinding(KeyCode.LeftArrow, shift: true)).Repeating();
            InputManager.Register("fight.skip.right", "Skip right", InputCategory.Game, () => EncounterScreen.Skip(1, 0))
                .AddBinding(new KeyboardBinding(KeyCode.RightArrow, shift: true)).Repeating();
            InputManager.Register("fight.edge.first", "First column", InputCategory.Game, () => Edge(UiActions.Home, (b, x, y) => (0, y)))
                .AddBinding(new KeyboardBinding(KeyCode.Home));
            InputManager.Register("fight.edge.last", "Last column", InputCategory.Game, () => Edge(UiActions.End, (b, x, y) => (b.Columns - 1, y)))
                .AddBinding(new KeyboardBinding(KeyCode.End));
            InputManager.Register("fight.edge.top", "Top row", InputCategory.Game, () => Edge(null, (b, x, y) => (x, b.Rows - 1)))
                .AddBinding(new KeyboardBinding(KeyCode.PageUp));
            InputManager.Register("fight.edge.bottom", "Bottom row", InputCategory.Game, () => Edge(null, (b, x, y) => (x, 0)))
                .AddBinding(new KeyboardBinding(KeyCode.PageDown));
            // Letter moves, the right hand's cluster: I J K L are the arrows, U O N M the diagonals.
            Arrow("fight.dir.up", "Move up", KeyCode.I, UiActions.Up);
            Arrow("fight.dir.down", "Move down", KeyCode.K, UiActions.Down);
            Arrow("fight.dir.left", "Move left", KeyCode.J, UiActions.Left);
            Arrow("fight.dir.right", "Move right", KeyCode.L, UiActions.Right);
            Diagonal("fight.dir.upleft", "Move up left", KeyCode.U, -1, 1);
            Diagonal("fight.dir.upright", "Move up right", KeyCode.O, 1, 1);
            Diagonal("fight.dir.downleft", "Move down left", KeyCode.N, -1, -1);
            Diagonal("fight.dir.downright", "Move down right", KeyCode.M, 1, -1);

            Read("fight.coords", "Coordinates", KeyCode.C, Coords);
            Read("fight.turn", "Turn", KeyCode.T, EncounterScreen.TurnText);
            // Doom reads the run, so it is live anywhere in one (RunKeys), not only in the fight.
            Read("fight.doom", "Doom", KeyCode.D, ctrl: true, line: RunKeys.DoomLine, category: InputCategory.Run);
            Read("run.stars", "Stars", KeyCode.S, ctrl: true, line: RunKeys.StarsLine, category: InputCategory.Run);
            Read("fight.heat", "Energy", KeyCode.H, ctrl: true, line: () => { var lines = Fight.Resources(); return lines.Count > 0 ? string.Join(", ", lines) : null; });
            Read("fight.objective", "Objective", KeyCode.O, ctrl: true, line: EncounterScreen.Objective);
            Read("fight.mech", "Mech", KeyCode.M, ctrl: true, line: MechLine);
            Read("fight.bombs", "Bombs and Summons", KeyCode.B, ctrl: true, line: () =>
            {
                string bombs = Fight.Bombs(), summons = Fight.Summons();
                return bombs != null && summons != null ? bombs + ", " + summons : bombs ?? summons;
            });
            InputManager.Register("fight.mech.jump", "Go to the mech", InputCategory.Game, () => JumpTo(Mech()))
                .AddBinding(new KeyboardBinding(KeyCode.M, shift: true));
            InputManager.Register("fight.hand", "Go to the hand", InputCategory.Game, JumpToHand)
                .AddBinding(new KeyboardBinding(KeyCode.H, alt: true));
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
                .AddBinding(GameKey("EndTurn", KeyCode.E));
            InputManager.Register("fight.deck", "Draw pile", InputCategory.Game, () => Pile(Fight.View?.CardPlayView?.DrawPileView))
                .AddBinding(GameKey("Deck", KeyCode.A));
            InputManager.Register("fight.discard", "Discard pile", InputCategory.Game, () => Pile(Fight.View?.CardPlayView?.DiscardView))
                .AddBinding(GameKey("Discard", KeyCode.S));
            InputManager.Register("fight.rewind", "Rewind", InputCategory.Game, Rewind)
                .AddBinding(GameKey("Reroll", KeyCode.R));
            InputManager.Register("fight.moreinfo", "More info", InputCategory.Game, () =>
            {
                var details = InputManager.Find(UiActions.Secondary);
                if (details != null) Navigation.DispatchJustPressed(details);
            }).AddBinding(GameKey("MoreInfo", KeyCode.F));
        }

        // A read key: speaks its line, interrupting; nothing when the line is null.
        private static void Read(string key, string label, KeyCode code, Func<string> line, bool ctrl = false,
            InputCategory category = InputCategory.Game)
        {
            InputManager.Register(key, label, category, () =>
            {
                string text = null;
                try { text = line(); }
                catch (Exception e) { CoreLog.Warning("FightKeys: " + key + " failed: " + e.Message); }
                if (!string.IsNullOrEmpty(text)) Speech.Say(text, interrupt: true);
            }).AddBinding(new KeyboardBinding(code, ctrl: ctrl));
        }

        // A letter standing in for an arrow: the arrow's own press, wherever focus is in the fight.
        private static void Arrow(string key, string label, KeyCode code, string uiKey)
        {
            InputManager.Register(key, label, InputCategory.Game, () =>
            {
                var arrow = InputManager.Find(uiKey);
                if (arrow != null) Navigation.DispatchJustPressed(arrow);
            }).AddBinding(new KeyboardBinding(code)).Repeating();
        }

        // A diagonal step on the board (dy up); silent off the board or at its edge, as an arrow there.
        private static void Diagonal(string key, string label, KeyCode code, int dx, int dy)
        {
            InputManager.Register(key, label, InputCategory.Game, () =>
            {
                var at = EncounterScreen.FocusedTile();
                var board = at.HasValue ? FightBoard.Read() : null;
                if (board == null) return;
                int x = at.Value.X + dx, y = at.Value.Y + dy;
                if (x < 0 || y < 0 || x >= board.Columns || y >= board.Rows) return;
                Navigation.Active?.MoveTo(EncounterScreen.TileId(x, y));
            }).AddBinding(new KeyboardBinding(code)).Repeating();
        }

        // Shift plus a card key: the card that key plays, as the hand buffer reads it; silent past the hand.
        private static void ReadCard(int n)
        {
            var hand = Fight.Hand();
            if (n < 1 || n > hand.Count) return;
            string line = null;
            try { line = FightBuffers.HandLine(hand[n - 1]); }
            catch (Exception e) { CoreLog.Warning("FightKeys: reading card " + n + " failed: " + e.Message); }
            if (!string.IsNullOrEmpty(line)) Speech.Say(line, interrupt: true);
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

        /// <summary>The mech's 0-based tile, or null.</summary>
        internal static (int X, int Y)? MechTile() => Mech();

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
        internal static string MechLine()
        {
            var at = Mech();
            var board = FightBoard.Read();
            if (at == null || board == null) return null;
            return board.Describe(board.At(at.Value.X, at.Value.Y)) + ", " + FightBoard.CoordText(at.Value.X, at.Value.Y);
        }

        // Home and End: the row's first and last column; Page Up and Page Down: the column's top and
        // bottom row; silent when already there. Off the board, Home and End are the navigator's own (these keys shadow them).
        private static void Edge(string uiKey, Func<FightBoard, int, int, (int X, int Y)> to)
        {
            // On the hand, Home and End are its first and last card.
            if (EncounterScreen.FocusedOnHand())
            {
                var hand = Fight.Hand();
                if (uiKey == null || hand.Count == 0) return;
                var card = EncounterScreen.CardId(uiKey == UiActions.Home ? hand[0] : hand[hand.Count - 1]);
                if (!Equals((Navigation.Active as GraphNavigator)?.FocusedNode?.Id, card)) Navigation.Active?.MoveTo(card);
                return;
            }
            var at = EncounterScreen.FocusedTile();
            var board = at.HasValue ? FightBoard.Read() : null;
            if (board == null)
            {
                var ui = uiKey != null ? InputManager.Find(uiKey) : null;
                if (ui != null) Navigation.DispatchJustPressed(ui);
                return;
            }
            var tile = to(board, at.Value.X, at.Value.Y);
            if (tile.Equals(at.Value)) return; // already there: nothing to say
            Navigation.Active?.MoveTo(EncounterScreen.TileId(tile.X, tile.Y));
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

        // Alt+H: the first card of the hand, under the grid; Alt+Backspace returns to the cell.
        private static void JumpToHand()
        {
            var hand = Fight.Hand();
            if (hand.Count == 0) return;
            var from = EncounterScreen.FocusedTile();
            if (Navigation.Active?.MoveTo(EncounterScreen.CardId(hand[0])) == true && from.HasValue) _back = from;
        }

        private static void JumpBack()
        {
            if (_back == null) return;
            var target = _back;
            JumpTo(target);
        }

        // ---- categories ----

        private static readonly string[] Categories = { "targets", "invaders", "mech", "summons", "items", "pickups", "danger" };
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
                        case "summons": member = board.IsSummon(x, y); break;
                        case "items": member = e?.TryCast<ItemEntityModel>() != null && !board.IsSummon(x, y); break;
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

        // ---- the rack's other buttons, as Enter on them ----

        // A and S: the pile's click, the card viewer (the game's keys hover the pile and click too).
        private static void Pile(AViewItem pile)
        {
            if (!ViewNodes.IsShown(pile)) return;
            Fight.Click(pile);
        }

        private static void Rewind()
        {
            var rewind = Fight.View?.ChronoButtonView;
            if (!ViewNodes.IsShown(rewind)) return;
            if (!rewind.ButtonEnabled)
            {
                Speech.Say(Strings.FightLabelled(GameLoc.Get("RewindButtonTitle_Misc") ?? "Rewind", Strings.StateDisabled), interrupt: true);
                return;
            }
            Fight.Click(rewind);
        }
    }
}
