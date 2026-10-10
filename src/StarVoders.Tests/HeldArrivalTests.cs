using System.Collections.Generic;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Screens;
using StarVoders.Core.UI;
using Xunit;

namespace StarVoders.Tests
{
    // A screen holding its arrival (Screen.HoldsArrival) under a chain of modal tips, as the fight
    // does under the tutorial's: nothing of the screen is spoken between two tips, and its name and
    // landing come once, when it lets go.
    public class HeldArrivalTests
    {
        private sealed class BaseScreen : Screen
        {
            public bool Hold;
            public override string Key => "test.base";
            public override string ScreenName => "Objective";
            public override bool HoldsArrival => Hold;
            public override bool IsActive() => true;

            public override void Build(GraphBuilder b)
            {
                b.PushContext(null);
                b.AddItem(ControlId.Structural("a"), Item("1, 7"));
                b.AddItem(ControlId.Structural("b"), Item("1, 6"));
                b.PopContext();
            }
        }

        private sealed class TipOverlay : Screen
        {
            public string Text; // null = no tip
            public override string Key => "test.tip";
            public override int Layer => 40;
            public override bool Exclusive => true;
            public override bool IsActive() => Text != null;

            public override void Build(GraphBuilder b)
            {
                if (Text != null) b.AddItem(ControlId.Structural("tip:" + Text), Item(Text));
            }
        }

        private static NodeVtable Item(string text) => new NodeVtable
        {
            ControlType = ControlTypes.Text,
            Announcements = new[] { new NodeAnnouncement(() => text, kind: AnnouncementKinds.Label) },
        };

        private static void Tick(FakeNavInput input)
        {
            input.FrameCount += 10;
            ScreenManager.Tick();
        }

        private static void Run(System.Action<BaseScreen, TipOverlay, List<string>, FakeNavInput> body)
        {
            var spoken = new List<string>();
            var input = new FakeNavInput { FrameCount = 1 };
            var previous = Navigation.Active;
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = input;
            Navigation.Active = new GraphNavigator();
            try
            {
                var screen = new BaseScreen();
                var tip = new TipOverlay();
                ScreenManager.Register(screen);
                ScreenManager.Register(tip);
                body(screen, tip, spoken, input);
            }
            finally
            {
                ScreenManager.Shutdown();
                Navigation.Active = previous;
                Speech.Speak = (t, i) => { };
            }
        }

        [Fact]
        public void A_chain_of_tips_reads_only_the_tips_then_the_screen_once()
        {
            Run((screen, tip, spoken, input) =>
            {
                screen.Hold = true;
                Tick(input);
                Tick(input);
                Assert.Empty(spoken);

                tip.Text = "First tip";
                Tick(input);
                tip.Text = null;
                Tick(input);
                Tick(input);
                tip.Text = "Second tip";
                Tick(input);
                tip.Text = null;
                Tick(input);
                Assert.Equal(new[] { "First tip", "Second tip" }, spoken.ToArray());
                spoken.Clear();

                screen.Hold = false;
                Tick(input);
                Tick(input);
                Assert.Equal(new[] { "Objective", "1, 7" }, spoken.ToArray());
            });
        }

        [Fact]
        public void Keys_speak_during_a_hold_and_nothing_is_repeated_after()
        {
            Run((screen, tip, spoken, input) =>
            {
                screen.Hold = true;
                Tick(input);
                Assert.True(Navigation.DispatchJustPressed(new InputAction(UiActions.Down, UiActions.Down) { Category = InputCategory.UI }));
                Assert.Equal(new[] { "1, 6" }, spoken.ToArray());
                spoken.Clear();

                screen.Hold = false;
                Tick(input);
                Tick(input);
                Assert.Empty(spoken);
            });
        }

        [Fact]
        public void A_screen_that_does_not_hold_speaks_on_arrival()
        {
            Run((screen, tip, spoken, input) =>
            {
                Tick(input);
                Assert.Equal(new[] { "Objective", "1, 7" }, spoken.ToArray());
            });
        }
    }
}
