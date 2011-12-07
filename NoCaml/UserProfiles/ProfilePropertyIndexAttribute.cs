using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    /// <summary>
    /// Indicate that a property exists in the search index and may be loadable from search results
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ProfilePropertyIndexAttribute : Attribute
    {

        public string ManagedPropertyName { get; set; }

        public ProfilePropertyIndexAttribute(string propertyName)
        {
            ManagedPropertyName = propertyName;
        }


    }
}
