using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using static TYBIM_2025.DataObject;

namespace TYBIM_2025
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    [Journaling(JournalingMode.NoCommandData)]
    public class CreateColumns : IExternalEventHandler
    {
        public double unit_conversion = 0.0; // 專案單位轉換
        public string unit_string = "cm"; // 單位字串
        public List<CreateColumn> createColumns = new List<CreateColumn>(); // 儲存要建立的柱資訊

        public void Execute(UIApplication app)
        {
            Document doc = app.ActiveUIDocument.Document;
            Units units = doc.GetUnits(); // 取得專案單位
            ForgeTypeId lengthOptions = units.GetFormatOptions(SpecTypeId.Length).GetUnitTypeId(); // 取得長度單位
            if (lengthOptions == UnitTypeId.Meters) { unit_conversion = 0.3048; unit_string = "m"; }
            else if (lengthOptions == UnitTypeId.Centimeters) { unit_conversion = 30.48; unit_string = "cm"; }
            else if (lengthOptions == UnitTypeId.Millimeters) { unit_conversion = 304.8; unit_string = "mm"; }

            // 找到當前專案的Level相關資訊
            FindLevel findLevel = new FindLevel();
            List<LevelElevation> levelElevList = findLevel.FindDocViewLevel(doc); // 全部樓層
            createColumns = new List<CreateColumn>();

            ElementCategoryFilter structuralColumnsFilter = new ElementCategoryFilter(BuiltInCategory.OST_StructuralColumns);
            ElementCategoryFilter columnsFilter = new ElementCategoryFilter(BuiltInCategory.OST_Columns);
            LogicalOrFilter logicalFilter = new LogicalOrFilter(structuralColumnsFilter, columnsFilter);
            List<FamilySymbol> familySymbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).WherePasses(logicalFilter).Cast<FamilySymbol>().OrderBy(x => x.FamilyName).ToList();
            FamilySymbol columnFS = familySymbols.Where(x => x.FamilyName.Equals("混凝土柱-矩形")).FirstOrDefault();
            if (!columnFS.IsActive)
            {
                using (Transaction trans = new Transaction(doc, "啟用族群"))
                {
                    trans.Start();
                    { columnFS.Activate(); doc.Regenerate(); } //如果柱的型號沒有啟用，則啟用它
                    trans.Commit();
                }
            }

            List<string> selectedLayers = LayersForm.selectedLayers.ToList(); // 取得圖層名稱
            foreach (string selectedLayer in selectedLayers)
            {
                List<LineInfo> linesList = LayersForm.lineInfos.Where(x => x.layerName.Equals(selectedLayer)).ToList();
                // 儲存要建立的柱資訊
                foreach (LineInfo lineInfo in linesList)
                {
                    PolyLine polyLine = lineInfo.polyLine;
                    if (polyLine.GetCoordinates().Count >= 4)
                    {
                        double height = Math.Round(polyLine.GetCoordinates()[0].DistanceTo(polyLine.GetCoordinates()[1]) * unit_conversion, 4, MidpointRounding.AwayFromZero);
                        double width = Math.Round(polyLine.GetCoordinates()[1].DistanceTo(polyLine.GetCoordinates()[2]) * unit_conversion, 4, MidpointRounding.AwayFromZero);
                        XYZ center = GetPolyLineCenter(polyLine); // 計算PolyLine的中心點
                        CreateColumn createColumn = new CreateColumn()
                        {
                            name = height + " x " + width + unit_string,
                            height = height,
                            width = width,
                            center = center,
                            axis = Line.CreateBound(center, new XYZ(center.X, center.Y, center.Z + 1)),
                            angle = Math.Atan2(polyLine.GetCoordinates()[1].Y - polyLine.GetCoordinates()[0].Y, polyLine.GetCoordinates()[1].X - polyLine.GetCoordinates()[0].X),
                            radius = 0.0
                        };
                        createColumns.Add(createColumn);
                    }
                }
            }
            CreateFamilySymbol(doc, familySymbols, createColumns); // 建立柱的FamilySymbol

            // 重新取得FamilySymbol
            familySymbols = new FilteredElementCollector(doc).OfClass(typeof(FamilySymbol)).WherePasses(logicalFilter).Cast<FamilySymbol>().OrderBy(x => x.FamilyName).ToList();
            int count = 0;
            using (Transaction trans = new Transaction(doc, "自動翻柱"))
            {
                trans.Start();
                Level level = doc.GetElement(doc.ActiveView.GenLevel.Id) as Level;
                foreach (CreateColumn createColumn in createColumns)
                {
                    try
                    {
                        columnFS = familySymbols.Where(x => x.FamilyName.Equals("混凝土柱-矩形") && x.Name.Equals(createColumn.name)).FirstOrDefault();
                        if (columnFS != null)
                        {
                            { columnFS.Activate(); doc.Regenerate(); } //如果柱的型號沒有啟用，則啟用它
                            FamilyInstance colunmFI = doc.Create.NewFamilyInstance(createColumn.center, columnFS, level, StructuralType.Column); // 生成柱
                            //if (createColumn.angle != 0)
                            //{
                            //    ElementTransformUtils.RotateElement(doc, colunmFI.Id, createColumn.axis, createColumn.angle * Math.PI / 180.0); // 旋轉柱
                            //}
                            count++;
                        }
                    }
                    catch (Exception) { }
                }
                trans.Commit();
            }

            if (count > 0) { TaskDialog.Show("Revit", "已成功建立 " + count + " 支柱。"); }
        }
        // 計算PolyLine的中心點
        public XYZ GetPolyLineCenter(PolyLine polyLine)
        {
            // 取得所有頂點
            IList<XYZ> pts = polyLine.GetCoordinates();
            if (pts.Count < 4) { return null; }

            XYZ p1 = pts[0];
            XYZ p2 = pts[1];
            XYZ p3 = pts[2];

            double x = (p1.X + p2.X) / 2.0;
            double y = (p2.Y + p3.Y) / 2.0;
            return new XYZ(x, y, p1.Z);
        }
        // 新增FamilySymbol
        private void CreateFamilySymbol(Document doc, List<FamilySymbol> familySymbols, List<CreateColumn> createColumns)
        {
            List<string> createSymbolNames = new List<string>();
            List<string> symbolNames = createColumns.Select(x => x.name).Distinct().OrderBy(x => x).ToList();
            foreach (string symbolName in symbolNames)
            {
                FamilySymbol isExistFS = familySymbols.Where(x => x.FamilyName.Equals("混凝土柱-矩形") && x.Name.Equals(symbolName)).FirstOrDefault();
                if (isExistFS == null) { createSymbolNames.Add(symbolName); } // 已經沒有這個FamilySymbol就加入
            }
            FamilySymbol columnFS = familySymbols.Where(x => x.FamilyName.Equals("混凝土柱-矩形")).FirstOrDefault();
            if (columnFS != null)
            {
                try
                {
                    // 獲取相關的Family
                    Family columnFamily = columnFS.Family;
                    // 獲取Family document
                    Document familyDoc = doc.EditFamily(columnFamily);
                    FamilyManager familyManager = familyDoc.FamilyManager;
                    using (Transaction transFS = new Transaction(familyDoc, "新增類型"))
                    {
                        transFS.Start();

                        foreach (string createSymbolName in createSymbolNames)
                        {
                            try
                            {
                                CreateColumn createColumn = createColumns.Where(x => x.name.Equals(createSymbolName)).FirstOrDefault();
                                double height = createColumn.height;
                                double width = createColumn.width;
                                string newTypeName = createSymbolName;

                                // 新增與編輯FamilyTypes
                                FamilyType newFamilyType = familyManager.NewType(newTypeName);
                                if (newFamilyType != null)
                                {
                                    // 設置柱寬與柱深
                                    FamilyParameter paraWidth = familyManager.get_Parameter("柱深");
                                    FamilyParameter paraHeight = familyManager.get_Parameter("柱寬");
                                    if (null != paraWidth && null != paraHeight)
                                    {
                                        familyManager.Set(paraWidth, width / unit_conversion);
                                        familyManager.Set(paraHeight, height / unit_conversion);
                                    }
                                }

                                familyDoc = doc.EditFamily(columnFamily); // 取得族群編輯                                
                                columnFamily = familyDoc.LoadFamily(doc, new LoadOpts()); // 將更新項目回傳到Revit Document中
                            }
                            catch (Autodesk.Revit.Exceptions.ArgumentException) // FamilyType名稱重複
                            {
                                familyDoc = doc.EditFamily(columnFamily); // 取得族群編輯
                                columnFamily = familyDoc.LoadFamily(doc, new LoadOpts()); // 將更新項目回傳到Revit Document中
                            }
                            catch { }
                        }

                        transFS.Commit();
                    }
                }
                catch (Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }
            }
        }
        /// <summary>
        /// 3D視圖中畫模型線
        /// </summary>
        /// <param name="doc"></param>
        /// <param name="curve"></param>
        private int DrawLine(Document doc, Curve curve)
        {
            int i = 0;
            try
            {
                Line line = Line.CreateBound(curve.Tessellate()[0], curve.Tessellate()[curve.Tessellate().Count - 1]);
                XYZ normal = new XYZ(line.Direction.Z - line.Direction.Y, line.Direction.X - line.Direction.Z, line.Direction.Y - line.Direction.X); // 使用與線不平行的任意向量
                Plane plane = Plane.CreateByNormalAndOrigin(normal, curve.Tessellate()[0]);
                SketchPlane sketchPlane = SketchPlane.Create(doc, plane);
                ModelCurve modelCurve = doc.Create.NewModelCurve(line, sketchPlane);
                i = 1;
            }
            catch (Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }

            return i;
        }
        // 關閉警示視窗 
        public class CloseWarnings : IFailuresPreprocessor
        {
            FailureProcessingResult IFailuresPreprocessor.PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                String transactionName = failuresAccessor.GetTransactionName();
                IList<FailureMessageAccessor> fmas = failuresAccessor.GetFailureMessages();
                if (fmas.Count == 0) { return FailureProcessingResult.Continue; }
                if (transactionName.Equals("EXEMPLE"))
                {
                    foreach (FailureMessageAccessor fma in fmas)
                    {
                        if (fma.GetSeverity() == FailureSeverity.Error)
                        {
                            failuresAccessor.DeleteAllWarnings();
                            return FailureProcessingResult.ProceedWithRollBack;
                        }
                        else { failuresAccessor.DeleteWarning(fma); }
                    }
                }
                else
                {
                    foreach (FailureMessageAccessor fma in fmas) { failuresAccessor.DeleteAllWarnings(); }
                }
                return FailureProcessingResult.Continue;
            }
        }
        // 更新Revit項目, Family有一個新的類型
        class LoadOpts : IFamilyLoadOptions
        {
            public bool OnFamilyFound(bool familyInUse, out bool overwriteParameterValues)
            {
                overwriteParameterValues = true;
                return true;
            }

            public bool OnSharedFamilyFound(Family sharedFamily, bool familyInUse, out FamilySource source, out bool overwriteParameterValues)
            {
                source = FamilySource.Family;
                overwriteParameterValues = true;
                return true;
            }
        }

        public string GetName()
        {
            return "Event handler is create walls !!";
        }
    }
}