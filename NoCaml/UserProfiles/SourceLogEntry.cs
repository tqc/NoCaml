using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    public class SourceLogEntry
    {
        public string Field { get; set; }
        public string Source { get; set; }
        public DateTime Updated { get; set; }
        public string User { get; set; }
    }
}
