pub mod focus_restore;
pub mod native_touch;
pub mod touch_contacts;
pub mod touch_input;
pub mod mouse {
    pub fn reset_movement() {}
}
use std::cell::Cell;
use std::sync::atomic::Ordering;
use windows::{
    core::w,
    Win32::{
        Foundation::{HINSTANCE, HWND, LPARAM, LRESULT, POINT, WPARAM},
        System::LibraryLoader::GetModuleHandleW,
        UI::{
            HiDpi::{SetProcessDpiAwarenessContext, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2},
            WindowsAndMessaging::*,
        },
    },
};
type HookHealth = (u64, Option<(i32, i32)>, u8);
thread_local! {
 static HOOK:Cell<HHOOK>=const{Cell::new(HHOOK(std::ptr::null_mut()))};
 static WARPING:Cell<bool>=const{Cell::new(false)};
 static HEALTH:Cell<HookHealth>=const{Cell::new((0,None,0))};
}
fn check_hook() {
    let mut p = POINT::default();
    if unsafe { GetCursorPos(&mut p) }.is_err() {
        touch_input::reset();
        return;
    }
    let count = crate::hook::hot_path::EVENTS.load(Ordering::Relaxed);
    let rehook = HEALTH.with(|state| {
        let (old, point, misses) = state.get();
        let misses = if old == count && point.is_some_and(|v| v != (p.x, p.y)) {
            misses.saturating_add(1)
        } else {
            0
        };
        state.set((count, Some((p.x, p.y)), misses));
        misses >= 2
    });
    if rehook {
        if let Err(e) = install() {
            eprintln!("Could not restore input hook: {e}");
        }
    }
}
pub fn restore_position(x: i32, y: i32) -> Option<(i32, i32)> {
    if WARPING.with(|v| v.replace(true)) {
        return None;
    }
    let result = unsafe { SetCursorPos(x, y) };
    let mut p = POINT::default();
    let read = unsafe { GetCursorPos(&mut p) };
    WARPING.with(|v| v.set(false));
    (result.is_ok() && read.is_ok()).then_some((p.x, p.y))
}
unsafe extern "system" fn mouse_proc(code: i32, w: WPARAM, l: LPARAM) -> LRESULT {
    let consumed = std::panic::catch_unwind(|| {
        if code < 0 || WARPING.with(Cell::get) {
            return false;
        }
        crate::hook::hot_path::count_event();
        let ms = unsafe { &*(l.0 as *const MSLLHOOKSTRUCT) };
        match touch_input::process(w.0 as u32, ms) {
            Some(v) => v,
            None => {
                touch_input::record_mouse_position((ms.pt.x, ms.pt.y), false);
                false
            }
        }
    })
    .unwrap_or(false);
    if consumed {
        LRESULT(1)
    } else {
        unsafe { CallNextHookEx(None, code, w, l) }
    }
}
fn install() -> windows::core::Result<()> {
    unsafe {
        let h = SetWindowsHookExW(WH_MOUSE_LL, Some(mouse_proc), GetModuleHandleW(None)?, 0)?;
        HOOK.with(|v| {
            let old = v.replace(h);
            if !old.is_invalid() {
                let _ = UnhookWindowsHookEx(old);
            }
        });
    }
    touch_input::reset();
    Ok(())
}
unsafe extern "system" fn wnd_proc(hwnd: HWND, msg: u32, w: WPARAM, l: LPARAM) -> LRESULT {
    let _ = std::panic::catch_unwind(|| match msg {
        WM_INPUT => native_touch::on_input(hwnd, l),
        WM_INPUT_DEVICE_CHANGE => native_touch::devices_changed(),
        WM_TIMER if w.0 == native_touch::RELEASE_TIMER => native_touch::on_timer(hwnd),
        WM_TIMER if w.0 == 2 => check_hook(),
        WM_TIMER if w.0 == 1 => {
            if crate::CLOSED.load(Ordering::SeqCst) {
                unsafe { PostQuitMessage(0) };
                return;
            }
            if let Some(rx) = crate::COMMANDS.get() {
                let mut latest = None;
                for s in rx.lock().unwrap().try_iter() {
                    latest = Some(s)
                }
                if let Some(s) = latest {
                    crate::shared::SHARED.get().unwrap().apply(s);
                    touch_input::reset();
                }
            }
        }
        WM_DISPLAYCHANGE | WM_POWERBROADCAST => {
            touch_input::reset();
            native_touch::devices_changed();
        }
        WM_DESTROY => unsafe { PostQuitMessage(0) },
        _ => {}
    });
    unsafe { DefWindowProcW(hwnd, msg, w, l) }
}
pub fn run(shared: &'static crate::shared::Shared) -> windows::core::Result<()> {
    unsafe {
        let _ = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
        let instance = HINSTANCE(GetModuleHandleW(None)?.0);
        let class = WNDCLASSW {
            lpfnWndProc: Some(wnd_proc),
            hInstance: instance,
            lpszClassName: w!("TouchPilot.Input"),
            ..Default::default()
        };
        if RegisterClassW(&class) == 0 {
            return Err(windows::core::Error::from_win32());
        }
        let hwnd = CreateWindowExW(
            WINDOW_EX_STYLE(0),
            w!("TouchPilot.Input"),
            w!("TouchPilot.Input"),
            WS_OVERLAPPEDWINDOW,
            0,
            0,
            0,
            0,
            None,
            None,
            instance,
            None,
        )?;
        install()?;
        shared.hooked.store(true, Ordering::SeqCst);
        native_touch::register(hwnd);
        focus_restore::start(shared);
        if SetTimer(hwnd, 1, 100, None) == 0 {
            return Err(windows::core::Error::from_win32());
        }
        SetTimer(hwnd, 2, 1000, None);
        println!("READY");
        use std::io::Write;
        let _ = std::io::stdout().flush();
        let mut msg = MSG::default();
        loop {
            let result = GetMessageW(&mut msg, None, 0, 0);
            if result.0 <= 0 {
                break;
            }
            let _ = TranslateMessage(&msg);
            DispatchMessageW(&msg);
        }
        shared.hooked.store(false, Ordering::SeqCst);
        focus_restore::reset();
        native_touch::unregister();
        HOOK.with(|v| {
            let _ = UnhookWindowsHookEx(v.get());
        });
        let _ = DestroyWindow(hwnd);
        Ok(())
    }
}
