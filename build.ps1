$ErrorActionPreference='Stop'
Push-Location $PSScriptRoot
try {
    cargo build --release --manifest-path engine/Cargo.toml
    if($LASTEXITCODE -ne 0){throw 'Input service build failed'}
    dotnet publish app/TouchPilot.csproj -c Release -r win-x64 --self-contained true -o dist/TouchPilot
    if($LASTEXITCODE -ne 0){throw 'App publish failed'}
    Copy-Item engine/target/release/touchpilot-input.exe dist/TouchPilot/TouchPilot.Input.exe
    Copy-Item README.md,LICENSE dist/TouchPilot
    Copy-Item -Recurse -Force licenses dist/TouchPilot
} finally {Pop-Location}
