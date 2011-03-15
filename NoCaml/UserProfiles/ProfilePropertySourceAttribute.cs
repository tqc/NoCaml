using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class ProfilePropertySourceAttribute : Attribute
    {

        public int Order { get; set; }
        public string ProfileSource { get; set; }
        public bool UseIfEmpty { get; set; }

        public bool RaisePriorityIfChanged { get; set; }

        public ProfilePropertySourceAttribute(int order, string profileSource, bool useIfEmpty)
        {
            Order = order;
            ProfileSource = profileSource;
            UseIfEmpty = useIfEmpty;
        }


    }
}
