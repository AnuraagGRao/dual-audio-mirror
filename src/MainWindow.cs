using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace DualAudioMirror {
    public class MainWindow {
        private Window _window;
        private AudioEngine _engine;
        private Application _app;
        private DispatcherTimer _meterTimer;
        private Forms.NotifyIcon _notifyIcon;

        // UI Controls
        private ComboBox _cbSource;
        private ComboBox _cbOut1;
        private ComboBox _cbOut2;
        private Slider _sliderVol1;
        private Slider _sliderVol2;
        private TextBlock _txtVol1;
        private TextBlock _txtVol2;
        private Button _btnMute1;
        private Button _btnMute2;
        private TextBlock _txtStatus;
        private CheckBox _chkAutoStart;

        // Visualizer Meters
        private Rectangle _meterMasterL;
        private Rectangle _meterMasterR;
        private Rectangle _meterOut1L;
        private Rectangle _meterOut1R;
        private Rectangle _meterOut2L;
        private Rectangle _meterOut2R;

        private const string AppName = "DualAudioMirror";

        public Window Window {
            get { return _window; }
        }

        public MainWindow(AudioEngine engine, Application app) {
            _engine = engine;
            _app = app;
            BuildUI();
            SetupNotifyIcon();
            SetupTimer();
        }

        private void BuildUI() {
            string xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='Dual Audio Mirror' Width='520' Height='680'
        WindowStartupLocation='CenterScreen' ResizeMode='CanMinimize'
        Background='#0c1017' Foreground='#e2e8f0'
        FontFamily='Segoe UI, -apple-system, BlinkMacSystemFont'>
    <Window.Resources>
        <Style TargetType='Slider'>
            <Setter Property='Margin' Value='0,4,0,4'/>
        </Style>
    </Window.Resources>

    <Grid Margin='20'>
        <Grid.RowDefinitions>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='Auto'/>
            <RowDefinition Height='*'/>
            <RowDefinition Height='Auto'/>
        </Grid.RowDefinitions>

        <!-- Header -->
        <Border Grid.Row='0' Background='#131b26' CornerRadius='12' Padding='16,12' Margin='0,0,0,16'
                BorderBrush='#1e293b' BorderThickness='1'>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width='*'/>
                    <ColumnDefinition Width='Auto'/>
                </Grid.ColumnDefinitions>
                <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                    <Border Background='#00f2fe' Width='8' Height='24' CornerRadius='4' Margin='0,0,12,0'>
                        <Border.Effect>
                            <DropShadowEffect Color='#00f2fe' BlurRadius='12' ShadowDepth='0'/>
                        </Border.Effect>
                    </Border>
                    <StackPanel>
                        <TextBlock Text='DUAL AUDIO MIRROR' FontWeight='Bold' FontSize='16' Foreground='#ffffff'/>
                        <TextBlock Name='TxtStatus' Text='INITIALIZING...' FontSize='11' Foreground='#10b981' Margin='0,2,0,0'/>
                    </StackPanel>
                </StackPanel>

                <Button Name='BtnTray' Grid.Column='1' Content='Send to Tray'
                        Background='#1e293b' Foreground='#94a3b8' BorderThickness='0'
                        Padding='12,6' FontSize='11' FontWeight='SemiBold' Cursor='Hand'>
                    <Button.Template>
                        <ControlTemplate TargetType='Button'>
                            <Border Background='{TemplateBinding Background}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
                                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                            </Border>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </Grid>
        </Border>

        <!-- Master Source Card -->
        <Border Grid.Row='1' Background='#131b26' CornerRadius='12' Padding='16' Margin='0,0,0,14'
                BorderBrush='#1e293b' BorderThickness='1'>
            <StackPanel>
                <Grid Margin='0,0,0,10'>
                    <TextBlock Text='MASTER INPUT (SYSTEM / VAIO)' FontWeight='Bold' FontSize='11' Foreground='#64748b'/>
                    <TextBlock Text='48 kHz • Stereo' HorizontalAlignment='Right' FontSize='11' Foreground='#00f2fe'/>
                </Grid>
                <ComboBox Name='CbSource' Background='#0c1017' Foreground='#e2e8f0' Padding='8,6' Margin='0,0,0,12'/>

                <TextBlock Text='INPUT LEVEL' FontSize='10' FontWeight='SemiBold' Foreground='#475569' Margin='0,0,0,4'/>
                <Grid Height='6' Margin='0,0,0,3'>
                    <Border Background='#0c1017' CornerRadius='3'/>
                    <Rectangle Name='MeterMasterL' HorizontalAlignment='Left' Width='0' Fill='#00f2fe' RadiusX='3' RadiusY='3'/>
                </Grid>
                <Grid Height='6'>
                    <Border Background='#0c1017' CornerRadius='3'/>
                    <Rectangle Name='MeterMasterR' HorizontalAlignment='Left' Width='0' Fill='#00f2fe' RadiusX='3' RadiusY='3'/>
                </Grid>
            </StackPanel>
        </Border>

        <!-- Headphone Channels Container -->
        <ScrollViewer Grid.Row='2' VerticalScrollBarVisibility='Auto'>
            <StackPanel>
                <!-- Channel 1: PRO X -->
                <Border Background='#131b26' CornerRadius='12' Padding='16' Margin='0,0,0,12'
                        BorderBrush='#1e293b' BorderThickness='1'>
                    <StackPanel>
                        <Grid Margin='0,0,0,10'>
                            <StackPanel Orientation='Horizontal'>
                                <TextBlock Text='OUTPUT 1: ' FontWeight='Bold' FontSize='12' Foreground='#64748b'/>
                                <TextBlock Text='HEADPHONES (PRO X)' FontWeight='Bold' FontSize='12' Foreground='#ffffff'/>
                            </StackPanel>
                            <Border HorizontalAlignment='Right' Background='#064e3b' CornerRadius='4' Padding='6,2'>
                                <TextBlock Text='CONNECTED' FontSize='9' FontWeight='Bold' Foreground='#10b981'/>
                            </Border>
                        </Grid>

                        <ComboBox Name='CbOut1' Background='#0c1017' Foreground='#e2e8f0' Padding='8,6' Margin='0,0,0,12'/>

                        <Grid Margin='0,0,0,4'>
                            <TextBlock Text='Volume' FontSize='11' Foreground='#94a3b8'/>
                            <TextBlock Name='TxtVol1' Text='100%' HorizontalAlignment='Right' FontSize='11' FontWeight='Bold' Foreground='#00f2fe'/>
                        </Grid>
                        <Slider Name='SliderVol1' Minimum='0' Maximum='125' Value='100' Margin='0,0,0,12'/>

                        <Grid Margin='0,0,0,8'>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width='*'/>
                                <ColumnDefinition Width='Auto'/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column='0' VerticalAlignment='Center' Margin='0,0,12,0'>
                                <Grid Height='6' Margin='0,0,0,3'>
                                    <Border Background='#0c1017' CornerRadius='3'/>
                                    <Rectangle Name='MeterOut1L' HorizontalAlignment='Left' Width='0' Fill='#10b981' RadiusX='3' RadiusY='3'/>
                                </Grid>
                                <Grid Height='6'>
                                    <Border Background='#0c1017' CornerRadius='3'/>
                                    <Rectangle Name='MeterOut1R' HorizontalAlignment='Left' Width='0' Fill='#10b981' RadiusX='3' RadiusY='3'/>
                                </Grid>
                            </StackPanel>

                            <Button Name='BtnMute1' Grid.Column='1' Content='Mute' Width='64' Height='28'
                                    Background='#1e293b' Foreground='#e2e8f0' BorderThickness='0'
                                    FontSize='11' FontWeight='SemiBold' Cursor='Hand'>
                                <Button.Template>
                                    <ControlTemplate TargetType='Button'>
                                        <Border Name='bdr' Background='{TemplateBinding Background}' CornerRadius='6'>
                                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                                        </Border>
                                    </ControlTemplate>
                                </Button.Template>
                            </Button>
                        </Grid>
                    </StackPanel>
                </Border>

                <!-- Channel 2: G435 -->
                <Border Background='#131b26' CornerRadius='12' Padding='16' Margin='0,0,0,12'
                        BorderBrush='#1e293b' BorderThickness='1'>
                    <StackPanel>
                        <Grid Margin='0,0,0,10'>
                            <StackPanel Orientation='Horizontal'>
                                <TextBlock Text='OUTPUT 2: ' FontWeight='Bold' FontSize='12' Foreground='#64748b'/>
                                <TextBlock Text='HEADPHONES (G435 WIRELESS)' FontWeight='Bold' FontSize='12' Foreground='#ffffff'/>
                            </StackPanel>
                            <Border HorizontalAlignment='Right' Background='#064e3b' CornerRadius='4' Padding='6,2'>
                                <TextBlock Text='CONNECTED' FontSize='9' FontWeight='Bold' Foreground='#10b981'/>
                            </Border>
                        </Grid>

                        <ComboBox Name='CbOut2' Background='#0c1017' Foreground='#e2e8f0' Padding='8,6' Margin='0,0,0,12'/>

                        <Grid Margin='0,0,0,4'>
                            <TextBlock Text='Volume' FontSize='11' Foreground='#94a3b8'/>
                            <TextBlock Name='TxtVol2' Text='100%' HorizontalAlignment='Right' FontSize='11' FontWeight='Bold' Foreground='#00f2fe'/>
                        </Grid>
                        <Slider Name='SliderVol2' Minimum='0' Maximum='125' Value='100' Margin='0,0,0,12'/>

                        <Grid Margin='0,0,0,8'>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width='*'/>
                                <ColumnDefinition Width='Auto'/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column='0' VerticalAlignment='Center' Margin='0,0,12,0'>
                                <Grid Height='6' Margin='0,0,0,3'>
                                    <Border Background='#0c1017' CornerRadius='3'/>
                                    <Rectangle Name='MeterOut2L' HorizontalAlignment='Left' Width='0' Fill='#10b981' RadiusX='3' RadiusY='3'/>
                                </Grid>
                                <Grid Height='6'>
                                    <Border Background='#0c1017' CornerRadius='3'/>
                                    <Rectangle Name='MeterOut2R' HorizontalAlignment='Left' Width='0' Fill='#10b981' RadiusX='3' RadiusY='3'/>
                                </Grid>
                            </StackPanel>

                            <Button Name='BtnMute2' Grid.Column='1' Content='Mute' Width='64' Height='28'
                                    Background='#1e293b' Foreground='#e2e8f0' BorderThickness='0'
                                    FontSize='11' FontWeight='SemiBold' Cursor='Hand'>
                                <Button.Template>
                                    <ControlTemplate TargetType='Button'>
                                        <Border Name='bdr' Background='{TemplateBinding Background}' CornerRadius='6'>
                                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                                        </Border>
                                    </ControlTemplate>
                                </Button.Template>
                            </Button>
                        </Grid>
                    </StackPanel>
                </Border>
            </StackPanel>
        </ScrollViewer>

        <!-- Footer -->
        <Border Grid.Row='3' Background='#131b26' CornerRadius='10' Padding='14,10' Margin='0,10,0,0'
                BorderBrush='#1e293b' BorderThickness='1'>
            <Grid>
                <CheckBox Name='ChkAutoStart' Content='Launch on Windows Startup' Foreground='#94a3b8' FontSize='11' VerticalAlignment='Center'/>
                <TextBlock Text='v1.0.0 Pure WASAPI • Zero Bloat' HorizontalAlignment='Right' VerticalAlignment='Center' FontSize='10' Foreground='#475569'/>
            </Grid>
        </Border>
    </Grid>
</Window>";

            _window = (Window)XamlReader.Parse(xaml);

            // Bind Elements
            _txtStatus = (TextBlock)_window.FindName("TxtStatus");
            _cbSource = (ComboBox)_window.FindName("CbSource");
            _cbOut1 = (ComboBox)_window.FindName("CbOut1");
            _cbOut2 = (ComboBox)_window.FindName("CbOut2");

            _sliderVol1 = (Slider)_window.FindName("SliderVol1");
            _sliderVol2 = (Slider)_window.FindName("SliderVol2");
            _txtVol1 = (TextBlock)_window.FindName("TxtVol1");
            _txtVol2 = (TextBlock)_window.FindName("TxtVol2");
            _btnMute1 = (Button)_window.FindName("BtnMute1");
            _btnMute2 = (Button)_window.FindName("BtnMute2");

            _meterMasterL = (Rectangle)_window.FindName("MeterMasterL");
            _meterMasterR = (Rectangle)_window.FindName("MeterMasterR");
            _meterOut1L = (Rectangle)_window.FindName("MeterOut1L");
            _meterOut1R = (Rectangle)_window.FindName("MeterOut1R");
            _meterOut2L = (Rectangle)_window.FindName("MeterOut2L");
            _meterOut2R = (Rectangle)_window.FindName("MeterOut2R");

            _chkAutoStart = (CheckBox)_window.FindName("ChkAutoStart");
            Button btnTray = (Button)_window.FindName("BtnTray");

            // Populate Devices
            var devices = AudioEngine.GetPlaybackDevices();
            foreach (var d in devices) {
                _cbSource.Items.Add(d);
                _cbOut1.Items.Add(d);
                _cbOut2.Items.Add(d);
            }

            // Auto-select preferred
            var vmInput = devices.Find(d => d.Name.Contains("Voicemeeter Input"));
            if (vmInput != null) _cbSource.SelectedItem = vmInput;
            else if (devices.Count > 0) _cbSource.SelectedIndex = 0;

            var prox = devices.Find(d => d.Name.Contains("PRO X"));
            if (prox != null) _cbOut1.SelectedItem = prox;
            else if (devices.Count > 1) _cbOut1.SelectedIndex = 1;

            var g435 = devices.Find(d => d.Name.Contains("G435"));
            if (g435 != null) _cbOut2.SelectedItem = g435;
            else if (devices.Count > 2) _cbOut2.SelectedIndex = 2;

            // Events
            _cbSource.SelectionChanged += (s, e) => {
                var d = _cbSource.SelectedItem as AudioDeviceInfo;
                if (d != null) {
                    _engine.CaptureDeviceId = d.Id;
                    RestartEngine();
                }
            };

            _cbOut1.SelectionChanged += (s, e) => {
                var d = _cbOut1.SelectedItem as AudioDeviceInfo;
                if (d != null) {
                    _engine.Render1DeviceId = d.Id;
                    RestartEngine();
                }
            };

            _cbOut2.SelectionChanged += (s, e) => {
                var d = _cbOut2.SelectedItem as AudioDeviceInfo;
                if (d != null) {
                    _engine.Render2DeviceId = d.Id;
                    RestartEngine();
                }
            };

            _sliderVol1.ValueChanged += (s, e) => {
                int val = (int)_sliderVol1.Value;
                _txtVol1.Text = val + "%";
                _engine.Volume1 = val / 100f;
            };

            _sliderVol2.ValueChanged += (s, e) => {
                int val = (int)_sliderVol2.Value;
                _txtVol2.Text = val + "%";
                _engine.Volume2 = val / 100f;
            };

            _btnMute1.Click += (s, e) => {
                _engine.IsMuted1 = !_engine.IsMuted1;
                _btnMute1.Content = _engine.IsMuted1 ? "MUTED" : "Mute";
                _btnMute1.Background = _engine.IsMuted1 ? new SolidColorBrush(Color.FromRgb(185, 28, 28)) : new SolidColorBrush(Color.FromRgb(30, 41, 59));
            };

            _btnMute2.Click += (s, e) => {
                _engine.IsMuted2 = !_engine.IsMuted2;
                _btnMute2.Content = _engine.IsMuted2 ? "MUTED" : "Mute";
                _btnMute2.Background = _engine.IsMuted2 ? new SolidColorBrush(Color.FromRgb(185, 28, 28)) : new SolidColorBrush(Color.FromRgb(30, 41, 59));
            };

            btnTray.Click += (s, e) => {
                _window.Hide();
                if (_notifyIcon != null) {
                    _notifyIcon.ShowBalloonTip(1500, "Dual Audio Mirror", "Running in system tray", Forms.ToolTipIcon.Info);
                }
            };

            _window.Closing += (s, e) => {
                e.Cancel = true;
                _window.Hide();
            };

            // Auto-start checkbox
            _chkAutoStart.IsChecked = IsAutoStartEnabled();
            _chkAutoStart.Checked += (s, e) => SetAutoStart(true);
            _chkAutoStart.Unchecked += (s, e) => SetAutoStart(false);

            _engine.OnStatusChanged += status => {
                _window.Dispatcher.Invoke(() => {
                    _txtStatus.Text = status;
                });
            };
        }

        private void SetupNotifyIcon() {
            System.Drawing.Icon trayIcon = null;
            try {
                using (var bmp = new System.Drawing.Bitmap(16, 16)) {
                    using (var g = System.Drawing.Graphics.FromImage(bmp)) {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.Clear(System.Drawing.Color.Transparent);
                        using (var brush = new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(0, 242, 254))) {
                            g.FillEllipse(brush, 1, 1, 14, 14);
                        }
                    }
                    trayIcon = System.Drawing.Icon.FromHandle(bmp.GetHicon());
                }
            } catch {
                trayIcon = System.Drawing.SystemIcons.Application;
            }

            _notifyIcon = new Forms.NotifyIcon {
                Icon = trayIcon,
                Text = "Dual Audio Mirror (PRO X + G435)",
                Visible = true
            };

            var menu = new Forms.ContextMenu();
            menu.MenuItems.Add("Show Dashboard", (s, e) => ShowWindow());
            menu.MenuItems.Add("-");
            menu.MenuItems.Add("Exit", (s, e) => {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _engine.Stop();
                _app.Shutdown();
            });

            _notifyIcon.ContextMenu = menu;
            _notifyIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow() {
            _window.Show();
            _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        private void SetupTimer() {
            _meterTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
            _meterTimer.Tick += (s, e) => {
                try {
                    double maxW = 440.0;
                    float pML = _engine.PeakMasterL;
                    float pMR = _engine.PeakMasterR;
                    if (float.IsNaN(pML) || float.IsInfinity(pML) || pML < 0f) pML = 0f;
                    if (float.IsNaN(pMR) || float.IsInfinity(pMR) || pMR < 0f) pMR = 0f;
                    _meterMasterL.Width = Math.Max(0.0, Math.Min(maxW, (double)pML * maxW));
                    _meterMasterR.Width = Math.Max(0.0, Math.Min(maxW, (double)pMR * maxW));

                    double chW = 340.0;
                    float p1L = _engine.PeakOut1L;
                    float p1R = _engine.PeakOut1R;
                    if (float.IsNaN(p1L) || float.IsInfinity(p1L) || p1L < 0f) p1L = 0f;
                    if (float.IsNaN(p1R) || float.IsInfinity(p1R) || p1R < 0f) p1R = 0f;
                    _meterOut1L.Width = Math.Max(0.0, Math.Min(chW, (double)p1L * chW));
                    _meterOut1R.Width = Math.Max(0.0, Math.Min(chW, (double)p1R * chW));

                    float p2L = _engine.PeakOut2L;
                    float p2R = _engine.PeakOut2R;
                    if (float.IsNaN(p2L) || float.IsInfinity(p2L) || p2L < 0f) p2L = 0f;
                    if (float.IsNaN(p2R) || float.IsInfinity(p2R) || p2R < 0f) p2R = 0f;
                    _meterOut2L.Width = Math.Max(0.0, Math.Min(chW, (double)p2L * chW));
                    _meterOut2R.Width = Math.Max(0.0, Math.Min(chW, (double)p2R * chW));
                } catch { }
            };
            _meterTimer.Start();
        }

        private void RestartEngine() {
            _engine.Stop();
            _engine.Start();
        }

        private bool IsAutoStartEnabled() {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", false)) {
                    return key != null && key.GetValue(AppName) != null;
                }
            } catch { return false; }
        }

        private void SetAutoStart(bool enable) {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                    if (key != null) {
                        if (enable) {
                            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            key.SetValue(AppName, "\"" + exePath + "\"");
                        } else {
                            key.DeleteValue(AppName, false);
                        }
                    }
                }
            } catch (Exception ex) {
                Forms.MessageBox.Show("Failed to update registry: " + ex.Message);
            }
        }
    }
}
