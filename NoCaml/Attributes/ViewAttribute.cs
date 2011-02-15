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

namespace NoCaml
{
    [AttributeUsage(AttributeTargets.Class)]
    public class ViewAttribute : Attribute
    {
        public string ViewName { get; set; }       
        
        /// <summary>
        /// Display names of fields to include in the view
        /// </summary>
        public string[] Fields {get;set;}

        public bool IsDefault { get; set; }

        public ViewAttribute()
        {            
        }



    }
}
