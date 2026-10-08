using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Module.Interop;

namespace StarVadersAccess.Module.Fight
{
    /// <summary>
    /// The board as the model has it: which entity stands on which tile, its name, what threatens a
    /// tile. Tiles are named "x, y", 1-based, column then row, row 1 at the bottom (the game's
    /// <see cref="Coord"/> is 0-based with y growing upward, the way the mech fires). Built fresh on
    /// each call from <see cref="EncounterModel.ModelItemDict"/>: nothing is kept between renders.
    /// An invader is numbered ("Migo 3", the game's own <see cref="EnemyID.Number"/> plus one,
    /// stable for the fight) when the fight has more than one of its kind, dead ones included.
    /// </summary>
    internal sealed class FightBoard
    {
        public readonly int Columns, Rows;
        private readonly EncounterModel _model;
        private readonly GridModel _grid;
        private readonly Dictionary<(int, int), AEntityModel> _at = new Dictionary<(int, int), AEntityModel>();
        private readonly Dictionary<EnemyName, int> _kinds = new Dictionary<EnemyName, int>();
        private readonly HashSet<(int, int)> _spawn = new HashSet<(int, int)>();
        public readonly List<AEntityModel> Entities = new List<AEntityModel>();

        private FightBoard(EncounterModel model)
        {
            _model = model;
            _grid = model.GridModel;
            Columns = _grid.GridShape.x;
            Rows = _grid.GridShape.y;
        }

        /// <summary>The board now, or null outside a fight.</summary>
        public static FightBoard Read()
        {
            var model = Fight.Model;
            if (model == null) return null;
            var board = new FightBoard(model);
            var grid = board._grid;
            try
            {
                foreach (var kv in model.ModelItemDict)
                {
                    var entity = kv.Value?.TryCast<AEntityModel>();
                    if (entity == null) continue;
                    var enemy = entity.TryCast<EnemyEntityModel>();
                    if (enemy != null)
                    {
                        var kind = enemy.EnemyID.EnemyName;
                        board._kinds[kind] = board._kinds.TryGetValue(kind, out int n) ? n + 1 : 1;
                    }
                    var id = entity.ID.Cast<EntityID>();
                    if (!grid.IsEntityOnGrid(id)) continue;
                    var c = grid.GetEntityCoord(id, false);
                    board._at[(c.x, c.y)] = entity;
                    board.Entities.Add(entity);
                }
                foreach (var c in grid.GetSpawnCoords()) board._spawn.Add((c.x, c.y));
            }
            catch (Exception e) { CoreLog.Warning("FightBoard: reading the board failed: " + e.Message); }
            return board;
        }

        public AEntityModel At(int x, int y) => _at.TryGetValue((x, y), out var e) ? e : null;

        public static Coord MakeCoord(int x, int y)
        {
            var c = new Coord();
            c.x = x;
            c.y = y;
            return c;
        }

        /// <summary>"x, y" for a 0-based game coordinate.</summary>
        public static string CoordText(int x, int y) => Strings.FightCoord(x + 1, y + 1);

        /// <summary>The entity's name: an invader's (numbered when it has kin) or item's own, "mech"
        /// for the player.</summary>
        public string Name(AEntityModel entity)
        {
            if (entity == null) return null;
            var enemy = entity.TryCast<EnemyEntityModel>();
            if (enemy != null)
            {
                var id = enemy.EnemyID;
                string name = GameLoc.EnemyName(id.EnemyName) ?? id.EnemyName.ToString();
                return _kinds.TryGetValue(id.EnemyName, out int n) && n > 1 ? name + " " + (id.Number + 1) : name;
            }
            var item = entity.TryCast<ItemEntityModel>();
            if (item != null) { var n = item.ItemID.ItemName; return GameLoc.Get(n + "_EntityTitle") ?? n.ToString(); }
            if (entity.TryCast<PlayerEntityModel>() != null) return Strings.FightMech;
            return entity.GetIl2CppType().Name;
        }

        /// <summary>The entity's state after its name: health above 1, shielded, shocked, charge.</summary>
        public static string State(AEntityModel entity)
        {
            if (entity == null) return null;
            var parts = new List<string>();
            if (entity.TryCast<PlayerEntityModel>() == null && entity.Health > 1) parts.Add(Strings.FightHealth(entity.Health));
            if (entity.IsShielded) parts.Add(GameLoc.Get("Shielded_Keyword"));
            if (entity.IsShocked) parts.Add(GameLoc.Get("Shocked_Keyword"));
            if (entity.Charge > 0) parts.Add(Strings.FightCharge(entity.Charge));
            parts.RemoveAll(string.IsNullOrEmpty);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>"Migo 3, 2 health, shielded": name then state.</summary>
        public string Describe(AEntityModel entity)
        {
            string name = Name(entity);
            string state = State(entity);
            return state != null ? name + ", " + state : name;
        }

        /// <summary>The entity's description (its info panel's text), or null.</summary>
        public static string Description(AEntityModel entity)
        {
            var enemy = entity?.TryCast<EnemyEntityModel>();
            if (enemy != null) return GameLoc.EnemyDescription(enemy.EnemyID.EnemyName);
            var item = entity?.TryCast<ItemEntityModel>();
            if (item != null) return GameLoc.Get(item.ItemID.ItemName + "_EntityDesc");
            return null;
        }

        /// <summary>"Danger Zone: Migo 1, Migo 3" when something will strike the tile, else null.</summary>
        public string Danger(int x, int y)
        {
            try
            {
                var c = MakeCoord(x, y);
                if (!_grid.IsCoordDangerZone(c)) return null;
                var who = new List<string>();
                foreach (var id in _grid.WhoIsTargettingDangerZone(c, _model))
                {
                    var entity = _model.ModelItemDict[id.Cast<ID>()]?.TryCast<AEntityModel>();
                    string name = Name(entity);
                    if (name != null && !who.Contains(name)) who.Add(name);
                }
                string label = GameLoc.Get("DangerZoneText_Misc") ?? "Danger Zone";
                return who.Count > 0 ? Strings.FightLabelled(label, string.Join(", ", who)) : label;
            }
            catch (Exception e)
            {
                CoreLog.Warning("FightBoard: reading the danger at " + x + "," + y + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The tile's status ("Cracked", "Hole"), or null when it has none.</summary>
        public TileStatus? Status(int x, int y)
        {
            try
            {
                var status = _grid.TryGetTileStatusAtCoord(MakeCoord(x, y));
                return status.HasValue && status.Value != TileStatus.None ? status.Value : (TileStatus?)null;
            }
            catch { return null; }
        }

        public static string StatusName(TileStatus status) => GameLoc.Get(status + "Title_Misc") ?? status.ToString();
        public static string StatusDescription(TileStatus status) => GameLoc.Get(status + "TileStatus_Misc");

        /// <summary>"Pickup: Fire!" for a card lying on the tile, else null.</summary>
        public string Pickup(int x, int y)
        {
            try
            {
                var map = _grid.PickupMap;
                var c = MakeCoord(x, y);
                if (!map.ContainsKey(c)) return null;
                var card = Fight.View?.CardPlayView?.GetCardView(map[c])?.CardModel;
                string label = GameLoc.Get("Pickup_Keyword") ?? "Pickup";
                return card != null ? Strings.FightLabelled(label, Fight.CardName(card)) : label;
            }
            catch { return null; }
        }

        /// <summary>The row's zone: "doom" (invaders there add Doom), "spawn", or "middle".</summary>
        public string Zone(int x, int y)
        {
            try { if (_grid.IsCoordDoomZone(MakeCoord(x, y))) return "doom"; }
            catch { }
            return _spawn.Contains((x, y)) ? "spawn" : "middle";
        }
    }
}
