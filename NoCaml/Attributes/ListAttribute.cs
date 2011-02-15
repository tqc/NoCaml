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
using System.Collections.Generic;

namespace NoCaml
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ListAttribute : Attribute
    {
        public string DisplayName { get; set; }       
        public string Description { get; set; }
        /// <summary>
        /// "/" to create on root web. empty to create on current web. anything else to create on
        /// specific web.
        /// </summary>
        public string SiteRelativeWebUrl { get; set; }

        public SPListTemplateType Type { get; set; }

        /// <summary>
        /// eg Lists/ListName
        /// </summary>
        public string WebRelativeUrl { get; set; }

        public ListAttribute()
        {
            Type = SPListTemplateType.GenericList;
        }

        public Type[] NewFormWebParts { get; set; }
        public Type[] EditFormWebParts { get; set; }


        public void UpdateWithDefaults(Type lt)
        {
            if (string.IsNullOrEmpty(DisplayName))
            {
                DisplayName = lt.Name;
            }

            if (string.IsNullOrEmpty(WebRelativeUrl))
            {
                WebRelativeUrl =  "Lists/"+lt.Name;
            }

        }


    }
}
