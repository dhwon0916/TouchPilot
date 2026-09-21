pub mod focus_restore;
pub mod touch;
pub mod touch_policy;
pub mod windows;
pub mod hot_path {
    use std::sync::atomic::{AtomicU64, Ordering};
    pub static EVENTS: AtomicU64 = AtomicU64::new(0);
    pub fn count_event() {
        EVENTS.fetch_add(1, Ordering::Relaxed);
    }
}
