using System.Collections.Generic;
using System.Linq;

namespace TeampptAddin
{
    /// <summary>회수 판단 순수함수. 추적 중 HWND 중 살아있지 않은 것을 골라낸다.</summary>
    public static class WindowSweep
    {
        public static IReadOnlyList<int> ToReclaim(IEnumerable<int> tracked, IEnumerable<int> live)
        {
            var liveSet = new HashSet<int>(live ?? Enumerable.Empty<int>());
            var seen = new HashSet<int>();
            var result = new List<int>();
            foreach (var hwnd in tracked ?? Enumerable.Empty<int>())
            {
                if (liveSet.Contains(hwnd)) continue;
                if (!seen.Add(hwnd)) continue;
                result.Add(hwnd);
            }
            return result;
        }
    }
}
