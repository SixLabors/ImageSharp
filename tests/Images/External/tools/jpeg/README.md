### dump-jpeg-coeffs.exe
Usage:
```
dump-jpeg-coeffs <input.jpg> [output.dctdump]
```

Dumps the raw DCT blocks of the input image into a binary file. The output file follows the following liear layout:
1. The number of components as `Int16`
2. For each component: (2.1) widthInBlocks as `Int16` (2.2) heightInBlocks as `Int16`
3. The block data as a raw `Int16` dump

The source code could be found here:
https://github.com/antonfirsov/libjpeg-turbo/blob/dump-jpeg-coeffs_/jcstest.cpp
