Encoder/Decoder adapted from:

https://github.com/yufeih/Nine.Imaging/
https://imagetools.codeplex.com/


Test images at:

- BMP Suite v1.0: http://www.hassconsult.co.ke/dompdf/www/test/image_bmp.html
- BMP Suite v2.5: http://entropymine.com/jason/bmpsuite/bmpsuite/html/bmpsuite.html
- Microsoft Windows Bitmap Sample Files: http://www.fileformat.info/format/bmp/sample/index.htm


# Supported BMP File Formats

## Loading/Decoding

### IBM OS/2 v1

> **See Microsoft Windows v2**

### IBM OS/2 v2

**Monocrome** (1-bit per pixel)

- [ ] Uncompressed
- [ ] Huffman 1D Compressed
  - [ ] No Halftoning
  - [ ] Error Diffusion Halftoning
  - [ ] PANDA Halftoning
  - [ ] Super Circle Halftoning

**16 Colors Palette** (4-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-4 Compressed

**256 Colors Palette** (8-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-8 Compressed

**24-bit RGB** (8-bit per pixel color)

- [X] Uncompressed - BGR888
- [ ] RLE-24 Compressed

### Microsoft Windows v1

>**Not supported** - Uses a diferente FileHeader and DDB (Device-Dependent Bitmap) instead of DIB (Device-Independent Bitmap)

### Microsoft Windows v2

**16 Colors Palette** (4-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-4 Compressed

**256 Colors Palette** (8-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-8 Compressed

**24-bit RGB** (8-bit per pixel color)

- [ ] Uncompressed - BGR888

### Microsoft Windows v3 to v5

**Extra Features**

- [X] Top-down DIBs for uncompressed files
- [ ] 90º degrees rotated DIBs for Windows CE files
- [ ] Bitfield masks on v3 DIBs for Windows NT and Windows CE files
- [ ] Gamma correction for v4 and v5 DIBs
- [ ] ICC color spaces profiles for v5 DIBs

**PNG**

- [ ] PNG Compressed

**JPEG**

- [ ] JPEG Compressed

**Monocrome** (1-bit per pixel)

- [ ] Uncompressed

**4 Colors Palette** (2-bit per pixel)

- [ ] Uncompressed

**16 Colors Palette** (4-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-4 Compressed

**256 Colors Palette** (8-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-8 Compressed

**16-bit RGB** (5-bit per pixel color)

- [ ] Uncompressed - BGR555
- [ ] Bitfields

**24-bit RGB** (8-bit per pixel color)

- [X] Uncompressed - BGR888

**32-bit RGBA** (8-bit per pixel color + 8-bit Alpha)

- [X] Uncompressed - BGRA8880
- [ ] Bitfields

## Saving/Encoding

### IBM OS/2 v1

> **See Microsoft Windows v2**

### IBM OS/2 v2

**Monocrome** (1-bit per pixel)

- [ ] Uncompressed
- [ ] Huffman 1D Compressed
  - [ ] No Halftoning
  - [ ] Error Diffusion Halftoning
  - [ ] PANDA Halftoning
  - [ ] Super Circle Halftoning

**16 Colors Palette** (4-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-4 Compressed

**256 Colors Palette** (8-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-8 Compressed

**24-bit RGB** (8-bit per pixel color)

- [ ] Uncompressed - BGR888
- [ ] RLE-24 Compressed

### Microsoft Windows v1

>**Not supported** - Uses a diferente FileHeader and DDB (Device-Dependent Bitmap) instead of DIB (Device-Independent Bitmap)

### Microsoft Windows v2

**16 Colors Palette** (4-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-4 Compressed

**256 Colors Palette** (8-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-8 Compressed

**24-bit RGB** (8-bit per pixel color)

- [ ] Uncompressed - BGR888

### Microsoft Windows v3 to v5

**Extra Features**

- [X] Top-down DIBs for uncompressed files
- [ ] 90º degrees rotated DIBs for Windows CE files
- [ ] Bitfield masks on v3 DIBs for Windows NT and Windows CE files
- [ ] Gamma correction for v4 and v5 DIBs
- [ ] ICC color spaces profiles for v5 DIBs

**PNG**

- [ ] PNG Compressed

**JPEG**

- [ ] JPEG Compressed

**Monocrome** (1-bit per pixel)

- [ ] Uncompressed

**4 Colors Palette** (2-bit per pixel)

- [ ] Uncompressed

**16 Colors Palette** (4-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-4 Compressed

**256 Colors Palette** (8-bit per pixel)

- [ ] Uncompressed
- [ ] RLE-8 Compressed

**16-bit RGB** (5-bit per pixel color)

- [ ] Uncompressed - BGR555
- [ ] Bitfields

**24-bit RGB** (8-bit per pixel color)

- [X] Uncompressed - BGR888

**32-bit RGBA** (8-bit per pixel color + 8-bit Alpha)

- [X] Uncompressed - BGRA8880
- [ ] Bitfields
