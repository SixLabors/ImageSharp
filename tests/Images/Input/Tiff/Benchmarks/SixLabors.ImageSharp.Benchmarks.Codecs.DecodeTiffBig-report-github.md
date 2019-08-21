``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19041.450 (2004/?/20H1)
Intel Core i7-3610QM CPU 2.30GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=3.1.401
  [Host]     : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT
  Job-MTZTUC : .NET Framework 4.8 (4.8.4200.0), X64 RyuJIT
  Job-BGVYTJ : .NET Core 2.1.21 (CoreCLR 4.6.29130.01, CoreFX 4.6.29130.02), X64 RyuJIT
  Job-ZDUDFU : .NET Core 3.1.7 (CoreCLR 4.700.20.36602, CoreFX 4.700.20.37001), X64 RyuJIT

InvocationCount=1  IterationCount=5  LaunchCount=1  
UnrollFactor=1  WarmupCount=3  

```
|                Method |        Job |       Runtime |                                               TestImage |        Mean |       Error |      StdDev |  Ratio | RatioSD |       Gen 0 |     Gen 1 |     Gen 2 |    Allocated |
|---------------------- |----------- |-------------- |-------------------------------------------------------- |------------:|------------:|------------:|-------:|--------:|------------:|----------:|----------:|-------------:|
| **&#39;System.Drawing Tiff&#39;** | **Job-MTZTUC** |    **.NET 4.7.2** | **Tiff/Benchmarks/jpeg444_big_grayscale_uncompressed.tiff** |    **180.2 ms** |    **15.21 ms** |     **2.35 ms** |   **1.00** |    **0.00** |  **85000.0000** |         **-** |         **-** |  **269221840 B** |
|     &#39;ImageSharp Tiff&#39; | Job-MTZTUC |    .NET 4.7.2 | Tiff/Benchmarks/jpeg444_big_grayscale_uncompressed.tiff | 31,527.8 ms | 4,371.70 ms | 1,135.32 ms | 176.11 |    8.81 |   1000.0000 | 1000.0000 | 1000.0000 | 1342029912 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 | Tiff/Benchmarks/jpeg444_big_grayscale_uncompressed.tiff |    185.5 ms |    15.88 ms |     2.46 ms |   1.00 |    0.00 |  85000.0000 |         - |         - |  268813936 B |
|     &#39;ImageSharp Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 | Tiff/Benchmarks/jpeg444_big_grayscale_uncompressed.tiff | 17,768.7 ms |   116.03 ms |    30.13 ms |  95.84 |    1.13 |   1000.0000 | 1000.0000 | 1000.0000 | 1342016464 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 | Tiff/Benchmarks/jpeg444_big_grayscale_uncompressed.tiff |    149.9 ms |     8.23 ms |     1.27 ms |   1.00 |    0.00 |           - |         - |         - |        176 B |
|     &#39;ImageSharp Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 | Tiff/Benchmarks/jpeg444_big_grayscale_uncompressed.tiff | 16,782.2 ms |   718.14 ms |   111.13 ms | 111.94 |    0.80 |   1000.0000 | 1000.0000 | 1000.0000 | 1342016440 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| **&#39;System.Drawing Tiff&#39;** | **Job-MTZTUC** |    **.NET 4.7.2** |   **Tiff/Benchmarks/jpeg444_big_palette_uncompressed.tiff** |    **178.0 ms** |     **7.07 ms** |     **1.83 ms** |   **1.00** |    **0.00** |  **85000.0000** |         **-** |         **-** |  **269221840 B** |
|     &#39;ImageSharp Tiff&#39; | Job-MTZTUC |    .NET 4.7.2 |   Tiff/Benchmarks/jpeg444_big_palette_uncompressed.tiff | 33,721.9 ms |    78.03 ms |    12.08 ms | 188.96 |    1.80 |   1000.0000 | 1000.0000 | 1000.0000 | 1342023280 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |   Tiff/Benchmarks/jpeg444_big_palette_uncompressed.tiff |    180.1 ms |     8.81 ms |     2.29 ms |   1.00 |    0.00 |  85000.0000 |         - |         - |  268815616 B |
|     &#39;ImageSharp Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |   Tiff/Benchmarks/jpeg444_big_palette_uncompressed.tiff | 22,941.4 ms |   728.12 ms |   189.09 ms | 127.37 |    1.07 |   1000.0000 | 1000.0000 | 1000.0000 | 1342022368 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |   Tiff/Benchmarks/jpeg444_big_palette_uncompressed.tiff |    145.5 ms |     3.20 ms |     0.50 ms |   1.00 |    0.00 |           - |         - |         - |        176 B |
|     &#39;ImageSharp Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |   Tiff/Benchmarks/jpeg444_big_palette_uncompressed.tiff | 21,485.0 ms |   711.10 ms |   184.67 ms | 148.04 |    0.66 |   1000.0000 | 1000.0000 | 1000.0000 | 1342025632 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| **&#39;System.Drawing Tiff&#39;** | **Job-MTZTUC** |    **.NET 4.7.2** |            **Tiff/Benchmarks/jpeg444_big_rgb_deflate.tiff** |  **2,518.2 ms** |    **76.22 ms** |    **19.79 ms** |   **1.00** |    **0.00** |   **6000.0000** |         **-** |         **-** |   **29598616 B** |
|     &#39;ImageSharp Tiff&#39; | Job-MTZTUC |    .NET 4.7.2 |            Tiff/Benchmarks/jpeg444_big_rgb_deflate.tiff | 29,327.2 ms |   102.72 ms |    26.68 ms |  11.65 |    0.10 |   1000.0000 | 1000.0000 | 1000.0000 | 1124088224 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |            Tiff/Benchmarks/jpeg444_big_rgb_deflate.tiff |  2,500.3 ms |    67.24 ms |    10.41 ms |   1.00 |    0.00 |   6000.0000 |         - |         - |   29528752 B |
|     &#39;ImageSharp Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |            Tiff/Benchmarks/jpeg444_big_rgb_deflate.tiff | 18,974.7 ms |   199.58 ms |    30.89 ms |   7.59 |    0.04 |   1000.0000 | 1000.0000 | 1000.0000 | 1123947608 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |            Tiff/Benchmarks/jpeg444_big_rgb_deflate.tiff |  2,541.1 ms |    21.36 ms |     5.55 ms |   1.00 |    0.00 |           - |         - |         - |        176 B |
|     &#39;ImageSharp Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |            Tiff/Benchmarks/jpeg444_big_rgb_deflate.tiff | 17,974.8 ms |   751.73 ms |   116.33 ms |   7.07 |    0.04 |   1000.0000 | 1000.0000 | 1000.0000 | 1123949960 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| **&#39;System.Drawing Tiff&#39;** | **Job-MTZTUC** |    **.NET 4.7.2** |                **Tiff/Benchmarks/jpeg444_big_rgb_lzw.tiff** |  **3,368.4 ms** |    **40.71 ms** |     **6.30 ms** |   **1.00** |    **0.00** |   **4000.0000** |         **-** |         **-** |   **22835824 B** |
|     &#39;ImageSharp Tiff&#39; | Job-MTZTUC |    .NET 4.7.2 |                Tiff/Benchmarks/jpeg444_big_rgb_lzw.tiff | 28,919.9 ms |   705.58 ms |   183.24 ms |   8.57 |    0.04 |   1000.0000 | 1000.0000 | 1000.0000 | 1123956384 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |                Tiff/Benchmarks/jpeg444_big_rgb_lzw.tiff |  3,365.1 ms |    36.93 ms |     5.72 ms |   1.00 |    0.00 |   4000.0000 |         - |         - |   22789840 B |
|     &#39;ImageSharp Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |                Tiff/Benchmarks/jpeg444_big_rgb_lzw.tiff | 17,905.1 ms |    40.08 ms |    10.41 ms |   5.32 |    0.01 |   1000.0000 | 1000.0000 | 1000.0000 | 1123949072 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |                Tiff/Benchmarks/jpeg444_big_rgb_lzw.tiff |  3,377.6 ms |   125.36 ms |    32.56 ms |   1.00 |    0.00 |           - |         - |         - |        176 B |
|     &#39;ImageSharp Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |                Tiff/Benchmarks/jpeg444_big_rgb_lzw.tiff | 16,998.0 ms |   460.59 ms |   119.61 ms |   5.03 |    0.07 |   1000.0000 | 1000.0000 | 1000.0000 | 1123952144 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| **&#39;System.Drawing Tiff&#39;** | **Job-MTZTUC** |    **.NET 4.7.2** |           **Tiff/Benchmarks/jpeg444_big_rgb_packbits.tiff** |  **1,849.3 ms** |    **43.52 ms** |    **11.30 ms** |   **1.00** |    **0.00** | **255000.0000** |         **-** |         **-** |  **812350880 B** |
|     &#39;ImageSharp Tiff&#39; | Job-MTZTUC |    .NET 4.7.2 |           Tiff/Benchmarks/jpeg444_big_rgb_packbits.tiff | 29,360.0 ms |   157.78 ms |    40.98 ms |  15.88 |    0.12 |           - |         - |         - | 2690323752 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |           Tiff/Benchmarks/jpeg444_big_rgb_packbits.tiff |  1,882.7 ms |    64.85 ms |    16.84 ms |   1.00 |    0.00 | 255000.0000 |         - |         - |  811943568 B |
|     &#39;ImageSharp Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |           Tiff/Benchmarks/jpeg444_big_rgb_packbits.tiff | 18,967.7 ms |   445.86 ms |   115.79 ms |  10.08 |    0.09 |           - |         - |         - | 2690318648 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |           Tiff/Benchmarks/jpeg444_big_rgb_packbits.tiff |  1,743.2 ms |    78.50 ms |    20.39 ms |   1.00 |    0.00 |           - |         - |         - |        176 B |
|     &#39;ImageSharp Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |           Tiff/Benchmarks/jpeg444_big_rgb_packbits.tiff | 17,379.6 ms |   243.53 ms |    63.24 ms |   9.97 |    0.10 |           - |         - |         - | 2690321912 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| **&#39;System.Drawing Tiff&#39;** | **Job-MTZTUC** |    **.NET 4.7.2** |       **Tiff/Benchmarks/jpeg444_big_rgb_uncompressed.tiff** |    **758.5 ms** |     **9.75 ms** |     **2.53 ms** |   **1.00** |    **0.00** | **255000.0000** |         **-** |         **-** |  **806059984 B** |
|     &#39;ImageSharp Tiff&#39; | Job-MTZTUC |    .NET 4.7.2 |       Tiff/Benchmarks/jpeg444_big_rgb_uncompressed.tiff | 29,198.2 ms |   677.81 ms |   176.03 ms |  38.50 |    0.19 |           - |         - |         - | 1878827096 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |       Tiff/Benchmarks/jpeg444_big_rgb_uncompressed.tiff |    760.1 ms |    15.95 ms |     2.47 ms |   1.00 |    0.00 | 255000.0000 |         - |         - |  805652192 B |
|     &#39;ImageSharp Tiff&#39; | Job-BGVYTJ | .NET Core 2.1 |       Tiff/Benchmarks/jpeg444_big_rgb_uncompressed.tiff | 18,457.2 ms |    35.60 ms |     5.51 ms |  24.28 |    0.08 |           - |         - |         - | 1878821992 B |
|                       |            |               |                                                         |             |             |             |        |         |             |           |           |              |
| &#39;System.Drawing Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |       Tiff/Benchmarks/jpeg444_big_rgb_uncompressed.tiff |    629.5 ms |    11.40 ms |     2.96 ms |   1.00 |    0.00 |           - |         - |         - |        176 B |
|     &#39;ImageSharp Tiff&#39; | Job-ZDUDFU | .NET Core 3.1 |       Tiff/Benchmarks/jpeg444_big_rgb_uncompressed.tiff | 17,579.8 ms |   371.72 ms |    96.54 ms |  27.93 |    0.11 |           - |         - |         - | 1878825256 B |
