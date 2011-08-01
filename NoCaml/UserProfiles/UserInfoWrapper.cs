using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;

namespace NoCaml.UserProfiles
{
    public class UserInfoWrapper
    {
        public string Email { get; set; }
        public string NTName { get; set; }
        public string PreferredName { get; set; }



        private static PropertyInfo piEmail;
        private static PropertyInfo piNTName;
        private static PropertyInfo piPreferredName;
        private static Type TUI;


        public UserInfoWrapper(object ui)
        {
            if (TUI == null)
            {
                TUI = ui.GetType();
                piEmail = TUI.GetProperty("Email");
                piNTName = TUI.GetProperty("NTName");
                piPreferredName = TUI.GetProperty("PreferredName");
            }

            Email = piEmail.GetValue(ui, new object[] { }) as string;
            NTName = piNTName.GetValue(ui, new object[] { }) as string;
            PreferredName = piPreferredName.GetValue(ui, new object[] { }) as string;

            if (string.IsNullOrEmpty(PreferredName)) PreferredName = NTName;
        }


    }
}
