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
using System.Reflection;
using System.Collections.Generic;
using Microsoft.SharePoint;
using Microsoft.SharePoint.Publishing;
using System.IO;


namespace NoCaml
{
    public static class ComponentManager
    {
        private static List<WebPartDefinition> WebPartDefinitions { get; set; }
        private static List<PageDefinition> PageDefinitions { get; set; }
        private static List<SectionDefinition> SectionDefinitions { get; set; }

        private static void EnsureItemsLoaded(Assembly componentAssembly)
        {

            // todo: fix the concurrency issues

            if (WebPartDefinitions == null || PageDefinitions == null || SectionDefinitions == null)
            {
                WebPartDefinitions = new List<WebPartDefinition>();
                PageDefinitions = new List<PageDefinition>();
                SectionDefinitions = new List<SectionDefinition>();

                foreach (var t in componentAssembly.GetTypes())
                {
                    if (t.IsSubclassOf(typeof(WebPartDefinition)) && !t.IsAbstract)
                    {
                        WebPartDefinitions.Add((WebPartDefinition)t.GetConstructor(new Type[] { }).Invoke(new object[] { }));
                    }
                    if (t.IsSubclassOf(typeof(PageDefinition)) && !t.IsAbstract)
                    {
                        PageDefinitions.Add((PageDefinition)t.GetConstructor(new Type[] { }).Invoke(new object[] { }));
                    }
                    if (t.IsSubclassOf(typeof(SectionDefinition)) && !t.IsAbstract)
                    {
                        SectionDefinitions.Add((SectionDefinition)t.GetConstructor(new Type[] { }).Invoke(new object[] { }));
                    }
                }
            }



        }

        public static IEnumerable<WebPartDefinition> GetWebPartsForTags(Assembly componentAssembly, string[] tags)
        {
            EnsureItemsLoaded(componentAssembly);
            return WebPartDefinitions.Where(wpd => (tags.Intersect(wpd.FilterTags).Any() || !tags.Any() || !wpd.FilterTags.Any()) && !wpd.Exists);
        }

        public static WebPartDefinition GetWebPartDefinition(Assembly componentAssembly, string id)
        {
            EnsureItemsLoaded(componentAssembly);
            return WebPartDefinitions.Where(wpd => wpd.ID == id).FirstOrDefault();
        }

        public static IEnumerable<PageDefinition> GetPagesForTags(Assembly componentAssembly, string[] tags)
        {
            EnsureItemsLoaded(componentAssembly);
            return PageDefinitions.Where(wpd => (tags.Intersect(wpd.FilterTags).Any() || !tags.Any() || !wpd.FilterTags.Any()) && !wpd.Exists);
        }

        public static PageDefinition GetPageDefinition(Assembly componentAssembly, string id)
        {
            EnsureItemsLoaded(componentAssembly);
            return PageDefinitions.Where(wpd => wpd.ID == id).FirstOrDefault();
        }

        public static IEnumerable<SectionDefinition> GetSectionsForTags(Assembly componentAssembly, string[] tags)
        {
            EnsureItemsLoaded(componentAssembly);
            return SectionDefinitions.Where(wpd => (tags.Intersect(wpd.FilterTags).Any() || !tags.Any() || !wpd.FilterTags.Any()) && !wpd.Exists);
        }

        public static SectionDefinition GetSectionDefinition(Assembly componentAssembly, string id)
        {
            EnsureItemsLoaded(componentAssembly);
            return SectionDefinitions.Where(wpd => wpd.ID == id).FirstOrDefault();
        }



  

    }
}
