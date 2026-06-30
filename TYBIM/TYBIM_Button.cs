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
            PushButton createFloorsBtn = ribbonPanel.AddItem(new PushButtonData("CreateFloors", "自動生板", addinAssmeblyPath, "TYBIM.AutoBuild.CreateFloor")) as PushButton;
            createFloorsBtn.LargeImage = convertFromBitmap(Properties.Resources.自動生板);
            PushButton createRoomWallBtn = ribbonPanel.AddItem(new PushButtonData("CreateRoomWall", "自動裝修牆", addinAssmeblyPath, "TYBIM.CreateRoomWall.RoomSelectionCommand")) as PushButton;
            createRoomWallBtn.LargeImage = convertFromBitmap(Properties.Resources.自動裝修牆);

            // 添加「CSD/SEM」面板
            try { ribbonPanel = application.CreateRibbonPanel(ribbonName, "CSD/SEM"); }
            catch
            {
                List<RibbonPanel> panel_list = new List<RibbonPanel>();
                panel_list = application.GetRibbonPanels(ribbonName);
                foreach (RibbonPanel rp in panel_list) { if (rp.Name == "CSD/SEM") { ribbonPanel = rp; } }
            }
            PushButton autoPipeOpenBtn = ribbonPanel.AddItem(new PushButtonData("AutoPipeOpen", "自動開口", addinAssmeblyPath, "TYBIM.CSDSEM.LinkOpening")) as PushButton;
            autoPipeOpenBtn.LargeImage = convertFromBitmap(Properties.Resources.自動開口);
            PushButton autoPipeTagBtn = ribbonPanel.AddItem(new PushButtonData("AutoPipeTag", "自動標籤", addinAssmeblyPath, "TYBIM.CSDSEM.AutoPipeTag")) as PushButton;
            autoPipeTagBtn.LargeImage = convertFromBitmap(Properties.Resources.自動標籤);
            PushButton tagArrayBtn = ribbonPanel.AddItem(new PushButtonData("TagArray", "標籤排序", addinAssmeblyPath, "TYBIM.CSDSEM.TagArray")) as PushButton;
            tagArrayBtn.LargeImage = convertFromBitmap(Properties.Resources.標籤排序);

            return Result.Succeeded;
        }
        /// <summary>
        /// 轉換圖片
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        BitmapSource convertFromBitmap(System.Drawing.Bitmap bitmap)
        {
            return Imaging.CreateBitmapSourceFromHBitmap(bitmap.GetHbitmap(), IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
