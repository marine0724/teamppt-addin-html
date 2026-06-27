using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DeckStructureSchema
    {
        public static JObject BuildResponseSchema()
        {
            var slide = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["index"] = new JObject { ["type"] = "integer" },
                    ["kind"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "cover", "toc", "body", "section", "end" } },
                    ["label"] = new JObject { ["type"] = "string" }
                },
                ["required"] = new JArray { "index", "kind", "label" }
            };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject { ["slides"] = new JObject { ["type"] = "array", ["items"] = slide } },
                ["required"] = new JArray { "slides" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 덱의 '디자인 구조'를 분석하는 엔진이야. 슬라이드별 텍스트 요약 목록(JSON)을 받아, 각 슬라이드의 역할(kind)과 짧은 라벨을 매겨.
## 절대 제약
- 디자인 구조(흐름)만 본다. 내용을 새로 기획하거나 메시지를 바꾸지 마라.
## 네 일
- 각 슬라이드에 kind 부여: cover(표지)/toc(목차)/body(본문)/section(섹션 구분 표지)/end(마무리).
- label: 그 슬라이드 역할을 2~8자 한국어로 (예: 목차, 회사소개, 장점 3단, 팀 소개, 연락처). 표지/엔드는 '표지'/'마무리'.
- index는 입력으로 받은 슬라이드 번호를 그대로 써라.
- 모르면 지어내지 말고 보수적으로 body.";
        }
    }
}
