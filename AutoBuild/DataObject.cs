using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AutoBuild
{
    public class DataObject
    {
        public class LineInfo
        {
            public string layerName { get; set; }
            public Reference reference {  get; set; }
            public Curve curve { get; set; }
        }
    }
}
