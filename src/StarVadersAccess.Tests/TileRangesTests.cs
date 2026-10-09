using StarVadersAccess.Core.Fight;
using Xunit;

namespace StarVadersAccess.Tests
{
    /// <summary>
    /// Danger Zone tiles said as shapes, on the 5 by 7 board: runs and rectangles by their corners,
    /// whole rows and columns by name, diagonals as such, scattered tiles one by one.
    /// </summary>
    public class TileRangesTests
    {
        private static string Say(params (int, int)[] tiles) => string.Join("; ", TileRanges.Parts(tiles, 5, 7));

        [Fact]
        public void A_shooters_column_is_a_run() =>
            Assert.Equal("5, 1 to 5, 5", Say((5, 1), (5, 2), (5, 3), (5, 4), (5, 5)));

        [Fact]
        public void A_whole_column_or_row_is_named() =>
            Assert.Equal("column 3; row 2",
                Say((3, 1), (3, 2), (3, 3), (3, 4), (3, 5), (3, 6), (3, 7), (1, 2), (2, 2), (4, 2), (5, 2)));

        [Fact]
        public void A_block_is_its_corners() =>
            Assert.Equal("2, 3 to 4, 5", Say((2, 3), (3, 3), (4, 3), (2, 4), (3, 4), (4, 4), (2, 5), (3, 5), (4, 5)));

        [Fact]
        public void A_plus_is_its_two_bars() =>
            Assert.Equal("3, 3 to 3, 5; 2, 4 to 4, 4", Say((3, 3), (2, 4), (3, 4), (4, 4), (3, 5)));

        [Fact]
        public void A_diagonal_is_said_as_one() =>
            Assert.Equal("diagonal 1, 5 to 4, 2", Say((1, 5), (2, 4), (3, 3), (4, 2)));

        [Fact]
        public void Scattered_tiles_are_listed() =>
            Assert.Equal("5, 1; 4, 2; 5, 3", Say((5, 1), (4, 2), (5, 3)));

        [Fact]
        public void Shapes_and_strays_mix() =>
            Assert.Equal("1, 1; 4, 2 to 4, 4", Say((4, 2), (4, 3), (4, 4), (1, 1)));

        [Fact]
        public void A_staircase_takes_its_diagonal_and_lists_the_rest() =>
            Assert.Equal("diagonal 1, 1 to 3, 3; 2, 1; 3, 2; 4, 6; 5, 7",
                Say((1, 1), (2, 1), (2, 2), (3, 2), (3, 3), (5, 7), (4, 6)));
    }
}
