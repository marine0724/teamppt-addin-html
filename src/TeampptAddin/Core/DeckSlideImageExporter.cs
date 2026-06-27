// src/TeampptAddin/Core/DeckSlideImageExporter.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>외부 초안 .pptx를 창 없이 ReadOnly로 열어 지정 인덱스 슬라이드를 PNG로 내보낸다(비파괴). 대표 본문 장(2~4)만.</summary>
    public static class DeckSlideImageExporter
    {
        public static Dictionary<int, string> Export(string pptxPath, IEnumerable<int> slideIndexes, string outDir = null)
        {
            var result = new Dictionary<int, string>();
            var indexes = (slideIndexes ?? Enumerable.Empty<int>()).Distinct().ToList();
            var app = Globals.Application;
            if (app == null) { Logger.Log("[DeckExport] app is null"); return result; }
            if (!File.Exists(pptxPath)) { Logger.Log($"[DeckExport] 파일 없음 {pptxPath}"); return result; }
            if (indexes.Count == 0) return result;

            outDir = outDir ?? Path.Combine(Path.GetTempPath(), "TeampptAddin", "cache", "deckreco");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            PowerPoint.Presentation pres = null;
            try
            {
                pres = app.Presentations.Open(
                    pptxPath,
                    MsoTriState.msoTrue,    // ReadOnly
                    MsoTriState.msoFalse,   // Untitled
                    MsoTriState.msoFalse);  // WithWindow = False
                foreach (var idx in indexes)
                {
                    try
                    {
                        var png = Path.Combine(outDir, $"deck-slide-{idx}.png");
                        SlideImageRenderer.Render(pres, idx, png);
                        result[idx] = png;
                    }
                    catch (Exception ex) { Logger.Log($"[DeckExport] slide {idx} 실패: {ex.Message}"); }
                }
                pres.Close();
                Logger.Log($"[DeckExport] {result.Count}/{indexes.Count} PNG from {Path.GetFileName(pptxPath)}");
                return result;
            }
            finally { if (pres != null) Marshal.ReleaseComObject(pres); }
        }
    }
}
