namespace TYBIM_2025.Calculate
{
    partial class CategorySelectionForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CategorySelectionForm));
            chkSelectAll = new System.Windows.Forms.CheckBox();
            clbCategories = new System.Windows.Forms.CheckedListBox();
            btnOK = new System.Windows.Forms.Button();
            btnCancel = new System.Windows.Forms.Button();
            SuspendLayout();
            // 
            // chkSelectAll
            // 
            chkSelectAll.AutoSize = true;
            chkSelectAll.Checked = true;
            chkSelectAll.CheckState = System.Windows.Forms.CheckState.Checked;
            chkSelectAll.Location = new System.Drawing.Point(20, 15);
            chkSelectAll.Name = "chkSelectAll";
            chkSelectAll.Size = new System.Drawing.Size(103, 21);
            chkSelectAll.TabIndex = 0;
            chkSelectAll.Text = "全選 / 全取消";
            chkSelectAll.UseVisualStyleBackColor = true;
            chkSelectAll.CheckedChanged += ChkSelectAll_CheckedChanged;
            // 
            // clbCategories
            // 
            clbCategories.CheckOnClick = true;
            clbCategories.FormattingEnabled = true;
            clbCategories.Location = new System.Drawing.Point(20, 45);
            clbCategories.Name = "clbCategories";
            clbCategories.Size = new System.Drawing.Size(345, 251);
            clbCategories.TabIndex = 1;
            // 
            // btnOK
            // 
            btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            btnOK.Location = new System.Drawing.Point(175, 325);
            btnOK.Name = "btnOK";
            btnOK.Size = new System.Drawing.Size(90, 32);
            btnOK.TabIndex = 2;
            btnOK.Text = "確定";
            btnOK.UseVisualStyleBackColor = true;
            btnOK.Click += BtnOK_Click;
            // 
            // btnCancel
            // 
            btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            btnCancel.Location = new System.Drawing.Point(275, 325);
            btnCancel.Name = "btnCancel";
            btnCancel.Size = new System.Drawing.Size(90, 32);
            btnCancel.TabIndex = 3;
            btnCancel.Text = "取消";
            btnCancel.UseVisualStyleBackColor = true;
            // 
            // CategorySelectionForm
            // 
            AcceptButton = btnOK;
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 17F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            CancelButton = btnCancel;
            ClientSize = new System.Drawing.Size(384, 381);
            Controls.Add(btnCancel);
            Controls.Add(btnOK);
            Controls.Add(clbCategories);
            Controls.Add(chkSelectAll);
            Font = new System.Drawing.Font("Microsoft JhengHei UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, 136);
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            Icon = (System.Drawing.Icon)resources.GetObject("$this.Icon");
            Margin = new System.Windows.Forms.Padding(4);
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "CategorySelectionForm";
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            Text = "MEP 頂面面積計算 - 選擇品類";
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox chkSelectAll;
        private System.Windows.Forms.CheckedListBox clbCategories;
        private System.Windows.Forms.Button btnOK;
        private System.Windows.Forms.Button btnCancel;
    }
}