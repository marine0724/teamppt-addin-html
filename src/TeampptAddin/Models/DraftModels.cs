using System.Collections.Generic;

namespace TeampptAddin
{
    public class DraftShape
    {
        public int Id { get; set; }
        public string Kind { get; set; }        // text|image|table|chart
        public string Text { get; set; } = "";
        public float Left { get; set; }
        public float Top { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int CharCount { get; set; }
        public int BulletCount { get; set; }
        public int MaxLevel { get; set; }
    }

    public class DraftProfile
    {
        public int SlideIndex { get; set; }
        public float SlideWidth { get; set; }
        public float SlideHeight { get; set; }
        public List<DraftShape> Shapes { get; set; } = new List<DraftShape>();
    }

    public class DraftMaterial
    {
        public string Role { get; set; }
        public string Type { get; set; }
        public int SourceShapeId { get; set; }
        public string Text { get; set; } = "";
        public int CharCount { get; set; }
        public int BulletCount { get; set; }
        public int Level { get; set; }
        public string Emphasis { get; set; }
    }

    public class DraftUnderstanding
    {
        public List<DraftMaterial> Materials { get; set; } = new List<DraftMaterial>();
        public Dictionary<string, int> Counts { get; set; } = new Dictionary<string, int>();
        public string LayoutShape { get; set; } = "";
        public string DesignSummary { get; set; } = "";
        public List<string> DominantColors { get; set; } = new List<string>();
        public string MatchIntent { get; set; } = "";
        public string SlideKind { get; set; } = "";
    }

    public class SlotMapping
    {
        public int DraftShapeId { get; set; }
        public string AssetShapeId { get; set; }
        public string FitNote { get; set; } = "";
        public double Confidence { get; set; }
    }

    public class MappingResult
    {
        public List<SlotMapping> Mappings { get; set; } = new List<SlotMapping>();
        public List<int> Overflow { get; set; } = new List<int>();
        public List<string> Empty { get; set; } = new List<string>();
    }
}
