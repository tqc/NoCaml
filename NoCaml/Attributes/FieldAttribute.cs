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
using System.Collections.Generic;

namespace NoCaml
{
    [AttributeUsage(AttributeTargets.Property)]
    public class FieldAttribute : Attribute
    {
        public string DisplayName { get; set; }
        public string InternalName { get; set; }
        
        public SPFieldType Type { get; set; }


        public string[] Choices { get; set; }
        public string DefaultValue { get; set; }
        public bool AllowFillInChoices { get; set; }
        public SPChoiceFormatType EditType { get; set; }

        public bool AddToDefaultView { get; set; }

        public bool IsRequired { get; set; }
        public bool AutoMap { get; set; }

        /// <summary>
        /// Site relative url of the list (eg "/subsite"). blank to use current web.
        /// </summary>
        public string LookupWeb { get; set; }
        
        /// <summary>
        /// Web relative url of the lookup list (eg "/Lists/ListName")
        /// </summary>
        public string LookupList { get; set; }

        public bool IsMultiValue { get; set; }


        public FieldAttribute()
        {
            AutoMap = true;
//            Type = SPFieldType.Text;
        }

        /// <summary>
        /// Set fields to defaults based on the property name and type if
        /// not already set explicitly
        /// </summary>
        /// <param name="p"></param>
        public void UpdateWithDefaults(PropertyInfo p)
        {
            if (string.IsNullOrEmpty(DisplayName))
            {
                DisplayName = p.Name.Replace("_", " ");
            }

            if (string.IsNullOrEmpty(InternalName))
            {
                InternalName = p.Name;
            }

            if (Type == SPFieldType.Invalid)
            {
                if (p.PropertyType == typeof(string))
                {
                    Type = SPFieldType.Text;
                }
                else if (p.PropertyType == typeof(int))
                {
                    Type = SPFieldType.Number;
                }
                else if (p.PropertyType == typeof(double))
                {
                    Type = SPFieldType.Number;
                }
                else if (p.PropertyType == typeof(Uri))
                {
                    Type = SPFieldType.Text;
                }
                else if (p.PropertyType == typeof(List<string>))
                {
                    Type = SPFieldType.Choice;
                }
                else if (p.PropertyType == typeof(string[]))
                {
                    Type = SPFieldType.Choice;
                }
                else if (p.PropertyType == typeof(DateTime))
                {
                    Type = SPFieldType.DateTime;
                }
                else if (p.PropertyType == typeof(DateTime?))
                {
                    Type = SPFieldType.DateTime;
                }
                else if (p.PropertyType.IsEnum)
                {
                    Type = SPFieldType.Choice;
                    AllowFillInChoices = false;
                }
                else if (p.PropertyType == typeof(bool))
                {
                    Type = SPFieldType.Boolean;
                }
            }

            if ((Type == SPFieldType.Choice || Type == SPFieldType.MultiChoice) && p.PropertyType.IsEnum)
            {

                Choices = p.PropertyType.GetFields(BindingFlags.Static | BindingFlags.GetField | BindingFlags.Public)
                    .Select(f => f.GetCustomAttributes(typeof(ChoiceAttribute), true).Any() ? ((ChoiceAttribute)f.GetCustomAttributes(typeof(ChoiceAttribute), true).First()).DisplayName : f.Name.Replace("_", " "))
                    .ToArray();

            }



        }



    }
}
