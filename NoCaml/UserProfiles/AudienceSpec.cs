using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    public class AudienceSpec
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Operator { get; set; }
        public List<Rule> Rules { get; set; }

        public List<string> PreviousNames { get; set; }

        public bool IsObsolete { get; set; }
        public bool ShouldDelete { get; set; }
    }
}
