using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public static class TextMetrics
    {
        public static int CharCount(string text)
            => string.IsNullOrEmpty(text) ? 0 : text.Count(c => !char.IsWhiteSpace(c));

        public static int BulletCount(IEnumerable<string> paragraphs)
            => paragraphs?.Count(p => !string.IsNullOrWhiteSpace(p)) ?? 0;

        public static int MaxLevel(IEnumerable<int> paragraphLevels)
        {
            var list = paragraphLevels?.ToList() ?? new List<int>();
            return list.Count == 0 ? 0 : list.Max();
        }
    }
}
