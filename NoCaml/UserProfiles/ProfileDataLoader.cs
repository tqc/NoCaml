using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;

namespace NoCaml.UserProfiles
{
    public abstract class ProfileDataLoader<TProfile, TSource> : ProfileDataLoader<TProfile> where TProfile : ProfileBase
    {

        public Dictionary<Expression<Func<TProfile, object>>, Func<TProfile, TSource, object>> Mappings { get; set; }

        /// <summary>
        /// Mappings which will be executed even if the source is null
        /// </summary>
        public Dictionary<Expression<Func<TProfile, object>>, Func<TProfile, TSource, object>> NullableMappings { get; set; }

        protected ProfileDataLoader()
        {
            NullableMappings = new Dictionary<Expression<Func<TProfile, object>>, Func<TProfile, TSource, object>>();
            Mappings = new Dictionary<Expression<Func<TProfile, object>>, Func<TProfile, TSource, object>>();
        }

        private class LoadablePropertyDetails
        {
            public PropertyInfo PropertyInfo { get; set; }
            public List<string> BetterSources { get; set; }
            public bool RaisePriorityOnChange { get; set; }
            public bool UseIfNull { get; set; }
            public bool UpdateForNullSource { get; set; }
            public Func<TProfile, TSource, object> ValueFunction { get; set; }
            public bool IsValid { get; set; }

            public LoadablePropertyDetails(Expression<Func<TProfile, object>> expr, Func<TProfile, TSource, object> val, string sourceName, bool updateForNullSource)
            {
                IsValid = true;
                var n = expr.Body as MemberExpression;
                if (expr.Body.NodeType == ExpressionType.Convert) n = ((UnaryExpression)expr.Body).Operand as MemberExpression;
                PropertyInfo = n.Member as PropertyInfo;
                var pial = PropertyInfo.GetCustomAttributes(true).Where(a => a is ProfilePropertySourceAttribute).Cast<ProfilePropertySourceAttribute>();
                var sa = pial.Where(a => a.ProfileSource == sourceName).FirstOrDefault();
                if (sa == null)
                {
                    IsValid = false;
                }
                else
                {
                    BetterSources = pial.Where(a => a.Order < sa.Order).Select(a => sa.ProfileSource).ToList();
                    RaisePriorityOnChange = sa.RaisePriorityIfChanged;
                    UseIfNull = sa.UseIfEmpty;
                    UpdateForNullSource = updateForNullSource;
                    ValueFunction = val;
                }
            }
        }

        private object syncRoot = new object();
        private Dictionary<string, LoadablePropertyDetails> PropertyCache { get; set; }

        private void UpdateCachedDetails()
        {
            PropertyCache = new Dictionary<string, LoadablePropertyDetails>();
            foreach (var kv in Mappings)
            {
                var lpd = new LoadablePropertyDetails(kv.Key, kv.Value, this.SourceName, false);
                if (lpd.IsValid) PropertyCache.Add(lpd.PropertyInfo.Name, lpd);
            }

            foreach (var kv in NullableMappings)
            {
                var lpd = new LoadablePropertyDetails(kv.Key, kv.Value, this.SourceName, true);
                if (lpd.IsValid) PropertyCache.Add(lpd.PropertyInfo.Name, lpd);
            }

        }


        private void UpdateProperty(TProfile profile, TSource source, LoadablePropertyDetails lpd)
        {
            // if this loader is not a valid source, return
            if (!lpd.IsValid) return;

            // only some update functions are valid for null source
            if (source == null && !lpd.UpdateForNullSource) return;

            var currentsource = profile.GetCurrentSource(lpd.PropertyInfo.Name);

            // if current source is higher priority, do not update
            if (!lpd.RaisePriorityOnChange
                && !string.IsNullOrEmpty(currentsource)
                && lpd.BetterSources.Contains(currentsource)) return;

            var currentvalue = lpd.PropertyInfo.GetValue(profile, null);
            var newvalue = lpd.ValueFunction(profile, source);

            // if new source is lower priority but can raise priority on change, 
            // check stored hash, update if different

            if (lpd.RaisePriorityOnChange && newvalue as string != null)
            {
                var changed = profile.ImportedPropertyChanged(lpd.PropertyInfo.Name, newvalue as string);
                if (!changed
                    && !string.IsNullOrEmpty(currentsource)
                    && lpd.BetterSources.Contains(currentsource)) return;
            }

            // may skip update if changed to null
            if (!lpd.UseIfNull && (newvalue == null || string.IsNullOrEmpty(newvalue.ToString()))) return;

            // do not update if not changed
            if (currentvalue == newvalue
                || (currentvalue == null && newvalue is string && string.IsNullOrEmpty((string)newvalue))
                || (currentvalue != null && newvalue != null && currentvalue.ToString() == newvalue.ToString())
                )
                return;

            lpd.PropertyInfo.SetValue(profile, newvalue, null);
            profile.SetUpdated(lpd.PropertyInfo.Name, SourceName);

        }


        [Obsolete]
        private void UpdateProperty(TProfile profile, TSource source, Expression<Func<TProfile, object>> expr, Func<TProfile, TSource, object> val)
        {
            var n = expr.Body as MemberExpression;
            if (expr.Body.NodeType == ExpressionType.Convert) n = ((UnaryExpression)expr.Body).Operand as MemberExpression;

            var pi = n.Member as PropertyInfo;

            //    Debug.WriteLine(pi.Name);
            //            var pial = pi.GetCustomAttributes(typeof(ProfilePropertySourceAttribute), false).Cast<ProfilePropertySourceAttribute>();
            var pial = pi.GetCustomAttributes(true).Where(a => a is ProfilePropertySourceAttribute).Cast<ProfilePropertySourceAttribute>();

            // foreach (var a in pial.OrderBy(aa => aa.Order))
            //  {
            //       Debug.WriteLine(a.ProfileSource);
            //  }

            var sa = pial.Where(a => a.ProfileSource == this.SourceName).FirstOrDefault();

            // if this loader is not a valid source, return
            if (sa == null) return;

            var currentsource = profile.GetCurrentSource(pi.Name);
            var nsa = pial.Where(a => a.ProfileSource == currentsource).FirstOrDefault();

            // if current source is higher priority, do not update
            if (nsa != null && nsa.Order < sa.Order && !sa.RaisePriorityIfChanged) return;

            var currentvalue = pi.GetValue(profile, null);
            var newvalue = val(profile, source);

            // if new source is lower priority but can raise priority on change, 
            // check stored hash, update if different

            if (sa.RaisePriorityIfChanged && newvalue as string != null)
            {
                var changed = profile.ImportedPropertyChanged(pi.Name, newvalue as string);
                if (nsa != null && nsa.Order < sa.Order && !changed) return;
            }


            // may skip update if changed to null
            if (!sa.UseIfEmpty && (newvalue == null || string.IsNullOrEmpty(newvalue.ToString()))) return;

            // do not update if not changed
            if (currentvalue == newvalue
                || (currentvalue == null && newvalue is string && string.IsNullOrEmpty((string)newvalue))
                || (currentvalue != null && newvalue != null && currentvalue.ToString() == newvalue.ToString())
                )
                return;

            pi.SetValue(profile, newvalue, null);
            profile.SetUpdated(pi.Name, SourceName);

        }


        public void UpdateProfile(TProfile profile, TSource source)
        {
            // this bit really belongs in the constructor, but it has to be called after the 
            // subclass constructor completes.
            if (PropertyCache == null)
            {
                lock (syncRoot)
                {
                    if (PropertyCache == null)
                    {
                        UpdateCachedDetails();
                    }
                }
            }
            foreach (var kv in PropertyCache)
            {
                UpdateProperty(profile, source, kv.Value);
            }
            /*

                        foreach (var kv in NullableMappings)
                        {
                            UpdateProperty(profile, source, kv.Key, kv.Value);
                        }

                        if (source != null)
                            foreach (var kv in Mappings)
                            {
                                UpdateProperty(profile, source, kv.Key, kv.Value);
                            }

            */
            // for each field in mappings where shouldupdate is true

            // set value in profile
            // set isdirty and source

        }
    }

    public abstract class ProfileDataLoader<TProfile> where TProfile : ProfileBase
    {
        protected string SourceName { get; set; }


        /// <summary>
        /// Expiry time in seconds for real time updates. Default is 10 minutes.
        /// </summary>
        public virtual int RealTimeUpdateExpiry { get { return 600; } }

        /// <summary>
        /// Maximum number of profiles to update each time the update is run. Set to 0 if updates should not 
        /// happen in batch process.
        /// </summary>
        public virtual int SpreadUpdateProfileCount { get { return 0; } }


        /// <summary>
        /// recent update times used to skip full updates
        /// </summary>
        private static Dictionary<string, DateTime> InProcUpdates = new Dictionary<string, DateTime>();

        public static void ResetUpdateFlags()
        {
            InProcUpdates = new Dictionary<string, DateTime>();
        }



        /// <summary>
        /// For sources where validity is dependent on other properties, allow filtering of the profile list 
        /// so that updates expected to fail do not run
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public virtual bool IsValidProfile(TProfile p)
        {
            return true;
        }


        /// <summary>
        /// Update an individual profile with no other data available - generally by calling a web service.        
        /// Return true if the profile should be recorded as updated sucessfully
        /// </summary>
        /// <param name="p"></param>
        protected virtual bool UpdateProfileRealTime(TProfile p)
        {
            return false;
        }

        /// <summary>
        /// Update an individual profile from data loaded with LoadBulkData
        /// Return true if the profile should be recorded as updated sucessfully
        /// </summary>
        /// <param name="p"></param>
        protected virtual bool UpdateProfileBatch(TProfile p)
        {
            return false;
        }

        public static Action<string, string, string> Log { get; set; }

        /// <summary>
        /// For batch updates, load necessary data. Returns true if bulk data is available, false if
        /// individual updates are required.
        /// </summary>
        public virtual bool LoadBulkData()
        {
            return false;
        }

        private string[] ProfilesToUpdate { get; set; }

        private string[] SelectProfilesToUpdate(IEnumerable<TProfile> profiles)
        {
            var rnd = new Random();
            if (SpreadUpdateProfileCount == 0) return null;
            if (SpreadUpdateProfileCount == int.MaxValue) return null;

            var result = profiles
    .Where(p => IsValidProfile(p))
    .Where(p => !InProcUpdates.ContainsKey(SourceName + "|" + p.LanID) || InProcUpdates[SourceName + "|" + p.LanID] < DateTime.Now.AddSeconds(-RealTimeUpdateExpiry))
    .OrderBy(p => rnd.Next())
    .Take(SpreadUpdateProfileCount)
    .Select(p => p.LanID.ToLower())
    .ToArray();

            foreach (var p in result)
            {
                // updating here to avoid potential issues updating on 64 threads simultaneously
                InProcUpdates[SourceName + "|" + p] = DateTime.Now;
            }

            return result;
        }

        /// <summary>
        /// return true if this profile is in the import file
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        protected virtual bool BulkDataContains(TProfile p)
        {
            return false;
        }

        /// <summary>
        /// return true if this profile may have been in the import previously so should be updated
        /// even if it is no longer present. This may overlap with BulkDataContains - it is used only
        /// for choosing records to be updated.
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        protected virtual bool BulkDataUsedToContain(TProfile p)
        {
            return false;
        }


        private bool ShouldUpdateInBatch(TProfile p)
        {
            if (!IsValidProfile(p)) return false;
            if (SpreadUpdateProfileCount == int.MaxValue) return true;
            if (BulkDataAvailable) return BulkDataContains(p) || BulkDataUsedToContain(p);
            if (ProfilesToUpdate != null && ProfilesToUpdate.Length > 0) return ProfilesToUpdate.Contains(p.LanID.ToLower());

            return false;
        }


        private static void LogException(string source, Exception ex)
        {
            if (Log != null)
            {
                if (ex is TargetInvocationException && ex.InnerException != null)
                {
                    Log(source, ex.InnerException.Message, ex.InnerException.StackTrace);
                }
                else
                {
                    Log(source, ex.Message, ex.StackTrace);
                }
            }

        }
        public static void RunRealtimeUpdate(IEnumerable<ProfileDataLoader<TProfile>> pdls, TProfile profile)
        {
            foreach (var pdl in pdls)
            {
                try
                {
                    var k = pdl.SourceName + "|" + profile.LanID;
                    if (pdl.IsValidProfile(profile) && (!InProcUpdates.ContainsKey(k) || InProcUpdates[k] < DateTime.Now.AddSeconds(-pdl.RealTimeUpdateExpiry)))
                    {
                        if (pdl.UpdateProfileRealTime(profile))
                        {
                            profile.Save();
                            InProcUpdates[k] = DateTime.Now;
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogException(pdl.SourceName, ex);
                }
            }
        }


        private object StatSync = new object();
        //public int InitializationTime;
        ///public int UpdateTime;
        public int ProfilesChecked;
        public int ProfilesUpdated;


        private bool LoaderInitialized = false;
        private bool BulkDataAvailable = false;


        public static void RunBatchUpdate(IEnumerable<ProfileDataLoader<TProfile>> pdls, IEnumerable<TProfile> profiles)
        {
            // initialize each loader

            foreach (var pdl in pdls)
            {
                try
                {
                    pdl.LoaderInitialized = false;
                    pdl.BulkDataAvailable = pdl.LoadBulkData();
                    pdl.ProfilesToUpdate = pdl.SelectProfilesToUpdate(profiles);
                    pdl.LoaderInitialized = true;
                }
                catch (Exception ex)
                {
                    // log init failure
                    LogException(pdl.SourceName, ex);
                }
            }

            var activeLoaders = pdls.Where(pdl => pdl.LoaderInitialized && (pdl.BulkDataAvailable || pdl.SpreadUpdateProfileCount > 0)).ToList();

            profiles.EachParallel(p =>
            {
                bool updated = false;
                var changedPropertyCount = 0;
                foreach (var pdl in activeLoaders)
                {

                    if (pdl.ShouldUpdateInBatch(p))
                    {
                        lock (pdl.StatSync) { pdl.ProfilesChecked++; }
                        try
                        {
                            if (pdl.BulkDataAvailable)
                            {
                                pdl.UpdateProfileBatch(p);
                            }
                            else
                            {
                                pdl.UpdateProfileRealTime(p);
                            }
                            var c2 = p.ChangedProperties.Count;
                            updated = updated || c2 > changedPropertyCount;
                            if (c2 > changedPropertyCount) { lock (pdl.StatSync) { pdl.ProfilesUpdated++; } }
                            changedPropertyCount = c2;
                        }

                        catch (Exception ex)
                        {
                            LogException(pdl.SourceName + ":" + p.LanID, ex);
                        }
                    }

                }
                if (updated)
                {
                    try
                    {
                        p.Save();
                    }
                    catch (Exception ex)
                    {
                        LogException("Saving:" + p.LanID, ex);
                    }
                }

            });

        }

    }
}
