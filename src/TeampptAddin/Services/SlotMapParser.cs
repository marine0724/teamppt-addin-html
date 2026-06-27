using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class SlotMapParser
    {
        public static MappingResult Parse(string llmJson, IEnumerable<int> draftShapeIds, IEnumerable<string> assetShapeIds)
        {
            var drafts = new HashSet<int>(draftShapeIds);
            var assets = new HashSet<string>(assetShapeIds);
            var o = JObject.Parse(llmJson);

            var mappings = new List<SlotMapping>();
            foreach (var m in (o["mappings"] as JArray) ?? new JArray())
            {
                var did = m["draftShapeId"]?.Value<int>() ?? -1;
                var aid = m["assetShapeId"]?.ToString();
                if (!drafts.Contains(did) || aid == null || !assets.Contains(aid)) continue;
                mappings.Add(new SlotMapping
                {
                    DraftShapeId = did,
                    AssetShapeId = aid,
                    FitNote = m["fitNote"]?.ToString() ?? "",
                    Confidence = m["confidence"]?.Value<double>() ?? 0
                });
            }

            var usedDrafts = new HashSet<int>(mappings.Select(x => x.DraftShapeId));
            var usedAssets = new HashSet<string>(mappings.Select(x => x.AssetShapeId));
            return new MappingResult
            {
                Mappings = mappings,
                Overflow = drafts.Where(d => !usedDrafts.Contains(d)).ToList(),
                Empty = assets.Where(a => !usedAssets.Contains(a)).ToList()
            };
        }
    }
}
