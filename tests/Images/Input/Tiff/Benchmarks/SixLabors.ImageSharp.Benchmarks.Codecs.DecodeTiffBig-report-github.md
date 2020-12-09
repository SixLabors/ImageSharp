``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-3610QM CPU 2.30GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.100
  [Host]     : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT
  Job-KSIANY : .NET Framework 4.8 (4.8.4250.0), X64 RyuJIT
  Job-VMCLSF : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
  Job-UHENIY : .NET Core 3.1.9 (CoreCLR 4.700.20.47201, CoreFX 4.700.20.47203), X64 RyuJIT

InvocationCount=1  IterationCount=5  LaunchCount=1  
UnrollFactor=1  WarmupCount=3  

```
|                Method |        Job |       Runtime |                          TestImage |       Mean |     Error |    StdDev | Ratio | RatioSD |      Gen 0 |     Gen 1 |     Gen 2 |   Allocated |
|---------------------- |----------- |-------------- |----------------------------------- |-----------:|----------:|----------:|------:|--------:|-----------:|----------:|----------:|------------:|
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** |                **medium_bw_Fax3.tiff** |   **491.6 ms** |  **20.40 ms** |   **5.30 ms** |  **1.00** |    **0.00** |  **1000.0000** |         **-** |         **-** |   **5768128 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 |                medium_bw_Fax3.tiff | 6,970.2 ms |  70.64 ms |  10.93 ms | 14.23 |    0.12 |  1000.0000 | 1000.0000 | 1000.0000 | 241518600 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |                medium_bw_Fax3.tiff |   486.2 ms |  23.15 ms |   3.58 ms |  1.00 |    0.00 |  1000.0000 |         - |         - |   5751016 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |                medium_bw_Fax3.tiff | 4,150.2 ms | 322.16 ms |  83.66 ms |  8.47 |    0.16 |          - |         - |         - | 235961088 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 |                medium_bw_Fax3.tiff |   490.1 ms |  12.76 ms |   3.31 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 |                medium_bw_Fax3.tiff | 3,582.9 ms |  61.89 ms |  16.07 ms |  7.31 |    0.06 |          - |         - |         - | 235961496 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** |                 **medium_bw_Rle.tiff** |   **499.1 ms** |  **26.71 ms** |   **6.94 ms** |  **1.00** |    **0.00** |  **1000.0000** |         **-** |         **-** |   **8494472 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 |                 medium_bw_Rle.tiff | 7,290.4 ms | 938.28 ms | 243.67 ms | 14.61 |    0.33 |  1000.0000 | 1000.0000 | 1000.0000 | 237020384 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |                 medium_bw_Rle.tiff |   490.6 ms |  30.19 ms |   4.67 ms |  1.00 |    0.00 |  1000.0000 |         - |         - |   8475688 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |                 medium_bw_Rle.tiff | 4,230.2 ms |  35.59 ms |   5.51 ms |  8.62 |    0.08 |          - |         - |         - | 235961944 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 |                 medium_bw_Rle.tiff |   487.6 ms |  12.07 ms |   1.87 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 |                 medium_bw_Rle.tiff | 3,647.4 ms |  42.62 ms |  11.07 ms |  7.48 |    0.04 |          - |         - |         - | 235962184 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** | **medium_grayscale_uncompressed.tiff** |   **606.7 ms** |  **20.45 ms** |   **5.31 ms** |  **1.00** |    **0.00** | **18000.0000** |         **-** |         **-** |  **90301696 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 | medium_grayscale_uncompressed.tiff | 1,852.9 ms |   6.74 ms |   1.75 ms |  3.05 |    0.03 |          - |         - |         - | 235970584 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 | medium_grayscale_uncompressed.tiff |   606.6 ms |  36.58 ms |   9.50 ms |  1.00 |    0.00 | 18000.0000 |         - |         - |  90104048 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 | medium_grayscale_uncompressed.tiff |   764.3 ms |  15.69 ms |   4.08 ms |  1.26 |    0.02 |          - |         - |         - | 235965376 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 | medium_grayscale_uncompressed.tiff |   569.6 ms |  17.44 ms |   4.53 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 | medium_grayscale_uncompressed.tiff |   655.2 ms |  17.48 ms |   4.54 ms |  1.15 |    0.01 |          - |         - |         - | 235965488 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** |   **medium_palette_uncompressed.tiff** |   **578.0 ms** |  **22.32 ms** |   **5.80 ms** |  **1.00** |    **0.00** | **18000.0000** |         **-** |         **-** |  **90301696 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 |   medium_palette_uncompressed.tiff | 3,336.9 ms |  21.42 ms |   5.56 ms |  5.77 |    0.07 |          - |         - |         - | 236003608 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |   medium_palette_uncompressed.tiff |   601.9 ms |  40.85 ms |   6.32 ms |  1.00 |    0.00 | 18000.0000 |         - |         - |  90107368 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |   medium_palette_uncompressed.tiff | 1,971.9 ms |  15.69 ms |   4.07 ms |  3.28 |    0.04 |          - |         - |         - | 235996096 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 |   medium_palette_uncompressed.tiff |   566.1 ms |  28.06 ms |   4.34 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 |   medium_palette_uncompressed.tiff | 1,664.1 ms |  11.59 ms |   1.79 ms |  2.94 |    0.02 |          - |         - |         - | 235996208 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** |            **medium_rgb_deflate.tiff** |   **357.4 ms** |  **15.54 ms** |   **2.40 ms** |  **1.00** |    **0.00** |  **3000.0000** |         **-** |         **-** |   **9662560 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 |            medium_rgb_deflate.tiff |   776.1 ms |  14.51 ms |   3.77 ms |  2.17 |    0.01 | 22000.0000 | 1000.0000 | 1000.0000 | 303476856 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |            medium_rgb_deflate.tiff |   359.7 ms |  12.29 ms |   3.19 ms |  1.00 |    0.00 |  3000.0000 |         - |         - |   9629400 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |            medium_rgb_deflate.tiff |   554.5 ms |  16.78 ms |   4.36 ms |  1.54 |    0.02 |  2000.0000 | 1000.0000 | 1000.0000 | 239716144 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 |            medium_rgb_deflate.tiff |   353.2 ms |   7.22 ms |   1.12 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 |            medium_rgb_deflate.tiff |   557.1 ms |  10.79 ms |   2.80 ms |  1.58 |    0.00 |  2000.0000 | 1000.0000 | 1000.0000 | 239470552 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** |                **medium_rgb_lzw.tiff** |   **511.0 ms** |   **6.43 ms** |   **1.67 ms** |  **1.00** |    **0.00** |  **3000.0000** |         **-** |         **-** |  **11600840 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 |                medium_rgb_lzw.tiff | 2,691.6 ms |  16.81 ms |   2.60 ms |  5.27 |    0.02 |          - |         - |         - | 236044312 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |                medium_rgb_lzw.tiff |   511.4 ms |  11.44 ms |   1.77 ms |  1.00 |    0.00 |  3000.0000 |         - |         - |  11569776 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |                medium_rgb_lzw.tiff | 1,654.1 ms |  12.42 ms |   1.92 ms |  3.23 |    0.01 |          - |         - |         - | 236041592 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 |                medium_rgb_lzw.tiff |   507.7 ms |   8.89 ms |   2.31 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 |                medium_rgb_lzw.tiff | 1,689.5 ms |  40.41 ms |   6.25 ms |  3.33 |    0.03 |          - |         - |         - | 236041656 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** |           **medium_rgb_packbits.tiff** |   **776.8 ms** |  **31.69 ms** |   **8.23 ms** |  **1.00** |    **0.00** | **56000.0000** |         **-** |         **-** | **304057016 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 |           medium_rgb_packbits.tiff |   531.2 ms |  23.17 ms |   6.02 ms |  0.68 |    0.01 |          - |         - |         - | 236003352 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |           medium_rgb_packbits.tiff |   764.2 ms |  41.43 ms |   6.41 ms |  1.00 |    0.00 | 56000.0000 |         - |         - | 303861120 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |           medium_rgb_packbits.tiff |   300.0 ms |   4.39 ms |   0.68 ms |  0.39 |    0.00 |          - |         - |         - | 235998408 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 |           medium_rgb_packbits.tiff |   659.1 ms |  34.59 ms |   8.98 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 |           medium_rgb_packbits.tiff |   297.5 ms |  21.13 ms |   5.49 ms |  0.45 |    0.00 |          - |         - |         - | 235998520 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| **&#39;System.Drawing Tiff&#39;** | **Job-KSIANY** |    **.NET 4.7.2** |       **medium_rgb_uncompressed.tiff** |   **742.5 ms** |  **50.45 ms** |  **13.10 ms** |  **1.00** |    **0.00** | **55000.0000** |         **-** |         **-** | **302644272 B** |
|     &#39;ImageSharp Tiff&#39; | Job-KSIANY |    .NET 4.7.2 |       medium_rgb_uncompressed.tiff |   414.3 ms |  15.37 ms |   3.99 ms |  0.56 |    0.01 |          - |         - |         - | 235986968 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |       medium_rgb_uncompressed.tiff |   750.2 ms |  74.13 ms |  19.25 ms |  1.00 |    0.00 | 55000.0000 |         - |         - | 302448096 B |
|     &#39;ImageSharp Tiff&#39; | Job-VMCLSF | .NET Core 2.1 |       medium_rgb_uncompressed.tiff |   283.6 ms |  21.56 ms |   5.60 ms |  0.38 |    0.01 |          - |         - |         - | 235981128 B |
|                       |            |               |                                    |            |           |           |       |         |            |           |           |             |
| &#39;System.Drawing Tiff&#39; | Job-UHENIY | .NET Core 3.1 |       medium_rgb_uncompressed.tiff |   662.6 ms |  49.79 ms |  12.93 ms |  1.00 |    0.00 |          - |         - |         - |       176 B |
|     &#39;ImageSharp Tiff&#39; | Job-UHENIY | .NET Core 3.1 |       medium_rgb_uncompressed.tiff |   278.6 ms |   9.48 ms |   2.46 ms |  0.42 |    0.01 |          - |         - |         - | 235981352 B |
