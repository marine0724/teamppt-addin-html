using System;
using System.IO;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>
    /// 슬라이드를 PNG로 렌더. 긴 변을 LlmImageLongEdgePx에 맞춰 다운스케일.
    /// LLM 화면읽기 전역 기본 = 768px (Gemini 1타일/258토큰, Claude ~442토큰).
    /// 텍스트는 pptx XML에서 읽으므로 OCR용 아님 → 768로 충분.
    /// </summary>
    public static class SlideImageRenderer
    {
        public const int LlmImageLongEdgePx = 768;

        public static void Render(
            PowerPoint.Presentation source,
            int slideIndex,
            string outputPngPath,
            int longEdgePx = LlmImageLongEdgePx)
        {
            var dir = Path.GetDirectoryName(outputPngPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            float slideW = source.PageSetup.SlideWidth;   // points
            float slideH = source.PageSetup.SlideHeight;

            int w, h;
            if (slideW >= slideH)
            {
                w = longEdgePx;
                h = (int)Math.Round(longEdgePx * (slideH / slideW));
            }
            else
            {
                h = longEdgePx;
                w = (int)Math.Round(longEdgePx * (slideW / slideH));
            }

            source.Slides[slideIndex].Export(outputPngPath, "PNG", w, h);
        }
    }
}
