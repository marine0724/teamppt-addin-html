using Newtonsoft.Json.Linq;
using TeampptAddin;
using Xunit;

public class GlmSchemaTest
{
    [Fact]
    public void LowercasesTypes_Recursively()
    {
        var input = JObject.Parse(@"{
            ""type"":""OBJECT"",
            ""properties"":{
                ""items"":{ ""type"":""ARRAY"", ""items"":{ ""type"":""STRING"" } }
            }
        }");
        var outp = GlmSchema.Normalize(input);
        Assert.Equal("object", outp["type"].ToString());
        Assert.Equal("array", outp["properties"]["items"]["type"].ToString());
        Assert.Equal("string", outp["properties"]["items"]["items"]["type"].ToString());
    }

    [Fact]
    public void DoesNotMutateInput()
    {
        var input = JObject.Parse(@"{""type"":""OBJECT""}");
        GlmSchema.Normalize(input);
        Assert.Equal("OBJECT", input["type"].ToString()); // 원본 보존
    }

    [Fact]
    public void PassesThroughAlreadyLowercase()
    {
        var input = JObject.Parse(@"{""type"":""object"",""properties"":{""n"":{""type"":""number""}}}");
        var outp = GlmSchema.Normalize(input);
        Assert.Equal("object", outp["type"].ToString());
        Assert.Equal("number", outp["properties"]["n"]["type"].ToString());
    }
}
