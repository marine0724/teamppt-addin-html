using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    public static class ThumbnailGenerator
    {
        public static void Generate(string headerPptxPath, string outputPngPath)
        {
            var app = Globals.Application;
            if (app == null)
            {
                Logger.Log("GenerateThumbnail: app is null");
                return;
            }
            if (File.Exists(outputPngPath)) return;

            var dir = Path.GetDirectoryName(outputPngPath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            PowerPoint.Presentation pres = null;
            try
            {
                pres = app.Presentations.Open(
                    headerPptxPath,
                    MsoTriState.msoTrue,
                    MsoTriState.msoFalse,
                    MsoTriState.msoFalse);

                var slide = pres.Slides[1];
                int count = slide.Shapes.Count;
                if (count == 0) return;

                try
                {
                    ExportShapesOnly(slide, count, outputPngPath);
                    Logger.Log($"Shape-only export OK: {Path.GetFileName(outputPngPath)}");
                }
                catch (Exception ex)
                {
                    Logger.Log($"Shape-only export failed, fallback to composite: {ex.Message}");
                    try
                    {
                        ExportShapesComposite(slide, count, outputPngPath);
                        Logger.Log($"Composite export OK: {Path.GetFileName(outputPngPath)}");
                    }
                    catch (Exception ex2)
                    {
                        Logger.Log($"Composite export failed, fallback to slide: {ex2.Message}");
                        ExportSlideNoBackground(slide, outputPngPath);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"GenerateThumbnail FAILED: {ex.Message}");
                throw;
            }
            finally
            {
                if (pres != null)
                {
                    pres.Close();
                    Marshal.ReleaseComObject(pres);
                }
            }
        }

        private static void ExportShapesOnly(PowerPoint.Slide slide, int count, string outputPath)
        {
            var indices = new int[count];
            for (int i = 0; i < count; i++)
                indices[i] = i + 1;

            var range = slide.Shapes.Range(indices);

            if (count == 1)
            {
                range[1].Export(outputPath, PowerPoint.PpShapeFormat.ppShapeFormatPNG);
            }
            else
            {
                var group = range.Group();
                group.Export(outputPath, PowerPoint.PpShapeFormat.ppShapeFormatPNG);
                group.Ungroup();
            }
        }

        private static void ExportShapesComposite(PowerPoint.Slide slide, int count, string outputPath)
        {
            float minL = float.MaxValue, minT = float.MaxValue;
            float maxR = float.MinValue, maxB = float.MinValue;

            for (int i = 1; i <= count; i++)
            {
                var s = slide.Shapes[i];
                float l = s.Left, t = s.Top, w = s.Width, h = s.Height;
                if (l < minL) minL = l;
                if (t < minT) minT = t;
                if (l + w > maxR) maxR = l + w;
                if (t + h > maxB) maxB = t + h;
            }

            float totalW = maxR - minL;
            float totalH = maxB - minT;
            if (totalW < 1 || totalH < 1) throw new InvalidOperationException("Empty bounding box");

            const float dpi = 96f;
            const float ptsPerInch = 72f;
            int canvasW = (int)(totalW * dpi / ptsPerInch);
            int canvasH = (int)(totalH * dpi / ptsPerInch);

            var tempDir = Path.Combine(Path.GetTempPath(), "teamppt_comp_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(tempDir);

            try
            {
                using (var canvas = new Bitmap(canvasW, canvasH, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(canvas))
                    {
                        g.Clear(Color.Transparent);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.CompositingMode = CompositingMode.SourceOver;

                        for (int i = 1; i <= count; i++)
                        {
                            var shape = slide.Shapes[i];
                            var tmpPng = Path.Combine(tempDir, $"s{i}.png");
                            try
                            {
                                shape.Export(tmpPng, PowerPoint.PpShapeFormat.ppShapeFormatPNG);
                            }
                            catch
                            {
                                continue;
                            }

                            if (!File.Exists(tmpPng)) continue;

                            using (var partImg = Image.FromFile(tmpPng))
                            {
                                float dx = (shape.Left - minL) * dpi / ptsPerInch;
                                float dy = (shape.Top - minT) * dpi / ptsPerInch;
                                float dw = shape.Width * dpi / ptsPerInch;
                                float dh = shape.Height * dpi / ptsPerInch;
                                g.DrawImage(partImg, dx, dy, dw, dh);
                            }
                        }
                    }

                    canvas.Save(outputPath, ImageFormat.Png);
                }
            }
            finally
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }

        private static void ExportSlideNoBackground(PowerPoint.Slide slide, string outputPath)
        {
            bool bgWasVisible = true;
            try
            {
                bgWasVisible = slide.FollowMasterBackground == MsoTriState.msoTrue
                    || slide.Background.Fill.Visible == MsoTriState.msoTrue;
                slide.FollowMasterBackground = MsoTriState.msoFalse;
                slide.Background.Fill.Visible = MsoTriState.msoFalse;
            }
            catch { }

            try
            {
                var pres = slide.Parent as PowerPoint.Presentation;
                float sw = pres.PageSetup.SlideWidth;
                float sh = pres.PageSetup.SlideHeight;
                const float dpi = 96f;
                const float ptsPerInch = 72f;
                int pw = (int)(sw * dpi / ptsPerInch);
                int ph = (int)(sh * dpi / ptsPerInch);
                slide.Export(outputPath, "PNG", pw, ph);
                Logger.Log($"Slide no-bg export OK: {Path.GetFileName(outputPath)}");
            }
            finally
            {
                try
                {
                    if (bgWasVisible)
                    {
                        slide.FollowMasterBackground = MsoTriState.msoTrue;
                    }
                }
                catch { }
            }
        }
    }
}
