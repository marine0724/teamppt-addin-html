using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public class SlideAssemblyItem
    {
        public string BoxKind { get; set; } = "";
        public BoxPlan Plan { get; set; }
        public List<RecommendedSlot> Slots { get; set; } = new List<RecommendedSlot>();
        public bool IsRepresentative { get; set; } = true;
        public int SourceSlideIndex { get; set; }
    }

    public static class DeckAssembler
    {
        public static List<SlideAssemblyItem> BuildSlideOrder(DeckRecommendation deck)
        {
            var items = new List<SlideAssemblyItem>();
            if (deck?.Boxes == null) return items;

            RecommendedSlot commonHeader = null;
            foreach (var box in deck.Boxes)
            {
                if (box.Plan.BoxKind == "header")
                {
                    commonHeader = box.Recommendation?.Header;
                    continue;
                }
                if (box.Plan.BoxKind == "body")
                {
                    var rec = box.Recommendation ?? new CombinationRecommendation();
                    var slots = new List<RecommendedSlot>();
                    if (commonHeader != null) slots.Add(commonHeader);
                    if (rec.Layout != null) slots.Add(rec.Layout);
                    foreach (var c in rec.Components ?? new List<RecommendedSlot>())
                        if (c != null) slots.Add(c);

                    var covered = box.Plan.CoveredSlideIndexes ?? new List<int>();
                    for (int i = 0; i < covered.Count; i++)
                    {
                        items.Add(new SlideAssemblyItem
                        {
                            BoxKind = "body",
                            Plan = box.Plan,
                            Slots = i == 0 ? slots : new List<RecommendedSlot>(),
                            IsRepresentative = i == 0,
                            SourceSlideIndex = covered[i]
                        });
                    }
                }
                else
                {
                    var rec = box.Recommendation ?? new CombinationRecommendation();
                    var slots = new List<RecommendedSlot>();
                    if (rec.Slide != null) slots.Add(rec.Slide);
                    items.Add(new SlideAssemblyItem
                    {
                        BoxKind = box.Plan.BoxKind,
                        Plan = box.Plan,
                        Slots = slots,
                        IsRepresentative = true,
                        SourceSlideIndex = box.Plan.CoveredSlideIndexes?.FirstOrDefault() ?? 0
                    });
                }
            }
            return items;
        }
    }
}
