using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using WpfShapes = System.Windows.Shapes;
using VideoToAnimationTool.Core;

namespace VideoToAnimationTool.Desktop
{
    public static class Program
    {
        [STAThread]
        public static int Main()
        {
            var app = new Application();
            app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e) { MessageBox.Show(e.Exception.Message, "Unexpected Error", MessageBoxButton.OK, MessageBoxImage.Error); e.Handled = true; };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e) { MessageBox.Show(Convert.ToString(e.ExceptionObject), "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error); };
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            app.Run(new MainWindow());
            return 0;
        }
    }

    public sealed class MainWindow : Window
    {
        private const int DefaultTolerance = 80;
        private const int DefaultSoftness = 60;
        private const int DefaultColorDespill = 75;
        private const int DefaultEdgeCleanup = 70;

        private readonly TextBox videoPathTextBox = new TextBox();
        private readonly TextBox imagePathTextBox = new TextBox();
        private readonly TextBox outputFolderTextBox = new TextBox();
        private readonly TextBox frameFolderTextBox = new TextBox();
        private readonly TextBox characterTextBox = new TextBox { Text = "Character" };
        private readonly TextBox actionTextBox = new TextBox { Text = "Action" };
        private readonly DecimalBox startSecondsBox = new DecimalBox("0");
        private readonly DecimalBox endSecondsBox = new DecimalBox("10");
        private readonly DecimalBox exportFpsBox = new DecimalBox("12");
        private readonly DecimalBox previewFpsBox = new DecimalBox("12");
        private readonly DecimalBox toleranceBox = new DecimalBox(DefaultTolerance.ToString(CultureInfo.InvariantCulture));
        private readonly DecimalBox softnessBox = new DecimalBox(DefaultSoftness.ToString(CultureInfo.InvariantCulture));
        private readonly DecimalBox colorDespillBox = new DecimalBox(DefaultColorDespill.ToString(CultureInfo.InvariantCulture));
        private readonly DecimalBox edgeCleanupBox = new DecimalBox(DefaultEdgeCleanup.ToString(CultureInfo.InvariantCulture));
        private readonly Slider toleranceSlider = NewParameterSlider(DefaultTolerance);
        private readonly Slider softnessSlider = NewParameterSlider(DefaultSoftness);
        private readonly Slider colorDespillSlider = NewParameterSlider(DefaultColorDespill);
        private readonly Slider edgeCleanupSlider = NewParameterSlider(DefaultEdgeCleanup);
        private readonly ComboBox presetComboBox = new ComboBox();
        private readonly Button loadPresetButton = new Button { Content = "Load" };
        private readonly Button savePresetButton = new Button { Content = "Save New" };
        private readonly Button updatePresetButton = new Button { Content = "Update" };
        private readonly Button deletePresetButton = new Button { Content = "Delete" };
        private readonly List<BackgroundPreset> presets = new List<BackgroundPreset>();
        private readonly ComboBox formatComboBox = new ComboBox();
        private readonly TextBlock metadataText = new TextBlock { Text = "Select a video to begin.", Foreground = BrushFrom(203, 213, 225) };
        private readonly TextBlock namePreviewText = new TextBlock { Foreground = BrushFrom(203, 213, 225) };
        private readonly TextBlock frameCounterText = new TextBlock { Text = "Frame 0 / 0", Foreground = BrushFrom(203, 213, 225) };
        private readonly TextBlock statusText = new TextBlock { Text = "Ready.", Foreground = BrushFrom(203, 213, 225) };
        private readonly TextBox logTextBox = new TextBox();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly Image previewImage = new Image { Stretch = Stretch.Uniform };
        private readonly Canvas previewCanvas = new Canvas();
        private readonly WpfShapes.Polyline lassoLine = new WpfShapes.Polyline { StrokeThickness = 2 };
        private readonly ListBox frameListBox = new ListBox();
        private readonly Button exportButton = new Button { Content = "Generate Frames" };
        private readonly Button cancelButton = new Button { Content = "Cancel", IsEnabled = false };
        private readonly Button removeSelectedButton = new Button { Content = "Remove Selected" };
        private readonly Button removeAllButton = new Button { Content = "Apply All Frames" };
        private readonly Button undoButton = new Button { Content = "Undo Last", IsEnabled = false };
        private readonly Button lassoModeButton = new Button { Content = "Start Lasso" };
        private readonly Button clearLassoButton = new Button { Content = "Clear Lasso" };
        private readonly Button removeWatermarkSelectedButton = new Button { Content = "Remove Selected" };
        private readonly Button removeWatermarkAllButton = new Button { Content = "Apply All Frames" };
        private readonly Button playButton = new Button { Content = "Play" };
        private readonly CheckBox loopCheckBox = new CheckBox { Content = "Loop", IsChecked = true };
        private readonly DispatcherTimer previewTimer = new DispatcherTimer();
        private readonly Stack<UndoBatch> undoStack = new Stack<UndoBatch>();
        private string[] frames = new string[0];
        private int currentFrameIndex;
        private Process exportProcess;
        private bool lassoMode;
        private readonly List<Point> lassoPoints = new List<Point>();
        private readonly List<System.Drawing.PointF> lassoImagePoints = new List<System.Drawing.PointF>();

        public MainWindow()
        {
            Title = "AI Character Animation Frame Tool";
            Width = 1280;
            Height = 820;
            MinWidth = 980;
            MinHeight = 680;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InstallDarkTheme();
            formatComboBox.Items.Add("png");
            formatComboBox.Items.Add("jpg");
            formatComboBox.SelectedIndex = 0;
            formatComboBox.Foreground = Brushes.Black;
            formatComboBox.Background = Brushes.White;
            Content = BuildLayout();
            WireEvents();
            LoadPresets();
            UpdateNamePreview();
            AddLog("Ready. Generate frames or load an image, select a thumbnail, then remove the background or watermark.");
        }

        private void InstallDarkTheme()
        {
            Background = BrushFrom(11, 18, 32);
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Resources[typeof(TextBlock)] = StyleForTextBlock();
            Resources[typeof(TextBox)] = StyleForTextBox();
            Resources[typeof(Button)] = StyleForButton();
            Resources[typeof(CheckBox)] = StyleForCheckBox();
            Resources[typeof(ProgressBar)] = StyleForProgressBar();
        }

        private UIElement BuildLayout()
        {
            var root = new Grid { Margin = new Thickness(12), Background = BrushFrom(11, 18, 32) };
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(560), MinWidth = 360 });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150), MinHeight = 90 });

            var left = new ScrollViewer { Content = BuildLeftPanel(), VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = BrushFrom(11, 18, 32) };
            Grid.SetColumn(left, 0);
            Grid.SetRow(left, 0);
            root.Children.Add(left);

            var columnSplitter = NewGridSplitter(Orientation.Vertical);
            Grid.SetColumn(columnSplitter, 1);
            Grid.SetRow(columnSplitter, 0);
            root.Children.Add(columnSplitter);

            var preview = BuildPreviewPanel();
            Grid.SetColumn(preview, 2);
            Grid.SetRow(preview, 0);
            root.Children.Add(preview);

            var rowSplitter = NewGridSplitter(Orientation.Horizontal);
            Grid.SetColumn(rowSplitter, 0);
            Grid.SetColumnSpan(rowSplitter, 3);
            Grid.SetRow(rowSplitter, 1);
            root.Children.Add(rowSplitter);

            var log = BuildLogPanel();
            Grid.SetColumn(log, 0);
            Grid.SetColumnSpan(log, 3);
            Grid.SetRow(log, 2);
            root.Children.Add(log);
            return root;
        }

        private UIElement BuildLeftPanel()
        {
            var panel = new StackPanel { Background = BrushFrom(11, 18, 32) };
            panel.Children.Add(BuildSourceGroup());
            panel.Children.Add(BuildImageGroup());
            panel.Children.Add(BuildExportGroup());
            panel.Children.Add(BuildFrameFolderGroup());
            panel.Children.Add(BuildGreenScreenGroup());
            panel.Children.Add(BuildWatermarkGroup());
            return panel;
        }

        private UIElement BuildSourceGroup()
        {
            var card = NewCard("1. Source Video", 96);
            var grid = NewTwoColumnGrid();
            AddPathRow(grid, 0, videoPathTextBox, "Browse", BrowseVideo);
            AddFullRow(grid, 1, metadataText);
            SetCardContent(card, grid);
            return card;
        }

        private UIElement BuildImageGroup()
        {
            var card = NewCard("1B. Single Image", 96);
            var grid = NewTwoColumnGrid();
            AddPathRow(grid, 0, imagePathTextBox, "Browse", BrowseImage);
            AddFullRow(grid, 1, new TextBlock { Text = "Load one PNG/JPG for background or watermark removal.", Foreground = BrushFrom(203, 213, 225), TextWrapping = TextWrapping.Wrap });
            SetCardContent(card, grid);
            return card;
        }

        private UIElement BuildExportGroup()
        {
            var card = NewCard("2. Sequence Frame Generation", 350);
            var grid = NewFormGrid(9);
            AddLabeledControl(grid, 0, "Character", characterTextBox);
            AddLabeledControl(grid, 1, "Action", actionTextBox);
            AddLabeledPathRow(grid, 2, "Output Folder", outputFolderTextBox, "Browse", BrowseOutputFolder);
            AddLabeledControl(grid, 3, "Start Seconds", startSecondsBox);
            AddLabeledControl(grid, 4, "End Seconds", endSecondsBox);
            AddLabeledControl(grid, 5, "Frames / Sec", exportFpsBox);
            AddLabeledControl(grid, 6, "Format", formatComboBox);
            AddFullRow(grid, 7, namePreviewText);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(124, 10, 0, 0) };
            exportButton.Width = 144;
            exportButton.Height = 34;
            cancelButton.Width = 98;
            cancelButton.Height = 34;
            cancelButton.Margin = new Thickness(12, 0, 0, 0);
            buttons.Children.Add(exportButton);
            buttons.Children.Add(cancelButton);
            AddFullRow(grid, 8, buttons);
            SetCardContent(card, grid);
            return card;
        }

        private UIElement BuildFrameFolderGroup()
        {
            var card = NewCard("3. Frame Folder", 92);
            var grid = NewTwoColumnGrid();
            AddPathRow(grid, 0, frameFolderTextBox, "Load", BrowseFrameFolder);
            SetCardContent(card, grid);
            return card;
        }

        private UIElement BuildGreenScreenGroup()
        {
            var card = NewCard("4. Background Removal", 440);
            var grid = NewFormGrid(10);
            grid.RowDefinitions[0].Height = new GridLength(36);
            grid.RowDefinitions[1].Height = new GridLength(46);
            grid.RowDefinitions[6].Height = GridLength.Auto;
            grid.RowDefinitions[7].Height = new GridLength(50);
            grid.RowDefinitions[8].Height = GridLength.Auto;
            AddLabeledElement(grid, 0, "Preset", BuildPresetLoadControl());
            AddFullRow(grid, 1, BuildPresetEditButtons());
            AddLabeledElement(grid, 2, "Tolerance", BuildSliderNumberControl(toleranceSlider, toleranceBox));
            AddLabeledElement(grid, 3, "Edge Softness", BuildSliderNumberControl(softnessSlider, softnessBox));
            AddLabeledElement(grid, 4, "Color Despill", BuildSliderNumberControl(colorDespillSlider, colorDespillBox));
            AddLabeledElement(grid, 5, "Edge Cleanup", BuildSliderNumberControl(edgeCleanupSlider, edgeCleanupBox));
            AddFullRow(grid, 6, new TextBlock { Text = "Select a frame thumbnail, test removal on that frame, then apply to all when it looks right.", Foreground = BrushFrom(203, 213, 225), TextWrapping = TextWrapping.Wrap });
            var buttons = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            buttons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
            removeSelectedButton.Height = 34;
            removeSelectedButton.MinWidth = 124;
            removeAllButton.Height = 34;
            removeAllButton.MinWidth = 124;
            undoButton.Height = 34;
            undoButton.MinWidth = 96;
            Grid.SetColumn(removeSelectedButton, 0);
            Grid.SetColumn(removeAllButton, 2);
            Grid.SetColumn(undoButton, 4);
            buttons.Children.Add(removeSelectedButton);
            buttons.Children.Add(removeAllButton);
            buttons.Children.Add(undoButton);
            AddFullRow(grid, 7, buttons);
            AddFullRow(grid, 8, new TextBlock { Text = "Output: current frame folder\\cutout. Undo restores the last removal operation.", Foreground = BrushFrom(203, 213, 225), TextWrapping = TextWrapping.Wrap });
            SetCardContent(card, grid);
            return card;
        }

        private UIElement BuildWatermarkGroup()
        {
            var card = NewCard("5. Watermark Removal", 178);
            var grid = NewFormGrid(4);
            grid.RowDefinitions[0].Height = GridLength.Auto;
            grid.RowDefinitions[1].Height = new GridLength(50);
            grid.RowDefinitions[2].Height = new GridLength(50);
            grid.RowDefinitions[3].Height = GridLength.Auto;
            AddFullRow(grid, 0, new TextBlock { Text = "Start lasso, click around the watermark on the preview, then double-click to close the lasso.", Foreground = BrushFrom(203, 213, 225), TextWrapping = TextWrapping.Wrap });

            var row1 = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row1.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lassoModeButton.Height = 34;
            clearLassoButton.Height = 34;
            Grid.SetColumn(lassoModeButton, 0);
            Grid.SetColumn(clearLassoButton, 2);
            row1.Children.Add(lassoModeButton);
            row1.Children.Add(clearLassoButton);
            AddFullRow(grid, 1, row1);

            var row2 = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            removeWatermarkSelectedButton.Height = 34;
            removeWatermarkAllButton.Height = 34;
            Grid.SetColumn(removeWatermarkSelectedButton, 0);
            Grid.SetColumn(removeWatermarkAllButton, 2);
            row2.Children.Add(removeWatermarkSelectedButton);
            row2.Children.Add(removeWatermarkAllButton);
            AddFullRow(grid, 2, row2);

            AddFullRow(grid, 3, new TextBlock { Text = "Output: current frame folder\\watermark_removed. Use Undo Last if the fill is not right.", Foreground = BrushFrom(203, 213, 225), TextWrapping = TextWrapping.Wrap });
            SetCardContent(card, grid);
            return card;
        }

        private UIElement BuildPreviewPanel()
        {
            var card = NewCard("Animation Preview", Double.NaN);
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star), MinHeight = 180 });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(6) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(132), MinHeight = 80 });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(52) });

            previewCanvas.Background = Brushes.Transparent;
            previewCanvas.Children.Add(previewImage);
            previewCanvas.Children.Add(lassoLine);
            lassoLine.Stroke = BrushFrom(34, 211, 238);
            lassoLine.Fill = new SolidColorBrush(Color.FromArgb(34, 34, 211, 238));
            previewCanvas.SizeChanged += delegate { ResizePreviewImage(); };
            var border = new Border { Background = BrushFrom(2, 6, 23), BorderBrush = BrushFrom(51, 65, 85), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Child = previewCanvas, Padding = new Thickness(8) };
            Grid.SetRow(border, 0);
            grid.Children.Add(border);

            frameListBox.Background = BrushFrom(15, 23, 42);
            frameListBox.Foreground = BrushFrom(226, 232, 240);
            frameListBox.BorderBrush = BrushFrom(51, 65, 85);
            frameListBox.Margin = new Thickness(0, 10, 0, 8); frameListBox.MinHeight = 118;
            frameListBox.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
            frameListBox.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
            var itemsPanel = new FrameworkElementFactory(typeof(WrapPanel));
            itemsPanel.SetValue(WrapPanel.OrientationProperty, Orientation.Horizontal);
            frameListBox.ItemsPanel = new ItemsPanelTemplate(itemsPanel);
            var previewSplitter = NewGridSplitter(Orientation.Horizontal);
            Grid.SetRow(previewSplitter, 1);
            grid.Children.Add(previewSplitter);

            Grid.SetRow(frameListBox, 2);
            grid.Children.Add(frameListBox);

            var controls = new StackPanel { Orientation = Orientation.Horizontal };
            playButton.Width = 76;
            controls.Children.Add(playButton);
            controls.Children.Add(new TextBlock { Text = "Preview FPS", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 8, 0), Foreground = BrushFrom(203, 213, 225) });
            previewFpsBox.Width = 70;
            controls.Children.Add(previewFpsBox);
            loopCheckBox.Margin = new Thickness(20, 6, 0, 0); loopCheckBox.Foreground = BrushFrom(226, 232, 240);
            controls.Children.Add(loopCheckBox);
            frameCounterText.Margin = new Thickness(24, 6, 0, 0);
            controls.Children.Add(frameCounterText);
            Grid.SetRow(controls, 3);
            grid.Children.Add(controls);
            SetCardContent(card, grid);
            return card;
        }

        private UIElement BuildLogPanel()
        {
            var grid = new Grid { Background = BrushFrom(11, 18, 32) };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(28) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(progressBar, 0);
            grid.Children.Add(progressBar);
            Grid.SetRow(statusText, 1);
            grid.Children.Add(statusText);
            logTextBox.AcceptsReturn = true;
            logTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            logTextBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            logTextBox.IsReadOnly = true;
            logTextBox.FontFamily = new FontFamily("Consolas");
            logTextBox.Background = BrushFrom(2, 6, 23);
            logTextBox.Foreground = BrushFrom(203, 213, 225);
            logTextBox.BorderBrush = BrushFrom(51, 65, 85);
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
            removeSelectedButton.Click += async delegate { await RemoveSelectedGreenScreenAsync(); };
            removeAllButton.Click += async delegate { await RemoveAllGreenScreenAsync(); };
            undoButton.Click += delegate { UndoLastOperation(); };
            lassoModeButton.Click += delegate { ToggleLassoMode(); };
            clearLassoButton.Click += delegate { ClearLasso(); };
            removeWatermarkSelectedButton.Click += async delegate { await RemoveSelectedWatermarkAsync(); };
            removeWatermarkAllButton.Click += async delegate { await RemoveAllWatermarkAsync(); };
            loadPresetButton.Click += delegate { LoadSelectedPreset(); };
            savePresetButton.Click += delegate { SaveCurrentAsNewPreset(); };
            updatePresetButton.Click += delegate { UpdateSelectedPreset(); };
            deletePresetButton.Click += delegate { DeleteSelectedPreset(); };
            playButton.Click += delegate { TogglePreview(); };
            previewFpsBox.TextChanged += delegate { UpdatePreviewTimer(); };
            BindSliderToBox(toleranceSlider, toleranceBox, DefaultTolerance);
            BindSliderToBox(softnessSlider, softnessBox, DefaultSoftness);
            BindSliderToBox(colorDespillSlider, colorDespillBox, DefaultColorDespill);
            BindSliderToBox(edgeCleanupSlider, edgeCleanupBox, DefaultEdgeCleanup);
            frameListBox.SelectionChanged += delegate { SelectFrameFromList(); };
            previewCanvas.MouseLeftButtonDown += PreviewCanvasMouseLeftButtonDown;
            previewCanvas.MouseMove += PreviewCanvasMouseMove;
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

            if (!Directory.Exists(outputFolderTextBox.Text)) Directory.CreateDirectory(outputFolderTextBox.Text);
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
                if (MessageBox.Show("The output folder already contains " + existing.Length + " matching frame file(s). Delete them before exporting?", "Confirm Replace Frames", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
                foreach (var path in existing) File.Delete(path);
                AddLog("Deleted " + existing.Length + " old matching frame file(s) before export.");
            }

            var args = FfmpegHelper.BuildFrameExportArguments(videoPathTextBox.Text, outputFolderTextBox.Text, characterTextBox.Text, actionTextBox.Text, start, end, fps, format);
            AddLog("Running: \"" + ffmpeg + "\" " + FfmpegHelper.ToArgumentString(args));
            var startInfo = new ProcessStartInfo { FileName = ffmpeg, Arguments = FfmpegHelper.ToArgumentString(args), UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            exportButton.IsEnabled = false;
            cancelButton.IsEnabled = true;
            progressBar.IsIndeterminate = true;
            SetStatus("Frame export started.");
            exportProcess = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            exportProcess.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) Dispatcher.Invoke(delegate { AddLog(e.Data); }); };
            exportProcess.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e) { if (e.Data != null) Dispatcher.Invoke(delegate { AddLog(e.Data); }); };
            await Task.Run(delegate { exportProcess.Start(); exportProcess.BeginOutputReadLine(); exportProcess.BeginErrorReadLine(); exportProcess.WaitForExit(); });
            var exitCode = exportProcess.ExitCode;
            exportProcess.Dispose();
            exportProcess = null;
            progressBar.IsIndeterminate = false;
            exportButton.IsEnabled = true;
            cancelButton.IsEnabled = false;
            if (exitCode == 0) { SetStatus("Frame export finished."); LoadFrameFolder(outputFolderTextBox.Text); }
            else SetStatus("Export failed with exit code " + exitCode + ".");
        }

        private async Task RemoveSelectedGreenScreenAsync()
        {
            if (frames.Length == 0 || currentFrameIndex < 0 || currentFrameIndex >= frames.Length)
            {
                MessageBox.Show("Select a frame thumbnail first.", "No Frame Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var inputFolder = Path.GetDirectoryName(frames[currentFrameIndex]);
            var outputFolder = GetCutoutFolder(inputFolder);
            var sourcePath = frames[currentFrameIndex];
            var outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(sourcePath) + ".png");
            var tolerance = toleranceBox.GetInt(DefaultTolerance);
            var softness = softnessBox.GetInt(DefaultSoftness);
            var colorDespill = colorDespillBox.GetInt(DefaultColorDespill);
            var edgeCleanup = edgeCleanupBox.GetInt(DefaultEdgeCleanup);

            try
            {
                SetBusyForChroma(true, "Removing background from selected frame.");
                await Task.Run(delegate
                {
                    if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
                    var undo = CaptureUndo(outputFolder, new[] { outputPath }, inputFolder, sourcePath);
                    ProcessOneFrame(sourcePath, outputPath, tolerance, softness, colorDespill, edgeCleanup);
                    Dispatcher.Invoke(delegate { undoStack.Push(undo); undoButton.IsEnabled = true; });
                });
                SetBusyForChroma(false, "Selected frame background removal finished. Preview is showing the processed frame.");
                previewImage.Source = LoadBitmapImage(outputPath, 1200);
            }
            catch (Exception ex)
            {
                SetBusyForChroma(false, "Selected frame removal failed.");
                MessageBox.Show(ex.Message, "Background Removal Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog(ex.ToString());
            }
        }
        private async Task RemoveAllGreenScreenAsync()
        {
            var inputFolder = String.IsNullOrWhiteSpace(frameFolderTextBox.Text) ? outputFolderTextBox.Text : frameFolderTextBox.Text;
            if (String.IsNullOrWhiteSpace(inputFolder) || !Directory.Exists(inputFolder))
            {
                MessageBox.Show("Load or generate a frame folder first.", "No Frame Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sourceFrames = PathUtils.GetFrameFiles(inputFolder);
            if (sourceFrames.Length > 0 && MessageBox.Show("Apply background removal to all " + sourceFrames.Length + " frames?", "Apply To All", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (sourceFrames.Length == 0)
            {
                MessageBox.Show("No PNG/JPG frames were found.", "No Frames", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var outputFolder = GetCutoutFolder(inputFolder);
            var tolerance = toleranceBox.GetInt(DefaultTolerance);
            var softness = softnessBox.GetInt(DefaultSoftness);
            var colorDespill = colorDespillBox.GetInt(DefaultColorDespill);
            var edgeCleanup = edgeCleanupBox.GetInt(DefaultEdgeCleanup);

            try
            {
                SetBusyForChroma(true, "Applying background removal to all frames.");
                await Task.Run(delegate
                {
                    EnsureCutoutMirror(inputFolder, outputFolder);
                    var outputPaths = new List<string>();
                    foreach (var frame in sourceFrames) outputPaths.Add(Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(frame) + ".png"));
                    var undo = CaptureUndo(outputFolder, outputPaths.ToArray(), inputFolder, null);
                    for (var i = 0; i < sourceFrames.Length; i++)
                    {
                        ProcessOneFrame(sourceFrames[i], outputPaths[i], tolerance, softness, colorDespill, edgeCleanup);
                        var done = i + 1;
                        var total = sourceFrames.Length;
                        Dispatcher.Invoke(delegate { SetStatus("Background removal " + done + " / " + total); });
                    }
                    Dispatcher.Invoke(delegate { undoStack.Push(undo); undoButton.IsEnabled = true; });
                });
                SetBusyForChroma(false, "All-frame background removal finished.");
                LoadFrameFolder(outputFolder);
            }
            catch (Exception ex)
            {
                SetBusyForChroma(false, "All-frame removal failed.");
                MessageBox.Show(ex.Message, "Background Removal Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog(ex.ToString());
            }
        }
        private void UndoLastOperation()
        {
            if (undoStack.Count == 0) return;
            var undo = undoStack.Pop();
            foreach (var item in undo.Items)
            {
                if (item.Existed) File.WriteAllBytes(item.Path, item.PreviousBytes);
                else if (File.Exists(item.Path)) File.Delete(item.Path);
            }
            undoButton.IsEnabled = undoStack.Count > 0;
            SetStatus("Undid last background removal operation.");
            if (Directory.Exists(undo.RestoreFolder)) LoadFrameFolder(undo.RestoreFolder);
            if (!String.IsNullOrWhiteSpace(undo.SelectPath)) SelectFrameByPath(undo.SelectPath);
        }

        private async Task RemoveSelectedWatermarkAsync()
        {
            if (frames.Length == 0 || currentFrameIndex < 0 || currentFrameIndex >= frames.Length)
            {
                MessageBox.Show("Select a frame thumbnail first.", "No Frame Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (lassoImagePoints.Count < 3)
            {
                MessageBox.Show("Draw a lasso around the watermark first.", "No Lasso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var inputFolder = Path.GetDirectoryName(frames[currentFrameIndex]);
            var outputFolder = GetWatermarkFolder(inputFolder);
            var sourcePath = frames[currentFrameIndex];
            var outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(sourcePath) + ".png");
            var polygon = lassoImagePoints.ToArray();

            try
            {
                SetBusyForWatermark(true, "Removing watermark from selected frame.");
                await Task.Run(delegate
                {
                    if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
                    var undo = CaptureUndo(outputFolder, new[] { outputPath }, inputFolder, sourcePath);
                    ProcessWatermarkFrame(sourcePath, outputPath, polygon);
                    Dispatcher.Invoke(delegate { undoStack.Push(undo); undoButton.IsEnabled = true; });
                });
                SetBusyForWatermark(false, "Selected frame watermark removal finished.");
                previewImage.Source = LoadBitmapImage(outputPath, 1200);
            }
            catch (Exception ex)
            {
                SetBusyForWatermark(false, "Selected watermark removal failed.");
                MessageBox.Show(ex.Message, "Watermark Removal Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog(ex.ToString());
            }
        }

        private async Task RemoveAllWatermarkAsync()
        {
            var inputFolder = String.IsNullOrWhiteSpace(frameFolderTextBox.Text) ? outputFolderTextBox.Text : frameFolderTextBox.Text;
            if (String.IsNullOrWhiteSpace(inputFolder) || !Directory.Exists(inputFolder))
            {
                MessageBox.Show("Load or generate a frame folder first.", "No Frame Folder", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (lassoImagePoints.Count < 3)
            {
                MessageBox.Show("Draw a lasso around the watermark first.", "No Lasso", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var sourceFrames = PathUtils.GetFrameFiles(inputFolder);
            if (sourceFrames.Length > 0 && MessageBox.Show("Apply watermark removal to all " + sourceFrames.Length + " frames using the current lasso?", "Apply To All", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (sourceFrames.Length == 0)
            {
                MessageBox.Show("No PNG/JPG frames were found.", "No Frames", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var outputFolder = GetWatermarkFolder(inputFolder);
            var polygon = lassoImagePoints.ToArray();

            try
            {
                SetBusyForWatermark(true, "Applying watermark removal to all frames.");
                await Task.Run(delegate
                {
                    if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
                    var outputPaths = new List<string>();
                    foreach (var frame in sourceFrames) outputPaths.Add(Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(frame) + ".png"));
                    var undo = CaptureUndo(outputFolder, outputPaths.ToArray(), inputFolder, null);
                    for (var i = 0; i < sourceFrames.Length; i++)
                    {
                        ProcessWatermarkFrame(sourceFrames[i], outputPaths[i], polygon);
                        var done = i + 1;
                        var total = sourceFrames.Length;
                        Dispatcher.Invoke(delegate { SetStatus("Watermark removal " + done + " / " + total); });
                    }
                    Dispatcher.Invoke(delegate { undoStack.Push(undo); undoButton.IsEnabled = true; });
                });
                SetBusyForWatermark(false, "All-frame watermark removal finished.");
                LoadFrameFolder(outputFolder);
            }
            catch (Exception ex)
            {
                SetBusyForWatermark(false, "All-frame watermark removal failed.");
                MessageBox.Show(ex.Message, "Watermark Removal Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                AddLog(ex.ToString());
            }
        }

        private void ToggleLassoMode()
        {
            lassoMode = !lassoMode;
            lassoModeButton.Content = lassoMode ? "Finish Lasso" : "Start Lasso";
            previewCanvas.Cursor = lassoMode ? Cursors.Cross : Cursors.Arrow;
            if (lassoMode) SetStatus("Lasso mode on. Click around the watermark; double-click closes the shape.");
            else CloseLassoIfPossible();
        }

        private void PreviewCanvasMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (!lassoMode) return;
            var canvasPoint = e.GetPosition(previewCanvas);
            System.Drawing.PointF imagePoint;
            if (!TryCanvasPointToImagePoint(canvasPoint, out imagePoint)) return;

            lassoPoints.Add(canvasPoint);
            lassoImagePoints.Add(imagePoint);
            RedrawLasso(false);
            if (e.ClickCount >= 2)
            {
                lassoMode = false;
                lassoModeButton.Content = "Start Lasso";
                previewCanvas.Cursor = Cursors.Arrow;
                CloseLassoIfPossible();
            }
        }

        private void PreviewCanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (!lassoMode || lassoPoints.Count == 0) return;
            RedrawLasso(false, e.GetPosition(previewCanvas));
        }

        private void ClearLasso()
        {
            lassoPoints.Clear();
            lassoImagePoints.Clear();
            lassoLine.Points.Clear();
            lassoMode = false;
            lassoModeButton.Content = "Start Lasso";
            previewCanvas.Cursor = Cursors.Arrow;
            SetStatus("Watermark lasso cleared.");
        }

        private void CloseLassoIfPossible()
        {
            if (lassoPoints.Count >= 3)
            {
                RedrawLasso(true);
                SetStatus("Watermark lasso ready.");
            }
        }

        private void RedrawLasso(bool closed)
        {
            RedrawLasso(closed, null);
        }

        private void RedrawLasso(bool closed, Point? previewPoint)
        {
            lassoLine.Points.Clear();
            foreach (var point in lassoPoints) lassoLine.Points.Add(point);
            if (previewPoint.HasValue) lassoLine.Points.Add(previewPoint.Value);
            if (closed && lassoPoints.Count > 0) lassoLine.Points.Add(lassoPoints[0]);
        }

        private void SetBusyForChroma(bool busy, string message)
        {
            removeSelectedButton.IsEnabled = !busy;
            removeAllButton.IsEnabled = !busy;
            undoButton.IsEnabled = !busy && undoStack.Count > 0;
            progressBar.IsIndeterminate = busy;
            SetStatus(message);
        }

        private void SetBusyForWatermark(bool busy, string message)
        {
            lassoModeButton.IsEnabled = !busy;
            clearLassoButton.IsEnabled = !busy;
            removeWatermarkSelectedButton.IsEnabled = !busy;
            removeWatermarkAllButton.IsEnabled = !busy;
            undoButton.IsEnabled = !busy && undoStack.Count > 0;
            progressBar.IsIndeterminate = busy;
            SetStatus(message);
        }

        private string GetCutoutFolder(string inputFolder)
        {
            return String.Equals(Path.GetFileName(inputFolder), "cutout", StringComparison.OrdinalIgnoreCase) ? inputFolder : Path.Combine(inputFolder, "cutout");
        }

        private string GetWatermarkFolder(string inputFolder)
        {
            return String.Equals(Path.GetFileName(inputFolder), "watermark_removed", StringComparison.OrdinalIgnoreCase) ? inputFolder : Path.Combine(inputFolder, "watermark_removed");
        }

        private void EnsureCutoutMirror(string inputFolder, string outputFolder)
        {
            if (!Directory.Exists(outputFolder)) Directory.CreateDirectory(outputFolder);
            foreach (var frame in PathUtils.GetFrameFiles(inputFolder))
            {
                if (String.Equals(Path.GetDirectoryName(frame), outputFolder, StringComparison.OrdinalIgnoreCase)) continue;
                var outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(frame) + ".png");
                if (File.Exists(outputPath)) continue;
                using (var bitmap = new System.Drawing.Bitmap(frame)) bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        private UndoBatch CaptureUndo(string folder, string[] outputPaths, string restoreFolder, string selectPath)
        {
            var undo = new UndoBatch(folder, restoreFolder, selectPath);
            foreach (var path in outputPaths)
            {
                undo.Items.Add(new UndoItem(path, File.Exists(path), File.Exists(path) ? File.ReadAllBytes(path) : null));
            }
            return undo;
        }

        private void ProcessOneFrame(string sourcePath, string outputPath, int tolerance, int softness, int colorDespill, int edgeCleanup)
        {
            System.Drawing.Bitmap cutout;
            using (var source = new System.Drawing.Bitmap(sourcePath)) cutout = GreenScreenRemover.RemoveGreenScreen(source, tolerance, softness, colorDespill, edgeCleanup);
            using (cutout) cutout.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        }

        private void ProcessWatermarkFrame(string sourcePath, string outputPath, System.Drawing.PointF[] polygon)
        {
            System.Drawing.Bitmap cleaned;
            using (var source = new System.Drawing.Bitmap(sourcePath)) cleaned = WatermarkRemover.RemoveWatermark(source, polygon);
            using (cleaned) cleaned.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        }

        private void CancelExport() { if (exportProcess != null && !exportProcess.HasExited) { exportProcess.Kill(); SetStatus("Export cancelled."); } }

        private void BrowseVideo()
        {
            var dialog = new OpenFileDialog { Title = "Choose AI character action video", Filter = "Video files|*.mp4;*.mov;*.webm;*.avi|All files|*.*" };
            if (dialog.ShowDialog(this) == true)
            {
                videoPathTextBox.Text = dialog.FileName;
                metadataText.Text = "Selected: " + Path.GetFileName(dialog.FileName);
                if (String.IsNullOrWhiteSpace(outputFolderTextBox.Text)) outputFolderTextBox.Text = Path.Combine(Path.GetDirectoryName(dialog.FileName), "frames");
            }
        }

        private void BrowseImage()
        {
            var dialog = new OpenFileDialog { Title = "Choose character image", Filter = "Image files|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*" };
            if (dialog.ShowDialog(this) == true)
            {
                imagePathTextBox.Text = dialog.FileName;
                LoadSingleImage(dialog.FileName);
            }
        }

        private void BrowseOutputFolder()
        {
            var folder = ChooseFolder(outputFolderTextBox.Text, "Choose output folder for sequence frames");
            if (!String.IsNullOrWhiteSpace(folder)) outputFolderTextBox.Text = folder;
        }

        private void BrowseFrameFolder()
        {
            var folder = ChooseFolder(frameFolderTextBox.Text, "Choose an existing frame folder");
            if (!String.IsNullOrWhiteSpace(folder)) LoadFrameFolder(folder);
        }

        private static string ChooseFolder(string selectedPath, string description)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = description;
                if (!String.IsNullOrWhiteSpace(selectedPath)) dialog.SelectedPath = selectedPath;
                return dialog.ShowDialog() == Forms.DialogResult.OK ? dialog.SelectedPath : null;
            }
        }

        private void LoadFrameFolder(string folderPath)
        {
            frames = PathUtils.GetFrameFiles(folderPath);
            currentFrameIndex = 0;
            frameFolderTextBox.Text = folderPath;
            ClearLasso();
            BuildFrameThumbnailList();
            if (frames.Length == 0)
            {
                previewImage.Source = null;
                frameCounterText.Text = "Frame 0 / 0";
                SetStatus("No PNG/JPG frames found in the selected folder.");
                return;
            }
            frameListBox.SelectedIndex = 0;
            ShowCurrentFrame();
            SetStatus("Loaded " + frames.Length + " frame(s).");
        }

        private void LoadSingleImage(string imagePath)
        {
            if (String.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                SetStatus("Image file does not exist.");
                return;
            }

            frames = new[] { imagePath };
            currentFrameIndex = 0;
            frameFolderTextBox.Text = Path.GetDirectoryName(imagePath);
            ClearLasso();
            BuildFrameThumbnailList();
            frameListBox.SelectedIndex = 0;
            ShowCurrentFrame();
            SetStatus("Loaded single image: " + Path.GetFileName(imagePath));
        }

        private void BuildFrameThumbnailList()
        {
            frameListBox.Items.Clear();
            for (var i = 0; i < frames.Length; i++)
            {
                var stack = new StackPanel { Width = 96, Margin = new Thickness(4) };
                stack.Children.Add(new Image { Source = LoadBitmapImage(frames[i], 86), Width = 86, Height = 68, Stretch = Stretch.Uniform });
                stack.Children.Add(new TextBlock { Text = Path.GetFileName(frames[i]), TextAlignment = TextAlignment.Center, FontSize = 10, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = BrushFrom(203, 213, 225) });
                frameListBox.Items.Add(new ListBoxItem { Content = stack, Tag = i, Background = BrushFrom(15, 23, 42), Foreground = BrushFrom(226, 232, 240), BorderBrush = BrushFrom(51, 65, 85), Padding = new Thickness(3) });
            }
        }

        private BitmapImage LoadBitmapImage(string path, int decodeWidth)
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = decodeWidth;
            image.UriSource = new Uri(path, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }

        private void SelectFrameFromList()
        {
            var item = frameListBox.SelectedItem as ListBoxItem;
            if (item == null) return;
            currentFrameIndex = (int)item.Tag;
            ShowCurrentFrame();
        }

        private void SelectFrameByPath(string path)
        {
            for (var i = 0; i < frames.Length; i++)
            {
                if (String.Equals(frames[i], path, StringComparison.OrdinalIgnoreCase)) { frameListBox.SelectedIndex = i; return; }
            }
        }

        private void TogglePreview()
        {
            if (previewTimer.IsEnabled) StopPreview();
            else
            {
                if (frames.Length == 0) { SetStatus("Load a frame folder before preview playback."); return; }
                UpdatePreviewTimer();
                previewTimer.Start();
                playButton.Content = "Pause";
            }
        }

        private void StopPreview() { previewTimer.Stop(); playButton.Content = "Play"; }

        private void AdvancePreview()
        {
            if (frames.Length == 0) { StopPreview(); return; }
            currentFrameIndex++;
            if (currentFrameIndex >= frames.Length)
            {
                if (loopCheckBox.IsChecked == true) currentFrameIndex = 0;
                else { currentFrameIndex = frames.Length - 1; StopPreview(); }
            }
            frameListBox.SelectedIndex = currentFrameIndex;
            ShowCurrentFrame();
        }

        private void ShowCurrentFrame()
        {
            if (frames.Length == 0) return;
            previewImage.Source = LoadBitmapImage(frames[currentFrameIndex], 1200);
            frameCounterText.Text = String.Format("Frame {0} / {1}", currentFrameIndex + 1, frames.Length);
            ResizePreviewImage();
        }

        private void ResizePreviewImage()
        {
            previewImage.Width = Math.Max(0, previewCanvas.ActualWidth);
            previewImage.Height = Math.Max(0, previewCanvas.ActualHeight);
        }

        private bool TryCanvasPointToImagePoint(Point canvasPoint, out System.Drawing.PointF imagePoint)
        {
            imagePoint = new System.Drawing.PointF();
            var bitmap = previewImage.Source as BitmapSource;
            if (bitmap == null || previewCanvas.ActualWidth <= 0 || previewCanvas.ActualHeight <= 0 || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0) return false;

            var scale = Math.Min(previewCanvas.ActualWidth / bitmap.PixelWidth, previewCanvas.ActualHeight / bitmap.PixelHeight);
            var displayedWidth = bitmap.PixelWidth * scale;
            var displayedHeight = bitmap.PixelHeight * scale;
            var offsetX = (previewCanvas.ActualWidth - displayedWidth) / 2.0;
            var offsetY = (previewCanvas.ActualHeight - displayedHeight) / 2.0;
            var x = (canvasPoint.X - offsetX) / scale;
            var y = (canvasPoint.Y - offsetY) / scale;
            if (x < 0 || y < 0 || x >= bitmap.PixelWidth || y >= bitmap.PixelHeight) return false;
            imagePoint = new System.Drawing.PointF((float)x, (float)y);
            return true;
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

        private void BindSliderToBox(Slider slider, DecimalBox box, int fallback)
        {
            var syncing = false;
            slider.ValueChanged += delegate
            {
                if (syncing) return;
                syncing = true;
                box.Text = ((int)Math.Round(slider.Value)).ToString(CultureInfo.InvariantCulture);
                syncing = false;
            };

            box.TextChanged += delegate
            {
                if (syncing) return;
                int value;
                if (!Int32.TryParse(box.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) return;
                value = Math.Max(0, Math.Min(255, value));
                syncing = true;
                slider.Value = value;
                if (box.Text != value.ToString(CultureInfo.InvariantCulture)) box.Text = value.ToString(CultureInfo.InvariantCulture);
                syncing = false;
            };

            slider.Value = Math.Max(0, Math.Min(255, box.GetInt(fallback)));
        }

        private void LoadPresets()
        {
            presets.Clear();
            var path = GetPresetFilePath();
            if (File.Exists(path))
            {
                foreach (var line in File.ReadAllLines(path))
                {
                    var preset = BackgroundPreset.TryParse(line);
                    if (preset != null) presets.Add(preset);
                }
            }
            if (presets.Count == 0)
            {
                presets.Add(new BackgroundPreset("Balanced Default", DefaultTolerance, DefaultSoftness, DefaultColorDespill, DefaultEdgeCleanup));
                SavePresets();
            }
            RefreshPresetCombo();
        }

        private void SavePresets()
        {
            var path = GetPresetFilePath();
            var folder = Path.GetDirectoryName(path);
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
            File.WriteAllLines(path, presets.ConvertAll(preset => preset.Serialize()).ToArray());
        }

        private void RefreshPresetCombo()
        {
            var selectedName = presetComboBox.SelectedItem == null ? null : Convert.ToString(presetComboBox.SelectedItem, CultureInfo.InvariantCulture);
            presetComboBox.Items.Clear();
            foreach (var preset in presets) presetComboBox.Items.Add(preset.Name);
            if (!String.IsNullOrWhiteSpace(selectedName) && presetComboBox.Items.Contains(selectedName)) presetComboBox.SelectedItem = selectedName;
            else if (presetComboBox.Items.Count > 0) presetComboBox.SelectedIndex = 0;
        }

        private void LoadSelectedPreset()
        {
            var preset = GetSelectedPreset();
            if (preset == null) return;
            ApplyPreset(preset);
            SetStatus("Loaded background preset: " + preset.Name);
        }

        private void SaveCurrentAsNewPreset()
        {
            var name = PromptDialog.Ask(this, "Save Preset", "Preset name", "Preset " + DateTime.Now.ToString("HHmm", CultureInfo.InvariantCulture));
            if (String.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            if (FindPreset(name) != null)
            {
                MessageBox.Show("A preset with this name already exists. Use Update to overwrite it.", "Preset Exists", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            presets.Add(CapturePreset(name));
            SavePresets();
            RefreshPresetCombo();
            presetComboBox.SelectedItem = name;
            SetStatus("Saved background preset: " + name);
        }

        private void UpdateSelectedPreset()
        {
            var preset = GetSelectedPreset();
            if (preset == null) return;
            preset.Tolerance = toleranceBox.GetInt(DefaultTolerance);
            preset.Softness = softnessBox.GetInt(DefaultSoftness);
            preset.ColorDespill = colorDespillBox.GetInt(DefaultColorDespill);
            preset.EdgeCleanup = edgeCleanupBox.GetInt(DefaultEdgeCleanup);
            SavePresets();
            SetStatus("Updated background preset: " + preset.Name);
        }

        private void DeleteSelectedPreset()
        {
            var preset = GetSelectedPreset();
            if (preset == null) return;
            if (MessageBox.Show("Delete preset \"" + preset.Name + "\"?", "Delete Preset", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            presets.Remove(preset);
            if (presets.Count == 0) presets.Add(new BackgroundPreset("Balanced Default", DefaultTolerance, DefaultSoftness, DefaultColorDespill, DefaultEdgeCleanup));
            SavePresets();
            RefreshPresetCombo();
            SetStatus("Deleted background preset.");
        }

        private BackgroundPreset CapturePreset(string name)
        {
            return new BackgroundPreset(name, toleranceBox.GetInt(DefaultTolerance), softnessBox.GetInt(DefaultSoftness), colorDespillBox.GetInt(DefaultColorDespill), edgeCleanupBox.GetInt(DefaultEdgeCleanup));
        }

        private void ApplyPreset(BackgroundPreset preset)
        {
            toleranceBox.Text = preset.Tolerance.ToString(CultureInfo.InvariantCulture);
            softnessBox.Text = preset.Softness.ToString(CultureInfo.InvariantCulture);
            colorDespillBox.Text = preset.ColorDespill.ToString(CultureInfo.InvariantCulture);
            edgeCleanupBox.Text = preset.EdgeCleanup.ToString(CultureInfo.InvariantCulture);
        }

        private BackgroundPreset GetSelectedPreset()
        {
            var name = presetComboBox.SelectedItem == null ? null : Convert.ToString(presetComboBox.SelectedItem, CultureInfo.InvariantCulture);
            return FindPreset(name);
        }

        private BackgroundPreset FindPreset(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return null;
            foreach (var preset in presets) if (String.Equals(preset.Name, name, StringComparison.OrdinalIgnoreCase)) return preset;
            return null;
        }

        private static string GetPresetFilePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoToAnimationTool", "background-presets.tsv");
        }

        private void SetStatus(string message) { statusText.Text = message; AddLog(message); }

        private void AddLog(string message)
        {
            if (String.IsNullOrWhiteSpace(message)) return;
            logTextBox.AppendText("[" + DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture) + "] " + message + Environment.NewLine);
            logTextBox.ScrollToEnd();
        }

        private Border NewCard(string title, double height)
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var titleBlock = new TextBlock { Text = title, FontWeight = FontWeights.SemiBold, FontSize = 14, Margin = new Thickness(0, 0, 0, 10), Foreground = BrushFrom(241, 245, 249) };
            Grid.SetRow(titleBlock, 0);
            grid.Children.Add(titleBlock);
            return new Border { Height = height, Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(14), Background = BrushFrom(17, 24, 39), BorderBrush = BrushFrom(30, 41, 59), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Child = grid };
        }

        private static void SetCardContent(Border card, UIElement content)
        {
            var grid = card.Child as Grid;
            Grid.SetRow(content, 1);
            grid.Children.Add(content);
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
            for (var i = 0; i < rows + 2; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
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

        private static void AddLabeledPathRow(Grid grid, int row, string label, TextBox textBox, string buttonText, Action click)
        {
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = BrushFrom(203, 213, 225) };
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 0);
            grid.Children.Add(text);
            var inner = new Grid();
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            inner.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(86) });
            textBox.Margin = new Thickness(0, 2, 8, 2);
            Grid.SetColumn(textBox, 0);
            inner.Children.Add(textBox);
            inner.Children.Add(NewButton(buttonText, click, 76, 0, 1));
            Grid.SetRow(inner, row);
            Grid.SetColumn(inner, 1);
            grid.Children.Add(inner);
        }

        private static void AddLabeledControl(Grid grid, int row, string label, Control control)
        {
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = BrushFrom(203, 213, 225) };
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 0);
            grid.Children.Add(text);
            control.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetRow(control, row);
            Grid.SetColumn(control, 1);
            grid.Children.Add(control);
        }

        private static void AddLabeledElement(Grid grid, int row, string label, UIElement element)
        {
            var text = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Foreground = BrushFrom(203, 213, 225) };
            Grid.SetRow(text, row);
            Grid.SetColumn(text, 0);
            grid.Children.Add(text);
            Grid.SetRow(element, row);
            Grid.SetColumn(element, 1);
            grid.Children.Add(element);
        }

        private static Grid BuildSliderNumberControl(Slider slider, DecimalBox box)
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });

            slider.Margin = new Thickness(0, 0, 0, 0);
            slider.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(slider, 0);
            grid.Children.Add(slider);

            box.Width = 78;
            box.Margin = new Thickness(0, 2, 0, 2);
            Grid.SetColumn(box, 2);
            grid.Children.Add(box);

            return grid;
        }

        private Grid BuildPresetLoadControl()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(76) });
            presetComboBox.Margin = new Thickness(0, 2, 0, 2);
            presetComboBox.Background = Brushes.White;
            presetComboBox.Foreground = Brushes.Black;
            Grid.SetColumn(presetComboBox, 0);
            grid.Children.Add(presetComboBox);
            loadPresetButton.Height = 30;
            Grid.SetColumn(loadPresetButton, 2);
            grid.Children.Add(loadPresetButton);
            return grid;
        }

        private Grid BuildPresetEditButtons()
        {
            var grid = new Grid { Margin = new Thickness(118, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(92) });
            savePresetButton.Height = 30;
            updatePresetButton.Height = 30;
            deletePresetButton.Height = 30;
            Grid.SetColumn(savePresetButton, 0);
            Grid.SetColumn(updatePresetButton, 2);
            Grid.SetColumn(deletePresetButton, 4);
            grid.Children.Add(savePresetButton);
            grid.Children.Add(updatePresetButton);
            grid.Children.Add(deletePresetButton);
            return grid;
        }

        private static void AddFullRow(Grid grid, int row, UIElement element)
        {
            if (row >= grid.RowDefinitions.Count) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(32) });
            var frameworkElement = element as FrameworkElement;
            if (frameworkElement != null) frameworkElement.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetRow(element, row);
            Grid.SetColumn(element, 0);
            Grid.SetColumnSpan(element, Math.Max(1, grid.ColumnDefinitions.Count));
            grid.Children.Add(element);
        }

        private static Button NewButton(string text, Action click, double width, int row, int column)
        {
            var button = new Button { Content = text, Width = width, Height = 28 };
            button.Click += delegate { click(); };
            Grid.SetRow(button, row);
            Grid.SetColumn(button, column);
            return button;
        }

        private static Slider NewParameterSlider(double value)
        {
            return new Slider
            {
                Minimum = 0,
                Maximum = 255,
                Value = value,
                SmallChange = 1,
                LargeChange = 10,
                TickFrequency = 1,
                IsSnapToTickEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        private static GridSplitter NewGridSplitter(Orientation orientation)
        {
            var splitter = new GridSplitter
            {
                Background = BrushFrom(71, 85, 105),
                ShowsPreview = true,
                ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                ResizeDirection = orientation == Orientation.Vertical ? GridResizeDirection.Columns : GridResizeDirection.Rows
            };

            if (orientation == Orientation.Vertical)
            {
                splitter.Width = 6;
                splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                splitter.VerticalAlignment = VerticalAlignment.Stretch;
            }
            else
            {
                splitter.Height = 6;
                splitter.HorizontalAlignment = HorizontalAlignment.Stretch;
                splitter.VerticalAlignment = VerticalAlignment.Stretch;
            }

            return splitter;
        }

        private static SolidColorBrush BrushFrom(byte red, byte green, byte blue) { var brush = new SolidColorBrush(Color.FromRgb(red, green, blue)); brush.Freeze(); return brush; }
        private static Style StyleForTextBlock() { var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.ForegroundProperty, BrushFrom(226, 232, 240))); return style; }
        private static Style StyleForTextBox() { var style = new Style(typeof(TextBox)); style.Setters.Add(new Setter(Control.BackgroundProperty, BrushFrom(15, 23, 42))); style.Setters.Add(new Setter(Control.ForegroundProperty, BrushFrom(226, 232, 240))); style.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFrom(71, 85, 105))); style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(6, 3, 6, 3))); return style; }
        private static Style StyleForButton() { var style = new Style(typeof(Button)); style.Setters.Add(new Setter(Control.BackgroundProperty, BrushFrom(37, 99, 235))); style.Setters.Add(new Setter(Control.ForegroundProperty, Brushes.White)); style.Setters.Add(new Setter(Control.BorderBrushProperty, BrushFrom(29, 78, 216))); style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(9, 4, 9, 4))); style.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold)); style.Setters.Add(new Setter(Control.OpacityProperty, 1.0)); return style; }
        private static Style StyleForCheckBox() { var style = new Style(typeof(CheckBox)); style.Setters.Add(new Setter(Control.ForegroundProperty, BrushFrom(226, 232, 240))); return style; }
        private static Style StyleForProgressBar() { var style = new Style(typeof(ProgressBar)); style.Setters.Add(new Setter(Control.ForegroundProperty, BrushFrom(34, 197, 94))); style.Setters.Add(new Setter(Control.BackgroundProperty, BrushFrom(30, 41, 59))); return style; }
    }

    public sealed class DecimalBox : TextBox
    {
        public DecimalBox(string value) { Text = value; Width = 100; Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)); Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)); BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105)); Padding = new Thickness(6, 3, 6, 3); }
        public double GetDouble(double fallback) { double value; return Double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ? value : fallback; }
        public int GetInt(int fallback) { int value; return Int32.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback; }
    }

    public sealed class BackgroundPreset
    {
        public BackgroundPreset(string name, int tolerance, int softness, int colorDespill, int edgeCleanup)
        {
            Name = name;
            Tolerance = Clamp(tolerance);
            Softness = Clamp(softness);
            ColorDespill = Clamp(colorDespill);
            EdgeCleanup = Clamp(edgeCleanup);
        }

        public string Name { get; private set; }
        public int Tolerance { get; set; }
        public int Softness { get; set; }
        public int ColorDespill { get; set; }
        public int EdgeCleanup { get; set; }

        public string Serialize()
        {
            return Escape(Name) + "\t" + Clamp(Tolerance).ToString(CultureInfo.InvariantCulture) + "\t" + Clamp(Softness).ToString(CultureInfo.InvariantCulture) + "\t" + Clamp(ColorDespill).ToString(CultureInfo.InvariantCulture) + "\t" + Clamp(EdgeCleanup).ToString(CultureInfo.InvariantCulture);
        }

        public static BackgroundPreset TryParse(string line)
        {
            if (String.IsNullOrWhiteSpace(line)) return null;
            var parts = line.Split('\t');
            if (parts.Length != 5) return null;
            int tolerance, softness, colorDespill, edgeCleanup;
            if (!Int32.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out tolerance)) return null;
            if (!Int32.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out softness)) return null;
            if (!Int32.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out colorDespill)) return null;
            if (!Int32.TryParse(parts[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out edgeCleanup)) return null;
            return new BackgroundPreset(Unescape(parts[0]), tolerance, softness, colorDespill, edgeCleanup);
        }

        private static int Clamp(int value) { return Math.Max(0, Math.Min(255, value)); }
        private static string Escape(string value) { return (value ?? String.Empty).Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", String.Empty).Replace("\n", String.Empty); }
        private static string Unescape(string value) { return (value ?? String.Empty).Replace("\\t", "\t").Replace("\\\\", "\\"); }
    }

    public sealed class PromptDialog : Window
    {
        private readonly TextBox inputBox = new TextBox();
        private string result;

        private PromptDialog(string title, string label, string defaultValue)
        {
            Title = title;
            Width = 360;
            Height = 150;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(17, 24, 39));
            var grid = new Grid { Margin = new Thickness(14) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(42) });

            var labelBlock = new TextBlock { Text = label, Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(labelBlock, 0);
            grid.Children.Add(labelBlock);

            inputBox.Text = defaultValue ?? String.Empty;
            inputBox.Background = new SolidColorBrush(Color.FromRgb(15, 23, 42));
            inputBox.Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240));
            inputBox.BorderBrush = new SolidColorBrush(Color.FromRgb(71, 85, 105));
            Grid.SetRow(inputBox, 1);
            grid.Children.Add(inputBox);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
            var ok = new Button { Content = "OK", Width = 76, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
            var cancel = new Button { Content = "Cancel", Width = 76, Height = 28 };
            ok.Click += delegate { result = inputBox.Text; DialogResult = true; };
            cancel.Click += delegate { DialogResult = false; };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
        }

        public static string Ask(Window owner, string title, string label, string defaultValue)
        {
            var dialog = new PromptDialog(title, label, defaultValue) { Owner = owner };
            return dialog.ShowDialog() == true ? dialog.result : null;
        }
    }

    public sealed class UndoBatch
    {
        public UndoBatch(string folder, string restoreFolder, string selectPath) { Folder = folder; RestoreFolder = restoreFolder; SelectPath = selectPath; Items = new List<UndoItem>(); }
        public string Folder { get; private set; }
        public string RestoreFolder { get; private set; }
        public string SelectPath { get; private set; }
        public List<UndoItem> Items { get; private set; }
    }

    public sealed class UndoItem
    {
        public UndoItem(string path, bool existed, byte[] previousBytes) { Path = path; Existed = existed; PreviousBytes = previousBytes; }
        public string Path { get; private set; }
        public bool Existed { get; private set; }
        public byte[] PreviousBytes { get; private set; }
    }
}
