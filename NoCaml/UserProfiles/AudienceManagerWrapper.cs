using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using Microsoft.Office.Server;
using System.Reflection;
using System.Collections;

namespace NoCaml.UserProfiles
{
    public class AudienceManagerWrapper
    {
        public object AM { get; set; }
        private SPSite Site { get; set; }
        static Type TAM { get; set; }
        static Type TAudience { get; set; }
        static Type TAudienceJob { get; set; }
        static Type TAudienceRuleComponent { get; set; }
        private static MethodInfo miGetUserAudienceIDs_Bool;
        private static MethodInfo miGetUserAudienceIDs_StringBoolSPWeb;
        private static MethodInfo miGetAudience_Guid;
        private static MethodInfo miGetAudience_String;



        public AudienceManagerWrapper(SPSite site)
        {
            Site = site;

            if (TAM == null)
            {
                // get a reference to Microsoft.Office.Servers
                var a = typeof(ServerContext).Assembly;
                TAM = a.GetExportedTypes()
                    .Where(t => t.FullName == "Microsoft.Office.Server.Audience.AudienceManager")
                    .FirstOrDefault();
                // if UserProfileManager wasn't defined in Microsoft.Office.Servers, this is SP2010 and 
                // we need to load Microsoft.Office.Servers.UserProfiles
                if (TAM == null)
                {
                    a = Assembly.Load("Microsoft.Office.Server.UserProfiles, Version=14.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c");
                    TAM = a.GetExportedTypes()
                        .Where(t => t.FullName == "Microsoft.Office.Server.Audience.AudienceManager")
                        .FirstOrDefault();
                }

                TAudience = TAM.Assembly.GetType("Microsoft.Office.Server.Audience.Audience");
                TAudienceJob = TAM.Assembly.GetType("Microsoft.Office.Server.Audience.AudienceJob");
                TAudienceRuleComponent = TAM.Assembly.GetType("Microsoft.Office.Server.Audience.AudienceRuleComponent");

                miGetUserAudienceIDs_Bool = TAM.GetMethod("GetUserAudienceIDs", new Type[] { typeof(bool) });
                miGetUserAudienceIDs_StringBoolSPWeb = TAM.GetMethod("GetUserAudienceIDs", new Type[] { typeof(string), typeof(bool), typeof(SPWeb) });
                miGetAudience_String = TAM.GetMethod("GetAudience", new Type[] { typeof(string) });
                miGetAudience_Guid = TAM.GetMethod("GetAudience", new Type[] { typeof(Guid) });


            }

            AM = TAM.GetConstructor(new Type[] { typeof(ServerContext) }).Invoke(new object[] { ServerContext.GetContext(site) });

        }

        public IEnumerable Audiences
        {
            get
            {
                return (IEnumerable)TAM.GetProperty("Audiences").GetValue(AM, null);
            }
        }

        public IEnumerable<AudienceWrapper> WrappedAudiences
        {
            get
            {
                foreach (object a in Audiences)
                {
                    yield return new AudienceWrapper(a);
                }
            }
        }

        public AudienceWrapper GetAudience(string audienceName)
        {
            try
            {
                return new AudienceWrapper(miGetAudience_String.Invoke(AM, new object[] { audienceName }));
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public AudienceWrapper GetAudience(Guid audienceId)
        {
            try
            {
                return new AudienceWrapper(miGetAudience_Guid.Invoke(AM, new object[] { audienceId }));
            }
            catch (TargetInvocationException tie)
            {
                throw tie.InnerException;
            }
        }

        public List<Guid> GetUserAudienceIDs(string accountName, bool needAudienceName, SPWeb web)
        {
            var al = (ArrayList)miGetUserAudienceIDs_StringBoolSPWeb.Invoke(AM, new object[] { accountName, needAudienceName, web });
            return al.OfType<object>()
                .Select(o => (Guid)o.GetType().GetProperty("GlobalAudienceID").GetValue(o, null))
                .Where(g => g != Guid.Empty)
                .ToList();
        }

        public List<string> GetUserAudienceNames(string accountName, SPWeb web)
        {
            var al = (ArrayList)miGetUserAudienceIDs_StringBoolSPWeb.Invoke(AM, new object[] { accountName, true, web });
            return al.OfType<object>()
                .Select(o => (string)o.GetType().GetProperty("AudienceName").GetValue(o, null))
                .ToList();
        }


        public List<Guid> GetUserAudienceIDs(bool needAudienceName)
        {
            var al = (ArrayList)miGetUserAudienceIDs_Bool.Invoke(AM, new object[] { needAudienceName });
            return al.OfType<object>()
                .Select(o => (Guid)o.GetType().GetProperty("GlobalAudienceID").GetValue(o, null))
                .Where(g => g != Guid.Empty)
                .ToList();
        }




        /// <summary>
        /// Create audiences specified as attributes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void EnsureAudiencesExist<T>(bool removeUnmanagedAudiences) where T : ProfileBase
        {
            var audiences = new Dictionary<string, AudienceWrapper>();

            foreach (var pi in typeof(T).GetProperties())
            {

                var propname = pi.GetCustomAttributes(typeof(ProfilePropertyStorageAttribute), false)
               .Cast<ProfilePropertyStorageAttribute>()
               .Select(a => a.PropertyName)
               .FirstOrDefault();

                // can only create rules where property storage is set
                if (propname == null) continue;



                var aal = pi.GetCustomAttributes(typeof(AudienceAttribute), false).Cast<AudienceAttribute>();
                foreach (var aa in aal)
                {

                    if (!audiences.ContainsKey(aa.AudienceName))
                    {
                        audiences[aa.AudienceName] = new AudienceWrapper()
                        {
                            Name = aa.AudienceName,
                            Description = aa.Description,
                            Operator = aa.MultiRuleOperator,
                            Rules = aa.UpdateRules ? new List<Rule>() : null
                        };
                    }

                    if (!string.IsNullOrEmpty(aa.Filter))
                    {
                        var r = new Rule()
                        {
                            Left = propname,
                            Right = aa.Filter,
                            Operator = aa.Type == AudienceRuleType.Equal ? "=" : "<>"
                        };

                        audiences[aa.AudienceName].Rules.Add(r);
                    }

                }


            }
            EnsureAudiencesExist(audiences.Values, true, removeUnmanagedAudiences);


        }



        public void EnsureAudiencesExist(IEnumerable<AudienceWrapper> audiences, bool updateRules, bool removeUnmanagedAudiences)
        {
            var adp = TAudience.GetProperty("AudienceDescription");
            var anp = TAudience.GetProperty("AudienceName");
            var aip = TAudience.GetProperty("AudienceID");
            var arp = TAudience.GetProperty("AudienceRules");
            var agop = TAudience.GetProperty("GroupOperation");

            
            // get audience basic properties
            var wrappedAudiences = WrappedAudiences.ToList();

            // because the indexer is painfully slow
            var cachedaudiences = Audiences.Cast<object>()
                // filter out the all site users audience which cannot be updated or removed
                .Where(o=> (Guid)aip.GetValue(o, null) != Guid.Empty)
                .ToDictionary(
                k => (string)anp.GetValue(k, null),
                v=> v
                );


            var missing = audiences.Where(a => !cachedaudiences.ContainsKey(a.Name));

            var removedNames = cachedaudiences.Keys.Where(en => !audiences.Any(a => a.Name == en)).ToList();



            foreach (var na in audiences)
            {
                if (na.Description == null) na.Description = na.Name;
                object ea = null;
                if (cachedaudiences.ContainsKey(na.Name)) {
                    // ea = Audiences[na.Name];
                    //ea = Audiences.GetType().GetProperty("Item", new Type[] { typeof(string)}).GetValue(Audiences, new object[] { na.Name });
                    ea = cachedaudiences[na.Name];
                }
                bool changed = false;

                if (ea == null)
                {
                    // new audience
                    ea = Audiences.GetType().GetMethod("Create", new Type[] { typeof(string), typeof(string) }).Invoke(Audiences, new object[] { na.Name, na.Description });
                    changed = true;
                }

                //ea.GroupOperation = 2 //Microsoft.Office.Server.Audience.AudienceGroupOperation.AUDIENCE_AND_OPERATION
                var ngop = na.Operator == "OR" ? 1 : 2;
                if ((int)agop.GetValue(ea, null) != ngop)
                {
                    agop.SetValue(ea, ngop, null);
                    changed = true;
                }


                if ((string)adp.GetValue(ea, null) != na.Description)
                {
                    adp.SetValue(ea, na.Description, null);
                    changed = true;
                }
                if (updateRules && na.Rules != null && na.Rules.Count > 0)
                {
                    // make sure there is a rules list
                    if (arp.GetValue(ea, null) == null)
                    {
                        arp.SetValue(ea, new ArrayList(), null);
                    }

                    // check for rule update
                    var erl = ((ArrayList)arp.GetValue(ea, null)).OfType<object>().ToList();

                    //  var ersl = erl.Select(r => r.LeftContent + r.Operator + r.RightContent);
                    //  var nrsl = na.Rules.Select(r => r.Left + r.Operator + r.Right);

                    var plc = TAudienceRuleComponent.GetProperty("LeftContent");
                    var prc = TAudienceRuleComponent.GetProperty("RightContent");
                    var pop = TAudienceRuleComponent.GetProperty("Operator");

                    // find removed rules
                    foreach (var er in erl.Where(r => (string)plc.GetValue(r, null) != null))
                    {
                        var nr = na.Rules.Where(r => r.Left == (string)plc.GetValue(er, null) && r.Operator == (string)pop.GetValue(er, null) && r.Right == (string)prc.GetValue(er, null)).FirstOrDefault();
                        if (nr == null)
                        {
                            changed = true;
                        }
                    }


                    foreach (var nr in na.Rules)
                    {
                        var er = erl.Where(r => nr.Left == (string)plc.GetValue(r, null) && nr.Operator == (string)pop.GetValue(r, null) && nr.Right == (string)prc.GetValue(r, null)).FirstOrDefault();
                        if (er == null)
                        {
                            changed = true;
                        }

                    }

                    if (changed)
                    {
                        var ci = TAudienceRuleComponent.GetConstructor(new Type[] { typeof(string), typeof(string), typeof(string) });
                        arp.SetValue(ea, new ArrayList(), null);
                        var ar = ((ArrayList)arp.GetValue(ea, null));

                        foreach (var nr in na.Rules)
                        {
                            if (ar.Count > 0) ar.Add(ci.Invoke(new object[] { null, na.Operator, null }));
                            ar.Add(ci.Invoke(new object[] { nr.Left, nr.Operator, nr.Right }));
                        }
                    }

                }

                

                if (changed)
                {
                    TAudience.GetMethod("Commit").Invoke(ea, new object[] { });
                    
                    // This will run before the standard compilation cleans up any stalled jobs.
                    // However, changes don't happen often enough for this to matter.
                    CompileAudience(na.Name, true);
                }

            }

            if (removeUnmanagedAudiences && removedNames.Count > 0)
            {
                foreach (var rn in removedNames)
                {
                    try
                    {                        
                        //Audiences.Remove(rn)
                        Audiences.GetType().GetMethod("Remove", new Type[] { typeof(string) }).Invoke(Audiences, new object[] { rn });
                    }
                    catch (TargetInvocationException tie)
                    {
                        throw new Exception("Error removing audience "+rn +" - "+ tie.InnerException.Message, tie.InnerException);
                    }
                }

            }


        }


        private string GetAudienceJobAppId2007()
        {
            var sc = Microsoft.Office.Server.Search.Administration.SearchContext.GetContext(Site);

            return sc.Name;
        }
        private string GetAudienceJobAppId2010()
        {

            var tcx = typeof(SPContext).Assembly.GetType("Microsoft.SharePoint.SPServiceContext");
            var serviceContext = tcx.GetMethod("GetContext", new Type[] { typeof(SPSite) }).Invoke(null, new object[] { Site });

            // get the assembly which hosts the UserProfile class
            var userProfilesAssembly = Assembly.Load("Microsoft.Office.Server.UserProfiles, Version=14.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c");

            // get the type of the UserProfileApplicationProxy
            Type userProfileApplicationProxyType = userProfilesAssembly.GetType("Microsoft.Office.Server.Administration.UserProfileApplicationProxy");

            // get the proxy object
            object proxy = tcx.GetMethod("GetDefaultProxy", new Type[] { typeof(Type) }).Invoke(serviceContext, new object[] { userProfileApplicationProxyType });
            // get the UserProfileApplication property which holds the actual application
            object profile = proxy.GetType().GetProperty("UserProfileApplication", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(proxy, null);
            // get the Id of the application
            return ((Microsoft.SharePoint.Administration.SPPersistedObject)(profile)).Id.ToString();
        }

        private bool Is2010()
        {
            return typeof(SPContext).Assembly.GetName().Version.Major >= 14;
        }


        
        /// <summary>
        /// Returns true if the compile succeeded
        /// </summary>
        /// <param name="name"></param>
        /// <param name="fullcompile"></param>
        /// <returns></returns>
        public bool CompileAudience(string name, bool fullcompile)
        {
            
            var result = (int)TAudienceJob.GetMethod("RunAudienceJob").Invoke(null, new object[] { new string[] { Is2010() ? GetAudienceJobAppId2010() : GetAudienceJobAppId2007(), "1", fullcompile ? "1" : "0", name } });

            return result == 0; //AUDIENCEJOB_JOBRUN
        }

        public bool StopAudienceCompilation()
        {

            var result = (int)TAudienceJob.GetMethod("RunAudienceJob").Invoke(null, new object[] { new string[] { Is2010() ? GetAudienceJobAppId2010() : GetAudienceJobAppId2007(), "0", "0", null } });

            return result == 0; //AUDIENCEJOB_JOBRUN
        }


    }
}
