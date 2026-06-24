using System.Collections.Generic;
using Microsoft.Office.Core;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace TeampptAddin
{
    public class AssetShapeInfo
    {
        public string Id { get; set; }
        public string Kind { get; set; }
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public string SampleText { get; set; } = "";
    }

    /// <summary>
    /// 삽입된 에셋 도형(ShapeRange)을 매핑 입력용 인벤토리로 읽는다(COM).
    /// Id="a"+순번. SlotMapper가 초안 재료와 매칭할 대상.
    /// </summary>
    public static class AssetShapeInventory
    {
        public static List<AssetShapeInfo> Read(PowerPoint.ShapeRange shapes)
        {
            var list = new List<AssetShapeInfo>();
            if (shapes == null) return list;
            int n = 1;
            foreach (PowerPoint.Shape sh in shapes)
            {
                var info = new AssetShapeInfo
                {
                    Id = "a" + n++,
                    Left = sh.Left, Top = sh.Top, Width = sh.Width, Height = sh.Height,
                    Kind = "text"
                };
                if (sh.HasTable == MsoTriState.msoTrue) info.Kind = "table";
                else if (sh.HasChart == MsoTriState.msoTrue) info.Kind = "chart";
                else if (sh.Type == MsoShapeType.msoPicture) info.Kind = "image";
                else if (sh.HasTextFrame == MsoTriState.msoTrue && sh.TextFrame.HasText == MsoTriState.msoTrue)
                {
                    info.Kind = "text";
                    var t = sh.TextFrame.TextRange.Text ?? "";
                    info.SampleText = t.Length > 40 ? t.Substring(0, 40) : t;
                }
                list.Add(info);
            }
            return list;
        }
    }
}
