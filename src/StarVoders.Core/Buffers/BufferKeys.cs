namespace StarVoders.Core.Buffers
{
    /// <summary>
    /// The registered buffer keys, in cycling order. The ui buffer carries the focused control's own
    /// lines (<see cref="NodeLines"/>); the hand, player and enemies buffers read the game whatever
    /// has focus (the fight's hand as its cards are announced; the player's Doom, energy and bombs, or
    /// in a run between fights its Doom and Stars; the invaders on the board), and so does the combat
    /// buffer, the fight's events log. The hero, quest, item, relic and party keys are guildrun's,
    /// unused here: <see cref="Graph.NodeVtable.SideLines"/> answers by key.
    /// </summary>
    public static class BufferKeys
    {
        public const string Ui = "ui";
        public const string Hand = "hand";
        public const string Player = "player";
        public const string Hero = "hero";
        public const string Quest = "quest";
        /// <summary>Not a buffer: the same quests without their reward lines, what the quests glance
        /// key speaks. A control answers it through <see cref="Graph.NodeVtable.SideLines"/> as it
        /// does a buffer's key.</summary>
        public const string QuestBrief = "quest.brief";
        public const string Item = "item";
        public const string Relic = "relic";
        public const string Party = "party";
        public const string Enemies = "enemies";
        public const string Combat = "combat";
    }
}
