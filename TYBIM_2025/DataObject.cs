using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TYBIM_2025
{
    public class DataObject
    {
        public class LineInfo
        {
            public string layerName { get; set; }
            public Reference reference {  get; set; }
            public PolyLine polyLine { get; set; }
        }
    }
}
