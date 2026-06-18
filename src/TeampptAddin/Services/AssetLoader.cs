using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public static class AssetLoader
    {
        public static List<HeaderAsset> Load(string assetsDir)
        {
            var jsonPath = Path.Combine(assetsDir, "assets.json");

            var assets = File.Exists(jsonPath)
                ? LoadFromJson(jsonPath)
                : ScanFolder(assetsDir);

            return assets
                .Where(a => File.Exists(Path.Combine(assetsDir, a.File)))
                .ToList();
        }

        private static List<HeaderAsset> LoadFromJson(string jsonPath)
        {
            var json = File.ReadAllText(jsonPath);
            return JsonConvert.DeserializeObject<List<HeaderAsset>>(json)
                   ?? new List<HeaderAsset>();
        }

        private static List<HeaderAsset> ScanFolder(string assetsDir)
        {
            if (!Directory.Exists(assetsDir))
                return new List<HeaderAsset>();

            return Directory.GetFiles(assetsDir, "header_*.pptx")
                .OrderBy(f => f)
                .Select(f => new HeaderAsset
                {
                    File = Path.GetFileName(f),
                    Name = Path.GetFileNameWithoutExtension(f),
                    Category = "헤더",
                    ContentFit = new List<string>(),
                    UseWhen = "",
                    GridColumns = 1
                })
                .ToList();
        }
    }
}
