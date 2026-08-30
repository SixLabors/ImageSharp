# AV1 reconstruction conformance fixtures

These fixtures provide independent reference output for AV1 reconstruction and AVIF presentation tests. ImageSharp output is compared exactly with the retained native YUV planes and presented PNG files; the tests do not use a tolerance. Native fixtures remain beside their inputs here, while the PNG files use the repository reference-output naming contract under `tests/Images/External/ReferenceOutput/Av1ReconstructionConformanceTests`.

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
- `-libaom.y4m` files retain the Y4M header together with the native sequence frames selected for comparison.
- Reference-output `.png` files contain the eight-bit RGBA presentation produced by the pinned scalar libavif build. Their names combine the public test method, `Rgba32`, and the input AVIF basename so `CompareToReferenceOutput` resolves them directly.

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
| `libaom-av1-1-b8-00-quantizer-*`, `libaom-av1-1-b10-00-quantizer-*` | Official minimum- and maximum-quantizer dependent-frame reconstruction |
| `libaom-av1-1-b8-01-size-*` | Official frame-size matrix corner reconstruction |
| `libaom-av1-1-b10-23`, `libaom-av1-1-b10-24` | Official ten-bit dependent-frame film grain and monochrome sequence reconstruction |
| `libavif-progressive-draw-points-8b` | A real two-layer color item whose final frame uses single-reference inter reconstruction, plus its progressive auxiliary alpha item |
| `libavif-webp-logo-average-compound` | A 19-frame YUV444 image sequence whose retained references reach equal-weight compound inter reconstruction |
| `libavif-webp-logo-distance-weighted-compound` | Selectable distance-weighted compound prediction |
| `libavif-webp-logo-wedge-compound` | Wedge compound prediction with both signaled mask orientations |
| `libavif-webp-logo-difference-weighted-compound` | Difference-weighted compound prediction with both mask orientations |
| `libavif-webp-logo-inter-intra` | Smooth and wedge inter-intra prediction |
| `libavif-webp-logo-obmc` | Overlapping motion compensation through a 19-frame dependent sequence |
| `libavif-webp-logo-scaled-reference` | A 40x40 retained reference scaled into an 80x80 dependent frame |
| `libavif-rotating-grid-local-warp` | Multi-sample local affine projection and warped prediction through a two-frame dependent sequence |
| `libavif-rotating-grid-global-warp` | Non-translational rotation/zoom GLOBALMV prediction through a two-frame dependent sequence |

The corresponding tests also assert the syntax required by each family before comparing output. This prevents an inactive tool or an incorrectly substituted stream from passing solely because its final pixels happen to match.

## Official ten-bit sequence fixtures

The `libaom-av1-1-b10-23-film-grain-50.ivf` and `libaom-av1-1-b10-24-monochrome.ivf` streams are the official files from libaom's test-data bucket. Their SHA-1 values are `2F883C7E11C21A31F79BD9C809541BE90B0C7C4A` and `03A8D002594CCC51932332002BB6F9837EF46D0F`, exactly matching `test/test-data.sha1` at pinned libaom commit `03087864cf4bea6abb0d28f95cf7843511413d8f`. Their SHA-256 values are `C36CF5AB6A2E9E27C212C06863759B60791E3FA681A0800B5D57FD4192EF29CB` and `6A1B0729305A167F10737A5375F0570139F055BCD7916DF260B653AB2210ADC1`.

The retained native references were generated from the pinned generic libaom build with:

```text
aomdec --threads=1 --output=libaom-av1-1-b10-23-film-grain-50-libaom.y4m libaom-av1-1-b10-23-film-grain-50.ivf
aomdec --threads=1 --output=libaom-av1-1-b10-24-monochrome-libaom.y4m libaom-av1-1-b10-24-monochrome.ivf
```

The film-grain Y4M SHA-256 is `A1B553BE140F48ABDDB2A6D39917AB714BA03AC7FFD6359EAA1CB0D89C985A3B`, and the monochrome Y4M SHA-256 is `7394BC8146485D200BFDEC62E170482F8B1A85A64D21FAC62D10116FB1BB140D`. The tests decode and compare all ten frames from each sequence exactly under normal and scalar dispatch. The film-grain stream retains dependent ungrained references while applying ten-bit grain to each displayed 352x288 YUV420 frame; the monochrome stream verifies every 320x180 ten-bit luma sample without manufacturing chroma in ImageSharp. Both sequences also run through a constrained tracked allocator.

## Official quantizer-boundary fixtures

The retained `quantizer-00` and `quantizer-63` streams are the minimum- and maximum-quantizer boundaries from libaom's official eight- and ten-bit test matrices. Their SHA-1 values are `C2E1EC9936B95254187A359E94AA32A9F3DAD1B7`, `2A8AA33513D8E01AE9410C4BF5FE1E471B775482`, `9BBE8499796AA588FF02E313FB0D4349940D2FEA`, and `8B6EB3FFF2E0DB7EAC775B08C745250CA591E2D9`, exactly matching `test/test-data.sha1` at pinned libaom commit `03087864cf4bea6abb0d28f95cf7843511413d8f`. Their SHA-256 values, in the same order, are `6382DBD2BEFBBC93D4EA283586F4FB43FEA5F1C52400E3D2C5281A46B1104C00`, `0E4EC80680F7AF8DE9621B016E0F2D7C0858B2951DEBC173DDA50C6A051547D3`, `FE6053CE4EE20A1C0EC6F7FE35DB097E92AD25D8A3505598BD89162C74D7944F`, and `39759AB77483E1D11049DC38B5F5262158FD9C3CBC9D1F82A02462FC5DF30E0C`.

The native references were generated with the pinned generic `aomdec --threads=1` build. Their SHA-256 values are `D499028E0606DB70CD56A72F151E04F36C09F300A448CCCD8430DD920D3589C5`, `4CC9892B3EE3399B293E31014B9F566C21E0C7A4765FC5F444528769C33E6D67`, `78373C28F401EB95D3E563D146622ED6C714ED96661E5E57C539CE71D7BED599`, and `A9DF86F671B8CF01EFC130660556412D4EBAF31A81D6F26FBDAEB0A7E839D8EA`. Each reference's two raw-frame MD5 values also match the corresponding official `.ivf.md5` file exactly. The tests compare every native sample under normal and scalar `FeatureTestRunner` dispatch and run all four sequences through a 2,560-byte row-aligned constrained tracked allocator.

## Official frame-size corner fixtures

The retained 196x196, 196x226, 226x196, and 226x226 streams are the four corners of libaom's official eight-bit frame-size matrix. Their SHA-1 values are `9F386D19C87DBFD6AC84A06D2393DD88863AC003`, `5525F7E312EC073F480ED5A2BE5BDC4F0CE51A09`, `1A57B913443B267F4A31A6925C39F5B58022F550`, and `40DD208EB525CD90D7C0674CF787097FB909AFAE`, exactly matching `test/test-data.sha1` at pinned libaom commit `03087864cf4bea6abb0d28f95cf7843511413d8f`. Their SHA-256 values, in the same order, are `ECACF9C2065EEC1A02248395412BE5E03ED7E3FEDFD6653622E09E73D1CAC747`, `2A2ECDDE60546FA8280039228B4BE541825EFAEE30F53EE50B91B11F2952BDA7`, `999522FBF3FCD9F8CC8DAC865BEF874B7BF655A4DAD93C397F50A7A1EC2C9027`, and `B5D9A30F24E33F8FA6A655961645650542DC2545FB7D557E2D55A043DAC69F50`.

The native references were generated with the pinned generic `aomdec --threads=1` build. Their SHA-256 values are `4479030861DD9D6AB9B00FA8CF77A34712BCECA06C31131927D3E7E9BF5DDA70`, `817FF76E70946763C000E19FCCD9F0258CF9358201B6771AA68FB8C84490D26F`, `F4A70BA358E8B4ED558B589BFE56354C9552469FDABD01B3F4D5754334B0CBD9`, and `44FC32FB1D24CE4A33830B67D927F038E0E1379C6B480C598F018382EC78E9CE`. Each reference's two raw-frame MD5 values also match the corresponding official `.ivf.md5` file exactly. The tests compare every native sample under normal and scalar `FeatureTestRunner` dispatch and run all four sequences through a 1 KiB constrained tracked allocator.

## Progressive dependent-frame fixture

The `libavif-progressive-draw-points-8b.avif` fixture is the unmodified `tests/data/draw_points_idat_progressive.avif` file from the pinned libavif tree. Its SHA-256 is `077AB2AD1E46DD912A973E4F024CB1EB242A08298BE2DBF1A52A058E88C48A4A`. It was generated with:

```text
./avifenc -q 100 --progressive ../tests/data/draw_points.png ../tests/data/draw_points_idat_progressive.avif
```

The primary color item's `a1lx` property divides its logical 72-byte AV1 payload into a 55-byte base layer and a 17-byte dependent layer. The container stores those layers in separate `iloc` extents at AVIF offsets 511 and 583. The `.bit` fixture concatenates those two logical color extents; it does not copy the physically adjacent auxiliary-alpha extent between them.

Exact pinned libaom decodes the corrected logical payload into two 33x11 YUV444 frames. Both frames' 1,089 color samples match the corresponding first three planes of the pinned libavif YUV444-alpha outputs exactly. The retained Y4M contains both progressive YUV444-alpha frames, and the PNG contains pinned libavif's final RGBA presentation. The production-path test selects the second native frame, requires inter-coded blocks in the final ImageSharp frame, and compares both native color and final presentation without a tolerance.

## Equal-average compound fixture

The `libavif-webp-logo-average-compound.avif` file is retained solely as interoperability input. It was
created from `tests/data/webp_logo_animated.y4m` with the following command; libavif is not used as an
AV1 implementation or reconstruction reference:

```text
./avifenc -j 1 -c aom -s 4 -q 80 -a enable-dist-wtd-comp=0 -a enable-masked-comp=0 -a enable-interintra-comp=0 -a enable-obmc=0 -a enable-warped-motion=0 -a enable-global-motion=0 tests/data/webp_logo_animated.y4m libavif-webp-logo-average-compound.avif
```

On 2026-08-31 the clean official libaom `main` checkout was refreshed from its upstream remote. At the
observed revision `441c439b9916474cac15d2822af47a9ad70674a8`, current `aomdec` decoded the 5,465-byte
`mdat` payload at file offset 1,065 as 19 shown 80x80 YUV444 frames:

```text
aomdec --codec=av1 --threads=1 --row-mt=0 --output-bit-depth=8 -o compound-current-main.y4m compound-current-main.obu
```

All 19 frames decoded successfully. The final frame's 19,200 native samples have SHA-256
`E79D2F49C260B1AC9B1B9BBBB2D611126AFD3B241DA389EB9E7BD4EA0ED42080` and match the stored Y4M's
Y, U, and V samples exactly with zero differences. The observed revision records the source used for
this verification; it does not pin the libaom checkout.

The production test decodes every preceding sample to establish the retained-reference state, requires
actual equal-average compound blocks, and compares the final native planes exactly. It then compares the
final RGBA output through ImageSharp's established reference-output API. The PNG is presentation evidence
only and is not used to establish AV1 reconstruction arithmetic.

## Selectable compound and inter-intra fixtures

The four AVIF files are retained solely as interoperability inputs. Their original creation commands
used the same `tests/data/webp_logo_animated.y4m` source and encoded at speed zero after disabling later
inter-mode checkpoints. These commands record container-fixture provenance only; they are not codec
implementation references. Each command also disables competing prediction tools that would prevent the
resulting stream from isolating its named mode:

```text
./avifenc -j 1 -c aom -s 0 -q 80 -a enable-obmc=0 -a enable-warped-motion=0 -a enable-global-motion=0 -a enable-masked-comp=0 -a enable-interintra-comp=0 tests/data/webp_logo_animated.y4m libavif-webp-logo-distance-weighted-compound.avif
./avifenc -j 1 -c aom -s 0 -q 80 -a enable-obmc=0 -a enable-warped-motion=0 -a enable-global-motion=0 -a enable-dist-wtd-comp=0 -a enable-diff-wtd-comp=0 -a enable-interintra-comp=0 tests/data/webp_logo_animated.y4m libavif-webp-logo-wedge-compound.avif
./avifenc -j 1 -c aom -s 0 -q 80 -a enable-obmc=0 -a enable-warped-motion=0 -a enable-global-motion=0 -a enable-dist-wtd-comp=0 -a enable-interinter-wedge=0 -a enable-interintra-comp=0 tests/data/webp_logo_animated.y4m libavif-webp-logo-difference-weighted-compound.avif
./avifenc -j 1 -c aom -s 0 -q 80 -a enable-obmc=0 -a enable-warped-motion=0 -a enable-global-motion=0 -a enable-dist-wtd-comp=0 -a enable-masked-comp=0 tests/data/webp_logo_animated.y4m libavif-webp-logo-inter-intra.avif
```

| Fixture | AVIF SHA-256 | Retained frame-18 Y4M SHA-256 | Retained frame-18 PNG SHA-256 |
| --- | --- | --- | --- |
| `libavif-webp-logo-distance-weighted-compound` | `DA710D11C60F03EEA209E4360E2FC807B89C49AD50671F0DFB1BCF4AD5EF76DD` | `904D1B5B3E7F334CE8D44040F9A7BDCAC1F7773122FF1C5F06A5B4DD31A62A97` | `D2CB388C9092EF17C4F0382C0150DD30D6F9D0EE247FF45AB5D7D4D312CEB23C` |
| `libavif-webp-logo-wedge-compound` | `98640640A445055FEA3D9E2F4A78FEEF54E97F8171B472CF57D99156E1C553B2` | `904D1B5B3E7F334CE8D44040F9A7BDCAC1F7773122FF1C5F06A5B4DD31A62A97` | `D2CB388C9092EF17C4F0382C0150DD30D6F9D0EE247FF45AB5D7D4D312CEB23C` |
| `libavif-webp-logo-difference-weighted-compound` | `FC6459CD334762D74D9D2654640221E80C463CC01B82B29A5866C9E725ABE273` | `904D1B5B3E7F334CE8D44040F9A7BDCAC1F7773122FF1C5F06A5B4DD31A62A97` | `D2CB388C9092EF17C4F0382C0150DD30D6F9D0EE247FF45AB5D7D4D312CEB23C` |
| `libavif-webp-logo-inter-intra` | `71DF22E63626B5BC9001FF1E88076B90F11BB47D18089750853E66F0CBBB084B` | `502265688138641A7B12C8C4190B66C76CD4808D9AED05B06486056B39D7E9A0` | `F0DE4CCDFB6D95A400E69B69FA4C57F0BEEEEE75825722C31613385F0B3FD9FC` |

The inter-intra fixture's 5,327-byte AV1 `mdat` payload was decoded directly with the current official
libaom `main` checkout observed at `441c439b9916474cac15d2822af47a9ad70674a8`, using one thread and
with row threading disabled. Current `aomdec` produced all 19 YUV444 frames. The final frame's 19,200
native samples have SHA-256 `E8B776C2751DC30CA838931A4B74535FC6E681179568A1278747A38CFF2E5BFA`
and match the retained Y4M with zero differing samples.

The distance-weighted fixture's 5,372-byte AV1 `mdat` payload was decoded under the same current-libaom
conditions. Current `aomdec` produced all 19 YUV444 frames. The final frame's 19,200 native samples have
SHA-256 `E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match the retained
Y4M with zero differing samples.

The wedge fixture's 5,374-byte AV1 `mdat` payload was decoded under the same current-libaom conditions.
Current `aomdec` produced all 19 YUV444 frames. The final frame's 19,200 native samples have SHA-256
`E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match the retained Y4M
with zero differing samples.

The difference-weighted fixture's 5,358-byte AV1 `mdat` payload was decoded under the same current-libaom
conditions. Current `aomdec` produced all 19 YUV444 frames. The final frame's 19,200 native samples have
SHA-256 `E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match the retained
Y4M with zero differing samples.

The production tests independently require their decoded mode states. The distance-weighted input must
exercise distance-weighted compound prediction, the wedge and difference-weighted inputs must each
exercise both mask orientations, and the inter-intra input must exercise both smooth and wedge inter-intra
prediction. The tests decode all preceding samples, compare final native Y, Cb, and Cr planes exactly,
compare final RGBA presentation through ImageSharp's established reference-output API, and repeat
reconstruction with constrained tracked allocation. The retained PNG files are presentation evidence only
and are not AV1 reconstruction references.

## Overlapping motion-compensation fixture

The `libavif-webp-logo-obmc.avif` fixture is retained interoperability input. It was originally
packaged by libavif from the same `tests/data/webp_logo_animated.y4m` source as the compound fixtures,
with libaom's encoder selected and competing compound, inter-intra, warped, and global prediction modes
disabled:

```text
./avifenc -j 1 -c aom -s 0 -q 80 -a max-reference-frames=3 -a enable-dist-wtd-comp=0 -a enable-masked-comp=0 -a enable-interintra-comp=0 -a enable-warped-motion=0 -a enable-global-motion=0 tests/data/webp_logo_animated.y4m libavif-webp-logo-obmc.avif
```

On 2026-08-31 the fixture's 5,387-byte AV1 `mdat` payload at AVIF offset 1,065 was decoded with
`aomdec` from a freshly updated clean checkout of current official libaom `main`. One decode thread
was used and row threading remained disabled. All 19 frames decoded. The final frame's 19,200 native
YUV444 samples have SHA-256
`E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match
`libavif-webp-logo-obmc-libaom.y4m` with zero differing samples.

The production test requires decoded OBMC mode state, decodes every retained-reference dependency,
compares final Y, Cb, and Cr planes exactly, compares final RGBA presentation through ImageSharp's
established reference-output API under normal and scalar FeatureTestRunner dispatch, and repeats the
decode with constrained tracked allocation. Direct production-branch tests separately cover
above-then-left blending at 8/10/12-bit and 4:2:0 and 4:2:2 overlap geometry. The retained presentation
PNG's SHA-256 is
`D2CB388C9092EF17C4F0382C0150DD30D6F9D0EE247FF45AB5D7D4D312CEB23C`; its pixels were not changed
when its contract-derived filename was updated with the test name.

## Scaled-reference fixture

The `libavif-webp-logo-scaled-reference.avif` and
`libavif-webp-logo-scaled-reference-lsel0.avif` files are retained solely as interoperability inputs.
Their shared 2,195-byte `.bit` payload contains a 701-byte independent base layer followed by the
dependent layer; no container implementation is used as an AV1 arithmetic reference.

On 2026-08-31 the payload was decoded with `aomdec` from the freshly updated clean checkout of current
official libaom `main`, observed at `441c439b9916474cac15d2822af47a9ad70674a8`:

```text
aomdec --codec=av1 --threads=1 --row-mt=0 --all-layers --rawvideo --output-bit-depth=8 -o current-libaom-scaled-reference-all.yuv libavif-webp-logo-scaled-reference.bit
```

Current libaom produced a 40x40 YUV444 base frame and an 80x80 YUV444 dependent frame. The combined
24,000 native samples have SHA-256
`DD219E41B52C6C9343A92CD0A2D451DF57B73B25F10124811675B4CB2F8D666F`. The base frame matches
`libavif-webp-logo-scaled-reference-base-libaom.yuv` exactly, and the dependent frame matches the native
planes in `libavif-webp-logo-scaled-reference-libaom.y4m` exactly, with zero differing samples.

The production tests require the retained 40x40 base and the 80x80 dependent reconstruction, compare both
native frames exactly, compare the selected base layer and final RGBA presentation through ImageSharp's
established reference-output API, and repeat both layer selections with constrained tracked allocation.
The scaled-prediction FeatureTestRunner tests cover native and no-round compound output at 8, 10, and
12 bits, and a complete `Av1BlockDecoder.DecodeBlock()` test proves that scaled compound references remain
in the no-round intermediate domain until the final blend.

The two presentation PNGs were renamed with their current-libaom test contracts without changing their
bytes. Their SHA-256 values remain
`DC4C6DBE6BD92C5FCE1E3E23700AFA603EF04ED02EDD336213EBBA1E3BD84BA0` and
`678C5E5D4650EA6F0C590302E7DB9E3C6608851BC577453DA4A6837BDB4D3AF3`.

## Local warped-motion fixture

The `libavif-rotating-grid-local-warp.avif` file is retained solely as interoperability input. It was
originally encoded from a deterministic two-frame 256x256 limited-range YUV444 source whose second frame
rotates the first by 2.5 degrees. The original packaging command records fixture provenance only; libavif
is not used as an AV1 implementation or reconstruction reference:

```text
./avifenc -j 1 -s 0 -q 60 -a color:enable-warped-motion=1 -a color:enable-global-motion=0 -a color:enable-obmc=0 rotating-grid-256-two-frame.y4m libavif-rotating-grid-local-warp.avif
```

On 2026-08-31 the clean official libaom `main` checkout was refreshed from its upstream remote. At the
observed revision `441c439b9916474cac15d2822af47a9ad70674a8`, the fixture's 2,310-byte AV1 `mdat`
payload at AVIF offset 997 has SHA-256
`644D04FE1D1A32BB7A3856AD7EB49CF1EFDE0AC845E55BEAC4170F72353F2391` and was decoded with:

```text
aomdec --codec=av1 --threads=1 --row-mt=0 --all-layers --output-bit-depth=8 -o local-warp-current-main.y4m local-warp-current-main.obu
```

Current libaom produced two 256x256 YUV444 frames. The complete Y4M has SHA-256
`8FDC5D46014F5E5A7455A83643AB6F0DA66FC5A984E72A43F8C75BAD8271C299`. Its final
frame's 196,608 native samples have SHA-256
`47B2AB39BF3B9DA15C1EC59840F964DFDF227760947F6E1295FB38A84555F75C` and match the
retained `libavif-rotating-grid-local-warp-libaom.y4m` reference with zero differing samples.

The multi-sample model at mode-information row 6, column 8 derives matrix
`[-191565, 599107, 61755, -140, -6909, 62012]` and reduced shear
`[-3776, -128, -7360, -3520]` from four retained spatial neighbor samples. The production test requires
decoded `WARPED_CAUSAL` state through `Av1BlockDecoder.DecodeBlock()`, compares every final native Y,
U, and V sample exactly, compares the retained final presentation through the established image-reference
API, runs intrinsic and scalar dispatch through `FeatureTestRunner`, and repeats reconstruction with a
constrained tracked allocator. Separate 8-, 10-, and 12-bit operator tests use an independent scalar
transcription of current libaom's affine loops and all 1,544 current filter coefficients across AVX-512,
AVX, 128-bit, and scalar execution.

## Global warped-motion fixture

The `libavif-rotating-grid-global-warp.avif` fixture uses the same deterministic two-frame 256x256 limited-range YUV444 source and its `82C1468C95C996B05165590417184F59373D67896F8C7B0EB398582C29C9C7A7` SHA-256. Pinned libavif commit `062e582e8afda88e6baf988fdcf046a801efa0f5` and libaom commit `03087864cf4bea6abb0d28f95cf7843511413d8f` generated the fixture and references with:

```text
./avifenc -j 1 -s 0 -q 100 -a color:enable-warped-motion=0 -a color:enable-global-motion=1 -a color:enable-obmc=0 rotating-grid-256-two-frame.y4m libavif-rotating-grid-global-warp.avif
./avifdec -j 1 --index 1 libavif-rotating-grid-global-warp.avif libavif-rotating-grid-global-warp-libaom.y4m
./avifdec -j 1 --index 1 libavif-rotating-grid-global-warp.avif libavif-rotating-grid-global-warp-libavif.png
```

The AVIF SHA-256 is `EE8CDF6DF36FB2999A17D2A86C41040D8BE13B958A1D5AE53E37343C6FB49E0E`. The retained frame-1 Y4M SHA-256 is `A36D445778BB2D37D69526A1058E3569DA24F3913317EFE22ADB61748A0F7511`, and the frame-1 PNG SHA-256 is `F7D27ABF79450DFA311F72106FD1DA80997EABC0937F2F5578EF627119FF83B0`. Pinned libaom tracing records seven actual `GLOBALMV` blocks using the valid rotation/zoom matrix `[-357376, 372736, 65468, 2856, -2856, 65468]` and reduced shear `[-64, 2880, -2880, 64]`. The production sequence test requires that decoded model and mode state, compares every final native Y, U, and V sample and the final RGBA presentation exactly, runs normal and scalar dispatch through `FeatureTestRunner`, and repeats reconstruction with constrained tracked allocation. A direct production-branch test independently drives both references of `GLOBAL_GLOBALMV` through the matrix predictor and compound averaging at 8, 10, and 12 bits.

## Updating fixtures

Do not create conformance references with ImageSharp. Generate both the native-plane and presentation references with an independent decoder, record the exact upstream revisions and source license, and preserve exact comparisons. A new tool-specific fixture should demonstrate that the relevant syntax is active and should be no larger than required to cover that behavior.
