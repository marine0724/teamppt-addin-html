using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public class RecommendationCache
    {
        private readonly string _path;
        public RecommendationCache(string path = null) { _path = path ?? DefaultPath; }

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeampptAddin", "cache", "last-candidates.json");

        public void Save(List<HeaderAsset> candidates)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path));
                File.WriteAllText(_path, JsonConvert.SerializeObject(candidates));
            }
            catch (Exception ex) { Logger.Log($"[Cache] save 실패: {ex.Message}"); }
        }

        public List<HeaderAsset> Load()
        {
            try
            {
                if (!File.Exists(_path)) return new List<HeaderAsset>();
                return JsonConvert.DeserializeObject<List<HeaderAsset>>(File.ReadAllText(_path))
                       ?? new List<HeaderAsset>();
            }
            catch (Exception ex) { Logger.Log($"[Cache] load 실패: {ex.Message}"); return new List<HeaderAsset>(); }
        }
    }
}
