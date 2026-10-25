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

    /// <summary>
    /// Zero-allocation, high-performance circular audio buffer for stereo 32-bit float samples.
    /// Eliminates audio tearing, buffer drops, and hardware clock-drift between independent audio endpoints.
    /// </summary>
    internal class AudioRingBuffer {
        private static readonly float[] Silence = new float[16384];

        private readonly float[] _buffer;
        private readonly int _capacityFloats;
        private int _writePos;
        private int _readPos;
        private int _availableFloats;
        private readonly object _lock = new object();

        public AudioRingBuffer(int capacityFrames = 48000) {
            _capacityFloats = capacityFrames * 2; // 2 floats per stereo frame
            _buffer = new float[_capacityFloats];
            _writePos = 0;
            _readPos = 0;
            _availableFloats = 0;
        }

        public int AvailableFrames {
            get {
                lock (_lock) {
                    return _availableFloats / 2;
                }
            }
        }

        public void Write(float[] source, int offset, int frameCount) {
            int floatCount = frameCount * 2;
            if (floatCount <= 0) return;

            lock (_lock) {
                // If writing would exceed capacity, advance read pointer (drop oldest) to avoid blocking
                int space = _capacityFloats - _availableFloats;
                if (floatCount > space) {
                    int overflow = floatCount - space;
                    _readPos = (_readPos + overflow) % _capacityFloats;
                    _availableFloats -= overflow;
                }

                int firstChunk = Math.Min(floatCount, _capacityFloats - _writePos);
                Array.Copy(source, offset, _buffer, _writePos, firstChunk);
                _writePos = (_writePos + firstChunk) % _capacityFloats;

                int secondChunk = floatCount - firstChunk;
                if (secondChunk > 0) {
                    Array.Copy(source, offset + firstChunk, _buffer, _writePos, secondChunk);
                    _writePos = (_writePos + secondChunk) % _capacityFloats;
                }

                _availableFloats += floatCount;
            }
        }

        public void Read(IntPtr destination, int frameCount) {
            int floatCount = frameCount * 2;
            if (floatCount <= 0) return;

            lock (_lock) {
                int toRead = Math.Min(floatCount, _availableFloats);
                if (toRead > 0) {
                    int firstChunk = Math.Min(toRead, _capacityFloats - _readPos);
                    Marshal.Copy(_buffer, _readPos, destination, firstChunk);
                    _readPos = (_readPos + firstChunk) % _capacityFloats;

                    int secondChunk = toRead - firstChunk;
                    if (secondChunk > 0) {
                        IntPtr destSecond = new IntPtr(destination.ToInt64() + firstChunk * sizeof(float));
                        Marshal.Copy(_buffer, _readPos, destSecond, secondChunk);
                        _readPos = (_readPos + secondChunk) % _capacityFloats;
                    }

                    _availableFloats -= toRead;
                }

                // If hardware requested more frames than available (underrun protection), pad with silence
                if (toRead < floatCount) {
                    int missing = floatCount - toRead;
                    IntPtr destOffset = new IntPtr(destination.ToInt64() + toRead * sizeof(float));
                    int padded = 0;
                    while (padded < missing) {
                        int chunk = Math.Min(missing - padded, Silence.Length);
                        Marshal.Copy(Silence, 0, new IntPtr(destOffset.ToInt64() + padded * sizeof(float)), chunk);
                        padded += chunk;
                    }
                }
            }
        }

        public void MaintainWatermark(int maxAllowedFrames) {
            lock (_lock) {
                int currentFrames = _availableFloats / 2;
                if (currentFrames > maxAllowedFrames) {
                    int excessFloats = (currentFrames - maxAllowedFrames) * 2;
                    _readPos = (_readPos + excessFloats) % _capacityFloats;
                    _availableFloats -= excessFloats;
                }
            }
        }

        public void Clear() {
            lock (_lock) {
                _writePos = 0;
                _readPos = 0;
                _availableFloats = 0;
            }
        }
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
        internal readonly AudioRingBuffer Fifo;

        public OutputChannel() {
            Id = Guid.NewGuid().ToString("N");
            Volume = 1.0f;
            IsMuted = false;
            Fifo = new AudioRingBuffer(48000); // 1 sec at 48kHz stereo
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

        [DllImport("winmm.dll", EntryPoint = "timeBeginPeriod")]
        private static extern uint TimeBeginPeriod(uint uMilliseconds);

        [DllImport("winmm.dll", EntryPoint = "timeEndPeriod")]
        private static extern uint TimeEndPeriod(uint uMilliseconds);

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
            TimeBeginPeriod(1);

            try {
                while (_isRunning) {
                    try {
                        RunStreamingSession();
                    } catch (Exception ex) {
                        TriggerStatus("Audio Engine Warning: " + ex.Message);
                        Thread.Sleep(1000);
                    }
                }
            } finally {
                TimeEndPeriod(1);
                if (mmcssHandle != IntPtr.Zero) {
                    AvRevertMmThreadCharacteristics(mmcssHandle);
                }
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

            short capChannels = Marshal.ReadInt16(pFmt, 2);
            int capSampleRate = Marshal.ReadInt32(pFmt, 4);

            // 20ms capture buffer duration
            long hnsCaptureBufferDuration = 200000;
            int hr = cCap.Initialize(0, 0x00020000, hnsCaptureBufferDuration, 0, pFmt, ref sessionGuid);
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
            // Generous 40ms render buffer duration to protect against hardware DAC jitter / underruns
            long hnsRenderBufferDuration = 400000;

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
                        if (cRender.Initialize(0, 0, hnsRenderBufferDuration, 0, pFmt, ref sessionGuid) == 0) {
                            IntPtr pRenderService;
                            cRender.GetService(ref IID_IAudioRenderClient, out pRenderService);
                            var renderClient = (IAudioRenderClient)Marshal.GetObjectForIUnknown(pRenderService);
                            uint bufSize;
                            cRender.GetBufferSize(out bufSize);

                            ch.Client = cRender;
                            ch.Render = renderClient;
                            ch.BufferSize = bufSize;
                            ch.ProcessBuffer = new float[32768];
                            ch.Fifo.Clear();

                            // Pre-fill with ~20ms of silence so the endpoint begins with a healthy safety cushion
                            uint prefillFrames = Math.Min(bufSize / 2, 960u);
                            IntPtr pPre;
                            if (renderClient.GetBuffer(prefillFrames, out pPre) == 0) {
                                ch.Fifo.Read(pPre, (int)prefillFrames); // Will write silence since Fifo is currently empty
                                renderClient.ReleaseBuffer(prefillFrames, 0);
                            }

                            cRender.Start();
                            readyChannels.Add(ch);
                        }
                    }
                } catch {
                    ch.Client = null;
                    ch.Render = null;
                }
            }

            cCap.Start();
            TriggerStatus("Active • " + readyChannels.Count + " Outputs Synced • " + (capSampleRate / 1000.0).ToString("F1") + " kHz");

            float[] rawCaptureBuffer = new float[65536];
            float[] stereoBuffer = new float[32768];

            while (_isRunning) {
                uint packetSize;
                int hrNext = capService.GetNextPacketSize(out packetSize);
                if (hrNext != 0) break; // Device invalidated or unplugged

                bool hadActivity = false;

                while (packetSize > 0 && _isRunning) {
                    IntPtr pData;
                    uint numFrames;
                    uint flags;
                    ulong devPos, qpcPos;
                    if (capService.GetBuffer(out pData, out numFrames, out flags, out devPos, out qpcPos) == 0 && numFrames > 0) {
                        hadActivity = true;
                        bool isSilent = (flags & 2) != 0;
                        int stereoFloats = (int)numFrames * 2;

                        if (stereoFloats > stereoBuffer.Length) {
                            stereoBuffer = new float[stereoFloats * 2];
                        }

                        if (isSilent) {
                            Array.Clear(stereoBuffer, 0, stereoFloats);
                            PeakMasterL = 0f;
                            PeakMasterR = 0f;
                        } else {
                            if (capChannels == 2) {
                                Marshal.Copy(pData, stereoBuffer, 0, stereoFloats);
                            } else if (capChannels == 8) {
                                // Downmix 7.1 Surround (FL, FR, Center, LFE, BL, BR, SL, SR) to Stereo
                                int rawCount = (int)numFrames * 8;
                                if (rawCount > rawCaptureBuffer.Length) rawCaptureBuffer = new float[rawCount * 2];
                                Marshal.Copy(pData, rawCaptureBuffer, 0, rawCount);

                                for (int f = 0; f < numFrames; f++) {
                                    int b = f * 8;
                                    float fl = rawCaptureBuffer[b];
                                    float fr = rawCaptureBuffer[b + 1];
                                    float c  = rawCaptureBuffer[b + 2];
                                    float bl = rawCaptureBuffer[b + 4];
                                    float br = rawCaptureBuffer[b + 5];
                                    float sl = rawCaptureBuffer[b + 6];
                                    float sr = rawCaptureBuffer[b + 7];
                                    stereoBuffer[f * 2]     = (fl + 0.707f * c + 0.707f * sl + 0.707f * bl) * 0.5f;
                                    stereoBuffer[f * 2 + 1] = (fr + 0.707f * c + 0.707f * sr + 0.707f * br) * 0.5f;
                                }
                            } else if (capChannels == 6) {
                                // Downmix 5.1 Surround (FL, FR, Center, LFE, SL, SR) to Stereo
                                int rawCount = (int)numFrames * 6;
                                if (rawCount > rawCaptureBuffer.Length) rawCaptureBuffer = new float[rawCount * 2];
                                Marshal.Copy(pData, rawCaptureBuffer, 0, rawCount);

                                for (int f = 0; f < numFrames; f++) {
                                    int b = f * 6;
                                    float fl = rawCaptureBuffer[b];
                                    float fr = rawCaptureBuffer[b + 1];
                                    float c  = rawCaptureBuffer[b + 2];
                                    float sl = rawCaptureBuffer[b + 4];
                                    float sr = rawCaptureBuffer[b + 5];
                                    stereoBuffer[f * 2]     = (fl + 0.707f * c + 0.707f * sl) * 0.6f;
                                    stereoBuffer[f * 2 + 1] = (fr + 0.707f * c + 0.707f * sr) * 0.6f;
                                }
                            } else {
                                // Fallback: Take first 2 channels
                                int rawCount = (int)numFrames * capChannels;
                                if (rawCount > rawCaptureBuffer.Length) rawCaptureBuffer = new float[rawCount * 2];
                                Marshal.Copy(pData, rawCaptureBuffer, 0, rawCount);

                                for (int f = 0; f < numFrames; f++) {
                                    stereoBuffer[f * 2]     = rawCaptureBuffer[f * capChannels];
                                    stereoBuffer[f * 2 + 1] = rawCaptureBuffer[f * capChannels + 1];
                                }
                            }

                            // Measure Master Peaks
                            float maxL = 0f, maxR = 0f;
                            for (int i = 0; i < stereoFloats; i += 2) {
                                float l = Math.Abs(stereoBuffer[i]);
                                float r = Math.Abs(stereoBuffer[i + 1]);
                                if (l > maxL) maxL = l;
                                if (r > maxR) maxR = r;
                            }
                            PeakMasterL = maxL;
                            PeakMasterR = maxR;
                        }

                        // Distribute to each active channel's ring buffer with volume & mute applied
                        for (int i = 0; i < readyChannels.Count; i++) {
                            var ch = readyChannels[i];
                            if (ch.ProcessBuffer == null || ch.ProcessBuffer.Length < stereoFloats) {
                                ch.ProcessBuffer = new float[stereoFloats * 2];
                            }

                            float g = ch.IsMuted ? 0f : ch.Volume;
                            for (int f = 0; f < stereoFloats; f++) {
                                ch.ProcessBuffer[f] = stereoBuffer[f] * g;
                            }
                            ch.PeakL = PeakMasterL * g;
                            ch.PeakR = PeakMasterR * g;

                            ch.Fifo.Write(ch.ProcessBuffer, 0, (int)numFrames);
                            // Keep buffer backlog under 50ms (2400 frames at 48kHz) to eliminate clock drift
                            ch.Fifo.MaintainWatermark(2400);
                        }

                        capService.ReleaseBuffer(numFrames);
                    } else {
                        break;
                    }
                    capService.GetNextPacketSize(out packetSize);
                }

                // Feed each render channel dynamically from its FIFO
                for (int i = 0; i < readyChannels.Count; i++) {
                    var ch = readyChannels[i];
                    if (ch.Render == null || ch.Client == null) continue;

                    uint pad;
                    if (ch.Client.GetCurrentPadding(out pad) == 0) {
                        uint space = ch.BufferSize - pad;
                        uint available = (uint)ch.Fifo.AvailableFrames;
                        uint framesToWrite = Math.Min(space, available);

                        if (framesToWrite > 0) {
                            IntPtr pRender;
                            if (ch.Render.GetBuffer(framesToWrite, out pRender) == 0) {
                                ch.Fifo.Read(pRender, (int)framesToWrite);
                                ch.Render.ReleaseBuffer(framesToWrite, 0);
                                hadActivity = true;
                            }
                        }
                    }
                }

                if (!hadActivity) {
                    Thread.Sleep(1);
                }
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
