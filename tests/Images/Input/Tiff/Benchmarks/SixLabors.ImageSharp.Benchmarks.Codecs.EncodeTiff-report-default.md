
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-3610QM CPU 2.30GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.101
  [Host]     : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  Job-KBSVFT : .NET Framework 4.8 (4.8.4300.0), X64 RyuJIT
  Job-SLIUCH : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
  Job-EFFLUU : .NET Core 3.1.10 (CoreCLR 4.700.20.51601, CoreFX 4.700.20.51901), X64 RyuJIT

IterationCount=3  LaunchCount=1  WarmupCount=3  

                Method |        Job |       Runtime |                             TestImage |     Compression |       Mean |       Error |    StdDev | Ratio | RatioSD |     Gen 0 |     Gen 1 |    Gen 2 |  Allocated |
---------------------- |----------- |-------------- |-------------------------------------- |---------------- |-----------:|------------:|----------:|------:|--------:|----------:|----------:|---------:|-----------:|
 **'System.Drawing Tiff'** | **Job-KBSVFT** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |            **None** |   **6.614 ms** |   **0.2900 ms** | **0.0159 ms** |  **1.00** |    **0.00** |  **984.3750** |  **984.3750** | **984.3750** | **11570062 B** |
     'ImageSharp Tiff' | Job-KBSVFT |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   4.844 ms |   0.8879 ms | 0.0487 ms |  0.73 |    0.01 |  375.0000 |  335.9375 | 335.9375 |  7445922 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   6.953 ms |   0.2917 ms | 0.0160 ms |  1.00 |    0.00 |  984.3750 |  984.3750 | 984.3750 | 11562768 B |
     'ImageSharp Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   3.189 ms |  15.5206 ms | 0.8507 ms |  0.46 |    0.12 |  925.7813 |  886.7188 | 882.8125 |  7444718 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   5.884 ms |   0.7275 ms | 0.0399 ms |  1.00 |    0.00 |  984.3750 |  984.3750 | 984.3750 |  8672224 B |
     'ImageSharp Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |            None |   3.342 ms |  18.8082 ms | 1.0309 ms |  0.57 |    0.18 |  796.8750 |  765.6250 | 757.8125 |  7444631 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 **'System.Drawing Tiff'** | **Job-KBSVFT** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |         **Deflate** |         **NA** |          **NA** |        **NA** |     **?** |       **?** |         **-** |         **-** |        **-** |          **-** |
     'ImageSharp Tiff' | Job-KBSVFT |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate |  87.815 ms |  11.2070 ms | 0.6143 ms |     ? |       ? |  833.3333 |  333.3333 | 333.3333 |  6617521 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate |         NA |          NA |        NA |     ? |       ? |         - |         - |        - |          - |
     'ImageSharp Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate |  84.005 ms |   3.1221 ms | 0.1711 ms |     ? |       ? | 1000.0000 |  500.0000 | 500.0000 |  6605507 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate |         NA |          NA |        NA |     ? |       ? |         - |         - |        - |          - |
     'ImageSharp Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |         Deflate |  81.102 ms |   6.5299 ms | 0.3579 ms |     ? |       ? | 1000.0000 |  428.5714 | 428.5714 |  6604792 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 **'System.Drawing Tiff'** | **Job-KBSVFT** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |             **Lzw** |  **47.121 ms** |   **7.2057 ms** | **0.3950 ms** |  **1.00** |    **0.00** |  **818.1818** |  **818.1818** | **818.1818** | **10673499 B** |
     'ImageSharp Tiff' | Job-KBSVFT |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw | 125.569 ms |   5.9762 ms | 0.3276 ms |  2.66 |    0.03 |  500.0000 |  500.0000 | 500.0000 |  8423760 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw |  47.311 ms |   4.2582 ms | 0.2334 ms |  1.00 |    0.00 |  818.1818 |  818.1818 | 818.1818 | 10668688 B |
     'ImageSharp Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw |  96.217 ms |  10.7439 ms | 0.5889 ms |  2.03 |    0.02 |  333.3333 |  333.3333 | 333.3333 |  8422488 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw |  46.347 ms |   3.7463 ms | 0.2053 ms |  1.00 |    0.00 |  818.1818 |  818.1818 | 818.1818 |  8001750 B |
     'ImageSharp Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |             Lzw |  93.635 ms |  11.9328 ms | 0.6541 ms |  2.02 |    0.01 |  333.3333 |  333.3333 | 333.3333 |  8422504 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 **'System.Drawing Tiff'** | **Job-KBSVFT** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |        **PackBits** |         **NA** |          **NA** |        **NA** |     **?** |       **?** |         **-** |         **-** |        **-** |          **-** |
     'ImageSharp Tiff' | Job-KBSVFT |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |  27.449 ms |   2.1924 ms | 0.1202 ms |     ? |       ? |  375.0000 |  343.7500 | 343.7500 |  7453052 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |         NA |          NA |        NA |     ? |       ? |         - |         - |        - |          - |
     'ImageSharp Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |  19.935 ms |   1.6746 ms | 0.0918 ms |     ? |       ? |  375.0000 |  343.7500 | 343.7500 |  7451912 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |         NA |          NA |        NA |     ? |       ? |         - |         - |        - |          - |
     'ImageSharp Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |        PackBits |  19.664 ms |   9.2973 ms | 0.5096 ms |     ? |       ? |  375.0000 |  343.7500 | 343.7500 |  7451974 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 **'System.Drawing Tiff'** | **Job-KBSVFT** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** |  **CcittGroup3Fax** |  **43.335 ms** |   **2.7418 ms** | **0.1503 ms** |  **1.00** |    **0.00** |         **-** |         **-** |        **-** |  **1169683 B** |
     'ImageSharp Tiff' | Job-KBSVFT |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax | 191.413 ms |  55.3579 ms | 3.0344 ms |  4.42 |    0.07 | 3333.3333 | 1333.3333 | 333.3333 | 22714336 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax |  43.559 ms |   4.3644 ms | 0.2392 ms |  1.00 |    0.00 |         - |         - |        - |  1169200 B |
     'ImageSharp Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax | 180.059 ms |  38.0202 ms | 2.0840 ms |  4.13 |    0.03 | 3666.6667 | 2000.0000 | 666.6667 | 22658509 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax |  43.437 ms |   3.9436 ms | 0.2162 ms |  1.00 |    0.00 |         - |         - |        - |   850187 B |
     'ImageSharp Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff |  CcittGroup3Fax | 171.370 ms | 129.4719 ms | 7.0968 ms |  3.94 |    0.14 | 3333.3333 | 1333.3333 | 333.3333 | 22658261 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 **'System.Drawing Tiff'** | **Job-KBSVFT** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_uncompressed.tiff** | **ModifiedHuffman** |  **17.099 ms** |   **9.2464 ms** | **0.5068 ms** |  **1.00** |    **0.00** |  **937.5000** |  **937.5000** | **937.5000** | **11561706 B** |
     'ImageSharp Tiff' | Job-KBSVFT |    .NET 4.7.2 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman | 191.066 ms |  16.8580 ms | 0.9240 ms | 11.18 |    0.36 | 3333.3333 | 1333.3333 | 333.3333 | 22710384 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman |  17.035 ms |   1.8390 ms | 0.1008 ms |  1.00 |    0.00 |  937.5000 |  937.5000 | 937.5000 | 11555088 B |
     'ImageSharp Tiff' | Job-SLIUCH | .NET Core 2.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman | 177.379 ms |  33.9255 ms | 1.8596 ms | 10.41 |    0.06 | 3666.6667 | 2000.0000 | 666.6667 | 22656395 B |
                       |            |               |                                       |                 |            |             |           |       |         |           |           |          |            |
 'System.Drawing Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman |  15.948 ms |   3.3609 ms | 0.1842 ms |  1.00 |    0.00 |  937.5000 |  937.5000 | 937.5000 |  8666468 B |
     'ImageSharp Tiff' | Job-EFFLUU | .NET Core 3.1 | Tiff/Calliphora_rgb_uncompressed.tiff | ModifiedHuffman | 167.231 ms |  21.2228 ms | 1.1633 ms | 10.49 |    0.09 | 3333.3333 | 1333.3333 | 333.3333 | 22659275 B |

Benchmarks with issues:
  EncodeTiff.'System.Drawing Tiff': Job-KBSVFT(Runtime=.NET 4.7.2, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=Deflate]
  EncodeTiff.'System.Drawing Tiff': Job-SLIUCH(Runtime=.NET Core 2.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=Deflate]
  EncodeTiff.'System.Drawing Tiff': Job-EFFLUU(Runtime=.NET Core 3.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=Deflate]
  EncodeTiff.'System.Drawing Tiff': Job-KBSVFT(Runtime=.NET 4.7.2, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=PackBits]
  EncodeTiff.'System.Drawing Tiff': Job-SLIUCH(Runtime=.NET Core 2.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=PackBits]
  EncodeTiff.'System.Drawing Tiff': Job-EFFLUU(Runtime=.NET Core 3.1, IterationCount=3, LaunchCount=1, WarmupCount=3) [TestImage=Tiff/Calliphora_rgb_uncompressed.tiff, Compression=PackBits]
