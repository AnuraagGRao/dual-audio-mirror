using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;

namespace DualAudioMirror {
    public static class Program {
        private const string SingleInstanceMutexName = @"Local\DualAudioMirror_WPF_SingleInstance_f8b1a3e9";
        private static Mutex _singleInstanceMutex;

        // Keep static references so the GC NEVER collects the window or engine during app.Run()
        private static Application _app;
        private static AudioEngine _engine;
        private static MainWindow _mainWindow;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int SW_RESTORE = 9;
        private static readonly IntPtr HWND_BROADCAST = new IntPtr(0xffff);

        private static void Log(string s) {
            try {
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), DateTime.Now.ToString("HH:mm:ss.fff") + " - " + s + Environment.NewLine);
            } catch { }
        }

        [STAThread]
        public static void Main(string[] args) {
            bool createdNew = false;
            try {
                _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out createdNew);
            } catch (Exception ex) {
                Log("Mutex creation note: " + ex.Message);
            }

            if (!createdNew) {
                Log("Another instance is already running. Signaling active instance to restore and focusing window.");
                try {
                    uint msg = RegisterWindowMessage("DUAL_AUDIO_MIRROR_WAKEUP");
                    if (msg != 0) {
                        PostMessage(HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
                    }
                    IntPtr hWnd = FindWindow(null, "Dual Audio Mirror");
                    if (hWnd != IntPtr.Zero) {
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                    }
                } catch { }
                return;
            }

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
            } finally {
                if (_singleInstanceMutex != null) {
                    try {
                        _singleInstanceMutex.ReleaseMutex();
                        _singleInstanceMutex.Dispose();
                    } catch { }
                }
            }
        }
    }
}
