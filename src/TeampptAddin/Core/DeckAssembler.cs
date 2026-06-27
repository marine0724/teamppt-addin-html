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
                    // Phase 4.5: 컴포넌트는 레이아웃에 이미 내장 → 별도 오버레이 삽입 안 함(겹침 제거).
                    //            컴포넌트 교체는 후속 단계(comp_ 모델, 로드맵 참조)에서 클릭-교체로 처리.

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

            // ── Phase 1: Pre-fetch — 모든 원격 에셋을 STA 스레드에서 미리 다운로드 ──
            // RemoteAssetCache 내부 ConfigureAwait(false) 때문에
            // COM 루프 안에서 await하면 thread-pool로 복귀 → RPC_E_WRONGTHREAD.
            // 여기서 모두 받아 놓으면 Phase 2는 await 없이 동기 실행.
            progress?.Invoke("에셋 다운로드 중…");
            var prefetched = new Dictionary<string, string>(); // remotePath → localPath
            foreach (var item in order)
            {
                foreach (var slot in SortSlotsByLayer(item.Slots))
                {
                    var remotePath = slot.Asset.Extra != null &&
                        slot.Asset.Extra.ContainsKey("remote_file")
                        ? slot.Asset.Extra["remote_file"].ToString()
                        : slot.Asset.File;

                    if (string.IsNullOrEmpty(remotePath) || prefetched.ContainsKey(remotePath))
                        continue;

                    try
                    {
                        var localPath = await remoteCache.GetPptxAsync(remotePath);
                        prefetched[remotePath] = localPath;
                    }
                    catch (Exception ex)
                    {
                        var warn = $"{slot.Asset.Name ?? slot.Asset.File} 다운로드 실패: {ex.Message}";
                        result.Warnings.Add(warn);
                        Logger.Log($"[DeckAssembler] {warn}");
                        prefetched[remotePath] = null; // 실패 기록 — Phase 2에서 skip
                    }
                }
            }

            // ── Phase 2: COM 조립 — await 없음, STA 스레드 유지 ──
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

                // slide 에셋(표지/엔드/목차/간지): 슬라이드 통째 InsertFromFile (배경·레이아웃 보존)
                // body 에셋(header+layout 합성): 빈 슬라이드에 도형 복사
                var isWholeSlide = item.Slots.Count == 1 &&
                    item.Slots[0].Asset?.Kind == "slide";

                PowerPoint.Slide newSlide;

                if (isWholeSlide)
                {
                    var slot = item.Slots[0];
                    var remotePath = slot.Asset.Extra != null &&
                        slot.Asset.Extra.ContainsKey("remote_file")
                        ? slot.Asset.Extra["remote_file"].ToString()
                        : slot.Asset.File;

                    string localPath;
                    if (!prefetched.TryGetValue(remotePath, out localPath) ||
                        string.IsNullOrEmpty(localPath) ||
                        !System.IO.File.Exists(localPath))
                    {
                        result.Warnings.Add($"{slot.Asset.Name ?? remotePath} 파일 없음");
                        continue;
                    }

                    try
                    {
                        int insertAt = pres.Slides.Count;
                        pres.Slides.InsertFromFile(localPath, insertAt, 1, 1);
                        newSlide = pres.Slides[insertAt + 1];

                        try { newSlide.Tags.Add(SlideProvenance.TagName, SlideProvenance.Format(item.Slots)); }
                        catch (Exception ex) { Logger.Log($"[DeckAssembler] 출처 태그 실패: {ex.Message}"); }
                    }
                    catch (Exception ex)
                    {
                        var warn = $"{slot.Asset.Name ?? remotePath} 슬라이드 삽입 실패: {ex.Message}";
                        result.Warnings.Add(warn);
                        Logger.Log($"[DeckAssembler] {warn}");
                        continue;
                    }
                }
                else
                {
                    // body: 빈 슬라이드 + 도형 복사 합성
                    int insertAt = pres.Slides.Count + 1;
                    newSlide = pres.Slides.AddSlide(insertAt, masterLayout);

                    while (newSlide.Shapes.Count > 0)
                        newSlide.Shapes[1].Delete();

                    var appliedSlots = new List<RecommendedSlot>();

                    foreach (var slot in SortSlotsByLayer(item.Slots))
                    {
                        var remotePath = slot.Asset.Extra != null &&
                            slot.Asset.Extra.ContainsKey("remote_file")
                            ? slot.Asset.Extra["remote_file"].ToString()
                            : slot.Asset.File;

                        string localPath;
                        if (!prefetched.TryGetValue(remotePath, out localPath) ||
                            string.IsNullOrEmpty(localPath) ||
                            !System.IO.File.Exists(localPath))
                        {
                            result.Warnings.Add($"{slot.Asset.Name ?? remotePath} 파일 없음");
                            continue;
                        }

                        try
                        {
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
                            appliedSlots.Add(slot);
                        }
                        catch (Exception ex)
                        {
                            var warn = $"{slot.Asset.Name ?? remotePath} 슬롯 삽입 실패: {ex.Message}";
                            result.Warnings.Add(warn);
                            Logger.Log($"[DeckAssembler] {warn}");
                        }
                    }

                    if (appliedSlots.Count > 0)
                    {
                        try { newSlide.Tags.Add(SlideProvenance.TagName, SlideProvenance.Format(appliedSlots)); }
                        catch (Exception ex) { Logger.Log($"[DeckAssembler] 출처 태그 실패: {ex.Message}"); }
                    }
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
