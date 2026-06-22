using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public static class EmbedTextBuilder
    {
        public static string Build(AssetUnderstanding u)
        {
            var a = u.Asset;
            var lines = new List<string> { a.Name, a.Category, a.UseWhen };
            lines.AddRange(a.ContentFit ?? new List<string>());
            lines.AddRange(a.Tags ?? new List<string>());
            lines.AddRange(u.ExampleIntents ?? new List<string>());
            return string.Join("\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
    }
}
