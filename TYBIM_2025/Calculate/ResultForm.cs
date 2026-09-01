using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TYBIM_2025.Calculate
{
    public partial class ResultForm : Form
    {
        private List<CategoryStat> _stats;

        /// <summary>
        /// 設計時 (Design-Time) 建構函式 - 供 Visual Studio Designer 檢視樣式
        /// </summary>
        public ResultForm()
        {
            InitializeComponent();
            _stats = new List<CategoryStat>();
            InitializeGridColumns();
        }

        /// <summary>
        /// 執行時 (Runtime) 建構函式
        /// </summary>
        public ResultForm(List<CategoryStat> stats)
        {
            InitializeComponent();
            _stats = stats ?? new List<CategoryStat>();
            InitializeGridColumns();
            BindData(_stats);
        }

        private void InitializeGridColumns()
        {
            dgvResults.Columns.Clear();
            dgvResults.Columns.Add("CategoryName", "品類名稱");
            dgvResults.Columns.Add("Count", "元件數量");
            dgvResults.Columns.Add("AreaSqM", "頂面總面積 (m²)");
        }

        private void BindData(List<CategoryStat> stats)
        {
            dgvResults.Rows.Clear();
            int grandCount = 0;
            double grandSqM = 0;

            foreach (var stat in stats)
            {
                dgvResults.Rows.Add(
                    stat.CategoryName,
                    stat.Count.ToString("N0"),
                    stat.TotalTopAreaSquareMeters.ToString("N3")
                );

                grandCount += stat.Count;
                grandSqM += stat.TotalTopAreaSquareMeters;
            }

            lblSummary.Text = $"【總計】元件總數：{grandCount:N0} 個 | 全品類頂面總面積：{grandSqM:N3} m²";
        }

        private void BtnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Title = "選擇匯出統計結果路徑";
                saveFileDialog.Filter = "CSV 逗號分隔檔 (*.csv)|*.csv|文字檔案 (*.txt)|*.txt";
                saveFileDialog.FileName = $"MEP_TopArea_Summary_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        var sb = new StringBuilder();
                        sb.AppendLine("品類名稱,元件數量,頂面總面積(m2)");

                        int totalCount = 0;
                        double totalAreaSqM = 0;

                        foreach (var stat in _stats)
                        {
                            sb.AppendLine($"\"{stat.CategoryName}\",{stat.Count},{stat.TotalTopAreaSquareMeters:F3}");
                            totalCount += stat.Count;
                            totalAreaSqM += stat.TotalTopAreaSquareMeters;
                        }

                        sb.AppendLine();
                        sb.AppendLine($"\"總計\",{totalCount},{totalAreaSqM:F3}");

                        File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

                        MessageBox.Show($"統計結果已成功匯出至：\n{saveFileDialog.FileName}", "匯出成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"匯出檔案時發生錯誤：\n{ex.Message}", "匯出失敗", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
    }
}