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

        public string PropertyType { get; set; }
        public int Length { get; set; }
        public bool Searchable { get; set; }
        public bool Multiple { get; set; }
        


        public ProfilePropertyStorageAttribute(string propertyName)
        {
            PropertyName = propertyName;
            PropertyType = "string";
            Length = 255;
            Searchable = true;
            Multiple = false;
        }


    }
}
