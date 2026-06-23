using Newtonsoft.Json;
using TeampptAddin;
using Xunit;

namespace TeampptAddin.Tests
{
    public class NormalizedPaletteTest
    {
        [Fact]
        public void NormalizedPalette_Holds_Five_Roles()
        {
            var np = new NormalizedPalette
            {
                Background = "#FFFFFF", Main = "#2563EB",
                Sub1 = "#3B82F6", Sub2 = "#93C5FD", Text = "#1E293B"
            };
            Assert.Equal("#FFFFFF", np.Background);
            Assert.Equal("#1E293B", np.Text);
        }

        [Fact]
        public void PaletteColors_Background_Serializes_As_background()
        {
            var json = JsonConvert.SerializeObject(new PaletteColors { Background = "#0A1428" });
            Assert.Contains("\"background\":\"#0A1428\"", json);
        }

        [Fact]
        public void PaletteColors_Without_Background_Deserializes_Null()
        {
            var pc = JsonConvert.DeserializeObject<PaletteColors>("{\"main\":\"#2563EB\"}");
            Assert.Null(pc.Background);
            Assert.Equal("#2563EB", pc.Main);
        }
    }
}
