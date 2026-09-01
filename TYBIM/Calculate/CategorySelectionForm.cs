using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Autodesk.Revit.DB;
using Form = System.Windows.Forms.Form;

namespace TYBIM.Calculate
{
    public partial class CategorySelectionForm : Form
    {
        public List<BuiltInCategory> SelectedCategories { get; private set; }

        /// <summary>
        /// 設計時 (Design-Time) 建構函式 - 供 Visual Studio Form Designer 使用
        /// </summary>
        public CategorySelectionForm()
        {
            InitializeComponent();
            SelectedCategories = new List<BuiltInCategory>();
            PopulateCategories(GetDefaultCategories());
        }

        /// <summary>
        /// 執行時 (Runtime) 建構函式
        /// </summary>
        public CategorySelectionForm(List<BuiltInCategory> categories)
        {
            InitializeComponent();
            SelectedCategories = new List<BuiltInCategory>();
            PopulateCategories(categories ?? GetDefaultCategories());
        }

        private void PopulateCategories(List<BuiltInCategory> categories)
        {
            clbCategories.Items.Clear();
            if (categories == null) return;

            foreach (var cat in categories)
            {
                var stat = new CategoryStat(cat);
                clbCategories.Items.Add(stat, true);
            }
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