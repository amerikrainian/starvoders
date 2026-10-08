using System;
using System.Collections.Generic;
using StarVaders;
using StarVadersAccess.Core;
using StarVadersAccess.Core.Strings;
using StarVadersAccess.Module.Interop;

namespace StarVadersAccess.Module.Fight
{
    /// <summary>
    /// The board as the model has it: which entity stands on which tile, and its name. Tiles are
    /// named "x, y", 1-based, column then row, row 1 at the bottom (the game's <see cref="Coord"/>
    /// is 0-based with y growing upward, the way the mech fires). Built fresh on each call from
    /// <see cref="EncounterModel.ModelItemDict"/>: nothing is kept between renders.
    /// </summary>
    internal sealed class FightBoard
    {
        public readonly int Columns, Rows;
        private readonly Dictionary<(int, int), AEntityModel> _at = new Dictionary<(int, int), AEntityModel>();
        public readonly List<AEntityModel> Entities = new List<AEntityModel>();

        private FightBoard(int columns, int rows) { Columns = columns; Rows = rows; }

        /// <summary>The board now, or null outside a fight.</summary>
        public static FightBoard Read()
        {
            var model = Fight.Model;
            if (model == null) return null;
            var grid = model.GridModel;
            var board = new FightBoard(grid.GridShape.x, grid.GridShape.y);
            try
            {
                foreach (var kv in model.ModelItemDict)
                {
                    var entity = kv.Value?.TryCast<AEntityModel>();
                    if (entity == null) continue;
                    var id = entity.ID.Cast<EntityID>();
                    if (!grid.IsEntityOnGrid(id)) continue;
                    var c = grid.GetEntityCoord(id, false);
                    board._at[(c.x, c.y)] = entity;
                    board.Entities.Add(entity);
                }
            }
            catch (Exception e) { CoreLog.Warning("FightBoard: reading the entities failed: " + e.Message); }
            return board;
        }

        public AEntityModel At(int x, int y) => _at.TryGetValue((x, y), out var e) ? e : null;

        /// <summary>"x, y" for a 0-based game coordinate.</summary>
        public static string CoordText(int x, int y) => Strings.FightCoord(x + 1, y + 1);

        /// <summary>The entity's name: an invader's or item's own, "mech" for the player.</summary>
        public static string Name(AEntityModel entity)
        {
            if (entity == null) return null;
            var enemy = entity.TryCast<EnemyEntityModel>();
            if (enemy != null) { var n = enemy.EnemyID.EnemyName; return GameLoc.EnemyName(n) ?? n.ToString(); }
            var item = entity.TryCast<ItemEntityModel>();
            if (item != null) { var n = item.ItemID.ItemName; return GameLoc.Get(n + "_EntityTitle") ?? n.ToString(); }
            if (entity.TryCast<PlayerEntityModel>() != null) return Strings.FightMech;
            return entity.GetIl2CppType().Name;
        }
    }
}
