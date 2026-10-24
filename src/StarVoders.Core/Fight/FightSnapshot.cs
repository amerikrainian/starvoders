using System.Collections.Generic;

namespace StarVoders.Core.Fight
{
    /// <summary>One entity on the board, as the events compare it: a stable key for the fight (the
    /// game's id), its spoken name, its tile (1-based, row 1 at the bottom) and its state.</summary>
    public sealed class EntitySnap
    {
        public string Key;
        public string Name;
        /// <summary>The name without its number ("Shooter" of "Shooter 9") and the number, 0 when
        /// the name has none (an item, an invader without kin); destructions are grouped by kind.
        /// A null kind groups by <see cref="Name"/>.</summary>
        public string Kind;
        public int Number;
        public int X, Y;
        public int Health;
        public bool Shielded, Shocked;
        /// <summary>The shield's name when it is not a plain one ("Explosive Shield"), else null.</summary>
        public string Shield;
        /// <summary>The game's word for immunity ("Immune") while strikes cannot harm the entity, else null.</summary>
        public string Immune;
        public int Charge;
        /// <summary>"channelling 1 Doom" while the invader channels Doom (its charge is then the Doom it
        /// adds, said here instead), else null.</summary>
        public string Channel;
        /// <summary>Health changes are not spoken for the mech (it has none to lose).</summary>
        public bool IsMech;
    }

    /// <summary>A tile something is about to strike (a Danger Zone) and who strikes it, 1-based.</summary>
    public sealed class DangerSnap
    {
        public string Who;
        public int X, Y;
    }

    /// <summary>A card lying on the grid: the card's id (as <see cref="CardSnap.Key"/>), its spoken
    /// name ("Pickup: Dash") and its tile, 1-based.</summary>
    public sealed class PickupSnap
    {
        public string Key;
        public string Name;
        public int X, Y;
    }

    /// <summary>A card in hand: the game's id (two Fire! cards differ) and its name.</summary>
    public sealed class CardSnap
    {
        public string Key;
        public string Name;
        /// <summary>For a card in a pile: the pile's title where a card added to it is said ("Draw
        /// Pile"), null for the piles kept only to know the card (purged, pickups).</summary>
        public string Pile;
    }

    /// <summary>A spell slot's spell: its id, name, and whether it is recharging (its cooldown drawn).</summary>
    public sealed class SpellSnap
    {
        public string Key;
        public string Name;
        public bool Recharging;
    }

    /// <summary>The fight at one settled moment, what <see cref="FightDiff"/> compares. Doom and
    /// the energy (Heat; Power and Battery; Mana) are kept as they read ("Doom 1/5"), so a change
    /// is said as the key reads it; null where the fight has no such panel.</summary>
    public sealed class FightSnapshot
    {
        public int Turn, Turns;
        /// <summary>The turn and the enemy phase as the game's banner names them ("Player Turn 3, 2
        /// Turns Left", "Invader Turn"); null falls back to the mod's "Turn 3/8" and "Enemy turn".</summary>
        public string TurnText, EnemyTurnText;
        /// <summary>The board's size (0 when unknown), for whole rows and columns.</summary>
        public int Columns, Rows;
        public bool PlayerTurn;
        public string Doom, Heat, Power, Battery, Mana;
        /// <summary>A boss kept off the board (the Overseer): its drawn health as read ("The Overseer
        /// 5/6"), null while none is drawn; its phase as read ("The Overseer, phase 2"), null when unknown.</summary>
        public string Boss, BossPhase;
        /// <summary>The combo (invaders destroyed this turn, the Stars multiplier) and how it reads
        /// ("Combo! 4, Nice!"): said when the count rises, never when it drops back to 0.</summary>
        public int Combo;
        public string ComboText;
        public List<EntitySnap> Entities = new List<EntitySnap>();
        public List<CardSnap> Hand = new List<CardSnap>();
        public List<DangerSnap> Dangers = new List<DangerSnap>();
        public List<PickupSnap> Pickups = new List<PickupSnap>();
        /// <summary>The fight's artifacts and spells (the game's id as the key), gained or lost mid-fight
        /// (Booby Traps from a card, a spell used up).</summary>
        public List<CardSnap> Artifacts = new List<CardSnap>();
        /// <summary>The cards in every pile but the hand, each with its <see cref="CardSnap.Pile"/>.</summary>
        public List<CardSnap> Piles = new List<CardSnap>();
        public List<SpellSnap> Spells = new List<SpellSnap>();
    }
}
