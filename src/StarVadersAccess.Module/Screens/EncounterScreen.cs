using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core.Graph;
using StarVadersAccess.Core.Input;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Core.UI;
using StarVadersAccess.Module.Fight;
using StarVadersAccess.Module.Interop;
using StarVadersAccess.Module.UI;
using TMPro;
using FightState = StarVadersAccess.Module.Fight.Fight;
using Screen = StarVadersAccess.Core.Screens.Screen;

namespace StarVadersAccess.Module.Screens
{
    /// <summary>
    /// The fight (<c>EncounterScene</c>). Stops: the hand (each card "name, cost, description", Enter
    /// playing it as the game's click does, unplayable cards disabled with the game's reason); the
    /// rack (End Turn, Rewind with the Chrono Tokens left, the draw and discard piles with their
    /// counts); the status (turn x/y, Doom, Heat, Power and Mana where the mech has them, Stars,
    /// the objective, the artifacts). Labels are the game's own strings (its <c>*_Misc</c> keys).
    /// </summary>
    public sealed class EncounterScreen : Screen
    {
        public override string Key => "fight";
        public override bool AllowsTypeahead => false;
        public override object InitialFocusStop => BoardStop;

        private static readonly InputCategory[] Categories = { InputCategory.Game, InputCategory.UI };
        public override IReadOnlyList<InputCategory> InputCategories => Categories;

        public override IEnumerable<ElementAction> GetActions()
        {
            // Escape cancels a selection the game lets go of.
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => FightState.Cancel());
        }

        public override bool IsActive()
        {
            var c = FightState.Controller;
            return c != null && !c.EncounterEnded;
        }

        public override string ScreenName => Objective();

        public override void Build(GraphBuilder b)
        {
            var c = FightState.Controller;
            if (c == null) return;
            var view = c.EncounterView;

            if (FightState.Selecting)
            {
                var targets = FightState.Targets();
                if (targets.Count > 0)
                {
                    var board = FightBoard.Read();
                    b.BeginStop(TargetsStop);
                    b.PushContext(FightState.Prompt(), Strings.RoleList);
                    foreach (var target in targets)
                        b.AddItem(ControlId.Structural("fight:target:" + target.GetInstanceID()), Target(target, board));
                    b.PopContext();
                }
            }

            BuildBoard(b);

            b.BeginStop("fight:hand");
            b.PushContext(null, Strings.RoleList);
            foreach (var card in FightState.Hand())
                b.AddItem(ControlId.Structural("fight:card:" + card.GetInstanceID()), Card(card));
            b.PopContext();

            b.BeginStop("fight:rack");
            b.PushContext(null, Strings.RoleList);
            var endTurn = view.EndTurnView;
            if (ViewNodes.IsShown(endTurn))
                b.AddItem(ControlId.Structural("fight:endturn"), Button(endTurn, () => GameLoc.Get("EndTurnTitle_Misc"),
                    () => endTurn.ButtonEnabled, null, () => GameLoc.Get("EndTurnDesc_Misc")));
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
            b.PushContext(null, Strings.RoleList);
            b.AddItem(ControlId.Structural("fight:turn"), Stat(TurnText, null));
            var doom = view.DoomView;
            if (ViewNodes.IsShown(doom))
                b.AddItem(ControlId.Structural("fight:doom"), Stat(() => Figure("DoomTitle_Misc", doom.DoomText), () => GameLoc.Get("DoomPanelDesc_Misc")));
            var heat = view.HeatView;
            if (ViewNodes.IsShown(heat) && Visible(heat))
                b.AddItem(ControlId.Structural("fight:heat"), Stat(() => Figure("HeatTitle_Misc", heat._heatValueText), () => GameLoc.Get("HeatPanelDesc_Misc")));
            var power = view.PowerView;
            if (ViewNodes.IsShown(power) && Visible(power))
                b.AddItem(ControlId.Structural("fight:power"), Stat(() => PowerText(power), () => GameLoc.Get("PowerBankDesc_Misc")));
            var mana = view.ManaView;
            if (ViewNodes.IsShown(mana) && Visible(mana))
                b.AddItem(ControlId.Structural("fight:mana"), Stat(() => ManaText(mana), () => GameLoc.Get("ManaPanelDesc_Misc")));
            var stars = view._starBucksText;
            if (stars != null && stars.gameObject.activeInHierarchy)
                b.AddItem(ControlId.Structural("fight:stars"), Stat(() => Join(GameLoc.Get("StarPanelTitle_Misc"), Number(stars)), () => GameLoc.Get("StarPanelDesc_Misc")));
            b.AddItem(ControlId.Structural("fight:objective"), Stat(Objective, null));
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

        public static ControlId TileId(int x, int y) => ControlId.Structural("fight:tile:" + x + "," + y);

        /// <summary>The 0-based tile the focus is on, or null off the board.</summary>
        public static (int X, int Y)? FocusedTile()
        {
            var id = (Navigation.Active as GraphNavigator)?.FocusedNode?.Id?.StructuralKey as string;
            if (id == null || !id.StartsWith("fight:tile:")) return null;
            var parts = id.Substring("fight:tile:".Length).Split(',');
            return (int.Parse(parts[0]), int.Parse(parts[1]));
        }

        // The grid, top row first so Up goes up the board; the rows share a key, so Up and Down keep
        // the column. Focus first lands on the mech.
        private static void BuildBoard(GraphBuilder b)
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
                // One part, in speechchem's order: the target mark, the coordinates, the zone when
                // crossed into, then what is there.
                Announcements = new List<NodeAnnouncement> { GameNodes.LabelPart(() => CellText(x, y)) },
                SearchText = () => FightBoard.CoordText(x, y),
                IsSelected = () => IsMech(x, y),
                OnFocus = () => { var tile = Tile(x, y); if (tile != null) FightState.Hover(tile); },
                OnActivate = () => { var tile = Tile(x, y); if (tile != null) FightState.Click(tile); },
                Details = () => CellDetails(x, y),
            };
        }

        private static string CellText(int x, int y)
        {
            var parts = new List<string>();
            if (IsTarget(x, y)) parts.Add(Strings.FightTarget);
            parts.Add(FightBoard.CoordText(x, y));
            string zone = ZoneCrossing(x, y);
            if (zone != null) parts.Add(zone);
            string contents = CellContents(FightBoard.Read(), x, y);
            if (contents != null) parts.Add(contents);
            return string.Join(", ", parts);
        }

        private static bool IsMech(int x, int y) => FightBoard.Read()?.At(x, y)?.TryCast<PlayerEntityModel>() != null;

        private static bool IsTarget(int x, int y)
        {
            if (!FightState.Selecting) return false;
            var tile = Tile(x, y);
            var sc = FightState.Controller?.SelectionController;
            try { return tile != null && sc != null && sc.IsSatisfied(tile); }
            catch { return false; }
        }

        // The zone is named only on entering it, like a region (speechchem's zone crossing); leaving
        // the board forgets it, so coming back names it again.
        private static string ZoneCrossing(int x, int y)
        {
            var board = FightBoard.Read();
            if (board == null) return null;
            string zone = board.Zone(x, y);
            if (zone == _lastZone) return null;
            _lastZone = zone;
            return Strings.FightZone(zone);
        }

        /// <summary>What a tile holds and what threatens it, after its coordinates: the entity with
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
            }
            string danger = board.Danger(x, y);
            if (danger != null) yield return danger;
            var status = board.Status(x, y);
            if (status.HasValue)
            {
                string d = FightBoard.StatusDescription(status.Value);
                yield return d != null ? Strings.FightLabelled(FightBoard.StatusName(status.Value), d) : FightBoard.StatusName(status.Value);
            }
            string pickup = board.Pickup(x, y);
            if (pickup != null) yield return pickup;
        }

        /// <summary>Shift+arrow: past the cells that read the same as this one (contents and zone),
        /// to the first that differs, or to the edge; at the edge, this cell again.</summary>
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

        private static string Signature(FightBoard board, int x, int y) => board.Zone(x, y) + "|" + CellContents(board, x, y);

        // ---- choosing a target ----

        private const string TargetsStop = "fight:targets";
        private bool _wasSelecting;

        // A selection starting takes focus to its targets; when it ends, focus is left to the
        // navigator (the target list vanishing lands it on the hand).
        public override void OnUpdate()
        {
            bool selecting = FightState.Selecting && FightState.Targets().Count > 0;
            if (selecting && !_wasSelecting) Navigation.Active?.FocusStop(TargetsStop);
            _wasSelecting = selecting;
            if (!(Navigation.Active is GraphNavigator nav) || !Equals(nav.FocusedNode?.StopKey, BoardStop)) _lastZone = null;
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

        private static NodeVtable Card(CardView view)
        {
            Func<string> name = () => FightState.CardName(view.CardModel);
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    new NodeAnnouncement(() => Cost(view.CardModel), kind: AnnouncementKinds.Value),
                    GameNodes.DisabledPart(() => Playable(view)),
                    new NodeAnnouncement(() => Playable(view) ? null : GameText.Of(view._unplayableText), kind: AnnouncementKinds.Value),
                    GameNodes.TooltipPart(() => FightState.CardDescription(view.CardModel)),
                },
                SearchText = name,
                OnFocus = () => FightState.Hover(view),
                OnActivate = () => FightState.Click(view),
            };
        }

        private static bool Playable(CardView view) => view._unplayableObject == null || !view._unplayableObject.activeInHierarchy;

        /// <summary>"1 Heat", "2 Power"...: each cost the card has, in the game's words.</summary>
        public static string Cost(CardModel card)
        {
            if (card == null) return null;
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
            if (description != null) anns.Add(GameNodes.TooltipPart(description));
            vt.Announcements = anns;
            vt.OnFocus = () => FightState.Hover(item);
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
            Announcements = new List<NodeAnnouncement>
            {
                new NodeAnnouncement(text, live: true, kind: AnnouncementKinds.Label),
                GameNodes.TooltipPart(description ?? (() => null)),
            },
            SearchText = text,
        };

        /// <summary>"Turn 1/8".</summary>
        public static string TurnText()
        {
            var model = FightState.Model;
            return model != null ? Strings.FightTurn(model.TurnNumber, model.MaxTurns) : null;
        }

        /// <summary>"Doom 0/5", "Heat 1/3": the game's title and the figure its panel draws.</summary>
        public static string Figure(string titleKey, TMP_Text value) => Join(GameLoc.Get(titleKey), GameText.Of(value));

        private static string PowerText(PowerView power) => Join(GameLoc.Get("PowerTitle_Misc"),
            power._maxPower > 0 ? Strings.Position(power._power, power._maxPower) : GameText.Of(power._powerValueText));

        private static string ManaText(ManaView mana) => Join(GameLoc.Get("ManaTitle_Misc"), GameText.Of(mana.GetComponentInChildren<TMP_Text>()));

        /// <summary>The objective the right-hand screen states when it shows nothing else.</summary>
        public static string Objective()
        {
            var screen = FightState.View?.ScreenView;
            if (screen == null || !screen._isDefault) return null;
            return GameText.Of(screen.Text);
        }

        private static NodeVtable Artifact(ArtifactView view)
        {
            var model = view.ArtifactModel;
            Func<string> name = () => model != null ? GameLoc.ArtifactName(model.ArtifactName) : null;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GameNodes.LabelPart(name),
                    GameNodes.TooltipPart(() => model != null ? GameLoc.ArtifactDescription(model.ArtifactName) : null),
                },
                SearchText = name,
                OnFocus = () => FightState.Hover(view),
            };
        }

        private static bool Visible(UnityEngine.Component view)
        {
            var group = view.GetComponent<UnityEngine.CanvasGroup>();
            return group == null || group.alpha > 0.05f;
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
