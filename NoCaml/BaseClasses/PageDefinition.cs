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
using Microsoft.SharePoint.Publishing;
using System.Collections.Generic;

namespace NoCaml
{

    public abstract class PageDefinition : BaseDefinition
    {

        /// <summary>
        /// Tags that will be applied to the new page
        /// </summary>
        public virtual string[] PageTags
        {
            get
            {
                return new string[] { };

            }
        }

        public virtual List<WebPartDefinition> WebParts
        {
            get
            {
                return new List<WebPartDefinition>();
            }
        }
        /// <summary>
        /// If set to true, the page will be set as the welcome page of the site on creation.
        /// </summary>
        public virtual bool IsWelcomePage { get { return false; } }

        /// <summary>
        /// Title of the page. Default is component name.
        /// </summary>
        public virtual string Title
        {
            get
            {
                return ComponentName;
            }
        }

        /// <summary>
        /// FileName for the new page. Default is [Title with spaces removed].aspx 
        /// </summary>
        public virtual string FileName
        {
            get
            {
                return Title.Replace(" ", "") + ".aspx";
            }
        }

        public virtual string Layout
        {
            get
            {
                return "superportal_contentpage.aspx";
            }
        }

        public override bool Exists
        {
            get
            {
                var web = SPContext.Current.Web;

                var pubSite = PublishingWeb.GetPublishingWeb(web);
                var pages = pubSite.GetPublishingPages();

                foreach (var p in pages)
                {
                    if (p.Name == FileName) return true;
                }
                return false;
            }
        }

        public void Ensure(SPWeb web)
        {
            if (!Exists) CreatePage(web);
        }

        public void CreatePage(SPWeb web)
        {

            PublishingSite pubSiteCollection = new PublishingSite(web.Site);
            PublishingWeb pubSite = null;
            if (pubSiteCollection != null)
            {
                // Assign an object to the pubSite variable
                if (PublishingWeb.IsPublishingWeb(web))
                {
                    pubSite = PublishingWeb.GetPublishingWeb(web);
                }
            }
            // Search for the page layout for creating the new page
            PageLayout currentPageLayout = FindPageLayout(pubSiteCollection, Layout);
            // Check or the Page Layout could be found in the collection
            // if not (== null, return because the page has to be based on
            // an excisting Page Layout
            if (currentPageLayout == null)
            {
                return;
            }
            PublishingPageCollection pages = pubSite.GetPublishingPages();
            PublishingPage newPage = pages.Add(FileName, currentPageLayout);
            newPage.Title = Title;
            //newPage.Description = ComponentDescription;

            // Here you can set some properties like:
            newPage.IncludeInCurrentNavigation = true;
            newPage.IncludeInGlobalNavigation = true;


            // End of setting properties
            var item = newPage.ListItem;
            if (PageTags != null && PageTags.Any())
            {
                item["Tags"] = string.Join(";#", PageTags);
            }
            if (FileContent.ContainsKey("Content"))
            {
                item["PublishingPageContent"] = FileContent["Content"];
            }
            item.Update();
         

            newPage.Update();

            foreach (var wpd in WebParts)
            {
                wpd.AddToPage(web, item.File.Url, true);
            }

            if (IsWelcomePage)
            {
                pubSite.DefaultPage = item.File;
                pubSite.Update();

            }


            /*
            // Check the file in (a major version)
            publishFile.CheckIn("Initial", SPCheckinType.MajorCheckIn);
            publishFile.Publish("Initial");

            // In case of content approval, approve the file
            if (pubSite.PagesList.EnableModeration)
            {
                publishFile.Approve("Initial");
            }
            */



        }

        private PageLayout FindPageLayout(PublishingSite pubSiteCollection, string templateName)
        {
            PageLayoutCollection plCollection = pubSiteCollection.GetPageLayouts(true);
            foreach (Microsoft.SharePoint.Publishing.PageLayout layout in plCollection)
            {
                // String Comparison based on the Page Layout Name
                if (layout.Name.Equals(templateName, StringComparison.InvariantCultureIgnoreCase))
                {
                    return layout;
                }
            }
            return null;
        }

    }

}
