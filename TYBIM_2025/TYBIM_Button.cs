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
            // 添加「自動翻模」面板
            PushButton autoBuildBtn = ribbonPanel.AddItem(new PushButtonData("Start", "自動翻模", addinAssmeblyPath, "TYBIM_2025.Start")) as PushButton;
            autoBuildBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻模);

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
