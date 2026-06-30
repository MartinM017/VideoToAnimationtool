$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$script:AppRoot = Split-Path -Parent $PSCommandPath
$script:CoreRoot = Join-Path $script:AppRoot 'Core'

Import-Module -Name (Join-Path $script:CoreRoot 'PathUtils.psm1') -Force -Global
Import-Module -Name (Join-Path $script:CoreRoot 'Validation.psm1') -Force -Global
Import-Module -Name (Join-Path $script:CoreRoot 'Ffmpeg.psm1') -Force -Global

[System.Windows.Forms.Application]::EnableVisualStyles()
[System.Windows.Forms.Application]::SetCompatibleTextRenderingDefault($false)

$script:Frames = @()
$script:CurrentFrameIndex = 0
$script:CurrentImage = $null
$script:ExportProcess = $null
$script:ExportStdOutEvent = $null
$script:ExportStdErrEvent = $null
$script:ExportExitEvent = $null
$script:ExportTimer = $null

function New-Label {
    param([string] $Text, [int] $X, [int] $Y, [int] $Width = 120)
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $Text
    $label.Location = New-Object System.Drawing.Point($X, $Y)
    $label.Size = New-Object System.Drawing.Size($Width, 22)
    $label.TextAlign = [System.Drawing.ContentAlignment]::MiddleLeft
    return $label
}

function New-Button {
    param([string] $Text, [int] $X, [int] $Y, [int] $Width = 92, [int] $Height = 28)
    $button = New-Object System.Windows.Forms.Button
    $button.Text = $Text
    $button.Location = New-Object System.Drawing.Point($X, $Y)
    $button.Size = New-Object System.Drawing.Size($Width, $Height)
    return $button
}

function Add-Log {
    param([string] $Message)
    if ([string]::IsNullOrWhiteSpace($Message)) {
        return
    }

    $timestamp = Get-Date -Format 'HH:mm:ss'
    $logBox.AppendText("[$timestamp] $Message`r`n")
    $logBox.SelectionStart = $logBox.TextLength
    $logBox.ScrollToCaret()
}

function Set-Status {
    param([string] $Message)
    $statusLabel.Text = $Message
    Add-Log $Message
}

function ConvertTo-ProcessArgument {
    param([Parameter(Mandatory = $true)] [string] $Value)

    if ($Value -notmatch '[\s"]') {
        return $Value
    }

    $escaped = $Value -replace '"', '\"'
    return '"' + $escaped + '"'
}

function Format-TimeValue {
    param([System.Windows.Forms.NumericUpDown] $Control)
    return [double] $Control.Value
}

function Reset-CurrentImage {
    if ($script:CurrentImage) {
        $previewBox.Image = $null
        $script:CurrentImage.Dispose()
        $script:CurrentImage = $null
    }
}

function Show-CurrentFrame {
    if ($script:Frames.Count -eq 0) {
        Reset-CurrentImage
        $frameCounterLabel.Text = 'Frame 0 / 0'
        return
    }

    if ($script:CurrentFrameIndex -lt 0) {
        $script:CurrentFrameIndex = 0
    }

    if ($script:CurrentFrameIndex -ge $script:Frames.Count) {
        $script:CurrentFrameIndex = $script:Frames.Count - 1
    }

    Reset-CurrentImage
    $path = $script:Frames[$script:CurrentFrameIndex]
    $stream = [System.IO.File]::OpenRead($path)
    try {
        $image = [System.Drawing.Image]::FromStream($stream)
        $script:CurrentImage = New-Object System.Drawing.Bitmap($image)
        $image.Dispose()
        $previewBox.Image = $script:CurrentImage
    }
    finally {
        $stream.Dispose()
    }

    $frameCounterLabel.Text = 'Frame {0} / {1}' -f ($script:CurrentFrameIndex + 1), $script:Frames.Count
}

function Load-FrameFolder {
    param([string] $FolderPath)

    $frames = Get-FrameFiles -FolderPath $FolderPath
    $script:Frames = @($frames)
    $script:CurrentFrameIndex = 0
    $frameFolderTextBox.Text = $FolderPath

    if ($script:Frames.Count -eq 0) {
        Set-Status 'No PNG/JPG frames found in the selected folder.'
        Show-CurrentFrame
        return
    }

    Show-CurrentFrame
    Set-Status ('Loaded {0} frame(s).' -f $script:Frames.Count)
}

function Update-PreviewTimer {
    $fps = [int] $previewFpsControl.Value
    if ($fps -lt 1) {
        $fps = 1
    }
    $previewTimer.Interval = [Math]::Max(1, [int] (1000 / $fps))
}

function Stop-Preview {
    $previewTimer.Stop()
    $playPauseButton.Text = 'Play'
}

function Start-Preview {
    if ($script:Frames.Count -eq 0) {
        Set-Status 'Load a frame folder before preview playback.'
        return
    }

    Update-PreviewTimer
    $previewTimer.Start()
    $playPauseButton.Text = 'Pause'
}

function Clear-ExportEvents {
    foreach ($eventSubscriber in @($script:ExportStdOutEvent, $script:ExportStdErrEvent, $script:ExportExitEvent)) {
        if ($eventSubscriber) {
            Unregister-Event -SubscriptionId $eventSubscriber.Id -ErrorAction SilentlyContinue
            Remove-Job -Id $eventSubscriber.Id -Force -ErrorAction SilentlyContinue
        }
    }

    $script:ExportStdOutEvent = $null
    $script:ExportStdErrEvent = $null
    $script:ExportExitEvent = $null
}

function Complete-Export {
    param([int] $ExitCode)

    if ($script:ExportTimer) {
        $script:ExportTimer.Stop()
    }

    Clear-ExportEvents
    $progressBar.Style = [System.Windows.Forms.ProgressBarStyle]::Blocks
    $progressBar.Value = 0
    $exportButton.Enabled = $true
    $cancelButton.Enabled = $false

    if ($ExitCode -eq 0) {
        Set-Status 'Export finished.'
        Load-FrameFolder -FolderPath $outputFolderTextBox.Text
    }
    else {
        Set-Status ('Export failed with exit code {0}.' -f $ExitCode)
    }

    $script:ExportProcess = $null
}

function Start-FrameExport {
    $ffmpegPath = Find-FfmpegExecutable
    if (-not $ffmpegPath) {
        [System.Windows.Forms.MessageBox]::Show(
            'ffmpeg.exe was not found. Put it in src\VideoToAnimationTool\tools\ffmpeg\ffmpeg.exe or add FFmpeg to PATH.',
            'FFmpeg Missing',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Error
        ) | Out-Null
        return
    }

    $inputPath = $videoPathTextBox.Text.Trim()
    $outputFolder = $outputFolderTextBox.Text.Trim()
    $start = Format-TimeValue $startTimeControl
    $end = Format-TimeValue $endTimeControl
    $fps = [int] $exportFpsControl.Value
    $format = [string] $formatComboBox.SelectedItem

    $validation = Test-FrameExportOptions `
        -InputPath $inputPath `
        -OutputFolder $outputFolder `
        -StartSeconds $start `
        -EndSeconds $end `
        -Fps $fps `
        -Format $format

    if (-not $validation.IsValid) {
        [System.Windows.Forms.MessageBox]::Show(
            ($validation.Errors -join [Environment]::NewLine),
            'Invalid Export Settings',
            [System.Windows.Forms.MessageBoxButtons]::OK,
            [System.Windows.Forms.MessageBoxIcon]::Warning
        ) | Out-Null
        return
    }

    $character = $characterTextBox.Text
    $action = $actionTextBox.Text
    $pattern = '{0}_{1}_*.{2}' -f (ConvertTo-SafeName $character), (ConvertTo-SafeName $action), $format
    $existing = @(Get-ChildItem -LiteralPath $outputFolder -File -Filter $pattern -ErrorAction SilentlyContinue)
    if ($existing.Count -gt 0) {
        $answer = [System.Windows.Forms.MessageBox]::Show(
            "The output folder already contains $($existing.Count) matching frame file(s). Continue and allow overwrite?",
            'Confirm Overwrite',
            [System.Windows.Forms.MessageBoxButtons]::YesNo,
            [System.Windows.Forms.MessageBoxIcon]::Question
        )
        if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) {
            return
        }
    }

    $args = New-FfmpegFrameExportArguments `
        -InputPath $inputPath `
        -OutputFolder $outputFolder `
        -CharacterName $character `
        -ActionName $action `
        -StartSeconds $start `
        -EndSeconds $end `
        -Fps $fps `
        -Format $format

    Add-Log ('Running: "{0}" {1}' -f $ffmpegPath, ($args -join ' '))

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $ffmpegPath
    $startInfo.Arguments = (($args | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $process.EnableRaisingEvents = $true
    $script:ExportProcess = $process

    Clear-ExportEvents

    $script:ExportStdOutEvent = Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
        if ($EventArgs.Data) {
            $form.BeginInvoke([Action[string]] { param($line) Add-Log $line }, $EventArgs.Data) | Out-Null
        }
    }

    $script:ExportStdErrEvent = Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
        if ($EventArgs.Data) {
            $form.BeginInvoke([Action[string]] { param($line) Add-Log $line }, $EventArgs.Data) | Out-Null
        }
    }

    $script:ExportExitEvent = Register-ObjectEvent -InputObject $process -EventName Exited -Action {
        $exitCode = $Event.Sender.ExitCode
        $form.BeginInvoke([Action[int]] { param($code) Complete-Export -ExitCode $code }, $exitCode) | Out-Null
    }

    $exportButton.Enabled = $false
    $cancelButton.Enabled = $true
    $progressBar.Style = [System.Windows.Forms.ProgressBarStyle]::Marquee
    Set-Status 'Export started.'

    [void] $process.Start()
    $process.BeginOutputReadLine()
    $process.BeginErrorReadLine()
}

$form = New-Object System.Windows.Forms.Form
$form.Text = 'Video to Animation Tool'
$form.StartPosition = 'CenterScreen'
$form.Size = New-Object System.Drawing.Size(1180, 760)
$form.MinimumSize = New-Object System.Drawing.Size(1020, 680)

$leftPanel = New-Object System.Windows.Forms.Panel
$leftPanel.Location = New-Object System.Drawing.Point(12, 12)
$leftPanel.Size = New-Object System.Drawing.Size(520, 520)
$leftPanel.Anchor = 'Top,Left,Bottom'
$form.Controls.Add($leftPanel)

$previewPanel = New-Object System.Windows.Forms.Panel
$previewPanel.Location = New-Object System.Drawing.Point(548, 12)
$previewPanel.Size = New-Object System.Drawing.Size(600, 520)
$previewPanel.Anchor = 'Top,Left,Right,Bottom'
$form.Controls.Add($previewPanel)

$logPanel = New-Object System.Windows.Forms.Panel
$logPanel.Location = New-Object System.Drawing.Point(12, 548)
$logPanel.Size = New-Object System.Drawing.Size(1136, 165)
$logPanel.Anchor = 'Left,Right,Bottom'
$form.Controls.Add($logPanel)

$sourceGroup = New-Object System.Windows.Forms.GroupBox
$sourceGroup.Text = 'Source Video'
$sourceGroup.Location = New-Object System.Drawing.Point(0, 0)
$sourceGroup.Size = New-Object System.Drawing.Size(520, 92)
$leftPanel.Controls.Add($sourceGroup)

$videoPathTextBox = New-Object System.Windows.Forms.TextBox
$videoPathTextBox.Location = New-Object System.Drawing.Point(14, 28)
$videoPathTextBox.Size = New-Object System.Drawing.Size(390, 24)
$videoPathTextBox.Anchor = 'Top,Left,Right'
$sourceGroup.Controls.Add($videoPathTextBox)

$browseVideoButton = New-Button -Text 'Browse' -X 414 -Y 26 -Width 82
$sourceGroup.Controls.Add($browseVideoButton)

$metadataLabel = New-Label -Text 'Select a video to begin.' -X 14 -Y 58 -Width 480
$sourceGroup.Controls.Add($metadataLabel)

$settingsGroup = New-Object System.Windows.Forms.GroupBox
$settingsGroup.Text = 'Export Settings'
$settingsGroup.Location = New-Object System.Drawing.Point(0, 104)
$settingsGroup.Size = New-Object System.Drawing.Size(520, 300)
$leftPanel.Controls.Add($settingsGroup)

$settingsGroup.Controls.Add((New-Label -Text 'Character' -X 14 -Y 30))
$characterTextBox = New-Object System.Windows.Forms.TextBox
$characterTextBox.Location = New-Object System.Drawing.Point(138, 28)
$characterTextBox.Size = New-Object System.Drawing.Size(130, 24)
$characterTextBox.Text = 'Character'
$settingsGroup.Controls.Add($characterTextBox)

$settingsGroup.Controls.Add((New-Label -Text 'Action' -X 284 -Y 30 -Width 55))
$actionTextBox = New-Object System.Windows.Forms.TextBox
$actionTextBox.Location = New-Object System.Drawing.Point(344, 28)
$actionTextBox.Size = New-Object System.Drawing.Size(150, 24)
$actionTextBox.Text = 'Action'
$settingsGroup.Controls.Add($actionTextBox)

$settingsGroup.Controls.Add((New-Label -Text 'Output Folder' -X 14 -Y 68))
$outputFolderTextBox = New-Object System.Windows.Forms.TextBox
$outputFolderTextBox.Location = New-Object System.Drawing.Point(138, 66)
$outputFolderTextBox.Size = New-Object System.Drawing.Size(266, 24)
$settingsGroup.Controls.Add($outputFolderTextBox)

$browseOutputButton = New-Button -Text 'Browse' -X 414 -Y 64 -Width 82
$settingsGroup.Controls.Add($browseOutputButton)

$settingsGroup.Controls.Add((New-Label -Text 'Start Seconds' -X 14 -Y 108))
$startTimeControl = New-Object System.Windows.Forms.NumericUpDown
$startTimeControl.Location = New-Object System.Drawing.Point(138, 106)
$startTimeControl.Size = New-Object System.Drawing.Size(96, 24)
$startTimeControl.DecimalPlaces = 2
$startTimeControl.Maximum = 99999
$settingsGroup.Controls.Add($startTimeControl)

$settingsGroup.Controls.Add((New-Label -Text 'End Seconds' -X 256 -Y 108 -Width 90))
$endTimeControl = New-Object System.Windows.Forms.NumericUpDown
$endTimeControl.Location = New-Object System.Drawing.Point(344, 106)
$endTimeControl.Size = New-Object System.Drawing.Size(96, 24)
$endTimeControl.DecimalPlaces = 2
$endTimeControl.Maximum = 99999
$endTimeControl.Value = 3
$settingsGroup.Controls.Add($endTimeControl)

$settingsGroup.Controls.Add((New-Label -Text 'Export FPS' -X 14 -Y 148))
$exportFpsControl = New-Object System.Windows.Forms.NumericUpDown
$exportFpsControl.Location = New-Object System.Drawing.Point(138, 146)
$exportFpsControl.Size = New-Object System.Drawing.Size(96, 24)
$exportFpsControl.Minimum = 1
$exportFpsControl.Maximum = 120
$exportFpsControl.Value = 12
$settingsGroup.Controls.Add($exportFpsControl)

$settingsGroup.Controls.Add((New-Label -Text 'Format' -X 256 -Y 148 -Width 90))
$formatComboBox = New-Object System.Windows.Forms.ComboBox
$formatComboBox.Location = New-Object System.Drawing.Point(344, 146)
$formatComboBox.Size = New-Object System.Drawing.Size(96, 24)
$formatComboBox.DropDownStyle = [System.Windows.Forms.ComboBoxStyle]::DropDownList
[void] $formatComboBox.Items.AddRange(@('png', 'jpg'))
$formatComboBox.SelectedIndex = 0
$settingsGroup.Controls.Add($formatComboBox)

$namePreviewLabel = New-Label -Text 'Output: Character_Action_0001.png' -X 14 -Y 188 -Width 480
$settingsGroup.Controls.Add($namePreviewLabel)

$exportButton = New-Button -Text 'Export Frames' -X 138 -Y 232 -Width 130 -Height 34
$settingsGroup.Controls.Add($exportButton)

$cancelButton = New-Button -Text 'Cancel' -X 282 -Y 232 -Width 96 -Height 34
$cancelButton.Enabled = $false
$settingsGroup.Controls.Add($cancelButton)

$frameGroup = New-Object System.Windows.Forms.GroupBox
$frameGroup.Text = 'Frame Folder'
$frameGroup.Location = New-Object System.Drawing.Point(0, 416)
$frameGroup.Size = New-Object System.Drawing.Size(520, 88)
$leftPanel.Controls.Add($frameGroup)

$frameFolderTextBox = New-Object System.Windows.Forms.TextBox
$frameFolderTextBox.Location = New-Object System.Drawing.Point(14, 32)
$frameFolderTextBox.Size = New-Object System.Drawing.Size(390, 24)
$frameGroup.Controls.Add($frameFolderTextBox)

$loadFrameFolderButton = New-Button -Text 'Load' -X 414 -Y 30 -Width 82
$frameGroup.Controls.Add($loadFrameFolderButton)

$previewGroup = New-Object System.Windows.Forms.GroupBox
$previewGroup.Text = 'Animation Preview'
$previewGroup.Dock = [System.Windows.Forms.DockStyle]::Fill
$previewPanel.Controls.Add($previewGroup)

$previewBox = New-Object System.Windows.Forms.PictureBox
$previewBox.Location = New-Object System.Drawing.Point(14, 24)
$previewBox.Size = New-Object System.Drawing.Size(568, 410)
$previewBox.Anchor = 'Top,Left,Right,Bottom'
$previewBox.BackColor = [System.Drawing.Color]::FromArgb(32, 32, 32)
$previewBox.SizeMode = [System.Windows.Forms.PictureBoxSizeMode]::Zoom
$previewGroup.Controls.Add($previewBox)

$playPauseButton = New-Button -Text 'Play' -X 14 -Y 448 -Width 76
$playPauseButton.Anchor = 'Left,Bottom'
$previewGroup.Controls.Add($playPauseButton)

$previousButton = New-Button -Text 'Prev' -X 102 -Y 448 -Width 70
$previousButton.Anchor = 'Left,Bottom'
$previewGroup.Controls.Add($previousButton)

$nextButton = New-Button -Text 'Next' -X 184 -Y 448 -Width 70
$nextButton.Anchor = 'Left,Bottom'
$previewGroup.Controls.Add($nextButton)

$previewGroup.Controls.Add((New-Label -Text 'Preview FPS' -X 274 -Y 451 -Width 84))
$previewFpsControl = New-Object System.Windows.Forms.NumericUpDown
$previewFpsControl.Location = New-Object System.Drawing.Point(364, 449)
$previewFpsControl.Size = New-Object System.Drawing.Size(70, 24)
$previewFpsControl.Minimum = 1
$previewFpsControl.Maximum = 120
$previewFpsControl.Value = 12
$previewFpsControl.Anchor = 'Left,Bottom'
$previewGroup.Controls.Add($previewFpsControl)

$loopCheckBox = New-Object System.Windows.Forms.CheckBox
$loopCheckBox.Text = 'Loop'
$loopCheckBox.Checked = $true
$loopCheckBox.Location = New-Object System.Drawing.Point(452, 450)
$loopCheckBox.Size = New-Object System.Drawing.Size(70, 24)
$loopCheckBox.Anchor = 'Left,Bottom'
$previewGroup.Controls.Add($loopCheckBox)

$frameCounterLabel = New-Label -Text 'Frame 0 / 0' -X 14 -Y 486 -Width 220
$frameCounterLabel.Anchor = 'Left,Bottom'
$previewGroup.Controls.Add($frameCounterLabel)

$progressBar = New-Object System.Windows.Forms.ProgressBar
$progressBar.Location = New-Object System.Drawing.Point(0, 0)
$progressBar.Size = New-Object System.Drawing.Size(1136, 18)
$progressBar.Anchor = 'Left,Right,Top'
$logPanel.Controls.Add($progressBar)

$statusLabel = New-Label -Text 'Ready.' -X 0 -Y 24 -Width 1136
$statusLabel.Anchor = 'Left,Right,Top'
$logPanel.Controls.Add($statusLabel)

$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Location = New-Object System.Drawing.Point(0, 52)
$logBox.Size = New-Object System.Drawing.Size(1136, 112)
$logBox.Anchor = 'Left,Right,Top,Bottom'
$logBox.Multiline = $true
$logBox.ScrollBars = [System.Windows.Forms.ScrollBars]::Vertical
$logBox.ReadOnly = $true
$logBox.Font = New-Object System.Drawing.Font('Consolas', 9)
$logPanel.Controls.Add($logBox)

$previewTimer = New-Object System.Windows.Forms.Timer
$previewTimer.Interval = 83
$previewTimer.Add_Tick({
    if ($script:Frames.Count -eq 0) {
        Stop-Preview
        return
    }

    $script:CurrentFrameIndex++
    if ($script:CurrentFrameIndex -ge $script:Frames.Count) {
        if ($loopCheckBox.Checked) {
            $script:CurrentFrameIndex = 0
        }
        else {
            $script:CurrentFrameIndex = $script:Frames.Count - 1
            Stop-Preview
        }
    }

    Show-CurrentFrame
})

$browseVideoButton.Add_Click({
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = 'Choose AI character action video'
    $dialog.Filter = 'Video files|*.mp4;*.mov;*.webm;*.avi|All files|*.*'
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $videoPathTextBox.Text = $dialog.FileName
        $metadataLabel.Text = 'Selected: ' + [System.IO.Path]::GetFileName($dialog.FileName)
        if ([string]::IsNullOrWhiteSpace($outputFolderTextBox.Text)) {
            $outputFolderTextBox.Text = Join-Path ([System.IO.Path]::GetDirectoryName($dialog.FileName)) 'frames'
        }
    }
})

$browseOutputButton.Add_Click({
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Choose output folder for sequence frames'
    if (-not [string]::IsNullOrWhiteSpace($outputFolderTextBox.Text)) {
        $dialog.SelectedPath = $outputFolderTextBox.Text
    }
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $outputFolderTextBox.Text = $dialog.SelectedPath
    }
})

$loadFrameFolderButton.Add_Click({
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = 'Choose an existing frame folder'
    if (-not [string]::IsNullOrWhiteSpace($frameFolderTextBox.Text)) {
        $dialog.SelectedPath = $frameFolderTextBox.Text
    }
    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        Load-FrameFolder -FolderPath $dialog.SelectedPath
    }
})

$exportButton.Add_Click({
    if (-not (Test-Path -LiteralPath $outputFolderTextBox.Text -PathType Container)) {
        New-Item -ItemType Directory -Path $outputFolderTextBox.Text -Force | Out-Null
    }
    Start-FrameExport
})

$cancelButton.Add_Click({
    if ($script:ExportProcess -and -not $script:ExportProcess.HasExited) {
        $script:ExportProcess.Kill()
        Set-Status 'Export cancelled.'
    }
})

$playPauseButton.Add_Click({
    if ($previewTimer.Enabled) {
        Stop-Preview
    }
    else {
        Start-Preview
    }
})

$previousButton.Add_Click({
    Stop-Preview
    if ($script:Frames.Count -gt 0) {
        $script:CurrentFrameIndex = [Math]::Max(0, $script:CurrentFrameIndex - 1)
        Show-CurrentFrame
    }
})

$nextButton.Add_Click({
    Stop-Preview
    if ($script:Frames.Count -gt 0) {
        $script:CurrentFrameIndex = [Math]::Min($script:Frames.Count - 1, $script:CurrentFrameIndex + 1)
        Show-CurrentFrame
    }
})

$previewFpsControl.Add_ValueChanged({ Update-PreviewTimer })

$updateNamePreview = {
    $extension = [string] $formatComboBox.SelectedItem
    if ([string]::IsNullOrWhiteSpace($extension)) {
        $extension = 'png'
    }
    $namePreviewLabel.Text = 'Output: ' + (New-FrameFileName -CharacterName $characterTextBox.Text -ActionName $actionTextBox.Text -Index 1 -Extension $extension)
}

$characterTextBox.Add_TextChanged($updateNamePreview)
$actionTextBox.Add_TextChanged($updateNamePreview)
$formatComboBox.Add_SelectedIndexChanged($updateNamePreview)

$form.Add_FormClosing({
    Stop-Preview
    if ($script:ExportProcess -and -not $script:ExportProcess.HasExited) {
        $script:ExportProcess.Kill()
    }
    Clear-ExportEvents
    Reset-CurrentImage
})

& $updateNamePreview
Add-Log 'Ready. Select a video or load an existing frame folder.'

[void] [System.Windows.Forms.Application]::Run($form)
