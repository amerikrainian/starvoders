using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Core.Strings;
using StarVoders.Module.Interop;

namespace StarVoders.Module.Fight
{
    /// <summary>
    /// The board as the model has it: which entity stands on which tile, its name, what threatens a
    /// tile. Tiles are named "x, y", 1-based, column then row, row 1 at the bottom (the game's
    /// <see cref="Coord"/> is 0-based with y growing upward, the way the mech fires). Built fresh on
    /// each call from <see cref="EncounterModel.ModelItemDict"/>: nothing is kept between renders.
    /// An invader is numbered within its kind ("Shooter 2": its place by the game's own
    /// <see cref="EnemyID.Number"/>, stable for the fight) when the fight has more than one of its
    /// kind, dead ones included.
    /// </summary>
    internal sealed class FightBoard
    {
        public readonly int Columns, Rows;
        private readonly EncounterModel _model;
        private readonly GridModel _grid;
        private readonly Dictionary<(int, int), AEntityModel> _at = new Dictionary<(int, int), AEntityModel>();
        // Every invader of the fight by kind, dead ones included, as the game's numbers in order: an
        // invader is called by its place in its kind ("Shooter 2"), stable since the dead stay.
        // Invaders numbered within the name they are read by: kinds that share a title (the Overseer
        // fight's three Chaotic Detritus) count as one, by kind then the game's number.
        private readonly Dictionary<string, List<(int Kind, int Number)>> _kinds = new Dictionary<string, List<(int Kind, int Number)>>();
        private readonly Dictionary<EnemyName, string> _kindNames = new Dictionary<EnemyName, string>();
        private readonly HashSet<(int, int)> _spawn = new HashSet<(int, int)>();
        private readonly HashSet<(int, int)> _summons = new HashSet<(int, int)>();
        public readonly List<AEntityModel> Entities = new List<AEntityModel>();
        // The player's tile (0-based), null while it is off the board (a Keeper with no Summon).
        private (int X, int Y)? _player;

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
                        string name = board.KindName(kind);
                        if (!board._kinds.TryGetValue(name, out var numbers)) board._kinds[name] = numbers = new List<(int Kind, int Number)>();
                        numbers.Add(((int)kind, enemy.EnemyID.Number));
                    }
                    var id = entity.ID.Cast<EntityID>();
                    if (!grid.IsEntityOnGrid(id)) continue;
                    var c = grid.GetEntityCoord(id, false);
                    // A Keeper haunting a Summon shares its tile: the cell is the Summon's ("Sol,
                    // haunted"), the player's tile kept apart.
                    bool player = entity.TryCast<PlayerEntityModel>() != null;
                    if (player) board._player = (c.x, c.y);
                    if (!player || !board._at.ContainsKey((c.x, c.y))) board._at[(c.x, c.y)] = entity;
                    board.Entities.Add(entity);
                }
                foreach (var numbers in board._kinds.Values) numbers.Sort();
                // The player's tile by the game's own reckoning: a Keeper has no entity of its own,
                // its id standing for the Summon it haunts (GetPlayerCoord is that Summon's tile).
                var playerId = grid.PlayerID;
                if (playerId != null && grid.IsEntityOnGrid(playerId))
                {
                    var p = grid.GetPlayerCoord();
                    board._player = (p.x, p.y);
                }
                foreach (var c in grid.GetSpawnCoords()) board._spawn.Add((c.x, c.y));
                var summons = grid.PlayerPuppetList;
                if (summons != null)
                    foreach (var id in summons)
                        if (grid.IsEntityOnGrid(id)) { var c = grid.GetEntityCoord(id, false); board._summons.Add((c.x, c.y)); }
            }
            catch (Exception e) { CoreLog.Warning("FightBoard: reading the board failed: " + e.Message); }
            return board;
        }

        public AEntityModel At(int x, int y) => _at.TryGetValue((x, y), out var e) ? e : null;

        /// <summary>The player's tile (0-based): the mech's, or the Summon a Keeper haunts; null while
        /// the player is off the board.</summary>
        public (int X, int Y)? PlayerTile => _player;

        /// <summary>Whether the entity is the Summon the player haunts (it shares the player's tile).</summary>
        public bool IsHaunted(AEntityModel entity)
        {
            if (entity == null || _player == null || entity.TryCast<PlayerEntityModel>() != null) return false;
            return ReferenceEquals(At(_player.Value.X, _player.Value.Y), entity) || At(_player.Value.X, _player.Value.Y)?.Pointer == entity.Pointer;
        }

        /// <summary>Whether the tile holds one of the player's Summons (the game's puppets:
        /// <see cref="GridModel.PlayerPuppetList"/>, a Keeper's).</summary>
        public bool IsSummon(int x, int y) => _summons.Contains((x, y));

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
                var (kind, number) = Kind(entity);
                return number > 0 ? kind + " " + number : kind;
            }
            var item = entity.TryCast<ItemEntityModel>();
            if (item != null) { var n = item.ItemID.ItemName; return GameLoc.Get(n + "_EntityTitle") ?? n.ToString(); }
            if (entity.TryCast<PlayerEntityModel>() != null) return Strings.FightMech;
            return entity.GetIl2CppType().Name;
        }

        /// <summary>The entity's name split into its kind and number ("Shooter", 9), the number 0 where
        /// the name has none: an invader without kin, anything else (its kind is its whole name).</summary>
        public (string Kind, int Number) Kind(AEntityModel entity)
        {
            if (entity == null) return (null, 0);
            var enemy = entity.TryCast<EnemyEntityModel>();
            if (enemy == null) return (Name(entity), 0);
            var id = enemy.EnemyID;
            string name = KindName(id.EnemyName);
            if (OneCreature.Contains(id.EnemyName)) return (name, 0);
            return _kinds.TryGetValue(name, out var numbers) && numbers.Count > 1
                ? (name, numbers.IndexOf(((int)id.EnemyName, id.Number)) + 1) : (name, 0);
        }

        // The parts of one boss the game draws as a single creature over several tiles, under one title:
        // the Mega-Blob's core and pieces, Nessie's head and body. Read unnumbered ("Mega-Blob" on each of
        // its nine tiles), not as nine of it.
        private static readonly HashSet<EnemyName> OneCreature = new HashSet<EnemyName>
        {
            EnemyName.MegaBlob, EnemyName.MegaBlobPiece, EnemyName.WormCore, EnemyName.WormBody,
        };

        // An invader kind's name, looked up once per board.
        private string KindName(EnemyName kind)
        {
            if (!_kindNames.TryGetValue(kind, out var name)) _kindNames[kind] = name = GameLoc.EnemyName(kind) ?? kind.ToString();
            return name;
        }

        // The Hands of Time (the Overseer fight's, the ultimate one's): never destroyed, their health
        // counts the hits they take from 99.
        private static readonly HashSet<EnemyName> HitCounters = new HashSet<EnemyName>
        {
            EnemyName.ChaosLeftHand, EnemyName.ChaosRightHand, EnemyName.UltimateLeftHand, EnemyName.UltimateRightHand,
        };

        // Bosses whose charge the game draws as a named panel beside the board (BossCounters): the
        // Grandmaster's hourglass.
        private static readonly HashSet<EnemyName> CountedElsewhere = new HashSet<EnemyName> { EnemyName.Grandmaster };

        /// <summary>The entity's charge as worth saying: 0 where a boss panel reads it by name.</summary>
        public static int ShownCharge(AEntityModel entity)
        {
            var enemy = entity?.TryCast<EnemyEntityModel>();
            if (enemy != null && CountedElsewhere.Contains(enemy.EnemyID.EnemyName)) return 0;
            return entity?.Charge ?? 0;
        }

        /// <summary>The entity's health as worth saying: 0 for the Hands of Time, whose health is a hit
        /// counter.</summary>
        public static int ShownHealth(AEntityModel entity)
        {
            var enemy = entity?.TryCast<EnemyEntityModel>();
            if (enemy != null && HitCounters.Contains(enemy.EnemyID.EnemyName)) return 0;
            return entity?.Health ?? 0;
        }

        /// <summary>The entity's state after its name: its named traits (Bomber Mech's "Bomb"), health
        /// above 1, shielded, immune, shocked, charge.</summary>
        public static string State(AEntityModel entity)
        {
            if (entity == null) return null;
            var parts = new List<string>();
            foreach (var trait in Traits(entity)) parts.Add(GameLoc.Get(trait + "_Keyword"));
            if (ActivateReady(entity)) parts.Add(Strings.FightReady(GameLoc.Get("Activate_Keyword") ?? "Activate"));
            if (entity.TryCast<PlayerEntityModel>() == null && ShownHealth(entity) > 1) parts.Add(Strings.FightHealth(entity.Health));
            if (entity.IsShielded) parts.Add(ShieldName(entity) ?? GameLoc.Get("Shielded_Keyword"));
            parts.Add(ImmuneName(entity));
            if (entity.IsShocked) parts.Add(GameLoc.Get("Shocked_Keyword"));
            string channel = ChannelText(entity);
            if (channel != null) parts.Add(channel);
            else if (ShownCharge(entity) > 0) parts.Add(Strings.FightCharge(entity.Charge));
            parts.RemoveAll(string.IsNullOrEmpty);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        /// <summary>The traits the tile panel's trait line names after the entity's kind ("Mech •
        /// Bomb"): the ones with a <c>_Keyword</c> name, the kind (<see cref="EntityTrait.Enemy"/>, every
        /// invader's) left out; the others are the game's own bookkeeping.</summary>
        public static List<EntityTrait> Traits(AEntityModel entity)
        {
            var traits = new List<EntityTrait>();
            try
            {
                if (entity?.Traits != null)
                    foreach (var trait in entity.Traits)
                        if (trait != EntityTrait.Enemy && GameLoc.Get(trait + "_Keyword") != null) traits.Add(trait);
            }
            catch (Exception e) { CoreLog.Warning("FightBoard: reading an entity's traits failed: " + e.Message); }
            return traits;
        }

        /// <summary>The explanations of an entity's traits ("Bomb: Detonates and strikes adjacent tiles
        /// when destroyed..."), each its <c>_InfoLine</c>, an Explosive Shield's included.</summary>
        public static List<string> TraitLines(AEntityModel entity)
        {
            var lines = new List<string>();
            var traits = Traits(entity);
            if (ExplosiveShield(entity)) traits.Add(EntityTrait.ExplosiveShield);
            foreach (var trait in traits)
            {
                string line = GameLoc.Get(trait + "_InfoLine");
                if (line != null && !lines.Contains(line)) lines.Add(line);
            }
            if (ImmuneName(entity) != null)
            {
                string line = GameLoc.Get("Invulnerable_InfoLine");
                if (line != null && !lines.Contains(line)) lines.Add(line);
            }
            return lines;
        }

        /// <summary>Whether the entity's Activate (a Summon's once-a-turn ability, used by clicking it:
        /// Enter on its cell) is ready: the game marks it with the hidden <see cref="EntityTrait.Charged"/>
        /// trait, taken on use and given back by a recharge.</summary>
        public static bool ActivateReady(AEntityModel entity)
        {
            try
            {
                if (entity == null) return false;
                if (entity.HiddenTraits != null && entity.HiddenTraits.Contains(EntityTrait.Charged)) return true;
                return entity.Traits != null && entity.Traits.Contains(EntityTrait.Charged);
            }
            catch { return false; }
        }

        /// <summary>"channelling 1 Doom" while the game counts the invader as channelling
        /// (<see cref="GridModel.IsChannelingDoom"/>: the game's "WARNING! +1 Doom" over it), its charge
        /// being the Doom it will add; "channelling Doom" when it has none (Doom Ritual's pick). Null
        /// otherwise, when a charge is just a charge.</summary>
        public static string ChannelText(AEntityModel entity)
        {
            try
            {
                var grid = Fight.Model?.GridModel;
                if (entity == null || grid == null || entity.TryCast<EnemyEntityModel>() == null) return null;
                if (!grid.IsChannelingDoom(entity.ID.Cast<EntityID>())) return null;
                string doom = GameLoc.Get("DoomTitle_Misc") ?? "Doom";
                return entity.Charge > 0 ? Strings.FightChannelling(entity.Charge, doom) : Strings.FightChannellingDoom(doom);
            }
            catch (Exception e) { CoreLog.Warning("FightBoard: reading channelling failed: " + e.Message); return null; }
        }

        /// <summary>The game's explanation of a channelling invader: the bottom rows' channel, else the
        /// attack's ("cannot be stopped unless it is destroyed"); null when it is not channelling.</summary>
        public string ChannelLine(AEntityModel entity, int x, int y)
        {
            if (ChannelText(entity) == null) return null;
            return GameLoc.Get(Zone(x, y) == "doom" ? "TileDoom_InfoLine" : "AttackDoom_InfoLine");
        }

        /// <summary>"Immune" while strikes cannot harm the entity (<see cref="AEntityModel.IsInvulnerable"/>:
        /// the Mothership gains and loses it; the tile's hover icons draw it), else null. The game has
        /// no keyword for it, only its floating "Immune!" and the "Immunity: ..." info line, so the
        /// word is the floating text without its exclamation mark.</summary>
        public static string ImmuneName(AEntityModel entity)
        {
            try
            {
                if (entity == null || !entity.IsInvulnerable) return null;
                return GameLoc.Get("FloatTextImmune_Misc")?.Trim().TrimEnd('!', '！').TrimStart('¡').Trim();
            }
            catch (Exception e) { CoreLog.Warning("FightBoard: reading immunity failed: " + e.Message); return null; }
        }

        /// <summary>Whether the entity's shield is an Explosive Shield: shielded with the
        /// <see cref="EntityTrait.ExplosiveShield"/> trait, the test
        /// <see cref="EntityHoverIcons.SetHoverIcons"/> makes to draw that icon instead of the plain
        /// shield's (the trait has no name of its own: it is the Explosive Shield card's).</summary>
        public static bool ExplosiveShield(AEntityModel entity)
        {
            try { return entity != null && entity.IsShielded && entity.Traits != null && entity.Traits.Contains(EntityTrait.ExplosiveShield); }
            catch (Exception e)
            {
                CoreLog.Warning("FightBoard: reading a shield failed: " + e.Message);
                return false;
            }
        }

        /// <summary>"Explosive Shield" for an explosive shield, by the card that gives it; null for a
        /// plain shield or none.</summary>
        public static string ShieldName(AEntityModel entity) =>
            ExplosiveShield(entity) ? GameLoc.Get("ExplosiveShield_CardTitle") : null;

        /// <summary>"Migo 3, 2 health, shielded": name then state.</summary>
        public string Describe(AEntityModel entity)
        {
            string name = Name(entity);
            string state = State(entity);
            if (IsHaunted(entity)) state = state != null ? Strings.FightHaunted + ", " + state : Strings.FightHaunted;
            return state != null ? name + ", " + state : name;
        }

        /// <summary>The entity's description (its info panel's text), or null.</summary>
        public static string Description(AEntityModel entity)
        {
            var enemy = entity?.TryCast<EnemyEntityModel>();
            if (enemy != null) return GameLoc.EntityDescription(entity, enemy.EnemyID.EnemyName + "_EntityDesc", Fight.Controller);
            var item = entity?.TryCast<ItemEntityModel>();
            if (item != null) return GameLoc.EntityDescription(entity, item.ItemID.ItemName + "_EntityDesc", Fight.Controller);
            return null;
        }

        /// <summary>Who strikes the tile (named), empty when it is no Danger Zone; the game's "Danger
        /// Zone" alone when it is one but names no attacker.</summary>
        public List<string> Attackers(int x, int y)
        {
            var who = new List<string>();
            try
            {
                var c = MakeCoord(x, y);
                if (!_grid.IsCoordDangerZone(c)) return who;
                foreach (var id in _grid.WhoIsTargettingDangerZone(c, _model))
                {
                    var entity = _model.ModelItemDict[id.Cast<ID>()]?.TryCast<AEntityModel>();
                    string name = Name(entity);
                    if (name != null && !who.Contains(name)) who.Add(name);
                }
                if (who.Count == 0) who.Add(GameLoc.Get("DangerZoneText_Misc") ?? "Danger Zone");
            }
            catch (Exception e) { CoreLog.Warning("FightBoard: reading the danger at " + x + "," + y + " failed: " + e.Message); }
            return who;
        }

        /// <summary>"targeted by Migo 1, Migo 3" when something will strike the tile (the game's
        /// "Danger Zone" alone when it names no attacker), else null.</summary>
        public string Danger(int x, int y)
        {
            var who = Attackers(x, y);
            if (who.Count == 0) return null;
            string label = GameLoc.Get("DangerZoneText_Misc") ?? "Danger Zone";
            return who.Count == 1 && who[0] == label ? label : Strings.FightTargeted(string.Join(", ", who));
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

        /// <summary>The status's name: the game's for Cloud, Hole and Cracked (its tile panel names no
        /// others), Cracked's for DarkCracked (the same cracks drawn dark), the mod's for the rest: the
        /// Overseer fight's TrueHole column is the Rift its hands speak of.</summary>
        public static string StatusName(TileStatus status)
        {
            switch (status)
            {
                case TileStatus.TrueHole: return Strings.FightStatus("rift");
                case TileStatus.Nebula: return Strings.FightStatus("nebula");
                case TileStatus.Vortex: return Strings.FightStatus("vortex");
                case TileStatus.DarkCracked: return GameLoc.Get("CrackedTitle_Misc") ?? status.ToString();
                default: return GameLoc.Get(status + "Title_Misc") ?? status.ToString();
            }
        }

        /// <summary>What the status does: the game's line where it has one, the mod's for the Rift
        /// (<see cref="GridModel.IsMoveableCoord"/> refuses it; a push into it carries the entity a tile
        /// further) and Nebula (a strike there does nothing: <see cref="StrikeTileEffectTask"/> stops before
        /// even a Cloud's check). None for DarkCracked and Vortex, whose rules the game's code does not show plainly.</summary>
        public static string StatusDescription(TileStatus status)
        {
            switch (status)
            {
                case TileStatus.TrueHole: return Strings.FightStatusDescription("rift");
                case TileStatus.Nebula: return Strings.FightStatusDescription("nebula");
                case TileStatus.Vortex:
                case TileStatus.DarkCracked: return null;
                default: return GameLoc.Get(status + "TileStatus_Misc");
            }
        }

        /// <summary>"Pickup: Fire!" for a card lying on the tile, else null.</summary>
        public string Pickup(int x, int y)
        {
            if (!HasPickup(x, y)) return null;
            var card = PickupCard(x, y).Card;
            string label = PickupLabel;
            return card != null ? Strings.FightLabelled(label, Fight.CardName(card)) : label;
        }

        /// <summary>"Pickup", or "Booby Trap" while Kaia's Booby Traps make every pickup one (the game
        /// names it only in its explanation, "Booby Trap: When destroyed...").</summary>
        public static string PickupLabel => BoobyTraps() ? BoobyTrapWord() ?? PickupWord : PickupWord;

        private static string PickupWord => GameLoc.Get("Pickup_Keyword") ?? "Pickup";

        /// <summary>The game's explanation of a booby trap while the artifact is in play, else null.</summary>
        public static string BoobyTrapLine => BoobyTraps() ? GameLoc.Get("BoobyTrap_InfoLine") : null;

        private static string BoobyTrapWord()
        {
            string line = GameLoc.Get("BoobyTrap_InfoLine");
            int colon = line != null ? line.IndexOf(':') : -1;
            return colon > 0 ? line.Substring(0, colon).Trim() : null;
        }

        // Whether the fight has Booby Traps (an artifact, not destroyed).
        private static bool BoobyTraps()
        {
            try
            {
                var dict = Fight.View?.ArtifactViewDict;
                if (dict == null) return false;
                foreach (var kv in dict)
                {
                    var art = kv.Value?.ArtifactModel;
                    if (art != null && !art.Destroyed && art.ArtifactName == ArtifactName.BoobyTrap) return true;
                }
            }
            catch { }
            return false;
        }

        public bool HasPickup(int x, int y)
        {
            try { return _grid.PickupMap.ContainsKey(MakeCoord(x, y)); }
            catch { return false; }
        }

        /// <summary>The card lying on the tile and its view (the hand's card views hold it while it
        /// is on the grid); nulls when none.</summary>
        public (CardModel Card, CardView View) PickupCard(int x, int y)
        {
            try
            {
                var map = _grid.PickupMap;
                var c = MakeCoord(x, y);
                if (!map.ContainsKey(c)) return (null, null);
                var view = Fight.View?.CardPlayView?.GetCardView(map[c]);
                return (view?.CardModel, view);
            }
            catch { return (null, null); }
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
