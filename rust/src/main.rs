mod audio;
mod ui;

use audio::AudioEngine;
use ui::MultiAudioMirrorApp;
use std::sync::Arc;

fn main() -> eframe::Result<()> {
    let engine = match AudioEngine::new() {
        Ok(mut eng) => {
            let out_devs = eng.get_output_devices();
            if let Some(dev1) = out_devs.first() {
                eng.add_channel(dev1.clone());
            }
            if let Some(dev2) = out_devs.get(1) {
                eng.add_channel(dev2.clone());
            }

            let _ = eng.start();
            Arc::new(parking_lot::Mutex::new(eng))
        }
        Err(e) => {
            eprintln!("Failed to initialize audio engine: {e}");
            return Ok(());
        }
    };

    let native_options = eframe::NativeOptions {
        viewport: eframe::egui::ViewportBuilder::default()
            .with_title("Dual & Multi Audio Mirror")
            .with_inner_size([560.0, 780.0])
            .with_min_inner_size([500.0, 560.0])
            .with_active(true),
        ..Default::default()
    };

    eframe::run_native(
        "Dual & Multi Audio Mirror",
        native_options,
        Box::new(|cc| {
            MultiAudioMirrorApp::setup_theme(&cc.egui_ctx);
            Box::new(MultiAudioMirrorApp::new(engine))
        }),
    )
}
