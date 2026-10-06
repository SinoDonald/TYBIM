using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TYBIM_2025
{
    // 預設Excel檔案路徑
    public class LicPath
    {
        public string previous = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        public string pathStr = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\tmp.txt"; // 我的文件
    }
    public class TYBIM_Button : IExternalApplication
    {
        public string addinAssmeblyPath = Assembly.GetExecutingAssembly().Location; // 封包版路徑位址
        public Result OnStartup(UIControlledApplication application)
        {
            string autoBuildAsb = Path.Combine(Directory.GetParent(addinAssmeblyPath).FullName, "TYBIM_2025.dll");
            string ribbonName = "拓源數位";
            // 創建一個新的選單
            RibbonPanel ribbonPanel = null;
            try { application.CreateRibbonTab(ribbonName); } catch { }
            try { ribbonPanel = application.CreateRibbonPanel(ribbonName, "自動翻模"); }
            catch
            {
                List<RibbonPanel> panel_list = new List<RibbonPanel>();
                panel_list = application.GetRibbonPanels(ribbonName);
                foreach (RibbonPanel rp in panel_list)
                {
                    if (rp.Name == "自動翻模")
                    {
                        ribbonPanel = rp;
                    }
                }
            }
            PushButton createColumnsBtn = ribbonPanel.AddItem(new PushButtonData("CreateColumns", "自動翻柱", addinAssmeblyPath, "TYBIM_2025.AutoBuild.Start")) as PushButton;
            createColumnsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻柱);
            createColumnsBtn.ToolTip = "讀取 DWG 圖層，依選擇的柱族群、基準與頂部樓層建立柱。";
            createColumnsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動翻柱_原圖, 250);
            PushButton createWallsBtn = ribbonPanel.AddItem(new PushButtonData("CreateWalls", "自動翻牆", addinAssmeblyPath, "TYBIM_2025.AutoBuild.StartWalls")) as PushButton;
            createWallsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻牆);
            createWallsBtn.ToolTip = "讀取 DWG 圖層中的封閉雙線區域，依選擇的基本牆類型與樓層建立牆。";
            createWallsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動翻牆_原圖, 250);
            PushButton createBeamsBtn = ribbonPanel.AddItem(new PushButtonData("CreateBeams", "自動翻樑", addinAssmeblyPath, "TYBIM_2025.AutoBuild.StartBeams")) as PushButton;
            createBeamsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻樑);
            createBeamsBtn.ToolTip = "讀取 DWG 圖層，依選擇的樑類型與參考樓層建立樑。";
            createBeamsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動翻樑_原圖, 250);
            PushButton createFloorsBtn = ribbonPanel.AddItem(new PushButtonData("CreateFloors", "自動生板", addinAssmeblyPath, "TYBIM_2025.AutoBuild.CreateFloor")) as PushButton;
            createFloorsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動生板);
            createFloorsBtn.ToolTip = "在 3D 視圖中依各樓層的柱樑邊界，自動生成樓板。";
            createFloorsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動生板_原圖, 250);
            PushButton createRoomWallBtn = ribbonPanel.AddItem(new PushButtonData("CreateRoomWall", "自動裝修牆", addinAssmeblyPath, "TYBIM_2025.CreateRoomWall.RoomSelectionCommand")) as PushButton;
            createRoomWallBtn.LargeImage = convertFromBitmap(Properties.Resources.自動裝修牆);
            createRoomWallBtn.ToolTip = "選擇房間，沿房間邊界建立裝修牆。";
            createRoomWallBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動裝修牆_原圖, 250);

            // 添加「SEM」面板
            try { ribbonPanel = application.CreateRibbonPanel(ribbonName, "SEM"); }
            catch
            {
                List<RibbonPanel> panel_list = new List<RibbonPanel>();
                panel_list = application.GetRibbonPanels(ribbonName);
                foreach (RibbonPanel rp in panel_list) { if (rp.Name == "SEM") { ribbonPanel = rp; } }
            }
            PushButton autoPipeOpenBtn = ribbonPanel.AddItem(new PushButtonData("AutoPipeOpen", "自動開口", addinAssmeblyPath, "TYBIM_2025.SEM.LinkOpening")) as PushButton;
            autoPipeOpenBtn.LargeImage = convertFromBitmap(Properties.Resources.自動開口);
            autoPipeOpenBtn.ToolTip = "在模型中讀取所有管道、風管、電纜架等機電設施, 當這些構件與樑、板、牆交接時, 在這些交接的位置進行開口與套管的動作。";
            autoPipeOpenBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動開口_原圖, 250);
            PushButton autoNumberBtn = ribbonPanel.AddItem(new PushButtonData("AutoNumber", "自動編號", addinAssmeblyPath, "TYBIM_2025.SEM.AutoNumber")) as PushButton;
            autoNumberBtn.LargeImage = convertFromBitmap(Properties.Resources.自動編號);
            autoNumberBtn.ToolTip = "在模型中依每一張視圖的所有開口, 依照網格順序由左而右、由上而下進行數字編號。";
            autoNumberBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動編號_原圖, 250);
            PushButton autoOpeningTagBtn = ribbonPanel.AddItem(new PushButtonData("AutoOpeningTag", "開口標籤", addinAssmeblyPath, "TYBIM_2025.SEM.AutoOpeningTag")) as PushButton;
            autoOpeningTagBtn.LargeImage = convertFromBitmap(Properties.Resources.開口標籤);
            autoOpeningTagBtn.ToolTip = "在開口的中心點放置標籤, 顯示開口的編號為多少。";
            autoOpeningTagBtn.ToolTipImage = convertFromBitmap(Properties.Resources.開口標籤_原圖, 250);
            PushButton openingTagArrayBtn = ribbonPanel.AddItem(new PushButtonData("OpeningTagArrayBtn", "標籤排序", addinAssmeblyPath, "TYBIM_2025.SEM.OpeningTagArray")) as PushButton;
            openingTagArrayBtn.LargeImage = convertFromBitmap(Properties.Resources.標籤排序);
            openingTagArrayBtn.ToolTip = "將開口標籤移至空白處, 盡量避免標籤重疊。";
            openingTagArrayBtn.ToolTipImage = convertFromBitmap(Properties.Resources.標籤排序_原圖, 250);

            // 添加「數量計算」面板
            try { ribbonPanel = application.CreateRibbonPanel(ribbonName, "數量計算"); }
            catch
            {
                List<RibbonPanel> panel_list = new List<RibbonPanel>();
                panel_list = application.GetRibbonPanels(ribbonName);
                foreach (RibbonPanel rp in panel_list) { if (rp.Name == "數量計算") { ribbonPanel = rp; } }
            }
            PushButton pipeAreaBtn = ribbonPanel.AddItem(new PushButtonData("PipeArea", "管道面積", addinAssmeblyPath, "TYBIM_2025.Calculate.PipeArea")) as PushButton;
            pipeAreaBtn.LargeImage = convertFromBitmap(Properties.Resources.管道面積);
            pipeAreaBtn.ToolTip = "選擇構件品類，計算管道面積並顯示結果。";
            pipeAreaBtn.ToolTipImage = convertFromBitmap(Properties.Resources.管道面積_原圖, 250);

            return Result.Succeeded;
        }
        /// <summary>
        /// 轉換圖片
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        BitmapSource convertFromBitmap(System.Drawing.Bitmap bitmap, int targetWidth = 0)
        {
            if (bitmap == null) return null;

            if (targetWidth > 0 && bitmap.Width != targetWidth)
            {
                int targetHeight = Math.Max(1, (int)((double)bitmap.Height / bitmap.Width * targetWidth));
                using (var resized = new System.Drawing.Bitmap(targetWidth, targetHeight))
                {
                    using (var graphics = System.Drawing.Graphics.FromImage(resized))
                    {
                        graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                        graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        graphics.DrawImage(bitmap, 0, 0, targetWidth, targetHeight);
                    }
                    return convertFromBitmap(resized);
                }
            }

            IntPtr hBitmap = bitmap.GetHbitmap();
            try
            {
                BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                source.Freeze();
                return source;
            }
            finally
            {
                DeleteObject(hBitmap);
            }
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
