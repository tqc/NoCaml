using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace NoCaml.UserProfiles
{
    public class AudienceWrapper
    {
        public Guid AudienceId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Operator { get; set; }
        public List<Rule> Rules { get; set; }

        private object A { get; set; }
        private static PropertyInfo piName;
        private static PropertyInfo piId;
        private static PropertyInfo piDescription;
        private static PropertyInfo piOperator;
        private static PropertyInfo piRules;
        private static PropertyInfo piMembershipCount;
        private static PropertyInfo piLastCompilation;
        private static PropertyInfo piLastError;
        private static MethodInfo miGetMembership;
        private static Type TA;

        public AudienceWrapper() { }

        public AudienceWrapper(object a)
		{
			if (TA == null)
			{
				TA = a.GetType();
                piId = TA.GetProperty("AudienceID");
                piName = TA.GetProperty("AudienceName");
                piDescription = TA.GetProperty("AudienceDescription");
                piOperator = TA.GetProperty("GroupOperation");
                piRules = TA.GetProperty("AudienceRules");
                piMembershipCount = TA.GetProperty("MemberShipCount");
                piLastCompilation = TA.GetProperty("LastCompilation");
                piLastError = TA.GetProperty("LastError");
                miGetMembership = TA.GetMethod("GetMembership");
            }
            A = a;


            Name = piName.GetValue(A, new object[] { }) as string;
            AudienceId = (Guid)piId.GetValue(A, new object[] { });
            Description = piDescription.GetValue(A, new object[] { }) as string;
            Operator = piOperator.GetValue(A, new object[] { }).ToString();

            var ral = (System.Collections.ArrayList)piRules.GetValue(A, new object[] { });
            Rules = new List<Rule>();
            if (ral != null)
            {
                foreach (object arc in ral)
                {
                    Rules.Add(new Rule(arc));
                }
            }

		}

        public int MembershipCount
        {
            get
            {
                return (int)piMembershipCount.GetValue(A, new object[] { });
            }
        }

        public DateTime LastCompilation
        {
            get
            {
                return (DateTime)piLastCompilation.GetValue(A, new object[] { });
            }
        }
        public string LastError
        {
            get
            {
                return (string)piLastError.GetValue(A, new object[] { });
            }
        }
        

        public List<UserInfoWrapper> GetMembers()
        {
            var uial = (System.Collections.ArrayList)miGetMembership.Invoke(A, new object[] { });
            var luiw = new List<UserInfoWrapper>();
            if (uial != null)
            {
                foreach (object ui in uial)
                {
                    luiw.Add(new UserInfoWrapper(ui));
                }
            }
            return luiw;
        }

    }
}
