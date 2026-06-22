using System.IO;
using Xunit;

namespace TeampptAddin.Tests
{
    public class AdminCredentialsTest
    {
        [Fact]
        public void Load_Reads_Service_And_Gemini_Keys()
        {
            var tmp = Path.GetTempFileName();
            File.WriteAllText(tmp, @"{""supabaseServiceKey"":""svc123"",""geminiKey"":""AIzaXYZ""}");
            try
            {
                var c = AdminCredentials.Load(tmp);
                Assert.Equal("svc123", c.SupabaseServiceKey);
                Assert.Equal("AIzaXYZ", c.GeminiKey);
            }
            finally { File.Delete(tmp); }
        }
    }
}
