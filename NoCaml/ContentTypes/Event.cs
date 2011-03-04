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

namespace NoCaml.ContentTypes
{
    [ContentType(ContentTypeID = "0x0102")]
    public interface Event : Item
    {
        [Field]
        string Location { get; set; }

        [Field(DisplayName = "Start Time", InternalName = "StartDate")]
        DateTime StartTime { get; set; }

        [Field(DisplayName = "End Time", InternalName = "EndDate")]
        DateTime EndTime { get; set; }

        [Field(DisplayName = "Description", InternalName = "Comments")]
        string Description { get; set; }


    }
}
