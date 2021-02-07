
BenchmarkDotNet=v0.12.1, OS=Windows 10.0.19042
Intel Core i7-3610QM CPU 2.30GHz (Ivy Bridge), 1 CPU, 8 logical and 4 physical cores
.NET Core SDK=5.0.101
  [Host]     : .NET Core 5.0.1 (CoreCLR 5.0.120.57516, CoreFX 5.0.120.57516), X64 RyuJIT
  Job-MVPLTM : .NET Framework 4.8 (4.8.4300.0), X64 RyuJIT
  Job-ZMKWLH : .NET Core 2.1.23 (CoreCLR 4.6.29321.03, CoreFX 4.6.29321.01), X64 RyuJIT
  Job-DYSEOC : .NET Core 3.1.10 (CoreCLR 4.700.20.51601, CoreFX 4.700.20.51901), X64 RyuJIT

InvocationCount=1  IterationCount=3  LaunchCount=1  
UnrollFactor=1  WarmupCount=3  

                Method |        Job |       Runtime |                                      TestImage |        Mean |         Error |       StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
---------------------- |----------- |-------------- |----------------------------------------------- |------------:|--------------:|-------------:|------:|--------:|------:|------:|------:|----------:|
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** |    **Tiff/Calliphora_grayscale_uncompressed.tiff** |  **1,513.5 μs** |   **6,982.54 μs** |    **382.74 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |  **974848 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 |    Tiff/Calliphora_grayscale_uncompressed.tiff | 29,504.9 μs |   2,030.88 μs |    111.32 μs | 20.46 |    5.74 |     - |     - |     - |   32768 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff |  1,441.5 μs |   5,692.62 μs |    312.03 μs |  1.00 |    0.00 |     - |     - |     - |  968832 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff | 12,512.9 μs |     669.83 μs |     36.72 μs |  8.98 |    2.11 |     - |     - |     - |   30072 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff |    989.7 μs |   1,621.23 μs |     88.87 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 |    Tiff/Calliphora_grayscale_uncompressed.tiff | 44,038.5 μs |   7,702.48 μs |    422.20 μs | 44.75 |    4.23 |     - |     - |     - |   29992 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** |     **Tiff/Calliphora_rgb_deflate_predictor.tiff** | **16,368.6 μs** |   **3,342.63 μs** |    **183.22 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **1483440 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 27,334.9 μs |   5,327.53 μs |    292.02 μs |  1.67 |    0.04 |     - |     - |     - |  848240 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 16,192.2 μs |   4,200.11 μs |    230.22 μs |  1.00 |    0.00 |     - |     - |     - | 1480344 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 18,448.9 μs |   3,957.52 μs |    216.93 μs |  1.14 |    0.00 |     - |     - |     - |   68224 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 15,936.7 μs |   3,145.57 μs |    172.42 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 |     Tiff/Calliphora_rgb_deflate_predictor.tiff | 18,973.7 μs |  16,625.54 μs |    911.30 μs |  1.19 |    0.06 |     - |     - |     - |   65168 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** |         **Tiff/Calliphora_rgb_lzw_predictor.tiff** | **81,687.4 μs** |   **6,229.79 μs** |    **341.48 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **2545736 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 67,259.5 μs |   4,315.22 μs |    236.53 μs |  0.82 |    0.01 |     - |     - |     - |   24576 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 81,554.2 μs |   9,082.88 μs |    497.86 μs |  1.00 |    0.00 |     - |     - |     - | 2541376 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 43,966.2 μs |   3,806.49 μs |    208.65 μs |  0.54 |    0.00 |     - |     - |     - |   23880 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 80,333.6 μs |  24,190.59 μs |  1,325.97 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 |         Tiff/Calliphora_rgb_lzw_predictor.tiff | 54,418.0 μs | 122,629.27 μs |  6,721.72 μs |  0.68 |    0.09 |     - |     - |     - |   23848 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** |              **Tiff/Calliphora_rgb_packbits.tiff** |  **3,554.1 μs** |   **2,577.75 μs** |    **141.30 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **2916000 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 |              Tiff/Calliphora_rgb_packbits.tiff |  7,231.9 μs |   3,934.62 μs |    215.67 μs |  2.04 |    0.08 |     - |     - |     - |   57344 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 |              Tiff/Calliphora_rgb_packbits.tiff |  3,815.4 μs |  11,074.41 μs |    607.03 μs |  1.00 |    0.00 |     - |     - |     - | 2903544 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 |              Tiff/Calliphora_rgb_packbits.tiff |  4,415.6 μs |   7,272.82 μs |    398.65 μs |  1.17 |    0.08 |     - |     - |     - |   51920 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 |              Tiff/Calliphora_rgb_packbits.tiff |  3,297.6 μs |   5,129.08 μs |    281.14 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 |              Tiff/Calliphora_rgb_packbits.tiff |  4,421.7 μs |   3,349.14 μs |    183.58 μs |  1.34 |    0.07 |     - |     - |     - |   51848 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** | **Tiff/Calliphora_rgb_palette_lzw_predictor.tiff** | **60,458.2 μs** |  **21,405.09 μs** |  **1,173.29 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |  **827416 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 76,324.5 μs |  12,909.45 μs |    707.61 μs |  1.26 |    0.04 |     - |     - |     - |   49152 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 61,210.5 μs |  17,165.24 μs |    940.88 μs |  1.00 |    0.00 |     - |     - |     - |  825648 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 46,951.4 μs |   1,602.53 μs |     87.84 μs |  0.77 |    0.01 |     - |     - |     - |   45984 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 59,056.7 μs |   6,187.79 μs |    339.17 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 | Tiff/Calliphora_rgb_palette_lzw_predictor.tiff | 66,042.9 μs | 291,880.02 μs | 15,998.93 μs |  1.12 |    0.27 |     - |     - |     - |   45912 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** |          **Tiff/Calliphora_rgb_uncompressed.tiff** |  **3,385.5 μs** |   **6,266.60 μs** |    **343.49 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** | **2915968 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 |          Tiff/Calliphora_rgb_uncompressed.tiff |  7,584.8 μs |     358.27 μs |     19.64 μs |  2.25 |    0.21 |     - |     - |     - |   57344 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  3,405.8 μs |   4,765.81 μs |    261.23 μs |  1.00 |    0.00 |     - |     - |     - | 2905840 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  3,930.3 μs |   3,250.19 μs |    178.15 μs |  1.16 |    0.05 |     - |     - |     - |   51568 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  3,087.8 μs |   5,556.58 μs |    304.58 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 |          Tiff/Calliphora_rgb_uncompressed.tiff |  3,909.1 μs |   2,519.27 μs |    138.09 μs |  1.27 |    0.14 |     - |     - |     - |   51496 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** |     **Tiff/ccitt_fax3_all_terminating_codes.tiff** |    **151.9 μs** |      **73.26 μs** |      **4.02 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |    **8192 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    648.7 μs |     165.54 μs |      9.07 μs |  4.27 |    0.08 |     - |     - |     - |   24576 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    160.6 μs |      80.44 μs |      4.41 μs |  1.00 |    0.00 |     - |     - |     - |    2032 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    427.7 μs |     431.89 μs |     23.67 μs |  2.66 |    0.17 |     - |     - |     - |   17952 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    230.1 μs |     952.83 μs |     52.23 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 |     Tiff/ccitt_fax3_all_terminating_codes.tiff |    945.4 μs |     511.67 μs |     28.05 μs |  4.26 |    1.00 |     - |     - |     - |   17872 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 **'System.Drawing Tiff'** | **Job-MVPLTM** |    **.NET 4.7.2** |         **Tiff/huffman_rle_all_makeup_codes.tiff** |    **188.4 μs** |     **609.38 μs** |     **33.40 μs** |  **1.00** |    **0.00** |     **-** |     **-** |     **-** |    **8192 B** |
     'ImageSharp Tiff' | Job-MVPLTM |    .NET 4.7.2 |         Tiff/huffman_rle_all_makeup_codes.tiff |  8,870.6 μs |   2,847.17 μs |    156.06 μs | 48.06 |    8.32 |     - |     - |     - |   24576 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-ZMKWLH | .NET Core 2.1 |         Tiff/huffman_rle_all_makeup_codes.tiff |    222.0 μs |     620.73 μs |     34.02 μs |  1.00 |    0.00 |     - |     - |     - |    2088 B |
     'ImageSharp Tiff' | Job-ZMKWLH | .NET Core 2.1 |         Tiff/huffman_rle_all_makeup_codes.tiff |  5,660.9 μs |   3,414.81 μs |    187.18 μs | 25.92 |    4.17 |     - |     - |     - |   18432 B |
                       |            |               |                                                |             |               |              |       |         |       |       |       |           |
 'System.Drawing Tiff' | Job-DYSEOC | .NET Core 3.1 |         Tiff/huffman_rle_all_makeup_codes.tiff |    176.5 μs |     227.25 μs |     12.46 μs |  1.00 |    0.00 |     - |     - |     - |     176 B |
     'ImageSharp Tiff' | Job-DYSEOC | .NET Core 3.1 |         Tiff/huffman_rle_all_makeup_codes.tiff | 14,251.6 μs |     597.28 μs |     32.74 μs | 81.00 |    5.71 |     - |     - |     - |   18352 B |
