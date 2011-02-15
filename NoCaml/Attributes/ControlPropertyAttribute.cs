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
    /// <summary>
    /// Indicates that a property on a user control web part 
    /// should be copied to the wrapped user control
    /// </summary>
    [global::System.AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public sealed class ControlPropertyAttribute : Attribute
    {
        public ControlPropertyAttribute()
        {

        }


    }
}
