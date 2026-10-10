using System.Collections.Generic;
using StarVoders.Core;
using StarVoders.Core.Graph;
using StarVoders.Core.Input;
using StarVoders.Core.Screens;
using StarVoders.Core.UI;
using Xunit;

namespace StarVoders.Tests
{
    // Tab and Shift+Tab on a screen of one stop (News, the credits) have nothing to wrap to: they are
    // consumed and do nothing, rather than re-landing on the stop (cursor back to its start, spoken
    // again). With two stops they still wrap.
    public class SingleStopTabTests
    {
        private sealed class ListScreen : Screen
        {
            public int Stops = 1;
            public override string Key => "test.list";
            public override bool IsActive() => true;
            public override void Build(GraphBuilder b)
            {
                b.BeginStop("lines");
                b.AddItem(ControlId.Structural("line1"), Item("line 1"));
                b.AddItem(ControlId.Structural("line2"), Item("line 2"));
                if (Stops > 1)
                {
                    b.BeginStop("back");
                    b.AddItem(ControlId.Structural("back"), Item("Back"));
                }
            }

            private static NodeVtable Item(string label) => new NodeVtable
            {
                Announcements = new[] { new NodeAnnouncement(() => label, kind: AnnouncementKinds.Label) },
            };
        }

        private static InputAction Action(string key) => new InputAction(key, key) { Category = InputCategory.UI };

        [Fact]
        public void TabOnASingleStopIsANoOp()
        {
            var spoken = new List<string>();
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = new FakeNavInput { FrameCount = 1 };
            var nav = new GraphNavigator();
            Navigation.Active = nav;
            try
            {
                nav.Attach(new ListScreen());
                nav.EnsureFocus();
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Down)));
                Assert.Equal("line2", nav.FocusedNodeId.StructuralKey);
                spoken.Clear();

                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Prev)));
                Assert.Equal("line2", nav.FocusedNodeId.StructuralKey);
                Assert.Empty(spoken);
            }
            finally
            {
                Speech.Speak = (t, i) => { };
                Navigation.Active = new GraphNavigator();
            }
        }

        [Fact]
        public void TabStillWrapsBetweenTwoStops()
        {
            var spoken = new List<string>();
            Speech.Speak = (t, i) => spoken.Add(t);
            NavInput.Current = new FakeNavInput { FrameCount = 1 };
            var nav = new GraphNavigator();
            Navigation.Active = nav;
            try
            {
                nav.Attach(new ListScreen { Stops = 2 });
                nav.EnsureFocus();
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.Equal("back", nav.FocusedNodeId.StructuralKey);
                Assert.True(nav.OnInputJustPressed(Action(UiActions.Next)));
                Assert.Equal("line1", nav.FocusedNodeId.StructuralKey);
            }
            finally
            {
                Speech.Speak = (t, i) => { };
                Navigation.Active = new GraphNavigator();
            }
        }
    }
}
