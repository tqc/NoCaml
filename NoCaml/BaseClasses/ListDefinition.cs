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
using System.Reflection;
using NoCaml.ContentTypes;
using System.Collections.Generic;

namespace NoCaml
{
    [Serializable]
    public abstract class ListDefinition
    {

        public int? ID { get; set; }
        public DateTime Created { get; private set; }

        protected ListDefinition()
        {
            Created = DateTime.Now;
        }

        public static object ParseEnum(Type t, string val)
        {
            if (string.IsNullOrEmpty(val)) return null;

            if (Enum.IsDefined(t, val.Replace(" ", "_"))) return Enum.Parse(t, val.Replace(" ", "_"), true);
            
                // standard parse didn't work - look for attributes
                foreach (var f in t.GetFields(BindingFlags.Public| BindingFlags.Static| BindingFlags.GetField)) {
                    var a = f.GetCustomAttributes(typeof(ChoiceAttribute), true).OfType<ChoiceAttribute>().FirstOrDefault();
                    if (a != null && a.DisplayName.Equals(val, StringComparison.CurrentCultureIgnoreCase)) return f.GetValue(null);
                }
                return null;
        }

        public static string DisplayEnumValue(Enum val)
        {
            var t = val.GetType();
            var f = t.GetField(val.ToString());
            if (f == null) return val.ToString().Replace("_", " ");
            var a = f.GetCustomAttributes(typeof(ChoiceAttribute), true).OfType<ChoiceAttribute>().FirstOrDefault();
            if (a == null) return val.ToString().Replace("_", " ");
            return a.DisplayName;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="item"></param>
        protected virtual void LoadProperties(SPListItem item)
        {
            ID = item.ID;
            Created = (DateTime)item["Created"];

            foreach (var p in this.GetType().GetProperties())
            {
                var pa = SchemaManager.GetFieldAttribute(p, true, false);
                if (pa == null) continue;
                if (!pa.AutoMap) continue;

                if (pa.Type == SPFieldType.Choice)
                {  
                    var v = (string)item[pa.DisplayName];
                    if (p.PropertyType == typeof(string))
                    {
                        p.SetValue(this, v, null);
                    }
                    else if (p.PropertyType.IsEnum)
                    {
                      
                        if (!string.IsNullOrEmpty(v))
                        {
                            var vs = ParseEnum(p.PropertyType, v);
                            p.SetValue(this, vs , null);
                        }
                    }
                }
                else if (pa.Type == SPFieldType.MultiChoice)
                {
                    var v = (string)item[pa.DisplayName] ?? "";
                    if (p.PropertyType == typeof(string))
                    {
                        p.SetValue(this, v, null);
                    }
                    else if (p.PropertyType == typeof(List<string>))
                    {
                        var ss = v.Split(new string[] { ";#" }, StringSplitOptions.RemoveEmptyEntries);
                        p.SetValue(this, ss.ToList(), null);
                    }
                    else if (p.PropertyType == typeof(string[]))
                    {
                        var ss = v.Split(new string[] { ";#" }, StringSplitOptions.RemoveEmptyEntries);
                        p.SetValue(this, ss, null);
                    }
                    else if (p.PropertyType.IsEnum)
                    {
                      
                        if (!string.IsNullOrEmpty(v))
                        {
                            throw new NotImplementedException("Getting a list from flags is complicated and not used yet");
                            //   p.SetValue(this, Enum.Parse(p.PropertyType, v, true), null);
                        }
                    }
                }
                else if (pa.Type == SPFieldType.Text && p.PropertyType == typeof(Uri))
                {

                    var s = (string)item[pa.DisplayName];
                    var uri = string.IsNullOrEmpty(s) ? null : new Uri(s);
                    p.SetValue(this, uri, null);
                     
                }
                else if (pa.Type == SPFieldType.User && p.PropertyType == typeof(string))
                {
                    
                    var s = (string)item[pa.DisplayName];
                    if (!string.IsNullOrEmpty(s)) { 
                    var spfuv = new SPFieldUserValue(item.Web, s);
                    p.SetValue(this, spfuv.User.LoginName, null);
                }

                }
                else if (p.PropertyType == typeof(int))
                {                    
                    p.SetValue(this, Convert.ToInt32(item[pa.DisplayName]), null);
                }
                else
                {
                    // todo: if this doesn't work, need to add more field types.
                    p.SetValue(this, item[pa.DisplayName], null);
                }

            }


        }

        protected virtual void SaveProperties(SPListItem item)
        {
            foreach (var p in this.GetType().GetProperties())
            {
                var pa = SchemaManager.GetFieldAttribute(p, true, false);
                if (pa == null) continue;
                if (!pa.AutoMap) continue;

                if (pa.Type == SPFieldType.Choice)
                {
                    if (p.PropertyType == typeof(string))
                    {
                        item[pa.DisplayName] = p.GetValue(this, null);
                    }
                    else if (p.PropertyType.IsEnum)
                    {   
                        item[pa.DisplayName] = DisplayEnumValue((Enum)p.GetValue(this, null));
                    }

                }
                else if (pa.Type == SPFieldType.MultiChoice)
                {
                    if (p.PropertyType == typeof(string))
                    {
                        item[pa.DisplayName] = p.GetValue(this, null);
                    }
                    else if (p.PropertyType == typeof(List<string>))
                    {
                        var v = string.Join(";#", ((List<string>)p.GetValue(this, null)).ToArray());
                        p.SetValue(this,v, null);
                    }
                    else if (p.PropertyType == typeof(string[]))
                    {
                        var v = string.Join(";#", (string[])p.GetValue(this, null));
                        p.SetValue(this, v, null);
                    }
                    else if (p.PropertyType.IsEnum)
                    {
                        throw new NotImplementedException("Getting a list from flags is complicated and not used yet");
                      //  item[pa.DisplayName] = p.GetValue(this, null);
                    }
                }
                else if (pa.Type == SPFieldType.Text && p.PropertyType == typeof(Uri))
                {
                    var uri = (Uri)p.GetValue(this, null);
                    item[pa.DisplayName] = uri == null ? null : uri.AbsoluteUri;
                }
                else if (pa.Type == SPFieldType.User && p.PropertyType == typeof(string))
                {                    
                    var un = (string)p.GetValue(this, null);
                    var u = item.Web.EnsureUser(un);

                    item[pa.DisplayName] = new SPFieldUserValue(item.Web, u.ID, un);
                }
                else
                {
                    // todo: if this doesn't work, need to add more field types.
                    item[pa.DisplayName] = p.GetValue(this, null);
                }

            }
        }

        public virtual void Save(SPList list)
        {
            if (ID == null)
            {
                // new item
                var item = list.Items.Add();
                SaveProperties(item);
                item.Update();
                ID = item.ID;
            }
            else
            {
                var item = list.GetItemById(ID.Value);
                SaveProperties(item);
                item.Update();
            }
        }

        public virtual void Save(SPWeb web)
        {
            var la = SchemaManager.GetListAttribute(this.GetType());
         
            // Get correct web

            if (string.IsNullOrEmpty(la.SiteRelativeWebUrl))
            {
                Save (web.Lists[la.DisplayName]);
            }
            else if (la.SiteRelativeWebUrl == "/")
            {
                Save(web.Site.RootWeb.Lists[la.DisplayName]);
            }
            else
            {
                using (var web2 = web.Site.OpenWeb(la.SiteRelativeWebUrl))
                {
                    Save(web2.Lists[la.DisplayName]);
                }
            }
        }

        public virtual void Delete(SPList list)
        {
            if (ID == null)
            {
                // nothing to delete
            }
            else
            {
                list.Items.DeleteItemById(ID.Value);
            }
        }

        public void Delete(SPWeb web)
        {
            var la = SchemaManager.GetListAttribute(this.GetType());

            // Get correct web

            if (string.IsNullOrEmpty(la.SiteRelativeWebUrl))
            {
                Delete(web.Lists[la.DisplayName]);
            }
            else if (la.SiteRelativeWebUrl == "/")
            {
                Delete(web.Site.RootWeb.Lists[la.DisplayName]);
            }
            else
            {
                using (var web2 = web.Site.OpenWeb(la.SiteRelativeWebUrl))
                {
                    Delete(web2.Lists[la.DisplayName]);
                }
            }
        }


    }
}
