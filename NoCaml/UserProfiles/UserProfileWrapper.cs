using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections;
using Microsoft.SharePoint;
using Microsoft.Office.Server;

namespace NoCaml.UserProfiles
{

	public enum LEQuickLinkGroupType
	{
		UserSpecified = 0,
		BestBet = 1,
		General = 2,
	}

	public enum LEPrivacy
	{
		Public = 1,
		Contacts = 2,
		Organization = 4,
		Manager = 8,
		Private = 16,
		NotSet = 1073741824,
	}
	public enum LEChoiceTypes
	{
		Off = 0,
		None = 1,
		Open = 2,
		Closed = 3,
	}
	public enum LEPrivacyPolicy
	{
		Mandatory = 1,
		OptIn = 2,
		OptOut = 4,
		Disabled = 8,
	}

	public enum LEMultiValueSeparator
	{
		Comma = 0,
		Semicolon = 1,
		Newline = 2,
		Unknown = 255,
	}

	public class UserProfilePropertyWrapper
	{
		public object UPP;
		private static Type TUPP;
		private static PropertyInfo piName;
		private static PropertyInfo piDisplayName;
		private static PropertyInfo piType;
		private static PropertyInfo piDefaultPrivacy;
		private static PropertyInfo piPrivacyPolicy;
		private static PropertyInfo piDescription;
		private static PropertyInfo piIsSearchable;
		private static PropertyInfo piIsVisibleOnEditor;
		private static PropertyInfo piIsAlias;
		private static PropertyInfo piIsMultivalued;
		private static PropertyInfo piIsRequired;
		private static PropertyInfo piSeparator;
		private static PropertyInfo piChoiceType;
		private static PropertyInfo piLength;
		private static PropertyInfo piIsUserEditable;

		public UserProfilePropertyWrapper(object upp)
		{
			if (TUPP == null)
			{
				TUPP = upp.GetType();
				piName = TUPP.GetProperty("Name");
				piDisplayName = TUPP.GetProperty("DisplayName");
				piType = TUPP.GetProperty("Type");
				piDefaultPrivacy = TUPP.GetProperty("DefaultPrivacy");
				piPrivacyPolicy = TUPP.GetProperty("PrivacyPolicy");
				piDescription = TUPP.GetProperty("Description");
				piIsSearchable = TUPP.GetProperty("IsSearchable");
				piIsVisibleOnEditor = TUPP.GetProperty("IsVisibleOnEditor");
				piIsAlias = TUPP.GetProperty("IsAlias");
				piIsMultivalued = TUPP.GetProperty("IsMultivalued");
				piIsRequired = TUPP.GetProperty("IsRequired");
				piSeparator = TUPP.GetProperty("Separator");
				piChoiceType = TUPP.GetProperty("ChoiceType");
				piLength = TUPP.GetProperty("Length");
				piIsUserEditable = TUPP.GetProperty("IsUserEditable");
			}
			UPP = upp;
		}

		public string Name
		{
			get { return piName.GetValue(UPP, null) as string; }
			set { piName.SetValue(UPP, value, null); }
		}

		public string DisplayName
		{
			get { return piDisplayName.GetValue(UPP, null) as string; }
			set { piDisplayName.SetValue(UPP, value, null); }
		}

		public string Type
		{
			get { return piType.GetValue(UPP, null) as string; }
			set { piType.SetValue(UPP, value, null); }
		}

		public int DefaultPrivacy
		{
			get { return (int)piDefaultPrivacy.GetValue(UPP, null); }
			set { piDefaultPrivacy.SetValue(UPP, value, null); }
		}

		public int PrivacyPolicy
		{
			get { return (int)piPrivacyPolicy.GetValue(UPP, null); }
			set { piPrivacyPolicy.SetValue(UPP, value, null); }
		}

		public string Description
		{
			get { return piDescription.GetValue(UPP, null) as string; }
			set { piDescription.SetValue(UPP, value, null); }
		}

		public bool IsSearchable
		{
			get { return (bool)piIsSearchable.GetValue(UPP, null); }
			set { piIsSearchable.SetValue(UPP, value, null); }
		}

		public bool IsVisibleOnEditor
		{
			get { return (bool)piIsVisibleOnEditor.GetValue(UPP, null); }
			set { piIsVisibleOnEditor.SetValue(UPP, value, null); }
		}

		public bool IsAlias
		{
			get { return (bool)piIsAlias.GetValue(UPP, null); }
			set { piIsAlias.SetValue(UPP, value, null); }
		}

		public bool IsMultivalued
		{
			get { return (bool)piIsMultivalued.GetValue(UPP, null); }
			set { piIsMultivalued.SetValue(UPP, value, null); }
		}

		public bool IsRequired
		{
			get { return (bool)piIsRequired.GetValue(UPP, null); }
			set { piIsRequired.SetValue(UPP, value, null); }
		}

		public int Separator
		{
			get { return (int)piSeparator.GetValue(UPP, null); }
			set { piSeparator.SetValue(UPP, value, null); }
		}

		public int ChoiceType
		{
			get { return (int)piChoiceType.GetValue(UPP, null); }
			set { piChoiceType.SetValue(UPP, value, null); }
		}

		public int Length
		{
			get { return (int)piLength.GetValue(UPP, null); }
			set { piLength.SetValue(UPP, value, null); }
		}

		public bool IsUserEditable
		{
			get { return (bool)piIsUserEditable.GetValue(UPP, null); }
			set { piIsUserEditable.SetValue(UPP, value, null); }
		}
	}

	public class UserProfileValueCollectionWrapper
	{
		private object UPVC;
		private static Type TUPVC;
		private static PropertyInfo piValue;
		private static PropertyInfo piCount;
		private static PropertyInfo piProperty;
		private static MethodInfo miClear;
        private static MethodInfo miAdd;
        private static MethodInfo miGetEnumerator;

		public UserProfileValueCollectionWrapper(object upvc)
		{
			if (TUPVC == null)
			{
				TUPVC = upvc.GetType();
				piValue = TUPVC.GetProperty("Value");
				piProperty = TUPVC.GetProperty("Property");
				piCount = TUPVC.GetProperty("Count");
				miClear = TUPVC.GetMethod("Clear");
				miAdd = TUPVC.GetMethod("Add");
				miGetEnumerator = TUPVC.GetMethod("GetEnumerator");
			}
			UPVC = upvc;
		}

		public object Value
		{
			get { return piValue.GetValue(UPVC, null); }
			set { piValue.SetValue(UPVC, value, null); }
		}

       

		public UserProfilePropertyWrapper Property
		{
			get { return new UserProfilePropertyWrapper(piProperty.GetValue(UPVC, null)); }
		}

		public void Clear()
		{
			miClear.Invoke(UPVC, new object[] { });
		}


		public void Add(object v)
		{
			miAdd.Invoke(UPVC, new object[] { v });
		}

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator) miGetEnumerator.Invoke(UPVC, new object[] {});
        }


		public int Count
		{
			get { return (int)piCount.GetValue(UPVC, null); }
		}
	}


	public class UserProfileWrapper
	{
		private object UP;
		private static Type TUP;
		private static PropertyInfo piItem;
		private static PropertyInfo piProfileManager;
		private static MethodInfo miCommit;
        private static PropertyInfo piQuickLinks;

		public UserProfileWrapper(object p)
		{
			if (TUP == null)
			{
				TUP = p.GetType();
				piItem = TUP.GetProperty("Item");
				piProfileManager = TUP.GetProperty("ProfileManager");
				miCommit = TUP.GetMethod("Commit");
                piQuickLinks = TUP.GetProperty("QuickLinks");
            }
			UP = p;

		}

		public UserProfileValueCollectionWrapper this[string s]
		{
			get
			{
				return new UserProfileValueCollectionWrapper(piItem.GetValue(UP, new object[] { s }));
			}
		}

		public UserProfileManagerWrapper ProfileManager
		{
			get
			{
				return new UserProfileManagerWrapper(piProfileManager.GetValue(UP, new object[] { }));
			}
		}

        public QuickLinkManagerWrapper QuickLinks
        {
            get
            {
                return new QuickLinkManagerWrapper(piQuickLinks.GetValue(UP, new object[] { }));
            }
        }


		public void Commit()
		{
			miCommit.Invoke(UP, new object[] { });
		}
	}

    public class QuickLinkWrapper
    {
        private object QL;

        private static Type TQL;
        private static MethodInfo miDelete;
        private static MethodInfo miCommit;
        private static PropertyInfo piID;
        private static PropertyInfo piTitle;
        private static PropertyInfo piUrl;
        private static PropertyInfo piGroup;


        public QuickLinkWrapper(object ql)
        {
            if (TQL == null)
            {
                TQL = ql.GetType();
                miDelete = TQL.GetMethod("Delete");
                miCommit = TQL.GetMethod("Commit");
                piID = TQL.GetProperty("ID");
                piTitle = TQL.GetProperty("Title");
                piUrl = TQL.GetProperty("Url");
                piGroup = TQL.GetProperty("Group");

            }

            QL = ql;
        }

        public long ID { get { return (long)piID.GetValue(QL, null); }  }
        public string Title { get { return (string)piTitle.GetValue(QL, null); }  }
        public string Url { get { return (string)piUrl.GetValue(QL, null); }  }
        public string Group { get { return (string)piGroup.GetValue(QL, null); } }


        public void Delete()
        {
            miDelete.Invoke(QL, new object[] { });
        }
        public void Commit()
        {
            miCommit.Invoke(QL, new object[] { });
        }

    }
	public class QuickLinkManagerWrapper
	{
		private object QLM;

		private static Type TQLM;
		private static PropertyInfo piItem;
		private static MethodInfo miCreate;
		private static MethodInfo miGetItems;

		public QuickLinkManagerWrapper(object qlm)
		{
			if (TQLM == null)
			{
				TQLM = qlm.GetType();
				piItem = TQLM.GetProperty("Item");
				miCreate = TQLM.GetMethod("Create");
				miGetItems = TQLM.GetMethod("GetItems", new Type[] {});
			}
		   
			QLM = qlm;
		}

     

        public IEnumerable<QuickLinkWrapper> GetItems()
        {
            foreach (var p in miGetItems.Invoke(QLM, new object[] { }) as IEnumerable)
            {
                yield return new QuickLinkWrapper(p);
            }
        }

		public QuickLinkWrapper this[long id]
		{
			get
			{
				return new QuickLinkWrapper(piItem.GetValue(QLM, new object[] { id }));
			}
		}


		public QuickLinkWrapper Create (
	string strTitle,
	string strUrl,
	LEQuickLinkGroupType groupType,
	string strGroup,
	LEPrivacy privacyLevel
) {
				return new QuickLinkWrapper(miCreate.Invoke(QLM, new object[] { strTitle, strUrl, (int)groupType, strGroup, (int)privacyLevel  }));
	}
	}

	public class UserProfileManagerWrapper
	{
		private object UPM;

		private static Type TUPM;
		private static MethodInfo miGetUserProfileBool;
		private static MethodInfo miUserExists;
		private static MethodInfo miGetUserProfileString;
		private static MethodInfo miCreateUserProfile;
		private static MethodInfo miGetEnumerator;
        private static ConstructorInfo ciContext;


		/// <summary>
		/// Set the first time EnsurePropertiesExists is called in the process so that it
		/// does not run unnecessarily
		/// </summary>
		public static bool PropertyListUpdated { get; set; }


		public UserProfileManagerWrapper(SPSite site)
		{
			if (TUPM == null)
			{
				// get a reference to Microsoft.Office.Servers
				var a = typeof(ServerContext).Assembly;
				TUPM = a.GetExportedTypes()
					.Where(t => t.FullName == "Microsoft.Office.Server.UserProfiles.UserProfileManager")
					.FirstOrDefault();
				// if UserProfileManager wasn't defined in Microsoft.Office.Servers, this is SP2010 and 
				// we need to load Microsoft.Office.Servers.UserProfiles
				if (TUPM == null)
				{
					a = Assembly.Load("Microsoft.Office.Server.UserProfiles, Version=14.0.0.0, Culture=neutral, PublicKeyToken=71e9bce111e9429c");
					TUPM = a.GetExportedTypes()
						.Where(t => t.FullName == "Microsoft.Office.Server.UserProfiles.UserProfileManager")
						.FirstOrDefault();
				}

				miGetUserProfileBool = TUPM.GetMethod("GetUserProfile", new Type[] { typeof(bool) });
				miGetUserProfileString = TUPM.GetMethod("GetUserProfile", new Type[] { typeof(string) });
				miUserExists = TUPM.GetMethod("UserExists", new Type[] { typeof(string) });
				miCreateUserProfile = TUPM.GetMethod("CreateUserProfile", new Type[] { typeof(string) });
                miGetEnumerator = TUPM.GetMethod("GetEnumerator", new Type[] { });
                ciContext = TUPM.GetConstructor(new Type[] { typeof(ServerContext) });
            }
			UPM = ciContext.Invoke(new object[] { ServerContext.GetContext(site) });
		}

		public UserProfileManagerWrapper(object upm)
		{
			if (TUPM == null)
			{
				throw new Exception("This should never be the first instance created");
			}
			UPM = upm;
		}


		public UserProfileWrapper GetUserProfile(bool createIfNotExist)
		{
			return new UserProfileWrapper(miGetUserProfileBool.Invoke(UPM, new object[] { createIfNotExist }));
		}

		public bool UserExists(string accountName)
		{
			return (bool)miUserExists.Invoke(UPM, new object[] { accountName });
		}

        public IEnumerator GetEnumerator()
        {
            return (IEnumerator)miGetEnumerator.Invoke(UPM, new object[] { });
        }

		public UserProfileWrapper GetUserProfile(string accountName)
		{
			return new UserProfileWrapper(miGetUserProfileString.Invoke(UPM, new object[] { accountName }));
		}

		public UserProfileWrapper CreateUserProfile(string accountName)
		{
			return new UserProfileWrapper(miCreateUserProfile.Invoke(UPM, new object[] { accountName }));
		}


		public IEnumerable<UserProfileWrapper> GetAllProfiles()
		{
			foreach (var p in this)
			{
				yield return new UserProfileWrapper(p);
			}
		}



		public void EnsurePropertyExists(string displayName, string name, string type, int length, bool searchable, bool multiple)
		{

			var pcpi = TUPM.GetProperty("Properties");
			var properties = pcpi.GetValue(UPM, null) as IEnumerable;




			if (!properties.Cast<object>().Select(o => new UserProfilePropertyWrapper(o)).Any(p => p.Name == name))
			{
                try
                {
                    var miCreate = properties.GetType().GetMethod("Create", new Type[] { typeof(bool) });
                    var miAdd = properties.GetType().GetMethod("Add");
                    var p = new UserProfilePropertyWrapper(miCreate.Invoke(properties, new object[] { false }));
                    //                var p = UPM.Properties.Create(false);


                    p.Name = name;
                    p.DisplayName = displayName;
                    p.Type = type;
                    p.DefaultPrivacy = (int)LEPrivacy.Public;
                    p.PrivacyPolicy = (int)LEPrivacyPolicy.OptOut;
                    p.Description = displayName;
                    p.IsSearchable = searchable;
                    p.IsVisibleOnEditor = true;
                    p.IsAlias = false;
                    p.IsMultivalued = multiple;
                    if (multiple)
                    {
                        p.Separator = (int)LEMultiValueSeparator.Semicolon;
                        //p.ChoiceType = (int)LEChoiceTypes.Open;
                    }
                    if (type == "string" || type == "HTML")
                    {
                        p.Length = length;
                    }

                    p.IsUserEditable = true;
                    //UPM.Properties.Add(p);
                    miAdd.Invoke(properties, new object[] { p.UPP });
                }
                catch (TargetInvocationException ex)
                {
                    if (ex.InnerException != null)
                    {
                        throw new Exception("Error creating profile property " + name + " - " + ex.InnerException.Message, ex.InnerException);
                    }
                    else
                    {
                        throw new Exception("Error creating profile property " + name + " - " + ex.Message, ex);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Error creating profile property " + name + " - " + ex.Message, ex);
                }
			}
		}




	}

}
