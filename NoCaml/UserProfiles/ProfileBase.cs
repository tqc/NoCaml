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
        [ProfilePropertyStorage("SourceLog")]
        public Dictionary<string, string> SourceLog { get; set; }

        [ProfilePropertyStorage("HashLog")]
        public Dictionary<string, string> HashLog { get; set; }


        /// <summary>
        /// A list of properties which have been changed since the profile was loaded
        /// from the profile store
        /// </summary>
        List<string> ChangedProperties { get; set; }


        #endregion

        protected SPSite Site { get; set; }
        protected UserProfileWrapper Profile { get; set; }
        protected UserProfileManagerWrapper UserProfileManager { get; set; }



        // TODO: this needs to be set to false if the elevated context will be disposed before the record is saved.
        public bool ContextProfileValid { get; set; }

        public abstract void EnsureCustomPropertiesExist();

        public void EnsurePropertiesExist()
        {
            UserProfileManager.EnsurePropertyExists("SourceLog", "SourceLog", "HTML", 2000, true, false);
            UserProfileManager.EnsurePropertyExists("HashLog", "HashLog", "HTML", 2000, true, false);

            EnsureCustomPropertiesExist();
        }


        protected ProfileBase(SPSite site, UserProfileWrapper profile)
            : this()
        {
            Site = site;
            Profile = profile;
            UserProfileManager = profile.ProfileManager;
            ContextProfileValid = true;

            EnsurePropertiesExist();

            var sl = (string)profile["SourceLog"].Value;
            if (!string.IsNullOrEmpty(sl))
            {
                var ll = sl.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var l in ll)
                {
                    var cl = l.Split(':');
                    var pn = cl[0].Trim();
                    var cs = cl[1].Trim();
                    this.SourceLog.Add(pn, cs);
                }

            }


            sl = (string)profile["HashLog"].Value;
            if (!string.IsNullOrEmpty(sl))
            {
                var ll = sl.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var l in ll)
                {
                    var cl = l.Split(':');
                    var pn = cl[0].Trim();
                    var cs = cl[1].Trim();
                    this.HashLog.Add(pn, cs);
                }

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

            // if not in changed properties list, do not save
            if (!ChangedProperties.Contains(pi.Name)) return;

            // get storage property name

            var propname = pi.GetCustomAttributes(typeof(ProfilePropertyStorageAttribute), false)
                .Cast<ProfilePropertyStorageAttribute>()
                .Select(a => a.PropertyName)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(propname)) return;

            LastProperty = propname;

            // call fval to get value in save format
            var val = fval((T)pi.GetValue(this, null));

            LastValue = val == null ? null : val.ToString();

            // write to profile store depending on type of value and profile property type

            var pp = Profile[propname];

            if (val is IEnumerable<string> && pp.Property.IsMultivalued)
            {
                pp.Clear();
                foreach (var v in (IEnumerable<string>)val) pp.Add(v);
            }
            else if (pp.Property.IsRequired && (val == null || val.ToString() == ""))
            {
                // don't try to clear required fields
            }
            else
            {
                pp.Value = val;
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
                
                SaveCustomProperties();
                SetIfChanged<ProfileBase, Dictionary<string, string>>(p => p.SourceLog, v => string.Join(
                        "\n",
                        v.Select(kv => kv.Key + ":" + kv.Value).ToArray()
                        ));
                SetIfChanged<ProfileBase, Dictionary<string, string>>(p => p.HashLog, v => string.Join(
                        "\n",
                        v.Select(kv => kv.Key + ":" + kv.Value).ToArray()
                        ));


                Profile.Commit();
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
