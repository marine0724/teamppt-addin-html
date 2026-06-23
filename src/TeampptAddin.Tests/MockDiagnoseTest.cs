using System.Threading.Tasks;
using Xunit;

namespace TeampptAddin.Tests
{
    public class MockDiagnoseTest
    {
        [Fact]
        public async Task Mock_Returns_Message_And_Three_Questions()
        {
            IAiService svc = new MockAiService();
            var d = await svc.DiagnoseSlideAsync("dummy.png");
            Assert.False(string.IsNullOrEmpty(d.Message));
            Assert.Equal(3, d.SuggestedQuestions.Count);
        }
    }
}
