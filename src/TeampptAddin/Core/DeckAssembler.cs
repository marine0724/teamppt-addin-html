using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

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

    public class DeckAssemblyResult
    {
        public int SlideCount { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public static class DeckAssembler
    {
        private static readonly Dictionary<string, int> LayerOrder = new Dictionary<string, int>
        {
            ["component"] = 0,  // 맨 먼저 삽입 → z-order 맨 아래
            ["layout"] = 1,
            ["slide"] = 2,
            ["header"] = 3      // 맨 마지막 삽입 → z-order 맨 위 (상단 고정)
        };

        public static List<RecommendedSlot> SortSlotsByLayer(List<RecommendedSlot> slots)
        {
            return (slots ?? new List<RecommendedSlot>())
                .OrderBy(s => LayerOrder.TryGetValue(s.Asset?.Kind ?? "", out var v) ? v : 1)
                .ToList();
        }

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

        public static async Task<DeckAssemblyResult> AssembleAsync(
            DeckRecommendation deck,
            RemoteAssetCache remoteCache,
            Action<string> progress)
        {
            var result = new DeckAssemblyResult();
            var order = BuildSlideOrder(deck);
            if (order.Count == 0) return result;

            var app = Globals.Application;
            var pres = app.ActivePresentation;

            app.StartNewUndoEntry();

            // 기존 슬라이드 모두 삭제 (빈 덱에서 시작)
            while (pres.Slides.Count > 0)
                pres.Slides[1].Delete();

            // 빈 슬라이드 하나 만들어서 레이아웃 참조 확보
            var masterLayout = pres.SlideMaster.CustomLayouts[1];

            int repSlideIndex = -1; // 마지막 대표 body 슬라이드 인덱스 (복제용)

            foreach (var item in order)
            {
                progress?.Invoke($"{item.Plan?.Label ?? item.BoxKind} 조립 중…");

                if (!item.IsRepresentative && repSlideIndex > 0)
                {
                    // 복제 장: 대표 body를 Duplicate
                    var repSlide = pres.Slides[repSlideIndex];
                    repSlide.Duplicate();
                    result.SlideCount++;
                    continue;
                }

                // 새 빈 슬라이드 추가
                int insertAt = pres.Slides.Count + 1;
                var newSlide = pres.Slides.AddSlide(insertAt, masterLayout);

                // 기본 placeholder 도형 제거
                while (newSlide.Shapes.Count > 0)
                    newSlide.Shapes[1].Delete();

                // 에셋 슬롯 순서대로 삽입 (SortSlotsByLayer로 z-order 보장)
                foreach (var slot in SortSlotsByLayer(item.Slots))
                {
                    string localPath = null;
                    try
                    {
                        var remotePath = slot.Asset.Extra != null &&
                            slot.Asset.Extra.ContainsKey("remote_file")
                            ? slot.Asset.Extra["remote_file"].ToString()
                            : slot.Asset.File;
                        localPath = await remoteCache.GetPptxAsync(remotePath);
                    }
                    catch (Exception ex)
                    {
                        var warn = $"{slot.Asset.Name ?? slot.Asset.File} 다운로드 실패: {ex.Message}";
                        result.Warnings.Add(warn);
                        Logger.Log($"[DeckAssembler] {warn}");
                        continue;
                    }

                    if (string.IsNullOrEmpty(localPath) || !System.IO.File.Exists(localPath))
                    {
                        result.Warnings.Add($"{slot.Asset.Name} 파일 없음");
                        continue;
                    }

                    // InsertFromFile → 임시 슬라이드 → 도형 복사 → 대상 슬라이드에 붙여넣기 → 임시 삭제
                    int tempIdx = pres.Slides.Count;
                    pres.Slides.InsertFromFile(localPath, tempIdx, 1, 1);
                    var tempSlide = pres.Slides[tempIdx + 1];
                    int shapeCount = tempSlide.Shapes.Count;
                    if (shapeCount > 0)
                    {
                        var indices = new int[shapeCount];
                        for (int i = 0; i < shapeCount; i++) indices[i] = i + 1;
                        tempSlide.Shapes.Range(indices).Copy();
                        newSlide.Shapes.Paste();
                    }
                    tempSlide.Delete();
                }

                if (item.BoxKind == "body" && item.IsRepresentative)
                    repSlideIndex = newSlide.SlideIndex;

                result.SlideCount++;
            }

            // 첫 슬라이드로 이동
            if (pres.Slides.Count > 0)
                app.ActiveWindow.View.GotoSlide(1);

            progress?.Invoke($"덱 조립 완료! {result.SlideCount}장 생성.");
            Logger.Log($"[DeckAssembler] AssembleAsync 완료: {result.SlideCount}장, 경고 {result.Warnings.Count}건");
            return result;
        }
    }
}
