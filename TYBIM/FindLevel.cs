using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace TYBIM
{
    public class LevelElevation
    {
        public Level level { get; set; }
        public string name { get; set; }
        public double elevation { get; set; }
        public double height { get; set; }
    }
    class FindLevel
    {
        // 找到當前視圖的Level相關資訊
        public List<LevelElevation> FindDocViewLevel(Document doc)
        {
            // 查詢所有Level的高程並排序
            List<Level> levels = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Levels).WhereElementIsNotElementType().Cast<Level>().ToList();
            List<LevelElevation> levelElevList = new List<LevelElevation>();
            foreach (Level level in levels)
            {
                LevelElevation levelElevation = new LevelElevation();
                levelElevation.name = level.Name;
                levelElevation.level = level;
                levelElevation.height = level.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble();
                //levelElevation.elevation = Convert.ToDouble(level.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsValueString());
                double elevation = level.get_Parameter(BuiltInParameter.LEVEL_ELEV).AsDouble();
                levelElevation.elevation = elevation;
                //levelElevation.elevation = UnitUtils.ConvertFromInternalUnits(elevation, UnitTypeId.Meters);
                levelElevList.Add(levelElevation);
            }
            levelElevList = levelElevList.OrderBy(x => x.elevation).ToList();
            return levelElevList;
        }
    }
}