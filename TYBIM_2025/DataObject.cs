using Autodesk.Revit.DB;

namespace TYBIM_2025
{
    public class DataObject
    {
        public class LineInfo
        {
            public string layerName { get; set; }
            public Reference reference { get; set; }
            public PolyLine polyLine { get; set; }
        }
        public class CreateColumn
        {
            public string name { get; set; } // 名稱
            public double height { get; set; } // 高度
            public double width { get; set; } // 寬度
            public XYZ center { get; set; } // 中心點            
            public Line axis { get; set; } // 軸心            
            public double angle { get; set; } // 角度           
            public double radius { get; set; } // 半徑
        }
    }
}
