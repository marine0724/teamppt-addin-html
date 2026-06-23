using System.Collections.Generic;

namespace TeampptAddin
{
    public static class PaletteGenerator
    {
        private const double TargetContrast = 4.5;

        public static List<StylePalette> Generate(NormalizedPalette np)
        {
            var result = new List<StylePalette>();
            if (np == null) return result;

            result.Add(Original(np));
            result.Add(Inverted(np));
            return result;
        }

        private static StylePalette Original(NormalizedPalette np)
        {
            return new StylePalette
            {
                Id = "asset-original",
                Name = "원본",
                Mood = new List<string> { "에셋 원본" },
                UseWhen = "에셋의 원래 색감을 유지할 때",
                Colors = new PaletteColors
                {
                    Background = np.Background,
                    Main = np.Main,
                    Sub1 = np.Sub1,
                    Sub2 = np.Sub2,
                    Text = np.Text
                }
            };
        }

        private static StylePalette Inverted(NormalizedPalette np)
        {
            var bg = ColorHsl.FromHex(np.Background);
            var text = ColorHsl.FromHex(np.Text);

            string newBg = ColorHsl.ToHex(ColorHsl.WithLightness(bg, text.L));
            string newText = ColorHsl.ToHex(ColorHsl.WithLightness(text, bg.L));

            newText = ColorHsl.AdjustForContrast(newText, newBg, TargetContrast);

            string newMain = ColorHsl.AdjustForContrast(np.Main, newBg, TargetContrast);
            string newSub1 = ColorHsl.AdjustForContrast(np.Sub1, newBg, TargetContrast);
            string newSub2 = ColorHsl.AdjustForContrast(np.Sub2, newBg, TargetContrast);

            return new StylePalette
            {
                Id = "asset-inverted",
                Name = "반전",
                Mood = new List<string> { "반전", "대비 전환" },
                UseWhen = "밝기/배경을 뒤집어 다른 컨셉을 줄 때",
                Colors = new PaletteColors
                {
                    Background = newBg,
                    Main = newMain,
                    Sub1 = newSub1,
                    Sub2 = newSub2,
                    Text = newText
                }
            };
        }
    }
}
