using System.Collections.Generic;
using Autodesk.Revit.DB;
using System.Drawing;
using Forms = System.Windows.Forms;

namespace TYBIM_2025.Architecture
{
    internal sealed class SpacingTagForm : Forms.Form
    {
        private readonly Forms.ComboBox direction = new Forms.ComboBox();
        private readonly Forms.ComboBox wallSide = new Forms.ComboBox();
        private readonly Forms.ComboBox mode = new Forms.ComboBox();
        private readonly Forms.ComboBox dimensionType = new Forms.ComboBox();
        private readonly Forms.ComboBox placement = new Forms.ComboBox();
        private readonly Forms.NumericUpDown offset = new Forms.NumericUpDown();
        private readonly Forms.CheckBox preselection = new Forms.CheckBox();
        private readonly Forms.CheckBox overall = new Forms.CheckBox();
        public int DirectionIndex { get { return direction.SelectedIndex; } }
        public int WallSideIndex { get { return wallSide.SelectedIndex; } }
        public bool ManualFaces { get { return mode.SelectedIndex == 1; } }
        public bool UsePreselection { get { return preselection.Checked; } }
        public bool OverallOnly { get { return overall.Checked; } }
        public DimensionType SelectedType { get { return (DimensionType)dimensionType.SelectedItem; } }
        public int PlacementIndex { get { return placement.SelectedIndex; } }
        public double PaperOffsetMm { get { return (double)offset.Value; } }

        public SpacingTagForm(IList<DimensionType> types, bool hasSelection)
        {
            Text = "間距標籤 — 建築連續尺寸";
            Font = new Font("Microsoft JhengHei UI", 10);
            AutoScaleMode = Forms.AutoScaleMode.Dpi;
            ClientSize = new Size(700, 650);
            FormBorderStyle = Forms.FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = Forms.FormStartPosition.CenterParent;
            var layout = new Forms.TableLayoutPanel
            {
                Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(18),
                ColumnCount = 2, RowCount = 10
            };
            layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 110));
            layout.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
            Controls.Add(layout);
            AddChoice(layout, 0, "選取方式", mode, new[] { "框選元件，自動擷取外緣", "手選參考面（精確指定量測基準）" });
            AddChoice(layout, 1, "量測方向", direction, new[] { "水平（視圖左右）", "垂直（視圖上下）", "兩點指定方向（斜向）", "點選直線／軸網決定方向" });
            AddChoice(layout, 2, "牆面基準", wallSide, new[] { "兩側／端面（含牆厚）", "內側面（依模型牆方向）", "外側面（依模型牆方向）" });
            dimensionType.DropDownStyle = Forms.ComboBoxStyle.DropDownList;
            dimensionType.DisplayMember = "Name";
            foreach (DimensionType type in types) dimensionType.Items.Add(type);
            dimensionType.SelectedIndex = 0;
            dimensionType.Dock = Forms.DockStyle.Fill;
            layout.Controls.Add(new Forms.Label { Text = "尺寸類型", AutoSize = true, Anchor = Forms.AnchorStyles.Left }, 0, 3);
            layout.Controls.Add(dimensionType, 1, 3);
            preselection.Text = "使用目前已選取的元件";
            preselection.AutoSize = true;
            preselection.Enabled = hasSelection;
            preselection.Checked = hasSelection;
            layout.Controls.Add(preselection, 1, 4);
            overall.Text = "僅建立最外兩點的總尺寸";
            overall.AutoSize = true;
            layout.Controls.Add(overall, 1, 5);
            AddChoice(layout, 6, "尺寸線位置", placement,
                new[] { "自動外側：上方", "自動外側：下方", "手動點選位置" });
            offset.Minimum = 1;
            offset.Maximum = 100;
            offset.DecimalPlaces = 1;
            offset.Value = 10;
            offset.Dock = Forms.DockStyle.Fill;
            layout.Controls.Add(new Forms.Label { Text = "圖紙偏移 mm", AutoSize = true, Anchor = Forms.AnchorStyles.Left }, 0, 7);
            layout.Controls.Add(offset, 1, 7);
            var note = new Forms.Label
            {
                Text = "選取同一組元件，依方向排序後建立連續尺寸。自動外側以所選元件包圍盒為界，偏移依視圖比例換算；斜向的左／右依指定起終點判定。\n\n" +
                    "自動模式：直線基本牆、門窗、柱、家具及設備的平面外緣，以及原生矩形牆開口的兩側切口面；門窗外框不等於粗開口。\n" +
                    "內／外側依模型牆方向，並不代表房間內外。需要淨距、指定開口邊或設備定位時，請手選參考面。\n" +
                    "僅處理目前視圖的本機模型；曲面、連結模型及無有效平面參考的族群會略過。Esc 可取消。",
                Dock = Forms.DockStyle.Fill, AutoSize = true, Padding = new Forms.Padding(0, 12, 0, 8)
            };
            layout.Controls.Add(note, 0, 8);
            layout.SetColumnSpan(note, 2);
            layout.RowStyles.Clear();
            for (int row = 0; row < 8; row++) layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 36));
            layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
            layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 40));
            var buttons = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.RightToLeft };
            var cancel = new Forms.Button { Text = "取消", DialogResult = Forms.DialogResult.Cancel, AutoSize = true };
            var ok = new Forms.Button { Text = "開始選取", DialogResult = Forms.DialogResult.OK, AutoSize = true };
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            layout.Controls.Add(buttons, 0, 9);
            layout.SetColumnSpan(buttons, 2);
            AcceptButton = ok;
            CancelButton = cancel;
            mode.SelectedIndexChanged += (sender, args) =>
            {
                wallSide.Enabled = !ManualFaces;
                preselection.Enabled = !ManualFaces && hasSelection;
            };
            direction.SelectedIndexChanged += (sender, args) =>
            {
                string first = DirectionIndex == 0 ? "上方" : DirectionIndex == 1 ? "左方" : "方向左側";
                string second = DirectionIndex == 0 ? "下方" : DirectionIndex == 1 ? "右方" : "方向右側";
                int selected = placement.SelectedIndex;
                placement.Items[0] = "自動外側：" + first;
                placement.Items[1] = "自動外側：" + second;
                placement.SelectedIndex = selected;
            };
            placement.SelectedIndexChanged += (sender, args) => offset.Enabled = PlacementIndex != 2;
        }

        private static void AddChoice(Forms.TableLayoutPanel layout, int row, string label, Forms.ComboBox box, string[] choices)
        {
            box.DropDownStyle = Forms.ComboBoxStyle.DropDownList;
            box.Items.AddRange(choices);
            box.SelectedIndex = 0;
            box.Dock = Forms.DockStyle.Fill;
            layout.Controls.Add(new Forms.Label { Text = label, AutoSize = true, Anchor = Forms.AnchorStyles.Left }, 0, row);
            layout.Controls.Add(box, 1, row);
        }
    }
}
