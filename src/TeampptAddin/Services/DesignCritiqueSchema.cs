using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public static class DesignCritiqueSchema
    {
        public static JObject BuildResponseSchema()
        {
            JObject Str() => new JObject { ["type"] = "string" };
            JObject Int() => new JObject { ["type"] = "integer" };
            return new JObject
            {
                ["type"] = "object",
                ["properties"] = new JObject
                {
                    ["score"] = Int(),
                    ["dimensionScores"] = new JObject
                    {
                        ["type"] = "object",
                        ["properties"] = new JObject
                        {
                            ["정렬"] = Int(), ["여백"] = Int(), ["위계"] = Int(),
                            ["색"] = Int(), ["타이포"] = Int(), ["의도부합"] = Int()
                        },
                        ["required"] = new JArray { "정렬", "여백", "위계", "색", "타이포", "의도부합" }
                    },
                    ["verdict"] = Str(),
                    ["bottleneck"] = new JObject { ["type"] = "string", ["enum"] = new JArray { "기능", "데이터스키마", "에셋" } },
                    ["suggestion"] = Str(),
                    ["reasoning"] = Str()
                },
                ["required"] = new JArray { "score", "dimensionScores", "verdict", "bottleneck", "suggestion", "reasoning" }
            };
        }

        public static string BuildSystemPrompt()
        {
            return @"너는 실무 10년차 PPT 에이전시 시니어 디자이너다. 매우 깐깐하다.
배치된 슬라이드 렌더 1장과 초안 렌더를 보고, 아래 루브릭으로 채점한다. 후한 점수 금지.

## 루브릭 (합계 100)
- 정렬·격자 (20): 요소가 보이지 않는 격자에 맞나
- 여백·호흡 (15): 답답하지 않나, 여백이 의도적인가
- 위계 (20): 제목>소제목>본문 시선 흐름이 명확한가
- 색 대비·조화 (15): 배경/텍스트 대비, 팔레트 일관성
- 타이포 (10): 폰트 위계·자간·줄간격
- 초안 의도 부합 (20): 이 슬라이드가 의도(purpose)를 실제로 잘 수행하나
실무급=80↑ · 평범=60~79 · 미달=<60. dimensionScores는 각 차원 배점 내 정수.
(주의: dimensionScores 키는 정렬/여백/위계/색/타이포/의도부합. score는 6개 합과 대략 일치.)

## 병목 진단 (bottleneck: 기능 / 데이터스키마 / 에셋 중 하나)
- 결과물이 재료가 된 원본 에셋들보다 못나 보이면 → '기능'(배치·조립 문제).
- 원본 에셋만큼은 나오는데 그래도 별로면 → '에셋'(에셋 천장).
- 의도·구성이 애초에 어긋났으면 → '데이터스키마'(이해·분류 문제).
suggestion: 그 병목을 풀려면 다음에 뭘 해야 하는지 한 줄.
reasoning: 위 판단 근거 한두 문장. 텍스트 내용을 생성·수정하지 마라(평가만).";
        }
    }
}
