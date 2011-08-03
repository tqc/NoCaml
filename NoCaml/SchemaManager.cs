using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using System.Reflection;
using Microsoft.Office.Server.Audience;

namespace NoCaml
{
    /// <summary>
    /// Contains methods for creating and updating lists and content types
    /// </summary>
    public static class SchemaManager
    {

        // From http://stackoverflow.com/questions/1460332/retrieving-a-types-leaf-interfaces
        public static Type[] GetLeafInterfaces(this Type type)
        {
            return type.FindInterfaces((candidateIfc, allIfcs) =>
            {
                foreach (Type ifc in (Type[])allIfcs)
                {
                    if (candidateIfc != ifc && candidateIfc.IsAssignableFrom(ifc))
                        return false;
                }
                return true;
            }
            , type.GetInterfaces());
        }

        /// <summary>
        /// Get the field attribute for a property. Any required attribute properties not explicitly 
        /// set will be added to the returned object.
        /// </summary>
        /// <param name="p"></param>
        /// <param name="includeInherited"></param>
        /// <param name="throwIfMissing"></param>
        /// <returns></returns>
        public static FieldAttribute GetFieldAttribute(PropertyInfo p, bool includeInherited, bool throwIfMissing)
        {
            var pa = p.GetCustomAttributes(typeof(FieldAttribute), includeInherited).FirstOrDefault() as FieldAttribute;

            // inherit parameter doesn't handle interfaces
            if (pa == null && includeInherited)
            {
                foreach (var cti in p.DeclaringType.GetInterfaces())
                {
                    var cta = SchemaManager.GetContentTypeAttribute(cti, false, false);
                    if (cta == null) continue;

                    var p2 = cti.GetProperty(p.Name);
                    if (p2 != null)
                    {
                        pa = GetFieldAttribute(p2, true, false);
                        if (pa != null) break;
                    }


                }
            }


            if (pa == null)
            {
                if (throwIfMissing)
                {
                    throw new Exception("Field attribute not found for " + p.Name);
                }
            }
            else
            {
                pa.UpdateWithDefaults(p);
            }
            return pa;
        }

        public static ListAttribute GetListAttribute(Type listType)
        {
            var la = listType.GetCustomAttributes(typeof(ListAttribute), true).FirstOrDefault() as ListAttribute;
            if (la == null)
            {
                throw new Exception("List class must have List attribute");
            }
            la.UpdateWithDefaults(listType);
            return la;
        }

        public static ViewAttribute[] GetViewAttributes(Type listType)
        {
            return listType.GetCustomAttributes(typeof(ViewAttribute), true).OfType<ViewAttribute>().ToArray();
        }

        public static ContentTypeAttribute GetContentTypeAttribute(Type p, bool includeInherited, bool throwIfMissing)
        {
            var cta = p.GetCustomAttributes(typeof(ContentTypeAttribute), includeInherited).FirstOrDefault() as ContentTypeAttribute;
            if (cta == null)
            {
                if (throwIfMissing)
                {
                    throw new Exception("ContentType attribute not found for " + p.Name);
                }
            }
            else
            {
                // cta.UpdateWithDefaults(p);
            }
            return cta;
        }

        public static SPContentType GetContentType(SPWeb web, Type t, ContentTypeAttribute cta)
        {

            foreach (SPContentType c in web.ContentTypes)
            {
                if (c.Name == t.Name.Replace("_", " "))
                {
                    if (cta != null && !string.IsNullOrEmpty(cta.ContentTypeID) && !c.Id.ToString().Equals(cta.ContentTypeID, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new Exception("Content type " + c.Name + " exists with wrong content type id " + c.Id);
                    }
                    return c;
                }
            }
            // if the content type is not found, it may exist in the parent site.
            // using web.AvailableContentTypes would find inherited content types, but that 
            // is a read only collection.
            if (!web.IsRootWeb)
            {
                return GetContentType(web.ParentWeb, t, cta);
            }
            return null;
        }

        public static void UpdateContentType(SPWeb web1, Type t, ContentTypeAttribute cta)
        {
            using (var web = web1.Site.OpenWeb())
            {
                web.AllowUnsafeUpdates = true;
                // first ensure all the site columns exist
                foreach (var p in t.GetProperties())
                {
                    var pa = SchemaManager.GetFieldAttribute(p, true, false);
                    if (pa == null) continue;

                    if (!web.AvailableFields.ContainsField(pa.InternalName) && !web.AvailableFields.ContainsField(pa.DisplayName))
                    {
                        // todo: use parameters


                        web.Fields.Add(pa.InternalName, pa.Type, pa.IsRequired);

                        // update display name
                        var f = web.Fields.GetFieldByInternalName(pa.InternalName);
                        f.Title = pa.DisplayName;


                        UpdateFieldSettings(f, pa, p);


                    }
                }
                web.Update();

                var c = GetContentType(web, t, cta);

                if (c == null)
                {

                    if (!string.IsNullOrEmpty(cta.ContentTypeID))
                    {
                        throw new Exception("Content type " + t.Name + " does not exist and cannot be created with specified ID");
                    }

                    var parentType = GetContentType(web, t.GetLeafInterfaces()[0], null);

                    // create
                    c = new SPContentType(parentType, web.ContentTypes, t.Name.Replace("_", " "));

                    web.ContentTypes.Add(c);
                }

                // add all fields from parent content type
                var interfaces = t.GetLeafInterfaces();
                if (interfaces.Length > 0)
                {
                    var parentType = GetContentType(web, interfaces[0], null);

                    foreach (SPField pf in parentType.Fields)
                    {
                        if (c.FieldLinks.OfType<SPFieldLink>().Where(fl => fl.Id == pf.Id).Any())
                        {
                            // property already linked
                            continue;
                        }
                        else
                        {
                            c.FieldLinks.Add(new SPFieldLink(pf));
                        }
                    }
                }
                // add fields defined in class
                foreach (var p in t.GetProperties())
                {
                    var pa = SchemaManager.GetFieldAttribute(p, true, false);
                    if (pa == null) continue;



                    var f = web.AvailableFields.GetFieldByInternalName(pa.InternalName);

                    if (c.FieldLinks.OfType<SPFieldLink>().Where(fl => fl.Id == f.Id).Any())
                    {
                        // property already linked
                        continue;
                    }
                    else
                    {
                        c.FieldLinks.Add(new SPFieldLink(f));
                    }
                }


                c.Update(true, false);

            }
        }

        private static void AddLookup(SPList list, FieldAttribute pa, PropertyInfo p)
        {

            Guid lookupWebId;
            Guid lookupListId;


            if (string.IsNullOrEmpty(pa.LookupWeb))
            {
                var web = list.ParentWeb;
                lookupWebId = web.ID;
                lookupListId = web.GetList(web.ServerRelativeUrl + "/" + pa.LookupList).ID;

            }
            else if (pa.LookupWeb == "/")
            {
                var web = list.ParentWeb.Site.RootWeb;
                lookupWebId = web.ID;
                lookupListId = web.GetList(web.ServerRelativeUrl + "/" + pa.LookupList).ID;


            }
            else
            {
                using (SPWeb web = list.ParentWeb.Site.OpenWeb(pa.LookupWeb))
                {
                    lookupWebId = web.ID;
                    lookupListId = web.GetList(web.ServerRelativeUrl + "/" + pa.LookupList).ID;

                }
            }
            list.Fields.AddLookup(pa.InternalName, lookupListId, lookupWebId, pa.IsRequired);

        }

        private static void UpdateFieldSettings(SPField ff, FieldAttribute pa, PropertyInfo p)
        {

            ff.Hidden = false;
            ff.Required = pa.IsRequired;

            ff.ShowInListSettings = true;


            // TODO: add other field types.

            // TODO: handle case where choice field already exists as a text field

            // TODO: this may result in duplicate choices

            if (pa.Type == SPFieldType.Choice)
            {
                var f = ff as SPFieldChoice;
                if (pa.Choices != null) f.Choices.AddRange(pa.Choices);
                f.DefaultValue = pa.DefaultValue;
                f.EditFormat = pa.EditType;
                f.FillInChoice = pa.AllowFillInChoices;


            }


            else if (pa.Type == SPFieldType.MultiChoice)
            {
                var f = ff as SPFieldMultiChoice;
                if (pa.Choices != null) f.Choices.AddRange(pa.Choices);
                f.DefaultValue = pa.DefaultValue;

                f.FillInChoice = pa.AllowFillInChoices;

            }
            else if (pa.Type == SPFieldType.Number)
            {
                var f = ff as SPFieldNumber;
                // for some reason SPFieldType.Integer always creates hidden fields, so we have to use
                // SPFieldType.Number instead.
                if (p.PropertyType == typeof(int) || p.PropertyType == typeof(int?))
                {
                    f.DisplayFormat = SPNumberFormatTypes.NoDecimal;
                }

            }
            else if (pa.Type == SPFieldType.Lookup)
            {
                var f = ff as SPFieldLookup;
                //if (string.IsNullOrEmpty(pa.LookupWeb))
                //{
                //    f.LookupWebId = f.ParentList.ParentWeb.ID;
                //    f.LookupList = f.ParentList.ParentWeb.GetList(pa.LookupList).ID.ToString();
                //}
                //else if (pa.LookupWeb == "/")
                //{
                //    f.LookupWebId = f.ParentList.ParentWeb.Site.RootWeb.ID;
                //    f.LookupList = f.ParentList.ParentWeb.Site.RootWeb.GetList(pa.LookupList).ID.ToString();

                //}
                //else
                //{
                //    using (SPWeb web = f.ParentList.ParentWeb.Site.OpenWeb(pa.LookupWeb))
                //    {
                //        f.LookupWebId = web.ID;
                //        f.LookupList = web.GetList(pa.LookupList).ID.ToString();

                //    }
                //}

                f.AllowMultipleValues = pa.IsMultiValue;


            }



            ff.Update();

        }

        public static void UpdateContentTypes(SPWeb web)
        {
            // find all content type interfaces

            foreach (var t in Assembly.GetCallingAssembly().GetTypes())
            {
                var cta = GetContentTypeAttribute(t, false, false);
                if (t.IsInterface && cta != null)
                {
                    UpdateContentType(web, t, cta);
                }

            }

        }

        public static void UpdateContentType(SPWeb web, Type t)
        {

            var cta = GetContentTypeAttribute(t, false, false);
            if (t.IsInterface && cta != null)
            {
                UpdateContentType(web, t, cta);
            }

        }


        public static SPList GetListIfExists(SPWeb web, string displayname)
        {
            return web.Lists.OfType<SPList>().Where(l => l.Title == displayname).FirstOrDefault();
        }

        private static void EnsureListExists(SPWeb web, Type listType, ListAttribute la)
        {

            // get list for field update if it already exists
            var list = GetListIfExists(web, la.DisplayName);

            // create list if new
            if (list == null)
            {

                web.Lists.Add(la.DisplayName, la.Description, la.WebRelativeUrl, null, (int)la.Type, "100");
                list = web.Lists[la.DisplayName];
            }

            // assign content types if necessary
            foreach (var ict in listType.GetLeafInterfaces())
            {
                var cta = GetContentTypeAttribute(ict, false, false);
                if (cta == null) continue;

                var pct = GetContentType(web, ict, cta);

                if (!list.ContentTypes.OfType<SPContentType>().Where(ct1 => ct1.Name == pct.Name).Any())
                {
                    list.ContentTypes.Add(pct);
                }

            }

            // update fields, assigning non-inherited fields to all content types
            foreach (var p in listType.GetProperties())
            {
                var pad = SchemaManager.GetFieldAttribute(p, false, false);
                var pa = pad ?? SchemaManager.GetFieldAttribute(p, true, false);

                if (pa == null) continue;


                if (!list.Fields.ContainsField(pa.InternalName) && !list.Fields.ContainsField(pa.DisplayName))
                {
                    // if from content type, add site column
                    if (pad == null)
                    {
                        list.Fields.Add(web.AvailableFields[pa.DisplayName]);
                    }
                    else
                    {
                        if (pa.Type == SPFieldType.Lookup)
                        {
                            AddLookup(list, pa, p);
                        }
                        else
                        {

                            list.Fields.Add(pa.InternalName, pa.Type, pa.IsRequired);
                        }

                        var ff = list.Fields.GetFieldByInternalName(pa.InternalName);

                        ff.Title = pa.DisplayName;

                        UpdateFieldSettings(ff, pa, p);

                        if (pa.AddToDefaultView)
                        {
                            var view = list.DefaultView;
                            view.ViewFields.Add(pa.DisplayName);
                            view.Update();
                        }

                    }
                }
            }

            // add/update views

            var existingviews = list.Views.OfType<SPView>();

            foreach (var va in GetViewAttributes(listType))
            {
                var spv = existingviews.Where(v => v.Title == va.ViewName).FirstOrDefault();
                if (spv == null)
                {
                    var sc = new System.Collections.Specialized.StringCollection();
                    sc.AddRange(va.Fields);
                    spv = list.Views.Add(va.ViewName, sc, "", 0, true, va.IsDefault, SPViewCollection.SPViewType.Html, false);
                }
                else
                {
                    var evf = spv.ViewFields.OfType<string>().ToArray();

                    foreach (var fn in va.Fields)
                    {
                        if (!evf.Contains(fn))
                        {
                            spv.ViewFields.Add(fn);
                        }
                    }
                }
                spv.Update();
            }

            // add web parts to edit forms if necessary

            if (la.NewFormWebParts != null)
            {
                foreach (var wpdt in la.NewFormWebParts)
                {
                    var wpd = (WebPartDefinition)wpdt.GetConstructor(new Type[] { }).Invoke(new object[] { });
                    wpd.AddToPage(web, list.Forms[PAGETYPE.PAGE_NEWFORM].Url, false);
                }
            }
            if (la.EditFormWebParts != null)
            {
                foreach (var wpdt in la.EditFormWebParts)
                {
                    var wpd = (WebPartDefinition)wpdt.GetConstructor(new Type[] { }).Invoke(new object[] { });
                    wpd.AddToPage(web, list.Forms[PAGETYPE.PAGE_EDITFORM].Url, false);

                }
            }


        }

        public static void EnsureListExists(SPWeb web, Type listType)
        {
            var la = GetListAttribute(listType);


            // get correct web for list
            if (string.IsNullOrEmpty(la.SiteRelativeWebUrl))
            {
                EnsureListExists(web, listType, la);
            }
            else if (la.SiteRelativeWebUrl == "/")
            {
                EnsureListExists(web.Site.RootWeb, listType, la);
            }
            else
            {
                using (var web2 = web.Site.OpenWeb(la.SiteRelativeWebUrl))
                {
                    EnsureListExists(web2, listType, la);
                }
            }



        }
        public static IEnumerable<T> Query<T>(SPWeb web, string field, string value) where T : ListDefinition
        {
            return Query<T>(web, field, value, false);
        }
        public static IEnumerable<T> Query<T>(SPWeb web, string field, string value, bool filterByAudience) where T : ListDefinition
        {
            return Query<T>(web, "<Where><Eq><FieldRef Name=\"" + field + "\" /><Value Type=\"Text\">" + value + "</Value></Eq></Where>", filterByAudience);
        }
        public static IEnumerable<T> Query<T>(SPList list, string q) where T : ListDefinition
        {
            return Query<T>(list, q, false);
        }

        public static IEnumerable<T> ApplyAudienceFilter<T>(this IEnumerable<T> allitems) where T : IAudienced
        {
            if (SPContext.Current == null
                || SPContext.Current.Web == null
                || SPContext.Current.Web.CurrentUser == null)
            {
                foreach (var t in allitems)
                {
                    yield return t;
                }
            }

            var audManager = new AudienceManager();

            foreach (var t in allitems)
            {
                if (string.IsNullOrEmpty(t.TargetAudiences)
                    || AudienceManager.IsCurrentUserInAudienceOf(t.TargetAudiences, true)
                    )
                {
                    yield return t;
                }
            }
        }

        public static IEnumerable<T> Query<T>(SPList list, string q, bool filterByAudience) where T : ListDefinition
        {
            var spq = new SPQuery()
            {
                Query = q
            };
            var splic = list.GetItems(spq);
            var ctor = typeof(T).GetConstructor(new Type[] { typeof(SPListItem) });
            if (ctor == null)
            {
                throw new Exception("Constructor " + typeof(T).Name + "(SPListItem) not found");
            }

            filterByAudience &=
                SPContext.Current != null
                && SPContext.Current.Web != null
                && SPContext.Current.Web.CurrentUser != null
                && list.Fields.ContainsField("Target Audiences");
            if (filterByAudience)
            {
                return splic.OfType<SPListItem>()
                    .Where(spli => string.IsNullOrEmpty((string)spli["Target Audiences"])
                    || AudienceManager.IsCurrentUserInAudienceOf((string)spli["Target Audiences"], false)
                    )
    .Select(spli => (T)ctor.Invoke(new object[] { spli })).ToArray();
            }
            else
            {
                return splic.OfType<SPListItem>()
                    .Select(spli => (T)ctor.Invoke(new object[] { spli })).ToArray();
            }
        }

        public static IEnumerable<T> Query<T>(SPWeb web, string q) where T : ListDefinition
        {
            return Query<T>(web, q, false);
        }
        public static IEnumerable<T> Query<T>(SPWeb web, string q, bool filterByAudience) where T : ListDefinition
        {
            var la = GetListAttribute(typeof(T));

            // get correct web for list
            if (string.IsNullOrEmpty(la.SiteRelativeWebUrl))
            {
                return Query<T>(web.Lists[la.DisplayName], q, filterByAudience);
            }
            else if (la.SiteRelativeWebUrl == "/")
            {
                return Query<T>(web.Site.RootWeb.Lists[la.DisplayName], q, filterByAudience);
            }
            else
            {
                using (var web2 = web.Site.OpenWeb(la.SiteRelativeWebUrl))
                {
                    return Query<T>(web2.Lists[la.DisplayName], q, filterByAudience);
                }
            }


        }

        private static T ListItemToObject<T>(SPListItem item) where T : ListDefinition
        {
            return (T)typeof(T).GetConstructor(new Type[] { typeof(SPListItem) }).Invoke(new object[] { item });
        }
        public static T GetById<T>(SPList list, int id) where T : ListDefinition
        {
            return ListItemToObject<T>(list.GetItemById(id));
        }
        public static T GetById<T>(SPWeb web, int id) where T : ListDefinition
        {
            var la = GetListAttribute(typeof(T));

            // get correct web for list
            if (string.IsNullOrEmpty(la.SiteRelativeWebUrl))
            {
                return GetById<T>(web.Lists[la.DisplayName], id);
            }
            else if (la.SiteRelativeWebUrl == "/")
            {
                return GetById<T>(web.Site.RootWeb.Lists[la.DisplayName], id);
            }
            else
            {
                using (var web2 = web.Site.OpenWeb(la.SiteRelativeWebUrl))
                {
                    return GetById<T>(web2.Lists[la.DisplayName], id);
                }
            }


        }

    }
}
