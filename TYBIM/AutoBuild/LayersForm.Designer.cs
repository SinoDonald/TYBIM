namespace TYBIM.AutoBuild
{
    partial class LayersForm
    {
        private System.ComponentModel.IContainer components = null;
        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            var resources = new System.ComponentModel.ComponentResourceManager(typeof(LayersForm));
            this.cancelBtn = new System.Windows.Forms.Button();
            this.sureBtn = new System.Windows.Forms.Button();
            this.allCancelRbtn = new System.Windows.Forms.RadioButton();
            this.allRbtn = new System.Windows.Forms.RadioButton();
            this.listView1 = new System.Windows.Forms.ListView();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.byLevelCB = new System.Windows.Forms.CheckBox();
            this.t_level_comboBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.b_level_comboBox = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.type_comboBox = new System.Windows.Forms.ComboBox();
            this.groupBox1.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();

            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Text = "取消";
            this.cancelBtn.Location = new System.Drawing.Point(508, 310);
            this.cancelBtn.Size = new System.Drawing.Size(96, 34);
            this.cancelBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this.cancelBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.cancelBtn.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(205, 212, 220);
            this.cancelBtn.BackColor = System.Drawing.Color.White;
            this.cancelBtn.TabIndex = 3;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);

            this.sureBtn.Name = "sureBtn";
            this.sureBtn.Text = "確定";
            this.sureBtn.Location = new System.Drawing.Point(400, 310);
            this.sureBtn.Size = new System.Drawing.Size(96, 34);
            this.sureBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this.sureBtn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.sureBtn.FlatAppearance.BorderSize = 0;
            this.sureBtn.BackColor = System.Drawing.Color.FromArgb(35, 105, 175);
            this.sureBtn.ForeColor = System.Drawing.Color.White;
            this.sureBtn.TabIndex = 2;
            this.sureBtn.Click += new System.EventHandler(this.sureBtn_Click);

            this.allRbtn.Name = "allRbtn";
            this.allRbtn.Text = "全選";
            this.allRbtn.AutoSize = true;
            this.allRbtn.Checked = true;
            this.allRbtn.Location = new System.Drawing.Point(14, 26);
            this.allRbtn.TabIndex = 0;
            this.allRbtn.CheckedChanged += new System.EventHandler(this.allRbtn_CheckedChanged);
            this.allCancelRbtn.Name = "allCancelRbtn";
            this.allCancelRbtn.Text = "全部取消";
            this.allCancelRbtn.AutoSize = true;
            this.allCancelRbtn.Location = new System.Drawing.Point(88, 26);
            this.allCancelRbtn.TabIndex = 1;
            this.allCancelRbtn.CheckedChanged += new System.EventHandler(this.allCancelRbtn_CheckedChanged);

            this.listView1.Name = "listView1";
            this.listView1.Location = new System.Drawing.Point(14, 56);
            this.listView1.Size = new System.Drawing.Size(254, 216);
            this.listView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.listView1.CheckBoxes = true;
            this.listView1.HideSelection = false;
            this.listView1.FullRowSelect = true;
            this.listView1.MultiSelect = false;
            this.listView1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.Details;
            this.listView1.TabIndex = 2;
            this.listView1.SelectedIndexChanged += new System.EventHandler(this.listView1_SelectedIndexChanged);

            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Text = "DWG 圖層";
            this.groupBox1.Location = new System.Drawing.Point(16, 16);
            this.groupBox1.Size = new System.Drawing.Size(282, 286);
            this.groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            this.groupBox1.BackColor = System.Drawing.Color.White;
            this.groupBox1.TabIndex = 0;
            this.groupBox1.Controls.Add(this.allRbtn);
            this.groupBox1.Controls.Add(this.allCancelRbtn);
            this.groupBox1.Controls.Add(this.listView1);

            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Text = "建立設定";
            this.groupBox3.Location = new System.Drawing.Point(310, 16);
            this.groupBox3.Size = new System.Drawing.Size(294, 286);
            this.groupBox3.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            this.groupBox3.BackColor = System.Drawing.Color.White;
            this.groupBox3.TabIndex = 1;
            this.groupBox3.Controls.Add(this.label1);
            this.groupBox3.Controls.Add(this.b_level_comboBox);
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Controls.Add(this.t_level_comboBox);
            this.groupBox3.Controls.Add(this.label3);
            this.groupBox3.Controls.Add(this.type_comboBox);
            this.groupBox3.Controls.Add(this.byLevelCB);

            this.label1.Name = "label1";
            this.label1.Text = "基準樓層";
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 28);
            this.label2.Name = "label2";
            this.label2.Text = "頂部樓層";
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 94);
            this.label3.Name = "label3";
            this.label3.Text = "類型選擇";
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 160);

            this.b_level_comboBox.Name = "b_level_comboBox";
            this.b_level_comboBox.Location = new System.Drawing.Point(14, 52);
            this.b_level_comboBox.Size = new System.Drawing.Size(266, 25);
            this.b_level_comboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.b_level_comboBox.FormattingEnabled = true;
            this.b_level_comboBox.IntegralHeight = false;
            this.b_level_comboBox.DropDownHeight = 180;
            this.b_level_comboBox.TabIndex = 0;
            this.t_level_comboBox.Name = "t_level_comboBox";
            this.t_level_comboBox.Location = new System.Drawing.Point(14, 118);
            this.t_level_comboBox.Size = new System.Drawing.Size(266, 25);
            this.t_level_comboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.t_level_comboBox.FormattingEnabled = true;
            this.t_level_comboBox.IntegralHeight = false;
            this.t_level_comboBox.DropDownHeight = 180;
            this.t_level_comboBox.TabIndex = 1;
            this.type_comboBox.Name = "type_comboBox";
            this.type_comboBox.Location = new System.Drawing.Point(14, 184);
            this.type_comboBox.Size = new System.Drawing.Size(266, 25);
            this.type_comboBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.type_comboBox.FormattingEnabled = true;
            this.type_comboBox.IntegralHeight = false;
            this.type_comboBox.DropDownHeight = 220;
            this.type_comboBox.TabIndex = 2;

            this.byLevelCB.Name = "byLevelCB";
            this.byLevelCB.Text = "分樓層建立";
            this.byLevelCB.AutoSize = true;
            this.byLevelCB.Location = new System.Drawing.Point(14, 234);
            this.byLevelCB.TabIndex = 3;

            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Regular);
            this.BackColor = System.Drawing.Color.FromArgb(245, 247, 250);
            this.ForeColor = System.Drawing.Color.FromArgb(45, 55, 68);
            this.ClientSize = new System.Drawing.Size(620, 360);
            this.MinimumSize = new System.Drawing.Size(636, 399);
            this.MaximizeBox = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "LayersForm";
            this.Text = "自動翻模";
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.sureBtn);
            this.Controls.Add(this.cancelBtn);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.Button sureBtn;
        private System.Windows.Forms.RadioButton allCancelRbtn;
        private System.Windows.Forms.RadioButton allRbtn;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.CheckBox byLevelCB;
        private System.Windows.Forms.ComboBox t_level_comboBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox b_level_comboBox;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox type_comboBox;
    }
}
