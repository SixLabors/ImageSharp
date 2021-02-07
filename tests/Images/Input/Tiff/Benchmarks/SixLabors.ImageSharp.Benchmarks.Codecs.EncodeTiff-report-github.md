``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-3610QM CPU 2.30GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.101
  [Host]     : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  Job-BXRYWG : .NET Framework 4.8 (4.8.4300.0), X64 RyuJIT
  Job-YFKMTZ : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
  Job-ONTENJ : .NET Core 3.1.10 (CoreCLR 4.700.20.51601, CoreFX 4.700.20.51901), X64 RyuJIT

IterationCount=3  LaunchCount=1  WarmupCount=3  

```
|                Method |        Job |       Runtime |                             TestImage |     Compression |       Mean |       Error |     StdDev | Ratio | RatioSD |     Gen 0 |     Gen 1 |     Gen 2 |  Allocated |
|---------------------- |----------- |-------------- |-------------------------------------- |---------------- |-----------:|------------:|-----------:|------:|--------:|----------:|----------:|----------:|-----------:|
| **&#39;System.Drawing Tiff&#39;** | **Job-BXRYWG** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |            **None** |   **6.520 ms** |   **2.1764 ms** |  **0.1193 ms** |  **1.00** |    **0.00** |  **984.3750** |  **984.3750** |  **984.3750** | **11570062 B** |
|     &#39;ImageSharp Tiff&#39; | Job-BXRYWG |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   5.698 ms |   8.2629 ms |  0.4529 ms |  0.87 |    0.06 |  539.0625 |  500.0000 |  492.1875 |  9919288 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   6.851 ms |   1.4499 ms |  0.0795 ms |  1.00 |    0.00 |  984.3750 |  984.3750 |  984.3750 | 11562768 B |
|     &#39;ImageSharp Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   4.294 ms |   2.0150 ms |  0.1104 ms |  0.63 |    0.02 |  539.0625 |  500.0000 |  492.1875 |  9918144 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   5.835 ms |   1.7302 ms |  0.0948 ms |  1.00 |    0.00 |  984.3750 |  984.3750 |  984.3750 |  8672224 B |
|     &#39;ImageSharp Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   5.167 ms |   1.1793 ms |  0.0646 ms |  0.89 |    0.02 |  539.0625 |  500.0000 |  492.1875 |  9918112 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| **&#39;System.Drawing Tiff&#39;** | **Job-BXRYWG** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |         **Deflate** |         **NA** |          **NA** |         **NA** |     **?** |       **?** |         **-** |         **-** |         **-** |          **-** |
|     &#39;ImageSharp Tiff&#39; | Job-BXRYWG |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate | 125.909 ms |   2.8957 ms |  0.1587 ms |     ? |       ? |  750.0000 |  750.0000 |  750.0000 | 11167960 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate |         NA |          NA |         NA |     ? |       ? |         - |         - |         - |          - |
|     &#39;ImageSharp Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate | 125.041 ms |   6.3920 ms |  0.3504 ms |     ? |       ? |  750.0000 |  750.0000 |  750.0000 | 11164792 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate |         NA |          NA |         NA |     ? |       ? |         - |         - |         - |          - |
|     &#39;ImageSharp Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate | 125.139 ms |  16.3106 ms |  0.8940 ms |     ? |       ? |  750.0000 |  750.0000 |  750.0000 | 11168428 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| **&#39;System.Drawing Tiff&#39;** | **Job-BXRYWG** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |             **Lzw** |  **49.024 ms** |  **35.9580 ms** |  **1.9710 ms** |  **1.00** |    **0.00** |  **800.0000** |  **800.0000** |  **800.0000** | **10673371 B** |
|     &#39;ImageSharp Tiff&#39; | Job-BXRYWG |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw | 411.728 ms |  47.6380 ms |  2.6112 ms |  8.41 |    0.39 | 1000.0000 | 1000.0000 | 1000.0000 | 23265464 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw |  47.288 ms |   1.4131 ms |  0.0775 ms |  1.00 |    0.00 |  818.1818 |  818.1818 |  818.1818 | 10668688 B |
|     &#39;ImageSharp Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw | 201.643 ms |   5.6002 ms |  0.3070 ms |  4.26 |    0.00 |  333.3333 |  333.3333 |  333.3333 | 27451168 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw |  46.526 ms |   6.2383 ms |  0.3419 ms |  1.00 |    0.00 |  818.1818 |  818.1818 |  818.1818 |  8001741 B |
|     &#39;ImageSharp Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw | 170.276 ms |  20.5515 ms |  1.1265 ms |  3.66 |    0.04 |  333.3333 |  333.3333 |  333.3333 | 27451445 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| **&#39;System.Drawing Tiff&#39;** | **Job-BXRYWG** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |        **PackBits** |         **NA** |          **NA** |         **NA** |     **?** |       **?** |         **-** |         **-** |         **-** |          **-** |
|     &#39;ImageSharp Tiff&#39; | Job-BXRYWG |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |  28.948 ms |   7.0740 ms |  0.3877 ms |     ? |       ? |  500.0000 |  468.7500 |  468.7500 |  9943858 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |         NA |          NA |         NA |     ? |       ? |         - |         - |         - |          - |
|     &#39;ImageSharp Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |  22.611 ms |   0.9267 ms |  0.0508 ms |     ? |       ? |  500.0000 |  468.7500 |  468.7500 |  9942792 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |         NA |          NA |         NA |     ? |       ? |         - |         - |         - |          - |
|     &#39;ImageSharp Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |  23.465 ms |   4.7353 ms |  0.2596 ms |     ? |       ? |  531.2500 |  500.0000 |  500.0000 |  9942772 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| **&#39;System.Drawing Tiff&#39;** | **Job-BXRYWG** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |  **CcittGroup3Fax** |  **43.618 ms** |   **6.0416 ms** |  **0.3312 ms** |  **1.00** |    **0.00** |         **-** |         **-** |         **-** |  **1169683 B** |
|     &#39;ImageSharp Tiff&#39; | Job-BXRYWG |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax | 191.602 ms |  34.9864 ms |  1.9177 ms |  4.39 |    0.04 | 3333.3333 | 1333.3333 |  333.3333 | 24829048 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax |  43.258 ms |   3.5472 ms |  0.1944 ms |  1.00 |    0.00 |         - |         - |         - |  1169200 B |
|     &#39;ImageSharp Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax | 177.930 ms |  50.1223 ms |  2.7474 ms |  4.11 |    0.04 | 3666.6667 | 2000.0000 |  666.6667 | 24772997 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax |  43.330 ms |   2.8194 ms |  0.1545 ms |  1.00 |    0.00 |         - |         - |         - |   850189 B |
|     &#39;ImageSharp Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax | 168.846 ms |  19.1390 ms |  1.0491 ms |  3.90 |    0.01 | 3333.3333 | 1333.3333 |  333.3333 | 24774571 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| **&#39;System.Drawing Tiff&#39;** | **Job-BXRYWG** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** | **ModifiedHuffman** |  **17.106 ms** |  **12.6692 ms** |  **0.6944 ms** |  **1.00** |    **0.00** |  **937.5000** |  **937.5000** |  **937.5000** | **11561706 B** |
|     &#39;ImageSharp Tiff&#39; | Job-BXRYWG |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman | 192.530 ms |   7.9946 ms |  0.4382 ms | 11.27 |    0.47 | 3333.3333 | 1333.3333 |  333.3333 | 24826163 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman |  16.988 ms |   2.7313 ms |  0.1497 ms |  1.00 |    0.00 |  937.5000 |  937.5000 |  937.5000 | 11555088 B |
|     &#39;ImageSharp Tiff&#39; | Job-YFKMTZ | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman | 180.265 ms |  78.0340 ms |  4.2773 ms | 10.61 |    0.18 | 3666.6667 | 2000.0000 |  666.6667 | 24769453 B |
|                       |            |               |                                       |                 |            |             |            |       |         |           |           |           |            |
| &#39;System.Drawing Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman |  15.989 ms |   2.7139 ms |  0.1488 ms |  1.00 |    0.00 |  937.5000 |  937.5000 |  937.5000 |  8666467 B |
|     &#39;ImageSharp Tiff&#39; | Job-ONTENJ | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman | 181.295 ms | 231.7796 ms | 12.7046 ms | 11.34 |    0.90 | 3333.3333 | 1333.3333 |  333.3333 | 24770275 B |

Benchmarks with issues:
  EncodeTiff.'System.Drawing Tiff': Job-BXRYWG(Runtime=.NET 4.7.2, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=Deflate]
  EncodeTiff.'System.Drawing Tiff': Job-YFKMTZ(Runtime=.NET Core 2.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=Deflate]
  EncodeTiff.'System.Drawing Tiff': Job-ONTENJ(Runtime=.NET Core 3.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=Deflate]
  EncodeTiff.'System.Drawing Tiff': Job-BXRYWG(Runtime=.NET 4.7.2, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=PackBits]
  EncodeTiff.'System.Drawing Tiff': Job-YFKMTZ(Runtime=.NET Core 2.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=PackBits]
  EncodeTiff.'System.Drawing Tiff': Job-ONTENJ(Runtime=.NET Core 3.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=PackBits]
