use crate::audio::AudioEngine;
use eframe::egui::{self, Color32, Rounding, Stroke};
use std::sync::Arc;

pub const BG_COLOR: Color32 = Color32::from_rgb(12, 16, 23);
pub const CARD_BG: Color32 = Color32::from_rgb(19, 27, 38);
pub const BORDER_COLOR: Color32 = Color32::from_rgb(30, 41, 59);
pub const CYAN_ACCENT: Color32 = Color32::from_rgb(0, 242, 254);
pub const GREEN_SYNCED: Color32 = Color32::from_rgb(16, 185, 129);
pub const MUTED_TEXT: Color32 = Color32::from_rgb(148, 163, 184);
pub const CRIMSON_ALERT: Color32 = Color32::from_rgb(239, 68, 68);
pub const CRIMSON_BG: Color32 = Color32::from_rgb(185, 28, 28);
pub const BUTTON_BG: Color32 = Color32::from_rgb(30, 41, 59);
pub const BLUE_ADD: Color32 = Color32::from_rgb(2, 132, 199);

pub struct MultiAudioMirrorApp {
    pub engine: Arc<parking_lot::Mutex<AudioEngine>>,
    input_devices: Vec<String>,
    output_devices: Vec<String>,
    selected_input: Option<String>,
}

impl MultiAudioMirrorApp {
    pub fn new(engine: Arc<parking_lot::Mutex<AudioEngine>>) -> Self {
        let (in_devs, out_devs, sel_in) = {
            let eng = engine.lock();
            let in_devs = eng.get_input_devices();
            let out_devs = eng.get_output_devices();
            let sel_in = eng.selected_input_device.read().clone();
            (in_devs, out_devs, sel_in)
        };

        Self {
            engine,
            input_devices: in_devs,
            output_devices: out_devs,
            selected_input: sel_in,
        }
    }

    pub fn setup_theme(ctx: &egui::Context) {
        let mut visuals = egui::Visuals::dark();
        visuals.panel_fill = BG_COLOR;
        visuals.window_fill = BG_COLOR;
        visuals.override_text_color = Some(Color32::from_rgb(241, 245, 249));
        visuals.widgets.inactive.bg_fill = BUTTON_BG;
        visuals.widgets.inactive.rounding = Rounding::same(6.0);
        visuals.widgets.hovered.bg_fill = Color32::from_rgb(51, 65, 85);
        visuals.widgets.hovered.rounding = Rounding::same(6.0);
        visuals.widgets.active.bg_fill = CYAN_ACCENT;
        visuals.widgets.active.rounding = Rounding::same(6.0);
        ctx.set_visuals(visuals);
    }
}

impl eframe::App for MultiAudioMirrorApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        ctx.request_repaint();

        egui::CentralPanel::default().show(ctx, |ui| {
            ui.spacing_mut().item_spacing = egui::vec2(0.0, 10.0);

            // =========================================================
            // 1. Header Bar
            // =========================================================
            egui::Frame::none()
                .fill(CARD_BG)
                .rounding(Rounding::same(12.0))
                .stroke(Stroke::new(1.0_f32, BORDER_COLOR))
                .inner_margin(egui::Margin::symmetric(16.0, 12.0))
                .show(ui, |ui| {
                    ui.horizontal(|ui| {
                        let (rect, _) = ui.allocate_exact_size(egui::vec2(8.0, 24.0), egui::Sense::hover());
                        ui.painter().rect_filled(rect, 4.0, CYAN_ACCENT);

                        ui.add_space(10.0);

                        ui.vertical(|ui| {
                            ui.label(
                                egui::RichText::new("DUAL & MULTI AUDIO MIRROR")
                                    .font(egui::FontId::proportional(15.0))
                                    .strong()
                                    .color(Color32::WHITE),
                            );

                            let status_text = {
                                let eng = self.engine.lock();
                                eng.get_status()
                            };

                            ui.label(
                                egui::RichText::new(status_text)
                                    .font(egui::FontId::proportional(10.5))
                                    .color(GREEN_SYNCED),
                            );
                        });
                    });
                });

            // =========================================================
            // 2. Master Source Card
            // =========================================================
            egui::Frame::none()
                .fill(CARD_BG)
                .rounding(Rounding::same(12.0))
                .stroke(Stroke::new(1.0_f32, BORDER_COLOR))
                .inner_margin(egui::Margin::same(16.0))
                .show(ui, |ui| {
                    ui.horizontal(|ui| {
                        ui.label(
                            egui::RichText::new("INPUT SOURCE")
                                .font(egui::FontId::proportional(11.0))
                                .strong()
                                .color(MUTED_TEXT),
                        );
                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                            ui.label(
                                egui::RichText::new("48 kHz • Stereo")
                                    .font(egui::FontId::proportional(11.0))
                                    .color(CYAN_ACCENT),
                            );
                        });
                    });

                    ui.add_space(4.0);

                    let mut changed_input = false;
                    let current_in = self.selected_input.clone().unwrap_or_else(|| "Default Input Device".to_string());

                    egui::ComboBox::from_id_source("combo_input")
                        .width(ui.available_width())
                        .selected_text(&current_in)
                        .show_ui(ui, |ui| {
                            for dev in &self.input_devices {
                                if ui.selectable_value(&mut self.selected_input, Some(dev.clone()), dev).clicked() {
                                    changed_input = true;
                                }
                            }
                        });

                    if changed_input {
                        let mut eng = self.engine.lock();
                        *eng.selected_input_device.write() = self.selected_input.clone();
                        let _ = eng.start();
                    }

                    ui.add_space(10.0);
                    ui.label(
                        egui::RichText::new("MASTER LEVEL")
                            .font(egui::FontId::proportional(10.0))
                            .strong()
                            .color(Color32::from_rgb(100, 116, 139)),
                    );

                    let (peak_m_l, peak_m_r) = {
                        let eng = self.engine.lock();
                        eng.get_master_peaks()
                    };

                    draw_stereo_meter(ui, peak_m_l, peak_m_r, CYAN_ACCENT);
                });

            // =========================================================
            // 3. Dynamic Output Channels Section
            // =========================================================
            ui.horizontal(|ui| {
                ui.label(
                    egui::RichText::new("OUTPUT CHANNELS")
                        .font(egui::FontId::proportional(11.0))
                        .strong()
                        .color(MUTED_TEXT),
                );

                egui::Frame::none()
                    .fill(Color32::from_rgb(30, 41, 59))
                    .rounding(Rounding::same(4.0))
                    .inner_margin(egui::Margin::symmetric(6.0, 2.0))
                    .show(ui, |ui| {
                        ui.label(
                            egui::RichText::new("MULTI-SYNC")
                                .font(egui::FontId::proportional(9.0))
                                .strong()
                                .color(CYAN_ACCENT),
                        );
                    });

                ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                    let btn_add = egui::Button::new(
                        egui::RichText::new("+ Add Output")
                            .font(egui::FontId::proportional(11.0))
                            .strong()
                            .color(Color32::WHITE),
                    )
                    .fill(BLUE_ADD)
                    .rounding(Rounding::same(6.0));

                    if ui.add(btn_add).clicked() {
                        let next_dev = self.output_devices.first().cloned().unwrap_or_else(|| "Default Output".to_string());
                        let mut eng = self.engine.lock();
                        eng.add_channel(next_dev);
                        let _ = eng.start();
                    }
                });
            });

            // Scrollable Output Channels List
            egui::ScrollArea::vertical()
                .auto_shrink([false; 2])
                .show(ui, |ui| {
                    let channels = {
                        let eng = self.engine.lock();
                        eng.get_channels_snapshot()
                    };

                    let total_count = channels.len();
                    let mut remove_id: Option<String> = None;
                    let mut need_restart = false;

                    for (idx, ch) in channels.iter().enumerate() {
                        egui::Frame::none()
                            .fill(CARD_BG)
                            .rounding(Rounding::same(12.0))
                            .stroke(Stroke::new(1.0_f32, BORDER_COLOR))
                            .inner_margin(egui::Margin::same(16.0))
                            .show(ui, |ui| {
                                ui.horizontal(|ui| {
                                    ui.label(
                                        egui::RichText::new(format!("OUTPUT {}", idx + 1))
                                            .font(egui::FontId::proportional(12.0))
                                            .strong()
                                            .color(Color32::WHITE),
                                    );

                                    egui::Frame::none()
                                        .fill(Color32::from_rgb(6, 78, 59))
                                        .rounding(Rounding::same(4.0))
                                        .inner_margin(egui::Margin::symmetric(6.0, 2.0))
                                        .show(ui, |ui| {
                                            ui.label(
                                                egui::RichText::new("SYNCED")
                                                    .font(egui::FontId::proportional(9.0))
                                                    .strong()
                                                    .color(GREEN_SYNCED),
                                            );
                                        });

                                    if total_count > 1 {
                                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                                            let btn_remove = egui::Button::new(
                                                egui::RichText::new("✕ Remove")
                                                    .font(egui::FontId::proportional(10.0))
                                                    .strong()
                                                    .color(CRIMSON_ALERT),
                                            )
                                            .fill(BUTTON_BG)
                                            .rounding(Rounding::same(4.0));

                                            if ui.add(btn_remove).clicked() {
                                                remove_id = Some(ch.id.clone());
                                            }
                                        });
                                    }
                                });

                                ui.add_space(4.0);

                                let mut current_device = ch.device_name.clone();
                                egui::ComboBox::from_id_source(format!("combo_out_{}", ch.id))
                                    .width(ui.available_width())
                                    .selected_text(&current_device)
                                    .show_ui(ui, |ui| {
                                        for out_d in &self.output_devices {
                                            if ui.selectable_value(&mut current_device, out_d.clone(), out_d).clicked() {
                                                need_restart = true;
                                            }
                                        }
                                    });

                                ui.add_space(6.0);

                                let mut vol = (ch.get_volume() * 100.0).round();
                                ui.horizontal(|ui| {
                                    ui.label(
                                        egui::RichText::new("Volume")
                                            .font(egui::FontId::proportional(11.0))
                                            .color(MUTED_TEXT),
                                    );
                                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                                        ui.label(
                                            egui::RichText::new(format!("{}%", vol as i32))
                                                .font(egui::FontId::proportional(11.0))
                                                .strong()
                                                .color(CYAN_ACCENT),
                                        );
                                    });
                                });

                                let slider = egui::Slider::new(&mut vol, 0.0..=125.0).show_value(false);
                                if ui.add(slider).changed() {
                                    ch.set_volume(vol / 100.0);
                                }

                                ui.add_space(6.0);

                                ui.horizontal(|ui| {
                                    let (p_l, p_r) = ch.get_peaks();

                                    ui.vertical(|ui| {
                                        draw_stereo_meter(ui, p_l, p_r, GREEN_SYNCED);
                                    });

                                    ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                                        let is_muted = ch.is_muted();
                                        let mute_btn_text = if is_muted { "MUTED" } else { "Mute" };
                                        let mute_bg = if is_muted { CRIMSON_BG } else { BUTTON_BG };

                                        let btn_mute = egui::Button::new(
                                            egui::RichText::new(mute_btn_text)
                                                .font(egui::FontId::proportional(11.0))
                                                .strong()
                                                .color(Color32::WHITE),
                                        )
                                        .min_size(egui::vec2(64.0, 28.0))
                                        .fill(mute_bg)
                                        .rounding(Rounding::same(6.0));

                                        if ui.add(btn_mute).clicked() {
                                            ch.set_muted(!is_muted);
                                        }
                                    });
                                });
                            });

                        ui.add_space(6.0);
                    }

                    if let Some(id) = remove_id {
                        let mut eng = self.engine.lock();
                        eng.remove_channel(&id);
                        let _ = eng.start();
                    } else if need_restart {
                        let mut eng = self.engine.lock();
                        let _ = eng.start();
                    }
                });

            // =========================================================
            // 4. Footer
            // =========================================================
            egui::Frame::none()
                .fill(CARD_BG)
                .rounding(Rounding::same(10.0))
                .stroke(Stroke::new(1.0_f32, BORDER_COLOR))
                .inner_margin(egui::Margin::symmetric(14.0, 10.0))
                .show(ui, |ui| {
                    ui.horizontal(|ui| {
                        ui.label(
                            egui::RichText::new("Pure Rust (cpal + egui) • Zero Bloat")
                                .font(egui::FontId::proportional(10.0))
                                .color(Color32::from_rgb(100, 116, 139)),
                        );

                        ui.with_layout(egui::Layout::right_to_left(egui::Align::Center), |ui| {
                            ui.label(
                                egui::RichText::new("v1.2.0 Multi-Endpoint")
                                    .font(egui::FontId::proportional(10.0))
                                    .color(CYAN_ACCENT),
                            );
                        });
                    });
                });
        });
    }
}

fn draw_stereo_meter(ui: &mut egui::Ui, peak_l: f32, peak_r: f32, fill_color: Color32) {
    let width = ui.available_width() - 80.0;
    let bar_height = 6.0;

    let (rect_l, _) = ui.allocate_exact_size(egui::vec2(width.max(100.0), bar_height), egui::Sense::hover());
    ui.painter().rect_filled(rect_l, 3.0, Color32::from_rgb(12, 16, 23));
    let fill_w_l = (peak_l.clamp(0.0, 1.0) * width).max(0.0);
    let mut active_rect_l = rect_l;
    active_rect_l.set_width(fill_w_l);
    ui.painter().rect_filled(active_rect_l, 3.0, fill_color);

    ui.add_space(3.0);

    let (rect_r, _) = ui.allocate_exact_size(egui::vec2(width.max(100.0), bar_height), egui::Sense::hover());
    ui.painter().rect_filled(rect_r, 3.0, Color32::from_rgb(12, 16, 23));
    let fill_w_r = (peak_r.clamp(0.0, 1.0) * width).max(0.0);
    let mut active_rect_r = rect_r;
    active_rect_r.set_width(fill_w_r);
    ui.painter().rect_filled(active_rect_r, 3.0, fill_color);
}
