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
using System.ComponentModel;
using Microsoft.SharePoint;
using System.Collections.Generic;
using System.Xml;

namespace NoCaml
{

    public abstract class SimpleWebPartDefinition<T> : WebPartDefinition<T> where T : WebPart, new()
    {
        private string _ComponentName;
        private string _ComponentDescription;
        private string _Zone;
        private string[] _FilterTags;


        public SimpleWebPartDefinition(string componentName, string componentDescription, string zone, params string[] filterTags)
        {
            _ComponentName = componentName;
            _ComponentDescription = componentDescription;
            _Zone = zone != null ? zone.ToString() : base.Zone;
            _FilterTags = filterTags ?? base.FilterTags;
        }





        public override string ComponentName
        {
            get { return _ComponentName; }
        }

        public override string ComponentDescription
        {
            get { return _ComponentDescription; }
        }

        public override string Zone
        {
            get
            {
                return _Zone;
            }
        }

        public override string[] FilterTags
        {
            get
            {
                return _FilterTags;
            }
        }


    }
}
