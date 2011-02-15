using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Reflection;

namespace NoCaml
{
    public static class AssemblyManager
    {
        public static string GetCallerFileVersion()
        {
            var a = Assembly.GetCallingAssembly();
            return FileVersionInfo.GetVersionInfo(a.Location).FileVersion;            
        }

        public static IEnumerable<LoadedAssembly> GetLoadedAssemblies(string prefix)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var n = a.GetName();
                if (n.Name == "Microsoft.SharePoint"
                    || n.Name == "NoCaml"
                    || n.Name.StartsWith(prefix))
                {
                    var la = new LoadedAssembly()
                    {
                        AssemblyName = n.Name,
                        AssemblyVersion = n.Version,
                        AssemblyFileVersion = FileVersionInfo.GetVersionInfo(a.Location).FileVersion
                    };
                    yield return la;
                }
            }
        }

    }
}
