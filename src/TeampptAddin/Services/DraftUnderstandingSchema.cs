using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DraftUnderstandingSchema
    {
        public static JObject BuildResponseSchema()
        {
            JObject Str() => new JObject { ["type"] = "string" };
            JObject Int() => new JObject { ["type"] = "integer" };
            JObject StrArr() => new JObject { ["type"] = "array", ["items"] = Str() };

            var material = new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["role"] = Str(),
                    ["type"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "text", "image", "table", "chart" } },
                    ["sourceShapeId"] = Int(),
                    ["emphasis"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "heading", "normal", "small" } }
                },
                ["required"] = new JArray { "role", "type", "sourceShapeId", "emphasis" }
            };

            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["materials"] = new JObject { ["type"] = "array", ["items"] = material },
                    ["counts"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["textBlocks"] = Int(), ["bullets"] = Int(), ["images"] = Int(),
                            ["tables"] = Int(), ["charts"] = Int()
                        },
                        ["required"] = new JArray { "textBlocks", "bullets", "images", "tables", "charts" }
                    },
                    ["layoutShape"] = Str(),
                    ["designSummary"] = Str(),
                    ["dominantColors"] = StrArr(),
                    ["matchIntent"] = Str(),
                    ["slideKind"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "cover", "toc", "body", "section", "end" } }
                },
                ["required"] = new JArray { "materials", "counts", "layoutShape", "designSummary", "dominantColors", "matchIntent", "slideKind" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 발표 초안 슬라이드를 분석하는 엔진이야. 슬라이드 이미지 1장과 도형 목록(JSON)을 받아, 이 초안을 적합한 디자인 에셋과 매칭하기 위한 구조화 표현을 만들어.

## 입력
- 이미지: 초안 슬라이드 렌더.
- 도형 목록: 각 도형의 id, kind(text/image/table/chart), 텍스트, 위치/크기, 글자수/불릿수. 이 값들이 사실이다.

## 네 일 (역할 판단만)
- materials: 각 도형(id)에 역할(title/subtitle/body/bullet/caption/image/table/chart/logo)과 강조(heading/normal/small)를 부여. sourceShapeId는 입력 도형 id를 그대로 써라. 텍스트 내용은 만들지 마라(사실은 입력에 있다).
- counts: 종류별 개수.
- layoutShape: 현재 골격을 짧게 (예: 'title-top + body-left + image-right').
- designSummary: 디자인 현황과 약점 1~2문장.
- dominantColors: 보이는 주요 색 hex 1~3개.
- matchIntent: 이 초안에 어울리는 에셋을 검색할 자연어 한 문장 (재료 종류·양 반영).
- slideKind: cover/toc/body/section/end 중 하나.
모르면 지어내지 말고 보수적으로.";
        }
    }
}
