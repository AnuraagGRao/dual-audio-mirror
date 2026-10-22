use std::sync::atomic::{AtomicBool, AtomicU32, Ordering};
use std::sync::Arc;
use anyhow::{Context, Result};
use cpal::traits::{DeviceTrait, HostTrait, StreamTrait};
use cpal::{Device, Host, Stream, StreamConfig};
use parking_lot::{Mutex, RwLock};

pub struct AudioDeviceInfo {
    pub name: String,
}

pub struct OutputChannel {
    pub id: String,
    pub device_name: String,
    pub volume: Arc<AtomicU32>,       // Stored as f32 bits
    pub is_muted: Arc<AtomicBool>,
    pub peak_l: Arc<AtomicU32>,        // Stored as f32 bits
    pub peak_r: Arc<AtomicU32>,        // Stored as f32 bits
    sender: Arc<Mutex<Option<crossbeam_channel::Sender<f32>>>>,
    _stream: Option<Stream>,
}

impl OutputChannel {
    pub fn new(device_name: String) -> Self {
        Self {
            id: uuid_simple(),
            device_name,
            volume: Arc::new(AtomicU32::new(1.0f32.to_bits())),
            is_muted: Arc::new(AtomicBool::new(false)),
            peak_l: Arc::new(AtomicU32::new(0)),
            peak_r: Arc::new(AtomicU32::new(0)),
            sender: Arc::new(Mutex::new(None)),
            _stream: None,
        }
    }

    pub fn get_volume(&self) -> f32 {
        f32::from_bits(self.volume.load(Ordering::Relaxed))
    }

    pub fn set_volume(&self, vol: f32) {
        self.volume.store(vol.to_bits(), Ordering::Relaxed);
    }

    pub fn is_muted(&self) -> bool {
        self.is_muted.load(Ordering::Relaxed)
    }

    pub fn set_muted(&self, muted: bool) {
        self.is_muted.store(muted, Ordering::Relaxed);
    }

    pub fn get_peaks(&self) -> (f32, f32) {
        (
            f32::from_bits(self.peak_l.load(Ordering::Relaxed)),
            f32::from_bits(self.peak_r.load(Ordering::Relaxed)),
        )
    }

    pub fn push_samples(&self, samples: &[f32]) {
        let guard = self.sender.lock();
        if let Some(ref tx) = *guard {
            for &s in samples {
                let _ = tx.try_send(s);
            }
        }
    }
}

pub struct AudioEngine {
    host: Host,
    pub selected_input_device: Arc<RwLock<Option<String>>>,
    pub channels: Arc<RwLock<Vec<Arc<OutputChannel>>>>,
    pub peak_master_l: Arc<AtomicU32>,
    pub peak_master_r: Arc<AtomicU32>,
    pub status: Arc<RwLock<String>>,
    _input_stream: Option<Stream>,
}

impl AudioEngine {
    pub fn new() -> Result<Self> {
        let host = cpal::default_host();
        let engine = Self {
            host,
            selected_input_device: Arc::new(RwLock::new(None)),
            channels: Arc::new(RwLock::new(Vec::new())),
            peak_master_l: Arc::new(AtomicU32::new(0)),
            peak_master_r: Arc::new(AtomicU32::new(0)),
            status: Arc::new(RwLock::new("Standby • Zero Lag".to_string())),
            _input_stream: None,
        };

        Ok(engine)
    }

    pub fn get_input_devices(&self) -> Vec<String> {
        let mut names = Vec::new();
        if let Ok(devices) = self.host.input_devices() {
            for dev in devices {
                if let Ok(name) = dev.name() {
                    names.push(name);
                }
            }
        }
        names
    }

    pub fn get_output_devices(&self) -> Vec<String> {
        let mut names = Vec::new();
        if let Ok(devices) = self.host.output_devices() {
            for dev in devices {
                if let Ok(name) = dev.name() {
                    names.push(name);
                }
            }
        }
        names
    }

    pub fn add_channel(&self, device_name: String) -> Arc<OutputChannel> {
        let ch = Arc::new(OutputChannel::new(device_name));
        self.channels.write().push(ch.clone());
        ch
    }

    pub fn remove_channel(&self, channel_id: &str) {
        let mut list = self.channels.write();
        list.retain(|c| c.id != channel_id);
    }

    pub fn get_master_peaks(&self) -> (f32, f32) {
        (
            f32::from_bits(self.peak_master_l.load(Ordering::Relaxed)),
            f32::from_bits(self.peak_master_r.load(Ordering::Relaxed)),
        )
    }

    pub fn start(&mut self) -> Result<()> {
        self.stop();

        // 1. Resolve Input Device
        let input_dev = match self.selected_input_device.read().clone() {
            Some(ref name) => self.find_input_device(name)?,
            None => self
                .host
                .default_input_device()
                .context("No default input device available")?,
        };

        let in_config = input_dev
            .default_input_config()
            .context("Failed to get default input config")?;
        let sample_rate = in_config.sample_rate();
        let channels_in = in_config.channels();

        // 2. Setup Output Channels
        let channel_list = self.channels.read().clone();
        for ch in channel_list.iter() {
            if let Ok(out_dev) = self.find_output_device(&ch.device_name) {
                let out_config = StreamConfig {
                    channels: 2,
                    sample_rate,
                    buffer_size: cpal::BufferSize::Default,
                };

                // Capacity for ~50ms of audio (48000 * 2 * 0.05 = ~4800 floats)
                let buffer_capacity = ((sample_rate.0 as usize) * 2 / 20).max(4096);
                let (tx, rx) = crossbeam_channel::bounded::<f32>(buffer_capacity);
                *ch.sender.lock() = Some(tx);

                let vol_atomic = ch.volume.clone();
                let mute_atomic = ch.is_muted.clone();
                let peak_l_atomic = ch.peak_l.clone();
                let peak_r_atomic = ch.peak_r.clone();

                let out_stream = out_dev.build_output_stream(
                    &out_config,
                    move |data: &mut [f32], _: &cpal::OutputCallbackInfo| {
                        let vol = f32::from_bits(vol_atomic.load(Ordering::Relaxed));
                        let muted = mute_atomic.load(Ordering::Relaxed);
                        let gain = if muted { 0.0 } else { vol };

                        let mut max_l = 0.0f32;
                        let mut max_r = 0.0f32;

                        for frame in data.chunks_exact_mut(2) {
                            let l = rx.try_recv().unwrap_or(0.0) * gain;
                            let r = rx.try_recv().unwrap_or(0.0) * gain;

                            frame[0] = l;
                            frame[1] = r;

                            let abs_l = l.abs();
                            let abs_r = r.abs();
                            if abs_l > max_l {
                                max_l = abs_l;
                            }
                            if abs_r > max_r {
                                max_r = abs_r;
                            }
                        }

                        peak_l_atomic.store(max_l.to_bits(), Ordering::Relaxed);
                        peak_r_atomic.store(max_r.to_bits(), Ordering::Relaxed);
                    },
                    |err| eprintln!("Output stream error: {err}"),
                    None,
                );

                if let Ok(stream) = out_stream {
                    let _ = stream.play();
                }
            }
        }

        // 3. Build Input Capture Stream
        let active_channels = self.channels.clone();
        let peak_m_l = self.peak_master_l.clone();
        let peak_m_r = self.peak_master_r.clone();

        let stream_config = StreamConfig {
            channels: channels_in,
            sample_rate,
            buffer_size: cpal::BufferSize::Default,
        };

        let in_stream = input_dev.build_input_stream(
            &stream_config,
            move |data: &[f32], _: &cpal::InputCallbackInfo| {
                let mut max_l = 0.0f32;
                let mut max_r = 0.0f32;

                let mut stereo_buffer = Vec::with_capacity(data.len() / (channels_in as usize) * 2);

                if channels_in >= 2 {
                    for chunk in data.chunks_exact(channels_in as usize) {
                        let l = chunk[0];
                        let r = chunk[1];
                        stereo_buffer.push(l);
                        stereo_buffer.push(r);

                        let abs_l = l.abs();
                        let abs_r = r.abs();
                        if abs_l > max_l {
                            max_l = abs_l;
                        }
                        if abs_r > max_r {
                            max_r = abs_r;
                        }
                    }
                } else if channels_in == 1 {
                    for &m in data {
                        stereo_buffer.push(m);
                        stereo_buffer.push(m);

                        let abs_m = m.abs();
                        if abs_m > max_l {
                            max_l = abs_m;
                        }
                        if abs_m > max_r {
                            max_r = abs_m;
                        }
                    }
                }

                peak_m_l.store(max_l.to_bits(), Ordering::Relaxed);
                peak_m_r.store(max_r.to_bits(), Ordering::Relaxed);

                let channels = active_channels.read();
                for ch in channels.iter() {
                    ch.push_samples(&stereo_buffer);
                }
            },
            |err| eprintln!("Input stream error: {err}"),
            None,
        )?;

        in_stream.play()?;
        self._input_stream = Some(in_stream);

        let count = self.channels.read().len();
        *self.status.write() = format!("ACTIVE • {} Outputs Synced • {} Hz", count, sample_rate.0);

        Ok(())
    }

    pub fn stop(&mut self) {
        self._input_stream = None;
        let channels = self.channels.read();
        for ch in channels.iter() {
            *ch.sender.lock() = None;
        }
        *self.status.write() = "Standby • Engine Stopped".to_string();
    }

    fn find_input_device(&self, name: &str) -> Result<Device> {
        let devices = self.host.input_devices()?;
        for d in devices {
            if let Ok(n) = d.name() {
                if n == name {
                    return Ok(d);
                }
            }
        }
        anyhow::bail!("Input device '{name}' not found");
    }

    fn find_output_device(&self, name: &str) -> Result<Device> {
        let devices = self.host.output_devices()?;
        for d in devices {
            if let Ok(n) = d.name() {
                if n == name {
                    return Ok(d);
                }
            }
        }
        anyhow::bail!("Output device '{name}' not found");
    }
}

fn uuid_simple() -> String {
    use std::time::{SystemTime, UNIX_EPOCH};
    let d = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default();
    format!("{:x}{:x}", d.as_secs(), d.subsec_nanos())
}
