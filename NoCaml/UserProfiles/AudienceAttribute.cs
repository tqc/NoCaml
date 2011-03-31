using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class AudienceAttribute : Attribute
    {
        public string AudienceName { get; set; }
        public AudienceRuleType Type { get; set; }
        public string Filter { get; set; }
        public string Description { get; set; }
        public string MultiRuleOperator { get; set; }
        public bool UpdateRules { get; set; }

        public AudienceAttribute(string audienceName)
        {
            AudienceName = audienceName;
            UpdateRules = true;
            MultiRuleOperator = "AND";
            Type=AudienceRuleType.Equal;
        }
        public AudienceAttribute(string audienceName, AudienceRuleType type, string filter)
            : this(audienceName)
        {
            Type = type;
            Filter = filter;
        }
    }

    public enum AudienceRuleType
    {
        Equal,
        NotEqual
    }

}
