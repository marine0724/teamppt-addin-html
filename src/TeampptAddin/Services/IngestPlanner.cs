using System.Collections.Generic;

namespace TeampptAddin
{
    /// <summary>섹션 목록 → 슬라이드 1장=에셋 1개 분할 계획. 순번은 카테고리별 1부터.</summary>
    public static class IngestPlanner
    {
        public static List<AssetSplitItem> Plan(IReadOnlyList<SectionInfo> sections)
        {
            var result = new List<AssetSplitItem>();
            if (sections == null) return result;

            foreach (var section in sections)
            {
                for (int offset = 0; offset < section.SlideCount; offset++)
                {
                    int sequence = offset + 1;
                    var id = AssetIdGenerator.Make(section.Name, sequence);
                    result.Add(new AssetSplitItem
                    {
                        SourceSlideIndex = section.FirstSlideIndex + offset,
                        Category = section.Name,
                        AssetId = id,
                        PptxFileName = id + ".pptx",
                        ThumbFileName = id + ".png",
                    });
                }
            }
            return result;
        }
    }
}
