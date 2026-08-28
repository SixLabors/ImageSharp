# AV1 reconstruction conformance fixtures

These fixtures provide independent reference output for AV1 reconstruction and AVIF presentation tests. ImageSharp output is compared exactly with the retained native YUV planes and presented PNG files; the tests do not use a tolerance.

## Provenance

The source images and original AVIF files come from `libavif/tests/data` at commit `062e582e8afda88e6baf988fdcf046a801efa0f5`. Their licenses are recorded in libavif's `tests/data/README.md` and continue to apply to the derived fixtures. This includes the unrestricted Kodak image, the CC BY 3.0 Cosmos Laundromat frame, and files distributed under libavif's BSD-2-Clause license.

Reference files were generated with scalar builds of:

- libaom commit `03087864cf4bea6abb0d28f95cf7843511413d8f`;
- libavif 1.4.2 from commit `062e582e8afda88e6baf988fdcf046a801efa0f5`, linked to that libaom build.

The reference builds use `AOM_TARGET_CPU=generic` and disable libyuv. Native reconstruction therefore comes from libaom, and AVIF presentation comes from libavif's own conversion path, without architecture-specific SIMD or ImageSharp code.

## File conventions

- `.avif` files exercise the complete container and presentation path.
- `.bit` files contain the exact AV1 elementary-stream payload used by reconstruction tests.
- `-libaom.yuv` files contain headerless planar Y, U, and V reference samples. Samples above eight bits are stored as little-endian 16-bit values.
- `-libaom-y4m.yuv` files retain the Y4M header together with the native planar frame.
- `.png` files contain the eight-bit RGBA presentation reference produced by the pinned scalar libavif build.

## Coverage

| Fixture family | Coverage |
| --- | --- |
| `libavif-kodim23`, `libavif-cosmos1650`, `libaom-cosmos1650` | Baseline 8-, 10-, and 12-bit reconstruction, chroma subsampling, and active deblocking |
| `*-cdef-*` | Active CDEF with loop restoration disabled |
| `*-superres-*` | Active horizontal super-resolution with CDEF and restoration disabled |
| `*-restoration-*` | Wiener and self-guided loop restoration |
| `*-restoration-superres-*` | Restoration after super-resolution, including 10-bit 4:2:2 clipped-edge transform coverage |
| `libavif-profile-*` | The 8-, 10-, and 12-bit matrix across monochrome, 4:2:0, 4:2:2, and 4:4:4 |
| `*-palette-*` | Luma and chroma palette prediction |
| `*-intrabc-*` | Intra-block copy at every supported bit depth |
| `*-lossless-*` | Lossless quantization, reversible transforms, and exact presentation |
| `*-film-grain-*` | Full and restricted range, monochrome, identity matrix, 8/10/12-bit synthesis, overlap, and odd frame dimensions |
| `libavif-progressive-draw-points-8b` | A real two-layer color item whose final frame uses single-reference inter reconstruction, plus its progressive auxiliary alpha item |
| `libavif-webp-logo-average-compound` | A 19-frame YUV444 image sequence whose retained references reach equal-weight compound inter reconstruction |

The corresponding tests also assert the syntax required by each family before comparing output. This prevents an inactive tool or an incorrectly substituted stream from passing solely because its final pixels happen to match.

## Progressive dependent-frame fixture

The `libavif-progressive-draw-points-8b.avif` fixture is the unmodified `tests/data/draw_points_idat_progressive.avif` file from the pinned libavif tree. Its SHA-256 is `077AB2AD1E46DD912A973E4F024CB1EB242A08298BE2DBF1A52A058E88C48A4A`. It was generated with:

```text
./avifenc -q 100 --progressive ../tests/data/draw_points.png ../tests/data/draw_points_idat_progressive.avif
```

The primary color item's `a1lx` property divides its logical 72-byte AV1 payload into a 55-byte base layer and a 17-byte dependent layer. The container stores those layers in separate `iloc` extents at AVIF offsets 511 and 583. The `.bit` fixture concatenates those two logical color extents; it does not copy the physically adjacent auxiliary-alpha extent between them.

Exact pinned libaom decodes the corrected logical payload into two 33x11 YUV444 frames. Both frames' 1,089 color samples match the corresponding first three planes of the pinned libavif YUV444-alpha outputs exactly. The retained Y4M contains both progressive YUV444-alpha frames, and the PNG contains pinned libavif's final RGBA presentation. The production-path test selects the second native frame, requires inter-coded blocks in the final ImageSharp frame, and compares both native color and final presentation without a tolerance.

## Equal-average compound fixture

The `libavif-webp-logo-average-compound.avif` fixture was encoded from the pinned libavif tree's `tests/data/webp_logo_animated.y4m` source. The source SHA-256 is `0872208D9C19B68B10A1647FA6849CFC4E2B21A19561ACD672E0629C70EFACA2`. It was generated with:

```text
./avifenc -j 1 -c aom -s 4 -q 80 -a enable-dist-wtd-comp=0 -a enable-masked-comp=0 -a enable-interintra-comp=0 -a enable-obmc=0 -a enable-warped-motion=0 -a enable-global-motion=0 tests/data/webp_logo_animated.y4m libavif-webp-logo-average-compound.avif
```

Pinned scalar libavif generated the retained references with:

```text
./avifdec -j 1 -c aom --index 18 libavif-webp-logo-average-compound.avif libavif-webp-logo-average-compound-libaom.y4m
./avifdec -j 1 -c aom --index 18 libavif-webp-logo-average-compound.avif libavif-webp-logo-average-compound-libavif.png
```

The AVIF SHA-256 is `7919049D367EEDB7C965E170309D6759660DDBFD4BB1AEF9496F9D66E314846A`. The retained frame-18 Y4M SHA-256 is `41FF2408DEB473D5483F3398882DF7F7AB6C7D376561C19798881595EB0C5C0C`, and the frame-18 PNG SHA-256 is `BCFABC1E1C7E17D8ECB40569849A04FFAC6CA1FCDF613F217B33816CA47337AC`. The test decodes every preceding hidden and shown sample to establish the same retained-reference state before comparing all native Y, U, and V samples and the final RGBA presentation.

## Updating fixtures

Do not create conformance references with ImageSharp. Generate both the native-plane and presentation references with an independent decoder, record the exact upstream revisions and source license, and preserve exact comparisons. A new tool-specific fixture should demonstrate that the relevant syntax is active and should be no larger than required to cover that behavior.
