using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using static TYBIM.DataObject;

namespace TYBIM
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
                        Tuple<XYZ, Line, double> getPolyLineInfo = GetPolyLineInfo(polyLine); // 計算PolyLine的中心點
                        XYZ center = getPolyLineInfo.Item1; // 中心點
                        Line axis = getPolyLineInfo.Item2; // 軸心
                        double angle = getPolyLineInfo.Item3; // 角度
                        CreateColumn createColumn = new CreateColumn()
                        {
                            name = height + " x " + width + unit_string,
                            height = height,
                            width = width,
                            center = center,
                            axis = axis,
                            angle = angle,
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
                // 基準樓層與頂部樓層
                List<Level> levels = new FilteredElementCollector(doc).OfClass(typeof(Level)).Cast<Level>().OrderBy(x => x.Name).ToList();
                Level base_level = levels.Where(x => x.Name.Equals(LayersForm.b_level_name)).FirstOrDefault();
                Level top_level = levels.Where(x => x.Name.Equals(LayersForm.t_level_name)).FirstOrDefault();
                foreach (CreateColumn createColumn in createColumns)
                {
                    try
                    {
                        columnFS = familySymbols.Where(x => x.FamilyName.Equals("混凝土柱-矩形") && x.Name.Equals(createColumn.name)).FirstOrDefault();
                        if(columnFS != null)
                        {
                            { columnFS.Activate(); doc.Regenerate(); } //如果柱的型號沒有啟用，則啟用它
                            // 是否依樓層建立
                            if (LayersForm.byLevel)
                            {
                                int startId = levelElevList.FindIndex(x => x.level.Id.Equals(base_level.Id));
                                int endId = levelElevList.FindIndex(x => x.level.Id.Equals(top_level.Id));
                                for(int i = startId; i < endId; i++)
                                {
                                    LevelElevation currentLevel = levelElevList[i];
                                    LevelElevation nextLevel = levelElevList[i + 1];
                                    FamilyInstance colunm = doc.Create.NewFamilyInstance(createColumn.center, columnFS, currentLevel.level, StructuralType.Column); // 生成柱
                                    colunm.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM).Set(nextLevel.level.Id); // 設定頂部樓層
                                    colunm.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM).Set(0); // 設定基準偏移
                                    colunm.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM).Set(0); // 設定頂部偏移
                                    ElementTransformUtils.RotateElement(doc, colunm.Id, createColumn.axis, createColumn.angle * Math.PI / 180.0); // 旋轉柱
                                    count++;
                                }
                            }
                            else
                            {
                                FamilyInstance colunm = doc.Create.NewFamilyInstance(createColumn.center, columnFS, base_level, StructuralType.Column); // 生成柱
                                colunm.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_PARAM).Set(top_level.Id); // 設定頂部樓層
                                colunm.get_Parameter(BuiltInParameter.SCHEDULE_BASE_LEVEL_OFFSET_PARAM).Set(0); // 設定基準偏移
                                colunm.get_Parameter(BuiltInParameter.SCHEDULE_TOP_LEVEL_OFFSET_PARAM).Set(0); // 設定頂部偏移
                                ElementTransformUtils.RotateElement(doc, colunm.Id, createColumn.axis, createColumn.angle * Math.PI / 180.0); // 旋轉柱
                                count++;
                            }
                        }
                    }
                    catch (Exception) { }
                }
                trans.Commit();
            }

            if (count > 0) { TaskDialog.Show("Revit", "已成功建立 " + count + " 支柱。"); }
        }
        // PolyLine資訊
        public Tuple<XYZ, Line, double> GetPolyLineInfo(PolyLine polyLine)
        {
            XYZ center = new XYZ(); // 中心點
            Line axis = null; // 軸心
            double angle = 0.0; // 角度

            // 取得所有頂點
            IList<XYZ> pts = polyLine.GetCoordinates();
            if (pts.Count < 4) { return null; }
            else
            {
                center = new XYZ((pts[0].X + pts[1].X + pts[2].X + pts[3].X) / 4,
                                    (pts[0].Y + pts[1].Y + pts[2].Y + pts[3].Y) / 4,
                                    (pts[0].Z + pts[1].Z + pts[2].Z + pts[3].Z) / 4);
                axis = Line.CreateBound(center, new XYZ(center.X, center.Y, center.Z + 1)); // 軸心                                
                angle = PointRotation(pts[0], pts[1]); // 角度
            }
            return Tuple.Create<XYZ, Line, double>(center, axis, angle);
        }
        // 旋轉角度
        private static double PointRotation(XYZ p1, XYZ p2)
        {
            XYZ pA = new XYZ(p1.X, p1.Y, 0);
            XYZ pB = new XYZ(p2.X, p2.Y, 0);
            double Dx = pB.X - pA.X;
            double Dy = pB.Y - pA.Y;
            double DRoation = Math.Atan2(Dy, Dx);
            double WRotation = DRoation / Math.PI * 180;

            return WRotation;
        }
        // 新增FamilySymbol
        private void CreateFamilySymbol(Document doc, List<FamilySymbol> familySymbols, List<CreateColumn> createColumns)
        {
            List<string> createSymbolNames = new List<string>();
            List<string> symbolNames = createColumns.Select(x => x.name).Distinct().OrderBy(x => x).ToList();
            foreach(string symbolName in symbolNames)
            {
                FamilySymbol isExistFS = familySymbols.Where(x => x.FamilyName.Equals("混凝土柱-矩形") && x.Name.Equals(symbolName)).FirstOrDefault();
                if(isExistFS == null) { createSymbolNames.Add(symbolName); } // 已經沒有這個FamilySymbol就加入
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
                catch(Exception ex) { string error = ex.Message + "\n" + ex.ToString(); }
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