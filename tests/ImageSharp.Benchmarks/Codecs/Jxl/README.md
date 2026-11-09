# JPEG XL codec comparisons

`DecodeJxl` compares ImageSharp, libjxl through JxlNet, and the original jxl-rs decoder in one BenchmarkDotNet class.
It uses `Config.Short` without changes. Each invocation creates a decoder, allocates its output, decodes the complete image and releases both.
The inputs are stored files. File reading and full-image correctness checks run in `GlobalSetup`, outside measurement.
All methods omit ancillary EXIF/XMP output. ImageSharp uses `SkipMetadata`; color information required to decode pixels remains enabled.

All methods produce unassociated, nonlinear sRGB float RGBA in codestream orientation.
The native methods apply the shared pixel bounds in place to match `RgbaVector`. They do not copy the output into another image.
libjxl uses no parallel runner. jxl-rs uses its sequential runner and high-precision reconstruction.
The benchmark does not change .NET thread settings, JIT settings, environment variables or process affinity.

JxlNet 0.12.0.5 supplies the libjxl bindings and Windows x64/ARM64 binaries.
`GlobalSetup` prints the loaded native version. On Linux, install a matching libjxl library.
The Rust bridge pins revision `d7a831fae15b903f1b56a76668030c19ac07bb6f` and retains the original runtime SIMD dispatch.

Build the Rust bridge once from the repository root. Rust and its native linker must be installed:

```powershell
pwsh -File tests/ImageSharp.Benchmarks/Codecs/Jxl/Native/Build.ps1
```

Then build and run the existing benchmark project in Release on .NET 10:

```powershell
$buildLog = "artifacts/jxl-benchmark-build-$([guid]::NewGuid().ToString('N')).binlog"
dotnet build tests/ImageSharp.Benchmarks/ImageSharp.Benchmarks.csproj -c Release -f net10.0 "-bl:$buildLog"
dotnet run --project tests/ImageSharp.Benchmarks/ImageSharp.Benchmarks.csproj -c Release -f net10.0 --no-build -- --filter '*Codecs.Jxl.DecodeJxl*' --artifacts artifacts
```

`Allocated` reports managed allocations only. The native output and decoder allocations are outside that column.
Every output contains 16 bytes per pixel. That output size is not a measurement of the decoder's total working memory.

`EncodeJxl` compares ImageSharp with libjxl through JxlNet on the same four inputs.
It measures distances zero and one, with progressive output enabled and disabled, through the unchanged `Config.Short`.
Setup decodes each stored input and prepares one unassociated sRGB RGBA8 array. Both encoders read that array.
File I/O, source decoding and pixel preparation run outside measurement. Tests verify encoded pixels separately.

Each measured operation creates the encoder, writes complete output into a pooled stream, and releases both.
Both output streams use the RGBA8 input size as a minimum chunk-size hint, capped by the allocator and the stream's 4 MiB limit.
Chunk allocation remains inside measurement. The native pool can reserve more memory than a requested chunk's logical length.
Use the managed allocation column separately from total or native memory use.
Both encoders preserve transparent colors and use one thread. Native effort is one except for progressive lossless output, which uses effort two.
The effort-one fast lossless path always writes one pass and ignores the native `RESPONSIVE` option.
Effort two selects the modular path that applies this option. Setup prints the native version and the exact settings.

Run the complete encoder matrix after the Release build above:

```powershell
dotnet run --project tests/ImageSharp.Benchmarks/ImageSharp.Benchmarks.csproj -c Release -f net10.0 --no-build -- --filter '*Codecs.Jxl.EncodeJxl*' --artifacts artifacts
```
