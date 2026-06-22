using System.Collections.Generic;

namespace TeampptAddin
{
    public class CatalogEntry
    {
        public string File { get; set; }
        public string Name { get; set; }
        public string Kind { get; set; }
        public string Category { get; set; }
        public List<string> Tags { get; set; }
        public string UseWhen { get; set; }
        public List<string> ContentFit { get; set; }
    }
}
