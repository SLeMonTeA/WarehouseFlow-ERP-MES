using System.IO;

namespace WarehouseFlow.WinForms;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, args) => HandleException(args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception) HandleException(exception);
        };

        try
        {
            WriteLifecycle("before Form1 construction");
            var form = new Form1();
            WriteLifecycle("Form1 constructed");
            form.Shown += (_, _) =>
            {
                WriteLifecycle($"Form1 shown: handle={form.Handle}, visible={form.Visible}, bounds={form.Bounds}");
                form.WindowState = FormWindowState.Normal;
                form.ShowInTaskbar = true;
                var workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
                form.Location = new Point(
                    workArea.X + Math.Max(0, (workArea.Width - form.Width) / 2),
                    workArea.Y + Math.Max(0, (workArea.Height - form.Height) / 2));
                WriteLifecycle($"Form1 positioned on {workArea}: bounds={form.Bounds}");
                form.Activate();
                form.BringToFront();
                // Toggle TopMost once so a window launched from a background host is surfaced.
                form.TopMost = true;
                form.TopMost = false;
            };
            WriteLifecycle("before Application.Run");
            Application.Run(form);
            WriteLifecycle("Application.Run returned");
        }
        catch (Exception exception)
        {
            HandleException(exception);
        }
    }    

    private static void HandleException(Exception exception)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "client-startup.log");
        try
        {
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] {exception}\r\n");
            MessageBox.Show($"客户端启动失败，详细日志：{logPath}\r\n\r\n{exception.Message}", "WarehouseFlow 启动错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch
        {
            // Do not throw again while reporting an unhandled UI exception.
        }
    }

    private static void WriteLifecycle(string message)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "client-startup.log");
            File.AppendAllText(logPath, $"[{DateTimeOffset.Now:O}] {message}\r\n");
        }
        catch
        {
            // Diagnostics must never prevent the client from starting.
        }
    }
}
