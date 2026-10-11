using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace Snapline.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var smoke = args.Contains("--smoke-test");
        // E2E uses an explicit temporary directory and synthetic capture; never a user's files or desktop.
        var e2e = args.Length == 2 && args[0] == "--e2e-data-dir";
        var motionTest = args.Length == 2 && args[0] == "--verify-motion";
        using var mutex = new Mutex(true, motionTest ? @"Local\Snapline.MotionVerification" : @"Local\Snapline.Windows", out var first);
        if (!first)
        {
            if (smoke || e2e || motionTest) return 2;
            for (var attempt = 0; attempt < 20; attempt++)
            {
                if (Native.ShowExistingInstance()) return 0;
                Thread.Sleep(100);
            }
            return 1;
        }
        var dataDirectory = motionTest ? Path.Combine(Path.GetFullPath(args[1]), "verification-errors") : smoke
            ? Path.Combine(Path.GetTempPath(), "Snapline-smoke-" + Guid.NewGuid().ToString("N"))
            : e2e ? Path.GetFullPath(args[1])
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Snapline");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Directory.CreateDirectory(dataDirectory);
            // Only exception type and stack: no clipboard payloads or image paths in logs.
            File.AppendAllText(Path.Combine(dataDirectory, "error.log"), $"{DateTime.UtcNow:O} {e.Exception.GetType().Name}\n{e.Exception.StackTrace}\n");
            e.Handled = true;
            if (!smoke && !e2e && !motionTest) MessageBox.Show("操作未完成，请重试。错误日志在 %LOCALAPPDATA%\\Snapline\\error.log。", "Snapline");
        };
        SnaplineApplication? controller = null;
        var motionPassed = false;
        app.Startup += async (_, _) =>
        {
            if (motionTest)
            {
                motionPassed = await MotionVerification.RunAsync(Path.GetFullPath(args[1]));
                app.Shutdown();
                return;
            }
            controller = new SnaplineApplication(app, dataDirectory, smoke, e2e);
            controller.Start();
            if (smoke)
            {
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (_, _) => { timer.Stop(); app.Shutdown(); };
                timer.Start();
            }
        };
        app.Exit += (_, _) => controller?.Dispose();
        app.Run();
        if (motionTest) return motionPassed && !File.Exists(Path.Combine(dataDirectory, "error.log")) ? 0 : 1;
        if (smoke)
        {
            var failed = File.Exists(Path.Combine(dataDirectory, "error.log"));
            Directory.Delete(dataDirectory, recursive: true);
            return failed ? 1 : 0;
        }
        return 0;
    }
}
