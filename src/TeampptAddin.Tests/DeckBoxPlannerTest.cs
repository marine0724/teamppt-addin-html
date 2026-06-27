using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckBoxPlannerTest
    {
        private static DeckStructure Struct(params (int idx, string kind)[] slides)
        {
            var st = new DeckStructure { TotalCount = slides.Length };
            foreach (var s in slides)
                st.Slides.Add(new DeckSlideStructure { Index = s.idx, Kind = s.kind, Label = s.kind });
            return st;
        }

        private static BodyPattern Pat(int rep, params int[] idx)
            => new BodyPattern { Signature = "sig" + rep, RepresentativeIndex = rep, SlideIndexes = idx.ToList() };

        [Fact]
        public void Cover_Body_End_Yields_Cover_Header_Body_End()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "body"), (3, "body"), (4, "end")),
                new List<BodyPattern> { Pat(2, 2, 3) });
            Assert.Equal(new[] { "cover", "header", "body", "end" }, boxes.Select(b => b.BoxKind).ToArray());
        }

        [Fact]
        public void No_Body_Skips_Header()
        {
            var boxes = DeckBoxPlanner.Plan(Struct((1, "cover"), (2, "end")), new List<BodyPattern>());
            Assert.Equal(new[] { "cover", "end" }, boxes.Select(b => b.BoxKind).ToArray());
        }

        [Fact]
        public void Two_Patterns_Become_Two_Body_Boxes_In_Rep_Order()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "body"), (3, "body"), (4, "body"), (5, "end")),
                new List<BodyPattern> { Pat(2, 2, 4), Pat(3, 3) });
            Assert.Equal(new[] { "cover", "header", "body", "body", "end" }, boxes.Select(b => b.BoxKind).ToArray());
            var bodyBoxes = boxes.Where(b => b.BoxKind == "body").ToList();
            Assert.Equal(2, bodyBoxes[0].RepresentativeIndex);
            Assert.Equal(3, bodyBoxes[1].RepresentativeIndex);
        }

        [Fact]
        public void Toc_Before_Body_Keeps_Its_Position()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "toc"), (3, "body"), (4, "end")),
                new List<BodyPattern> { Pat(3, 3) });
            Assert.Equal(new[] { "cover", "toc", "header", "body", "end" }, boxes.Select(b => b.BoxKind).ToArray());
        }

        [Fact]
        public void Header_Box_Covers_All_Body_Indexes_With_First_Rep()
        {
            var boxes = DeckBoxPlanner.Plan(
                Struct((1, "cover"), (2, "body"), (3, "body")),
                new List<BodyPattern> { Pat(2, 2, 3) });
            var header = boxes.First(b => b.BoxKind == "header");
            Assert.Equal(new[] { 2, 3 }, header.CoveredSlideIndexes.ToArray());
            Assert.Equal(2, header.RepresentativeIndex);
        }
    }
}
