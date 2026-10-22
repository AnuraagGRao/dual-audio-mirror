using System;
using System.Collections.Generic;
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
        private StackPanel _channelsContainer;
        private Button _btnAddOutput;
        private TextBlock _txtStatus;
        private CheckBox _chkAutoStart;

        // Visualizer Meters
        private Rectangle _meterMasterL;
        private Rectangle _meterMasterR;

        private List<AudioDeviceInfo> _playbackDevices;
        private readonly List<ChannelCardBinding> _channelBindings = new List<ChannelCardBinding>();

        private class ChannelCardBinding {
            public OutputChannel Channel;
            public Rectangle MeterL;
            public Rectangle MeterR;
        }

        private const string AppName = "DualAudioMirror";

        private const string CardTemplateXaml = @"
<Border xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Background='#131b26' CornerRadius='12' Padding='16' Margin='0,0,0,12'
        BorderBrush='#1e293b' BorderThickness='1'>
    <StackPanel>
        <Grid Margin='0,0,0,8'>
            <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                <TextBlock Name='TxtCardHeader' Text='OUTPUT {idx}' FontWeight='Bold' FontSize='12' Foreground='#ffffff'/>
                <Border Background='#064e3b' CornerRadius='4' Padding='6,2' Margin='10,0,0,0'>
                    <TextBlock Text='SYNCED' FontSize='9' FontWeight='Bold' Foreground='#10b981'/>
                </Border>
            </StackPanel>
            <Button Name='BtnRemove' Content='✕ Remove' HorizontalAlignment='Right'
                    Background='#1e293b' Foreground='#ef4444' BorderThickness='0'
                    Padding='8,3' FontSize='10' FontWeight='SemiBold' Cursor='Hand'>
                <Button.Template>
                    <ControlTemplate TargetType='Button'>
                        <Border Background='{TemplateBinding Background}' CornerRadius='4' Padding='{TemplateBinding Padding}'>
                            <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                        </Border>
                    </ControlTemplate>
                </Button.Template>
            </Button>
        </Grid>

        <ComboBox Name='CbDevice' Margin='0,0,0,10'/>

        <Grid Margin='0,0,0,2'>
            <TextBlock Text='Volume' FontSize='11' Foreground='#94a3b8'/>
            <TextBlock Name='TxtVol' Text='{vol}%' HorizontalAlignment='Right' FontSize='11' FontWeight='Bold' Foreground='#00f2fe'/>
        </Grid>
        <Slider Name='SliderVol' Minimum='0' Maximum='125' Value='{vol}' Margin='0,0,0,10'/>

        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width='*'/>
                <ColumnDefinition Width='Auto'/>
            </Grid.ColumnDefinitions>
            <StackPanel Grid.Column='0' VerticalAlignment='Center' Margin='0,0,12,0'>
                <Grid Height='6' Margin='0,0,0,3'>
                    <Border Background='#0c1017' CornerRadius='3'/>
                    <Rectangle Name='MeterL' HorizontalAlignment='Left' Width='0' Fill='#10b981' RadiusX='3' RadiusY='3'/>
                </Grid>
                <Grid Height='6'>
                    <Border Background='#0c1017' CornerRadius='3'/>
                    <Rectangle Name='MeterR' HorizontalAlignment='Left' Width='0' Fill='#10b981' RadiusX='3' RadiusY='3'/>
                </Grid>
            </StackPanel>

            <Button Name='BtnMute' Grid.Column='1' Content='Mute' Width='64' Height='28'
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
</Border>";

        public Window Window {
            get { return _window; }
        }

        public MainWindow(AudioEngine engine, Application app) {
            _engine = engine;
            _app = app;
            _playbackDevices = AudioEngine.GetPlaybackDevices();
            BuildUI();
            SetupNotifyIcon();
            SetupTimer();
        }

        private void BuildUI() {
            string xaml = @"
<Window xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
        xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'
        Title='Dual Audio Mirror' Width='560' Height='780' MinWidth='500' MinHeight='560'
        WindowStartupLocation='CenterScreen' ResizeMode='CanResizeWithGrip'
        Background='#0c1017' Foreground='#f1f5f9'
        FontFamily='Segoe UI, -apple-system, BlinkMacSystemFont'>
    <Window.Resources>
        <!-- Custom Dark ScrollBar -->
        <Style TargetType='ScrollBar'>
            <Setter Property='Background' Value='Transparent'/>
            <Setter Property='Width' Value='8'/>
            <Setter Property='Template'>
                <Setter.Value>
                    <ControlTemplate TargetType='ScrollBar'>
                        <Grid Background='Transparent'>
                            <Track Name='PART_Track' IsDirectionReversed='True'>
                                <Track.Thumb>
                                    <Thumb>
                                        <Thumb.Template>
                                            <ControlTemplate TargetType='Thumb'>
                                                <Border Background='#334155' CornerRadius='4'/>
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
                        <TextBlock Text='DUAL &amp; MULTI AUDIO MIRROR' FontWeight='Bold' FontSize='16' Foreground='#ffffff'/>
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

        <!-- Dynamic Output Channels Section -->
        <Grid Grid.Row='2' Margin='0,0,0,12'>
            <Grid.RowDefinitions>
                <RowDefinition Height='Auto'/>
                <RowDefinition Height='*'/>
            </Grid.RowDefinitions>

            <Grid Grid.Row='0' Margin='0,0,0,8'>
                <StackPanel Orientation='Horizontal' VerticalAlignment='Center'>
                    <TextBlock Text='OUTPUT CHANNELS' FontWeight='Bold' FontSize='11' Foreground='#94a3b8'/>
                    <Border Background='#1e293b' CornerRadius='4' Padding='6,2' Margin='8,0,0,0'>
                        <TextBlock Text='MULTI-SYNC' FontSize='9' FontWeight='Bold' Foreground='#00f2fe'/>
                    </Border>
                </StackPanel>

                <Button Name='BtnAddOutput' Content='+ Add Output' HorizontalAlignment='Right'
                        Background='#0284c7' Foreground='#ffffff' BorderThickness='0'
                        Padding='12,5' FontSize='11' FontWeight='SemiBold' Cursor='Hand'>
                    <Button.Template>
                        <ControlTemplate TargetType='Button'>
                            <Border Name='bdr' Background='{TemplateBinding Background}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
                                <ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/>
                            </Border>
                            <ControlTemplate.Triggers>
                                <Trigger Property='IsMouseOver' Value='True'>
                                    <Setter TargetName='bdr' Property='Background' Value='#0369a1'/>
                                </Trigger>
                            </ControlTemplate.Triggers>
                        </ControlTemplate>
                    </Button.Template>
                </Button>
            </Grid>

            <ScrollViewer Grid.Row='1' VerticalScrollBarVisibility='Auto' HorizontalScrollBarVisibility='Disabled' Focusable='False'>
                <StackPanel Name='ChannelsContainer' Margin='0,0,6,0'/>
            </ScrollViewer>
        </Grid>

        <!-- Footer -->
        <Border Grid.Row='3' Background='#131b26' CornerRadius='10' Padding='14,10'
                BorderBrush='#1e293b' BorderThickness='1'>
            <Grid>
                <CheckBox Name='ChkAutoStart' Content='Launch on Windows Startup' Foreground='#94a3b8' FontSize='11' VerticalAlignment='Center'/>
                <TextBlock Text='v1.2.0 Multi-Endpoint • Pure WASAPI' HorizontalAlignment='Right' VerticalAlignment='Center' FontSize='10' Foreground='#64748b'/>
            </Grid>
        </Border>
    </Grid>
</Window>";

            _window = (Window)XamlReader.Parse(xaml);

            // Bind Elements
            _txtStatus = (TextBlock)_window.FindName("TxtStatus");
            _cbSource = (ComboBox)_window.FindName("CbSource");
            _channelsContainer = (StackPanel)_window.FindName("ChannelsContainer");
            _btnAddOutput = (Button)_window.FindName("BtnAddOutput");
            _meterMasterL = (Rectangle)_window.FindName("MeterMasterL");
            _meterMasterR = (Rectangle)_window.FindName("MeterMasterR");
            _chkAutoStart = (CheckBox)_window.FindName("ChkAutoStart");
            Button btnTray = (Button)_window.FindName("BtnTray");

            // Populate Master Source Dropdown
            foreach (var d in _playbackDevices) {
                _cbSource.Items.Add(d);
            }

            var prox = _playbackDevices.Find(d => d.Name.Contains("PRO X"));
            var vmInput = _playbackDevices.Find(d => d.Name.Contains("Voicemeeter Input"));
            if (prox != null) _cbSource.SelectedItem = prox;
            else if (vmInput != null) _cbSource.SelectedItem = vmInput;
            else if (_playbackDevices.Count > 0) _cbSource.SelectedIndex = 0;

            _cbSource.SelectionChanged += (s, e) => {
                var d = _cbSource.SelectedItem as AudioDeviceInfo;
                if (d != null) {
                    _engine.CaptureDeviceId = d.Id;
                    RestartEngine();
                }
            };

            // Hook Add Output button
            _btnAddOutput.Click += (s, e) => {
                var currentChannels = _engine.GetChannels();
                var usedIds = new HashSet<string>();
                foreach (var c in currentChannels) {
                    if (!string.IsNullOrEmpty(c.DeviceId)) usedIds.Add(c.DeviceId);
                }

                AudioDeviceInfo nextDev = null;
                foreach (var d in _playbackDevices) {
                    if (!usedIds.Contains(d.Id)) {
                        nextDev = d;
                        break;
                    }
                }
                if (nextDev == null && _playbackDevices.Count > 0) {
                    nextDev = _playbackDevices[0];
                }

                _engine.AddChannel(nextDev != null ? nextDev.Id : null);
                RefreshChannelCards();
                RestartEngine();
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

            _window.SourceInitialized += (s, e) => {
                try {
                    var helper = new System.Windows.Interop.WindowInteropHelper(_window);
                    var source = System.Windows.Interop.HwndSource.FromHwnd(helper.Handle);
                    if (source != null) {
                        source.AddHook(WndProc);
                    }
                } catch { }
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

            // Initial render of output channels
            RefreshChannelCards();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true, CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        private static readonly uint WM_SHOWME = RegisterWindowMessage("DUAL_AUDIO_MIRROR_WAKEUP");

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if (WM_SHOWME != 0 && (uint)msg == WM_SHOWME) {
                _window.Dispatcher.Invoke(ShowWindow);
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void RefreshChannelCards() {
            _channelsContainer.Children.Clear();
            _channelBindings.Clear();

            var channels = _engine.GetChannels();
            for (int i = 0; i < channels.Count; i++) {
                var ch = channels[i];
                int cardIndex = i + 1;
                string cardXaml = CardTemplateXaml
                    .Replace("{idx}", cardIndex.ToString())
                    .Replace("{vol}", ((int)(ch.Volume * 100)).ToString());

                var cardBorder = (Border)XamlReader.Parse(cardXaml);

                var btnRemove = (Button)cardBorder.FindName("BtnRemove");
                var cbDevice = (ComboBox)cardBorder.FindName("CbDevice");
                var sliderVol = (Slider)cardBorder.FindName("SliderVol");
                var txtVol = (TextBlock)cardBorder.FindName("TxtVol");
                var meterL = (Rectangle)cardBorder.FindName("MeterL");
                var meterR = (Rectangle)cardBorder.FindName("MeterR");
                var btnMute = (Button)cardBorder.FindName("BtnMute");

                if (channels.Count <= 1) {
                    btnRemove.Visibility = Visibility.Collapsed;
                } else {
                    btnRemove.Click += (s, e) => {
                        _engine.RemoveChannel(ch);
                        RefreshChannelCards();
                        RestartEngine();
                    };
                }

                foreach (var d in _playbackDevices) {
                    cbDevice.Items.Add(d);
                }

                if (!string.IsNullOrEmpty(ch.DeviceId)) {
                    var match = _playbackDevices.Find(d => d.Id == ch.DeviceId);
                    if (match != null) cbDevice.SelectedItem = match;
                } else if (_playbackDevices.Count > 0) {
                    cbDevice.SelectedIndex = 0;
                    ch.DeviceId = ((AudioDeviceInfo)cbDevice.SelectedItem).Id;
                }

                cbDevice.SelectionChanged += (s, e) => {
                    var d = cbDevice.SelectedItem as AudioDeviceInfo;
                    if (d != null && d.Id != ch.DeviceId) {
                        ch.DeviceId = d.Id;
                        RestartEngine();
                    }
                };

                sliderVol.ValueChanged += (s, e) => {
                    int val = (int)sliderVol.Value;
                    txtVol.Text = val + "%";
                    ch.Volume = val / 100f;
                };

                if (ch.IsMuted) {
                    btnMute.Content = "MUTED";
                    btnMute.Background = new SolidColorBrush(Color.FromRgb(185, 28, 28));
                }

                btnMute.Click += (s, e) => {
                    ch.IsMuted = !ch.IsMuted;
                    btnMute.Content = ch.IsMuted ? "MUTED" : "Mute";
                    btnMute.Background = ch.IsMuted ? new SolidColorBrush(Color.FromRgb(185, 28, 28)) : new SolidColorBrush(Color.FromRgb(30, 41, 59));
                };

                _channelsContainer.Children.Add(cardBorder);
                _channelBindings.Add(new ChannelCardBinding {
                    Channel = ch,
                    MeterL = meterL,
                    MeterR = meterR
                });
            }
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
            if (!_window.IsVisible) {
                _window.Show();
            }
            if (_window.WindowState == WindowState.Minimized) {
                _window.WindowState = WindowState.Normal;
            }
            _window.Activate();
            _window.Topmost = true;
            _window.Topmost = false;
            _window.Focus();
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
                    for (int i = 0; i < _channelBindings.Count; i++) {
                        var b = _channelBindings[i];
                        if (b != null && b.Channel != null && b.MeterL != null && b.MeterR != null) {
                            float pL = b.Channel.PeakL;
                            float pR = b.Channel.PeakR;
                            if (float.IsNaN(pL) || float.IsInfinity(pL) || pL < 0f) pL = 0f;
                            if (float.IsNaN(pR) || float.IsInfinity(pR) || pR < 0f) pR = 0f;
                            b.MeterL.Width = Math.Max(0.0, Math.Min(chW, (double)pL * chW));
                            b.MeterR.Width = Math.Max(0.0, Math.Min(chW, (double)pR * chW));
                        }
                    }
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
