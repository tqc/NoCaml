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
using System.Reflection;
using System.Text.RegularExpressions;
using System.IO;

namespace NoCaml
{

    public abstract class BaseDefinition
    {
        /// <summary>
        /// Tags that will be used to determine which pages the component can be added from
        /// </summary>
        public virtual string[] FilterTags
        {
            get
            {
                return new string[] { };

            }
        }

        public string ID { get { return this.GetType().FullName; } }

        /// <summary>
        /// Name that will be displayed in the add dialog
        /// </summary>
        public abstract string ComponentName
        {
            get;
        }

        /// <summary>
        /// Description that will be displayed in the add dialog
        /// </summary>
        public abstract string ComponentDescription
        {
            get;
        }

        /// <summary>
        /// True if the component already exists. Usually used to avoid creation of existing components.
        /// </summary>
        public abstract bool Exists { get; }


        /// <summary>
        /// Get content from embedded resource files associated with the current type
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> FileContent
        {
            get
            {
                if (_FileContent == null)
                {
                    _FileContent = new Dictionary<string, string>();
                    var t = this.GetType();
                    var a = t.Assembly;
                    foreach (var n in a.GetManifestResourceNames())
                    {
                        var m = Regex.Match(n, t.FullName + "\\.([^\\.]+)\\.html");
                        if (m.Groups.Count > 1)
                        {
                            var pn = m.Groups[1].Value;

                            var fs = a.GetManifestResourceStream(n);
                            var sr = new StreamReader(fs);
                            var fc = sr.ReadToEnd();
                            sr.Close();
                            _FileContent[pn] = fc;
                        }

                    }
                }
                return _FileContent;
            }
        }
        private Dictionary<string, string> _FileContent;



    }

}
