using TeampptAddin;
using Xunit;

namespace TeampptAddin.Tests
{
    public class PaletteGeneratorTest
    {
        private static NormalizedPalette DarkSample() => new NormalizedPalette
        {
            Background = "#0A1428",
            Main = "#5DBEE0",
            Sub1 = "#3B82F6",
            Sub2 = "#93C5FD",
            Text = "#FFFFFF"
        };

        [Fact]
        public void Null_Returns_Empty()
        {
            Assert.Empty(PaletteGenerator.Generate(null));
        }

        [Fact]
        public void Produces_Original_And_Inverted()
        {
            var list = PaletteGenerator.Generate(DarkSample());
            Assert.Equal(2, list.Count);
            Assert.Equal("asset-original", list[0].Id);
            Assert.Equal("asset-inverted", list[1].Id);
            Assert.Equal("원본", list[0].Name);
            Assert.Equal("반전", list[1].Name);
        }

        [Fact]
        public void Original_Mirrors_Input()
        {
            var orig = PaletteGenerator.Generate(DarkSample())[0];
            Assert.Equal("#0A1428", orig.Colors.Background);
            Assert.Equal("#5DBEE0", orig.Colors.Main);
            Assert.Equal("#FFFFFF", orig.Colors.Text);
        }

        [Fact]
        public void Inverted_Flips_Background_And_Text_Lightness()
        {
            var list = PaletteGenerator.Generate(DarkSample());
            double origBgL = ColorHsl.FromHex(list[0].Colors.Background).L;
            double invBgL = ColorHsl.FromHex(list[1].Colors.Background).L;
            double invTextL = ColorHsl.FromHex(list[1].Colors.Text).L;
            Assert.True(invBgL > origBgL);
            Assert.True(invTextL < invBgL);
        }

        [Fact]
        public void Inverted_Text_Meets_Contrast()
        {
            var inv = PaletteGenerator.Generate(DarkSample())[1];
            Assert.True(ColorHsl.ContrastRatio(inv.Colors.Text, inv.Colors.Background) >= 4.5);
        }

        [Fact]
        public void Inverted_Main_Meets_Contrast_And_Keeps_Hue()
        {
            var inv = PaletteGenerator.Generate(DarkSample())[1];
            Assert.True(ColorHsl.ContrastRatio(inv.Colors.Main, inv.Colors.Background) >= 4.5);
            double origH = ColorHsl.FromHex("#5DBEE0").H;
            double invH = ColorHsl.FromHex(inv.Colors.Main).H;
            Assert.True(System.Math.Abs(origH - invH) < 2.0);
        }
    }
}
