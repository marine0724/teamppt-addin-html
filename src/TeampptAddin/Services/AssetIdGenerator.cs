using System.Linq;

namespace TeampptAddin
{
    /// <summary>섹션명 + 순번 → 파일명 안전한 에셋 ID. 예: ("표지", 1) → "표지_01".</summary>
    public static class AssetIdGenerator
    {
        private static readonly char[] Unsafe = { '\\', '/', ':', '*', '?', '"', '<', '>', '|', ' ', '(', ')' };

        public static string Make(string category, int sequence)
        {
            var safe = new string((category ?? "asset")
                .Select(c => Unsafe.Contains(c) ? '_' : c)
                .ToArray());
            return $"{safe}_{sequence:D2}";
        }
    }
}
