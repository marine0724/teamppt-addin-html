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
        public void BuildSlideOrder_BodyRepresentative_MergesHeaderLayoutComponent()
        {
            var deck = ThreeBoxDeck();
            var order = DeckAssembler.BuildSlideOrder(deck);
            var bodyRep = order[1]; // 대표 장
            Assert.True(bodyRep.IsRepresentative);
            // header + layout + component = 3개 슬롯
            Assert.Equal(3, bodyRep.Slots.Count);
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "header");
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "layout");
            Assert.Contains(bodyRep.Slots, s => s.Asset.Kind == "component");
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
    }
}
