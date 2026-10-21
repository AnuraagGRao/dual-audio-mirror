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
        public string Render1DeviceId { get; set; }
        public string Render2DeviceId { get; set; }

        // Gains (0.0 to 1.5)
        public float Volume1 { get; set; }
        public bool IsMuted1 { get; set; }
        public float Volume2 { get; set; }
        public bool IsMuted2 { get; set; }

        // Live visualizer levels (0.0 to 1.0)
        public float PeakMasterL { get; private set; }
        public float PeakMasterR { get; private set; }
        public float PeakOut1L { get; private set; }
        public float PeakOut1R { get; private set; }
        public float PeakOut2L { get; private set; }
        public float PeakOut2R { get; private set; }

        public bool IsActive {
            get { return _isRunning; }
        }

        public event Action<string> OnStatusChanged;

        public AudioEngine() {
            Volume1 = 1.0f;
            Volume2 = 1.0f;
            IsMuted1 = false;
            IsMuted2 = false;
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
            IMMDevice devCap = null, devR1 = null, devR2 = null;

            // Resolve Devices
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

            if (!string.IsNullOrEmpty(Render1DeviceId)) {
                enumerator.GetDevice(Render1DeviceId, out devR1);
            }
            if (devR1 == null) {
                var all = GetPlaybackDevices();
                var p = all.Find(d => d.Name.Contains("PRO X"));
                if (p != null) enumerator.GetDevice(p.Id, out devR1);
            }

            if (!string.IsNullOrEmpty(Render2DeviceId)) {
                enumerator.GetDevice(Render2DeviceId, out devR2);
            }
            if (devR2 == null) {
                var all = GetPlaybackDevices();
                var g = all.Find(d => d.Name.Contains("G435"));
                if (g != null) enumerator.GetDevice(g.Id, out devR2);
            }

            if (devCap == null) {
                TriggerStatus("Waiting for Capture Device...");
                Thread.Sleep(500);
                return;
            }

            Guid sessionGuid = Guid.Empty;
            object oCap, oR1 = null, oR2 = null;

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

            string devCapId = "", devR1Id = "", devR2Id = "";
            if (devCap != null) devCap.GetId(out devCapId);
            if (devR1 != null) devR1.GetId(out devR1Id);
            if (devR2 != null) devR2.GetId(out devR2Id);

            bool isR1Target = devR1 != null && !string.Equals(devR1Id, devCapId, StringComparison.OrdinalIgnoreCase);
            bool isR2Target = devR2 != null && !string.Equals(devR2Id, devCapId, StringComparison.OrdinalIgnoreCase);

            IAudioClient cR1 = null, cR2 = null;
            IAudioRenderClient ren1 = null, ren2 = null;
            uint bufSizeR1 = 0, bufSizeR2 = 0;

            if (isR1Target) {
                try {
                    devR1.Activate(ref IID_IAudioClient, 1, IntPtr.Zero, out oR1);
                    cR1 = (IAudioClient)oR1;
                    if (cR1.Initialize(0, 0, hnsBufferDuration, 0, pFmt, ref sessionGuid) == 0) {
                        IntPtr pR1Service;
                        cR1.GetService(ref IID_IAudioRenderClient, out pR1Service);
                        ren1 = (IAudioRenderClient)Marshal.GetObjectForIUnknown(pR1Service);
                        cR1.GetBufferSize(out bufSizeR1);
                        cR1.Start();
                    }
                } catch { cR1 = null; ren1 = null; }
            }

            if (isR2Target) {
                try {
                    devR2.Activate(ref IID_IAudioClient, 1, IntPtr.Zero, out oR2);
                    cR2 = (IAudioClient)oR2;
                    if (cR2.Initialize(0, 0, hnsBufferDuration, 0, pFmt, ref sessionGuid) == 0) {
                        IntPtr pR2Service;
                        cR2.GetService(ref IID_IAudioRenderClient, out pR2Service);
                        ren2 = (IAudioRenderClient)Marshal.GetObjectForIUnknown(pR2Service);
                        cR2.GetBufferSize(out bufSizeR2);
                        cR2.Start();
                    }
                } catch { cR2 = null; ren2 = null; }
            }

            cCap.Start();
            TriggerStatus("Active • 48.0 kHz • Zero Lag");

            float[] floatBuffer = new float[16384];
            float[] processBuffer1 = new float[16384];
            float[] processBuffer2 = new float[16384];

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
                            processBuffer1 = new float[numFloats * 2];
                            processBuffer2 = new float[numFloats * 2];
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

                        // Process Output 1 (PRO X)
                        if (ren1 != null && cR1 != null) {
                            float g1 = IsMuted1 ? 0f : Volume1;
                            for (int i = 0; i < numFloats; i++) {
                                processBuffer1[i] = floatBuffer[i] * g1;
                            }
                            PeakOut1L = maxL * g1;
                            PeakOut1R = maxR * g1;

                            uint pad1;
                            cR1.GetCurrentPadding(out pad1);
                            if (bufSizeR1 - pad1 >= numFrames) {
                                IntPtr pRender1;
                                if (ren1.GetBuffer(numFrames, out pRender1) == 0) {
                                    Marshal.Copy(processBuffer1, 0, pRender1, numFloats);
                                    ren1.ReleaseBuffer(numFrames, 0);
                                }
                            }
                        }

                        // Process Output 2 (G435)
                        if (ren2 != null && cR2 != null) {
                            float g2 = IsMuted2 ? 0f : Volume2;
                            for (int i = 0; i < numFloats; i++) {
                                processBuffer2[i] = floatBuffer[i] * g2;
                            }
                            PeakOut2L = maxL * g2;
                            PeakOut2R = maxR * g2;

                            uint pad2;
                            cR2.GetCurrentPadding(out pad2);
                            if (bufSizeR2 - pad2 >= numFrames) {
                                IntPtr pRender2;
                                if (ren2.GetBuffer(numFrames, out pRender2) == 0) {
                                    Marshal.Copy(processBuffer2, 0, pRender2, numFloats);
                                    ren2.ReleaseBuffer(numFrames, 0);
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
            try { if (cR1 != null) cR1.Stop(); } catch { }
            try { if (cR2 != null) cR2.Stop(); } catch { }
        }

        public void Dispose() {
            Stop();
        }
    }
}
