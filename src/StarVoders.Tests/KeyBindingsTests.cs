using System;
using System.Collections.Generic;
using StarVoders.Core.Input;
using Xunit;

namespace StarVoders.Tests
{
    // The mod's own key rebinding: only changes against the registered defaults are saved, a saved
    // form replaces an action's keys, protected actions keep one, and conflicts are within a
    // category (or with Global).
    public class KeyBindingsTests : IDisposable
    {
        // A key combination by name ("Ctrl+M"), saved as "fake:Ctrl+M".
        private sealed class Key : InputBinding
        {
            private readonly string _name;
            public Key(string name) => _name = name;
            public override string DisplayName => _name;
            public override bool JustPressed() => false;
            public override bool Held() => false;
            public override bool Released() => false;
            public override string Type => "fake";
            public override string Serialize() => _name;
        }

        // A binding that follows someone else's keys: not saveable, so its action is not rebindable.
        private sealed class Followed : InputBinding
        {
            public override string DisplayName => "E";
            public override bool JustPressed() => false;
            public override bool Held() => false;
            public override bool Released() => false;
            public override string Type => "followed";
            public override string Serialize() => "EndTurn";
        }

        private string _saved;

        public KeyBindingsTests()
        {
            InputManager.Clear();
            KeyBindings.Clear();
            InputBinding.RegisterType("fake", data => new Key(data));
            InputManager.Register("mod.menu", "Mod menu", InputCategory.Global).AddBinding(new Key("Ctrl+Shift+M"));
            InputManager.Register("ui.back", "Back", InputCategory.UI).AddBinding(new Key("Escape"));
            InputManager.Register("buffer.next", "Next buffer", InputCategory.UI).AddBinding(new Key("Ctrl+Right"));
            InputManager.Register("fight.coords", "Coordinates", InputCategory.Game).AddBinding(new Key("C"));
            InputManager.Register("fight.turn", "Turn", InputCategory.Game).AddBinding(new Key("T"));
            InputManager.Register("fight.endturn", "End turn", InputCategory.Game).AddBinding(new Followed());
            KeyBindings.Snapshot();
            KeyBindings.Persist = s => _saved = s;
        }

        public void Dispose()
        {
            KeyBindings.Clear();
            InputManager.Clear();
        }

        private static InputAction A(string key) => InputManager.Find(key);

        [Fact]
        public void Only_changed_actions_are_saved_and_a_return_to_the_defaults_saves_nothing()
        {
            KeyBindings.Add(A("fight.coords"), new Key("F3"));
            Assert.Equal("fight.coords=fake:C fake:F3", _saved);
            KeyBindings.Reset(A("fight.coords"));
            Assert.Equal("", _saved);
            Assert.True(KeyBindings.IsDefault(A("fight.coords")));
        }

        [Fact]
        public void A_saved_form_replaces_the_keys_and_an_empty_list_unbinds()
        {
            KeyBindings.Load("fight.coords=fake:Ctrl+C;fight.turn=");
            Assert.Equal("Ctrl+C", A("fight.coords").BindingsDisplay);
            Assert.Empty(A("fight.turn").Bindings);
            Assert.Equal("fight.coords=fake:Ctrl+C;fight.turn=", KeyBindings.Format());
        }

        [Fact]
        public void A_protected_action_keeps_its_defaults_over_an_empty_save_and_its_last_key()
        {
            KeyBindings.Load("mod.menu=");
            Assert.Equal("Ctrl+Shift+M", A("mod.menu").BindingsDisplay);
            Assert.False(KeyBindings.Remove(A("mod.menu"), A("mod.menu").Bindings[0]));
            Assert.Single(A("mod.menu").Bindings);
            KeyBindings.Add(A("mod.menu"), new Key("F10"));
            Assert.True(KeyBindings.Remove(A("mod.menu"), A("mod.menu").Bindings[0]));
            Assert.Equal("F10", A("mod.menu").BindingsDisplay);
        }

        [Fact]
        public void Unknown_actions_survive_a_save_and_unreadable_keys_are_skipped()
        {
            KeyBindings.Load("gone.action=fake:X;fight.coords=nope:Z fake:V");
            Assert.Equal("V", A("fight.coords").BindingsDisplay);
            KeyBindings.Add(A("fight.turn"), new Key("Y"));
            Assert.Equal("fight.coords=fake:V;fight.turn=fake:T fake:Y;gone.action=fake:X", _saved);
        }

        [Fact]
        public void Keys_that_follow_the_game_are_not_rebindable_and_a_save_cannot_touch_them()
        {
            Assert.False(KeyBindings.IsRebindable(A("fight.endturn")));
            Assert.True(KeyBindings.IsRebindable(A("fight.coords")));
            KeyBindings.Load("fight.endturn=fake:Q");
            Assert.Equal("E", A("fight.endturn").BindingsDisplay);
        }

        [Fact]
        public void Conflicts_are_within_a_category_or_with_global_and_other_categories_only_overlap()
        {
            Assert.Equal("fight.turn", KeyBindings.Conflict(A("fight.coords"), new Key("T"))?.Key);
            // The game's followed keys count: E is End turn's.
            Assert.Equal("fight.endturn", KeyBindings.Conflict(A("fight.coords"), new Key("E"))?.Key);
            Assert.Equal("mod.menu", KeyBindings.Conflict(A("fight.coords"), new Key("Ctrl+Shift+M"))?.Key);
            Assert.Equal("fight.coords", KeyBindings.Conflict(A("mod.menu"), new Key("C"))?.Key);
            Assert.Null(KeyBindings.Conflict(A("fight.coords"), new Key("Ctrl+Right")));
            Assert.Equal(new[] { "buffer.next" }, KeyBindings.Overlaps(A("fight.coords"), new Key("Ctrl+Right")).ConvertAll(a => a.Key));
            Assert.Same(A("fight.coords").Bindings[0], KeyBindings.Existing(A("fight.coords"), new Key("C")));
        }

        [Fact]
        public void Reset_all_restores_every_action_and_forgets_unknown_entries()
        {
            KeyBindings.Load("gone.action=fake:X;fight.coords=fake:V;buffer.next=");
            KeyBindings.ResetAll();
            Assert.Equal("C", A("fight.coords").BindingsDisplay);
            Assert.Equal("Ctrl+Right", A("buffer.next").BindingsDisplay);
            Assert.Equal("", _saved);
        }

        [Fact]
        public void The_saved_form_parses_entries_in_order()
        {
            var entries = KeyBindings.Parse(" a.b=fake:Ctrl+M  x:y ;;c=;bad");
            Assert.Equal(2, entries.Count);
            Assert.Equal("a.b", entries[0].Key);
            Assert.Equal(new List<(string, string)> { ("fake", "Ctrl+M"), ("x", "y") }, entries[0].Value);
            Assert.Equal("c", entries[1].Key);
            Assert.Empty(entries[1].Value);
        }
    }
}
