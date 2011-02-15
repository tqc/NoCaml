using System;
using System.Data;
using System.Configuration;
using System.Linq;
using System.Web;
using System.Web.Security;
using System.Web.UI;
using System.Web.UI.HtmlControls;
using System.Web.UI.WebControls;
using System.Web.UI.WebControls.WebParts;
using Microsoft.SharePoint;
using System.Reflection;
using System.Collections.Generic;

namespace NoCaml
{
    [AttributeUsage(AttributeTargets.All)]
    public class ChoiceAttribute : Attribute
    {
        public string DisplayName { get; set; }

        public ChoiceAttribute(string displayName)
        {
            DisplayName = displayName;
        }


    }
}
