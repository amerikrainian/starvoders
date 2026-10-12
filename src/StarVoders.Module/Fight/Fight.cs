using System;
using System.Collections.Generic;
using StarVaders;
using StarVoders.Core;
using StarVoders.Module.Interop;
using StarVoders.Module.UI;

namespace StarVoders.Module.Fight
{
    /// <summary>
    /// The fight (<c>EncounterScene</c>): its controller and model, and the game's own click. The
    /// game's keyboard plays through its input handler: hover an item
    /// (<see cref="InputHandler.OnHoverViewItemByKey"/>), then <see cref="EncounterInputHandler.ClickViewItems"/>,
    /// which plays a card, presses a button, or answers a running target selection with the hovered
    /// tile or arrow. Every node here clicks that way, so a click means what it means to the game.
    /// </summary>
    internal static class Fight
    {
        public static EncounterController Controller
        {
            get
            {
                var c = GameScenes.Controller<EncounterController>();
                return c != null && c.EncounterModel != null ? c : null;
            }
        }

        public static EncounterModel Model => Controller?.EncounterModel;
        public static EncounterView View => Controller?.EncounterView;

        /// <summary>Hover the item as the game's keys do: its highlight and info panel follow.</summary>
        public static void Hover(AViewItem item)
        {
            var handler = Controller?.EncounterInputHandler;
            if (handler == null || item == null) return;
            try { handler.OnHoverViewItemByKey(item); }
            catch (Exception e) { CoreLog.Warning("Fight: hovering " + item.gameObject.name + " failed: " + e.Message); }
        }

        /// <summary>The game's click on the item: hover it, then click what is hovered.</summary>
        public static void Click(AViewItem item)
        {
            var handler = Controller?.EncounterInputHandler;
            if (handler == null || item == null) return;
            try
            {
                handler.OnHoverViewItemByKey(item);
                handler.ClickViewItems();
            }
            catch (Exception e) { CoreLog.Warning("Fight: clicking " + item.gameObject.name + " failed: " + e.Message); }
        }

        /// <summary>The cards in hand, in hand order, those already being played left out.</summary>
        public static List<CardView> Hand()
        {
            var cards = new List<CardView>();
            var model = Model;
            var play = View?.CardPlayView;
            if (model == null || play == null) return cards;
            try
            {
                foreach (var id in model.CardPlayModel.GetCardsInHandButNotBeingPlayed())
                {
                    var view = play.GetCardView(id);
                    if (view != null) cards.Add(view);
                }
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the hand failed: " + e.Message); }
            return cards;
        }

        public static string CardName(CardModel card) => card != null ? GameLoc.Get(card.CardName + "_CardTitle") ?? card.CardName.ToString() : null;

        /// <summary>The card's description with its live numbers, as its info panel shows it; none when
        /// its upgrade replaces it (<see cref="AComponent.SkipNormalDescription"/>: the panel blanks it).</summary>
        public static string CardDescription(CardModel card)
        {
            if (card == null) return null;
            try
            {
                if (Upgrade(card) != null && Upgrade(card).SkipNormalDescription) return null;
                string raw = LocalizationUtils.LocalizeSynchronous(card.CardName + "_CardDesc");
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("No translation for", StringComparison.Ordinal)) return null;
                // As the card's panel: out of a fight, with no encounter, it drops a counter's "([#])".
                return GameText.Clean(LocalizationUtils.GetModifiedDescriptions(raw, card, Controller));
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: describing " + card.CardName + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The card's upgrade, or null: a card without one still carries a component named
        /// None (the game's <see cref="CardModel.HasComponent"/> tests the name too).</summary>
        private static AComponent Upgrade(CardModel card) => card != null && card.HasComponent ? card.Component : null;

        /// <summary>The card's upgrade (its <see cref="AComponent"/>) by the title its panel draws under
        /// the card's ("Unstable"), or null for a card without one.</summary>
        public static string CardUpgrade(CardModel card)
        {
            var component = Upgrade(card);
            return component != null ? GameLoc.Get(component.ComponentName + "_CompTitle") ?? component.ComponentName.ToString() : null;
        }

        /// <summary>What the card's upgrade does, with its live numbers, as the panel's component line
        /// shows it ("+1 Repeat and +1 Heat cost when played.").</summary>
        public static string CardUpgradeDescription(CardModel card)
        {
            var component = Upgrade(card);
            if (component == null) return null;
            try
            {
                string raw = LocalizationUtils.LocalizeSynchronous(component.ComponentName + "_CompDesc");
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("No translation for", StringComparison.Ordinal)) return null;
                return GameText.Clean(LocalizationUtils.GetModifiedDescriptions(raw, component.ComponentName, Controller));
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: describing the upgrade of " + card.CardName + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The card's traits as its panel lists them: its own, then the ones its upgrade adds
        /// (Broken adds Junk), each by its <c>_Keyword</c> name ("Move, Junk").</summary>
        public static string CardTraits(CardModel card)
        {
            var names = new List<string>();
            foreach (var trait in Traits(card))
            {
                string name = GameLoc.Get(trait + "_Keyword");
                if (name != null && !names.Contains(name)) names.Add(name);
            }
            return names.Count > 0 ? string.Join(", ", names) : null;
        }

        private static List<CardTrait> Traits(CardModel card)
        {
            var traits = new List<CardTrait>();
            try
            {
                foreach (var set in new[] { card?.Traits, Upgrade(card)?.AddedTraits })
                    if (set != null)
                        foreach (var trait in set)
                            if (!traits.Contains(trait)) traits.Add(trait);
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the traits of " + card?.CardName + " failed: " + e.Message); }
            return traits;
        }

        /// <summary>What a card's more info shows, its upgrade's included: its traits' explanations
        /// (Junk's "Junk cards are usually gained when you are struck."), the keyword explanations
        /// (Unstable adds Repeat and Heat), then the cards, items and invaders it names ("Jam: Purge
        /// this card." for Broken, the Nova Bomb for Nova Bomb).</summary>
        public static List<string> CardInfoLines(CardModel card)
        {
            var lines = new List<string>();
            var component = Upgrade(card);
            void Add(string line) { if (line != null && !lines.Contains(line)) lines.Add(line); }
            foreach (var trait in Traits(card)) Add(GameLoc.Get(trait + "_InfoLine"));
            try
            {
                MoreInfo(Add, card?.MoreInfoWordNames, card?.MoreInfoCardNames, card?.MoreInfoItemNames, card?.MoreInfoEnemyNames);
                if (component != null)
                    MoreInfo(Add, component.MoreInfoWordNames, component.MoreInfoCardNames, component.MoreInfoItemNames, component.MoreInfoEnemyNames);
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the more info of " + card?.CardName + " failed: " + e.Message); }
            return lines;
        }

        /// <summary>One more-info set's lines, as the game's more info panel lists them: the keyword
        /// explanations, then each card, item and invader it names with its description.</summary>
        private static void MoreInfo(Action<string> add,
            Il2CppSystem.Collections.Generic.HashSet<MoreInfoWordName> words,
            Il2CppSystem.Collections.Generic.HashSet<CardName> cards,
            Il2CppSystem.Collections.Generic.HashSet<ItemName> items,
            Il2CppSystem.Collections.Generic.HashSet<EnemyName> enemies)
        {
            foreach (var line in InfoLines(words)) add(line);
            if (cards != null)
                foreach (var name in cards) add(Named(GameLoc.Get(name + "_CardTitle") ?? name.ToString(), GameLoc.Get(name + "_CardDesc")));
            if (items != null)
                foreach (var name in items) add(Named(GameLoc.Get(name + "_EntityTitle") ?? name.ToString(), GameLoc.Get(name + "_EntityDesc")));
            if (enemies != null)
                foreach (var name in enemies) add(Named(GameLoc.EnemyName(name) ?? name.ToString(), GameLoc.Get(name + "_EntityDesc")));
        }

        // ---- artifacts ----

        /// <summary>The artifact's description as its panel shows it: a spell's "[#1#]" numbers filled
        /// by <see cref="LocalizationUtils.GetModifiedDescriptions(string, ArtifactModel,
        /// EncounterController)"/>, which the panel always runs (with the fight's controller in one).</summary>
        public static string ArtifactDescription(ArtifactModel model)
        {
            if (model == null) return null;
            try
            {
                string raw = LocalizationUtils.LocalizeSynchronous(model.ArtifactName + "_ArtiDesc");
                if (string.IsNullOrEmpty(raw) || raw.StartsWith("No translation for", StringComparison.Ordinal)) return null;
                return GameText.Clean(LocalizationUtils.GetModifiedDescriptions(raw, model, Controller));
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: describing " + model.ArtifactName + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>The panel's trait line: "Spell" for a spell, else "Artifact".</summary>
        public static string ArtifactTrait(ArtifactModel model) =>
            model == null ? null : GameLoc.Get((model.IsSpell ? "Spell" : "Artifact") + "_Keyword");

        /// <summary>A spell's "Cooldown: 3" (the game's <c>Cooldown_Misc</c> with its base cooldown), as
        /// its panel draws it; null for other artifacts.</summary>
        public static string ArtifactCooldown(ArtifactModel model)
        {
            if (model == null || !model.IsSpell) return null;
            string text = GameLoc.Get("Cooldown_Misc");
            return text != null ? text.Replace("[X]", model.BaseCooldown.ToString()) : null;
        }

        /// <summary>What an artifact's more info shows: a spell's "Spell: A type of artifact that can be
        /// casted...", then its keywords, cards, items and invaders.</summary>
        public static List<string> ArtifactInfoLines(ArtifactModel model)
        {
            var lines = new List<string>();
            if (model == null) return lines;
            void Add(string line) { if (line != null && !lines.Contains(line)) lines.Add(line); }
            try
            {
                if (model.IsSpell) Add(GameLoc.Get("Spell_InfoLine"));
                MoreInfo(Add, model.MoreInfoWordNames, model.MoreInfoCardNames, model.MoreInfoItemNames, model.MoreInfoEnemyNames);
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the more info of " + model.ArtifactName + " failed: " + e.Message); }
            return lines;
        }

        private static string Named(string name, string description) => description != null ? name + ": " + description : name;

        /// <summary>The cost as a card's art draws it ("1 Jam" for a Broken card the model still costs
        /// 1 Heat), with every play modifier in: the cost icons under the view's
        /// <see cref="CardImageView"/>, one per point or a large one with a number.
        /// <see cref="CardImageView.SetCostIcons"/> draws max(cost, original) small icons, those past the
        /// modded cost as <see cref="CostIconEffectType.Minus"/> (a point taken off: Chilled, Dark
        /// Power) and those past the original as Plus (Confusion, Overclock); a large icon carries the
        /// modded cost. So a Minus icon is not a point, and a card drawn with only those is free ("Free").
        /// Null when none is drawn (no cost, or the view not shown).</summary>
        public static string DrawnCost(UnityEngine.Component view)
        {
            try
            {
                var image = view.GetComponentInChildren<CardImageView>(false);
                var group = image != null ? image._costIconsLayoutGroup : null;
                if (group == null || !group.activeInHierarchy) return null;
                var counts = new List<KeyValuePair<CostType, int>>();
                void Count(CostType type, int n)
                {
                    for (int i = 0; i < counts.Count; i++)
                        if (counts[i].Key == type) { counts[i] = new KeyValuePair<CostType, int>(type, counts[i].Value + n); return; }
                    counts.Add(new KeyValuePair<CostType, int>(type, n));
                }
                bool reduced = false;
                foreach (var icon in group.GetComponentsInChildren<CostIcon>(false))
                {
                    if (icon._costIconEffectType == CostIconEffectType.Minus) reduced = true;
                    else Count(icon._costType, 1);
                }
                foreach (var icon in group.GetComponentsInChildren<LargeCostIcon>(false)) Count(icon._costType, icon._number);
                if (counts.Count == 0) return reduced ? GameLoc.Get("Free_Misc") ?? "Free" : null;
                var parts = new List<string>();
                foreach (var kv in counts) parts.Add(StarVoders.Core.Strings.Strings.FightCost(kv.Value, CostWord(kv.Key)));
                return string.Join(", ", parts);
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: reading a card's cost icons failed: " + e.Message);
                return null;
            }
        }

        /// <summary>How many times a hand card plays as it draws it beside its art ("x3"), its own
        /// repeats and the modifiers' (Overclock's +2) together: <see cref="CardView.SetRepeatText"/>
        /// from the modded <see cref="ArgKey.RepeatAmount"/> + 1, blank for a single play. Null then.</summary>
        public static string DrawnRepeat(CardView view)
        {
            try
            {
                var text = view._repeatText;
                return text != null && text.gameObject.activeInHierarchy ? GameText.Of(text) : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: reading a card's repeat text failed: " + e.Message);
                return null;
            }
        }

        /// <summary>What play modifiers add to a hand card beyond its cost and repeats, which the art
        /// draws only as a glow (<see cref="CardView.UpdateView"/> drives the purge and flow indicators
        /// from <see cref="CardModel.GetModdedCardArgs"/>): "Purge" (Chaos Drive), "Flow" (Slide),
        /// each when the modded args have it and the card's own (<see cref="CardModel.GetDefaultCardArgs"/>)
        /// do not. Null when a modifier adds neither, or out of a fight.</summary>
        public static string ModifierTraits(CardModel card)
        {
            var engine = Controller?.TaskEngine;
            if (card == null || engine == null) return null;
            try
            {
                var modded = card.GetModdedCardArgs(engine, false);
                var own = card.GetDefaultCardArgs();
                bool Added(ArgKey key) => Flag(modded, key) && !Flag(own, key);
                var names = new List<string>();
                if (Added(ArgKey.IsPurged)) names.Add(GameLoc.Keyword("Purge_InfoLine") ?? "Purge");
                if (Added(ArgKey.IsFlow)) names.Add(GameLoc.Get("Flow_Keyword") ?? "Flow");
                return names.Count > 0 ? string.Join(", ", names) : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: reading the modifiers of " + card.CardName + " failed: " + e.Message);
                return null;
            }
        }

        /// <summary>How a hand card would pay, where the art marks it. "not enough Power" (or Mana) while
        /// the game's own <see cref="CardView.WillUnpowered"/> holds: the modded cost is more than Power
        /// and Battery together (Mana for the Keeper), drawn as darkened cost icons. Else "spends
        /// Battery" when it would take any, as <see cref="PlayCardCostTask"/> pays and marks the card
        /// (<see cref="CardView.SetBatteryPowerIndicator"/>, in its preview only, which a keyboard hover
        /// does not run): from Battery first for <see cref="ArgKey.IsCellOnly"/> or
        /// <see cref="ArgKey.IsCellFirst"/>, else from Power with what Power lacks from Battery. A free
        /// card pays nothing. Null otherwise, or out of a fight.</summary>
        public static string PaymentNote(CardView view)
        {
            var model = Model;
            var engine = Controller?.TaskEngine;
            var card = view?.CardModel;
            if (card == null || model == null || engine == null) return null;
            try
            {
                var args = card.GetModdedCardArgs(engine, false);
                if (Flag(args, ArgKey.IsFree)) return null;
                int power = Number(args, ArgKey.PowerCost), mana = Number(args, ArgKey.ManaCost);
                if (view.WillUnpowered(args))
                    return StarVoders.Core.Strings.Strings.FightNotEnough(power > 0 || mana == 0 ? CostWord(CostType.Power) : CostWord(CostType.Mana));
                if (power <= 0) return null;
                int cells = model.Values[EncounterValue.PowerCell];
                bool spends = Flag(args, ArgKey.IsCellOnly) || Flag(args, ArgKey.IsCellFirst)
                    ? cells > 0
                    : power > model.Values[EncounterValue.Power] && cells > 0;
                return spends ? StarVoders.Core.Strings.Strings.FightSpends(GameLoc.Get("Battery_Keyword") ?? "Battery") : null;
            }
            catch (Exception e)
            {
                CoreLog.Warning("Fight: reading how " + card.CardName + " pays failed: " + e.Message);
                return null;
            }
        }

        private static int Number(Il2CppSystem.Collections.Generic.Dictionary<ArgKey, Il2CppSystem.Object> args, ArgKey key) =>
            args != null && args.ContainsKey(key) && args[key] != null ? args[key].Unbox<int>() : 0;

        private static bool Flag(Il2CppSystem.Collections.Generic.Dictionary<ArgKey, Il2CppSystem.Object> args, ArgKey key) =>
            args != null && args.ContainsKey(key) && args[key] != null && args[key].Unbox<bool>();

        private static string CostWord(CostType type)
        {
            switch (type)
            {
                case CostType.Heat: return GameLoc.Get("HeatTitle_Misc") ?? "Heat";
                case CostType.Power: return GameLoc.Get("PowerTitle_Misc") ?? "Power";
                case CostType.Mana: return GameLoc.Get("ManaTitle_Misc") ?? "Mana";
                case CostType.Jam: return GameLoc.Get("Jam_CardTitle") ?? "Jam";
                case CostType.Neutral: return GameLoc.Get("Neutral_Misc") ?? "Neutral";
                default: return type.ToString();
            }
        }

        /// <summary>The keyword explanations a card or entity carries (its info panel's lines:
        /// "Danger Zone: Tiles that will be struck..."), from the game's <c>*_InfoLine</c> strings.</summary>
        public static List<string> InfoLines(Il2CppSystem.Collections.Generic.HashSet<MoreInfoWordName> words)
        {
            var lines = new List<string>();
            if (words == null) return lines;
            try
            {
                foreach (var word in words)
                {
                    string line = GameLoc.Get(word + "_InfoLine");
                    if (line != null && !lines.Contains(line)) lines.Add(line);
                }
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the info lines failed: " + e.Message); }
            return lines;
        }

        // ---- the fight's figures ----

        /// <summary>One of the fight's figures as the model holds it (<see cref="EncounterModel.Values"/>),
        /// never as a panel draws it: hovering a card or End Turn makes the panels preview what it
        /// would do ("Doom 5/5" while the model is at 3).</summary>
        public static int? Value(EncounterValue key)
        {
            var values = Model?.Values;
            try { return values != null && values.ContainsKey(key) ? values[key] : (int?)null; }
            catch { return null; }
        }

        /// <summary>The Doom that loses the fight: the "/5" the Doom panel draws after its figure,
        /// which a preview never changes.</summary>
        public static int? DoomMax()
        {
            string text = GameText.Of(View?.DoomView?.DoomText);
            int slash = text != null ? text.LastIndexOf('/') : -1;
            return slash >= 0 && int.TryParse(text.Substring(slash + 1).Trim(), out int max) ? max : (int?)null;
        }

        /// <summary>"Doom 3/5", "Heat 1/3", "Power 2/4", "Mana 3/5": the game's title and the model's
        /// figure over its maximum; null when the fight has no such figure.</summary>
        public static string Figure(string which)
        {
            int? value, max;
            switch (which)
            {
                case "Doom": value = Value(EncounterValue.Doom); max = DoomMax(); break;
                case "Heat": value = Value(EncounterValue.Heat); max = Value(EncounterValue.MaxHeat); break;
                case "Power": value = Value(EncounterValue.Power); max = Value(EncounterValue.MaxPower); break;
                default: value = Value(EncounterValue.Mana); max = Value(EncounterValue.MaxMana); break;
            }
            if (value == null) return null;
            string figure = max.HasValue && max.Value > 0 ? Core.Strings.Strings.Position(value.Value, max.Value) : value.Value.ToString();
            string title = GameLoc.Get(which + "Title_Misc");
            return title != null ? title + " " + figure : figure;
        }

        /// <summary>The player's bombs on the grid out of the limit, "Bombs 1/3"
        /// (<see cref="GridModel.PlayerBombList"/>). The limit is the 3 the game writes into its
        /// counter (<see cref="BombLimitView.SetBombAmount"/>) and tests in AddToPlayerBombListTask,
        /// both constants.</summary>
        public static string Bombs()
        {
            var bombs = Model?.GridModel?.PlayerBombList;
            if (bombs == null) return null;
            string figure = Core.Strings.Strings.Position(bombs.Count, BombLimit);
            string title = GameLoc.Get("Bombs_Keyword");
            return title != null ? title + " " + figure : figure;
        }

        private const int BombLimit = 3;

        // ---- target selection ----

        /// <summary>Whether the game is asking for a target (a card being played waits on it).</summary>
        public static bool Selecting
        {
            get
            {
                var sc = Controller?.SelectionController;
                return sc != null && sc.IsSelecting;
            }
        }

        /// <summary>The view items the running selection accepts, by the game's own test
        /// (<see cref="SelectionController.IsSatisfied"/>), in the encounter's view order.</summary>
        public static List<AEncounterViewItem> Targets()
        {
            var targets = new List<AEncounterViewItem>();
            var c = Controller;
            var sc = c?.SelectionController;
            if (sc == null || !sc.IsSelecting) return targets;
            try
            {
                foreach (var item in c.EncounterView.ViewsItems)
                {
                    if (item == null || !item.gameObject.activeInHierarchy) continue;
                    bool ok;
                    try { ok = sc.IsSatisfied(item); } catch { ok = false; }
                    if (ok) targets.Add(item);
                }
            }
            catch (Exception e) { CoreLog.Warning("Fight: listing the targets failed: " + e.Message); }
            return targets;
        }

        /// <summary>What a running selection accepts, sorted by where it is answered: tiles, the play
        /// area ("click the grid to play"), direction arrows: all answered on the board; anything
        /// else (cards to choose) only in a list.</summary>
        public sealed class TargetSet
        {
            public readonly List<TileView> Tiles = new List<TileView>();
            public PlayAreaView PlayArea;
            public readonly List<DirectionSelectionArrow> Arrows = new List<DirectionSelectionArrow>();
            public readonly List<AEncounterViewItem> Others = new List<AEncounterViewItem>();
            /// <summary>The tile the arrows point from (the selection's base, else the mech), 0-based.</summary>
            public (int X, int Y)? Origin;

            public bool OnBoard => Tiles.Count > 0 || PlayArea != null || Arrows.Count > 0;

            public bool HasTile(int x, int y)
            {
                foreach (var t in Tiles)
                {
                    var c = t.TileModel.Coord;
                    if (c.x == x && c.y == y) return true;
                }
                return false;
            }

            /// <summary>The arrow pointing from <see cref="Origin"/> toward the tile, when the tile lies
            /// straight or diagonally out from it and that arrow is a target; else null.</summary>
            public DirectionSelectionArrow ArrowToward(int x, int y)
            {
                if (Arrows.Count == 0 || Origin == null) return null;
                int dx = x - Origin.Value.X, dy = y - Origin.Value.Y;
                if (dx == 0 && dy == 0) return null;
                if (dx != 0 && dy != 0 && Math.Abs(dx) != Math.Abs(dy)) return null;
                string compass = (dy > 0 ? "N" : dy < 0 ? "S" : "") + (dx > 0 ? "E" : dx < 0 ? "W" : "");
                foreach (var a in Arrows)
                    if (a.Direction.ToString() == compass) return a;
                return null;
            }
        }

        // Read once per frame at most: a board render asks for every cell (never kept past the frame).
        private static int _targetsFrame = -1;
        private static TargetSet _targets;

        /// <summary>The running selection's targets, sorted; null when nothing is being chosen.</summary>
        public static TargetSet ReadTargets()
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _targetsFrame) return _targets;
            _targetsFrame = frame;
            _targets = ReadTargetsNow();
            return _targets;
        }

        private static TargetSet ReadTargetsNow()
        {
            var targets = Targets();
            if (targets.Count == 0) return null;
            var set = new TargetSet();
            foreach (var item in targets)
            {
                var tile = item.TryCast<TileView>();
                if (tile != null) { set.Tiles.Add(tile); continue; }
                var area = item.TryCast<PlayAreaView>();
                if (area != null) { set.PlayArea = area; continue; }
                var arrow = item.TryCast<DirectionSelectionArrow>();
                if (arrow != null) { set.Arrows.Add(arrow); continue; }
                set.Others.Add(item);
            }
            if (set.Arrows.Count > 0) set.Origin = ArrowOrigin();
            return set;
        }

        // ---- the game's strike marks ----

        // Read once per frame at most, as the targets.
        private static int _strikeFrame = -1;
        private static HashSet<(int X, int Y)> _strike;

        /// <summary>The tiles (0-based) the game marks as about to be struck: what a card's preview will
        /// strike (Flamethrower's cone while its burn is chosen; <see cref="StrikeTileEffectTask"/> run
        /// in preview mode calls <see cref="TileView.EnableTargetTile"/>), a delayed artillery strike's
        /// tiles. The mark is the tile's target overlay being enabled; previews are reset by turning it
        /// off again.</summary>
        public static HashSet<(int X, int Y)> StrikeTiles()
        {
            int frame = UnityEngine.Time.frameCount;
            if (frame == _strikeFrame) return _strike;
            _strikeFrame = frame;
            var tiles = new HashSet<(int X, int Y)>();
            var dict = View?.GridView?.TileViewDict;
            try
            {
                if (dict != null)
                    foreach (var kv in dict)
                    {
                        var tile = kv.Value;
                        if (tile == null) continue;
                        var back = tile._targetPreviewRenderer;
                        var front = tile._targetFrontPreviewRenderer;
                        if ((back != null && back.enabled) || (front != null && front.enabled)) tiles.Add((kv.Key.x, kv.Key.y));
                    }
            }
            catch (Exception e) { CoreLog.Warning("Fight: reading the strike marks failed: " + e.Message); }
            _strike = tiles;
            return tiles;
        }

        public static bool Strike(int x, int y) => StrikeTiles().Contains((x, y));

        // The selection's base tile when it has one, else the mech's.
        private static (int X, int Y)? ArrowOrigin()
        {
            var c = Controller;
            try
            {
                var baseCoord = c?.SelectionController?.CurrentSelection?.BaseCoord;
                if (baseCoord != null)
                {
                    var coord = baseCoord.Unbox<Coord>();
                    return (coord.x, coord.y);
                }
            }
            catch { }
            try
            {
                var p = Model.GridModel.GetPlayerCoord();
                return (p.x, p.y);
            }
            catch { return null; }
        }

        /// <summary>The selection's prompt as the right-hand screen states it ("Click grid to play").</summary>
        public static string Prompt()
        {
            var screen = View?.ScreenView;
            var prompt = screen != null ? screen.SelectionPrompt : null;
            return prompt != null && prompt.gameObject.activeInHierarchy ? GameText.Of(prompt) : null;
        }

        public static bool Cancellable
        {
            get
            {
                var sc = Controller?.SelectionController;
                try { return sc != null && sc.IsSelecting && sc.IsCancellable(); }
                catch { return false; }
            }
        }

        /// <summary>Cancel the selection as the game's Cancel key does: the selection, then the
        /// previews, info panel and hover it left behind.</summary>
        public static void Cancel()
        {
            var c = Controller;
            if (c == null || !Cancellable) return;
            try
            {
                c.SelectionController.Cancel(false);
                c.PreviewController?.ResetAllPreviews();
                c.EncounterView.DisplayBoard?.Hide();
                c.EncounterInputHandler?.ResetHover(-1);
            }
            catch (Exception e) { CoreLog.Warning("Fight: cancelling the selection failed: " + e.Message); }
        }
    }
}
