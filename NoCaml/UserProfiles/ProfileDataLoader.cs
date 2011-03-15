using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Reflection;

namespace NoCaml.UserProfiles
{
    public abstract class ProfileDataLoader<TProfile, TSource> where TProfile : ProfileBase
    {
       protected string SourceName { get; set; }

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



        public void UpdateProperty(TProfile profile, TSource source, Expression<Func<TProfile, object>> expr, Func<TProfile, TSource, object> val)
        {
            var n = expr.Body as MemberExpression;
            if (expr.Body.NodeType == ExpressionType.Convert) n = ((UnaryExpression)expr.Body).Operand as MemberExpression;

            var pi = n.Member as PropertyInfo;

        //    Debug.WriteLine(pi.Name);
//            var pial = pi.GetCustomAttributes(typeof(ProfilePropertySourceAttribute), false).Cast<ProfilePropertySourceAttribute>();
            var pial = pi.GetCustomAttributes(true).Where(a=>a is ProfilePropertySourceAttribute).Cast<ProfilePropertySourceAttribute>();

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

            
            // do not update if changed
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

            foreach (var kv in NullableMappings)
            {
                UpdateProperty(profile, source, kv.Key, kv.Value);
            }

            if (source != null)
            foreach (var kv in Mappings)
            {
                UpdateProperty(profile, source, kv.Key, kv.Value);
            }


            // for each field in mappings where shouldupdate is true

            // set value in profile
            // set isdirty and source

        }

    }
}
