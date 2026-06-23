using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using VideoToAnimationTool.Core;

namespace VideoToAnimationTool.App
{
    public sealed class MainWindow : Window
    {
        private readonly TextBox videoPathTextBox = new TextBox();
        private readonly TextBox outputFolderTextBox = new TextBox();
        private readonly TextBox frameFolderTextBox = new TextBox();
        private readonly TextBox characterTextBox = new TextBox { Text = "Character" };
        private readonly TextBox actionTextBox = new TextBox { Text = "Action" };
        private readonly TextBox logTextBox = new TextBox();
        private readonly TextBlock statusText = new TextBlock { Text = "Ready." };
        private readonly TextBlock metadataText = new TextBlock { Text = "Select a video to begin." };
        private readonly TextBlock namePreviewText = new TextBlock();
        private readonly TextBlock frameCounterText = new TextBlock { Text = "Frame 0 / 0" };
        private readonly Image previewImage = new Image { Stretch = Stretch.Uniform };
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Button exportButton = new Button { Content = "Export Frames" };
        private readonly Button cancelButton = new Button { Content = "Cancel", IsEnabled = false };
        private readonly Button playButton = new Button { Content = "Play" };
        private readonly CheckBox loopCheckBox = new CheckBox { Content = "Loop", IsChecked = true };
        private readonly ComboBox formatComboBox = new ComboBox();
        private readonly DecimalBox startSecondsBox = new DecimalBox("0");
        private readonly DecimalBox endSecondsBox = new DecimalBox("3");
        private readonly DecimalBox exportFpsBox = new DecimalBox("12");
        private readonly DecimalBox previewFpsBox = new DecimalBox("12");
        private readonly DispatcherTimer previewTimer = new DispatcherTimer();

        private string[] frames = new string[0];
        private int currentFrameIndex;
        private Process exportProcess;

        public MainWindow()
        {
            Title = "AI Character Sequence Frame Tool";
            Width = 1180;
            Height = 760;
            MinWidth = 1020;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            formatComboBox.Items.Add("png");
            formatComboBox.Items.Add("jpg");
            formatComboBox.SelectedIndex = 0;

            Content = BuildLayout();
            WireEvents();
            UpdateNamePreview();
            AddLog("Ready. Select a video or load an existing frame folder.");
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(12) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(520) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(165) });

            var left = BuildLeftPanel();
            Grid.SetColumn(left, 0);
            Grid.SetRow(left, 0);
            root.Children.Add(left);

            var preview = BuildPreviewPanel();
            Grid.SetColumn(preview, 2);
            Grid.SetRow(preview, 0);
            root.Children.Add(preview);

            var log = BuildLogPanel();
            Grid.SetColumn(log, 0);
            Grid.SetColumnSpan(log, 3);
            Grid.SetRow(log, 2);
            root.Children.Add(log);

            return root;
        }

        private UIElement BuildLeftPanel()
        {
            var panel = new StackPanel();
            panel.Children.Add(BuildSourceGroup());
            panel.Children.Add(BuildExportGroup());
            panel.Children.Add(BuildFrameFolderGroup());
            return panel;
        }

        private UIElement BuildSourceGroup()
        {
            var group = NewGroup("Source Video", 92);
            var grid = NewTwoColumnGrid();
            AddPathRow(grid, 0, videoPathTextBox, "Browse", BrowseVideo);
            AddFullRow(grid, 1, metadataText);
            group.Content = grid;
            return group;
        }

        private UIElement BuildExportGroup()
        {
            var group = NewGroup("Export Settings", 304);
            var grid = NewFormGrid(7);

            AddLabeledControl(grid, 0, "Character", characterTextBox);
            AddLabeledControl(grid, 1, "Action", actionTextBox);
            AddPathRow(grid, 2, outputFolderTextBox, "Browse", BrowseOutputFolder);
            AddLabeledControl(grid, 3, "Start Seconds", startSecondsBox);
            AddLabeledControl(grid, 4, "End Seconds", endSecondsBox);
            AddLabeledControl(grid, 5, "Export FPS", exportFpsBox);
            AddLabeledControl(grid, 6, "Format", formatComboBox);
            AddFullRow(grid, 7, namePreviewText);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(124, 8, 0, 0) };
            exportButton.Width = 130;
            exportButton.Height = 32;
            cancelButton.Width = 96;
            cancelButton.Height = 32;
            cancelButton.Margin = new Thickness(12, 0, 0, 0);
            buttons.Children.Add(exportButton);
            buttons.Children.Add(cancelButton);
            AddFullRow(grid, 8, buttons);

            group.Content = grid;
            return group;
        }

        private UIElement BuildFrameFolderGroup()
        {
            var group = NewGroup("Frame Folder", 88);
            var grid = NewTwoColumnGrid();
            AddPathRow(grid, 0, frameFolderTextBox, "Load", BrowseFrameFolder);
            group.Content = grid;
            return group;
        }

        private UIElement BuildPreviewPanel()
        {
            var group = NewGroup("Animation Preview", Double.NaN);
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(76) });

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 32, 32)),
                Child = previewImage,
                Padding = new Thickness(8)
            };
            Grid.SetRow(border, 0);
            grid.Children.Add(border);

            var controls = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 10, 0, 0) };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            playButton.Width = 76;
            row.Children.Add(playButton);
            row.Children.Add(NewButton("Prev", PreviousFrame, 70, 0, 12));
            row.Children.Add(NewButton("Next", NextFrame, 70, 0, 12));
            row.Children.Add(new TextBlock { Text = "Preview FPS", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 8, 0) });
            previewFpsBox.Width = 70;
            row.Children.Add(previewFpsBox);
            loopCheckBox.Margin = new Thickness(20, 4, 0, 0);
            row.Children.Add(loopCheckBox);
            controls.Children.Add(row);
            controls.Children.Add(frameCounterText);

            Grid.SetRow(controls, 1);
            grid.Children.Add(controls);
            group.Content = grid;
            return group;
        }

        private UIElement BuildLogPanel()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            progressBar.IsIndeterminate = false;
            Grid.SetRow(progressBar, 0);
            grid.Children.Add(progressBar);

            Grid.SetRow(statusText, 1);
            grid.Children.Add(statusText);

            logTextBox.AcceptsReturn = true;
            logTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            logTextBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            logTextBox.IsReadOnly = true;
            logTextBox.FontFamily = new FontFamily("Consolas");
            Grid.SetRow(logTextBox, 2);
            grid.Children.Add(logTextBox);
            return grid;
        }

        private void WireEvents()
        {
            characterTextBox.TextChanged += delegate { UpdateNamePreview(); };
            actionTextBox.TextChanged += delegate { UpdateNamePreview(); };
            formatComboBox.SelectionChanged += delegate { UpdateNamePreview(); };
            exportButton.Click += async delegate { await StartExportAsync(); };
            cancelButton.Click += delegate { CancelExport(); };
            playButton.Click += delegate { TogglePreview(); };
            previewFpsBox.TextChanged += delegate { UpdatePreviewTimer(); };
            previewTimer.Tick += delegate { AdvancePreview(); };
            Closing += delegate { CancelExport(); };
        }

        private async Task StartExportAsync()
        {
            var ffmpeg = FfmpegHelper.FindExecutable(AppDomain.CurrentDomain.BaseDirectory);
            if (String.IsNullOrWhiteSpace(ffmpeg))
            {
                MessageBox.Show("ffmpeg.exe was not found. Put it in tools\\ffmpeg\\ffmpeg.exe next to the app, or add FFmpeg to PATH.", "FFmpeg Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!Directory.Exists(outputFolderTextBox.Text))
            {
                Directory.CreateDirectory(outputFolderTextBox.Text);
            }

            var start = startSecondsBox.GetDouble(0);
            var end = endSecondsBox.GetDouble(3);
            var fps = exportFpsBox.GetInt(12);
            var format = Convert.ToString(formatComboBox.SelectedItem, CultureInfo.InvariantCulture);
            var validation = ExportOptionsValidator.Validate(videoPathTextBox.Text, outputFolderTextBox.Text, start, end, fps, format);
            if (!validation.IsValid)
            {
                MessageBox.Show(String.Join(Environment.NewLine, validation.Errors), "Invalid Export Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var pattern = PathUtils.ToSafeName(characterTextBox.Text) + "_" + PathUtils.ToSafeName(actionTextBox.Text) + "_*." + format;
            var existing = Directory.GetFiles(outputFolderTextBox.Text, pattern);
            if (existing.Length > 0)
            {
                var answer = MessageBox.Show("The output folder already contains " + existing.Length + " matching frame file(s). Continue and allow overwrite?", "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (answer != MessageBoxResult.Yes)
                {
                    return;
                }
            }

            var args = FfmpegHelper.BuildFrameExportArguments(videoPathTextBox.Text, outputFolderTextBox.Text, characterTextBox.Text, actionTextBox.Text, start, end, fps, format);
            AddLog("Running: \"" + ffmpeg + "\" " + FfmpegHelper.ToArgumentString(args));

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                Arguments = FfmpegHelper.ToArgumentString(args),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            exportButton.IsEnabled = false;
            cancelButton.IsEnabled = true;
            progressBar.IsIndeterminate = true;
            SetStatus("Export started.");

            exportProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            exportProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) Dispatcher.Invoke(delegate { AddLog(e.Data); }); };
            exportProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) Dispatcher.Invoke(delegate { AddLog(e.Data); }); };

            await Task.Run(delegate
            {
                exportProcess.Start();
                exportProcess.BeginOutputReadLine();
                exportProcess.BeginErrorReadLine();
                exportProcess.WaitForExit();
            });

            var exitCode = exportProcess.ExitCode;
            exportProcess.Dispose();
            exportProcess = null;
            progressBar.IsIndeterminate = false;
            exportButton.IsEnabled = true;
            cancelButton.IsEnabled = false;

            if (exitCode == 0)
            {
                SetStatus("Export finished.");
                LoadFrameFolder(outputFolderTextBox.Text);
            }
            else
            {
                SetStatus("Export failed with exit code " + exitCode + ".");
            }
        }

        private void CancelExport()
        {
            if (exportProcess != null && !exportProcess.HasExited)
            {
                exportProcess.Kill();
                SetStatus("Export cancelled.");
            }
        }

        private void BrowseVideo()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose AI character action video",
                Filter = "Video files|*.mp4;*.mov;*.webm;*.avi|All files|*.*"
            };
            if (dialog.ShowDialog(this) == true)
            {
                videoPathTextBox.Text = dialog.FileName;
                metadataText.Text = "Selected: " + Path.GetFileName(dialog.FileName);
                if (String.IsNullOrWhiteSpace(outputFolderTextBox.Text))
                {
                    outputFolderTextBox.Text = Path.Combine(Path.GetDirectoryName(dialog.FileName), "frames");
                }
            }
        }

        private void BrowseOutputFolder()
        {
            var folder = ChooseFolder(outputFolderTextBox.Text, "Choose output folder for sequence frames");
            if (!String.IsNullOrWhiteSpace(folder))
            {
                outputFolderTextBox.Text = folder;
            }
        }

        private void BrowseFrameFolder()
        {
            var folder = ChooseFolder(frameFolderTextBox.Text, "Choose an existing frame folder");
            if (!String.IsNullOrWhiteSpace(folder))
            {
                LoadFrameFolder(folder);
            }
        }

        private static string ChooseFolder(string selectedPath, string description)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                if (!String.IsNullOrWhiteSpace(selectedPath))
                {
                    dialog.SelectedPath = selectedPath;
                }

                return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private void LoadFrameFolder(string folderPath)
        {
            frames = PathUtils.GetFrameFiles(folderPath);
            currentFrameIndex = 0;
            frameFolderTextBox.Text = folderPath;
            if (frames.Length == 0)
            {
                previewImage.Source = null;
                frameCounterText.Text = "Frame 0 / 0";
                SetStatus("No PNG/JPG frames found in the selected folder.");
                return;
            }

            ShowCurrentFrame();
            SetStatus("Loaded " + frames.Length + " frame(s).");
        }

        private void TogglePreview()
        {
            if (previewTimer.IsEnabled)
            {
                StopPreview();
            }
            else
            {
                if (frames.Length == 0)
                {
                    SetStatus("Load a frame folder before preview playback.");
                    return;
                }
                UpdatePreviewTimer();
                previewTimer.Start();
                playButton.Content = "Pause";
            }
        }

        private void StopPreview()
        {
            previewTimer.Stop();
            playButton.Content = "Play";
        }

        private void PreviousFrame()
        {
            StopPreview();
            if (frames.Length == 0) return;
            currentFrameIndex = Math.Max(0, currentFrameIndex - 1);
            ShowCurrentFrame();
        }

        private void NextFrame()
        {
            StopPreview();
            if (frames.Length == 0) return;
            currentFrameIndex = Math.Min(frames.Length - 1, currentFrameIndex + 1);
            ShowCurrentFrame();
        }

        private void AdvancePreview()
        {
            if (frames.Length == 0)
            {
                StopPreview();
                return;
            }

            currentFrameIndex++;
            if (currentFrameIndex >= frames.Length)
            {
                if (loopCheckBox.IsChecked == true)
                {
                    currentFrameIndex = 0;
                }
                else
                {
                    currentFrameIndex = frames.Length - 1;
                    StopPreview();
                }
            }

            ShowCurrentFrame();
        }

        private void ShowCurrentFrame()
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.UriSource = new Uri(frames[currentFrameIndex], UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            previewImage.Source = image;
            frameCounterText.Text = String.Format("Frame {0} / {1}", currentFrameIndex + 1, frames.Length);
        }

        private void UpdatePreviewTimer()
        {
            var fps = Math.Max(1, previewFpsBox.GetInt(12));
            previewTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, 1000 / fps));
        }

        private void UpdateNamePreview()
        {
            var format = formatComboBox.SelectedItem == null ? "png" : Convert.ToString(formatComboBox.SelectedItem, CultureInfo.InvariantCulture);
            namePreviewText.Text = "Output: " + PathUtils.NewFrameFileName(characterTextBox.Text, actionTextBox.Text, 1, format);
        }

        private void SetStatus(string message)
        {
            statusText.Text = message;
            AddLog(message);
        }

        private void AddLog(string message)
        {
            if (String.IsNullOrWhiteSpace(message)) return;
            logTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine);
            logTextBox.ScrollToEnd();
        }

        private static GroupBox NewGroup(string title, double height)
        {
            return new GroupBox
            {
                Header = title,
                Height = height,
                Margin = new Thickness(0, 0, 0, 12),
                Padding = new Thickness(10)
            };
        }

        private static Grid NewTwoColumnGrid()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(24) });
            return grid;
        }

        private static Grid NewFormGrid(int rows)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var index = 0; index < rows + 2; index++)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            }
            return grid;
        }

        private static void AddPathRow(Grid grid, int row, TextBox textBox, string buttonText, Action click)
        {
            textBox.Margin = new Thickness(0, 0, 8, 0);
            Grid.SetRow(textBox, row);
            Grid.SetColumn(textBox, 0);
            grid.Children.Add(textBox);
            grid.Children.Add(NewButton(buttonText, click, 76, row, 1));
        }

        private static void AddLabeledControl(Grid grid, int row, string label, Control control)
        {
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 0);
            grid.Children.Add(text);

            control.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
        }

        private static void AddFullRow(Grid grid, int row, UIElement element)
        {
            if (row >= grid.RowDefinitions.Count)
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            }

            var frameworkElement = element as FrameworkElement;
            if (frameworkElement != null)
            {
                frameworkElement.VerticalAlignment = VerticalAlignment.Center;
            }

            Grid.SetRow(element, row);
            Grid.SetColumn(element, 0);
            Grid.SetColumnSpan(element, Math.Max(1, grid.ColumnDefinitions.Count));
            grid.Children.Add(element);
        }

        private static Button NewButton(string text, Action click, double width, int row, int column)
        {
            var button = new Button { Content = text, Width = width, Height = 26 };
            button.Click += delegate { click(); };
            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);
            return button;
        }

        private static Button NewButton(string text, Action click, double width, double top, double left)
        {
            var button = new Button { Content = text, Width = width, Height = 26, Margin = new Thickness(left, top, 0, 0) };
            button.Click += delegate { click(); };
            return button;
        }
    }

    public sealed class DecimalBox : TextBox
    {
        public DecimalBox(string value)
        {
            Text = value;
            Width = 100;
        }

        public double GetDouble(double fallback)
        {
            double value;
            return Double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        public int GetInt(int fallback)
        {
            int value;
            return Int32.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }
    }
}
