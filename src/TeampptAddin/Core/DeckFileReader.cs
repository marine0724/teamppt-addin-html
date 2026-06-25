using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>
    /// 외부 초안 .pptx를 창 없이 ReadOnly로 열어 전 슬라이드를 DraftProfile[]로 읽는다(비파괴).
    /// 텍스트·메트릭만(D1 1단 — 이미지 렌더 없음). 반드시 Close + Release.
    /// </summary>
    public static class DeckFileReader
    {
        public static List<DraftProfile> ReadFile(string pptxPath)
        {
            var app = Globals.Application;
            var profiles = new List<DraftProfile>();
            if (app == null) { Logger.Log("DeckFileReader: app is null"); return profiles; }
            if (!File.Exists(pptxPath)) { Logger.Log($"DeckFileReader: 파일 없음 {pptxPath}"); return profiles; }

            PowerPoint.Presentation pres = null;
            try
            {
                pres = app.Presentations.Open(
                    pptxPath,
                    MsoTriState.msoTrue,    // ReadOnly
                    MsoTriState.msoFalse,   // Untitled
                    MsoTriState.msoFalse);  // WithWindow = False

                float w = pres.PageSetup.SlideWidth, h = pres.PageSetup.SlideHeight;
                foreach (PowerPoint.Slide slide in pres.Slides)
                    profiles.Add(DraftSlideReader.ReadSlide(slide, w, h));

                pres.Close();
                Logger.Log($"[DeckFileReader] {profiles.Count} slides from {Path.GetFileName(pptxPath)}");
                return profiles;
            }
            finally
            {
                if (pres != null) Marshal.ReleaseComObject(pres);
            }
        }
    }
}
