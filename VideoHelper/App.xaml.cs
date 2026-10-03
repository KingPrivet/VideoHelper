// Copyright (c) 2026 KingPrivet. All rights reserved.
using Microsoft.UI.Xaml;
using System.Text;

namespace VideoHelper;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        UnhandledException += (_, args) => WriteCrashLog(args.Exception);
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
            throw;
        }
    }

    private static void WriteCrashLog(Exception exception)
    {
        var path = Path.Combine(Path.GetTempPath(), "VideoHelper-crash.log");
        var text = new StringBuilder()
            .AppendLine(DateTime.Now.ToString("O"))
            .AppendLine(exception.ToString())
            .AppendLine()
            .ToString();
        File.AppendAllText(path, text);
    }
}
