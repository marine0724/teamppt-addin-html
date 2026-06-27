using TeampptAddin;
using Xunit;

public class GlmAiServiceTest
{
    [Fact]
    public void ExtractContent_ReturnsMessageContent()
    {
        var body = @"{""choices"":[{""message"":{""content"":""{\""x\"":1}""}}],
                      ""usage"":{""prompt_tokens"":5,""completion_tokens"":3,""total_tokens"":8}}";
        Assert.Equal("{\"x\":1}", GlmAiService.ExtractContent(body));
    }

    [Fact]
    public void ExtractContent_ThrowsWhenEmpty()
    {
        Assert.ThrowsAny<System.Exception>(() =>
            GlmAiService.ExtractContent(@"{""choices"":[]}"));
    }
}
