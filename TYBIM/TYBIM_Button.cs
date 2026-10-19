using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace TYBIM
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
            string autoBuildAsb = Path.Combine(Directory.GetParent(addinAssmeblyPath).FullName, "TYBIM.dll");
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
            PushButton createColumnsBtn = ribbonPanel.AddItem(new PushButtonData("CreateColumns", "自動翻柱", addinAssmeblyPath, "TYBIM.AutoBuild.Start")) as PushButton;
            createColumnsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻柱);
            createColumnsBtn.ToolTip = "【功能介紹】\n讀取 DWG 圖層，依指定柱類型與樓層自動建立柱。";
            createColumnsBtn.LongDescription = "【使用步驟】\n1. 先將 DWG 匯入或連結至專案，再開啟「自動翻柱」。\n2. 勾選柱圖層，選擇柱類型、基準樓層及頂部樓層。\n3. 按「確定」建立，完成後檢查柱的位置與高度。";
            createColumnsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動翻柱_原圖, 250);
            PushButton createBeamsBtn = ribbonPanel.AddItem(new PushButtonData("CreateBeams", "自動翻樑", addinAssmeblyPath, "TYBIM.AutoBuild.StartBeams")) as PushButton;
            createBeamsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻樑);
            createBeamsBtn.ToolTip = "【功能介紹】\n讀取 DWG 圖層，依指定樑類型與參考樓層自動建立樑。";
            createBeamsBtn.LongDescription = "【使用步驟】\n1. 先將 DWG 匯入或連結至專案，再開啟「自動翻樑」。\n2. 勾選樑圖層，選擇樑類型與參考樓層。\n3. 按「確定」建立，完成後檢查樑的位置與方向。";
            createBeamsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動翻樑_原圖, 250);
            PushButton createFloorsBtn = ribbonPanel.AddItem(new PushButtonData("CreateFloors", "自動翻板", addinAssmeblyPath, "TYBIM.AutoBuild.CreateFloor")) as PushButton;
            createFloorsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻板);
            createFloorsBtn.ToolTip = "【功能介紹】\n依本機模型的柱樑封閉區域建立樓板，並跳過與既有樓板重疊的區域。";
            createFloorsBtn.LongDescription = "【使用步驟】\n1. 確認專案已有 Revit 柱樑實體。\n2. 開啟樓層平面或結構平面以處理該樓層；開啟 3D 視圖則處理各樓層。\n3. 點選「自動翻板」，完成後查看建立與跳過數量。";
            createFloorsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動翻板_原圖, 250);
            PushButton createWallsBtn = ribbonPanel.AddItem(new PushButtonData("CreateWalls", "自動翻牆", addinAssmeblyPath, "TYBIM.AutoBuild.StartWalls")) as PushButton;
            createWallsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻牆);
            createWallsBtn.ToolTip = "【功能介紹】\n讀取 DWG 圖層中的封閉雙線區域，依指定基本牆類型與樓層自動建立牆。";
            createWallsBtn.LongDescription = "【使用步驟】\n1. 先將具有封閉雙線牆區域的 DWG 匯入或連結至專案。\n2. 開啟「自動翻牆」，勾選牆圖層，選擇基本牆類型、基準樓層及頂部樓層。\n3. 按「確定」建立，完成後檢查牆的位置與厚度。";
            createWallsBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動翻牆_原圖, 250);
            PushButton createRoomWallBtn = ribbonPanel.AddItem(new PushButtonData("CreateRoomWall", "自動裝修牆", addinAssmeblyPath, "TYBIM.CreateRoomWall.RoomSelectionCommand")) as PushButton;
            createRoomWallBtn.Image = convertFromBitmap(Properties.Resources.自動裝修牆, 16);
            createRoomWallBtn.LargeImage = convertFromBitmap(Properties.Resources.自動裝修牆, 32);
            createRoomWallBtn.ToolTip = "【功能介紹】\n沿房間完成面邊界向內建立裝修牆，牆底位於房間所屬樓層，高度固定為 3 公尺。";
            createRoomWallBtn.LongDescription = "【使用步驟】\n1. 先放置房間並確認房間已封閉且面積大於零。\n2. 展開樓層勾選房間，或勾選樓層以全選該樓層房間，再選擇裝修牆類型。\n3. 按「確定」建立，完成後查看處理房間數與建立牆數。";
            createRoomWallBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動裝修牆_原圖, 250);

            // 添加「SEM」面板
            try { ribbonPanel = application.CreateRibbonPanel(ribbonName, "SEM"); }
            catch
            {
                List<RibbonPanel> panel_list = new List<RibbonPanel>();
                panel_list = application.GetRibbonPanels(ribbonName);
                foreach (RibbonPanel rp in panel_list) { if (rp.Name == "SEM") { ribbonPanel = rp; } }
            }
            PushButton autoPipeOpenBtn = ribbonPanel.AddItem(new PushButtonData("AutoPipeOpen", "自動開口", addinAssmeblyPath, "TYBIM.SEM.LinkOpening")) as PushButton;
            autoPipeOpenBtn.LargeImage = convertFromBitmap(Properties.Resources.自動開口);
            autoPipeOpenBtn.ToolTip = "【功能介紹】\n檢查所選連結模型的機電管線與主模型樑、板、牆的交接位置，自動放置開口或套管。";
            autoPipeOpenBtn.LongDescription = "【使用步驟】\n1. 確認機電模型已連結至專案，再開啟「自動開口」。\n2. 選擇檔名簡碼解析位置，勾選連結專案並指定專業代碼，按「確定」加入設定。\n3. 視需要設定高程偏移值，按「完成」執行，再檢查開口位置與數量。";
            autoPipeOpenBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動開口_原圖, 250);
            PushButton autoNumberBtn = ribbonPanel.AddItem(new PushButtonData("AutoNumber", "自動編號", addinAssmeblyPath, "TYBIM.SEM.AutoNumber")) as PushButton;
            autoNumberBtn.LargeImage = convertFromBitmap(Properties.Resources.自動編號);
            autoNumberBtn.ToolTip = "【功能介紹】\n依專業代碼與設定的順序，自動編排開口及套管編號。";
            autoNumberBtn.LongDescription = "【使用步驟】\n1. 先完成開口或套管建立，再開啟「自動編號」。\n2. 設定檔名解析欄位，調整開口與套管的編號順序。\n3. 按「開始編號」，完成後檢查元件編號與執行結果。";
            autoNumberBtn.ToolTipImage = convertFromBitmap(Properties.Resources.自動編號_原圖, 250);
            PushButton autoOpeningTagBtn = ribbonPanel.AddItem(new PushButtonData("AutoOpeningTag", "開口標籤", addinAssmeblyPath, "TYBIM.SEM.AutoOpeningTag")) as PushButton;
            autoOpeningTagBtn.LargeImage = convertFromBitmap(Properties.Resources.開口標籤);
            autoOpeningTagBtn.ToolTip = "【功能介紹】\n在所選平面視圖中批次建立開口與套管標籤，跳過已標註的元件。";
            autoOpeningTagBtn.LongDescription = "【使用步驟】\n1. 先確認開口與套管已完成編號。\n2. 開啟「開口標籤」，選擇視圖類型並按「確定」。\n3. 勾選要標註的視圖並確認執行，完成後檢查標籤與執行報告。";
            autoOpeningTagBtn.ToolTipImage = convertFromBitmap(Properties.Resources.開口標籤_原圖, 250);
            PushButton openingTagArrayBtn = ribbonPanel.AddItem(new PushButtonData("OpeningTagArrayBtn", "標籤排序", addinAssmeblyPath, "TYBIM.SEM.OpeningTagArray")) as PushButton;
            openingTagArrayBtn.LargeImage = convertFromBitmap(Properties.Resources.標籤排序);
            openingTagArrayBtn.ToolTip = "【功能介紹】\n自動排列目前平面視圖中未開啟引線的開口與套管標籤，盡量減少重疊；保留已有引線的標籤位置。";
            openingTagArrayBtn.LongDescription = "【使用步驟】\n1. 開啟要整理的平面視圖，確認已建立開口或套管標籤。\n2. 點選「標籤排序」，自動整理尚未開啟引線的標籤。\n3. 查看處理數量並檢查排版，必要時手動微調。";
            openingTagArrayBtn.ToolTipImage = convertFromBitmap(Properties.Resources.標籤排序_原圖, 250);

            // 添加「建築」面板
            try { ribbonPanel = application.CreateRibbonPanel(ribbonName, "建築"); }
            catch
            {
                ribbonPanel = application.GetRibbonPanels(ribbonName).Find(panel => panel.Name == "建築");
            }
            PushButton spacingTagBtn = ribbonPanel.AddItem(new PushButtonData("SpacingTag", "間距標籤", addinAssmeblyPath, "TYBIM.Architecture.SpacingTag")) as PushButton;
            spacingTagBtn.LargeImage = convertFromBitmap(Properties.Resources.間距標籤);
            spacingTagBtn.ToolTip = "【功能介紹】\n依選取建築元件的模型參考面建立連續尺寸，可設定量測方向、牆面基準與放置位置。";
            spacingTagBtn.LongDescription = "【使用步驟】\n1. 開啟平面、結構平面或天花板平面視圖，點選「間距標籤」。\n2. 選擇尺寸類型、量測方向與放置方式，確認設定。\n3. 依提示選取元件或參考面；如選擇自訂方向或手動定位，依提示點選方向與位置。";
            spacingTagBtn.ToolTipImage = convertFromBitmap(Properties.Resources.間距標籤_原圖, 250);

            // 添加「數量計算」面板
            try { ribbonPanel = application.CreateRibbonPanel(ribbonName, "數量計算"); }
            catch
            {
                List<RibbonPanel> panel_list = new List<RibbonPanel>();
                panel_list = application.GetRibbonPanels(ribbonName);
                foreach (RibbonPanel rp in panel_list) { if (rp.Name == "數量計算") { ribbonPanel = rp; } }
            }
            PushButton pipeAreaBtn = ribbonPanel.AddItem(new PushButtonData("PipeArea", "管道面積", addinAssmeblyPath, "TYBIM.Calculate.PipeArea")) as PushButton;
            pipeAreaBtn.LargeImage = convertFromBitmap(Properties.Resources.管道面積);
            pipeAreaBtn.ToolTip = "【功能介紹】\n統計主模型與已載入連結模型中，所選管道、風管、電纜架及其配件的頂面面積。";
            pipeAreaBtn.LongDescription = "【使用步驟】\n1. 點選「管道面積」，勾選要統計的品類。\n2. 按「確定」執行計算。\n3. 在結果視窗查看各品類面積與總計，需要時匯出結果。";
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
