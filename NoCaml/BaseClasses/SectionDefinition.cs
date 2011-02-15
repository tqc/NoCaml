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

namespace NoCaml
{

    public abstract class SectionDefinition : BaseDefinition
    {
  

        public abstract List<PageDefinition> Pages { get; }

        /// <summary>
        /// Title of the site. Default is component name
        /// </summary>
        public virtual string Title
        {
            get
            {
                return ComponentName;
            }
        }

        /// <summary>
        /// Url for the new sute. Default is Title with spaces removed
        /// </summary>
        public string Url { get { return Title.Replace(" ", ""); } }

        public override bool Exists
        {
            get { return false; }
        }

        public void CreateSite(SPWeb parentWeb)
        {
            // todo: create site, enable publishing, create pages

            var newWeb = parentWeb.Webs.Add(Url);

            newWeb.Title = ComponentName;
            newWeb.Description = ComponentDescription;
            
            newWeb.Features.Add(new Guid("22A9EF51-737B-4ff2-9346-694633FE4416"));
            newWeb.Navigation.UseShared = true;
            
            newWeb.Update();


            foreach (var pd in Pages)
            {
                pd.CreatePage(newWeb);
            }



        }


    }
 
}
