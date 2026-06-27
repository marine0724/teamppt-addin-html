using System.Collections.Generic;
using System.Threading.Tasks;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    public class RedesignPreview
    {
        public int SlideIndex { get; set; }
        public int SlideId { get; set; }     // 안정 식별자 — 복제 다수 생성 시 인덱스가 밀려도 불변
        public string ThumbPath { get; set; }
        public HeaderAsset Asset { get; set; }
        public MappingResult Mapping { get; set; }
    }

    /// <summary>
    /// 비파괴 적용: 원본 슬라이드를 Duplicate()한 복제본에만 작업한다(원본 불변).
    /// 복제본 위에 에셋 도형을 삽입하고, 매핑대로 초안 재료(COM 원문 텍스트·이미지)를 에셋 자리에 채운 뒤 썸네일을 렌더한다.
    /// 텍스트는 항상 profile의 COM 원문(LLM 생성 금지). 좌표 변환은 CoordinateConverter 규칙 준수(폴백 추가 금지).
    /// </summary>
    public static class RedesignApplier
    {
        public static async Task<RedesignPreview> BuildPreviewAsync(
            int originalSlideIndex, DraftProfile profile, DraftUnderstanding u,
            HeaderAsset asset, string assetPptxPath, SlotMapper mapper)
        {
            var app = Globals.Application;
            var pres = app.ActivePresentation;

            // ① 복제 (원본 바로 뒤에 생성됨) — 원본은 어떤 실패에도 불변
            var original = pres.Slides[originalSlideIndex];
            original.Duplicate();
            var dup = pres.Slides[originalSlideIndex + 1];

            // 복제본을 현재 슬라이드로 만들어 ShapeInserter가 여기에 삽입하도록
            app.ActiveWindow.View.GotoSlide(dup.SlideIndex);

            // 삭제로 인덱스가 밀리기 전에 초안 도형 COM 참조를 미리 확보
            // (SourceShapeId == slide.Shapes의 1-based 인덱스 — DraftSlideReader가 모든 도형마다 id 증가)
            var draftComById = new Dictionary<int, PowerPoint.Shape>();
            foreach (var ds in profile.Shapes)
            {
                if (ds.Id >= 1 && ds.Id <= dup.Shapes.Count)
                    draftComById[ds.Id] = dup.Shapes[ds.Id];
            }

            // ② 에셋 삽입 → 복제본에 추가된 에셋 도형 ShapeRange
            var assetShapes = ShapeInserter.InsertToActiveSlide(assetPptxPath);
            var inventory = AssetShapeInventory.Read(assetShapes);

            // ③ 매핑 (LLM: 역할·타입·개수 배정만)
            // ConfigureAwait(false) 금지: 이 await 뒤 주입/삭제/렌더는 COM(STA)이라 호출 스레드(UI)에서 이어져야 함.
            var mapping = await mapper.MapAsync(u, inventory);

            // 에셋 도형 인덱싱: "a"+k ↔ assetShapes[k] (1-based, 인벤토리 순서와 동일)
            var assetComById = new Dictionary<string, PowerPoint.Shape>();
            if (assetShapes != null)
            {
                for (int k = 1; k <= assetShapes.Count; k++)
                    assetComById["a" + k] = assetShapes[k];
            }

            var profileById = new Dictionary<int, DraftShape>();
            foreach (var ds in profile.Shapes) profileById[ds.Id] = ds;

            // ④ 주입 + ⑤ 매핑된 원본 도형 처리 — 인덱스 밀림 방지 위해 참조만 모았다가 끝에서 삭제
            var draftToDelete = new List<PowerPoint.Shape>();   // 텍스트 이주 끝난 원본 도형
            var assetToDelete = new List<PowerPoint.Shape>();   // 초안 이미지로 대체된 에셋 자리표시자

            foreach (var m in mapping.Mappings)
            {
                if (!profileById.TryGetValue(m.DraftShapeId, out var draft)) continue;
                if (!assetComById.TryGetValue(m.AssetShapeId, out var assetCom)) continue;
                draftComById.TryGetValue(m.DraftShapeId, out var draftCom);

                if (draft.Kind == "text")
                {
                    // 텍스트 = COM 원문 (profile). 에셋 텍스트 도형에 주입.
                    try
                    {
                        if (assetCom.HasTextFrame == Microsoft.Office.Core.MsoTriState.msoTrue)
                            assetCom.TextFrame.TextRange.Text = draft.Text;
                    }
                    catch { /* 텍스트프레임 없는 에셋 도형이면 건너뜀 */ }
                    if (draftCom != null) draftToDelete.Add(draftCom);
                }
                else if (draft.Kind == "image" && draftCom != null)
                {
                    // 초안 이미지를 에셋 이미지 슬롯 위치/크기에 맞춰 배치, 에셋 자리표시자 제거
                    draftCom.Left = assetCom.Left;
                    draftCom.Top = assetCom.Top;
                    draftCom.Width = assetCom.Width;
                    draftCom.Height = assetCom.Height;
                    assetToDelete.Add(assetCom);
                }
                // table/chart 등은 범위 외 — 그대로 둠
            }

            foreach (var s in draftToDelete) { try { s.Delete(); } catch { } }
            foreach (var s in assetToDelete) { try { s.Delete(); } catch { } }

            Logger.Log($"[RedesignApplier] dup={dup.SlideIndex} asset={asset?.File} " +
                       $"mapped={mapping.Mappings.Count} overflow={mapping.Overflow.Count} empty={mapping.Empty.Count}");

            // ⑥ 썸네일
            var thumb = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "TeampptAddin", "cache", "redesign", $"preview-{dup.SlideIndex}.png");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(thumb));
            SlideImageRenderer.Render(pres, dup.SlideIndex, thumb);

            return new RedesignPreview { SlideIndex = dup.SlideIndex, SlideId = dup.SlideID, ThumbPath = thumb, Asset = asset, Mapping = mapping };
        }
    }
}
