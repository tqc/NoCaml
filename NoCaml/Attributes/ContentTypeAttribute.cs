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

namespace NoCaml
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class ContentTypeAttribute : Attribute
    {
        public string DisplayName { get; set; }
    // if this is set, the content type must already exist with this ID.
        public string ContentTypeID { get; set; }
    }
}
