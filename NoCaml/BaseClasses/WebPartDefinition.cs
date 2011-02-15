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

    public abstract class WebPartDefinition : BaseDefinition
    {
        public virtual string Zone
        {
            get
            {
                return "Default";
            }
        }

        /// <summary>
        /// Title of the web part. default is component name
        /// </summary>
        public virtual string Title
        {
            get
            {
                return ComponentName;
            }
        }


        public override bool Exists
        {
            get { return false; }
        }

        public virtual List<Type> RequiredLists
        {
            get
            {
                return new List<Type>();

            }

        }

        public abstract void AddToPage(SPWeb web, string pageUrl, bool allowDuplicates);
      

    }


    public abstract class WebPartDefinition<T> : WebPartDefinition where T:WebPart, new()
    {

/// <summary>
        /// Get a new instance of the web part. This can be overridden if the web part needs to use
        /// a non-default constructor.
        /// </summary>
        /// <returns></returns>
        public virtual T GetWebPart()
        {
            return new T();
        }

        public void SetStandardProperties(T wp)
        {
            wp.Title = Title;
        }


        public virtual void SetCustomProperties(T wp)
        {

        }

        private void SetFileProperties(T wp)
        {
            var t = wp.GetType();
            var pl = t.GetProperties();

            foreach (var k in FileContent.Keys)
            {
                var pi = pl.Where(p => p.Name == k).FirstOrDefault();
                if (pi == null)
                {
                    continue;
                }
                else if (pi.PropertyType == typeof(string))
                {
                    pi.SetValue(wp, FileContent[k], null);

                }
                else if (pi.PropertyType == typeof(XmlElement))
                {
                    XmlDocument doc = new XmlDocument();
                    var el = doc.CreateElement("div");
                    el.InnerText = FileContent[k];
                    pi.SetValue(wp, el, null);
                }

            }
        }

    
        public override void AddToPage(SPWeb web, string pageUrl, bool allowDuplicates)
        {
            foreach (var l in RequiredLists)
            {
                SchemaManager.EnsureListExists(web, l);
            }


            using (var wpm = web.GetLimitedWebPartManager(pageUrl, PersonalizationScope.Shared))
            {
                if (!allowDuplicates) {
                    foreach (WebPart wp in wpm.WebParts)
                    {
                        if (wp.GetType() == typeof(T)
                            && wp.Title == this.Title)
                        {
                            return;
                        }
                    }
                
            }
                using (var wp = GetWebPart())
                {
                    SetStandardProperties(wp);
                    SetFileProperties(wp);
                    SetCustomProperties(wp);
                    wpm.AddWebPart(wp, Zone, 0);
                }
            }
        }


    }
}
