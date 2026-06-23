using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    public static class ShapeInserter
    {
        /// <summary>
        /// 소스 pptx의 shape를 활성 슬라이드에 삽입하고 ShapeRange를 반환.
        /// Presentations.Open 대신 Slides.InsertFromFile을 사용하여 Undo 스택을 보존한다.
        /// </summary>
        public static PowerPoint.ShapeRange InsertToActiveSlide(string headerPptxPath)
        {
            var app = Globals.Application;
            if (app == null) return null;

            var window = app.ActiveWindow;
            if (window == null) return null;

            var pres = app.ActivePresentation;
            var targetSlide = (PowerPoint.Slide)window.View.Slide;

            app.StartNewUndoEntry();

            int insertAfter = pres.Slides.Count;
            pres.Slides.InsertFromFile(headerPptxPath, insertAfter, 1, 1);
            var tempSlide = pres.Slides[insertAfter + 1];

            int count = tempSlide.Shapes.Count;
            if (count == 0)
            {
                tempSlide.Delete();
                return null;
            }

            var indices = new int[count];
            for (int i = 0; i < count; i++)
                indices[i] = i + 1;

            tempSlide.Shapes.Range(indices).Cut();
            var pasted = targetSlide.Shapes.Paste();

            tempSlide.Delete();

            return pasted;
        }

        /// <summary>
        /// 드래그용: 클립보드에 shape 복사 (Undo 스택 보존 버전).
        /// InsertFromFile로 임시 슬라이드를 만들고 shape를 복사한 뒤 삭제.
        /// </summary>
        public static void CopyShapesToClipboard(string headerPptxPath)
        {
            var app = Globals.Application;
            if (app == null) return;

            var pres = app.ActivePresentation;
            if (pres == null) return;

            int insertAfter = pres.Slides.Count;
            pres.Slides.InsertFromFile(headerPptxPath, insertAfter, 1, 1);
            var tempSlide = pres.Slides[insertAfter + 1];

            int count = tempSlide.Shapes.Count;
            if (count > 0)
            {
                var indices = new int[count];
                for (int i = 0; i < count; i++)
                    indices[i] = i + 1;

                tempSlide.Shapes.Range(indices).Copy();
            }

            tempSlide.Delete();
        }
    }
}
