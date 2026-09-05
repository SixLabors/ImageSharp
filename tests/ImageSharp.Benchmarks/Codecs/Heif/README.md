# AV1 sequence encoder comparison

`Av1SequenceEncoderBenchmarks` compares the managed AV1 sequence encoder with an optimized build of official libaom `main`.
The native adapter belongs only to this benchmark project. ImageSharp production code remains fully managed.

## Measurement boundary

Both methods start with the same three `Rgb24` photographic frames and finish with the complete raw AV1 OBU sequence in a reused `MemoryStream`.
The measured operation includes encoder/workspace construction, RGB-to-YUV conversion for every frame, encoding, output writes, flushing where required, and disposal.
Both use the existing ImageSharp SIMD color converter and write directly into the aligned source planes consumed by their encoder.
Libaom receives pinned plane pointers for each synchronous encode call; it does not read a preconverted input file.

Image loading, resizing, half-pixel source translations, validation-file writes, decoding, and quality calculations are outside both measured methods.
MemoryStream capacity is retained between operations for both methods. These are RGB-to-OBU measurements, not public HEIF container-save measurements.
Do not compare them with `aomenc`'s internal encode-time report, which excludes the conversion boundary.

The source is the existing `TestImages.Png.Bike` photograph. Frames one and two are independently translated by half a pixel and one pixel on both axes.
The parameter matrix contains 256x256 and 512x512 frames at ImageSharp efforts seven, eight, and nine.
Input is full-range BT.601, eight-bit 4:2:0; each sequence contains one key frame and two dependent frames.
Both codecs use one coding thread. Libaom uses good-quality mode, no lookahead, disabled automatic key frames, and `cpu-used=6`.
That speed is an independent reference setting, not a mapping from the ImageSharp effort scale.

ImageSharp's base quantizer index is 120. Libaom's public quantizer 30 maps to that same index in the pinned reference.
The native minimum and maximum quantizers are both fixed to 30, in addition to the CQ setting, so frame-level CQ boosts cannot lower the base quantizer.
Other native coding tools remain enabled. Identical quantizers do not imply identical quality or bitrate; always retain size and decoded-quality results with timings.

BenchmarkDotNet's allocation column measures managed allocations only. It does not measure libaom's native allocations, pooled native memory, or total peak memory.
Do not interpret its allocation ratio as a whole-encoder memory comparison.

## Build the reference and benchmark adapter

The verified source revision is `d565eec60f084421fa34fc0534b760c6452b6a6c`, libaom 3.15.0, exported at `D:\GitHub\ynse01\aom-d565eec6-source`.
The x64 Release build is `D:\GitHub\ynse01\aom-d565eec6-build-x64-release`.
It uses MSVC 19.51.36256.0 and NASM 3.02, runtime CPU detection, and SSE2, SSE4.1, AVX2, and AVX512 kernels.
The generic reference decoder build is suitable for conformance but must not be used as the timing reference.

Run from the repository root in an x64 Visual Studio developer PowerShell with the existing CMake, Ninja, NASM, and Perl installations available:

```powershell
$aomSourceDirectory = 'D:/GitHub/ynse01/aom-d565eec6-source'
$aomBuildDirectory = 'D:/GitHub/ynse01/aom-d565eec6-build-x64-release'
$adapterBuildDirectory = 'artifacts/av1-native-benchmark-release'

cmake -S $aomSourceDirectory -B $aomBuildDirectory -G Ninja `
    -DCMAKE_BUILD_TYPE=Release -DAOM_TARGET_CPU=x86_64 -DENABLE_NASM=ON `
    -DENABLE_TESTS=OFF -DENABLE_EXAMPLES=ON -DENABLE_TOOLS=ON `
    -DCONFIG_WEBM_IO=OFF -DCONFIG_LIBYUV=OFF
if ($LASTEXITCODE -ne 0) { throw 'Reference configuration failed.' }

cmake --build $aomBuildDirectory --target aomenc aomdec --parallel 4
if ($LASTEXITCODE -ne 0) { throw 'Reference build failed.' }

cmake -S tests/ImageSharp.Benchmarks/Codecs/Heif/Native -B $adapterBuildDirectory -G Ninja `
    -DCMAKE_BUILD_TYPE=Release "-DAOM_SOURCE_DIRECTORY=$aomSourceDirectory" "-DAOM_BUILD_DIRECTORY=$aomBuildDirectory"
if ($LASTEXITCODE -ne 0) { throw 'Adapter configuration failed.' }

cmake --build $adapterBuildDirectory --parallel 1
if ($LASTEXITCODE -ne 0) { throw 'Adapter build failed.' }

dotnet build tests/ImageSharp.Benchmarks/ImageSharp.Benchmarks.csproj -c Release -f net11.0 `
    --no-restore --disable-build-servers -m:1 -p:UseSharedCompilation=false `
    -p:SIXLABORS_TESTING_PREVIEW=true -p:SIXLABORS_DISABLE_CONFIG_COPY=true
if ($LASTEXITCODE -ne 0) { throw 'Managed build failed.' }

Copy-Item -LiteralPath "$adapterBuildDirectory/imagesharp_aom_benchmark.dll" `
    -Destination artifacts/bin/tests/ImageSharp.Benchmarks/Release/net11.0/imagesharp_aom_benchmark.dll
```

The adapter links the explicit reference build. It neither downloads tools nor adds a native production dependency.
Rebuild it whenever the source headers or reference library change; record the exact source revision and optimized build configuration with every report.

## Validate, then measure

Run one pair first, in the existing benchmark host with the in-process toolchain. Do not run builds or tests concurrently with timing.

```powershell
$benchmarkAssembly = 'artifacts/bin/tests/ImageSharp.Benchmarks/Release/net11.0/ImageSharp.Benchmarks.dll'
$benchmarkFilter = '*Av1SequenceEncoderBenchmarks.*(Dimension: 256, Effort: 7)'

dotnet $benchmarkAssembly --inProcess --job Dry --filter $benchmarkFilter `
    --stopOnFirstError --noOverwrite --artifacts artifacts/BenchmarkDotNet/av1-sequence-rgb-dry
```

The actual last measured payload for each method and the independently exported source planes are retained under
`tests/Images/ActualOutput/Heif/Av1/Av1SequenceEncoderBenchmarks`.
Both timed methods convert RGB on every invocation; the exported source file exists only for offline quality calculation.
Decode both payloads using the matching current-main `aomdec`, and require the expected complete three-frame YUV length:

```powershell
$outputDirectory = 'tests/Images/ActualOutput/Heif/Av1/Av1SequenceEncoderBenchmarks'
foreach ($payloadName in 'bike-256-q120-effort7.obu', 'bike-256-q120-libaom-cpu6.obu') {
    $payloadPath = Join-Path $outputDirectory $payloadName
    & "$aomBuildDirectory/aomdec.exe" --codec=av1 --threads=1 --rawvideo -o "$payloadPath.yuv" $payloadPath
    if ($LASTEXITCODE -ne 0) { throw "Reference decode failed: $payloadName" }

    if ((Get-Item -LiteralPath "$payloadPath.yuv").Length -ne (3 * 256 * 256 * 3 / 2)) {
        throw "Incomplete decoded sequence: $payloadName"
    }
}

dotnet $benchmarkAssembly --inProcess --job Short --filter $benchmarkFilter `
    --stopOnFirstError --noOverwrite --artifacts artifacts/BenchmarkDotNet/av1-sequence-rgb-short
```

Repeat output validation after the measured run. Compute per-plane PSNR against the exported source using
`10 * log10(255^2 * sampleCount / squaredErrorSum)`, accumulated over all three frames.
Aggregate YUV PSNR uses the total squared error and sample count, not the arithmetic mean of the three PSNR values.
Keep bitstream length, quality, absolute time, runtime, CPU information, reference settings, and any environment warnings together.
The short job is an initial diagnostic checkpoint, not a substitute for the complete size/quality/performance matrix or an equal-quality rate-distortion comparison.

## Verified checkpoint: 2026-09-05

The final fixed-quantizer pair ran with BenchmarkDotNet 0.15.8, .NET 11.0.0-preview.7.26381.103, x64 RyuJIT, and AVX512 available.
The in-process Short job used three warmup iterations and three measured iterations. Each operation encoded all three 256x256 frames.

| Encoder setting | Mean sequence time | OBU size | Y PSNR | U PSNR | V PSNR | Aggregate YUV PSNR |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| ImageSharp effort 7, base index 120 | 2,053.31 ms | 11.54 KiB | 39.040 dB | 38.630 dB | 32.778 dB | 37.124 dB |
| Libaom cpu-used 6, fixed public quantizer 30 | 70.67 ms | 8.27 KiB | 39.275 dB | 39.637 dB | 37.351 dB | 38.942 dB |

Both final measured payloads decode to exactly three complete native YUV frames using the optimized current-main reference decoder.
ImageSharp is about 29 times slower for this case while producing a larger, lower-PSNR sequence. The performance exit gate remains open.
The managed allocation counter reports 9.38 MiB per ImageSharp operation; its source still needs attribution, and it is not comparable with libaom's unmeasured native footprint.

Evidence is retained in `artifacts/BenchmarkDotNet/av1-sequence-rgb-fixed-q-short-20260905/20260905-131149`.
The initial Dry run and earlier unrestricted-CQ Short run are preliminary checks, not the final comparison above.
BenchmarkDotNet could not change the power plan or query CPU model information in this environment; those warnings remain in the log.
This result is a local diagnostic, not a controlled-hardware or equal-quality performance claim.

Final OBU SHA-256 values:

- ImageSharp: `AB0327D33405FB7A6EC1895D465C9EE08E0B218DDA9D7E8EE7E0DCA6C5F8A73B`
- Libaom: `0C44A3475849762B5A09937397DEA4397264B83E79DAF930A06B70734977E4A9`

The final Release benchmark build has zero errors and 39 existing benchmark warnings outside the new files.
Roslyn compiler and analyzer diagnostics contain no errors or warnings in the new benchmark files.
No production file changed in this benchmark checkpoint; the preceding 2,524-case encoder/entropy verification remains the latest production test run.
