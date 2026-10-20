using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Forms = System.Windows.Forms;

namespace TYBIM.Architecture
{
    // Category conditions feed native associative dimensions; no tag families are required.
    internal static class ArchitecturalTagging
    {
        internal sealed class CategoryChoice
        {
            public string Name;
            public BuiltInCategory Model;
            public override string ToString() { return Name; }
        }

        internal static readonly CategoryChoice[] Categories =
        {
            new CategoryChoice { Name = "外牆配置", Model = BuiltInCategory.OST_Walls },
            new CategoryChoice { Name = "內牆配置", Model = BuiltInCategory.OST_Walls }
        };

        internal sealed class ViewChoice
        {
            public ElementId Id;
            public string Name;
            public string Kind, LevelName, TypeName;
            public bool IsActive;
            public override string ToString()
            {
                return Name + " ｜ " + Kind + " ｜ 樓層：" + LevelName + " ｜ " + TypeName +
                    (IsActive ? "（目前視圖）" : "");
            }
        }

        internal sealed class Rule
        {
            public CategoryChoice Category;
            public string WallFunction, Keyword, Parameter, Value;
            public DimensionType DimensionType; // null selects the built-in green preset.
            public bool UseRoomClassification;
            public bool Matches(Document doc, Element element, Autodesk.Revit.DB.WallFunction? classifiedFunction = null)
            {
                if (element.Category == null || element.Category.BuiltInCategory != Category.Model) return false;
                if (Category.Model == BuiltInCategory.OST_Walls && WallFunction != "不限")
                {
                    var wall = element as Wall;
                    if (wall == null || (classifiedFunction ?? wall.WallType.Function) !=
                        (WallFunction == "外牆" ? Autodesk.Revit.DB.WallFunction.Exterior : Autodesk.Revit.DB.WallFunction.Interior)) return false;
                }
                Element type = doc.GetElement(element.GetTypeId());
                var symbol = type as FamilySymbol;
                string names = element.Name + " " + (type == null ? "" : type.Name) + " " + (symbol == null ? "" : symbol.FamilyName);
                if (!Contains(names, Keyword)) return false;
                if (Parameter.Length == 0) return true;
                // Instance parameters take precedence; only fall back when absent on the instance.
                IList<Parameter> parameters = element.GetParameters(Parameter);
                if (parameters.Count == 0 && type != null) parameters = type.GetParameters(Parameter);
                // Ambiguous names must not silently select a different shared parameter.
                if (parameters.Count != 1 || !parameters[0].HasValue) return false;
                string text = parameters[0].AsValueString() ?? parameters[0].AsString() ?? "";
                return Contains(text, Value);
            }
            private static bool Contains(string text, string value)
            {
                return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        internal static WallFunction? ClassifyWallByRooms(Document doc, View view, Wall wall)
        {
            var line = (wall.Location as LocationCurve)?.Curve as Line;
            Level level = view.GenLevel;
            if (line == null || level == null) return null;
            var phase = doc.GetElement(view.get_Parameter(BuiltInParameter.VIEW_PHASE)?.AsElementId()
                ?? ElementId.InvalidElementId) as Phase;
            XYZ tangent = line.Direction;
            XYZ normal = tangent.CrossProduct(XYZ.BasisZ);
            if (normal.GetLength() < 1e-8) return null;
            normal = normal.Normalize();
            bool oneSideFound = false;
            // Sampling along the wall avoids junctions and openings. Two distances also cover
            // non-center wall location lines; query only the selected view's level and phase.
            foreach (double fraction in new[] { 0.15, 0.35, 0.5, 0.65, 0.85 })
                foreach (double height in new[] { 1000.0, 300.0 })
                    foreach (double distance in new[] { wall.Width / 2 + 30 / 304.8, wall.Width + 30 / 304.8 })
                    {
                        XYZ point = line.Evaluate(fraction, true);
                        point = new XYZ(point.X, point.Y, level.ProjectElevation + height / 304.8);
                        var first = doc.GetRoomAtPoint(point + normal * distance, phase);
                        var second = doc.GetRoomAtPoint(point - normal * distance, phase);
                        bool left = first != null && first.LevelId == level.Id;
                        bool right = second != null && second.LevelId == level.Id;
                        // Both sides can belong to the same room for non-room-bounding partitions.
                        if (left && right) return Autodesk.Revit.DB.WallFunction.Interior;
                        if (left || right) oneSideFound = true;
                    }
            return oneSideFound ? (WallFunction?)Autodesk.Revit.DB.WallFunction.Exterior : null;
        }

        public static Result Execute(ExternalCommandData command, ref string message)
        {
            UIDocument ui = command.Application.ActiveUIDocument;
            Document doc = ui.Document;
            View view = doc.ActiveView;
            var symbols = new FilteredElementCollector(doc).OfClass(typeof(DimensionType)).Cast<DimensionType>()
                .Where(s => s.StyleType == DimensionStyleType.Linear).OrderBy(s => s.Name).ToList();
            var views = new FilteredElementCollector(doc).OfClass(typeof(ViewPlan)).Cast<ViewPlan>()
                .Where(v => SpacingTag.IsSupportedPlan(v)).OrderBy(v => v.Name).ThenBy(v => v.ViewType)
                .Select(v => new ViewChoice
                {
                    Id = v.Id, Name = v.Name, IsActive = v.Id == view.Id,
                    Kind = v.ViewType == ViewType.CeilingPlan ? "天花板平面圖" :
                        v.ViewType == ViewType.EngineeringPlan ? "結構平面圖" : "樓板平面圖",
                    LevelName = v.GenLevel?.Name ?? "無關聯樓層",
                    TypeName = doc.GetElement(v.GetTypeId())?.Name ?? ""
                }).ToList();
            if (views.Count == 0)
            {
                TaskDialog.Show("間距標籤", "專案中沒有可標註的平面視圖。");
                return Result.Cancelled;
            }
            using (var form = new RuleForm(symbols, ui.Selection.GetElementIds().Count > 0, views))
            {
                if (form.ShowDialog(new Owner(command.Application.MainWindowHandle)) != Forms.DialogResult.OK)
                    return Result.Cancelled;
                try
                {
                    if (form.Scope == 2)
                    {
                        view = doc.GetElement(form.SelectedView.Id) as ViewPlan;
                        if (!SpacingTag.IsSupportedPlan(view))
                            throw new InvalidOperationException("所選視圖無法建立平面尺寸。");
                        // Switch before selection/collection, outside any transaction, so the user sees the result.
                        if (ui.ActiveView.Id != view.Id) ui.ActiveView = view;
                    }
                    else if (!SpacingTag.IsSupportedPlan(view))
                        throw new InvalidOperationException("框選或使用目前選取時，請先開啟平面視圖；也可改用整張視圖標註。");
                    var visible = new HashSet<ElementId>(new FilteredElementCollector(doc, view.Id)
                        .WhereElementIsNotElementType().ToElementIds());
                    IEnumerable<ElementId> selected;
                    if (form.Scope == 1) selected = ui.Selection.GetElementIds();
                    else if (form.Scope == 2) selected = visible;
                    else selected = ui.Selection.PickElementsByRectangle(new CategoryFilter(form.Rules),
                        "框選要建立綠色間距尺寸的元件範圍（僅本機模型），Esc 取消").Select(e => e.Id);
                    var candidates = selected.Distinct().Where(visible.Contains).Select(doc.GetElement)
                        .Where(e => e != null && !(e is ElementType) && e.Category != null &&
                            form.Rules.Any(r => r.Category.Model == e.Category.BuiltInCategory))
                        .OrderBy(e => e.Id.Value).ToList();
                    return SpacingTag.CreateAutomaticDimensions(doc, view, candidates, form.Rules,
                        form.OffsetMm, form.DirectionIndex, form.IndependentSegments);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException) { return Result.Cancelled; }
                catch (Exception ex)
                {
                    message = ex.Message;
                    TaskDialog.Show("間距標籤", "無法完成間距尺寸。\n" + ex.Message);
                    return Result.Failed;
                }
            }
        }

        private sealed class CategoryFilter : ISelectionFilter
        {
            private readonly HashSet<BuiltInCategory> categories;
            public CategoryFilter(IList<Rule> rules) { categories = new HashSet<BuiltInCategory>(rules.Select(r => r.Category.Model)); }
            public bool AllowElement(Element element)
            {
                return element.Category != null && !(element is ElementType) && categories.Contains(element.Category.BuiltInCategory);
            }
            public bool AllowReference(Reference reference, XYZ point) { return false; }
        }

        private sealed class Owner : Forms.IWin32Window
        {
            public IntPtr Handle { get; private set; }
            public Owner(IntPtr handle) { Handle = handle; }
        }

        private sealed class SymbolChoice
        {
            public DimensionType Type;
            public override string ToString() { return Type == null ? "預設：TYBIM 綠色斜線（公分，2 mm）" : Type.Name; }
        }

        private sealed class RuleForm : Forms.Form
        {
            private readonly Forms.DataGridView grid = new Forms.DataGridView();
            private readonly Forms.ComboBox scope = new Forms.ComboBox();
            private readonly Forms.ComboBox direction = new Forms.ComboBox();
            private readonly Forms.ComboBox targetView = new Forms.ComboBox();
            private readonly Forms.CheckBox independent = new Forms.CheckBox();
            private readonly Forms.CheckBox roomClassification = new Forms.CheckBox();
            private readonly Forms.NumericUpDown offset = new Forms.NumericUpDown();
            private readonly List<DimensionType> symbols;
            public List<Rule> Rules { get; private set; }
            public int Scope { get { return scope.SelectedIndex; } }
            public int DirectionIndex { get { return direction.SelectedIndex; } }
            public double OffsetMm { get { return (double)offset.Value; } }
            public ViewChoice SelectedView { get { return targetView.SelectedItem as ViewChoice; } }
            public bool IndependentSegments { get { return independent.Checked; } }

            public RuleForm(List<DimensionType> availableSymbols, bool hasSelection, List<ViewChoice> views)
            {
                Text = "間距標籤 — 建築間距尺寸條件";
                Font = new Font("Microsoft JhengHei UI", 10);
                AutoScaleMode = Forms.AutoScaleMode.Dpi;
                ClientSize = new Size(1160, 650);
                MinimumSize = new Size(950, 620);
                StartPosition = Forms.FormStartPosition.CenterParent;
                ShowInTaskbar = false;
                MinimizeBox = false;
                symbols = availableSymbols;
                var layout = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(16), ColumnCount = 1, RowCount = 5 };
                layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 90));
                layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 40));
                layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Percent, 100));
                layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 110));
                layout.RowStyles.Add(new Forms.RowStyle(Forms.SizeType.Absolute, 48));
                Controls.Add(layout);
                layout.Controls.Add(new Forms.Label { Dock = Forms.DockStyle.Fill, Text =
                    "提供外牆配置、內牆配置，預設綠色斜線。預設依房間兩側辨識內外牆；無房間可判斷時才依牆類型「功能」。\n" +
                    "名稱及參數值採「包含」比對；空白不限制。參數先查實例、再查類型，重名參數不比對。可改選專案既有尺寸類型。\n" +
                    "外牆標於外圍，內牆逐面標於室內牆邊，並納入牆上門窗開口。已標註的區段不重複建立；無有效參考面會列出原因。" }, 0, 0);
                var viewRow = new Forms.TableLayoutPanel { Dock = Forms.DockStyle.Fill, ColumnCount = 2 };
                viewRow.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Absolute, 110));
                viewRow.ColumnStyles.Add(new Forms.ColumnStyle(Forms.SizeType.Percent, 100));
                viewRow.Controls.Add(new Forms.Label { Text = "標註平面視圖", AutoSize = true, Anchor = Forms.AnchorStyles.Left }, 0, 0);
                targetView.DropDownStyle = Forms.ComboBoxStyle.DropDownList;
                targetView.Dock = Forms.DockStyle.Fill;
                foreach (ViewChoice choice in views) targetView.Items.Add(choice);
                targetView.SelectedItem = views.FirstOrDefault(v => v.IsActive) ?? views.FirstOrDefault();
                viewRow.Controls.Add(targetView, 1, 0);
                layout.Controls.Add(viewRow, 0, 1);
                grid.Dock = Forms.DockStyle.Fill;
                grid.AllowUserToAddRows = false;
                grid.AllowUserToDeleteRows = false;
                grid.AutoSizeColumnsMode = Forms.DataGridViewAutoSizeColumnsMode.Fill;
                grid.RowHeadersVisible = false;
                grid.SelectionMode = Forms.DataGridViewSelectionMode.CellSelect;
                grid.EditMode = Forms.DataGridViewEditMode.EditOnEnter;
                grid.MultiSelect = false;
                grid.Columns.Add(new Forms.DataGridViewCheckBoxColumn { Name = "Enabled", HeaderText = "啟用", FillWeight = 40 });
                grid.Columns.Add(new Forms.DataGridViewComboBoxColumn { Name = "Category", HeaderText = "元件類別", DataSource = Categories.Select(c => c.Name).ToArray(), FillWeight = 75 });
                grid.Columns.Add(new Forms.DataGridViewComboBoxColumn { Name = "Function", HeaderText = "牆功能", DataSource = new[] { "不限", "外牆", "內牆" }, FillWeight = 65 });
                grid.Columns["Function"].Visible = false;
                grid.Columns.Add("Keyword", "族／類型名稱包含");
                grid.Columns.Add("Parameter", "參數名稱（選填）");
                grid.Columns.Add("Value", "參數值包含");
                grid.Columns.Add(new Forms.DataGridViewComboBoxColumn { Name = "Symbol", HeaderText = "尺寸類型（已有預設）", FillWeight = 200 });
                grid.CurrentCellDirtyStateChanged += (s, e) =>
                {
                    if (grid.IsCurrentCellDirty && grid.CurrentCell is Forms.DataGridViewCheckBoxCell)
                        grid.CommitEdit(Forms.DataGridViewDataErrorContexts.Commit);
                };
                grid.CellClick += (s, e) =>
                {
                    if (e.RowIndex < 0 || e.ColumnIndex < 0 || grid[e.ColumnIndex, e.RowIndex].ReadOnly) return;
                    if (grid[e.ColumnIndex, e.RowIndex] is Forms.DataGridViewComboBoxCell)
                    {
                        grid.BeginEdit(true);
                        var combo = grid.EditingControl as Forms.ComboBox;
                        if (combo != null) combo.DroppedDown = true;
                    }
                };
                grid.CellValueChanged += (s, e) =>
                {
                    if (e.RowIndex >= 0 && e.ColumnIndex == grid.Columns["Category"].Index) UpdateSymbols(grid.Rows[e.RowIndex]);
                };
                layout.Controls.Add(grid, 0, 2);
                AddRule("外牆配置", "外牆", true);
                AddRule("內牆配置", "內牆", true);
                var edit = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, Padding = new Forms.Padding(0, 6, 0, 0) };
                var add = new Forms.Button { Text = "新增條件", AutoSize = true };
                add.Click += (s, e) => AddRule("外牆配置", "外牆", true);
                var remove = new Forms.Button { Text = "移除條件", AutoSize = true };
                remove.Click += (s, e) => { if (grid.CurrentRow != null) grid.Rows.Remove(grid.CurrentRow); };
                edit.Controls.Add(add);
                edit.Controls.Add(remove);
                var up = new Forms.Button { Text = "上移", AutoSize = true };
                up.Click += (s, e) => MoveRule(-1);
                var down = new Forms.Button { Text = "下移", AutoSize = true };
                down.Click += (s, e) => MoveRule(1);
                edit.Controls.Add(up);
                edit.Controls.Add(down);
                scope.DropDownStyle = Forms.ComboBoxStyle.DropDownList;
                scope.Width = 180;
                scope.Items.AddRange(new[] { "框選目前視圖範圍", "目前已選取元件", "所選平面視圖全部元件" });
                scope.SelectedIndex = 2;
                scope.SelectedIndexChanged += (s, e) => targetView.Enabled = Scope == 2;
                edit.Controls.Add(scope);
                direction.DropDownStyle = Forms.ComboBoxStyle.DropDownList;
                direction.Width = 140;
                direction.Items.AddRange(new[] { "水平＋垂直", "僅水平", "僅垂直" });
                direction.SelectedIndex = 0;
                edit.Controls.Add(direction);
                edit.Controls.Add(new Forms.Label { Text = "牆邊偏移（圖紙 mm）", AutoSize = true, Padding = new Forms.Padding(0, 6, 0, 0) });
                offset.Minimum = 1;
                offset.Maximum = 100;
                offset.DecimalPlaces = 1;
                offset.Value = 10;
                offset.Width = 65;
                edit.Controls.Add(offset);
                independent.Text = "每段獨立（可單獨刪除）";
                independent.AutoSize = true;
                independent.Checked = true;
                edit.Controls.Add(independent);
                roomClassification.Text = "依房間辨識內外牆（無房間時依模型功能）";
                roomClassification.AutoSize = true;
                roomClassification.Checked = true;
                edit.Controls.Add(roomClassification);
                layout.Controls.Add(edit, 0, 3);
                var buttons = new Forms.FlowLayoutPanel { Dock = Forms.DockStyle.Fill, FlowDirection = Forms.FlowDirection.RightToLeft };
                var cancel = new Forms.Button { Text = "取消", AutoSize = true, DialogResult = Forms.DialogResult.Cancel };
                var ok = new Forms.Button { Text = "開始建立", AutoSize = true };
                ok.Click += (s, e) => ValidateRules(hasSelection);
                buttons.Controls.Add(cancel);
                buttons.Controls.Add(ok);
                layout.Controls.Add(buttons, 0, 4);
                AcceptButton = ok;
                CancelButton = cancel;
            }

            private void AddRule(string category, string function, bool enabled)
            {
                int index = grid.Rows.Add(enabled, category, function, "", "", "");
                UpdateSymbols(grid.Rows[index]);
            }

            private void MoveRule(int delta)
            {
                grid.EndEdit();
                if (grid.CurrentRow == null) return;
                int index = grid.CurrentRow.Index;
                int target = index + delta;
                if (target < 0 || target >= grid.Rows.Count) return;
                Forms.DataGridViewRow row = grid.Rows[index];
                grid.Rows.RemoveAt(index);
                grid.Rows.Insert(target, row);
                grid.CurrentCell = row.Cells["Enabled"];
            }

            private void UpdateSymbols(Forms.DataGridViewRow row)
            {
                CategoryChoice category = Categories.FirstOrDefault(c => c.Name == Convert.ToString(row.Cells["Category"].Value));
                if (category == null) return;
                var cell = (Forms.DataGridViewComboBoxCell)row.Cells["Symbol"];
                cell.Value = null;
                cell.Items.Clear();
                cell.Items.Add(new SymbolChoice());
                foreach (DimensionType symbol in symbols)
                    cell.Items.Add(new SymbolChoice { Type = symbol });
                cell.Value = cell.Items[0];
                row.Cells["Function"].ReadOnly = true;
                row.Cells["Function"].Value = category.Model != BuiltInCategory.OST_Walls ? "不限" :
                    category.Name == "內牆配置" ? "內牆" : "外牆";
            }

            private void ValidateRules(bool hasSelection)
            {
                grid.EndEdit();
                var rules = new List<Rule>();
                foreach (Forms.DataGridViewRow row in grid.Rows)
                {
                    if (!Convert.ToBoolean(row.Cells["Enabled"].Value)) continue;
                    var choice = row.Cells["Symbol"].Value as SymbolChoice;
                    string parameter = Convert.ToString(row.Cells["Parameter"].Value).Trim();
                    string value = Convert.ToString(row.Cells["Value"].Value).Trim();
                    if (choice == null || (parameter.Length == 0 && value.Length > 0))
                    {
                        Forms.MessageBox.Show(this, "第 " + (row.Index + 1) + " 列：請選擇尺寸類型或綠色預設；指定參數值時必須填寫參數名稱。", "條件尚未完成");
                        return;
                    }
                    rules.Add(new Rule
                    {
                        Category = Categories.First(c => c.Name == Convert.ToString(row.Cells["Category"].Value)),
                        WallFunction = Convert.ToString(row.Cells["Function"].Value),
                        Keyword = Convert.ToString(row.Cells["Keyword"].Value).Trim(),
                        Parameter = parameter, Value = value, DimensionType = choice.Type,
                        UseRoomClassification = roomClassification.Checked
                    });
                }
                if (rules.Count == 0 || (Scope == 1 && !hasSelection) || (Scope == 2 && SelectedView == null))
                {
                    Forms.MessageBox.Show(this, "請至少啟用一個條件並選擇目標平面視圖；使用目前選取範圍時，必須先選取元件。", "請確認範圍與條件");
                    return;
                }
                Rules = rules;
                DialogResult = Forms.DialogResult.OK;
            }
        }
    }
}
