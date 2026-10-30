using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Fight;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Strings;
using StarVoders.Core.UI;
using StarVoders.Module.Fight;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;
using TMPro;
using FightState = StarVoders.Module.Fight.Fight;
using Screen = StarVoders.Core.Screens.Screen;

namespace StarVoders.Module.Screens
{
    /// <summary>
    /// The fight (<c>EncounterScene</c>). Stops: the board with the hand as a strip under its bottom
    /// row (each card "name, cost, description", Enter playing it as the game's click does,
    /// unplayable cards disabled with the game's reason; card n under column n); the spells (a Keeper's, when there are any: Enter casts); the rack (End Turn, the Transformer's
    /// Draw on the Power panel, Rewind with the Chrono Tokens left, the draw and discard piles with their
    /// counts); the status (turn x/y, Doom, Heat, Power and its Battery, Mana and its gain a turn, where the mech has them, Stars,
    /// the objective, the artifacts). Labels are the game's own strings (its <c>*_Misc</c> keys).
    /// </summary>
    public sealed class EncounterScreen : Screen
    {
        public override string Key => "fight";
        public override bool AllowsTypeahead => false;
        public override object InitialFocusStop => BoardStop;

        private static readonly InputCategory[] Categories = { InputCategory.Game, InputCategory.Run, InputCategory.UI };
        public override IReadOnlyList<InputCategory> InputCategories => Categories;

        public override IEnumerable<ElementAction> GetActions()
        {
            // Escape cancels a selection the game lets go of; otherwise it pauses (Navigation's
            // fallback, as on every run screen).
            if (FightState.Cancellable)
                yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => FightState.Cancel());
        }

        public override bool IsActive()
        {
            var c = FightState.Controller;
            return c != null && !c.EncounterEnded;
        }

        public override string ScreenName => Objective() ?? (Overseer.Model() != null ? Overseer.Name() : null);

        // The objective and the landing wait while the fight still moves or a tip is on its way: the
        // tutorial's tips come one after another, each covering the fight, and the fight showing
        // between two of them is no arrival.
        public override bool HoldsArrival => !FightEvents.Settled || Tips.Coming;

        public override void Build(GraphBuilder b)
        {
            var c = FightState.Controller;
            if (c == null) return;
            var view = c.EncounterView;

            // What the board cannot answer (cards to choose, a Jab's "Draw 1 Card" panel) is a list of
            // those targets; the grid's (tiles, the play area, arrows) are answered on the board itself.
            // A selection can take both: a Jab takes an adjacent entity or the draw.
            var selection = FightState.Selecting ? FightState.ReadTargets() : null;
            if (selection != null && selection.Others.Count > 0 && !selection.InHand)
            {
                var board = FightBoard.Read();
                b.BeginStop(TargetsStop);
                b.PushContext(FightState.Prompt());
                foreach (var target in selection.Others)
                    b.AddItem(ControlId.Structural("fight:target:" + target.GetInstanceID()), Target(target, board));
                b.PopContext();
            }

            BuildBoard(b, selection);

            // A Keeper's spells, in the Mana panel's slots (cast by clicking): their own stop, the
            // slots still empty after them (the panel draws them as empty frames: 3, 5 with Grimoire),
            // so the positions count the slots.
            var spells = new List<SpellView>();
            int slots = 0;
            if (view.ManaView != null && FightState.Shown(view.ManaView) && view.ManaView.SpellViews != null)
            {
                foreach (var spell in view.ManaView.SpellViews)
                    if (ViewNodes.IsShown(spell) && spell.ArtifactModel != null) spells.Add(spell);
                try { slots = DataManager.PlayerData.spellSlots; }
                catch (Exception e) { CoreLog.Warning("Encounter: reading the spell slots failed: " + e.Message); }
            }
            if (spells.Count > 0 || slots > 0)
            {
                b.BeginStop("fight:spells");
                b.PushContext(null);
                foreach (var spell in spells)
                    b.AddItem(ControlId.Structural("fight:spell:" + spell.GetInstanceID()), Spell(spell));
                for (int i = spells.Count; i < slots; i++)
                    b.AddItem(ControlId.Structural("fight:spellslot:" + i), CardNodes.EmptySpellSlot());
                b.PopContext();
            }

            // The rack and the status read short: no positions, their descriptions in the ui buffer
            // (and on Backspace) rather than spoken on every visit.
            b.BeginStop("fight:rack");
            b.PushContext(null, positions: false);
            // Skip Repeats, while a repeating card's selection offers it: the only way out of a
            // repeat with nothing left to choose.
            var skip = FightState.SkipRepeatsPanel;
            if (skip != null)
                b.AddItem(SkipRepeatsId, GameNodes.Button(() => GameText.Of(skip.Text) ?? ViewNodes.Label(skip),
                    FightState.SkipRepeats, () => FightState.CanSkipRepeats));
            var endTurn = view.EndTurnView;
            if (ViewNodes.IsShown(endTurn))
                b.AddItem(ControlId.Structural("fight:endturn"), Button(endTurn, () => GameLoc.Get("EndTurnTitle_Misc"),
                    () => endTurn.ButtonEnabled, null, () => GameLoc.Get("EndTurnDesc_Misc")));
            // The Transformer's Draw button on the Power panel: spend 1 Power to draw, once a turn.
            var powerDraw = view.PowerView != null ? view.PowerView.DrawButton : null;
            if (ViewNodes.IsShown(powerDraw) && FightState.Shown(view.PowerView))
                b.AddItem(ControlId.Structural("fight:powerdraw"), Button(powerDraw, () => GameLoc.Get("DrawButton_Misc"),
                    () => !view.PowerView.DrawButtonLock && powerDraw.ButtonEnabled, null, () => GameLoc.Get("TransformerDrawButtonDesc_Misc")));
            var rewind = view.ChronoButtonView;
            if (ViewNodes.IsShown(rewind))
                b.AddItem(ControlId.Structural("fight:rewind"), Button(rewind, () => GameLoc.Get("RewindButtonTitle_Misc"),
                    () => rewind.ButtonEnabled, () => Tokens(rewind), () => GameLoc.Get("RewindButtonDesc_Misc")));
            var play = view.CardPlayView;
            var draw = play != null ? play.DrawPileView : null;
            if (ViewNodes.IsShown(draw))
                b.AddItem(ControlId.Structural("fight:draw"), Button(draw, () => GameLoc.Get("DrawPileTitle_Misc"),
                    null, () => PileCount(Pile.Draw), () => GameLoc.Get("DrawPileDesc_Misc")));
            var discard = play != null ? play.DiscardView : null;
            if (ViewNodes.IsShown(discard))
                b.AddItem(ControlId.Structural("fight:discard"), Button(discard, () => GameLoc.Get("DiscardPileTitle_Misc"),
                    null, () => PileCount(Pile.Discard), () => GameLoc.Get("DiscardPileDesc_Misc")));
            b.PopContext();

            b.BeginStop("fight:status");
            b.PushContext(null, positions: false);
            b.AddItem(ControlId.Structural("fight:turn"), Stat(TurnText, null));
            if (Overseer.Model() != null) b.AddItem(ControlId.Structural("fight:overseer"), Stat(Overseer.Line, null));
            foreach (var counter in BossCounters.Read())
            {
                string key = counter.Key;
                Func<BossCounters.Counter> live = () => BossCounters.Read().Find(c => c.Key == key);
                b.AddItem(ControlId.Structural("fight:counter:" + key), Stat(() => live()?.Line, () => live()?.Description));
            }
            var doom = view.DoomView;
            if (ViewNodes.IsShown(doom))
                b.AddItem(ControlId.Structural("fight:doom"), Stat(() => FightState.Figure("Doom"), () => GameLoc.Get("DoomPanelDesc_Misc")));
            var heat = view.HeatView;
            if (ViewNodes.IsShown(heat) && Visible(heat))
                b.AddItem(ControlId.Structural("fight:heat"), Stat(() => FightState.Figure("Heat"), () => GameLoc.Get("HeatPanelDesc_Misc")));
            var power = view.PowerView;
            if (ViewNodes.IsShown(power) && Visible(power))
            {
                b.AddItem(ControlId.Structural("fight:power"), Stat(() => FightState.Figure("Power"), () => GameLoc.Get("PowerBankDesc_Misc")));
                if (FightState.Battery() != null)
                    b.AddItem(ControlId.Structural("fight:battery"), Stat(FightState.Battery, () => GameLoc.Get("BatteryPanelDesc_Misc")));
            }
            var mana = view.ManaView;
            if (ViewNodes.IsShown(mana) && Visible(mana))
                b.AddItem(ControlId.Structural("fight:mana"), Stat(() =>
                {
                    string figure = FightState.Figure("Mana"), regen = FightState.ManaRegen();
                    return figure != null && regen != null ? figure + ", " + regen : figure;
                }, () => GameLoc.Get("ManaPanelDesc_Misc")));
            if (Stars() != null)
                b.AddItem(ControlId.Structural("fight:stars"), Stat(Stars, () => GameLoc.Get("StarPanelDesc_Misc")));
            if (Objective() != null) b.AddItem(ControlId.Structural("fight:objective"), Stat(Objective, null));
            var artifacts = view.ArtifactViewDict;
            if (artifacts != null && artifacts.Count > 0)
            {
                int i = 0;
                foreach (var kv in artifacts)
                {
                    var artifact = kv.Value;
                    if (artifact == null) continue;
                    b.AddItem(ControlId.Structural("fight:artifact" + i++), Artifact(artifact));
                }
            }
            b.PopContext();
        }

        // ---- the board ----

        private const string BoardStop = "fight:board";
        private static string _lastZone;
        private static (int X, int Y)? _lastTile; // the cell last focused, for a target choice begun from the hand

        public static ControlId TileId(int x, int y) => ControlId.Structural("fight:tile:" + x + "," + y);

        /// <summary>The 0-based tile the focus is on, or null off the board.</summary>
        public static (int X, int Y)? FocusedTile()
        {
            var id = (Navigation.Active as GraphNavigator)?.FocusedNode?.Id?.StructuralKey as string;
            if (id == null || !id.StartsWith("fight:tile:")) return null;
            var parts = id.Substring("fight:tile:".Length).Split(',');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        public static ControlId CardId(CardView card) => ControlId.Structural("fight:card:" + card.GetInstanceID());

        /// <summary>Whether the focus is on a card of the hand.</summary>
        public static bool FocusedOnHand()
        {
            var id = (Navigation.Active as GraphNavigator)?.FocusedNode?.Id?.StructuralKey as string;
            return id != null && id.StartsWith("fight:card:");
        }

        // The grid, top row first so Up goes up the board; the rows share a key, so Up and Down keep
        // the column. Focus first lands on the mech. Under the bottom row, in the same stop, the hand
        // as one strip in hand order: card n under column n (a strict row: Down from a column with no
        // card under it, or Up from a card past the last column, stays put). While a target is chosen
        // on the board the hand is keyed apart, so no arrow leaves the cells for a card (Enter there
        // would click it); Alt+H still reaches it.
        private static void BuildBoard(GraphBuilder b, FightState.TargetSet selection)
        {
            var board = FightBoard.Read();
            var grid = FightState.View?.GridView;
            if (board == null || grid == null) return;
            b.BeginStop(BoardStop);
            for (int y = board.Rows - 1; y >= 0; y--)
            {
                b.StartRow(BoardStop);
                for (int x = 0; x < board.Columns; x++)
                    b.AddItem(TileId(x, y), Cell(x, y));
                b.EndRow();
            }
            var hand = FightState.Hand();
            if (hand.Count == 0) return;
            bool apart = selection != null && selection.OnBoard;
            b.PushContext(null);
            b.StartRow(apart ? "fight:hand" : BoardStop, strictColumns: true);
            foreach (var card in hand)
                b.AddItem(CardId(card), Card(card));
            b.EndRow();
            b.PopContext();
        }

        private static TileView Tile(int x, int y)
        {
            var dict = FightState.View?.GridView?.TileViewDict;
            if (dict == null) return null;
            var c = FightBoard.MakeCoord(x, y);
            return dict.ContainsKey(c) ? dict[c] : null;
        }

        private static NodeVtable Cell(int x, int y)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                SpeaksOwnPosition = true,
                // One part: the target and strike marks, what is there, the coordinates, then the
                // zone when crossed into ("mech, 3 2", "Hole, 4 2").
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => CellText(x, y)) },
                SearchText = () => FightBoard.CoordText(x, y),
                IsSelected = () => IsMech(x, y),
                OnFocus = () => { var tile = Tile(x, y); if (tile != null) FightState.Hover(tile); },
                OnActivate = () => ActivateCell(x, y),
                Details = () => CellDetails(x, y),
                OnSecondary = () => SayDetails(CellDetails(x, y)),
            };
        }

        private static string CellText(int x, int y)
        {
            var parts = new List<string>();
            if (IsTarget(x, y)) parts.Add(Strings.FightTarget);
            if (FightState.Strike(x, y)) parts.Add(Strings.FightStrike);
            string contents = CellContents(FightBoard.Read(), x, y);
            if (contents != null) parts.Add(contents);
            parts.Add(FightBoard.CoordText(x, y));
            string zone = ZoneCrossing(x, y);
            if (zone != null) parts.Add(zone);
            return string.Join(", ", parts);
        }

        // The landing: the player's tile, or with the player off the board (a Keeper with no Summon)
        // the bottom-left cell, above the first card.
        private static bool IsMech(int x, int y)
        {
            var board = FightBoard.Read();
            if (board == null) return false;
            var player = board.PlayerTile;
            return player.HasValue ? player.Value.X == x && player.Value.Y == y : x == 0 && y == 0;
        }

        /// <summary>Whether the running selection takes this cell: a valid tile, or the cell next to
        /// the arrows' origin in a valid direction. A play-area selection marks nothing (any cell
        /// plays the card).</summary>
        public static bool IsTarget(int x, int y)
        {
            if (!FightState.Selecting) return false;
            var set = FightState.ReadTargets();
            if (set == null) return false;
            if (set.HasTile(x, y)) return true;
            var o = set.Origin;
            return o != null && Math.Abs(x - o.Value.X) <= 1 && Math.Abs(y - o.Value.Y) <= 1 && set.ArrowToward(x, y) != null;
        }

        // Enter on a cell: outside a selection, the game's click on the tile; in one, the answer the
        // cell gives: the tile itself, the play area (from any cell), or the arrow toward the cell.
        private static void ActivateCell(int x, int y)
        {
            // A choice among the hand's cards runs: no cell answers it.
            if (HandChoice() != null) { Speech.Say(Strings.FightNotTarget, interrupt: true); return; }
            var tile = Tile(x, y);
            var set = FightState.Selecting ? FightState.ReadTargets() : null;
            if (set == null || !set.OnBoard)
            {
                if (tile != null) FightState.Click(tile);
                return;
            }
            if (set.HasTile(x, y)) { FightState.Click(tile); return; }
            if (set.PlayArea != null) { FightState.Click(set.PlayArea); return; }
            var arrow = set.ArrowToward(x, y);
            if (arrow != null) { FightState.Click(arrow); return; }
            Speech.Say(Strings.FightNotTarget, interrupt: true);
        }

        // The zone is named only on entering it, like a region (speechchem's zone crossing); leaving
        // the board forgets it, so coming back names it again. The middle rows have no name: entering
        // them says nothing, but still counts as leaving the zone, so going back names it.
        private static string ZoneCrossing(int x, int y)
        {
            var board = FightBoard.Read();
            if (board == null) return null;
            string zone = board.Zone(x, y);
            if (zone == _lastZone) return null;
            _lastZone = zone;
            return zone == "middle" ? null : Strings.FightZone(zone);
        }

        /// <summary>What a tile holds and what threatens it, before its coordinates: the entity with
        /// its state, the danger, the tile's status, a card pickup. Null for an empty, safe tile.</summary>
        internal static string CellContents(FightBoard board, int x, int y)
        {
            if (board == null) return null;
            var parts = new List<string>();
            var entity = board.At(x, y);
            if (entity != null) parts.Add(board.Describe(entity));
            string danger = board.Danger(x, y);
            if (danger != null) parts.Add(danger);
            var status = board.Status(x, y);
            if (status.HasValue) parts.Add(FightBoard.StatusName(status.Value));
            string pickup = board.Pickup(x, y);
            if (pickup != null) parts.Add(pickup);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static IEnumerable<string> CellDetails(int x, int y)
        {
            var board = FightBoard.Read();
            if (board == null) yield break;
            var entity = board.At(x, y);
            if (entity != null)
            {
                string d = FightBoard.Description(entity);
                yield return d != null ? Strings.FightLabelled(board.Name(entity), d) : board.Describe(entity);
                var traits = FightBoard.TraitLines(entity);
                foreach (var line in traits) yield return line;
                string channel = board.ChannelLine(entity, x, y);
                if (channel != null) yield return channel;
                // The haunted Summon is the Keeper's mech: the game's word on it.
                if (board.IsHaunted(entity)) { string haunt = GameLoc.Get("Haunt_InfoLine"); if (haunt != null) yield return haunt; }
                foreach (var line in FightState.EntityInfoLines(entity))
                    if (!traits.Contains(line)) yield return line;
            }
            string danger = board.Danger(x, y);
            if (danger != null) yield return danger;
            var status = board.Status(x, y);
            if (status.HasValue)
            {
                string d = FightBoard.StatusDescription(status.Value);
                yield return d != null ? Strings.FightLabelled(FightBoard.StatusName(status.Value), d) : FightBoard.StatusName(status.Value);
            }
            // A pickup reads as the card in hand does: "Pickup: Kunai, 1 Power, Attack, Tactic, ...",
            // then its more info.
            var (card, cardView) = board.PickupCard(x, y);
            if (card != null)
            {
                var parts = new List<string> { FightState.CardName(card) };
                foreach (var part in new[] { FightState.CardUpgrade(card), Cost(card, cardView), FightState.CardTraits(card), FightState.CardRarity(card),
                    FightState.CardDescription(card), FightState.CardUpgradeDescription(card) })
                    if (!string.IsNullOrEmpty(part)) parts.Add(part);
                yield return Strings.FightLabelled(FightBoard.PickupLabel, string.Join(", ", parts));
                string trap = FightBoard.BoobyTrapLine;
                if (trap != null) yield return trap;
                foreach (var line in FightState.CardInfoLines(card)) yield return line;
            }
            else if (board.HasPickup(x, y)) yield return FightBoard.PickupLabel;
        }

        /// <summary>Shift+arrow: past the cells that read the same as this one (contents and strike
        /// mark; zones do not count, so the spawn and doom rows are crossed), to the first that
        /// differs, or to the edge; at the edge, this cell again.</summary>
        public static void Skip(int dx, int dy)
        {
            var at = FocusedTile();
            var board = FightBoard.Read();
            if (at == null || board == null) return;
            var (x, y) = at.Value;
            string from = Signature(board, x, y);
            int nx = x, ny = y;
            while (true)
            {
                int tx = nx + dx, ty = ny + dy;
                if (tx < 0 || ty < 0 || tx >= board.Columns || ty >= board.Rows) break;
                nx = tx;
                ny = ty;
                if (Signature(board, nx, ny) != from) break;
            }
            if (nx == x && ny == y) { Navigation.AnnounceCurrent(); return; }
            Navigation.Active?.MoveTo(TileId(nx, ny));
        }

        private static string Signature(FightBoard board, int x, int y) =>
            FightState.Strike(x, y) + "|" + CellContents(board, x, y);

        // ---- choosing a target ----

        private const string TargetsStop = "fight:targets";
        private bool _wasSelecting;
        // The game's selection last seen: a card can chain them (Kunai: a card of the hand, then a
        // direction; Nether Lash: two entities), each a new one to announce and focus.
        private IntPtr _selectionSeen;
        // The strike marks while a selection runs: the game previews the strike of the target under
        // the cursor (a Titan Rocket's area follows it), a few frames after the hover, so the marks are
        // said once they have held still for StrikeStableFrames, and again whenever they settle on a
        // different set. _strikeSeen is the marked set as last read, _strikeSaid as last settled.
        private string _strikeSeen, _strikeSaid;
        private int _strikeStable;
        private const int StrikeStableFrames = 3;

        // A selection starting: on the board, its prompt is said (not the play area's) and focus goes to the board (staying
        // where it is if already there, the game's cursor brought to it); otherwise focus goes to the
        // target list. When it ends, focus is left to the navigator.
        public override void OnUpdate()
        {
            var set = FightState.Selecting ? FightState.ReadTargets() : null;
            bool selecting = set != null;
            IntPtr selection = selecting ? FightState.SelectionId : IntPtr.Zero;
            if (selecting && (!_wasSelecting || selection != _selectionSeen))
            {
                if (set.OnBoard)
                {
                    // A card played onto the grid as a whole ("Click grid to play") needs no prompt:
                    // landing on the board says it; Enter on any cell plays it.
                    string prompt = set.Tiles.Count > 0 || set.Arrows.Count > 0 ? FightState.Prompt() : null;
                    // The other choices it also takes, listed in their own stop: named with the prompt.
                    if (prompt != null && set.Others.Count > 0)
                    {
                        var board = FightBoard.Read();
                        var others = new List<string>();
                        foreach (var o in set.Others) others.Add(TargetText(o, board));
                        prompt = Strings.FightPromptOr(prompt, string.Join(", ", others));
                    }
                    if (prompt != null) Speech.Say(prompt);
                    // Onto a cell: the one on, else the one last on, else the mech (the stop's memory may
                    // be a hand card), said after the prompt, and the game's cursor brought to it so the
                    // strike it previews (said once settled, after both) is this cell's.
                    var to = FocusedTile() ?? _lastTile ?? FightKeys.MechTile() ?? FirstTarget(set);
                    if (to == null || Navigation.Active == null || !Navigation.Active.LandOn(TileId(to.Value.X, to.Value.Y)))
                        Navigation.Active?.FocusStop(BoardStop);
                }
                else if (set.InHand)
                {
                    // Cards of the hand to choose (a purge): answered where the game outlines them.
                    string prompt = FightState.Prompt();
                    if (prompt != null) Speech.Say(prompt);
                    var first = FightState.Hand().Find(set.HasCard);
                    if (first != null) Navigation.Active?.LandOn(CardId(first));
                }
                else Navigation.Active?.FocusStop(TargetsStop);
                _strikeSeen = _strikeSaid = null;
                _strikeStable = 0;
            }
            if (selecting) SayStrikeChanges();
            _wasSelecting = selecting;
            _selectionSeen = selection;
            NoChoicesUpdate();
            var on = FocusedTile();
            if (on.HasValue) _lastTile = on;
            else _lastZone = null; // off the cells (the hand counts): the zone is named again on return
        }

        private static readonly ControlId SkipRepeatsId = ControlId.Structural("fight:skiprepeats");
        private bool _wasNoChoices, _toSkipRepeats;

        // A selection that accepts nothing (Flamethrower x2's second burn with no card left): the game
        // waits on it all the same, drawing Skip Repeats as the way on. Its prompt is said with "no
        // choices", and focus goes to Skip Repeats once the button is in the graph.
        private void NoChoicesUpdate()
        {
            bool none = FightState.NoChoices;
            if (none && !_wasNoChoices)
            {
                string prompt = FightState.Prompt();
                Speech.Say(prompt != null ? prompt + ", " + Strings.FightNoChoices : Strings.FightNoChoices);
                _toSkipRepeats = true;
            }
            if (!none || !FightState.CanSkipRepeats) _toSkipRepeats = false;
            else if (_toSkipRepeats && Navigation.Active != null && Navigation.Active.MoveTo(SkipRepeatsId)) _toSkipRepeats = false;
            _wasNoChoices = none;
        }

        // The first tile a selection takes, top row first; null when it takes none.
        private static (int X, int Y)? FirstTarget(FightState.TargetSet set)
        {
            (int X, int Y)? best = null;
            foreach (var tile in set.Tiles)
            {
                var c = tile.TileModel.Coord;
                if (best == null || c.y > best.Value.Y || (c.y == best.Value.Y && c.x < best.Value.X)) best = (c.x, c.y);
            }
            return best;
        }

        // Says the strike marks once they settle on a set other than the last said. Settling on no
        // marks (a hover that previews nothing) forgets the last set, so coming back to a target says
        // its area again.
        private void SayStrikeChanges()
        {
            string seen = StrikeSignature();
            if (seen != _strikeSeen) { _strikeSeen = seen; _strikeStable = 0; return; }
            if (++_strikeStable != StrikeStableFrames) return;
            if (seen.Length == 0) { _strikeSaid = null; return; }
            if (seen == _strikeSaid) return;
            _strikeSaid = seen;
            string line = StrikeLine();
            if (line != null) Speech.Say(line);
        }

        // The marked tiles in a fixed order, "" when none.
        private static string StrikeSignature()
        {
            var tiles = new List<(int X, int Y)>(FightState.StrikeTiles());
            tiles.Sort();
            return string.Join(";", tiles);
        }

        /// <summary>"Hits Migo 1, mech; strikes 4 2; 3 3 to 5 3": who stands on the tiles the game marks as
        /// about to be struck, then those tiles merged into shapes; null when nothing is marked.</summary>
        private static string StrikeLine()
        {
            var marked = FightState.StrikeTiles();
            var board = FightBoard.Read();
            if (marked.Count == 0 || board == null) return null;
            var tiles = new List<(int X, int Y)>();
            foreach (var (x, y) in marked) tiles.Add((x + 1, y + 1));
            string where = string.Join("; ", TileRanges.Parts(tiles, board.Columns, board.Rows));
            // Who is hit, in reading order (top row first).
            var who = new List<string>();
            for (int y = board.Rows - 1; y >= 0; y--)
                for (int x = 0; x < board.Columns; x++)
                    if (marked.Contains((x, y)) && board.At(x, y) != null) who.Add(board.Name(board.At(x, y)));
            return Strings.FightStrikes(where, who.Count > 0 ? string.Join(", ", who) : null);
        }

        private static NodeVtable Target(AEncounterViewItem item, FightBoard board)
        {
            Func<string> label = () => TargetText(item, board);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(label) },
                SearchText = label,
                OnFocus = () => FightState.Hover(item),
                OnActivate = () => FightState.Click(item),
            };
        }

        // What a target is: the play area ("Play"), an arrow's direction, a tile with what stands on
        // it, a card's name.
        private static string TargetText(AEncounterViewItem item, FightBoard board)
        {
            if (item.TryCast<PlayAreaView>() != null) return Strings.FightPlay;
            var arrow = item.TryCast<DirectionSelectionArrow>();
            if (arrow != null) return Strings.FightDirection(arrow.Direction.ToString());
            var tile = item.TryCast<TileView>();
            if (tile != null)
            {
                var c = tile.TileModel.Coord;
                string where = FightBoard.CoordText(c.x, c.y);
                var occupant = board?.At(c.x, c.y);
                return occupant != null ? where + ", " + board.Describe(occupant) : where;
            }
            var card = item.TryCast<CardView>();
            if (card != null) return FightState.CardName(card.CardModel);
            return ViewNodes.Label(item);
        }

        // ---- the hand ----

        /// <summary>A hand card's node, also what the hand buffer reads each card by.</summary>
        internal static NodeVtable Card(CardView view)
        {
            Func<string> name = () => FightState.CardName(view.CardModel);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    // While a choice among the hand's cards runs, the ones it takes say so first, as cells do.
                    GameNodes.LabelPart(() => HandTarget(view) ? Strings.FightTarget + ", " + name() : name()),
                    new NodeAnnouncement(() => FightState.CardUpgrade(view.CardModel), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => Cost(view.CardModel, view), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => FightState.PaymentNote(view), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => FightState.DrawnRepeat(view), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => FightState.ModifierTraits(view.CardModel), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => FightState.CardTraits(view.CardModel), kind: AnnouncementKinds.Value),
                    new NodeAnnouncement(() => FightState.CardRarity(view.CardModel), kind: AnnouncementKinds.Value),
                    // Not playable says nothing while a choice in the hand runs: the card is a choice then.
                    GameNodes.DisabledPart(() => Playable(view) || HandChoice() != null),
                    new NodeAnnouncement(() => Playable(view) || HandChoice() != null ? null : GameText.Of(view._unplayableText), kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => FightState.CardDescription(view.CardModel)),
                    GameNodes.TooltipPart(() => FightState.CardUpgradeDescription(view.CardModel)),
                },
                SearchText = name,
                OnFocus = () => FightState.Hover(view),
                // In a choice among the hand's cards, Enter answers it on a target and refuses others.
                OnActivate = () =>
                {
                    var choice = HandChoice();
                    if (choice != null && !choice.HasCard(view)) { Speech.Say(Strings.FightNotTarget, interrupt: true); return; }
                    FightState.Click(view);
                },
                Details = () => FightState.CardInfoLines(view.CardModel),
                OnSecondary = () => SayDetails(FightState.CardInfoLines(view.CardModel)),
            };
        }

        // The running selection when it is a choice among the hand's cards, else null.
        private static FightState.TargetSet HandChoice()
        {
            var set = FightState.Selecting ? FightState.ReadTargets() : null;
            return set != null && set.InHand ? set : null;
        }

        private static bool HandTarget(CardView view) => HandChoice()?.HasCard(view) == true;

        /// <summary>Backspace, the game's right click ("more info"): the focused thing's details,
        /// read out at once, the ones the ui buffer steps through.</summary>
        private static void SayDetails(IEnumerable<string> lines)
        {
            var list = new List<string>(lines ?? Array.Empty<string>());
            Speech.Say(list.Count > 0 ? string.Join(" ", list) : Strings.NoDetails, interrupt: true);
        }

        private static bool Playable(CardView view) => view._unplayableObject == null || !view._unplayableObject.activeInHierarchy;

        /// <summary>"1 Heat", "2 Power", "1 Jam"...: each cost the card has, in the game's words. As
        /// <paramref name="view"/>'s art draws it when it has one (an upgrade can change the kind:
        /// Broken pays with a Jam, and the model still says 1 Heat), else from the model.</summary>
        public static string Cost(CardModel card, UnityEngine.Component view = null)
        {
            if (card == null) return null;
            string drawn = view != null ? FightState.DrawnCost(view) : null;
            if (drawn != null) return drawn;
            var parts = new List<string>();
            if (card.HeatCost > 0) parts.Add(Strings.FightCost(card.HeatCost, GameLoc.Get("HeatTitle_Misc")));
            if (card.PowerCost > 0) parts.Add(Strings.FightCost(card.PowerCost, GameLoc.Get("PowerTitle_Misc")));
            if (card.ManaCost > 0) parts.Add(Strings.FightCost(card.ManaCost, GameLoc.Get("ManaTitle_Misc")));
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>Play the nth card in hand (1-based), as the game's number keys do.</summary>
        public static void PlayCard(int n)
        {
            var hand = FightState.Hand();
            if (n < 1 || n > hand.Count) return;
            FightState.Click(hand[n - 1]);
        }

        // ---- the rack ----

        private static NodeVtable Button(AViewItem item, Func<string> label, Func<bool> enabled, Func<string> value, Func<string> description)
        {
            var vt = GameNodes.Button(label, () => FightState.Click(item), () => ViewNodes.IsShown(item) && (enabled == null || enabled()));
            var anns = new List<NodeAnnouncement>(vt.Announcements);
            if (value != null) anns.Insert(1, new NodeAnnouncement(value, live: true, kind: AnnouncementKinds.Value));
            vt.Announcements = anns;
            vt.OnFocus = () => FightState.Hover(item);
            if (description != null)
            {
                vt.Details = () => GameNodes.Lines(description());
                vt.OnSecondary = () => SayDetails(GameNodes.Lines(description()));
            }
            return vt;
        }

        // The Chrono Tokens left, as the button draws them ("3", "/3").
        private static string Tokens(EncounterChronoButtonView rewind)
        {
            if (rewind.NumberObject == null || !rewind.NumberObject.activeInHierarchy) return null;
            string left = GameText.Of(rewind.NumberText);
            string max = GameText.Of(rewind.MaxNumberText)?.TrimStart('/').Trim();
            if (left == null) return null;
            return int.TryParse(left, out int l) && int.TryParse(max, out int m) ? Strings.Position(l, m) : left;
        }

        private static string PileCount(Pile pile)
        {
            var model = FightState.Model;
            return model != null ? model.CardPlayModel.GetPile(pile).Count.ToString() : null;
        }

        // ---- the status ----

        private static NodeVtable Stat(Func<string> text, Func<string> description) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new List<NodeAnnouncement> { new NodeAnnouncement(text, live: true, kind: AnnouncementKinds.Label) },
            SearchText = text,
            Details = description != null ? () => GameNodes.Lines(description()) : (Func<IEnumerable<string>>)null,
            OnSecondary = description != null ? () => SayDetails(GameNodes.Lines(description())) : (Action)null,
        };

        /// <summary>The turn as the game's banner names it at the turn's start (TurnOverlay): "Encounter
        /// Start" for the opening turn, then "Player Turn 3", with ", 2 Turns Left", ", Final Turn" or
        /// ", Bonus Turn" once fewer than 4 remain. The banner is given the model's TurnNumber before
        /// StartTurnTask raises it, so the turn the game shows is TurnNumber - 1 for the whole turn
        /// (seen: the opening banner made at 0, the model then 1; "Player Turn 5" with the model at 6);
        /// turns left are MaxTurns minus that (100 without a limit, as GetTurnsRemaining).</summary>
        public static string TurnText()
        {
            var model = FightState.Model;
            if (model == null) return null;
            int shown = model.TurnNumber - 1;
            if (shown <= 0) return GameLoc.Get("OverlayEncounterStart_Misc") ?? Strings.FightTurn(0, model.MaxTurns);
            string turn = GameLoc.Get("OverlayPlayerTurnX_Misc");
            turn = turn != null ? turn.Replace("[X]", shown.ToString()) : Strings.FightTurn(shown, model.MaxTurns);
            int left = model.MaxTurns < 0 ? 100 : model.MaxTurns - shown;
            if (left >= 4) return turn;
            string more = left == 1 ? GameLoc.Get("OverlayFinalTurn_Misc")
                : left < 1 ? GameLoc.Get("OverlayBonusTurn_Misc")
                : GameLoc.Get("OverlayXTurnsLeft_Misc")?.Replace("[X]", left.ToString());
            return more != null ? turn + ", " + more : turn;
        }

        /// <summary>The objective the right-hand screen states ("Objective: Defeat Mothership!"). It
        /// is hidden while anything is hovered, which our focus always is, but kept: a hover or a
        /// selection writes <see cref="ScreenView.SelectionPrompt"/>, and only the default path
        /// (<see cref="ScreenView.UpdateText"/>) writes <see cref="ScreenView.Text"/>. None in the
        /// Overseer's fight: the game has no objective for it and leaves the Kraken's there.</summary>
        public static string Objective()
        {
            if (Overseer.Model() != null) return null;
            var screen = FightState.View?.ScreenView;
            return screen != null ? GameText.Of(screen.Text) : null;
        }

        /// <summary>The objective key's line: the objective, else (the Overseer's fight) the Overseer's
        /// health or phase.</summary>
        public static string ObjectiveLine() => Objective() ?? Overseer.Line();

        // "Used, Sign Magick, Artifact, Starter, The first time...": the game's "Used" before the name while
        // it greys the artifact out (its UsedObject: a once-a-turn or once-a-fight effect spent). The label
        // always reads first, so the word is part of it, as a target card's "target, ".
        private static NodeVtable Artifact(ArtifactView view)
        {
            var model = view.ArtifactModel;
            var vt = CardNodes.Artifact(model, view, cooldown: false, focus: () => FightState.Hover(view));
            Func<string> name = () => model != null ? GameLoc.ArtifactName(model.ArtifactName) : null;
            var parts = new List<NodeAnnouncement>(vt.Announcements);
            parts[0] = GameNodes.LabelPart(() =>
            {
                string used = view.UsedObject != null && view.UsedObject.activeInHierarchy ? GameLoc.Get("UsedArtifact_Misc") : null;
                return used != null ? used + ", " + name() : name();
            });
            vt.Announcements = parts;
            return vt;
        }

        // "Conjure, Spell, button, Cooldown: 2, disabled, Give or break a Shield on any entity.": the
        // turns left while the slot draws its counter; Enter casts it (the game's click, which refuses
        // a used, destroyed or recharging spell).
        private static NodeVtable Spell(SpellView view)
        {
            var model = view.ArtifactModel;
            Func<bool> recharging = () => view.CooldownObject != null && view.CooldownObject.activeInHierarchy;
            var vt = CardNodes.Artifact(model, view, cooldown: false, focus: () => FightState.Hover(view));
            vt.ControlType = ControlTypes.Button;
            var parts = new List<NodeAnnouncement>(vt.Announcements);
            parts.Insert(2, new NodeAnnouncement(() =>
            {
                if (!recharging()) return null;
                string text = GameLoc.Get("Cooldown_Misc");
                string turns = GameText.Of(view.Cooldown);
                return text != null ? text.Replace("[X]", turns) : turns;
            }, live: true, kind: AnnouncementKinds.Value));
            parts.Insert(3, GameNodes.DisabledPart(() => !model.Used && !model.Destroyed && !recharging()));
            vt.Announcements = parts;
            vt.OnActivate = () => FightState.Click(view);
            return vt;
        }

        private static bool Visible(UnityEngine.Component view)
        {
            var group = view.GetComponent<UnityEngine.CanvasGroup>();
            return group == null || group.alpha > 0.05f;
        }

        /// <summary>"Stars 7500", as the fight's Stars panel draws it; null while it is hidden.</summary>
        internal static string Stars()
        {
            var stars = FightState.View?._starBucksText;
            if (stars == null || !stars.gameObject.activeInHierarchy) return null;
            return Join(GameLoc.Get("StarPanelTitle_Misc"), Number(stars));
        }

        private static string Number(TMP_Text tmp)
        {
            string text = GameText.Of(tmp);
            if (text == null) return null;
            string trimmed = text.TrimStart('0');
            return trimmed.Length == 0 ? "0" : trimmed;
        }

        private static string Join(string label, string value)
        {
            if (label == null) return value;
            return value == null ? label : label + " " + value;
        }
    }
}
