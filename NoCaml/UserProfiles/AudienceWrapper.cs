using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    public class AudienceWrapper
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Operator { get; set; }
        public List<Rule> Rules { get; set; }
        
    }
}
