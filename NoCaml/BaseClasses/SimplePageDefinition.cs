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
    public abstract class SimplePageDefinition : PageDefinition
    {
        private string _ComponentName;
        private string _ComponentDescription;
        private bool _IsWelcomePage;
        private string _LayoutFileName;
        private string[] _FilterTags;
        private string[] _PageTags;


        public SimplePageDefinition(string componentName, string componentDescription, string layoutfilename, bool isWelcomePage, string[] filterTags, string[] pageTags)
        {
            _ComponentName = componentName;
            _ComponentDescription = componentDescription;
            _IsWelcomePage = isWelcomePage;
            _FilterTags = filterTags ?? base.FilterTags;
            _PageTags = pageTags ?? base.PageTags;
            _LayoutFileName = layoutfilename;
        }



       

        public override string ComponentName
        {
            get { return _ComponentName; }
        }

        public override string ComponentDescription
        {
            get { return _ComponentDescription; }
        }

        public override bool IsWelcomePage
        {
            get { return _IsWelcomePage; }
        }

        public override string[] FilterTags
        {
            get
            {
                return _FilterTags;
            }
        }

        public override string[] PageTags
        {
            get
            {
                return _PageTags;
            }
        }

        public override string Layout
        {
            get
            {
                return _LayoutFileName;
            }
        }

    }
}
