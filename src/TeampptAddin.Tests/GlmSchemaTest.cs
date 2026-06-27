using System.Linq;
using Newtonsoft.Json.Linq;
using TeampptAddin;
using Xunit;

public class GlmSchemaTest
{
    // ── ToExample ──────────────────────────────────────────────────────────

    [Fact]
    public void StringProperty_BecomesAngleBracketPlaceholder()
    {
        var schema = JObject.Parse(@"{
            ""type"":""object"",
            ""properties"":{
                ""name"":{""type"":""string""},
                ""desc"":{""type"":""string"",""description"":""the title""}
            }
        }");
        var ex = GlmSchema.ToExample(schema);
        Assert.Equal("<string>", ex["name"].ToString());
        Assert.Equal("<the title>", ex["desc"].ToString());
    }

    [Fact]
    public void EnumProperty_BecomesBarSeparatedOptions()
    {
        var schema = JObject.Parse(@"{
            ""type"":""object"",
            ""properties"":{
                ""status"":{""type"":""string"",""enum"":[""active"",""inactive"",""pending""]}
            }
        }");
        var ex = GlmSchema.ToExample(schema);
        Assert.Equal("<active|inactive|pending>", ex["status"].ToString());
    }

    [Fact]
    public void NestedObject_IsRecursed()
    {
        var schema = JObject.Parse(@"{
            ""type"":""object"",
            ""properties"":{
                ""inner"":{
                    ""type"":""object"",
                    ""properties"":{""x"":{""type"":""number""}}
                }
            }
        }");
        var ex = GlmSchema.ToExample(schema);
        var inner = (JObject)ex["inner"];
        Assert.NotNull(inner);
        Assert.Equal(0.0, inner["x"].Value<double>());
    }

    [Fact]
    public void ArrayOfObjects_IsExpanded()
    {
        var schema = JObject.Parse(@"{
            ""type"":""object"",
            ""properties"":{
                ""items"":{
                    ""type"":""array"",
                    ""items"":{""type"":""object"",""properties"":{""id"":{""type"":""integer""}}}
                }
            }
        }");
        var ex = GlmSchema.ToExample(schema);
        var arr = (JArray)ex["items"];
        Assert.NotNull(arr);
        Assert.Equal(1, arr.Count);
        Assert.Equal(0, arr[0]["id"].Value<int>());
    }

    [Fact]
    public void IntegerProperty_BecomesZero()
    {
        var schema = JObject.Parse(@"{
            ""type"":""object"",
            ""properties"":{
                ""count"":{""type"":""integer""},
                ""score"":{""type"":""number""},
                ""flag"":{""type"":""boolean""}
            }
        }");
        var ex = GlmSchema.ToExample(schema);
        Assert.Equal(0, ex["count"].Value<int>());
        Assert.Equal(0.0, ex["score"].Value<double>());
        Assert.False(ex["flag"].Value<bool>());
    }

    // ── Normalize (기존) ────────────────────────────────────────────────────

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
