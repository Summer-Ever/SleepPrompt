# 睡眠确认弹窗：点「睡眠」才让电脑进入睡眠，点「取消」什么都不做
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
[System.Windows.Forms.Application]::EnableVisualStyles()

$form = New-Object System.Windows.Forms.Form
$form.Text = '确认进入睡眠？'
$form.FormBorderStyle = [System.Windows.Forms.FormBorderStyle]::FixedDialog
$form.MaximizeBox = $false
$form.MinimizeBox = $false
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.StartPosition = [System.Windows.Forms.FormStartPosition]::CenterScreen
$form.BackColor = [System.Drawing.Color]::White
$form.ClientSize = New-Object System.Drawing.Size(720, 380)

$lbl = New-Object System.Windows.Forms.Label
$lbl.Text = '要让电脑进入睡眠吗？'
$lbl.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 15)
$lbl.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$lbl.SetBounds(0, 30, 720, 60)

$btnFont = New-Object System.Drawing.Font('Microsoft YaHei UI', 22, [System.Drawing.FontStyle]::Bold)

$btnSleep = New-Object System.Windows.Forms.Button
$btnSleep.Text = '睡眠'
$btnSleep.Font = $btnFont
$btnSleep.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$btnSleep.BackColor = [System.Drawing.Color]::FromArgb(0, 120, 212)
$btnSleep.ForeColor = [System.Drawing.Color]::White
$btnSleep.FlatAppearance.BorderSize = 0
$btnSleep.SetBounds(60, 130, 290, 130)
$btnSleep.DialogResult = [System.Windows.Forms.DialogResult]::OK

$btnCancel = New-Object System.Windows.Forms.Button
$btnCancel.Text = '取消'
$btnCancel.Font = $btnFont
$btnCancel.FlatStyle = [System.Windows.Forms.FlatStyle]::Flat
$btnCancel.BackColor = [System.Drawing.Color]::FromArgb(230, 230, 230)
$btnCancel.ForeColor = [System.Drawing.Color]::Black
$btnCancel.FlatAppearance.BorderSize = 0
$btnCancel.SetBounds(370, 130, 290, 130)
$btnCancel.DialogResult = [System.Windows.Forms.DialogResult]::Cancel

$hint = New-Object System.Windows.Forms.Label
$hint.Text = '按回车 = 睡眠　　按 Esc = 取消'
$hint.Font = New-Object System.Drawing.Font('Microsoft YaHei UI', 9)
$hint.ForeColor = [System.Drawing.Color]::Gray
$hint.TextAlign = [System.Drawing.ContentAlignment]::MiddleCenter
$hint.SetBounds(0, 292, 720, 40)

$form.Controls.Add($lbl)
$form.Controls.Add($btnSleep)
$form.Controls.Add($btnCancel)
$form.Controls.Add($hint)
$form.AcceptButton = $btnSleep
$form.CancelButton = $btnCancel

$result = $form.ShowDialog()
$form.Dispose()

if ($result -eq [System.Windows.Forms.DialogResult]::OK) {
    [void][System.Windows.Forms.Application]::SetSuspendState([System.Windows.Forms.PowerState]::Suspend, $false, $false)
}
