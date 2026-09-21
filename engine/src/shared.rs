use crate::hook::touch_policy::TouchPolicy;
use std::sync::{
    atomic::{AtomicBool, AtomicU32, Ordering},
    Arc, Mutex, OnceLock,
};
pub static SHARED: OnceLock<Shared> = OnceLock::new();
pub struct Shared {
    pub hooked: AtomicBool,
    pub want_hook: AtomicBool,
    pub paused: AtomicBool,
    pub touch_mouse_independent: AtomicBool,
    pub restore_keyboard_focus: AtomicBool,
    pub touch_generation: AtomicU32,
    pub touch_policy: Mutex<Arc<TouchPolicy>>,
}
impl Shared {
    pub fn new() -> Self {
        Self {
            hooked: AtomicBool::new(false),
            want_hook: AtomicBool::new(true),
            paused: AtomicBool::new(false),
            touch_mouse_independent: AtomicBool::new(false),
            restore_keyboard_focus: AtomicBool::new(false),
            touch_generation: AtomicU32::new(0),
            touch_policy: Mutex::new(Arc::new(TouchPolicy::from_layout(&Default::default()))),
        }
    }
    pub fn touch_active(&self) -> bool {
        self.want_hook.load(Ordering::SeqCst)
            && self.touch_mouse_independent.load(Ordering::SeqCst)
            && !self.paused.load(Ordering::SeqCst)
    }
    pub fn apply(&self, s: crate::settings::Settings) {
        *self.touch_policy.lock().unwrap_or_else(|p| p.into_inner()) =
            Arc::new(TouchPolicy::from_layout(&s));
        self.restore_keyboard_focus
            .store(s.restore_focus, Ordering::SeqCst);
        self.touch_mouse_independent
            .store(s.enabled, Ordering::SeqCst);
        self.touch_generation.fetch_add(1, Ordering::SeqCst);
    }
}
