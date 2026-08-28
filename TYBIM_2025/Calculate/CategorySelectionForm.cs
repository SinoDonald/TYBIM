using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Form = System.Windows.Forms.Form;
using Point = System.Drawing.Point;

namespace TYBIM_2025.Calculate
{
    public class CategorySelectionForm : Form
    {
        private CheckedListBox clbCategories;
        private Button btnOK;
        private Button btnCancel;
        private CheckBox chkSelectAll;

        public List<BuiltInCategory> SelectedCategories { get; private set; }

        /// <summary>
        /// 設計時 (Design-Time) 建構函式 - 供 Visual Studio Form Designer 使用
        /// </summary>
        public CategorySelectionForm()
        {
            SelectedCategories = new List<BuiltInCategory>();
            InitializeComponent(GetDefaultCategories());
        }

        /// <summary>
        /// 執行時 (Runtime) 建構函式
        /// </summary>
        public CategorySelectionForm(List<BuiltInCategory> categories)
        {
            SelectedCategories = new List<BuiltInCategory>();
            InitializeComponent(categories);
        }

        private List<BuiltInCategory> GetDefaultCategories()
        {
            return new List<BuiltInCategory>
            {
                BuiltInCategory.OST_PipeCurves,
                BuiltInCategory.OST_PipeFitting,
                BuiltInCategory.OST_DuctCurves,
                BuiltInCategory.OST_DuctFitting,
                BuiltInCategory.OST_CableTray,
                BuiltInCategory.OST_CableTrayFitting
            };
        }

        private void InitializeComponent(List<BuiltInCategory> categories)
        {
            this.Text = "MEP 頂面面積計算 - 選擇品類";
            this.Size = new Size(400, 420);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Microsoft JhengHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

            chkSelectAll = new CheckBox
            {
                Text = "全選 / 全取消",
                Location = new Point(20, 15),
                AutoSize = true,
                Checked = true
            };
            chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;

            clbCategories = new CheckedListBox
            {
                Location = new Point(20, 45),
                Size = new Size(345, 260),
                CheckOnClick = true
            };

            if (categories != null)
            {
                foreach (var cat in categories)
                {
                    var stat = new CategoryStat(cat);
                    // 加入物件項目，CheckedListBox 會自動呼叫 CategoryStat.ToString() 顯示中文名稱
                    clbCategories.Items.Add(stat, true);
                }
            }

            btnOK = new Button
            {
                Text = "確定",
                DialogResult = DialogResult.OK,
                Location = new Point(175, 325),
                Size = new Size(90, 32)
            };
            btnOK.Click += BtnOK_Click;

            btnCancel = new Button
            {
                Text = "取消",
                DialogResult = DialogResult.Cancel,
                Location = new Point(275, 325),
                Size = new Size(90, 32)
            };

            this.Controls.Add(chkSelectAll);
            this.Controls.Add(clbCategories);
            this.Controls.Add(btnOK);
            this.Controls.Add(btnCancel);
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
        }

        private void ChkSelectAll_CheckedChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < clbCategories.Items.Count; i++)
            {
                clbCategories.SetItemChecked(i, chkSelectAll.Checked);
            }
        }

        private void BtnOK_Click(object sender, EventArgs e)
        {
            SelectedCategories.Clear();
            foreach (var item in clbCategories.CheckedItems)
            {
                if (item is CategoryStat stat)
                {
                    SelectedCategories.Add(stat.Category);
                }
            }
        }
    }
}