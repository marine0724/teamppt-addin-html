using System.Collections.Generic;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    /// <summary>
    /// 슬라이드를 COM으로 읽어 DraftProfile(정확한 텍스트·위치·타입·메트릭, shapeId)을 만든다.
    /// 사실의 출처. LLM 판단은 DraftUnderstandingService/DeckStructureService에서.
    /// </summary>
    public static class DraftSlideReader
    {
        public static DraftProfile ReadCurrentSlide()
        {
            var app = Globals.Application;
            var win = app?.ActiveWindow;
            if (win == null) return null;

            PowerPoint.Slide slide;
            try { slide = (PowerPoint.Slide)win.View.Slide; }
            catch { return null; }
            if (slide == null) return null;

            var pres = win.Presentation;
            return ReadSlide(slide, pres.PageSetup.SlideWidth, pres.PageSetup.SlideHeight);
        }

        /// <summary>주어진 슬라이드 1장을 DraftProfile로 읽는다(현재슬라이드/파일 공용).</summary>
        public static DraftProfile ReadSlide(PowerPoint.Slide slide, float slideW, float slideH)
        {
            var profile = new DraftProfile
            {
                SlideIndex = slide.SlideIndex,
                SlideWidth = slideW,
                SlideHeight = slideH
            };

            int id = 1;
            foreach (PowerPoint.Shape sh in slide.Shapes)
            {
                var ds = new DraftShape
                {
                    Id = id++,
                    Left = sh.Left, Top = sh.Top, Width = sh.Width, Height = sh.Height,
                    Kind = "text"
                };

                if (sh.HasTable == MsoTriState.msoTrue) ds.Kind = "table";
                else if (sh.HasChart == MsoTriState.msoTrue) ds.Kind = "chart";
                else if (sh.Type == MsoShapeType.msoPicture) ds.Kind = "image";
                else if (sh.HasTextFrame == MsoTriState.msoTrue &&
                         sh.TextFrame.HasText == MsoTriState.msoTrue)
                {
                    ds.Kind = "text";
                    var paras = new List<string>();
                    var levels = new List<int>();
                    var tr = sh.TextFrame.TextRange;
                    foreach (PowerPoint.TextRange p in tr.Paragraphs())
                    {
                        paras.Add(p.Text);
                        levels.Add(p.IndentLevel);
                    }
                    ds.Text = tr.Text;
                    ds.CharCount = TextMetrics.CharCount(tr.Text);
                    ds.BulletCount = TextMetrics.BulletCount(paras);
                    ds.MaxLevel = TextMetrics.MaxLevel(levels);
                }
                else
                {
                    continue; // 빈 장식 도형은 스킵
                }

                profile.Shapes.Add(ds);
            }

            Logger.Log($"[DraftReader] slide={profile.SlideIndex} shapes={profile.Shapes.Count}");
            return profile;
        }
    }
}
