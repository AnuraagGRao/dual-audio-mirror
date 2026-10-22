using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace DualAudioMirror {
    #region COM Interfaces

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    internal class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice(string id, out IMMDevice endpoint);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice {
        [PreserveSig] int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PROPERTYKEY key);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT value);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROPERTYKEY {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PROPVARIANT {
        [FieldOffset(0)] public short vt;
        [FieldOffset(8)] public IntPtr pwszVal;
    }

    [Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient {
        [PreserveSig] int Initialize(int shareMode, int streamFlags, long hnsBufferDuration, long hnsPeriodicity, IntPtr pFormat, ref Guid audioSessionGuid);
        [PreserveSig] int GetBufferSize(out uint pNumBufferFrames);
        [PreserveSig] int GetStreamLatency(out long phnsLatency);
        [PreserveSig] int GetCurrentPadding(out uint pNumPaddingFrames);
        [PreserveSig] int IsFormatSupported(int shareMode, IntPtr pFormat, out IntPtr ppClosestMatch);
        [PreserveSig] int GetMixFormat(out IntPtr ppDeviceFormat);
        [PreserveSig] int GetDevicePeriod(out long phnsDefaultDevicePeriod, out long phnsMinimumDevicePeriod);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr eventHandle);
        [PreserveSig] int GetService([In] ref Guid riid, out IntPtr ppv);
    }

    [Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient {
        [PreserveSig] int GetBuffer(out IntPtr ppData, out uint pNumFramesToRead, out uint pdwFlags, out ulong pu64DevicePosition, out ulong pu64QPCPosition);
        [PreserveSig] int ReleaseBuffer(uint numFramesRead);
        [PreserveSig] int GetNextPacketSize(out uint pNumFramesInNextPacket);
    }

    [Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient {
        [PreserveSig] int GetBuffer(uint numFramesRequested, out IntPtr ppData);
        [PreserveSig] int ReleaseBuffer(uint numFramesWritten, uint dwFlags);
    }

    #endregion

    public class AudioDeviceInfo {
        public string Id { get; set; }
        public string Name { get; set; }
        public override string ToString() { return Name; }
    }

    public class OutputChannel {
        public string Id { get; set; }
        public string DeviceId { get; set; }
        public float Volume { get; set; }
        public bool IsMuted { get; set; }
        public float PeakL { get; set; }
        public float PeakR { get; set; }

        internal IAudioClient Client;
        internal IAudioRenderClient Render;
        internal uint BufferSize;
        internal float[] ProcessBuffer;

        public OutputChannel() {
            Id = Guid.NewGuid().ToString("N");
            Volume = 1.0f;
            IsMuted = false;
        }

        public OutputChannel(string deviceId, float volume = 1.0f) : this() {
            DeviceId = deviceId;
            Volume = volume;
        }
    }

    public class AudioEngine : IDisposable {
        private static Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
        private static Guid IID_IAudioRenderClient = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");

        [DllImport("Avrt.dll")]
        private static extern IntPtr AvSetMmThreadCharacteristics(string TaskName, ref uint TaskIndex);

        [DllImport("Avrt.dll")]
        private static extern bool AvRevertMmThreadCharacteristics(IntPtr AvrtHandle);

        private Thread _workerThread;
        private volatile bool _isRunning;

        // Device IDs
        public string CaptureDeviceId { get; set; }

        // Dynamic Multi-Output Channels
        private readonly List<OutputChannel> _channels = new List<OutputChannel>();
        private readonly object _channelLock = new object();

        // Live visualizer levels (0.0 to 1.0)
        public float PeakMasterL { get; private set; }
        public float PeakMasterR { get; private set; }

        public bool IsActive {
            get { return _isRunning; }
        }

        public event Action<string> OnStatusChanged;

        public AudioEngine() {
            var all = GetPlaybackDevices();
            var prox = all.Find(d => d.Name.Contains("PRO X"));
            var g435 = all.Find(d => d.Name.Contains("G435"));

            string id1 = prox != null ? prox.Id : (all.Count > 0 ? all[0].Id : null);
            string id2 = g435 != null ? g435.Id : (all.Count > 1 ? all[1].Id : null);

            _channels.Add(new OutputChannel(id1));
            _channels.Add(new OutputChannel(id2));
        }

        public List<OutputChannel> GetChannels() {
            lock (_channelLock) {
                return new List<OutputChannel>(_channels);
            }
        }

        public OutputChannel AddChannel(string deviceId = null) {
            lock (_channelLock) {
                var ch = new OutputChannel(deviceId);
                _channels.Add(ch);
                return ch;
            }
        }

        public bool RemoveChannel(OutputChannel ch) {
            lock (_channelLock) {
                return _channels.Remove(ch);
            }
        }

        public static List<AudioDeviceInfo> GetPlaybackDevices() {
            var list = new List<AudioDeviceInfo>();
            try {
                var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
                IMMDeviceCollection coll;
                if (enumerator.EnumAudioEndpoints(0, 1, out coll) == 0) {
                    uint count;
                    coll.GetCount(out count);
                    var pkeyName = new PROPERTYKEY {
                        fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                        pid = 14
                    };

                    for (uint i = 0; i < count; i++) {
                        IMMDevice dev;
                        if (coll.Item(i, out dev) == 0) {
                            string id;
                            dev.GetId(out id);
                            IPropertyStore store;
                            dev.OpenPropertyStore(0, out store);
                            PROPVARIANT val;
                            store.GetValue(ref pkeyName, out val);
                            string name = Marshal.PtrToStringUni(val.pwszVal) ?? "Unknown Device";
                            list.Add(new AudioDeviceInfo { Id = id, Name = name });
                        }
                    }
                }
            } catch (Exception ex) {
                Console.WriteLine("EnumAudioEndpoints error: " + ex.Message);
            }
            return list;
        }

        public void Start() {
            if (_isRunning) return;
            _isRunning = true;
            _workerThread = new Thread(AudioLoop) {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "DualAudioMirrorEngine"
            };
            _workerThread.Start();
        }

        public void Stop() {
            _isRunning = false;
            if (_workerThread != null && _workerThread.IsAlive) {
                _workerThread.Join(500);
            }
        }

        private void TriggerStatus(string msg) {
            var handler = OnStatusChanged;
            if (handler != null) handler(msg);
        }

        private void AudioLoop() {
            uint taskIndex = 0;
            IntPtr mmcssHandle = AvSetMmThreadCharacteristics("Pro Audio", ref taskIndex);

            while (_isRunning) {
                try {
                    RunStreamingSession();
                } catch (Exception ex) {
                    TriggerStatus("Audio Engine Warning: " + ex.Message);
                    Thread.Sleep(1000);
                }
            }

            if (mmcssHandle != IntPtr.Zero) {
                AvRevertMmThreadCharacteristics(mmcssHandle);
            }
        }

        private void RunStreamingSession() {
            var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject();
            IMMDevice devCap = null;

            // Resolve Capture Device
            if (!string.IsNullOrEmpty(CaptureDeviceId)) {
                enumerator.GetDevice(CaptureDeviceId, out devCap);
            }
            if (devCap == null) {
                // Default to Voicemeeter Input or system default
                var all = GetPlaybackDevices();
                var vm = all.Find(d => d.Name.Contains("Voicemeeter Input"));
                if (vm != null) enumerator.GetDevice(vm.Id, out devCap);
                else enumerator.GetDefaultAudioEndpoint(0, 0, out devCap);
            }

            if (devCap == null) {
                TriggerStatus("Waiting for Capture Device...");
                Thread.Sleep(500);
                return;
            }

            string devCapId = "";
            devCap.GetId(out devCapId);

            Guid sessionGuid = Guid.Empty;
            object oCap;

            devCap.Activate(ref IID_IAudioClient, 1, IntPtr.Zero, out oCap);
            var cCap = (IAudioClient)oCap;

            IntPtr pFmt;
            cCap.GetMixFormat(out pFmt);

            // 15ms buffer duration = 150,000 100-nanoseconds
            long hnsBufferDuration = 150000;
            int hr = cCap.Initialize(0, 0x00020000, hnsBufferDuration, 0, pFmt, ref sessionGuid);
            if (hr != 0) {
                TriggerStatus("Capture init failed (0x" + hr.ToString("X8") + ")");
                Thread.Sleep(500);
                return;
            }

            IntPtr pCapService;
            cCap.GetService(ref IID_IAudioCaptureClient, out pCapService);
            var capService = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(pCapService);

            // Snapshot active channels
            List<OutputChannel> activeChannels;
            lock (_channelLock) {
                activeChannels = new List<OutputChannel>(_channels);
            }

            var readyChannels = new List<OutputChannel>();
            foreach (var ch in activeChannels) {
                ch.Client = null;
                ch.Render = null;
                ch.BufferSize = 0;
                ch.ProcessBuffer = null;

                if (string.IsNullOrEmpty(ch.DeviceId)) continue;
                if (string.Equals(ch.DeviceId, devCapId, StringComparison.OrdinalIgnoreCase)) continue;

                try {
                    IMMDevice devRender;
                    if (enumerator.GetDevice(ch.DeviceId, out devRender) == 0 && devRender != null) {
                        object oRender;
                        devRender.Activate(ref IID_IAudioClient, 1, IntPtr.Zero, out oRender);
                        var cRender = (IAudioClient)oRender;
                        if (cRender.Initialize(0, 0, hnsBufferDuration, 0, pFmt, ref sessionGuid) == 0) {
                            IntPtr pRenderService;
                            cRender.GetService(ref IID_IAudioRenderClient, out pRenderService);
                            var renderClient = (IAudioRenderClient)Marshal.GetObjectForIUnknown(pRenderService);
                            uint bufSize;
                            cRender.GetBufferSize(out bufSize);
                            cRender.Start();

                            ch.Client = cRender;
                            ch.Render = renderClient;
                            ch.BufferSize = bufSize;
                            ch.ProcessBuffer = new float[16384];

                            readyChannels.Add(ch);
                        }
                    }
                } catch {
                    ch.Client = null;
                    ch.Render = null;
                }
            }

            cCap.Start();
            TriggerStatus("Active • " + readyChannels.Count + " Outputs Synced • 48.0 kHz");

            float[] floatBuffer = new float[16384];

            while (_isRunning) {
                uint packetSize;
                int hrNext = capService.GetNextPacketSize(out packetSize);
                if (hrNext != 0) break; // Device invalidated or changed

                while (packetSize > 0 && _isRunning) {
                    IntPtr pData;
                    uint numFrames;
                    uint flags;
                    ulong devPos, qpcPos;
                    if (capService.GetBuffer(out pData, out numFrames, out flags, out devPos, out qpcPos) == 0 && numFrames > 0) {
                        int numFloats = (int)numFrames * 2;
                        if (numFloats > floatBuffer.Length) {
                            floatBuffer = new float[numFloats * 2];
                        }

                        bool isSilent = (flags & 2) != 0;
                        float maxL = 0f, maxR = 0f;

                        if (isSilent) {
                            Array.Clear(floatBuffer, 0, numFloats);
                        } else {
                            Marshal.Copy(pData, floatBuffer, 0, numFloats);
                            // Measure Master Peaks
                            for (int i = 0; i < numFloats; i += 2) {
                                float l = Math.Abs(floatBuffer[i]);
                                float r = Math.Abs(floatBuffer[i + 1]);
                                if (l > maxL) maxL = l;
                                if (r > maxR) maxR = r;
                            }
                        }

                        PeakMasterL = maxL;
                        PeakMasterR = maxR;

                        for (int i = 0; i < readyChannels.Count; i++) {
                            var ch = readyChannels[i];
                            if (ch.Render == null || ch.Client == null) continue;

                            if (ch.ProcessBuffer == null || ch.ProcessBuffer.Length < numFloats) {
                                ch.ProcessBuffer = new float[numFloats * 2];
                            }

                            float g = ch.IsMuted ? 0f : ch.Volume;
                            for (int f = 0; f < numFloats; f++) {
                                ch.ProcessBuffer[f] = floatBuffer[f] * g;
                            }
                            ch.PeakL = maxL * g;
                            ch.PeakR = maxR * g;

                            uint pad;
                            if (ch.Client.GetCurrentPadding(out pad) == 0) {
                                if (ch.BufferSize - pad >= numFrames) {
                                    IntPtr pRender;
                                    if (ch.Render.GetBuffer(numFrames, out pRender) == 0) {
                                        Marshal.Copy(ch.ProcessBuffer, 0, pRender, numFloats);
                                        ch.Render.ReleaseBuffer(numFrames, 0);
                                    }
                                }
                            }
                        }

                        capService.ReleaseBuffer(numFrames);
                    } else {
                        break;
                    }
                    capService.GetNextPacketSize(out packetSize);
                }

                Thread.Sleep(1);
            }

            try { cCap.Stop(); } catch { }
            for (int i = 0; i < readyChannels.Count; i++) {
                try {
                    if (readyChannels[i].Client != null) readyChannels[i].Client.Stop();
                } catch { }
            }
        }

        public void Dispose() {
            Stop();
        }
    }
}
