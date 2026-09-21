#![cfg_attr(test, allow(clippy::field_reassign_with_default))]
use std::{
    io::{self, BufRead},
    sync::{mpsc, OnceLock},
};
mod hook;
mod platform;
mod settings;
mod shared;
static COMMANDS: OnceLock<std::sync::Mutex<mpsc::Receiver<settings::Settings>>> = OnceLock::new();
static CLOSED: std::sync::atomic::AtomicBool = std::sync::atomic::AtomicBool::new(false);
fn main() {
    let shared = shared::SHARED.get_or_init(shared::Shared::new);
    let (tx, rx) = mpsc::sync_channel(8);
    COMMANDS.set(std::sync::Mutex::new(rx)).unwrap();
    std::thread::spawn(move || {
        let input = io::stdin();
        let mut input = input.lock();
        loop {
            let mut line = Vec::new();
            match (&mut input).take(65537).read_until(b'\n', &mut line) {
                Ok(0) | Err(_) => break,
                Ok(_) => {
                    if line.len() > 65536 {
                        break;
                    }
                    match serde_json::from_slice::<settings::Settings>(&line) {
                        Ok(s) => {
                            if tx.send(s).is_err() {
                                break;
                            }
                        }
                        Err(e) => eprintln!("Invalid configuration: {e}"),
                    }
                }
            }
        }
        CLOSED.store(true, std::sync::atomic::Ordering::SeqCst);
    });
    hook::windows::run(shared).unwrap_or_else(|e| {
        eprintln!("Input service failed: {e}");
        std::process::exit(1)
    });
}
use std::io::Read;
