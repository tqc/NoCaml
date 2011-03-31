using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ProfilePropertyStorageAttribute : Attribute
    {

        public string PropertyName { get; set; }

        public ProfilePropertyStorageAttribute(string propertyName)
        {
            PropertyName = propertyName;
        }


    }
}
