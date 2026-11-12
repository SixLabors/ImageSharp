<#
.SYNOPSIS
Builds the pinned jxl-rs benchmark bridge and copies its native library into the benchmark asset directory.
.DESCRIPTION
Requires Rust and its native linker. Cargo uses the checked-in lockfile and the exact source revision in Cargo.toml.
The benchmark project copies the resulting library into its output and the BenchmarkDotNet runner's output.
#>

$ErrorActionPreference = 'Stop'
$benchmarkRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$bridgeManifest = Join-Path $PSScriptRoot 'JxlRs/Cargo.toml'
$bridgeTarget = Join-Path $benchmarkRoot 'artifacts/native-build/jxl-rs'

# The Rust host selects the native ABI. These identifiers match the .NET SDK runtime identifier used by the project copy item.
$compilerDetails = rustc -vV
if ($LASTEXITCODE -ne 0) {
    throw 'Rust could not report its host target.'
}

$hostTarget = ($compilerDetails | Where-Object { $_.StartsWith('host: ') }).Substring(6)
switch -Regex ($hostTarget) {
    '^x86_64-pc-windows-' { $runtimeId = 'win-x64'; $libraryName = 'imagesharp_jxl_rs_benchmark.dll'; break }
    '^aarch64-pc-windows-' { $runtimeId = 'win-arm64'; $libraryName = 'imagesharp_jxl_rs_benchmark.dll'; break }
    '^x86_64-unknown-linux-' { $runtimeId = 'linux-x64'; $libraryName = 'libimagesharp_jxl_rs_benchmark.so'; break }
    '^aarch64-unknown-linux-' { $runtimeId = 'linux-arm64'; $libraryName = 'libimagesharp_jxl_rs_benchmark.so'; break }
    '^x86_64-apple-darwin' { $runtimeId = 'osx-x64'; $libraryName = 'libimagesharp_jxl_rs_benchmark.dylib'; break }
    '^aarch64-apple-darwin' { $runtimeId = 'osx-arm64'; $libraryName = 'libimagesharp_jxl_rs_benchmark.dylib'; break }
    default { throw "The JPEG XL benchmark build does not map Rust host '$hostTarget' to a .NET runtime identifier." }
}

# Keep Cargo's intermediates and native binaries outside the source tree. Other codec benchmarks do not require Rust.
cargo build --release --locked --manifest-path $bridgeManifest --target-dir $bridgeTarget
if ($LASTEXITCODE -ne 0) {
    throw 'The jxl-rs benchmark bridge failed to build.'
}

$nativeDestination = Join-Path $benchmarkRoot "artifacts/native/jxl-rs/$runtimeId"
[IO.Directory]::CreateDirectory($nativeDestination) | Out-Null
Copy-Item -LiteralPath (Join-Path $bridgeTarget "release/$libraryName") -Destination $nativeDestination
Write-Output (Join-Path $nativeDestination $libraryName)
