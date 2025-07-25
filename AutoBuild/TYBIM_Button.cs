using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AutoBuild
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
            //// 2020
            //string autoBuildAsb = Path.Combine(Directory.GetParent(addinAssmeblyPath).FullName, "AutoBuild_Old.dll"); // 快速翻模

            // 2024
            string autoBuildAsb = Path.Combine(Directory.GetParent(addinAssmeblyPath).FullName, "AutoBuild.dll"); // 快速翻模

            // 創建一個新的選單
            RibbonPanel ribbonPanel = null;
            try { application.CreateRibbonTab("拓源數位"); } catch { }
            try { ribbonPanel = application.CreateRibbonPanel("拓源數位", "自動翻模"); }
            catch
            {
                List<RibbonPanel> panel_list = new List<RibbonPanel>();
                panel_list = application.GetRibbonPanels("拓源數位");
                foreach (RibbonPanel rp in panel_list)
                {
                    if (rp.Name == "自動翻模")
                    {
                        ribbonPanel = rp;
                    }
                }
            }
            // 添加「圖紙更新」面板
            PushButton sinotechBtn = ribbonPanel.AddItem(new PushButtonData("Start", "自動翻模", addinAssmeblyPath, "AutoBuild.Start")) as PushButton;
            sinotechBtn.LargeImage = convertFromBitmap(Properties.Resources.自動翻模);

            return Result.Succeeded;
        }
        /// <summary>
        /// 轉換圖片
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        BitmapSource convertFromBitmap(System.Drawing.Bitmap bitmap)
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                bitmap.GetHbitmap(),
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
