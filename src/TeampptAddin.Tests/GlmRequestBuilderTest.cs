using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using TeampptAddin;
using Xunit;

public class GlmRequestBuilderTest
{
    private static JObject Schema() =>
        JObject.Parse(@"{""type"":""object"",""properties"":{""x"":{""type"":""string""}}}");

    [Fact]
    public void TextMode_UsesFlashModel_StringContent()
    {
        var body = GlmRequestBuilder.Build("sys", "hello", null, Schema(), 0.7, 0);
        Assert.Equal("glm-4.7-flash", body["model"].ToString());
        var msgs = (JArray)body["messages"];
        Assert.Equal("system", msgs[0]["role"].ToString());
        Assert.Equal("user", msgs[1]["role"].ToString());
        Assert.Equal(JTokenType.String, msgs[1]["content"].Type); // 텍스트는 문자열
        Assert.Equal("hello", msgs[1]["content"].ToString());
    }

    [Fact]
    public void ImageMode_UsesVisionModel_ContentArrayWithDataUrl()
    {
        var imgs = new List<string> { "QUJD" }; // base64
        var body = GlmRequestBuilder.Build("sys", "look", imgs, Schema(), 0.4, 0);
        Assert.Equal("glm-4.6v-flash", body["model"].ToString());
        var content = (JArray)body["messages"][1]["content"];
        Assert.Equal("text", content[0]["type"].ToString());
        Assert.Equal("image_url", content[1]["type"].ToString());
        Assert.Equal("data:image/png;base64,QUJD", content[1]["image_url"]["url"].ToString());
    }

    [Fact]
    public void ResponseFormat_IsJsonObject()
    {
        // z.ai docs: response_format은 json_object만 지원(json_schema 미지원)
        var body = GlmRequestBuilder.Build("sys", "hi", null, Schema(), 0.5, 0);
        Assert.Equal("json_object", body["response_format"]["type"].ToString());
    }

    [Fact]
    public void SchemaIsEmbeddedInSystemPrompt()
    {
        // 구조 강제는 response_format 대신 system 프롬프트의 스키마로 한다
        var body = GlmRequestBuilder.Build("sys", "hi", null, Schema(), 0.5, 0);
        var sys = body["messages"][0]["content"].ToString();
        Assert.Contains("\"x\"", sys); // 스키마 속성명이 시스템 프롬프트에 포함됨
    }

    [Fact]
    public void SchemaEmbeddedAsExample_NotRawSchema()
    {
        // ToExample 변환 후 임베딩 — "properties" 같은 raw schema 키워드가 아니라
        // "<string>" 같은 placeholder 예시가 시스템 프롬프트에 들어가야 한다
        var body = GlmRequestBuilder.Build("sys", "hi", null, Schema(), 0.5, 0);
        var sys = body["messages"][0]["content"].ToString();
        Assert.Contains("\"x\"", sys);       // 필드명은 남아있어야
        Assert.Contains("<string>", sys);    // 예시 placeholder 포함
        Assert.DoesNotContain("\"properties\"", sys); // raw schema 키워드 없음
    }

    [Fact]
    public void Thinking_EnabledWhenBudgetPositive_DisabledWhenZero()
    {
        Assert.Equal("enabled",
            GlmRequestBuilder.Build("s","u",null,Schema(),0.4,512)["thinking"]["type"].ToString());
        Assert.Equal("disabled",
            GlmRequestBuilder.Build("s","u",null,Schema(),0.4,0)["thinking"]["type"].ToString());
    }

    [Fact]
    public void Temperature_IsForwarded()
    {
        var body = GlmRequestBuilder.Build("s", "u", null, Schema(), 0.33, 0);
        Assert.Equal(0.33, body["temperature"].Value<double>(), 3);
    }
}
