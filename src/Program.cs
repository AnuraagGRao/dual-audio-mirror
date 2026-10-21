using System;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DualAudioMirror {
    public static class Program {
        // Keep static references so the GC NEVER collects the window or engine during app.Run()
        private static Application _app;
        private static AudioEngine _engine;
        private static MainWindow _mainWindow;

        [STAThread]
        public static void Main(string[] args) {
            Forms.Application.EnableVisualStyles();
            Forms.Application.SetCompatibleTextRenderingDefault(false);

            _app = new Application();
            _app.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            _engine = new AudioEngine();
            _engine.Start();

            _mainWindow = new MainWindow(_engine, _app);
            _app.MainWindow = _mainWindow.Window;
            _mainWindow.Window.Show();

            _app.Run();

            _engine.Stop();
            _engine.Dispose();
        }
    }
}
