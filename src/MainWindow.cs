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
        Title='Dual Audio Mirror' Width='540' Height='720'
        WindowStartupLocation='CenterScreen' ResizeMode='CanMinimize'
        Background='#0c1017' Foreground='#f1f5f9'
        FontFamily='Segoe UI, -apple-system, BlinkMacSystemFont'>
    <Window.Resources>
        <!-- Custom Dark ComboBoxItem Template -->
        <Style TargetType='ComboBoxItem'>
            <Setter Property='Foreground' Value='#f1f5f9'/>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Padding' Value='10,8'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='ComboBoxItem'>
                        <Border Name='Border' Background='{TemplateBinding Background}' CornerRadius='6' Padding='{TemplateBinding Padding}' Margin='2,1'>
                            <ContentPresenter VerticalAlignment='Center'/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property='IsHighlighted' Value='True'>
                                <Setter TargetName='Border' Property='Background' Value='#1e293b'/>
                                <Setter Property='Foreground' Value='#00f2fe'/>
                            </Trigger>
                            <Trigger Property='IsSelected' Value='True'>
                                <Setter TargetName='Border' Property='Background' Value='#0284c7'/>
                                <Setter Property='Foreground' Value='#ffffff'/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Custom Dark ComboBox Template -->
        <Style TargetType='ComboBox'>
            <Setter Property='Foreground' Value='#f1f5f9'/>
            <Setter Property='FontSize' Value='12'/>
            <Setter Property='Height' Value='36'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='ComboBox'>
                        <Grid>
                            <ToggleButton Name='ToggleButton' Focusable='false'
                                          IsChecked='{Binding Path=IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}'
                                          ClickMode='Press'>
                                <ToggleButton.Template>
                                    <ControlTemplate TargetType='ToggleButton'>
                                        <Border Background='#0f172a' BorderBrush='#334155' BorderThickness='1' CornerRadius='6' Padding='10,6'>
                                            <Path HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,4,0'
                                                  Fill='#94a3b8' Data='M 0 0 L 4 4 L 8 0 Z'/>
                                        </Border>
                                    </ControlTemplate>
                                </ToggleButton.Template>
                            </ToggleButton>
                            <ContentPresenter Name='ContentSite' IsHitTestVisible='False'
                                              Content='{TemplateBinding SelectionBoxItem}'
                                              ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}'
                                              ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'
                                              VerticalAlignment='Center' HorizontalAlignment='Left'
                                              Margin='12,0,24,0'/>
                            <Popup Name='Popup' Placement='Bottom' IsOpen='{TemplateBinding IsDropDownOpen}'
                                   AllowsTransparency='True' Focusable='False' PopupAnimation='Slide'>
                                <Grid Name='DropDown' SnapsToDevicePixels='True'
                                      MinWidth='{TemplateBinding ActualWidth}' MaxHeight='{TemplateBinding MaxDropDownHeight}'>
                                    <Border Background='#0f172a' BorderBrush='#334155' BorderThickness='1' CornerRadius='8' Padding='4' Margin='0,4,0,0'>
                                        <Border.Effect>
                                            <DropShadowEffect Color='#000000' BlurRadius='16' Opacity='0.6' ShadowDepth='4'/>
                                        </Border.Effect>
                                        <ScrollViewer SnapsToDevicePixels='True'>
                                            <StackPanel IsItemsHost='True' KeyboardNavigation.DirectionalNavigation='Cycle'/>
                                        </ScrollViewer>
                                    </Border>
                                </Grid>
                            </Popup>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Custom Cyber Slider Template -->
        <Style TargetType='Slider'>
            <Setter Property='Margin' Value='0,6,0,6'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='Slider'>
                        <Grid VerticalAlignment='Center'>
                            <Track Name='PART_Track'>
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Command='Slider.DecreaseLarge'>
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType='RepeatButton'>
                                                <Border Height='6' Background='#00f2fe' CornerRadius='3,0,0,3'/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.DecreaseRepeatButton>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Command='Slider.IncreaseLarge'>
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType='RepeatButton'>
                                                <Border Height='6' Background='#0c1017' CornerRadius='0,3,3,0' BorderBrush='#1e293b' BorderThickness='0,1,1,1'/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.IncreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb Width='18' Height='18' Cursor='Hand'>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='Thumb'>
                                                <Border Background='#ffffff' BorderBrush='#00f2fe' BorderThickness='2' CornerRadius='9'>
                                                    <Border.Effect>
                                                        <DropShadowEffect Color='#00f2fe' BlurRadius='8' ShadowDepth='0'/>
                                                    </Border.Effect>
                                                </Border>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
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
        <Border Grid.Row='0' Background='#131b26' CornerRadius='12' Padding='16,12' Margin='0,0,0,14'
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
                        <TextBlock Name='TxtStatus' Text='ACTIVE • 48.0 kHz • Zero Lag' FontSize='11' Foreground='#10b981' Margin='0,2,0,0'/>
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
        <Border Grid.Row='1' Background='#131b26' CornerRadius='12' Padding='16' Margin='0,0,0,12'
                BorderBrush='#1e293b' BorderThickness='1'>
            <StackPanel>
                <Grid Margin='0,0,0,8'>
                    <TextBlock Text='INPUT SOURCE' FontWeight='Bold' FontSize='11' Foreground='#94a3b8'/>
                    <TextBlock Text='48 kHz • Stereo' HorizontalAlignment='Right' FontSize='11' Foreground='#00f2fe'/>
                </Grid>
                <ComboBox Name='CbSource' Margin='0,0,0,10'/>

                <TextBlock Text='MASTER LEVEL' FontSize='10' FontWeight='SemiBold' Foreground='#64748b' Margin='0,0,0,4'/>
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
        <StackPanel Grid.Row='2'>
            <!-- Channel 1: PRO X -->
            <Border Background='#131b26' CornerRadius='12' Padding='16' Margin='0,0,0,12'
                    BorderBrush='#1e293b' BorderThickness='1'>
                <StackPanel>
                    <Grid Margin='0,0,0,8'>
                        <TextBlock Text='OUTPUT 1' FontWeight='Bold' FontSize='12' Foreground='#ffffff'/>
                        <Border HorizontalAlignment='Right' Background='#064e3b' CornerRadius='4' Padding='6,2'>
                            <TextBlock Text='CONNECTED' FontSize='9' FontWeight='Bold' Foreground='#10b981'/>
                        </Border>
                    </Grid>

                    <ComboBox Name='CbOut1' Margin='0,0,0,10'/>

                    <Grid Margin='0,0,0,2'>
                        <TextBlock Text='Volume' FontSize='11' Foreground='#94a3b8'/>
                        <TextBlock Name='TxtVol1' Text='100%' HorizontalAlignment='Right' FontSize='11' FontWeight='Bold' Foreground='#00f2fe'/>
                    </Grid>
                    <Slider Name='SliderVol1' Minimum='0' Maximum='125' Value='100' Margin='0,0,0,10'/>

                    <Grid>
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
                    <Grid Margin='0,0,0,8'>
                        <TextBlock Text='OUTPUT 2' FontWeight='Bold' FontSize='12' Foreground='#ffffff'/>
                        <Border HorizontalAlignment='Right' Background='#064e3b' CornerRadius='4' Padding='6,2'>
                            <TextBlock Text='CONNECTED' FontSize='9' FontWeight='Bold' Foreground='#10b981'/>
                        </Border>
                    </Grid>

                    <ComboBox Name='CbOut2' Margin='0,0,0,10'/>

                    <Grid Margin='0,0,0,2'>
                        <TextBlock Text='Volume' FontSize='11' Foreground='#94a3b8'/>
                        <TextBlock Name='TxtVol2' Text='100%' HorizontalAlignment='Right' FontSize='11' FontWeight='Bold' Foreground='#00f2fe'/>
                    </Grid>
                    <Slider Name='SliderVol2' Minimum='0' Maximum='125' Value='100' Margin='0,0,0,10'/>

                    <Grid>
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

        <!-- Footer -->
        <Border Grid.Row='3' Background='#131b26' CornerRadius='10' Padding='14,10'
                BorderBrush='#1e293b' BorderThickness='1'>
            <Grid>
                <CheckBox Name='ChkAutoStart' Content='Launch on Windows Startup' Foreground='#94a3b8' FontSize='11' VerticalAlignment='Center'/>
                <TextBlock Text='v1.0.0 Pure WASAPI • Zero Bloat' HorizontalAlignment='Right' VerticalAlignment='Center' FontSize='10' Foreground='#64748b'/>
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
            // Prioritize PRO X as default source now that it's default speaker
            var prox = devices.Find(d => d.Name.Contains("PRO X"));
            var vmInput = devices.Find(d => d.Name.Contains("Voicemeeter Input"));
            var g435 = devices.Find(d => d.Name.Contains("G435"));

            if (prox != null) _cbSource.SelectedItem = prox;
            else if (vmInput != null) _cbSource.SelectedItem = vmInput;
            else if (devices.Count > 0) _cbSource.SelectedIndex = 0;

            if (prox != null) _cbOut1.SelectedItem = prox;
            else if (devices.Count > 0) _cbOut1.SelectedIndex = 0;

            if (g435 != null) _cbOut2.SelectedItem = g435;
            else if (devices.Count > 1) _cbOut2.SelectedIndex = 1;

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
            System.Drawing.Icon appIcon = null;
            try {
                string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            } catch { }

            if (appIcon == null) {
                try {
                    string localIco = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
                    if (System.IO.File.Exists(localIco)) {
                        appIcon = new System.Drawing.Icon(localIco);
                    }
                } catch { }
            }

            if (appIcon == null) {
                appIcon = System.Drawing.SystemIcons.Application;
            }

            try {
                _window.Icon = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    appIcon.Handle,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
            } catch { }

            _notifyIcon = new Forms.NotifyIcon {
                Icon = appIcon,
                Text = "Dual Audio Mirror",
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
