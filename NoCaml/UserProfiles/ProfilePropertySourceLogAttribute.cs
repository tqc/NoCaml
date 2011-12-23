using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class ProfilePropertySourceLogAttribute : Attribute
    {

        public string LogPropertyName { get; set; }

        public int HistoryLength { get; set; }
        public bool StorePastValues { get; set; }
        public bool StoreUsername { get; set; }
        public bool StoreDate { get; set; }
        public bool StoreHash { get; set; }


        public ProfilePropertySourceLogAttribute()
        {
            LogPropertyName = "SourceLog";
            HistoryLength = 0;
            StoreDate = true;
            StorePastValues = false;
            StoreUsername = true;
            StoreHash = false;
        }


    }
}
