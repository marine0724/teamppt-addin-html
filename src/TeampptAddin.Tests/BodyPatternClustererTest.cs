// src/TeampptAddin.Tests/BodyPatternClustererTest.cs
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class BodyPatternClustererTest
    {
        private static DraftProfile P(int idx, params (string kind, float left, float width)[] shapes)
        {
            var p = new DraftProfile { SlideIndex = idx, SlideWidth = 960, SlideHeight = 540 };
            foreach (var s in shapes)
                p.Shapes.Add(new DraftShape { Kind = s.kind, Left = s.left, Width = s.width });
            return p;
        }

        [Fact]
        public void Same_Signature_Groups_Into_One_Pattern()
        {
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile>
            {
                P(3, ("text", 0, 300), ("image", 600, 300)),
                P(4, ("text", 0, 300), ("image", 600, 300)),
            });
            Assert.Single(r);
            Assert.Equal(new[] { 3, 4 }, r[0].SlideIndexes.ToArray());
            Assert.Equal(3, r[0].RepresentativeIndex);
        }

        [Fact]
        public void Different_Shape_Counts_Make_Two_Patterns()
        {
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile>
            {
                P(2, ("text", 0, 300)),
                P(3, ("text", 0, 300), ("text", 0, 300), ("text", 0, 300)),
            });
            Assert.Equal(2, r.Count);
        }

        [Fact]
        public void Column_Difference_Makes_Two_Patterns()
        {
            var oneCol = P(2, ("text", 0, 300), ("text", 0, 300));      // 둘 다 왼쪽 1/3 → 1열
            var twoCol = P(3, ("text", 0, 300), ("text", 660, 300));    // 왼/오 → 2열
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile> { oneCol, twoCol });
            Assert.Equal(2, r.Count);
        }

        [Fact]
        public void Empty_Input_Returns_Empty()
        {
            Assert.Empty(BodyPatternClusterer.Cluster(new List<DraftProfile>()));
        }

        [Fact]
        public void Representative_Is_Lowest_Index_Regardless_Of_Order()
        {
            var r = BodyPatternClusterer.Cluster(new List<DraftProfile>
            {
                P(7, ("text", 0, 300)),
                P(2, ("text", 0, 300)),
            });
            Assert.Single(r);
            Assert.Equal(2, r[0].RepresentativeIndex);
            Assert.Equal(new[] { 2, 7 }, r[0].SlideIndexes.ToArray());
        }
    }
}
