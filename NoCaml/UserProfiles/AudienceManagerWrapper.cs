using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using Microsoft.Office.Server;
using System.Reflection;
using System.Collections;
using System.Diagnostics;

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


        public void EnsureAudiencesExist<T>(bool allowAudienceDeletion) where T : ProfileBase
        {
            EnsureAudiencesExist(typeof(T), allowAudienceDeletion);
        }

        public void EnsureAudiencesExist<T>(bool allowAudienceDeletion, Dictionary<string, string> additionalAudiences) where T : ProfileBase
        {
            EnsureAudiencesExist(typeof(T), allowAudienceDeletion, additionalAudiences);
        }

        public void EnsureAudiencesExist(Type profileType, bool allowAudienceDeletion)
        {
            EnsureAudiencesExist(profileType, allowAudienceDeletion, new Dictionary<string, string>());
        }

        public static List<AudienceSpec> ParseAudienceSpecs(Dictionary<string, string> stringspecs)
        {
            var result = new List<AudienceSpec>();

            if (stringspecs.Count > 0)
            {
                var scanner = new TinyPG.Scanner();
                var parser = new TinyPG.Parser(scanner);

                // override the definitions from the code
                foreach (var kv in stringspecs)
                {
                    var name = kv.Key;
                    var ruleExpression = kv.Value;
                    if (string.IsNullOrEmpty(ruleExpression)) continue;


                    var parseTree = parser.Parse(ruleExpression);
                    if (parseTree.Errors.Count > 0)
                    {
                        foreach (var err in parseTree.Errors)
                        {
                            // probably should have some real logging here
                            Debug.WriteLine(err.Message);
                        }
                        continue;
                    }
                    else
                    {
                        var a = new AudienceSpec()
                        {
                            Name = name,
                            Description = name,
                            Operator = "AND",
                            Rules = new List<Rule>(),
                            ShouldDelete = false,
                            IsObsolete = false,
                            PreviousNames = new List<string>()
                        };

                        UpdateAudienceSpecFromParseTree(parseTree, a);

                        result.Add(a);
                    }

                }
            }

            return result;
        }

        public void EnsureAudiencesExist(Type profileType, bool allowAudienceDeletion, Dictionary<string, string> additionalAudiences)
        {
            EnsureAudiencesExist(profileType, allowAudienceDeletion, ParseAudienceSpecs(additionalAudiences));
        }

        /// <summary>
        /// Create audiences specified as attributes
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public void EnsureAudiencesExist(Type profileType, bool allowAudienceDeletion, List<AudienceSpec> additionalAudiences)
        {
            var audiences = new Dictionary<string, AudienceSpec>();

            foreach (var pi in profileType.GetProperties())
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
                        audiences[aa.AudienceName] = new AudienceSpec()
                        {
                            Name = aa.AudienceName,
                            Description = aa.Description,
                            Operator = aa.MultiRuleOperator,
                            Rules = aa.UpdateRules ? new List<Rule>() : null,
                            ShouldDelete = false,
                            IsObsolete = false,
                            PreviousNames = new List<string>()
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

            foreach (var a in additionalAudiences)
            {
                // remove audience specs using the previous names so we can override audiences 
                //defined with attributes
                foreach (var pn in a.PreviousNames)
                {
                    if (audiences.ContainsKey(pn)) audiences.Remove(pn);
                }

                audiences[a.Name] = a;
            }


            EnsureAudiencesExist(audiences.Values, true, allowAudienceDeletion);


        }

        private static void UpdateAudienceSpecFromParseTree(TinyPG.ParseNode n, AudienceSpec audienceSpec)
        {

            var nodeType = n.Text.Contains(" ") ? n.Text.Substring(0, n.Text.IndexOf(" ")) : n.Text;
            var value = n.Text.Contains("'") ? n.Text.Substring(n.Text.IndexOf("'") + 1, n.Text.LastIndexOf("'") - n.Text.IndexOf("'") - 1) : "";
            if (nodeType == "SimpleRule")
            {
                var name = n.Nodes[0].Text.Substring(n.Nodes[0].Text.IndexOf("'") + 1, n.Nodes[0].Text.LastIndexOf("'") - n.Nodes[0].Text.IndexOf("'") - 1);
                var op = n.Nodes[1].Nodes[0].Text.Substring(0, n.Nodes[1].Nodes[0].Text.IndexOf(" "));
                var val = n.Nodes[2].Text.Substring(n.Nodes[2].Text.IndexOf("'") + 2, n.Nodes[2].Text.LastIndexOf("'") - n.Nodes[2].Text.IndexOf("'") - 3);
                op = op == "EQ" ? "="
                    : op == "NEQ" ? "<>"
                    : op == "Contains" ? "Contains"
                    : "=";
                Debug.WriteLine(name + ", " + op + ", " + val);
                audienceSpec.Rules.Add(new Rule(name, op, val));
            }
            else if (nodeType == "BROPEN")
            {
                Debug.WriteLine("(");
                audienceSpec.Rules.Add(new Rule(null, "(", null));
            }
            else if (nodeType == "BRCLOSE")
            {
                Debug.WriteLine(")");
                audienceSpec.Rules.Add(new Rule(null, ")", null));
            }
            else if (nodeType == "AND")
            {
                Debug.WriteLine("AND");
                audienceSpec.Rules.Add(new Rule(null, "AND", null));
            }
            else if (nodeType == "OR")
            {
                Debug.WriteLine("OR");
                audienceSpec.Rules.Add(new Rule(null, "OR", null));
            }
            else if (nodeType == "DeleteStatement")
            {
                Debug.WriteLine("DELETE");
                audienceSpec.ShouldDelete = true;
            }
            else if (nodeType == "ObsoleteStatement")
            {
                Debug.WriteLine("OBSOLETE");
                audienceSpec.IsObsolete = true;
            }
            else if (nodeType == "RenameStatement")
            {
                var oldName = n.Nodes[1].Text.Substring(n.Nodes[1].Text.IndexOf("'") + 2, n.Nodes[1].Text.LastIndexOf("'") - n.Nodes[1].Text.IndexOf("'") - 3);

                Debug.WriteLine("Renamed from "+oldName);
                audienceSpec.PreviousNames.Add(oldName);
            }
            else
            {
                foreach (var n2 in n.Nodes) { UpdateAudienceSpecFromParseTree(n2, audienceSpec); };
            }


        }

 

        public void EnsureAudiencesExist(IEnumerable<AudienceSpec> specifiedAudiences, bool updateRules, bool allowAudienceDeletion)
        {
            var adp = TAudience.GetProperty("AudienceDescription");
            var anp = TAudience.GetProperty("AudienceName");
            var aip = TAudience.GetProperty("AudienceID");
            var arp = TAudience.GetProperty("AudienceRules");
            var agop = TAudience.GetProperty("GroupOperation");

            
            // get audience basic properties
            var wrappedExistingAudiences = WrappedAudiences.ToList();

            // because the indexer is painfully slow
            var cachedExistingAudiences = Audiences.Cast<object>()
                // filter out the all site users audience which cannot be updated or removed
                .Where(o=> (Guid)aip.GetValue(o, null) != Guid.Empty)
                .ToDictionary(
                k => (string)anp.GetValue(k, null),
                v=> v
                );


            var missing = specifiedAudiences.Where(a => !cachedExistingAudiences.ContainsKey(a.Name));

            var audiencesToDelete = new List<string>();

            foreach (var audienceSpec in specifiedAudiences)
            {

                if (audienceSpec.Description == null) audienceSpec.Description = audienceSpec.Name;

                bool changed = false;
                object actualAudience = null;                
                if (cachedExistingAudiences.ContainsKey(audienceSpec.Name)) {
                    actualAudience = cachedExistingAudiences[audienceSpec.Name];
                }
                if (actualAudience == null)
                {
                    // check for version of audience with obsolete prefix
                    var prefixedName = "ZZZZ OBSOLETE " + audienceSpec.Name;
                    if (cachedExistingAudiences.ContainsKey(prefixedName))
                    {
                        actualAudience = cachedExistingAudiences[prefixedName];
                    }
                }

                if (actualAudience == null && audienceSpec.PreviousNames.Count > 0)
                {
                    // check for existing audience to be renamed
                    foreach (var previousName in audienceSpec.PreviousNames)
                    {
                        if (cachedExistingAudiences.ContainsKey(previousName))
                        {
                            actualAudience = cachedExistingAudiences[previousName];
                            break;
                        }
                    }
                }

                if (actualAudience == null && audienceSpec.IsObsolete || audienceSpec.ShouldDelete)
                {
                    // no need to create an obsolete or deleted audience
                    continue;
                }

                if (actualAudience == null)
                {
                    // new audience
                    actualAudience = Audiences.GetType().GetMethod("Create", new Type[] { typeof(string), typeof(string) }).Invoke(Audiences, new object[] { audienceSpec.Name, audienceSpec.Description });
                    changed = true;
                }

                // make sure audience is correctly named

                var existingAudienceName = // actualAudience.Name
                    (string)anp.GetValue(actualAudience, null);

                if (audienceSpec.ShouldDelete)
                {
                    audiencesToDelete.Add(existingAudienceName);
                    // no need to update an audience that is about to be deleted
                    continue;
                }


                var specifiedAudienceName =
                    audienceSpec.IsObsolete ? "ZZZZ OBSOLETE " + audienceSpec.Name : audienceSpec.Name;


                if (existingAudienceName != specifiedAudienceName)
                {
                    anp.SetValue(actualAudience, specifiedAudienceName, null);
                    changed = true;
                }
                
                // update audience operator if necessary

                //ea.GroupOperation = 2 //Microsoft.Office.Server.Audience.AudienceGroupOperation.AUDIENCE_AND_OPERATION
                var ngop = audienceSpec.Operator == "OR" ? 1 : 2;
                if ((int)agop.GetValue(actualAudience, null) != ngop)
                {
                    agop.SetValue(actualAudience, ngop, null);
                    changed = true;
                }

                // Update description if necessary

                if ((string)adp.GetValue(actualAudience, null) != audienceSpec.Description)
                {
                    adp.SetValue(actualAudience, audienceSpec.Description, null);
                    changed = true;
                }

                // Update audience rules if there are any changes

                if (updateRules && audienceSpec.Rules != null && audienceSpec.Rules.Count > 0)
                {
                    // make sure there is a rules list
                    if (arp.GetValue(actualAudience, null) == null)
                    {
                        arp.SetValue(actualAudience, new ArrayList(), null);
                    }

                    // check for rule update
                    var erl = ((ArrayList)arp.GetValue(actualAudience, null)).OfType<object>().ToList();

                    //  var ersl = erl.Select(r => r.LeftContent + r.Operator + r.RightContent);
                    //  var nrsl = na.Rules.Select(r => r.Left + r.Operator + r.Right);

                    var plc = TAudienceRuleComponent.GetProperty("LeftContent");
                    var prc = TAudienceRuleComponent.GetProperty("RightContent");
                    var pop = TAudienceRuleComponent.GetProperty("Operator");

                    // find removed rules
                    foreach (var er in erl.Where(r => (string)plc.GetValue(r, null) != null))
                    {
                        var nr = audienceSpec.Rules.Where(r => r.Left == (string)plc.GetValue(er, null) && r.Operator == (string)pop.GetValue(er, null) && r.Right == (string)prc.GetValue(er, null)).FirstOrDefault();
                        if (nr == null)
                        {
                            changed = true;
                        }
                    }


                    foreach (var nr in audienceSpec.Rules)
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
                        arp.SetValue(actualAudience, new ArrayList(), null);
                        var ar = ((ArrayList)arp.GetValue(actualAudience, null));

                        foreach (var nr in audienceSpec.Rules)
                        {
                            if (ar.Count > 0) ar.Add(ci.Invoke(new object[] { null, audienceSpec.Operator, null }));
                            ar.Add(ci.Invoke(new object[] { nr.Left, nr.Operator, nr.Right }));
                        }
                    }

                }

                

                if (changed)
                {
                    TAudience.GetMethod("Commit").Invoke(actualAudience, new object[] { });
                    
                    // This will run before the standard compilation cleans up any stalled jobs.
                    // However, changes don't happen often enough for this to matter.
                    CompileAudience(audienceSpec.Name, true);
                }

            }

            if (allowAudienceDeletion && audiencesToDelete.Count > 0)
            {
                foreach (var rn in audiencesToDelete)
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
