``` ini

BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-3610QM CPU 2.30GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.101
  [Host]     : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  Job-EMDSBW : .NET Framework 4.8 (4.8.4300.0), X64 RyuJIT
  Job-KCUIVJ : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
  Job-NIWDJE : .NET Core 3.1.10 (CoreCLR 4.700.20.51601, CoreFX 4.700.20.51901), X64 RyuJIT

InvocationCount=1  IterationCount=3  LaunchCount=1  
UnrollFactor=1  WarmupCount=3  

```
|                Method |        Job |       Runtime |                                      TestImage |        Mean |         Error |       StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
|---------------------- |----------- |-------------- |----------------------------------------------- |------------:|--------------:|-------------:|------:|--------:|------:|------:|------:|----------:|
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** |    **Tiff/Calliphora_grayscale_uncompressed.tiff** |  **1,107.9 μs** |     **260.10 μs** |     **14.26 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |  **974848 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 |    Tiff/Calliphora_grayscale_uncompressed.tiff | 29,794.8 μs |   3,103.68 μs |    170.12 μs | 26.90 |    0.49 |     - |     - |     - |   32768 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff |  1,020.4 μs |     641.11 μs |     35.14 μs |  1.00 |    0.00 |     - |     - |     - |  968832 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff | 12,593.4 μs |   4,807.87 μs |    263.54 μs | 12.36 |    0.67 |     - |     - |     - |   29976 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff |    987.2 μs |   2,211.93 μs |    121.24 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff | 44,255.5 μs |  13,031.10 μs |    714.28 μs | 45.23 |    4.88 |     - |     - |     - |   29896 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** |     **Tiff/Calliphora_rgb_deflate_predictor.tiff** | **16,118.9 μs** |   **2,095.51 μs** |    **114.86 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **1483440 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 25,967.5 μs |   4,545.04 μs |    249.13 μs |  1.61 |    0.01 |     - |     - |     - |  848240 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 16,465.6 μs |   7,761.65 μs |    425.44 μs |  1.00 |    0.00 |     - |     - |     - | 1480344 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 18,536.9 μs |   3,415.62 μs |    187.22 μs |  1.13 |    0.02 |     - |     - |     - |   68176 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 16,216.2 μs |   3,288.12 μs |    180.23 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 20,740.6 μs |  54,608.55 μs |  2,993.28 μs |  1.28 |    0.17 |     - |     - |     - |   65120 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** |         **Tiff/Calliphora_rgb_lzw_predictor.tiff** | **83,012.1 μs** |  **14,786.35 μs** |    **810.49 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **2545736 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 64,895.5 μs |  11,397.89 μs |    624.76 μs |  0.78 |    0.01 |     - |     - |     - |   24576 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 82,854.1 μs |  45,495.28 μs |  2,493.75 μs |  1.00 |    0.00 |     - |     - |     - | 2541376 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 44,307.1 μs |  15,595.85 μs |    854.86 μs |  0.53 |    0.01 |     - |     - |     - |   23832 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 83,297.5 μs |  15,796.71 μs |    865.87 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 59,464.0 μs |  13,870.15 μs |    760.27 μs |  0.71 |    0.01 |     - |     - |     - |   23760 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** |              **Tiff/Calliphora_rgb_packbits.tiff** |  **3,707.2 μs** |   **6,293.27 μs** |    **344.96 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **2916008 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 |              Tiff/Calliphora_rgb_packbits.tiff |  7,526.9 μs |   5,965.86 μs |    327.01 μs |  2.04 |    0.24 |     - |     - |     - |   81920 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |              Tiff/Calliphora_rgb_packbits.tiff |  4,037.7 μs |   9,243.97 μs |    506.69 μs |  1.00 |    0.00 |     - |     - |     - | 2903544 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |              Tiff/Calliphora_rgb_packbits.tiff |  4,395.7 μs |   1,394.13 μs |     76.42 μs |  1.10 |    0.15 |     - |     - |     - |   80256 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |              Tiff/Calliphora_rgb_packbits.tiff |  3,456.3 μs |   4,443.73 μs |    243.58 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |              Tiff/Calliphora_rgb_packbits.tiff |  4,542.9 μs |   3,820.61 μs |    209.42 μs |  1.32 |    0.13 |     - |     - |     - |   80184 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_palette_lzw_predictor.tiff** | **60,298.5 μs** |  **24,263.76 μs** |  **1,329.98 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |  **827416 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 76,021.3 μs |   4,206.79 μs |    230.59 μs |  1.26 |    0.02 |     - |     - |     - |   49152 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 59,122.1 μs |   9,681.07 μs |    530.65 μs |  1.00 |    0.00 |     - |     - |     - |  825648 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 45,789.3 μs |   7,453.72 μs |    408.56 μs |  0.77 |    0.00 |     - |     - |     - |   45936 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 61,361.5 μs |  25,759.90 μs |  1,411.99 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 68,134.6 μs | 303,212.80 μs | 16,620.12 μs |  1.11 |    0.25 |     - |     - |     - |   45864 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** |          **Tiff/Calliphora_rgb_uncompressed.tiff** |  **3,431.7 μs** |   **7,649.10 μs** |    **419.27 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **2915944 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 |          Tiff/Calliphora_rgb_uncompressed.tiff |  6,382.4 μs |   2,573.27 μs |    141.05 μs |  1.87 |    0.18 |     - |     - |     - |   57344 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  3,636.1 μs |   8,607.66 μs |    471.81 μs |  1.00 |    0.00 |     - |     - |     - | 2905840 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  4,018.7 μs |   1,662.68 μs |     91.14 μs |  1.12 |    0.16 |     - |     - |     - |   51472 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  2,970.8 μs |   5,028.62 μs |    275.64 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  4,009.6 μs |   3,007.19 μs |    164.83 μs |  1.36 |    0.17 |     - |     - |     - |   51400 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** |     **Tiff/ccitt_fax3_all_terminating_codes.tiff** |    **178.4 μs** |     **375.89 μs** |     **20.60 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |    **8192 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    634.5 μs |     251.14 μs |     13.77 μs |  3.58 |    0.37 |     - |     - |     - |   24576 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    171.7 μs |     606.95 μs |     33.27 μs |  1.00 |    0.00 |     - |     - |     - |    2032 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    421.0 μs |      31.60 μs |      1.73 μs |  2.51 |    0.49 |     - |     - |     - |   17848 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    137.2 μs |      78.18 μs |      4.29 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    888.5 μs |     495.11 μs |     27.14 μs |  6.47 |    0.05 |     - |     - |     - |   17768 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| **&#39;System.Drawing Tiff&#39;** | **Job-EMDSBW** |    **.NET 4.7.2** |         **Tiff/huffman_rle_all_makeup_codes.tiff** |    **189.8 μs** |     **818.95 μs** |     **44.89 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |    **8192 B** |
|     &#39;ImageSharp Tiff&#39; | Job-EMDSBW |    .NET 4.7.2 |         Tiff/huffman_rle_all_makeup_codes.tiff |  9,137.1 μs |   1,178.82 μs |     64.62 μs | 49.85 |   10.86 |     - |     - |     - |   24576 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |         Tiff/huffman_rle_all_makeup_codes.tiff |    298.5 μs |   1,361.33 μs |     74.62 μs |  1.00 |    0.00 |     - |     - |     - |    2088 B |
|     &#39;ImageSharp Tiff&#39; | Job-KCUIVJ | .NET Core 2.1 |         Tiff/huffman_rle_all_makeup_codes.tiff |  5,717.5 μs |   2,533.21 μs |    138.85 μs | 19.89 |    4.51 |     - |     - |     - |   18328 B |
|                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
| &#39;System.Drawing Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |         Tiff/huffman_rle_all_makeup_codes.tiff |    159.5 μs |     140.52 μs |      7.70 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
|     &#39;ImageSharp Tiff&#39; | Job-NIWDJE | .NET Core 3.1 |         Tiff/huffman_rle_all_makeup_codes.tiff | 15,047.7 μs |   2,686.03 μs |    147.23 μs | 94.47 |    4.56 |     - |     - |     - |   18248 B |
