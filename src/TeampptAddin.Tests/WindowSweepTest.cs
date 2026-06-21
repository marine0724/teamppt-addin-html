using System.Collections.Generic;
using Xunit;

namespace TeampptAddin.Tests
{
    public class WindowSweepTest
    {
        [Fact]
        public void AllAlive_ReclaimsNothing()
        {
            var result = WindowSweep.ToReclaim(new[] { 1, 2, 3 }, new[] { 1, 2, 3 });
            Assert.Empty(result);
        }

        [Fact]
        public void OneClosed_ReclaimsThatOne()
        {
            var result = WindowSweep.ToReclaim(new[] { 1, 2, 3 }, new[] { 1, 3 });
            Assert.Equal(new[] { 2 }, result);
        }

        [Fact]
        public void SeveralClosed_ReclaimsThem_InTrackedOrder()
        {
            var result = WindowSweep.ToReclaim(new[] { 1, 2, 3, 4 }, new[] { 3 });
            Assert.Equal(new[] { 1, 2, 4 }, result);
        }

        [Fact]
        public void EmptyTracked_ReclaimsNothing()
        {
            Assert.Empty(WindowSweep.ToReclaim(new int[0], new[] { 1, 2 }));
        }
    }
}
