# AV1 encoder and decoder comparisons

The 2026-09-05 measurements below are historical results from earlier trees. The takeover source audit has not
accepted encoder or decoder completeness. The first decoder comparison following that audit's corrections ran
on 2026-09-07; its current results and limitations are recorded in the implementation plan.
Encoder comparisons require identical source samples and explicitly reconciled settings, with a maximum
component difference of one unit. Report component maxima and counts exceeding one.
Decoder comparisons for identical bitstreams must be byte exact: maximum error zero and zero differing samples.
Historical Y/U/V maxima of 40/30/53 fail that requirement; their exceedance counts were not recorded.
Agreement between two decoders on the same bitstream is a separate claim from agreement between two encoders.

`Av1SequenceEncoderBenchmarks` compares the managed AV1 sequence encoder with an optimized build of official libaom `main`.
The native adapter is temporary local comparison tooling already present in this project. It is excluded from the
codec deliverable and future checkpoint commits. ImageSharp production code remains fully managed.

`Av1DecoderBenchmarks` compares complete AV1 elementary-stream decoding to `Rgb48` against that same optimized native build.
It includes decoder construction, parsing, reconstruction, output allocation, color conversion, and disposal on both sides.
File reads and correctness checks are setup work. HEIF container parsing is outside this elementary-stream comparison.
Native output planes are borrowed only during synchronous conversion through the existing HEIF converter; no plane copy or per-row interop call is added.
Setup compares every packed RGB sample with ImageSharp and fails before timing on any disagreement.

## Measurement boundary

Both methods start with the same three `Rgb24` photographic frames and finish with the complete raw AV1 OBU sequence in a reused `MemoryStream`.
The measured operation includes encoder/workspace construction, RGB-to-YUV conversion for every frame, encoding, output writes, flushing where required, and disposal.
Both use the existing ImageSharp SIMD color converter and write directly into the aligned source planes consumed by their encoder.
Libaom receives pinned plane pointers for each synchronous encode call; it does not read a preconverted input file.

Image loading, resizing, half-pixel source translations, validation-file writes, decoding, and quality calculations are outside both measured methods.
MemoryStream capacity is retained between operations for both methods. These are RGB-to-OBU measurements, not public HEIF container-save measurements.
Do not compare them with `aomenc`'s internal encode-time report, which excludes the conversion boundary.

The source is the existing `TestImages.Png.Bike` photograph. Frames one and two are independently translated by half a pixel and one pixel on both axes.
Encoder setup also checks all three encoded photographic frames with independent ImageSharp and libaom decoder contexts, including their retained references.
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

## Decoder baseline and sparse-transform checkpoint: 2026-09-05

Run the two existing conformance inputs with the same in-process toolchain:

```powershell
dotnet $benchmarkAssembly --inProcess --job Dry --filter '*Av1DecoderBenchmarks*' `
    --stopOnFirstError --noOverwrite --artifacts artifacts/BenchmarkDotNet/av1-decoder-rgb-dry
dotnet $benchmarkAssembly --inProcess --job Short --filter '*Av1DecoderBenchmarks*' `
    --stopOnFirstError --noOverwrite --artifacts artifacts/BenchmarkDotNet/av1-decoder-rgb-short
```

Both complete `Rgb48` outputs match libaom exactly. Initial warmed measurements were:

| Input | ImageSharp | Libaom |
| --- | ---: | ---: |
| `libavif-kodim23-8b.bit` | 15.130 ms | 5.423 ms |
| `libavif-cosmos1650-10b.bit` | 27.655 ms | 10.246 ms |

The lossy inverse dispatcher previously ignored EOB and transformed every coefficient, including DC-only blocks.
The first sparse path now evaluates the two DC cosine stages once, preserving rectangular normalization, axis rounding, input clamps, and final clipping.
The existing reconstruction output operators handle Vector512, Vector256, Vector128, and scalar stores without an extra sample buffer.
After this change the short measurements were 14.658/5.416 ms for the eight-bit pair and 26.722/9.992 ms for the ten-bit pair.
These small changes are not a statistically established decoder speedup; the approximately 2.7x gap remains open.
The encoder's measured output at this checkpoint remains byte-identical to the baseline hash above.

Evidence: `artifacts/BenchmarkDotNet/av1-decoder-rgb-short-20260905` and `artifacts/BenchmarkDotNet/av1-dc-short-20260905/20260905-134350`.
DC regression cases compare the sparse and full transforms for every size, both destination layouts, signed rounding boundaries, clipping, and untouched row padding.
The final focused screening/DC/native-profile/moving-color set passes 25 cases in each of the normal, AVX512-disabled, AVX-disabled, and all-intrinsics-disabled VSTest processes.
Reports are under `artifacts/TestResults/av1-screen-20260905`.
The matching optimized current-main decoder also reproduces every native Y/U/V sample in all twelve two-frame moving-color streams regenerated by this tree.

## Historical intra screening experiment: 2026-09-05

The current decoder-only comparison after source-led corrections is recorded at the end of this document.

The experimental fixed 8x8 luma search imported libaom's unnormalized Hadamard magnitude and 1.5x threshold
without its full candidate ordering, top-ranked list, neighbor/quantizer policy, or surrounding search controller.
The implementation reuses block scratch and existing vectorized tensor/transpose APIs, retains Int32 precision for twelve-bit residuals, and omits coefficient permutations irrelevant to SATD.
Lossless search is unchanged. Screening for larger blocks, top-ranked candidate pruning, transform bounds, and winner refinement remain open.
The first three-iteration run measured 1,437.53 ms versus 82.34 ms, with considerable run variance.
The managed output is 11.577 KiB at Y/U/V PSNR 38.942/38.613/32.755 dB and aggregate 37.069 dB.
Libaom remains 8.272 KiB at aggregate 38.942 dB. Both decode to three complete frames.
This experiment is not an accepted improvement: aggregate PSNR fell 0.055 dB and the payload grew slightly.
Neither its isolated primitive tests nor these timing results validate the imported pruning policy.
The exact photographic sequence also passes independent packed-RGB decoder comparison before either timed method runs.

A repeat with five warmup iterations and ten measurement iterations gives 1,363.09 ms for ImageSharp and 71.73 ms for libaom.
The payload and quality are unchanged from the screening result above. The native baseline is stable relative to the original run;
the managed reduction is about 34%, but the remaining encoding gap is still approximately 19x.
Evidence: `artifacts/BenchmarkDotNet/av1-screen-verified-short-20260905`.
The final managed OBU SHA-256 is `CCA72021F1AC0BBEA5E69F536589F859B76D2CF293ECD6BA86A644A57B56686E`.

The focused production run passes 84 screening/DC/superblock cases and 165 frame/public encoder cases, with no failures or skips.
Release builds report zero errors, 1,009 existing test warnings, and 39 existing benchmark warnings; the changed source files have no build warnings.
One initial narrower-tier VSTest launch aborted before tests because the inherited environment contained both `Path` and `PATH`.
Subsequent serialized runs use a case-insensitively deduplicated child-process environment. No machine or user environment setting was changed.
The independent Hadamard matrix oracle and zero-allocation test pass at all tested hardware tiers; no golden output was changed to accept a mismatch.

## Decoder source-correction measurement: 2026-09-07

The initial takeover investigation, reconstruction/filter/scratch corrections, and final production verification
preceded this measurement. No encoder timing or separately encoded output comparison was run.
The existing decoder benchmark uses the elementary-stream boundary described above, with one coding thread,
construction and disposal per operation, and independently allocated RGB48 output. Both paths use ImageSharp's
color converter, so this is not an independent color-conversion oracle or a HEIF container-load measurement.

| Input | Encoded size | RGB48 output size | ImageSharp mean | Native mean | ImageSharp managed allocation |
| --- | ---: | ---: | ---: | ---: | ---: |
| Kodak, 768x512, 8-bit 4:2:0 | 20.264 KiB | 2.250 MiB | 13.296 ms | 5.099 ms | 715.97 KiB |
| Cosmos, 1024x428, 10-bit 4:4:4 | 36.298 KiB | 2.508 MiB | 25.419 ms | 9.448 ms | 797.24 KiB |

Setup verifies all 2,494,464 packed RGB48 components exactly: maximum error zero, no differing samples, and
zero errors above one. Fresh optimized native decoding also matches the 1,904,640 retained native plane samples
used by the current conformance tests, with the same zero-error counts. Decoder agreement on these inputs does
not establish complete decoder conformance or separately encoded output parity.

Immediately before the CDF-width correction, the same Short job reported 13.739/5.133 ms for Kodak and
24.848/9.263 ms for Cosmos. Replacing 32-bit CDF threshold storage with its required 16-bit representation
reduced managed allocation by about 217 KiB per decode. These three-iteration timing samples do not establish
a speedup. The current managed/native gap remains approximately 2.61x and 2.69x. The native allocation footprint
is not included in the managed allocation column.

The benchmark uses BenchmarkDotNet 0.15.8, its existing in-process toolchain, .NET 11.0.0-preview.7.26381.103,
x64 RyuJIT x86-64-v4, three warmups and three measured iterations. CPU-model lookup and power-plan changes
were denied by the environment. Current 99.9% error half-widths are 2.800/1.959 ms for Kodak and
26.309/6.052 ms for Cosmos (managed/native); the latter results have substantial variance.

The optimized reference remains `d565eec60f084421fa34fc0534b760c6452b6a6c` with the Release configuration above.
The existing local adapter SHA-256 is `B4FCB9028C15F0F7961A4CEF77FDFF265A11720132F5B23562673E5A65635B23`.
All 9,997 AV1 and selected HEIF production cases pass after the final CDF edit, and fresh comparisons preserve
exact agreement across the established 8,521,435-sample restoration, film-grain, and moving-color corpus.
These are bounded verification results, not codec completion.

Reports remain outside the repository under `D:\GitHub\ynse01\av1-takeover-20260905`:
`decoder-current-short\20260907-011627` before CDF narrowing and `decoder-current-short\20260907-012459`
afterward, with matching Dry jobs and `decoder-benchmark-inputs.json`. No native material was staged or committed.
