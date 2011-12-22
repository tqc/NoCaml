using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    public interface IProfile
    {
        string LanID { get; set; }

        SourceLogEntry GetSourceLog(string propname);

        bool IsUpdatedSince(string propname, DateTime since);

        void SetUpdated(string p, string source, object oldvalue);
        void Save();


        // internals
        bool ImportedPropertyChanged(string p, string source, string newvalue);
        string GetCurrentSource(string p);
        List<string> ChangedProperties { get; set; }
    }
}
