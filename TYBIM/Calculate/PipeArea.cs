using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace TYBIM.Calculate
{
    [Transaction(TransactionMode.Manual)]
    public class PipeArea : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. 定義目標品類
                var targetCategories = new List<BuiltInCategory>
                {
                    BuiltInCategory.OST_PipeCurves,
                    BuiltInCategory.OST_PipeFitting,
                    BuiltInCategory.OST_DuctCurves,
                    BuiltInCategory.OST_DuctFitting,
                    BuiltInCategory.OST_CableTray,
                    BuiltInCategory.OST_CableTrayFitting
                };

                // 2. 彈出品類選擇 UI
                List<BuiltInCategory> selectedCategories;
                using (var selectForm = new CategorySelectionForm(targetCategories))
                {
                    if (selectForm.ShowDialog() != DialogResult.OK)
                    {
                        return Result.Cancelled;
                    }
                    selectedCategories = selectForm.SelectedCategories;
                }

                if (!selectedCategories.Any())
                {
                    TaskDialog.Show("提示", "未勾選任何品類。");
                    return Result.Succeeded;
                }

                // 3. 初始化統計數據與錯誤 Log 清單
                var stats = selectedCategories.ToDictionary(cat => cat, cat => new CategoryStat(cat));
                var errorLogs = new List<string>();

                // 4. 計算主模型
                ProcessDocument(doc, Transform.Identity, selectedCategories, stats, errorLogs, "主模型");

                // 5. 計算連結檔
                FilteredElementCollector linkCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance));

                foreach (RevitLinkInstance linkInstance in linkCollector.Cast<RevitLinkInstance>())
                {
                    Document linkDoc = linkInstance.GetLinkDocument();
                    if (linkDoc == null) continue;

                    Transform totalTransform = linkInstance.GetTotalTransform();
                    ProcessDocument(linkDoc, totalTransform, selectedCategories, stats, errorLogs, $"連結檔: {linkInstance.Name}");
                }

                // 6. 若有錯誤元件，將錯誤 Log 放置桌面
                if (errorLogs.Any())
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                    string logPath = Path.Combine(desktopPath, $"MEP_Area_ErrorLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllLines(logPath, errorLogs);
                    TaskDialog.Show("稽核提示", $"部分元件幾何無法取得頂面，詳細 Log 已輸出至桌面：\n{logPath}");
                }

                // 7. 顯示統計與匯出 UI
                using (var resultForm = new ResultForm(stats.Values.ToList()))
                {
                    resultForm.ShowDialog();
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.ToString();
                return Result.Failed;
            }
        }

        private void ProcessDocument(
            Document doc,
            Transform transform,
            List<BuiltInCategory> categories,
            Dictionary<BuiltInCategory, CategoryStat> stats,
            List<string> errorLogs,
            string docName)
        {
            ElementMulticategoryFilter categoryFilter = new ElementMulticategoryFilter(categories);
            FilteredElementCollector collector = new FilteredElementCollector(doc)
                .WherePasses(categoryFilter)
                .WhereElementIsNotElementType();

            Options geomOptions = new Options
            {
                ComputeReferences = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            foreach (Element elem in collector)
            {
                if (elem.Category == null) continue;

                BuiltInCategory elemCat = (BuiltInCategory)elem.Category.Id.Value;
                if (!stats.ContainsKey(elemCat)) continue;

                try
                {
                    double topArea = CalculateElementTopArea(elem, geomOptions, transform);

                    if (topArea <= 0.00001)
                    {
                        errorLogs.Add($"[{docName}] 品類: {elem.Category.Name} | Element ID: {elem.Id.Value} - 未能擷取到頂面面積。");
                    }

                    stats[elemCat].Count++;
                    stats[elemCat].TotalTopAreaSquareFeet += topArea;
                }
                catch (Exception ex)
                {
                    errorLogs.Add($"[{docName}] 品類: {elem.Category.Name} | Element ID: {elem.Id.Value} - 幾何錯誤: {ex.Message}");
                }
            }
        }

        private double CalculateElementTopArea(Element elem, Options options, Transform transform)
        {
            GeometryElement geomElem = elem.get_Geometry(options);
            if (geomElem == null) return 0.0;

            double totalTopArea = 0.0;
            List<Solid> solids = GetSolidsFromGeometry(geomElem);

            foreach (Solid solid in solids)
            {
                if (solid == null || solid.Faces.IsEmpty || solid.Volume <= 0.00001) continue;

                foreach (Face face in solid.Faces)
                {
                    if (IsTopFace(face, transform))
                    {
                        totalTopArea += face.Area;
                    }
                }
            }

            return totalTopArea;
        }

        private List<Solid> GetSolidsFromGeometry(GeometryElement geomElem)
        {
            var results = new List<Solid>();
            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Volume > 0.00001)
                {
                    results.Add(solid);
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    if (instGeom != null)
                    {
                        results.AddRange(GetSolidsFromGeometry(instGeom));
                    }
                }
            }
            return results;
        }

        private bool IsTopFace(Face face, Transform transform)
        {
            if (face is PlanarFace planarFace)
            {
                XYZ normal = planarFace.FaceNormal;
                XYZ worldNormal = transform.OfVector(normal).Normalize();
                return worldNormal.Z > 0.707;
            }
            else if (face is CylindricalFace cylindricalFace)
            {
                BoundingBoxUV bbox = cylindricalFace.GetBoundingBox();
                UV centerUV = (bbox.Min + bbox.Max) * 0.5;
                XYZ normal = cylindricalFace.ComputeNormal(centerUV);
                XYZ worldNormal = transform.OfVector(normal).Normalize();
                return worldNormal.Z > 0.707;
            }

            return false;
        }
    }
    /// <summary>
    /// 品類統計數據傳遞物件 (DTO)
    /// </summary>
    public class CategoryStat
    {
        public BuiltInCategory Category { get; }
        public string CategoryName { get; }
        public int Count { get; set; }
        public double TotalTopAreaSquareFeet { get; set; }

        /// <summary>
        /// 平方英呎轉平方米 (1 sq ft = 0.09290304 sq m)
        /// </summary>
        public double TotalTopAreaSquareMeters => TotalTopAreaSquareFeet * 0.09290304;

        public CategoryStat(BuiltInCategory category)
        {
            Category = category;
            CategoryName = GetFriendlyName(category);
            Count = 0;
            TotalTopAreaSquareFeet = 0.0;
        }

        private string GetFriendlyName(BuiltInCategory cat)
        {
            switch (cat)
            {
                case BuiltInCategory.OST_PipeCurves: return "管 (Pipes)";
                case BuiltInCategory.OST_PipeFitting: return "管配件 (Pipe Fittings)";
                case BuiltInCategory.OST_DuctCurves: return "風管 (Ducts)";
                case BuiltInCategory.OST_DuctFitting: return "風管配件 (Duct Fittings)";
                case BuiltInCategory.OST_CableTray: return "電纜架 (Cable Trays)";
                case BuiltInCategory.OST_CableTrayFitting: return "電纜架配件 (Cable Tray Fittings)";
                default: return cat.ToString();
            }
        }

        /// <summary>
        /// 覆寫 ToString 確保 CheckedListBox 能正確顯示品類名稱而非類別全名
        /// </summary>
        public override string ToString()
        {
            return CategoryName;
        }
    }
}