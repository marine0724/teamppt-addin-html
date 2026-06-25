using System.Collections.Generic;

namespace TeampptAddin
{
    public class DeckSlideStructure
    {
        public int Index { get; set; }        // 1-based 슬라이드 인덱스
        public string Kind { get; set; } = ""; // cover/toc/body/section/end
        public string Label { get; set; } = ""; // 짧은 역할 라벨
    }

    public class DeckStructure
    {
        public List<DeckSlideStructure> Slides { get; set; } = new List<DeckSlideStructure>();
        public int TotalCount { get; set; }
    }
}
