using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class SlideDiagnosisParser
    {
        public static SlideDiagnosis Parse(string llmJson)
        {
            var o = JObject.Parse(llmJson);
            var questions = (o["questions"] as JArray)?
                .Select(t => t.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Take(3)
                .ToList() ?? new List<string>();

            return new SlideDiagnosis
            {
                Message = o["message"]?.ToString() ?? "",
                SuggestedQuestions = questions
            };
        }
    }
}
