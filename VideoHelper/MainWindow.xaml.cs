// Copyright (c) 2026 KingPrivet. All rights reserved.
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Windowing;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace VideoHelper;

public sealed partial class MainWindow : Window
{
    private static readonly Regex NumberRegex = new(@"(\d+)(?!.*\d)", RegexOptions.Compiled);
    private string? _lastOutputPath;
    public ObservableCollection<VideoPart> Parts { get; } = [];

    public MainWindow()
    {
        InitializeComponent();
        PartsList.ItemsSource = Parts;
        Title = "Video Helper";
        AppWindow.Title = "Video Helper";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(980, 760));
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBarDragRegion);

        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "VideoHelper.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }

        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            AppWindow.TitleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            AppWindow.TitleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            AppWindow.TitleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(36, 255, 255, 255);
            AppWindow.TitleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(24, 255, 255, 255);
            AppWindow.TitleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
            AppWindow.TitleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(140, 255, 255, 255);
        }
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".mp4");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
        {
            AddFiles(files.Select(file => file.Path));
        }
    }

    private void DropArea_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Добавить части видео";
        e.DragUIOverride.IsContentVisible = true;
    }

    private async void DropArea_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            return;
        }

        var items = await e.DataView.GetStorageItemsAsync();
        AddFiles(items.OfType<StorageFile>().Select(file => file.Path));
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        var existing = Parts.Select(part => part.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths.Where(IsMp4).Where(File.Exists))
        {
            if (!existing.Add(path))
            {
                continue;
            }

            var file = new FileInfo(path);
            Parts.Add(new VideoPart
            {
                Path = file.FullName,
                Name = file.Name,
                Folder = file.DirectoryName ?? string.Empty,
                Size = file.Length,
            });
        }

        SortAndRefresh();
    }

    private void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        foreach (var item in PartsList.SelectedItems.Cast<VideoPart>().ToArray())
        {
            Parts.Remove(item);
        }
        SortAndRefresh();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Parts.Clear();
        UpdateState();
    }

    private async void Join_Click(object sender, RoutedEventArgs e)
    {
        if (Parts.Count < 2)
        {
            ShowStatus("Добавьте минимум две части видео.", InfoBarSeverity.Warning);
            return;
        }

        var ffmpegPath = FindFfmpeg() ?? await PickFfmpegAsync();
        if (ffmpegPath is null)
        {
            ShowStatus("FFmpeg не выбран. Укажите ffmpeg.exe, положите его рядом с программой или добавьте в PATH.", InfoBarSeverity.Error);
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = GetSuggestedName(),
            DefaultFileExtension = ".mp4",
        };
        picker.FileTypeChoices.Add("Видео MP4", [".mp4"]);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var output = await picker.PickSaveFileAsync();
        if (output is null)
        {
            return;
        }
        if (Parts.Any(part => string.Equals(part.Path, output.Path, StringComparison.OrdinalIgnoreCase)))
        {
            ShowStatus("Итоговый файл не может совпадать с одной из частей.", InfoBarSeverity.Error);
            return;
        }

        SetBusy(true);
        ShowInFolderButton.Visibility = Visibility.Collapsed;
        try
        {
            var result = await JoinVideosAsync(ffmpegPath, output.Path);
            if (result.ExitCode == 0 && File.Exists(output.Path))
            {
                _lastOutputPath = output.Path;
                ShowStatus($"Готово: {output.Path}", InfoBarSeverity.Success);
                ShowInFolderButton.Visibility = Visibility.Visible;
            }
            else
            {
                ShowStatus($"FFmpeg не смог склеить видео. {LastLine(result.Error)}", InfoBarSeverity.Error);
            }
        }
        catch (Exception ex)
        {
            ShowStatus($"Ошибка: {ex.Message}", InfoBarSeverity.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowInFolder_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_lastOutputPath) || !File.Exists(_lastOutputPath))
        {
            ShowInFolderButton.Visibility = Visibility.Collapsed;
            ShowStatus("Готовый файл больше не найден.", InfoBarSeverity.Warning);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "explorer.exe",
            UseShellExecute = true,
        };
        startInfo.ArgumentList.Add($"/select,{_lastOutputPath}");
        Process.Start(startInfo);
    }

    private async Task<(int ExitCode, string Error)> JoinVideosAsync(string ffmpegPath, string outputPath)
    {
        var listPath = Path.Combine(Path.GetTempPath(), $"video-helper-{Guid.NewGuid():N}.txt");
        try
        {
            var lines = Parts.Select(part => $"file '{EscapeForConcat(part.Path)}'");
            await File.WriteAllLinesAsync(listPath, lines, new UTF8Encoding(false));

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("-y");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("concat");
            startInfo.ArgumentList.Add("-safe");
            startInfo.ArgumentList.Add("0");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(listPath);
            startInfo.ArgumentList.Add("-c");
            startInfo.ArgumentList.Add("copy");
            startInfo.ArgumentList.Add("-movflags");
            startInfo.ArgumentList.Add("+faststart");
            startInfo.ArgumentList.Add(outputPath);

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Не удалось запустить FFmpeg.");
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return (process.ExitCode, error);
        }
        finally
        {
            File.Delete(listPath);
        }
    }

    private void SortAndRefresh()
    {
        var sorted = Parts.OrderBy(part => NaturalKey(part.Name)).ThenBy(part => part.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        Parts.Clear();
        for (var index = 0; index < sorted.Length; index++)
        {
            sorted[index].Number = index + 1;
            Parts.Add(sorted[index]);
        }
        UpdateState();
    }

    private void UpdateState()
    {
        EmptyState.Visibility = Parts.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        JoinButton.IsEnabled = Parts.Count >= 2;
        var total = Parts.Sum(part => part.Size);
        FileCountText.Text = FormatPartCount(Parts.Count);
        TotalSizeText.Text = Parts.Count == 0 ? "0 МБ" : FormatSize(total);
    }

    private void SetBusy(bool busy)
    {
        JoinProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        JoinButton.IsEnabled = !busy && Parts.Count >= 2;
        PartsList.IsEnabled = !busy;
    }

    private void ShowStatus(string message, InfoBarSeverity severity)
    {
        StatusBar.Message = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
    }

    private string GetSuggestedName()
    {
        var name = Path.GetFileNameWithoutExtension(Parts[0].Name);
        name = Regex.Replace(name, @"([_. -]?(part|часть)?[_. -]*\d+)$", string.Empty, RegexOptions.IgnoreCase);
        return string.IsNullOrWhiteSpace(name) ? "joined-video" : $"{name}-joined";
    }

    private static string? FindFfmpeg()
    {
        var adjacent = Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe");
        if (File.Exists(adjacent))
        {
            return adjacent;
        }

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        return pathValue.Split(Path.PathSeparator)
            .Select(directory => Path.Combine(directory.Trim(), "ffmpeg.exe"))
            .FirstOrDefault(File.Exists);
    }

    private async Task<string?> PickFfmpegAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add(".exe");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private static bool IsMp4(string path) => string.Equals(Path.GetExtension(path), ".mp4", StringComparison.OrdinalIgnoreCase);

    private static int NaturalKey(string name)
    {
        var match = NumberRegex.Match(Path.GetFileNameWithoutExtension(name));
        return match.Success && int.TryParse(match.Value, out var number) ? number : int.MaxValue;
    }

    private static string EscapeForConcat(string path) => path.Replace('\\', '/').Replace("'", "'\\''");

    private static string LastLine(string value) => value.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "";

    private static string FormatSize(long bytes)
    {
        var value = bytes / 1024d / 1024d;
        return value >= 1024 ? $"{value / 1024:0.##} ГБ" : $"{value:0.#} МБ";
    }

    private static string FormatPartCount(int count)
    {
        var ending = count % 10 == 1 && count % 100 != 11
            ? "часть"
            : count % 10 is >= 2 and <= 4 && count % 100 is not (>= 12 and <= 14)
                ? "части"
                : "частей";
        return $"{count} {ending}";
    }
}
