use crossbeam_channel::Sender;

#[cfg(unix)]
use std::os::unix::net::{UnixListener, UnixStream};

pub struct SingleInstanceGuard {
    #[cfg(unix)]
    _listener_thread: Option<std::thread::JoinHandle<()>>,
    #[cfg(unix)]
    socket_path: std::path::PathBuf,
    #[cfg(not(unix))]
    _listener_thread: Option<std::thread::JoinHandle<()>>,
}

#[cfg(unix)]
fn get_socket_path() -> std::path::PathBuf {
    if let Ok(runtime_dir) = std::env::var("XDG_RUNTIME_DIR") {
        let p = std::path::PathBuf::from(runtime_dir);
        if p.is_dir() {
            return p.join("multi-audio-mirror.sock");
        }
    }

    let user = std::env::var("USER")
        .or_else(|_| std::env::var("LOGNAME"))
        .unwrap_or_else(|_| "default".to_string());

    std::env::temp_dir().join(format!("multi-audio-mirror-{user}.sock"))
}

impl SingleInstanceGuard {
    pub fn new(wakeup_tx: Sender<()>) -> Result<Self, ()> {
        #[cfg(unix)]
        {
            let path = get_socket_path();

            let listener = match UnixListener::bind(&path) {
                Ok(l) => l,
                Err(err) if err.kind() == std::io::ErrorKind::AddrInUse => {
                    use std::io::Write;
                    // Probe if the active instance is actually responding
                    match UnixStream::connect(&path) {
                        Ok(mut stream) => {
                            let _ = stream.write_all(b"FOCUS\n");
                            eprintln!("[*] Another instance of Multi Audio Mirror is already running. Focusing window.");
                            return Err(());
                        }
                        Err(_) => {
                            // Dead socket from crash or ungraceful shutdown: clean and re-bind
                            let _ = std::fs::remove_file(&path);
                            match UnixListener::bind(&path) {
                                Ok(l) => l,
                                Err(e) => {
                                    eprintln!("Warning: failed to re-bind single instance socket: {e}");
                                    return Ok(Self {
                                        _listener_thread: None,
                                        socket_path: path,
                                    });
                                }
                            }
                        }
                    }
                }
                Err(e) => {
                    eprintln!("Warning: could not bind single instance socket ({e}). Proceeding.");
                    return Ok(Self {
                        _listener_thread: None,
                        socket_path: path,
                    });
                }
            };

            let listener_clone = listener.try_clone().ok();
            let handle = std::thread::spawn(move || {
                if let Some(l) = listener_clone {
                    for stream in l.incoming() {
                        if let Ok(mut s) = stream {
                            use std::io::Read;
                            let mut buf = [0u8; 16];
                            let _ = s.read(&mut buf);
                            let _ = wakeup_tx.send(());
                        }
                    }
                }
            });

            Ok(Self {
                _listener_thread: Some(handle),
                socket_path: path,
            })
        }

        #[cfg(not(unix))]
        {
            use std::io::{Read, Write};
            use std::net::{TcpListener, TcpStream};

            const PORT: u16 = 48991;
            match TcpListener::bind(("127.0.0.1", PORT)) {
                Ok(listener) => {
                    let listener_clone = listener.try_clone().ok();
                    let handle = std::thread::spawn(move || {
                        if let Some(l) = listener_clone {
                            for stream in l.incoming() {
                                if let Ok(mut s) = stream {
                                    let mut buf = [0u8; 16];
                                    let _ = s.read(&mut buf);
                                    let _ = wakeup_tx.send(());
                                }
                            }
                        }
                    });
                    Ok(Self {
                        _listener_thread: Some(handle),
                    })
                }
                Err(_) => {
                    if let Ok(mut stream) = TcpStream::connect(("127.0.0.1", PORT)) {
                        let _ = stream.write_all(b"FOCUS\n");
                    }
                    eprintln!("[*] Another instance of Multi Audio Mirror is already running. Focusing window.");
                    Err(())
                }
            }
        }
    }
}

#[cfg(unix)]
impl Drop for SingleInstanceGuard {
    fn drop(&mut self) {
        let _ = std::fs::remove_file(&self.socket_path);
    }
}
