using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint;
using System.IO;

namespace NoCaml
{
    public static class LayoutManager
    {
        /// <summary>
        /// Overwrite a customized or manually created page layout with a file
        /// in the feature folder
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="layoutname"></param>
        public static void ForceLayoutPageUpdate(SPSite site, string featurefolder, string layoutfilename)
        {

            var web = site.RootWeb;


            // check that file exists

            // first check the destination file - if it isn't customized, no need
            // to overwrite


            var fileurl = "_catalogs/masterpage/" + layoutfilename;

            var f = web.GetFile(fileurl);

            if (!f.Exists)
            {
                // should create file through caml
                return;
            }


            else if (f.CustomizedPageStatus == SPCustomizedPageStatus.Customized)
            {
                // page was created from the feature - we can use standard reghost method

                f.RevertContentStream();

                f.Update();

                return;
            }
            else if (f.CustomizedPageStatus == SPCustomizedPageStatus.Uncustomized)
            {
                // this is a standard uncustomized (ghosted) page - do nothing
                return;
            }
            else if (f.CustomizedPageStatus == SPCustomizedPageStatus.None)
            {
                // this page was created manually - need to overwrite it

                if (f.CheckOutStatus != SPFile.SPCheckOutStatus.None)
                {
                    f.UndoCheckOut();
                }

                f.CheckOut();

                var b = File.ReadAllBytes(featurefolder + Path.DirectorySeparatorChar + layoutfilename);

                var f2 = web.Files.Add(fileurl, b, true);

                f2.CheckIn("Checked in by feature");

                if (f2.Item.ParentList.EnableModeration)
                {
                    f2.Approve("Approved by feature");
                }
                f2.Update();

            }

        }


    }
}
