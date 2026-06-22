using System;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace TeampptAddin
{
    public class AdminCredentials
    {
        public string SupabaseServiceKey { get; set; }
        public string GeminiKey { get; set; }

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TeampptAddin", "admin.json");

        public static AdminCredentials Load(string path)
        {
            var o = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
            return new AdminCredentials
            {
                SupabaseServiceKey = o["supabaseServiceKey"]?.ToString(),
                GeminiKey = o["geminiKey"]?.ToString()
            };
        }
    }
}
