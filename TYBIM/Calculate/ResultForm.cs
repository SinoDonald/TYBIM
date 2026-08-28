using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace TYBIM.Calculate
{
    public class ResultForm : Form
    {
        private DataGridView dgvResults;
        private Label lblSummary;
        private Button btnExport;
        private Button btnClose;
        private List<CategoryStat> _stats;

        /// <summary>
        /// 設計時 (Design-Time) 建構函式 - 供 Visual Studio Designer 雙擊檢視樣式
        /// </summary>
        public ResultForm()
        {
            _stats = new List<CategoryStat>();
            InitializeComponent();
        }

        /// <summary>
        /// 執行時 (Runtime) 建構函式
        /// </summary>
        public ResultForm(List<CategoryStat> stats)
        {
            _stats = stats ?? new List<CategoryStat>();
            InitializeComponent();
            BindData(_stats);
        }

        private void InitializeComponent()
        {
            this.Text = "MEP 頂面面積加總統計結果";
            this.Size = new Size(580, 450);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Font = new Font("Microsoft JhengHei UI", 9.5F, FontStyle.Regular, GraphicsUnit.Point);

            dgvResults = new DataGridView
            {
                Location = new Point(20, 20),
                Size = new Size(525, 270),
                ReadOnly = true,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                RowHeadersVisible = false
            };

            lblSummary = new Label
            {
                Location = new Point(20, 305),
                Size = new Size(525, 30),
                Font = new Font("Microsoft JhengHei UI", 10F, FontStyle.Bold),
                ForeColor = Color.DarkBlue
            };

            btnExport = new Button
            {
                Text = "匯出結果...",
                Location = new Point(340, 355),
                Size = new Size(100, 32)
            };
            btnExport.Click += BtnExport_Click;

            btnClose = new Button
            {
                Text = "關閉",
                Location = new Point(450, 355),
                Size = new Size(95, 32)
            };
            btnClose.Click += (s, e) => this.Close();

            this.Controls.Add(dgvResults);
            this.Controls.Add(lblSummary);
            this.Controls.Add(btnExport);
            this.Controls.Add(btnClose);
        }

        private void BindData(List<CategoryStat> stats)
        {
            dgvResults.Columns.Clear();
            dgvResults.Columns.Add("CategoryName", "品類名稱");
            dgvResults.Columns.Add("Count", "元件數量");
            dgvResults.Columns.Add("AreaSqM", "頂面總面積 (m²)");

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

        /// <summary>
        /// 彈出 SaveFileDialog 供使用者自由指定儲存路徑並輸出報告
        /// </summary>
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
                        StringBuilder sb = new StringBuilder();

                        // 寫入 CSV/TXT 標頭
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

                        // 使用 UTF-8 帶 BOM 編碼寫入，確保 Excel 開啟 CSV 中文不亂碼
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