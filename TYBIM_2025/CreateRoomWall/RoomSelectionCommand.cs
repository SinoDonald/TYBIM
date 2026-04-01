using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace TYBIM_2025.CreateRoomWall
{
    [Transaction(TransactionMode.Manual)]
    public class RoomSelectionCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;
            Document doc = uiapp.ActiveUIDocument.Document;

            // 1. 取得所有已放置的房間 (Area > 0)
            var rooms = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0)
                .ToList();

            // 2. 取得所有牆類型
            var wallTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .OrderBy(w => w.Name)
                .ToList();

            // 3. 建立 ViewModel 並以樓層分組
            MainViewModel viewModel = new MainViewModel();
            foreach (var wt in wallTypes)
            {
                viewModel.WallTypes.Add(wt);
            }

            var roomsByLevel = rooms.GroupBy(r => r.Level.Name).OrderBy(g => g.Key);
            foreach (var group in roomsByLevel)
            {
                LevelNode levelNode = new LevelNode { Name = group.Key };
                foreach (var room in group.OrderBy(r => r.Number))
                {
                    RoomNode roomNode = new RoomNode
                    {
                        Name = $"{room.Number} - {room.Name}",
                        RoomElement = room,
                        ParentLevel = levelNode
                    };
                    levelNode.Rooms.Add(roomNode);
                }
                viewModel.Levels.Add(levelNode);
            }

            // 4. 開啟 WPF 視窗
            RoomSelectionWindow window = new RoomSelectionWindow();
            window.DataContext = viewModel;

            // 必須設定 owner 為 Revit 視窗 (推薦使用 WindowInteropHelper)
            System.Windows.Interop.WindowInteropHelper helper = new System.Windows.Interop.WindowInteropHelper(window);
            helper.Owner = uiapp.MainWindowHandle;

            if (window.ShowDialog() == true)
            {
                // 使用者按下了確定
                WallType selectedWall = viewModel.SelectedWallType;

                // 篩選出所有被勾選的房間
                List<Room> selectedRooms = viewModel.Levels
                    .SelectMany(l => l.Rooms)
                    .Where(r => r.IsSelected)
                    .Select(r => r.RoomElement)
                    .ToList();

                // 在進入 Transaction 與迴圈之前，先取得並排序所有樓層
                List<Level> allLevels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation) // 依照高程由低到高排序
                    .ToList();

                if (selectedWall == null || selectedRooms.Count == 0)
                {
                    TaskDialog.Show("提示", "未選擇牆類型或未勾選任何房間。");
                    return Result.Cancelled;
                }

                // 在這裡撰寫你後續要生成的牆邏輯
                using (Transaction t = new Transaction(doc, "依房間邊界建立牆壁"))
                {
                    t.Start();

                    SpatialElementBoundaryOptions options = new SpatialElementBoundaryOptions
                    {
                        SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
                    };

                    int createdWallCount = 0;

                    foreach (Room room in selectedRooms)
                    {
                        IList<IList<BoundarySegment>> boundaries = room.GetBoundarySegments(options);
                        if (boundaries == null || boundaries.Count == 0) continue;

                        // 保留你計算高度的邏輯
                        double roomHeight = 3000.0 / 304.8;

                        // 【關鍵 1】取得使用者選擇的牆壁厚度
                        double wallWidth = selectedWall.Width;

                        foreach (IList<BoundarySegment> boundaryLoop in boundaries)
                        {
                            // 組合原本的邊界線
                            CurveLoop originalLoop = new CurveLoop();
                            foreach (BoundarySegment seg in boundaryLoop)
                            {
                                originalLoop.Append(seg.GetCurve());
                            }

                            // 【關鍵 2】利用 API 直接將整圈邊界線「往房間內部」縮小！
                            // 說明：Revit 房間邊界的左側永遠是房間內部。
                            // 在 CreateViaOffset 中，正值代表向右偏，負值代表向左偏。
                            // 所以我們給「負的半個牆厚 (-wallWidth / 2.0)」，整圈線就會乖乖往房間內縮。
                            double offsetDist = -(wallWidth / 2.0);

                            CurveLoop offsetLoop;
                            try
                            {
                                // 執行幾何偏移
                                offsetLoop = CurveLoop.CreateViaOffset(originalLoop, offsetDist, XYZ.BasisZ);
                            }
                            catch
                            {
                                // 防呆機制：萬一房間角落太過畸形導致偏移運算失敗，就退回原邊界
                                offsetLoop = originalLoop;
                            }

                            // 沿著「已經往內縮」的線畫牆
                            foreach (Curve curve in offsetLoop)
                            {
                                // 1. 建立牆壁 (預設用中心線畫，此時實體牆的外側已經完美貼齊原本的房間邊界了)
                                Wall newWall = Wall.Create(doc, curve, selectedWall.Id, room.LevelId, roomHeight, 0.0, false, false);

                                // 2. 建立完成後，將定位線參數改為【塗層面：外部】
                                // 因為牆壁已經精準到位，改這個參數只會讓基準線(藍色控制點)貼齊房間邊界，不會觸發亂跑的接合 Bug！
                                Parameter locLineParam = newWall.get_Parameter(BuiltInParameter.WALL_KEY_REF_PARAM);
                                if (locLineParam != null && !locLineParam.IsReadOnly)
                                {
                                    locLineParam.Set((int)WallLocationLine.FinishFaceExterior);
                                }

                                createdWallCount++;
                            }
                        }
                    }

                    t.Commit();

                    TaskDialog.Show("成功", $"作業完成！\n共處理了 {selectedRooms.Count} 個房間，\n總計建立了 {createdWallCount} 道牆。");
                }

                return Result.Succeeded;
            }

            return Result.Cancelled;
        }
    }
}
