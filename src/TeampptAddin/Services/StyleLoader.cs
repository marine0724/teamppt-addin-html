using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TeampptAddin
{
    public static class StyleLoader
    {
        public static StyleConfig Load(string assetsDir)
        {
            var jsonPath = Path.Combine(assetsDir, "styles.json");

            if (File.Exists(jsonPath))
            {
                var json = File.ReadAllText(jsonPath);
                var config = JsonConvert.DeserializeObject<StyleConfig>(json);
                if (config != null)
                    return config;
            }

            return DefaultConfig();
        }

        private static StyleConfig DefaultConfig()
        {
            return new StyleConfig
            {
                Palettes = new List<StylePalette>
                {
                    new StylePalette
                    {
                        Id = "blue-professional",
                        Name = "블루 프로페셔널",
                        Colors = new PaletteColors
                        {
                            Main = "#2563EB",
                            Sub1 = "#3B82F6",
                            Sub2 = "#93C5FD",
                            Text = "#1E293B"
                        },
                        Mood = new List<string> { "신뢰", "전문성", "기업" },
                        UseWhen = "B2B 제안서, 기업 소개, 공식 발표"
                    }
                },
                Fonts = new List<StyleFont>
                {
                    new StyleFont
                    {
                        Name = "Pretendard",
                        Mood = new List<string> { "모던", "가독성" },
                        UseWhen = "범용, 스타트업, 깔끔한 느낌"
                    }
                }
            };
        }
    }
}
