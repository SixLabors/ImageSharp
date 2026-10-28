### Some useful links for documentation about the OpenEXR format:

- [Technical Introduction](https://openexr.readthedocs.io/en/latest/TechnicalIntroduction.html)
- [OpenExr file layout](https://openexr.readthedocs.io/en/latest/OpenEXRFileLayout.html)

## Implementation Status

The encoder writes scanline images with three color channels and alpha. The
decoder reads scanline images with one luminance channel or three color channels,
with or without alpha.

Tiled images are not supported. A default `Image.Load` keeps HALF and FLOAT
samples in floating-point pixel formats; loading into an explicitly requested
bounded pixel format applies that format's conversion rules.

### Pixel Sample Types

| Sample type | Encoder | Decoder |
|-------------|:-------:|:-------:|
| HALF        |    Y    |    Y    |
| FLOAT       |    Y    |    Y    |
| UINT        |    Y    |    Y    |

### Compression Formats

| Compression | Encoder | Decoder |
|-------------|:-------:|:-------:|
| None        |    Y    |    Y    |
| RLE         |         |    Y    |
| ZIPS        |    Y    |    Y    |
| ZIP         |    Y    |    Y    |
| PIZ         |         |         |
| PXR24       |         |    Y    |
| B44         |         |    Y    |
| B44A        |         |         |
