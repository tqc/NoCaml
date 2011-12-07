using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    public interface IProfile
    {
        string LanID { get; set; }

        Dictionary<string, SourceLogEntry> SourceLog { get; set; }

        Dictionary<string, string> HashLog { get; set; }


        void SetUpdated(string p, string source);
        void Save();


        // internals
        bool ImportedPropertyChanged(string p, string newvalue);
        string GetCurrentSource(string p);
        List<string> ChangedProperties { get; set; }
    }
}
