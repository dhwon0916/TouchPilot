pub mod paths {
    pub fn data_file(name: &str) -> Option<std::path::PathBuf> {
        let p = std::path::PathBuf::from(std::env::var_os("LOCALAPPDATA")?).join("TouchPilot");
        std::fs::create_dir_all(&p).ok()?;
        Some(p.join(name))
    }
}
pub mod process {
    use windows::{
        core::PWSTR,
        Win32::{
            Foundation::{CloseHandle, HWND},
            System::Threading::{
                OpenProcess, QueryFullProcessImageNameW, PROCESS_NAME_WIN32,
                PROCESS_QUERY_LIMITED_INFORMATION,
            },
            UI::WindowsAndMessaging::GetWindowThreadProcessId,
        },
    };
    pub fn exe_path_from_window(hwnd: HWND) -> Option<String> {
        unsafe {
            let mut pid = 0;
            GetWindowThreadProcessId(hwnd, Some(&mut pid));
            let h = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid).ok()?;
            let mut b = vec![0u16; 32768];
            let mut n = b.len() as u32;
            let ok =
                QueryFullProcessImageNameW(h, PROCESS_NAME_WIN32, PWSTR(b.as_mut_ptr()), &mut n)
                    .is_ok();
            let _ = CloseHandle(h);
            ok.then(|| String::from_utf16_lossy(&b[..n as usize]))
        }
    }
}
