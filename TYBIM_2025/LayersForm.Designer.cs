namespace TYBIM_2025
{
    partial class LayersForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(LayersForm));
            this.cancelBtn = new System.Windows.Forms.Button();
            this.sureBtn = new System.Windows.Forms.Button();
            this.radioBtnPanel = new System.Windows.Forms.Panel();
            this.allCancelRbtn = new System.Windows.Forms.RadioButton();
            this.allRbtn = new System.Windows.Forms.RadioButton();
            this.listView1 = new System.Windows.Forms.ListView();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.t_level_comboBox = new System.Windows.Forms.ComboBox();
            this.label3 = new System.Windows.Forms.Label();
            this.b_level_comboBox = new System.Windows.Forms.ComboBox();
            this.label2 = new System.Windows.Forms.Label();
            this.byLevelCB = new System.Windows.Forms.CheckBox();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // cancelBtn
            // 
            this.cancelBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cancelBtn.Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.cancelBtn.Location = new System.Drawing.Point(449, 316);
            this.cancelBtn.Margin = new System.Windows.Forms.Padding(4);
            this.cancelBtn.Name = "cancelBtn";
            this.cancelBtn.Size = new System.Drawing.Size(80, 35);
            this.cancelBtn.TabIndex = 0;
            this.cancelBtn.Text = "取消";
            this.cancelBtn.UseVisualStyleBackColor = true;
            this.cancelBtn.Click += new System.EventHandler(this.cancelBtn_Click);
            // 
            // sureBtn
            // 
            this.sureBtn.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.sureBtn.Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.sureBtn.Location = new System.Drawing.Point(361, 316);
            this.sureBtn.Margin = new System.Windows.Forms.Padding(4);
            this.sureBtn.Name = "sureBtn";
            this.sureBtn.Size = new System.Drawing.Size(80, 35);
            this.sureBtn.TabIndex = 0;
            this.sureBtn.Text = "確定";
            this.sureBtn.UseVisualStyleBackColor = true;
            this.sureBtn.Click += new System.EventHandler(this.sureBtn_Click);
            // 
            // radioBtnPanel
            // 
            this.radioBtnPanel.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.radioBtnPanel.Location = new System.Drawing.Point(6, 49);
            this.radioBtnPanel.Name = "radioBtnPanel";
            this.radioBtnPanel.Size = new System.Drawing.Size(127, 240);
            this.radioBtnPanel.TabIndex = 2;
            // 
            // allCancelRbtn
            // 
            this.allCancelRbtn.AutoSize = true;
            this.allCancelRbtn.Location = new System.Drawing.Point(73, 24);
            this.allCancelRbtn.Name = "allCancelRbtn";
            this.allCancelRbtn.Size = new System.Drawing.Size(78, 21);
            this.allCancelRbtn.TabIndex = 4;
            this.allCancelRbtn.Text = "全部取消";
            this.allCancelRbtn.UseVisualStyleBackColor = true;
            this.allCancelRbtn.CheckedChanged += new System.EventHandler(this.allCancelRbtn_CheckedChanged);
            // 
            // allRbtn
            // 
            this.allRbtn.AutoSize = true;
            this.allRbtn.Checked = true;
            this.allRbtn.Location = new System.Drawing.Point(6, 24);
            this.allRbtn.Name = "allRbtn";
            this.allRbtn.Size = new System.Drawing.Size(52, 21);
            this.allRbtn.TabIndex = 5;
            this.allRbtn.TabStop = true;
            this.allRbtn.Text = "全選";
            this.allRbtn.UseVisualStyleBackColor = true;
            this.allRbtn.CheckedChanged += new System.EventHandler(this.allRbtn_CheckedChanged);
            // 
            // listView1
            // 
            this.listView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listView1.CheckBoxes = true;
            this.listView1.HideSelection = false;
            this.listView1.Location = new System.Drawing.Point(6, 49);
            this.listView1.Name = "listView1";
            this.listView1.Size = new System.Drawing.Size(200, 240);
            this.listView1.TabIndex = 6;
            this.listView1.UseCompatibleStateImageBehavior = false;
            this.listView1.View = System.Windows.Forms.View.SmallIcon;
            this.listView1.SelectedIndexChanged += new System.EventHandler(this.listView1_SelectedIndexChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox1.Controls.Add(this.allRbtn);
            this.groupBox1.Controls.Add(this.listView1);
            this.groupBox1.Controls.Add(this.allCancelRbtn);
            this.groupBox1.Location = new System.Drawing.Point(12, 12);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(212, 295);
            this.groupBox1.TabIndex = 7;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "請選擇要翻模的線";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox2.Controls.Add(this.radioBtnPanel);
            this.groupBox2.Location = new System.Drawing.Point(230, 12);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(139, 295);
            this.groupBox2.TabIndex = 7;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "請選擇要翻模的類型";
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.groupBox3.Controls.Add(this.byLevelCB);
            this.groupBox3.Controls.Add(this.t_level_comboBox);
            this.groupBox3.Controls.Add(this.label3);
            this.groupBox3.Controls.Add(this.b_level_comboBox);
            this.groupBox3.Controls.Add(this.label2);
            this.groupBox3.Location = new System.Drawing.Point(375, 12);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(153, 295);
            this.groupBox3.TabIndex = 8;
            this.groupBox3.TabStop = false;
            // 
            // t_level_comboBox
            // 
            this.t_level_comboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.t_level_comboBox.FormattingEnabled = true;
            this.t_level_comboBox.Location = new System.Drawing.Point(7, 165);
            this.t_level_comboBox.Name = "t_level_comboBox";
            this.t_level_comboBox.Size = new System.Drawing.Size(140, 25);
            this.t_level_comboBox.TabIndex = 10;
            this.t_level_comboBox.Text = "請選擇頂部樓層";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.label3.Font = new System.Drawing.Font("微軟正黑體", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label3.Location = new System.Drawing.Point(28, 124);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(96, 26);
            this.label3.TabIndex = 9;
            this.label3.Text = "頂部樓層";
            // 
            // b_level_comboBox
            // 
            this.b_level_comboBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.b_level_comboBox.FormattingEnabled = true;
            this.b_level_comboBox.Location = new System.Drawing.Point(7, 91);
            this.b_level_comboBox.Name = "b_level_comboBox";
            this.b_level_comboBox.Size = new System.Drawing.Size(140, 25);
            this.b_level_comboBox.TabIndex = 8;
            this.b_level_comboBox.Text = "請選擇基準樓層";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.BackColor = System.Drawing.SystemColors.ActiveCaption;
            this.label2.Font = new System.Drawing.Font("微軟正黑體", 15.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.label2.Location = new System.Drawing.Point(28, 49);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(96, 26);
            this.label2.TabIndex = 3;
            this.label2.Text = "基準樓層";
            // 
            // byLevelCB
            // 
            this.byLevelCB.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.byLevelCB.AutoSize = true;
            this.byLevelCB.Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.byLevelCB.Location = new System.Drawing.Point(32, 206);
            this.byLevelCB.Name = "byLevelCB";
            this.byLevelCB.Size = new System.Drawing.Size(92, 21);
            this.byLevelCB.TabIndex = 11;
            this.byLevelCB.Text = "分樓層建立";
            this.byLevelCB.UseVisualStyleBackColor = true;
            // 
            // LayersForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(542, 361);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.sureBtn);
            this.Controls.Add(this.cancelBtn);
            this.Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(136)));
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.MinimumSize = new System.Drawing.Size(400, 400);
            this.Name = "LayersForm";
            this.Text = "自動翻模";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cancelBtn;
        private System.Windows.Forms.Button sureBtn;
        private System.Windows.Forms.Panel radioBtnPanel;
        private System.Windows.Forms.RadioButton allCancelRbtn;
        private System.Windows.Forms.RadioButton allRbtn;
        private System.Windows.Forms.ListView listView1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.ComboBox t_level_comboBox;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.ComboBox b_level_comboBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox byLevelCB;
    }
}