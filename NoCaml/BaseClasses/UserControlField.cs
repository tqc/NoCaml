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
using Microsoft.SharePoint.WebControls;

namespace NoCaml
{
    public abstract class UserControlFieldControl<T> : BaseFieldControl where T:UserControl, IFieldEditUserControl
    {
        protected UserControlFieldControl()
        {           
        }

        private T UC;

        public override object Value { get { return UC.Value; } set { UC.Value = value; } }

        protected virtual void TransferProperties(T ctrl)
        {
            var tc = typeof(T);
            var tt = this.GetType();

            foreach (var p in tt.GetProperties()) {
                if (p.IsDefined(typeof(ControlPropertyAttribute), true)) {
                    foreach (var p2 in tc.GetProperties()) {
                        if (p2.Name == p.Name) {
                            p2.SetValue(ctrl, p.GetValue(this, null), null);
                        }
                    }
                }
            }
       }


        protected override void CreateChildControls()
        {
            string controlURL = ControlFolder+typeof(T).Name+".ascx";
            UC = Page.LoadControl(controlURL) as T;
            TransferProperties(UC);
            this.Controls.Add(UC);
        }

        protected virtual string ControlFolder
        {
            get {
                return "~/_layouts/UserControlWebParts/";
            }
        }
    
    }

    public interface IFieldEditUserControl
    {
        object Value { get; set; }
    }

}
