using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    public static class CatalogBuilder
    {
        public static List<CatalogEntry> Build(IEnumerable<HeaderAsset> assets)
        {
            return assets.Select(a => new CatalogEntry
            {
                File = a.File,
                Name = a.Name,
                Kind = a.Kind,
                Category = a.Category,
                Tags = a.Tags ?? new List<string>(),
                UseWhen = a.UseWhen,
                ContentFit = a.ContentFit ?? new List<string>()
            }).ToList();
        }
    }
}
