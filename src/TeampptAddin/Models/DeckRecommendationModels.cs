// src/TeampptAddin/Models/DeckRecommendationModels.cs
using System.Collections.Generic;

namespace TeampptAddin
{
    /// <summary>본문 슬라이드를 도형 시그니처로 묶은 패턴(대표 1장 + 같은 패턴 장들). 토큰0.</summary>
    public class BodyPattern
    {
        public string Signature { get; set; } = "";
        public List<int> SlideIndexes { get; set; } = new List<int>();   // 1-based
        public int RepresentativeIndex { get; set; }
    }

    /// <summary>박스 하나의 계획. BoxKind = cover/header/body/toc/section/end.</summary>
    public class BoxPlan
    {
        public string BoxKind { get; set; } = "";
        public string Label { get; set; } = "";
        public List<int> CoveredSlideIndexes { get; set; } = new List<int>();
        public int? RepresentativeIndex { get; set; }
        public string Signature { get; set; } = "";   // body 박스만(패턴 식별)
    }

    /// <summary>컨셉적합 점수(계산값, 토큰0).</summary>
    public class ConceptFitResult
    {
        public int Score { get; set; }     // 0-100
        public string Note { get; set; } = "";
    }

    /// <summary>박스 하나의 추천 결과 + 두 배지.</summary>
    public class BoxRecommendation
    {
        public BoxPlan Plan { get; set; }
        public CombinationRecommendation Recommendation { get; set; }
        public MaterialFitResult MaterialFit { get; set; }
        public ConceptFitResult ConceptFit { get; set; }
    }

    /// <summary>덱 전체 박스별 추천.</summary>
    public class DeckRecommendation
    {
        public List<BoxRecommendation> Boxes { get; set; } = new List<BoxRecommendation>();
        public DesignConcept Concept { get; set; }
    }
}
