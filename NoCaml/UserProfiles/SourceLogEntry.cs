using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    public class SourceLogEntry
    {
        public class SourceLogHistoryEntry
        {
            public string Source { get; set; }
            public DateTime Updated { get; set; }
            public string User { get; set; }
            public string Value { get; set; }
            public string Hash { get; set; }
        }

        public string Field { get; set; }
        public string Source { get; set; }
        public DateTime Updated { get; set; }
        public string User { get; set; }
        public List<SourceLogHistoryEntry> SourceLogHistory { get; set; }
    }
}
