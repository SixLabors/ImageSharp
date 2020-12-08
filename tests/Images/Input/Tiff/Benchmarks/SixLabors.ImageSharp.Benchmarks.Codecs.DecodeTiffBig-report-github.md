``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-3610QM CPU 2.30GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.100
  [Host]     : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
  Job-ORBNFQ : .NET Framework 4.8 (4.8.4250.0), X64 RyuJIT
  Job-OLKFNC : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
  Job-PCYTCM : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT

InvocationCount=1  IterationCount=5  LaunchCount=1  
UnrollFactor=1  WarmupCount=3  

```
|                Method |        Job |       Runtime |                          TestImage |       Mean |     Error |    StdDev | Ratio | RatioSD |      Gen 0 |     Gen 1 |     Gen 2 |   Allocated |
|---------------------- |----------- |-------------- |----------------------------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** |                **medium_bw_Fax3.tiff** |   **483.0 ms** |  **25.89 ms** |   **6.72 ms** |  **1.00** |    **0.00** |  **1000.0000** |         **-** |         **-** |   **5768128 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 |                medium_bw_Fax3.tiff | 6,920.1 ms |  50.09 ms |  13.01 ms | 14.33 |    0.22 |  1000.0000 | 1000.0000 | 1000.0000 | 241519088 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |                medium_bw_Fax3.tiff |   480.6 ms |  15.76 ms |   4.09 ms |  1.00 |    0.00 |  1000.0000 |         - |         - |   5751016 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |                medium_bw_Fax3.tiff | 4,024.8 ms |  67.05 ms |  17.41 ms |  8.37 |    0.09 |          - |         - |         - | 235961088 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |                medium_bw_Fax3.tiff |   494.7 ms |  66.04 ms |  10.22 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |                medium_bw_Fax3.tiff | 3,609.1 ms |  40.03 ms |  10.40 ms |  7.29 |    0.15 |          - |         - |         - | 235961328 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** |                 **medium_bw_Rle.tiff** |   **508.8 ms** |  **70.45 ms** |  **18.30 ms** |  **1.00** |    **0.00** |  **1000.0000** |         **-** |         **-** |   **8494472 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 |                 medium_bw_Rle.tiff | 7,256.1 ms | 862.61 ms | 224.02 ms | 14.26 |    0.19 |  1000.0000 | 1000.0000 | 1000.0000 | 237020384 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |                 medium_bw_Rle.tiff |   498.6 ms |  19.57 ms |   5.08 ms |  1.00 |    0.00 |  1000.0000 |         - |         - |   8475688 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |                 medium_bw_Rle.tiff | 4,077.0 ms |  63.52 ms |  16.50 ms |  8.18 |    0.08 |          - |         - |         - | 235961944 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |                 medium_bw_Rle.tiff |   484.9 ms |   9.27 ms |   1.44 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |                 medium_bw_Rle.tiff | 3,544.6 ms |  67.38 ms |  17.50 ms |  7.32 |    0.00 |          - |         - |         - | 235962272 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** | **medium_grayscale_uncompressed.tiff** |   **603.1 ms** |  **12.35 ms** |   **3.21 ms** |  **1.00** |    **0.00** | **18000.0000** |         **-** |         **-** |  **90301696 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 | medium_grayscale_uncompressed.tiff | 1,815.4 ms |  29.18 ms |   7.58 ms |  3.01 |    0.02 |          - |         - |         - | 235970584 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 | medium_grayscale_uncompressed.tiff |   608.9 ms |  30.77 ms |   7.99 ms |  1.00 |    0.00 | 18000.0000 |         - |         - |  90104048 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 | medium_grayscale_uncompressed.tiff | 1,001.3 ms |  10.80 ms |   1.67 ms |  1.65 |    0.02 |          - |         - |         - | 235965376 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 | medium_grayscale_uncompressed.tiff |   567.6 ms |  14.90 ms |   3.87 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 | medium_grayscale_uncompressed.tiff |   910.8 ms |  22.95 ms |   5.96 ms |  1.60 |    0.01 |          - |         - |         - | 235965440 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** |   **medium_palette_uncompressed.tiff** |   **602.2 ms** |   **5.20 ms** |   **0.80 ms** |  **1.00** |    **0.00** | **18000.0000** |         **-** |         **-** |  **90301696 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 |   medium_palette_uncompressed.tiff | 3,329.3 ms |  38.02 ms |   5.88 ms |  5.53 |    0.01 |          - |         - |         - | 236004096 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |   medium_palette_uncompressed.tiff |   601.8 ms |  21.00 ms |   5.45 ms |  1.00 |    0.00 | 18000.0000 |         - |         - |  90107368 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |   medium_palette_uncompressed.tiff | 1,954.6 ms |  21.60 ms |   5.61 ms |  3.25 |    0.03 |          - |         - |         - | 235996096 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |   medium_palette_uncompressed.tiff |   575.5 ms |  25.83 ms |   6.71 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |   medium_palette_uncompressed.tiff | 1,656.7 ms |  15.51 ms |   2.40 ms |  2.88 |    0.04 |          - |         - |         - | 235996256 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** |            **medium_rgb_deflate.tiff** |   **358.0 ms** |   **8.50 ms** |   **2.21 ms** |  **1.00** |    **0.00** |  **3000.0000** |         **-** |         **-** |   **9662560 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 |            medium_rgb_deflate.tiff | 1,020.5 ms |  14.93 ms |   2.31 ms |  2.84 |    0.02 | 22000.0000 | 1000.0000 | 1000.0000 | 302745704 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |            medium_rgb_deflate.tiff |   356.9 ms |  11.32 ms |   1.75 ms |  1.00 |    0.00 |  3000.0000 |         - |         - |   9629400 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |            medium_rgb_deflate.tiff |   921.4 ms |   8.62 ms |   1.33 ms |  2.58 |    0.01 |          - |         - |         - | 238909800 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |            medium_rgb_deflate.tiff |   357.3 ms |  28.17 ms |   7.32 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |            medium_rgb_deflate.tiff |   929.0 ms |  10.26 ms |   2.66 ms |  2.60 |    0.05 |          - |         - |         - | 238664536 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** |                **medium_rgb_lzw.tiff** |   **509.2 ms** |   **8.93 ms** |   **2.32 ms** |  **1.00** |    **0.00** |  **3000.0000** |         **-** |         **-** |  **11600840 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 |                medium_rgb_lzw.tiff | 2,967.3 ms |  23.69 ms |   6.15 ms |  5.83 |    0.03 |          - |         - |         - | 236060696 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |                medium_rgb_lzw.tiff |   508.9 ms |  15.11 ms |   3.93 ms |  1.00 |    0.00 |  3000.0000 |         - |         - |  11569776 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |                medium_rgb_lzw.tiff | 2,046.1 ms |  24.58 ms |   6.38 ms |  4.02 |    0.04 |          - |         - |         - | 236056952 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |                medium_rgb_lzw.tiff |   511.1 ms |  16.58 ms |   4.31 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |                medium_rgb_lzw.tiff | 2,072.9 ms |   9.12 ms |   2.37 ms |  4.06 |    0.03 |          - |         - |         - | 236057016 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** |           **medium_rgb_packbits.tiff** |   **779.8 ms** |  **51.30 ms** |  **13.32 ms** |  **1.00** |    **0.00** | **56000.0000** |         **-** |         **-** | **304057016 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 |           medium_rgb_packbits.tiff |   778.8 ms |  14.17 ms |   3.68 ms |  1.00 |    0.02 |          - |         - |         - | 236003352 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |           medium_rgb_packbits.tiff |   769.3 ms |  57.35 ms |  14.89 ms |  1.00 |    0.00 | 56000.0000 |         - |         - | 303861120 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |           medium_rgb_packbits.tiff |   675.7 ms |  13.16 ms |   3.42 ms |  0.88 |    0.02 |          - |         - |         - | 235998408 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |           medium_rgb_packbits.tiff |   665.7 ms |  32.83 ms |   8.53 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |           medium_rgb_packbits.tiff |   671.7 ms |  14.76 ms |   2.28 ms |  1.01 |    0.02 |          - |         - |         - | 235998568 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-ORBNFQ** |    **.NET 4.7.2** |       **medium_rgb_uncompressed.tiff** |   **738.3 ms** |  **26.41 ms** |   **6.86 ms** |  **1.00** |    **0.00** | **55000.0000** |         **-** |         **-** | **302644272 B** |
|     &#39;ImageSharp Tiff&#39; | Job-ORBNFQ |    .NET 4.7.2 |       medium_rgb_uncompressed.tiff |   740.1 ms |   8.51 ms |   1.32 ms |  1.00 |    0.01 |          - |         - |         - | 235986968 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |       medium_rgb_uncompressed.tiff |   747.5 ms |  64.06 ms |  16.64 ms |  1.00 |    0.00 | 55000.0000 |         - |         - | 302448096 B |
|     &#39;ImageSharp Tiff&#39; | Job-OLKFNC | .NET Core 2.1 |       medium_rgb_uncompressed.tiff |   654.6 ms |  10.01 ms |   2.60 ms |  0.88 |    0.02 |          - |         - |         - | 235981128 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |       medium_rgb_uncompressed.tiff |   664.0 ms |  51.23 ms |  13.30 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-PCYTCM | .NET Core 3.1 |       medium_rgb_uncompressed.tiff |   653.0 ms |   4.88 ms |   1.27 ms |  0.98 |    0.02 |          - |         - |         - | 235981192 B |
