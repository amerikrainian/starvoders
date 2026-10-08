using System.Collections.Generic;

namespace StarVadersAccess.Core.Fight
{
    /// <summary>One entity on the board, as the events compare it: a stable key for the fight (the
    /// game's id), its spoken name, its tile (1-based, row 1 at the bottom) and its state.</summary>
    public sealed class EntitySnap
    {
        public string Key;
        public string Name;
        public int X, Y;
        public int Health;
        public bool Shielded, Shocked;
        public int Charge;
        /// <summary>Health changes are not spoken for the mech (it has none to lose).</summary>
        public bool IsMech;
    }

    /// <summary>A tile something is about to strike (a Danger Zone) and who strikes it, 1-based.</summary>
    public sealed class DangerSnap
    {
        public string Who;
        public int X, Y;
    }

    /// <summary>A card in hand: the game's id (two Fire! cards differ) and its name.</summary>
    public sealed class CardSnap
    {
        public string Key;
        public string Name;
    }

    /// <summary>The fight at one settled moment, what <see cref="FightDiff"/> compares. Doom and
    /// Heat are kept as the panel draws them ("1/5"), with their titles, so a change reads as the
    /// panel would.</summary>
    public sealed class FightSnapshot
    {
        public int Turn, Turns;
        public bool PlayerTurn;
        public string DoomTitle, Doom;
        public string HeatTitle, Heat;
        public List<EntitySnap> Entities = new List<EntitySnap>();
        public List<CardSnap> Hand = new List<CardSnap>();
        public List<DangerSnap> Dangers = new List<DangerSnap>();
    }
}
