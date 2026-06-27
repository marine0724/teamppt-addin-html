using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TeampptAddin.Tests
{
    public class DeckAssemblerTest
    {
        private static HeaderAsset FakeAsset(string name, string kind) => new HeaderAsset
        {
            Name = name, Kind = kind, File = $"{name}.pptx",
            Extra = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
            {
                ["remote_file"] = $"assets/{name}.pptx"
            }
        };

        private static RecommendedSlot Slot(string name, string kind)
            => new RecommendedSlot { Asset = FakeAsset(name, kind), FitNote = kind, Confidence = 0.9 };

        private static DeckRecommendation ThreeBoxDeck()
        {
            return new DeckRecommendation
            {
                Boxes = new List<BoxRecommendation>
                {
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "cover", Label = "표지", CoveredSlideIndexes = new List<int>{1} },
                        Recommendation = new CombinationRecommendation
                        {
                            SlideKind = "cover",
                            Slide = Slot("cover-blue", "slide")
                        }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "header", Label = "공통 헤더", CoveredSlideIndexes = new List<int>{2,3,4} },
                        Recommendation = new CombinationRecommendation
                        {
                            Header = Slot("header-corp", "header")
                        }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan
                        {
                            BoxKind = "body", Label = "본문 패턴 (3장)",
                            CoveredSlideIndexes = new List<int>{2,3,4},
                            RepresentativeIndex = 2, Signature = "t2i1b0c0|col2"
                        },
                        Recommendation = new CombinationRecommendation
                        {
                            SlideKind = "body",
                            Layout = Slot("layout-2col", "layout"),
                            Components = new List<RecommendedSlot> { Slot("icon-card", "component") }
                        }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "end", Label = "마무리", CoveredSlideIndexes = new List<int>{5} },
                        Recommendation = new CombinationRecommendation
                        {
                            SlideKind = "end",
                            Slide = Slot("end-thankyou", "slide")
                        }
                    }
                }
            };
        }

        [Fact]
        public void BuildSlideOrder_CoverBodyEnd_CorrectOrder()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);

            // cover=1장, header는 독립 슬라이드 아님(body에 합성), body패턴=3장, end=1장 → 총 5항목
            Assert.Equal(5, order.Count);
            Assert.Equal("cover", order[0].BoxKind);
            Assert.Equal("body", order[1].BoxKind); // 본문 장 1 (대표)
            Assert.Equal("body", order[2].BoxKind); // 본문 장 2 (복제)
            Assert.Equal("body", order[3].BoxKind); // 본문 장 3 (복제)
            Assert.Equal("end", order[4].BoxKind);
        }

        [Fact]
        public void BuildSlideOrder_CoverSlideBox_HasSlideSlotOnly()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);
            var coverItem = order[0];
            Assert.Single(coverItem.Slots);
            Assert.Equal("slide", coverItem.Slots[0].Asset.Kind);
        }

        [Fact]
        public void BuildSlideOrder_BodyRepresentative_HeaderAndLayoutOnly()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);
            var bodyRep = order[1]; // 대표 장
            Assert.True(bodyRep.IsRepresentative);
            // Phase 4.5: 컴포넌트 제외 → header + layout = 2개
            Assert.Equal(2, bodyRep.Slots.Count);
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "header");
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "layout");
            Assert.DoesNotContain(bodyRep.Slots, s => s.Asset.Kind == "component");
        }

        [Fact]
        public void BuildSlideOrder_BodyClone_IsNotRepresentative()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);
            Assert.False(order[2].IsRepresentative);
            Assert.False(order[3].IsRepresentative);
            // 복제 장은 슬롯 없음(대표 장을 COM Duplicate)
            Assert.Empty(order[2].Slots);
        }

        [Fact]
        public void BuildSlideOrder_NullSlots_Skipped()
        {
            var deck = new DeckRecommendation
            {
                Boxes = new List<BoxRecommendation>
                {
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "cover", Label = "표지", CoveredSlideIndexes = new List<int>{1} },
                        Recommendation = new CombinationRecommendation { Slide = null }
                    }
                }
            };
            var order = DeckAssembler.BuildSlideOrder(deck);
            Assert.Single(order);
            Assert.Empty(order[0].Slots); // Slide가 null이면 슬롯 없음
        }

        [Fact]
        public void SortSlotsByLayer_HeaderLastForTopZOrder()
        {
            var slots = new List<RecommendedSlot>
            {
                new RecommendedSlot { Asset = new HeaderAsset { Kind = "header", Name = "h" } },
                new RecommendedSlot { Asset = new HeaderAsset { Kind = "layout", Name = "l" } },
                new RecommendedSlot { Asset = new HeaderAsset { Kind = "component", Name = "c" } }
            };
            var sorted = DeckAssembler.SortSlotsByLayer(slots);
            Assert.Equal("component", sorted[0].Asset.Kind); // 먼저 삽입 → 맨 아래
            Assert.Equal("layout", sorted[1].Asset.Kind);
            Assert.Equal("header", sorted[2].Asset.Kind);    // 마지막 삽입 → 맨 위
        }

        [Fact]
        public void SortSlotsByLayer_SlideKindUnchanged()
        {
            var slots = new List<RecommendedSlot>
            {
                new RecommendedSlot { Asset = new HeaderAsset { Kind = "slide", Name = "s" } }
            };
            var sorted = DeckAssembler.SortSlotsByLayer(slots);
            Assert.Single(sorted);
            Assert.Equal("slide", sorted[0].Asset.Kind);
        }

        [Fact]
        public void BuildSlideOrder_TocAndSection_TreatedAsSlideBox()
        {
            var deck = new DeckRecommendation
            {
                Boxes = new List<BoxRecommendation>
                {
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "cover", Label = "표지", CoveredSlideIndexes = new List<int>{1} },
                        Recommendation = new CombinationRecommendation { Slide = Slot("cover", "slide") }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "toc", Label = "목차", CoveredSlideIndexes = new List<int>{2} },
                        Recommendation = new CombinationRecommendation { Slide = Slot("toc", "slide") }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "header", Label = "공통 헤더", CoveredSlideIndexes = new List<int>{3,4} },
                        Recommendation = new CombinationRecommendation { Header = Slot("header", "header") }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan
                        {
                            BoxKind = "body", Label = "본문 (2장)",
                            CoveredSlideIndexes = new List<int>{3,4},
                            Signature = "t1i0b0c0|col1"
                        },
                        Recommendation = new CombinationRecommendation
                        {
                            Layout = Slot("layout", "layout")
                        }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "section", Label = "간지", CoveredSlideIndexes = new List<int>{5} },
                        Recommendation = new CombinationRecommendation { Slide = Slot("section", "slide") }
                    },
                    new BoxRecommendation
                    {
                        Plan = new BoxPlan { BoxKind = "end", Label = "마무리", CoveredSlideIndexes = new List<int>{6} },
                        Recommendation = new CombinationRecommendation { Slide = Slot("end", "slide") }
                    }
                }
            };
            var order = DeckAssembler.BuildSlideOrder(deck);

            // cover + toc + body대표 + body복제 + section + end = 6
            Assert.Equal(6, order.Count);
            Assert.Equal("cover", order[0].BoxKind);
            Assert.Equal("toc", order[1].BoxKind);
            Assert.Equal("body", order[2].BoxKind);
            Assert.True(order[2].IsRepresentative);
            Assert.Equal("body", order[3].BoxKind);
            Assert.False(order[3].IsRepresentative);
            Assert.Equal("section", order[4].BoxKind);
            Assert.Equal("end", order[5].BoxKind);

            // toc/section = slide-box → Slide 슬롯 1개씩
            Assert.Single(order[1].Slots);
            Assert.Single(order[4].Slots);
        }
    }
}
