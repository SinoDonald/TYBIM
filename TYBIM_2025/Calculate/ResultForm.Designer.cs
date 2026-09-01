namespace TYBIM_2025.Calculate
{
    partial class ResultForm
    {
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清除任何使用中的資源。
        /// </summary>
        /// <param name="disposing">如果應該處置受控資源則為 true，否則為 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 設計工具產生的程式碼

        /// <summary>
        /// 此為設計工具支援所需的方法 - 請勿使用程式碼編輯器修改
        /// 這個方法的內容。
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(ResultForm));
            dgvResults = new System.Windows.Forms.DataGridView();
            lblSummary = new System.Windows.Forms.Label();
            btnExport = new System.Windows.Forms.Button();
            btnClose = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)dgvResults).BeginInit();
            SuspendLayout();
            // 
            // dgvResults
            // 
            dgvResults.AllowUserToAddRows = false;
            dgvResults.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dgvResults.BackgroundColor = System.Drawing.Color.White;
            dgvResults.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvResults.Location = new System.Drawing.Point(20, 20);
            dgvResults.Name = "dgvResults";
            dgvResults.ReadOnly = true;
            dgvResults.RowHeadersVisible = false;
            dgvResults.RowTemplate.Height = 24;
            dgvResults.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgvResults.Size = new System.Drawing.Size(525, 270);
            dgvResults.TabIndex = 0;
            // 
            // lblSummary
            // 
            lblSummary.Font = new System.Drawing.Font("Microsoft JhengHei UI", 10F, System.Drawing.FontStyle.Bold);
            lblSummary.ForeColor = System.Drawing.Color.DarkBlue;
            lblSummary.Location = new System.Drawing.Point(20, 305);
            lblSummary.Name = "lblSummary";
            lblSummary.Size = new System.Drawing.Size(525, 30);
            lblSummary.TabIndex = 1;
            lblSummary.Text = "【總計】元件總數：0 個 | 全品類頂面總面積：0.000 m²";
            // 
            // btnExport
            // 
            btnExport.Location = new System.Drawing.Point(340, 355);
            btnExport.Name = "btnExport";
            btnExport.Size = new System.Drawing.Size(100, 32);
            btnExport.TabIndex = 2;
            btnExport.Text = "匯出結果...";
            btnExport.UseVisualStyleBackColor = true;
            btnExport.Click += BtnExport_Click;
            // 
            // btnClose
            // 
            btnClose.Location = new System.Drawing.Point(450, 355);
            btnClose.Name = "btnClose";
            btnClose.Size = new System.Drawing.Size(95, 32);
            btnClose.TabIndex = 3;
            btnClose.Text = "關閉";
            btnClose.UseVisualStyleBackColor = true;
            btnClose.Click += BtnClose_Click;
            // 
            // ResultForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(564, 411);
            Controls.Add(btnClose);
            Controls.Add(btnExport);
            Controls.Add(lblSummary);
            Controls.Add(dgvResults);
            Font = new System.Drawing.Font("Microsoft JhengHei UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 136);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            MaximizeBox = false;
            Name = "ResultForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "MEP 頂面面積加總統計結果";
            ((System.ComponentModel.ISupportInitialize)dgvResults).EndInit();
            ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvResults;
        private System.Windows.Forms.Label lblSummary;
        private System.Windows.Forms.Button btnExport;
        private System.Windows.Forms.Button btnClose;
    }
}