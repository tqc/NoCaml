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
    public abstract class ProfileBase
    {

        protected ProfileBase()
        {
            ChangedProperties = new List<string>();
            SourceLog = new Dictionary<string, string>();
            HashLog = new Dictionary<string, string>();
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
        public Dictionary<string, string> SourceLog { get; set; }

        [ProfilePropertyStorage("HashLog", PropertyType = "HTML", Length = 2000)]
        public Dictionary<string, string> HashLog { get; set; }


        /// <summary>
        /// A list of properties which have been changed since the profile was loaded
        /// from the profile store
        /// </summary>
        public List<string> ChangedProperties { get; set; }


        #endregion

        protected SPSite Site { get; set; }
        protected UserProfileWrapper Profile { get; set; }
        protected UserProfileManagerWrapper UserProfileManager { get; set; }


        public static List<string> ChangedAudiences { get; private set; }

        private static void RecordChangedAudience(string audienceName)
        {
            if (ChangedAudiences == null) ChangedAudiences = new List<string>();
            if (!ChangedAudiences.Contains(audienceName)) ChangedAudiences.Add(audienceName);
        }

        public static void CompileChangedAudiences(SPSite site)
        {
            if (ChangedAudiences == null) return;

            var am = new AudienceManagerWrapper(site);
            foreach (var an in ChangedAudiences) am.CompileAudience(an, false);
            ChangedAudiences = null;
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

        private static Dictionary<string, Func<UserProfileValueCollectionWrapper, object>> LoadFunctions { get; set; }
        private static Dictionary<string, Func<object, object>> SaveFunctions { get; set; }


        protected abstract void RegisterCustomLoadSaveFunctions();

        protected void RegisterCustomPropertyLoader<TProfile, T>(
            Expression<Func<TProfile, T>> propfunc,
            Func<UserProfileValueCollectionWrapper, T> loadfunc,
            Func<T, object> savefunc
            )
        {
            // get property name from expression
            var n = propfunc.Body as MemberExpression;
            if (propfunc.Body.NodeType == ExpressionType.Convert) n = ((UnaryExpression)propfunc.Body).Operand as MemberExpression;
            var pi = n.Member as PropertyInfo;


            LoadFunctions[pi.Name] = upvc => loadfunc(upvc);
            SaveFunctions[pi.Name] = o => savefunc((T)o);


        }

        private void RegisterLoadSaveFunctions()
        {
            if (LoadFunctions != null && SaveFunctions != null) { return; }

            lock (syncRoot)
            {
                if (LoadFunctions != null && SaveFunctions != null) { return; }
                LoadFunctions = new Dictionary<string, Func<UserProfileValueCollectionWrapper, object>>();
                SaveFunctions = new Dictionary<string, Func<object, object>>();

                RegisterCustomPropertyLoader<ProfileBase, Dictionary<string, string>>(p => p.SourceLog,
                        ppvc =>
                        {
                            var sl = (string)ppvc.Value;
                            var dsl = new Dictionary<string, string>();
                            if (!string.IsNullOrEmpty(sl))
                            {
                                var ll = sl.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                                foreach (var l in ll)
                                {
                                    if (!l.Contains(":")) continue;
                                    if (l.Contains("<")) continue;
                                    var cl = l.Split(':');
                                    var pn = cl[0].Trim();
                                    var cs = cl[1].Trim();
                                    dsl.Add(pn, cs);
                                }

                            }
                            return dsl;
                        },
                        v => string.Join(
                            "\n",
                            v.Select(kv => kv.Key + ":" + kv.Value).ToArray()
                            ));



                RegisterCustomPropertyLoader<ProfileBase, Dictionary<string, string>>(p => p.HashLog,
            ppvc =>
            {
                var sl = (string)ppvc.Value;
                var dsl = new Dictionary<string, string>();
                if (!string.IsNullOrEmpty(sl))
                {
                    var ll = sl.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var l in ll)
                    {
                        if (!l.Contains(":")) continue;
                        if (l.Contains("<")) continue;
                        var cl = l.Split(':');
                        var pn = cl[0].Trim();
                        var cs = cl[1].Trim();
                        dsl.Add(pn, cs);
                    }

                }
                return dsl;
            },
            v => string.Join(
                "\n",
                v.Select(kv => kv.Key + ":" + kv.Value).ToArray()
                ));

                RegisterCustomLoadSaveFunctions();

                RegisterAllLoadFunctions();

            }


        }


        private static List<PropertyAction> AllLoadFunctions;

        private class PropertyAction
        {
            public PropertyInfo PropertyInfo;
            public string DataPropertyName;
            public Action<UserProfileWrapper, ProfileBase> LoadAction;
            public Action<ProfileBase> SaveAction;
            public List<AudienceAttribute> AffectedAudiences;


            public PropertyAction(PropertyInfo pi, ProfilePropertyStorageAttribute psa)
            {
                PropertyInfo = pi;
                DataPropertyName = psa.PropertyName;

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
                    else
                    {
                        LoadAction = ((source, dest) => PropertyInfo.SetValue(dest, source[DataPropertyName].Value, null));
                    }

                    // property needs saving - check for a custom save function
                    if (SaveFunctions.ContainsKey(PropertyInfo.Name))
                    {
                        SaveAction = (dest) => dest.SetIfChanged(PropertyInfo, DataPropertyName, AffectedAudiences, SaveFunctions[PropertyInfo.Name]);
                    }
                    else
                    {
                        SaveAction = (dest) => dest.SetIfChanged(PropertyInfo, DataPropertyName, AffectedAudiences, o => o);
                    }

                    AffectedAudiences = PropertyInfo.GetCustomAttributes(typeof(AudienceAttribute), false)
               .Cast<AudienceAttribute>().ToList();
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
                // only need to load properties that are stored
                if (psa == null) continue;
                AllLoadFunctions.Add(new PropertyAction(p, psa));
            }
        }

        protected ProfileBase(SPSite site, UserProfileWrapper profile)
            : this()
        {
            Site = site;
            Profile = profile;
            UserProfileManager = profile.ProfileManager;
            ContextProfileValid = true;

            EnsurePropertiesExist();

            RegisterLoadSaveFunctions();

            LanID = (string)profile["AccountName"].Value;

            // load properties that have a known source
            foreach (var a in AllLoadFunctions)
            {
                a.LoadAction(profile, this);
            }


        }


        internal string GetCurrentSource(string p)
        {
            if (SourceLog.ContainsKey(p)) return SourceLog[p];
            else return null;
        }

        public void SetUpdated(string p, string source)
        {
            SourceLog[p] = source;
            ChangedProperties.Add(p);
            ChangedProperties.Add("SourceLog");
            Debug.WriteLine(string.Format("{0}: Updated {1} from {2}", this.LanID, p, source));
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

        internal bool ImportedPropertyChanged(string p, string newvalue)
        {

            var newhash = GetHash(newvalue);

            if (!HashLog.ContainsKey(p) || HashLog[p] != newhash)
            {
                HashLog[p] = newhash;
                ChangedProperties.Add("HashLog");
                return true;
            }
            else
            {
                return false;
            }





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
