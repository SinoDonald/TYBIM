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
            cancelBtn = new System.Windows.Forms.Button();
            sureBtn = new System.Windows.Forms.Button();
            radioBtnPanel = new System.Windows.Forms.Panel();
            allCancelRbtn = new System.Windows.Forms.RadioButton();
            allRbtn = new System.Windows.Forms.RadioButton();
            listView1 = new System.Windows.Forms.ListView();
            groupBox1 = new System.Windows.Forms.GroupBox();
            groupBox2 = new System.Windows.Forms.GroupBox();
            groupBox1.SuspendLayout();
            groupBox2.SuspendLayout();
            SuspendLayout();
            // 
            // cancelBtn
            // 
            cancelBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            cancelBtn.Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 136);
            cancelBtn.Location = new System.Drawing.Point(291, 316);
            cancelBtn.Margin = new System.Windows.Forms.Padding(4);
            cancelBtn.Name = "cancelBtn";
            cancelBtn.Size = new System.Drawing.Size(80, 35);
            cancelBtn.TabIndex = 0;
            cancelBtn.Text = "取消";
            cancelBtn.UseVisualStyleBackColor = true;
            cancelBtn.Click += cancelBtn_Click;
            // 
            // sureBtn
            // 
            sureBtn.Anchor = System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right;
            sureBtn.Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, 136);
            sureBtn.Location = new System.Drawing.Point(203, 316);
            sureBtn.Margin = new System.Windows.Forms.Padding(4);
            sureBtn.Name = "sureBtn";
            sureBtn.Size = new System.Drawing.Size(80, 35);
            sureBtn.TabIndex = 0;
            sureBtn.Text = "確定";
            sureBtn.UseVisualStyleBackColor = true;
            sureBtn.Click += sureBtn_Click;
            // 
            // radioBtnPanel
            // 
            radioBtnPanel.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            radioBtnPanel.Location = new System.Drawing.Point(6, 49);
            radioBtnPanel.Name = "radioBtnPanel";
            radioBtnPanel.Size = new System.Drawing.Size(127, 240);
            radioBtnPanel.TabIndex = 2;
            // 
            // allCancelRbtn
            // 
            allCancelRbtn.AutoSize = true;
            allCancelRbtn.Location = new System.Drawing.Point(73, 24);
            allCancelRbtn.Name = "allCancelRbtn";
            allCancelRbtn.Size = new System.Drawing.Size(78, 21);
            allCancelRbtn.TabIndex = 4;
            allCancelRbtn.Text = "全部取消";
            allCancelRbtn.UseVisualStyleBackColor = true;
            allCancelRbtn.CheckedChanged += allCancelRbtn_CheckedChanged;
            // 
            // allRbtn
            // 
            allRbtn.AutoSize = true;
            allRbtn.Checked = true;
            allRbtn.Location = new System.Drawing.Point(6, 24);
            allRbtn.Name = "allRbtn";
            allRbtn.Size = new System.Drawing.Size(52, 21);
            allRbtn.TabIndex = 5;
            allRbtn.TabStop = true;
            allRbtn.Text = "全選";
            allRbtn.UseVisualStyleBackColor = true;
            allRbtn.CheckedChanged += allRbtn_CheckedChanged;
            // 
            // listView1
            // 
            listView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            listView1.CheckBoxes = true;
            listView1.Location = new System.Drawing.Point(6, 49);
            listView1.Name = "listView1";
            listView1.Size = new System.Drawing.Size(200, 240);
            listView1.TabIndex = 6;
            listView1.UseCompatibleStateImageBehavior = false;
            listView1.View = System.Windows.Forms.View.SmallIcon;
            listView1.SelectedIndexChanged += listView1_SelectedIndexChanged;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupBox1.Controls.Add(allRbtn);
            groupBox1.Controls.Add(listView1);
            groupBox1.Controls.Add(allCancelRbtn);
            groupBox1.Location = new System.Drawing.Point(12, 12);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new System.Drawing.Size(212, 295);
            groupBox1.TabIndex = 7;
            groupBox1.TabStop = false;
            groupBox1.Text = "請選擇要翻模的線";
            // 
            // groupBox2
            // 
            groupBox2.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            groupBox2.Controls.Add(radioBtnPanel);
            groupBox2.Location = new System.Drawing.Point(230, 12);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new System.Drawing.Size(139, 295);
            groupBox2.TabIndex = 7;
            groupBox2.TabStop = false;
            groupBox2.Text = "請選擇要翻模的類型";
            // 
            // LayersForm
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(384, 361);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(sureBtn);
            Controls.Add(cancelBtn);
            Font = new System.Drawing.Font("微軟正黑體", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 136);
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            MinimumSize = new System.Drawing.Size(400, 400);
            Name = "LayersForm";
            Text = "自動翻模";
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            groupBox2.ResumeLayout(false);
            ResumeLayout(false);

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
    }
}