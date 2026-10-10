using System.Collections.Generic;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Screens;
using StarVoders.Core.UI;
using Xunit;

namespace StarVoders.Tests
{
    // The game leaving a scene from an overlay (the pause menu's Save & Exit): the overlay closes on
    // its way out, and the screen it covered must not announce itself again before the next scene's.
    public class TransitionTests
    {
        private sealed class Simple : Screen
        {
            public bool Active, Kept;
            public string Name, Text;
            public int ScreenLayer;
            public override string Key => "test." + Name;
            public override string ScreenName => Name;
            public override int Layer => ScreenLayer;
            public override bool KeptThroughTransitions => Kept;
            public override bool IsActive() => Active;

            public override void Build(GraphBuilder b) =>
                b.AddItem(ControlId.Structural(Name + ":item"), new NodeVtable
                {
                    ControlType = ControlTypes.Text,
                    Announcements = new[] { new NodeAnnouncement(() => Text, kind: AnnouncementKinds.Label) },
                });
        }

        private static void Tick(FakeNavInput input)
        {
            input.FrameCount += 10;
            ScreenManager.Tick();
        }

        [Fact]
        public void An_overlay_closing_during_a_scene_change_uncovers_nothing_and_the_next_scene_arrives_once()
        {
            var spoken = new List<string>();
            var input = new FakeNavInput { FrameCount = 1 };
            var previous = Navigation.Active;
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = input;
            Navigation.Active = new GraphNavigator();
            try
            {
                bool transitioning = false;
                var globe = new Simple { Name = "Choose an Encounter", Text = "Elysia Event!", Active = true };
                var pause = new Simple { Name = "Paused", Text = "Save & Exit", ScreenLayer = 30, Active = true };
                var menu = new Simple { Name = "Main menu", Text = "Act 2-1" };
                var help = new Simple { Name = "Keys here", Text = "F1", ScreenLayer = 50, Kept = true };
                foreach (var s in new[] { globe, pause, menu, help }) ScreenManager.Register(s);
                ScreenManager.Transitioning = () => transitioning;
                Tick(input);
                spoken.Clear();

                transitioning = true;     // Save & Exit clicked
                Tick(input);
                pause.Active = false;     // the pause menu unloads
                Tick(input);
                globe.Active = false;     // the globe unloads
                menu.Active = true;       // the menu loads
                Tick(input);
                Assert.Empty(spoken);

                transitioning = false;
                Tick(input);
                Assert.Equal(new[] { "Main menu", "Act 2-1" }, spoken.ToArray());

                spoken.Clear();
                help.Active = true;
                transitioning = true;
                Tick(input);
                Assert.Equal(new[] { "Keys here", "F1" }, spoken.ToArray());
            }
            finally
            {
                ScreenManager.Shutdown();
                Navigation.Active = previous;
                Speech.Speak = (t, i) => { };
            }
        }
    }
}
