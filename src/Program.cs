using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DualAudioMirror {
    public static class Program {
        // Keep static references so the GC NEVER collects the window or engine during app.Run()
        private static Application _app;
        private static AudioEngine _engine;
        private static MainWindow _mainWindow;

        private static void Log(string s) {
            try {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " - " + s + Environment.NewLine);
            } catch { }
        }

        [STAThread]
        public static void Main(string[] args) {
            try {
                AppDomain.CurrentDomain.UnhandledException += (s, e) => {
                    Log("CRITICAL UNHANDLED: " + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "null"));
                };

                Log("Starting application");
                Forms.Application.EnableVisualStyles();
                Forms.Application.SetCompatibleTextRenderingDefault(false);

                _app = new Application();
                _app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                _app.DispatcherUnhandledException += (s, e) => {
                    Log("CRITICAL DISPATCHER: " + (e.Exception != null ? e.Exception.ToString() : "null"));
                    e.Handled = true;
                };
                Log("Application initialized");

                _engine = new AudioEngine();
                _engine.Start();
                Log("AudioEngine started");

                _mainWindow = new MainWindow(_engine, _app);
                Log("MainWindow constructed");

                _app.MainWindow = _mainWindow.Window;
                _mainWindow.Window.Show();
                Log("MainWindow.Show() called, HWND: " + new System.Windows.Interop.WindowInteropHelper(_mainWindow.Window).Handle);

                _app.Run();
                Log("app.Run() returned");

                _engine.Stop();
                _engine.Dispose();
            } catch (Exception ex) {
                try {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.txt"), ex.ToString());
                } catch { }
                Forms.MessageBox.Show(ex.ToString(), "Dual Audio Mirror Error", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
            }
        }
    }
}
