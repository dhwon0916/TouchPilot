use serde::Deserialize;
#[derive(Clone, Deserialize)]
#[serde(default)]
pub struct Settings {
    pub enabled: bool,
    pub restore_focus: bool,
    pub stylus_mouse_independent: bool,
    pub focus_restore_delay: i32,
    pub focus_restore_on_mouse_move: bool,
    pub touch_all_displays: bool,
    pub touch_display_bounds: String,
    pub touch_override_modifier: String,
    pub focus_keep_apps: String,
    pub focus_restore_apps: String,
}
impl Default for Settings {
    fn default() -> Self {
        Self {
            enabled: false,
            restore_focus: false,
            stylus_mouse_independent: false,
            focus_restore_delay: 120,
            focus_restore_on_mouse_move: false,
            touch_all_displays: true,
            touch_display_bounds: String::new(),
            touch_override_modifier: String::new(),
            focus_keep_apps: String::new(),
            focus_restore_apps: String::new(),
        }
    }
}
