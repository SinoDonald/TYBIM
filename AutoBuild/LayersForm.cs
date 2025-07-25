using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoBuild
{
    public partial class LayersForm : System.Windows.Forms.Form
    {
        //外部事件處理:讀取其他的cs檔
        ExternalEvent m_externalEvent_CreateWalls; // 自動翻牆

        RadioButton[] radioButtons = new RadioButton[] { };
        Options options = new Options
        {
            ComputeReferences = true, // 讓 GeometryObject 產生 Reference
            IncludeNonVisibleObjects = true,
            DetailLevel = ViewDetailLevel.Fine
        };
        /// <summary>
        /// 自訂ListView滾輪只有上下滑動
        /// </summary>
        public class NativeMethods
        {
            public const int GWL_STYLE = -16;
            public const int WS_HSCROLL = 0x00100000;
            [DllImport("user32.dll")]
            public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
            [DllImport("user32.dll")]
            public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        }

        public static List<(string, Curve, Reference)> result = new List<(string, Curve, Reference)>();
        public static List<string> layers = new List<string>(); // 儲存圖層名稱

        private void HideHorizontalScrollBar(ListView listView)
        {
            int style = NativeMethods.GetWindowLong(listView.Handle, NativeMethods.GWL_STYLE);
            NativeMethods.SetWindowLong(listView.Handle, NativeMethods.GWL_STYLE, style & ~NativeMethods.WS_HSCROLL);
        }
        public LayersForm(UIDocument uidoc)
        {
            InitializeComponent();

            IExternalEventHandler handler_CreateWalls = new CreateWalls(); // 自動翻牆
            ExternalEvent externalEvent_CreateWalls = ExternalEvent.Create(handler_CreateWalls);
            m_externalEvent_CreateWalls = externalEvent_CreateWalls;

            layers.Clear(); // 清空圖層名稱
            layers = GetCADLayerLines(uidoc.Document); // 取得CAD圖層線條
            CreateLayerNames(layers); // 建立圖層名稱
            CreateRadioButton(); // 新增RadioButton
            CenterToParent(); // 視窗置中

            listView1.View = System.Windows.Forms.View.Details;
            foreach (ColumnHeader column in listView1.Columns) { column.Width = listView1.ClientSize.Width / listView1.Columns.Count; }
            HideHorizontalScrollBar(listView1); // 自訂ListView滾輪只有上下滑動
        }
        /// <summary>
        /// 取得CAD圖層線條
        /// </summary>
        /// <param name="doc"></param>
        /// <returns></returns>
        private List<string> GetCADLayerLines(Document doc)
        {
            List<ImportInstance> importInstances = new FilteredElementCollector(doc).OfClass(typeof(ImportInstance)).Cast<ImportInstance>().ToList();
            foreach (ImportInstance importInstance in importInstances)
            {
                if (importInstance.IsLinked)
                {
                    //CategoryNameMap categorys = importInstance.Category.SubCategories;
                    //foreach (Category category in categorys)
                    //{
                    //    layers.Add(category.Name);
                    //}

                    GeometryElement geomElement = importInstance.get_Geometry(options);
                    foreach (GeometryObject geomObj in geomElement)
                    {
                        if (geomObj is GeometryInstance geomInstance)
                        {
                            GeometryElement instanceGeom = geomInstance.GetInstanceGeometry();
                            foreach (GeometryObject obj in instanceGeom)
                            {
                                if (obj is Curve curve)
                                {
                                    // 取得圖層(實際是 GraphicsStyle 對應到 DWG 圖層)
                                    ElementId styleId = curve.GraphicsStyleId;
                                    GraphicsStyle style = doc.GetElement(styleId) as GraphicsStyle;
                                    string layerName = style?.GraphicsStyleCategory?.Name;

                                    if (!String.IsNullOrEmpty(layerName) /*&& layerName.Contains("WALL")*/)
                                    {
                                        Reference reference = curve.Reference;
                                        result.Add((layerName, curve, reference));
                                        layers.Add(layerName);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return layers;
        }
        /// <summary>
        /// 建立圖層名稱
        /// </summary>
        /// <param name="layers"></param>
        private void CreateLayerNames(List<string> layers)
        {
            listView1.Columns.Clear(); // 清空欄位
            listView1.Items.Clear(); // 清空節點
            try
            {
                listView1.Columns.Add("圖層名稱");
                foreach (string layer in layers)
                {
                    listView1.Items.Add(layer);
                }
            }
            catch(Exception ex)
            {
                MessageBox.Show("建立圖層名稱時發生錯誤: " + ex.Message);
            }
            listView1.View = System.Windows.Forms.View.List;
        }
        /// <summary>
        /// 新增RadioButton
        /// </summary>
        private void CreateRadioButton()
        {
            List<string> createElemTypes = new List<string>() { "柱", "樑", "板", "牆" };
            this.radioButtons = new RadioButton[createElemTypes.Count];
            for (int i = 0; i < createElemTypes.Count; i++)
            {
                radioButtons[i] = new RadioButton();
                radioButtons[i].Font = new Font("微軟正黑體", 10, FontStyle.Regular);
                radioButtons[i].Text = createElemTypes[i];
                radioButtons[i].AutoSize = true;
                radioButtons[i].Location = new System.Drawing.Point(5, 5 + i * 25);
                radioBtnPanel.Controls.Add(radioButtons[i]);
                if (i == 0) { radioButtons[0].Checked = true; } // 預設第一個                
            }
        }
        // 全選
        private void allRbtn_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                listView1.Items[i].Checked = true;
            }
        }
        // 全部取消
        private void allCancelRbtn_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < listView1.Items.Count; i++)
            {
                listView1.Items[i].Checked = false;
            }
        }
        // 選取文字即勾選
        private void listView1_SelectedIndexChanged(object sender, EventArgs e)
        {
            ListView selectListView = sender as ListView;
            ListViewItem focusedItem = selectListView.FocusedItem;
            if (selectListView.SelectedItems.Count > 0)
            {
                if (focusedItem.Checked == true)
                {
                    focusedItem.Checked = false;
                }
                else
                {
                    focusedItem.Checked = true;
                }
            }
        }
        // 確定
        private void sureBtn_Click(object sender, System.EventArgs e)
        {
            m_externalEvent_CreateWalls.Raise(); // 自動翻牆
            Close();
        }
        // 取消
        private void cancelBtn_Click(object sender, System.EventArgs e)
        {
            Close();
        }
    }
}
