using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public class ProvenanceEntry
    {
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string RemoteFile { get; set; } = "";
    }

    /// <summary>
    /// 조립된 슬라이드에 어떤 에셋이 적용됐는지 기록/복원. 슬라이드 Tags에 JSON으로 저장.
    /// 후속 단계(컴포넌트 교체·슬라이드별 대화형 추천)가 vision 없이 슬라이드→에셋을 역참조하는 토대.
    /// 포맷은 의도적으로 최소(Kind/Name/RemoteFile) — 소비자가 생기는 후속 단계에서 확장.
    /// </summary>
    public static class SlideProvenance
    {
        public const string TagName = "TEAMPPT_PROVENANCE";

        private static string RemotePath(HeaderAsset a)
            => a?.Extra != null && a.Extra.ContainsKey("remote_file")
                ? a.Extra["remote_file"].ToString()
                : a?.File ?? "";

        public static string Format(IEnumerable<RecommendedSlot> slots)
        {
            var entries = (slots ?? Enumerable.Empty<RecommendedSlot>())
                .Where(s => s?.Asset != null)
                .Select(s => new ProvenanceEntry
                {
                    Kind = s.Asset.Kind ?? "",
                    Name = s.Asset.Name ?? "",
                    RemoteFile = RemotePath(s.Asset)
                })
                .ToList();
            return JsonConvert.SerializeObject(entries);
        }

        public static List<ProvenanceEntry> Parse(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag)) return new List<ProvenanceEntry>();
            try
            {
                return JsonConvert.DeserializeObject<List<ProvenanceEntry>>(tag)
                       ?? new List<ProvenanceEntry>();
            }
            catch
            {
                return new List<ProvenanceEntry>();
            }
        }
    }
}
