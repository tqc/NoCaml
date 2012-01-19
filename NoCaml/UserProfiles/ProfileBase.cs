using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;

namespace NoCaml.UserProfiles
{

    /// <summary>
    /// Base class for strongly typed user profile
    /// </summary>
    public abstract class ProfileBase : IProfile
    {

        protected ProfileBase()
        {
            ChangedProperties = new List<string>();
            SourceLog = new SourceLog();
        }

        /// <summary>
        /// Login. Should be in the format "domain\user"
        /// </summary>
        public string LanID { get; set; }

        #region Properties for source/change management

        /// <summary>
        /// Records the source for each property currently set on the profile
        /// </summary>
        [ProfilePropertyStorage("SourceLog", PropertyType = "HTML", Length = 2000)]
        public SourceLog SourceLog { get; set; }


        /// <summary>
        /// A list of properties which have been changed since the profile was loaded
        /// from the profile store
        /// </summary>
        public List<string> ChangedProperties { get; set; }


        #endregion

        protected SPSite Site { get; set; }
        protected UserProfileWrapper Profile { get; set; }
        protected UserProfileManagerWrapper UserProfileManager { get; set; }


        /// <summary>
        /// A thread safe stack that holds at most one instance of each audience name
        /// Returns the most recently added audience first so that a backlog of queued audiences does 
        /// not affect realtime functionality.
        /// </summary>
        public class AudienceCompilationQueue
        {
            private List<string> data = new List<string>();
            private object syncRoot = new object();

            public AudienceCompilationQueue()
            {

            }

            public string Pop()
            {
                if (data.Count == 0) return null;
                lock (syncRoot)
                {
                    if (data.Count == 0) return null;
                    var result = data[data.Count - 1];
                    data.RemoveAt(data.Count - 1);
                    return result;
                }
            }

            public void Push(string val)
            {
                lock (syncRoot)
                {
                    if (data.Contains(val))
                    {
                        data.Remove(val);
                    }
                    data.Add(val);
                }
            }

            public int Count { get { return data.Count; } }

        }


        public static AudienceCompilationQueue ChangedAudiences = new AudienceCompilationQueue();


        public static void RecordChangedAudience(string audienceName)
        {
            ChangedAudiences.Push(audienceName);
        }

        private static object compilationSyncRoot = new object();
        private static bool audienceCompilationInProgress = false;
        /// <summary>
        /// Compile audiences flagged as changed. 
        /// </summary>
        /// <param name="site"></param>
        /// <param name="max"></param>
        public static void CompileChangedAudiences(SPSite site, int max)
        {
            if (audienceCompilationInProgress) return;
            lock (compilationSyncRoot)
            {
                if (audienceCompilationInProgress) return;
                audienceCompilationInProgress = true;
                AudienceManagerWrapper am = null;
                for (int i = 0; i < max; i++)
                {
                    try
                    {
                        var an = ChangedAudiences.Pop();
                        if (an == null) break;
                        if (am == null) am = new AudienceManagerWrapper(site);
                        var compiled = am.CompileAudience(an, false);

                        if (!compiled)
                        {
                            // something went wrong with the compilation.
                            // most likely another job running
                            if (max > 100)
                            {
                                // compiling many audiences indicates a timer job. 
                                // stop the previous compilation and try again.
                                am.StopAudienceCompilation();
                                compiled = am.CompileAudience(an, false);
                                // if stopping the job didn't help, move on. The audience
                                // will be updated by the oob daily compile.

                            }
                            else
                            {
                                // this is the realtime update. add the audience back in the queue 
                                // and leave compilation to run after the error is resolved.
                                ChangedAudiences.Push(an);
                                break;

                            }


                        }

                    }
                    catch (Exception ex)
                    {

                        // no logging available here, but errors in individual audiences are probably logged elsewhere.
                        // ignore the error and move on to the next audience
                    }
                }
                audienceCompilationInProgress = false;
            }
        }
        public List<AudienceWrapper> GetAudiences()
        {
            var am = new AudienceManagerWrapper(this.Site);
            var ids = am.GetUserAudienceIDs(LanID, false, Site.RootWeb);
            var result = new List<AudienceWrapper>();

            foreach (Guid g in ids)
            {
                try
                {
                    var a = am.GetAudience(g);
                    result.Add(a);
                }
                catch
                {
                    // ignore any invalid ids returned
                }
            }

            return result;

        }

        public List<string> GetAudienceNames()
        {
            var am = new AudienceManagerWrapper(this.Site);
            var ids = am.GetUserAudienceNames(LanID, Site.RootWeb);
            return ids;
        }


        // TODO: this needs to be set to false if the elevated context will be disposed before the record is saved.
        public bool ContextProfileValid { get; set; }

        /// <summary>
        /// true if the object is fully loaded from a profile. 
        /// false if it is partially loaded from search results
        /// </summary>
        public bool FullyLoaded { get; set; }
        protected List<string> LoadedProperties { get; set; }

        public abstract void EnsureCustomPropertiesExist();

        private static object syncRoot = new object();
        public static bool propertiesEnsured = false;

        public void EnsurePropertiesExist()
        {
            if (propertiesEnsured) return;

            lock (syncRoot)
            {
                if (propertiesEnsured) return;

                if (SPContext.Current != null)
                {
                    SPContext.Current.Web.AllowUnsafeUpdates = true;
                }
                //UserProfileManager.EnsurePropertyExists("SourceLog", "SourceLog", "HTML", 2000, true, false);
                //UserProfileManager.EnsurePropertyExists("HashLog", "HashLog", "HTML", 2000, true, false);

                // find all properties with ProfilePropertyStorage attributes

                var pl = this.GetType().GetProperties();
                foreach (var p in pl)
                {
                    var psa = (ProfilePropertyStorageAttribute)p.GetCustomAttributes(typeof(ProfilePropertyStorageAttribute), true).FirstOrDefault();
                    if (psa != null)
                    {
                        UserProfileManager.EnsurePropertyExists(psa.PropertyName, psa.PropertyName, psa.PropertyType, psa.Length, psa.Searchable, psa.Multiple);
                    }
                }

                EnsureCustomPropertiesExist();

                propertiesEnsured = true;
            }
        }

        protected static Dictionary<string, Func<UserProfileValueCollectionWrapper, object>> LoadFunctions { get; set; }
        protected static Dictionary<string, Func<string, object>> PartialLoadFunctions { get; set; }
        protected static Dictionary<string, Func<object, object>> SaveFunctions { get; set; }


        protected abstract void RegisterCustomLoadSaveFunctions();

        protected void RegisterCustomPropertyLoader<TProfile, T>(
            Expression<Func<TProfile, T>> propfunc,
            Func<UserProfileValueCollectionWrapper, T> loadfunc,
            Func<string, T> partialLoadFunc,
            Func<T, object> savefunc
            )
        {
            // get property name from expression
            var n = propfunc.Body as MemberExpression;
            if (propfunc.Body.NodeType == ExpressionType.Convert) n = ((UnaryExpression)propfunc.Body).Operand as MemberExpression;
            var pi = n.Member as PropertyInfo;

            RegisterCustomPropertyLoader<TProfile, T>(pi.Name, loadfunc, partialLoadFunc, savefunc);


        }

        protected void RegisterCustomPropertyLoader<TProfile, T>(
        string propname,
        Func<UserProfileValueCollectionWrapper, T> loadfunc,
        Func<string, T> partialLoadFunc,
        Func<T, object> savefunc
        )
        {
             if (loadfunc != null)
            {
                LoadFunctions[propname] = upvc => loadfunc(upvc);
            }
            if (partialLoadFunc != null)
            {
                PartialLoadFunctions[propname] = s => partialLoadFunc(s);
            }
            if (savefunc != null)
            {
                SaveFunctions[propname] = o => savefunc((T)o);
            }

        }

        private static bool LoadSaveFunctionsRegistered = false;

        private static Type ProfileType { get; set; }

        private void RegisterLoadSaveFunctions()
        {
            if (ProfileType != null && ProfileType.AssemblyQualifiedName != this.GetType().AssemblyQualifiedName)
            {
                // handle regeneration of profile classes
                LoadSaveFunctionsRegistered = false;
            }

            if (LoadSaveFunctionsRegistered) { return; }

            lock (syncRoot)
            {
                if (LoadSaveFunctionsRegistered) { return; }
                LoadFunctions = new Dictionary<string, Func<UserProfileValueCollectionWrapper, object>>();
                PartialLoadFunctions = new Dictionary<string, Func<string, object>>();
                SaveFunctions = new Dictionary<string, Func<object, object>>();

                



            //    RegisterCustomPropertyLoader<ProfileBase, Dictionary<string, string>>(p => p.HashLog,
            //ppvc =>
            //{
            //    var sl = (string)ppvc.Value;
            //    var dsl = new Dictionary<string, string>();
            //    if (!string.IsNullOrEmpty(sl))
            //    {
            //        var ll = sl.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            //        foreach (var l in ll)
            //        {
            //            if (!l.Contains(":")) continue;
            //            if (l.Contains("<")) continue;
            //            var cl = l.Split(':');
            //            var pn = cl[0].Trim();
            //            var cs = cl[1].Trim();
            //            dsl.Add(pn, cs);
            //        }

            //    }
            //    return dsl;
            //},
            //null,
            //v => string.Join(
            //    "\n",
            //    v.Select(kv => kv.Key + ":" + kv.Value).ToArray()
            //    ));

                RegisterCustomLoadSaveFunctions();

                RegisterAllLoadFunctions();

                LoadSaveFunctionsRegistered = true;
            }


        }


        private static List<PropertyAction> AllLoadFunctions;

        private class PropertyAction
        {
            public PropertyInfo PropertyInfo;
            /// <summary>
            /// Property name as stored in the user profile
            /// </summary>
            public string DataPropertyName;
            
/// <summary>
/// Managed property name in the search index
/// </summary>
            public string SearchPropertyName { get; set; }
            public Action<UserProfileWrapper, ProfileBase> LoadAction;
            public Action<string, ProfileBase> PartialLoadAction;
            public Action<ProfileBase> SaveAction;
            public List<AudienceAttribute> AffectedAudiences;
            public List<ProfilePropertySourceAttribute> ValidSources;
            public ProfilePropertySourceLogAttribute LogSettings;

           

            public PropertyAction(PropertyInfo pi, ProfilePropertyStorageAttribute psa,ProfilePropertyIndexAttribute pia)
            {
                PropertyInfo = pi;
                DataPropertyName = psa.PropertyName;
                SearchPropertyName = pia == null ? null : pia.ManagedPropertyName;
                // property needs loading - check for a custom load function
                if (LoadFunctions.ContainsKey(PropertyInfo.Name))
                {
                    LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, LoadFunctions[PropertyInfo.Name](source[DataPropertyName]), null));
                }
                else
                    if (PropertyInfo.PropertyType == typeof(double))
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, Convert.ToDouble(source[DataPropertyName].Value), null));
                    }
                    else if (PropertyInfo.PropertyType == typeof(decimal))
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, Convert.ToDecimal(source[DataPropertyName].Value), null));
                    }
                    else if (PropertyInfo.PropertyType == typeof(int))
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, Convert.ToInt32(source[DataPropertyName].Value), null));
                    }
                    else if (PropertyInfo.PropertyType == typeof(bool))
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, Convert.ToBoolean(source[DataPropertyName].Value), null));
                    }
                    else if (PropertyInfo.PropertyType == typeof(DateTime?))
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, source[DataPropertyName].Count == 0 ? (DateTime?)null : Convert.ToDateTime(source[DataPropertyName].Value), null));
                    }
                    else if (PropertyInfo.PropertyType == typeof(DateTime))
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, Convert.ToDateTime(source[DataPropertyName].Value), null));
                    }
                    else if (PropertyInfo.PropertyType == typeof(SourceLog))
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, NoCaml.UserProfiles.SourceLog.Load(source[DataPropertyName]), null));
                    }
                    else
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, source[DataPropertyName].Value, null));
                    }

                if (pia != null)
                {
                    if (PartialLoadFunctions.ContainsKey(PropertyInfo.Name))
                    {
                        PartialLoadAction = ((source, dest) => PropertyInfo.SetValue(dest, PartialLoadFunctions[PropertyInfo.Name](source), null));
                    }
                    else
                    {
                        PartialLoadAction = ((source, dest) => PropertyInfo.SetValue(dest, source, null));
                    }
                }
                else
                {
                    PartialLoadAction = null;
                }

                // property needs saving - check for a custom save function
                if (SaveFunctions.ContainsKey(PropertyInfo.Name))
                {
                    SaveAction = (dest) => dest.SetIfChanged(PropertyInfo, DataPropertyName, AffectedAudiences, SaveFunctions[PropertyInfo.Name]);
                }
                else if (PropertyInfo.PropertyType == typeof(SourceLog))
                {
                    SaveAction = (dest) => dest.SetIfChanged(PropertyInfo, DataPropertyName, AffectedAudiences, o=>((SourceLog)o).Serialize());                
                }
                else
                {
                    SaveAction = (dest) => dest.SetIfChanged(PropertyInfo, DataPropertyName, AffectedAudiences, o => o);
                }

                AffectedAudiences = PropertyInfo.GetCustomAttributes(typeof(AudienceAttribute), false)
           .Cast<AudienceAttribute>().ToList();

                ValidSources = PropertyInfo.GetCustomAttributes(false)
                    .Where(a => a is ProfilePropertySourceAttribute)
                    .Cast<ProfilePropertySourceAttribute>().ToList();

                LogSettings = PropertyInfo.GetCustomAttributes(false)
                    .Where(a => a is ProfilePropertySourceLogAttribute)
                    .OfType<ProfilePropertySourceLogAttribute>().FirstOrDefault();
                if (LogSettings == null) LogSettings = new ProfilePropertySourceLogAttribute();

            }

        }

        private void RegisterAllLoadFunctions()
        {
            AllLoadFunctions = new List<PropertyAction>();

            var pl = this.GetType().GetProperties();
            foreach (var p in pl)
            {

                // no need to find a load function for a read only property
                if (!p.CanWrite) continue;
                var psa = (ProfilePropertyStorageAttribute)p.GetCustomAttributes(typeof(ProfilePropertyStorageAttribute), true).FirstOrDefault();
                var pia = (ProfilePropertyIndexAttribute)p.GetCustomAttributes(typeof(ProfilePropertyIndexAttribute), true).FirstOrDefault();
                // only need to load properties that are stored
                if (psa == null) continue;
                AllLoadFunctions.Add(new PropertyAction(p, psa, pia));
            }
        }

        protected ProfileBase(SPSite site, Dictionary<string, string> searchResult)
            : this()
        {

            Site = site;
            Profile = null;
            UserProfileManager = null;
            ContextProfileValid = false;
            FullyLoaded = false;
            LoadedProperties = new List<string>();
            RegisterLoadSaveFunctions();

            LanID = searchResult["AccountName"];

            foreach (var a in AllLoadFunctions)
            {
                if (!string.IsNullOrEmpty(a.SearchPropertyName) 
                    && searchResult.ContainsKey(a.SearchPropertyName)
                    && a.PartialLoadAction != null
                    ) 
                {
                    LoadedProperties.Add(a.PropertyInfo.Name);
                    a.PartialLoadAction(searchResult[a.SearchPropertyName], this);                
                }
            }
        }

        /// <summary>
        /// Load unindexed properties 
        /// </summary>
        protected void LoadFullProfile()
        {
            UserProfileManager = new UserProfileManagerWrapper(Site);
            Profile = UserProfileManager.GetUserProfile(LanID);
            
         
            // load properties that have a known source
            foreach (var a in AllLoadFunctions)
            {
                a.LoadAction(Profile, this);
            }

            FullyLoaded = true;
            ContextProfileValid = true;

        }

        protected ProfileBase(SPSite site, UserProfileWrapper profile)
            : this()
        {
            Site = site;
            Profile = profile;
            UserProfileManager = profile.ProfileManager;
            ContextProfileValid = true;
            FullyLoaded = true;
            EnsurePropertiesExist();

            RegisterLoadSaveFunctions();

            LanID = (string)profile["AccountName"].Value;

            // load properties that have a known source
            foreach (var a in AllLoadFunctions)
            {
                a.LoadAction(profile, this);
            }


        }


        public string GetCurrentSource(string p)
        {
            if (SourceLog.ContainsKey(p)) return SourceLog[p].Source;
            else return null;
        }

        public bool HistoryRequired(string source)
        {
            // may add somthing to the attributes so that only user/admin changes get logged
            return true;
        }

        public bool IsUpdatedSince(string propname, DateTime since)
        {
            var sle = GetSourceLog(propname);
            if (sle == null) return false;
            return sle.Updated >= since;
        }


        public SourceLogEntry GetSourceLog(string propname)
        {
            var prop = AllLoadFunctions.Where(pa => pa.PropertyInfo.Name == propname).FirstOrDefault();
            if (prop == null)
            {
                // no load/save/log settings
                return null;
            }
            SourceLogEntry e =null;

            var customSourceLog = (SourceLog)this.GetType().GetProperty(prop.LogSettings.LogPropertyName).GetValue(this, null);
            if (customSourceLog.ContainsKey(propname))
            {
                e = customSourceLog[propname];
            }
            else if (SourceLog.ContainsKey(propname))
            {
                e = SourceLog[propname];
            }

            return e;
        }



        public void SetUpdated(string propname, string source, object oldValue)
        {
            var prop = AllLoadFunctions.Where(pa => pa.PropertyInfo.Name == propname).FirstOrDefault();
            if (prop == null)
            {
                // no load/save/log settings
                return;
            }

            SourceLogEntry e;

            var customSourceLog = (SourceLog)this.GetType().GetProperty(prop.LogSettings.LogPropertyName).GetValue(this, null);
            if (customSourceLog.ContainsKey(propname))
            {
                e = customSourceLog[propname];
            }
            else if (SourceLog.ContainsKey(propname))
            {
                e = SourceLog[propname];
                customSourceLog[propname] = e;
                SourceLog.Remove(propname);
                ChangedProperties.Add("SourceLog");
            }
            else
            {
                e = new SourceLogEntry()
                {
                    Field = propname,
                    SourceLogHistory = new List<SourceLogEntry.SourceLogHistoryEntry>()
                };
                customSourceLog[propname] = e;
            }

            // move oldvalue and the current source details if any to the history

            e.SourceLogHistory.Add(new SourceLogEntry.SourceLogHistoryEntry()
            {
                Source = e.Source,
                Updated = prop.LogSettings.StoreDate? e.Updated : DateTime.MinValue,
                User = prop.LogSettings.StoreUsername ? e.User : null,
                Value = prop.LogSettings.StorePastValues ? GetLogValue(oldValue) : null,
                Hash = prop.LogSettings.StoreHash ? GetLogHash(oldValue) : null,
            });


            // remove any excess history records
            while (e.SourceLogHistory.Count > prop.LogSettings.HistoryLength)
            {
                e.SourceLogHistory.RemoveAt(0);
            }

            // update log with details of change

            e.Source = source;
            e.Updated = DateTime.Now;
            e.User = SPContext.Current != null && SPContext.Current.Web != null && SPContext.Current.Web.CurrentUser != null
            ? SPContext.Current.Web.CurrentUser.LoginName : "";


            ChangedProperties.Add(propname);
            ChangedProperties.Add(prop.LogSettings.LogPropertyName);
            Debug.WriteLine(string.Format("{0}: Updated {1} from {2}", this.LanID, propname, source));
        }

        private string GetLogHash(object val)
        {
            if (val == null) return null;
            return GetHash(val.ToString());
        }

        /// <summary>
        /// return a simple string version of the value used in the source log. 
        /// special characters are removed and the max length is 32 characters.
        /// This works best for numbers or short strings.
        /// </summary>
        /// <param name="val"></param>
        /// <returns></returns>
        private string GetLogValue(object val)
        {
            if (val == null) return null;
            var s = val.ToString()
                .Replace(",", "")
                .Replace("|", "")
                .Replace(";", "")
                .Replace("\r", "")
                .Replace("\n", "")
                .Replace("\t", "")
                .Replace("#", "");
            if (s.Length > 32) s = s.Substring(0, 32);

            return s;
        }

        private static SHA1Managed SHA1 = new SHA1Managed();

        private string GetHash(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;

            var data = Encoding.UTF8.GetBytes(s);
            var hashData = SHA1.ComputeHash(data);


            var hash = hashData[0].ToString("X2")
            + hashData[1].ToString("X2")
            + hashData[2].ToString("X2")
            + hashData[3].ToString("X2");


            return hash;

        }

        public bool ImportedPropertyChanged(string propname, string source, string newvalue)
        {

            var prop = AllLoadFunctions.Where(pa => pa.PropertyInfo.Name == propname).FirstOrDefault();
            if (prop == null || !prop.LogSettings.StoreHash)
            {
                // no load/save/log settings - assume changed
                return true;
            }

            
            var customSourceLog = (SourceLog)this.GetType().GetProperty(prop.LogSettings.LogPropertyName).GetValue(this, null);

            if (!customSourceLog.ContainsKey(propname)) {
                // no previous history - assume changed
                return true;
            }

            SourceLogEntry e = customSourceLog[propname];
            var oldhash = e.SourceLogHistory.Where(o => o.Source == source).Select(o=>o.Hash).FirstOrDefault();
            if (oldhash == null) return true;

            var newhash = GetHash(newvalue);
            if (newhash != oldhash) return true;

            return false;



        }
        private string LastProperty { get; set; }
        private string LastValue { get; set; }

        protected void SetIfChanged<TProfile>(Expression<Func<TProfile, object>> expr)
        {
            SetIfChanged(expr, o => o);
        }

        protected void SetIfChanged<TProfile, T>(Expression<Func<TProfile, T>> expr, Func<T, object> fval)
        {
            // get property name from expression
            var n = expr.Body as MemberExpression;
            if (expr.Body.NodeType == ExpressionType.Convert) n = ((UnaryExpression)expr.Body).Operand as MemberExpression;
            var pi = n.Member as PropertyInfo;
            SetIfChanged(pi, o => fval((T)o));
        }
        protected void SetIfChanged<TProfile, T>(PropertyInfo pi, Func<T, object> fval)
        {
            SetIfChanged(pi, o => fval((T)o));
        }

        [Obsolete]
        private void SetIfChanged(PropertyInfo pi, Func<object, object> fval)
        {
            if (!ChangedProperties.Contains(pi.Name)) return;

            // get storage property name

            var propname = pi.GetCustomAttributes(typeof(ProfilePropertyStorageAttribute), false)
    .Cast<ProfilePropertyStorageAttribute>()
    .Select(a => a.PropertyName)
    .FirstOrDefault();
            if (string.IsNullOrEmpty(propname)) return;

            var aal = pi.GetCustomAttributes(typeof(AudienceAttribute), false)
            .Cast<AudienceAttribute>().ToList();

            SetIfChanged(pi, propname, aal, fval);
        }

        private void SetIfChanged(PropertyInfo pi, string propname, List<AudienceAttribute> audienceAttributes, Func<object, object> fval)
        {
            // if not in changed properties list, do not save
            if (!ChangedProperties.Contains(pi.Name)) return;


            if (string.IsNullOrEmpty(propname)) return;

            LastProperty = propname;

            // call fval to get value in save format
            var val = fval(pi.GetValue(this, null));

            LastValue = val == null ? null : val.ToString();

            // write to profile store depending on type of value and profile property type

            var pp = Profile[propname];

            string oldval = null;
            if (pp != null) oldval = pp.Value as string;

            if (val is IEnumerable<string> && pp.Property.IsMultivalued)
            {
                pp.Clear();
                foreach (var v in (IEnumerable<string>)val) pp.Add(v);
            }
            else if (pp.Property.IsRequired && (val == null || val.ToString() == ""))
            {
                // don't try to clear required fields
            }
            else if (val is DateTime?)
            {
                DateTime? nd = (DateTime?)val;
                pp.Value = nd != null && nd.HasValue ? (object)nd.Value : null;
            }
            else
            {
                pp.Value = val;
            }

            // queue audience update if necessary
            foreach (var aa in audienceAttributes)
            {
                if (string.IsNullOrEmpty(aa.Filter)
                    || (oldval != null && oldval.Contains(aa.Filter))
                    || (pi.PropertyType == typeof(bool)) // boolean filters are always affected if the value changes
                    || (val is string && val != null && ((string)val).Contains(aa.Filter))
                    )
                {
                    RecordChangedAudience(aa.AudienceName);
                }
            }

        }

        protected abstract void SaveCustomProperties();


        /// <summary>
        /// Save to the user profile store using profile/manager from load
        /// </summary>
        public void Save()
        {
            EnsurePropertiesExist();

            // todo: allow for manual updates
            if (ChangedProperties.Count == 0) return;

            try
            {

                foreach (var a in AllLoadFunctions)
                {
                    a.SaveAction(this);
                }

                SaveCustomProperties();

                Profile.Commit();
                ChangedProperties = new List<string>();
            }
            catch (Exception ex)
            {
                LogSaveError(Site, LanID, ex.Message + " " + LastProperty + "=" + LastValue, ex.StackTrace);
                return;
            }

        }
        /// <summary>
        /// Update the profile object to save to. This includes creating a blank profile if necessary
        /// so will fail if LanID is invalid
        /// </summary>
        /// <param name="site">An elevated site</param>
        /// <param name="upm">An elevated UserProfileManager</param>
        public void UpdateContextProfile(SPSite site)
        {
            Site = site;
            UserProfileManager = new UserProfileManagerWrapper(site);
            if (UserProfileManager.UserExists(LanID))
            {
                Profile = UserProfileManager.GetUserProfile(LanID);
            }
            else
            {
                Profile = UserProfileManager.CreateUserProfile(LanID);
            }

            ContextProfileValid = true;
        }

        public abstract void LogSaveError(SPSite site, string user, string msg, string stacktrace);



        /// <summary>
        /// Return null if lanId does not exist in the profile store so that 
        /// profiles with invalid assistant/manager fields can still be loaded.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="lanId"></param>
        /// <returns></returns>
        public string ClearInvalidLanId(ProfileBase p, string lanId)
        {
            if (string.IsNullOrEmpty(lanId)) return null;
            if (!p.UserProfileManager.UserExists(lanId)) return null;
            return lanId;
        }

    }
}
