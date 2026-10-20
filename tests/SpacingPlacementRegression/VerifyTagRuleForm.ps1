param([string]$RepositoryRoot = (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent))

# Run with Windows PowerShell 5.1 -STA: exercises the 2024 form without a live Revit document.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()
$env:PATH = 'C:/Program Files/Autodesk/Revit 2024;' + $env:PATH
[void][Reflection.Assembly]::LoadFrom('C:/Program Files/Autodesk/Revit 2024/RevitAPI.dll')
$addinAssembly = [Reflection.Assembly]::LoadFrom((Join-Path $RepositoryRoot 'TYBIM/bin/Release/TYBIM.dll'))
$formType = $addinAssembly.GetType('TYBIM.Architecture.ArchitecturalTagging+RuleForm', $true)
$symbolList = New-Object 'System.Collections.Generic.List[Autodesk.Revit.DB.DimensionType]'
$viewChoiceType = $addinAssembly.GetType('TYBIM.Architecture.ArchitecturalTagging+ViewChoice', $true)
$viewChoice = [Activator]::CreateInstance($viewChoiceType, $true)
$viewChoice.Name = 'Test plan'
$viewChoice.Kind = '樓板平面圖'
$viewChoice.LevelName = 'FL1'
$viewChoice.TypeName = '樓層平面'
$viewChoice.IsActive = $true
$ceilingChoice = [Activator]::CreateInstance($viewChoiceType, $true)
$ceilingChoice.Name = 'Test plan'
$ceilingChoice.Kind = '天花板平面圖'
$ceilingChoice.LevelName = 'FL1'
$ceilingChoice.TypeName = '天花板平面'
if ($ceilingChoice.ToString() -eq $viewChoice.ToString()) { throw 'Same-name floor and ceiling views must be distinguishable.' }
if (-not $viewChoice.ToString().Contains('FL1') -or -not $viewChoice.ToString().Contains('樓板平面圖')) { throw 'View label must include level and view kind.' }
$viewListType = [System.Collections.Generic.List``1].MakeGenericType([Type[]]@($viewChoiceType))
$viewList = [Activator]::CreateInstance($viewListType)
$viewList.Add($viewChoice)
$viewList.Add($ceilingChoice)
$constructorArgs = New-Object object[] 3
$constructorArgs[0] = $symbolList.PSObject.BaseObject
$constructorArgs[1] = $false
$constructorArgs[2] = $viewList.PSObject.BaseObject
$ruleForm = $formType.GetConstructors()[0].Invoke($constructorArgs)
try {
    $flags = [Reflection.BindingFlags]'NonPublic,Instance'
    $ruleGrid = $formType.GetField('grid', $flags).GetValue($ruleForm)
    if ($ruleGrid.Rows.Count -ne 2) { throw 'Expected exterior and interior presets only.' }
    if ($formType.GetProperty('Scope').GetValue($ruleForm) -ne 2) { throw 'Whole selected view must be the default scope.' }
    if (-not $formType.GetProperty('IndependentSegments').GetValue($ruleForm)) { throw 'Dimensions must be independently deletable by default.' }
    if ($ruleGrid.EditMode -ne [System.Windows.Forms.DataGridViewEditMode]::EditOnEnter) { throw 'Text cells must edit on first entry.' }
    foreach ($row in $ruleGrid.Rows) {
        if ($null -eq $row.Cells['Symbol'].Value) { throw 'Every category must have an automatic dimension preset without tag families.' }
        if ($row.Cells['Symbol'].Items.Count -ne 1) { throw 'Empty project type list must still offer the green preset.' }
    }
    if ($ruleGrid.Rows[0].Cells['Function'].Value -ne '外牆') { throw 'Missing exterior preset.' }
    if ($ruleGrid.Rows[1].Cells['Category'].Value -ne '內牆配置' -or $ruleGrid.Rows[1].Cells['Function'].Value -ne '內牆') { throw 'Missing interior preset or incorrect wall function.' }
    if ($ruleGrid.Rows[0].Cells['Category'].Items.Contains('地坪')) { throw 'Floor category must be removed.' }
    if ($ruleGrid.Columns['Function'].Visible) { throw 'Wall function should not complicate the test UI.' }
    $ruleGrid.Rows[1].Cells['Category'].Value = '外牆配置'
    if ($ruleGrid.Rows[1].Cells['Function'].Value -ne '外牆') { throw 'Exterior category must enforce exterior function.' }
    $ruleGrid.Rows[1].Cells['Category'].Value = '內牆配置'
    if ($ruleGrid.Rows[1].Cells['Function'].Value -ne '內牆') { throw 'Interior category must enforce interior function.' }
    $formType.GetMethod('AddRule', $flags).Invoke($ruleForm, [object[]]@('外牆配置', '外牆', $true))
    $ruleGrid.CurrentCell = $ruleGrid.Rows[2].Cells['Enabled']
    $formType.GetMethod('MoveRule', $flags).Invoke($ruleForm, [object[]]@(-1))
    if ($ruleGrid.Rows[1].Cells['Category'].Value -ne '外牆配置') { throw 'Rule priority move failed.' }
    $ruleGrid.Rows.RemoveAt(1)
    # A shown form is needed for WinForms to lay out and paint children. Keep it offscreen.
    $ruleForm.StartPosition = [System.Windows.Forms.FormStartPosition]::Manual
    $ruleForm.Location = New-Object Drawing.Point(-20000, -20000)
    $ruleForm.Show()
    [System.Windows.Forms.Application]::DoEvents()
    $ruleForm.PerformLayout()
    $ruleGrid.CurrentCell = $ruleGrid.Rows[0].Cells['Category']
    $clickArgs = New-Object System.Windows.Forms.DataGridViewCellEventArgs($ruleGrid.Columns['Category'].Index, 0)
    [System.Windows.Forms.DataGridView].GetMethod('OnCellClick', $flags).Invoke($ruleGrid, [object[]]@($clickArgs.PSObject.BaseObject))
    if (-not $ruleGrid.EditingControl.DroppedDown) { throw 'One cell click must open the category dropdown.' }
    $ruleGrid.EditingControl.DroppedDown = $false
    $ruleGrid.EndEdit()
    $defaultSize = $ruleForm.Size
    $ruleForm.Size = $ruleForm.MinimumSize
    [System.Windows.Forms.Application]::DoEvents()
    $editPanel = $ruleForm.Controls[0].GetControlFromPosition(0, 3)
    foreach ($control in $editPanel.Controls) {
        if ($control.Bottom -gt $editPanel.ClientSize.Height) { throw 'Options clipped at minimum window size.' }
    }
    $ruleForm.Size = $defaultSize
    [System.Windows.Forms.Application]::DoEvents()
    $preview = New-Object Drawing.Bitmap($ruleForm.Width, $ruleForm.Height)
    try {
        $ruleForm.DrawToBitmap($preview, (New-Object Drawing.Rectangle(0, 0, $ruleForm.Width, $ruleForm.Height)))
        $previewDirectory = Join-Path $PSScriptRoot 'bin'
        [void][IO.Directory]::CreateDirectory($previewDirectory)
        $preview.Save((Join-Path $previewDirectory 'tag-rule-form.png'))
    } finally { $preview.Dispose() }
    # Reproduce the reported blocker: no tag families and all presets enabled must allow Start.
    $formType.GetMethod('ValidateRules', $flags).Invoke($ruleForm, [object[]]@($false))
    if ($ruleForm.DialogResult -ne [System.Windows.Forms.DialogResult]::OK) { throw 'Default rules must not require tag families.' }
    $acceptedRules = $formType.GetProperty('Rules').GetValue($ruleForm)
    if ($acceptedRules.Count -ne 2) { throw 'Expected two default rules to be accepted.' }
    foreach ($rule in $acceptedRules) {
        if ($null -ne $rule.DimensionType) { throw 'Default selection must resolve to the green dimension factory.' }
        if (-not $rule.UseRoomClassification) { throw 'Room classification must be enabled by default for shared wall types.' }
    }
    Write-Output 'PASS: exterior/interior only, room classification default, distinct same-name view labels, independent dimensions, one-click dropdown, Start validation and layout.'
} finally { $ruleForm.Dispose() }
