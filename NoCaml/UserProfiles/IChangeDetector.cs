using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NoCaml.UserProfiles
{
    /// <summary>
    /// Implement this to customize change detection instead of using the default ToString method
    /// </summary>
    public interface IChangeDetector
    {
        bool IsChange(object newValue);
    }
}
