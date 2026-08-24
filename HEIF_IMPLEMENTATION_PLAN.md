# HEIF family implementation plan

## Goal

Complete a production-quality, fully managed HEIF family implementation for ImageSharp. The implementation must support HEIC, HIF/HEIF, and AVIF files, interoperate with independent encoders and decoders, follow the existing ImageSharp architecture and code style, reuse existing ImageSharp infrastructure wherever its semantics match, and use SIMD for measured hot paths without maintaining a separate behavior model.

HEIF is the shared ISO BMFF-derived container. HEIC carries HEVC image items, AVIF carries AV1 image items, and `.hif`/`.heif` are container extensions whose payload codec must be determined from brands and item types rather than the filename. The completed implementation will support HEVC, AV1, and legacy JPEG image items. Other registered HEIF payload codecs must not be advertised unless they are implemented and independently verified.

## Completion boundary

This plan has one PR completion gate. The phases below are dependency order and internal verification points only; none is a separately releasable or merge-complete subset. The PR is not complete until the complete matrix is implemented and independently verified.

Full completion includes:

- correct HEIF, HEIC, HEVC-sequence, AVIF, and AVIF-sequence brand and item-type detection without relying on file extensions.
- still-image and image-sequence decode and encode for HEVC/HEIC and AV1/AVIF.
- standards-compliant legacy JPEG image-item decode and encode for HEIF/HIF files.
- every bit depth and chroma format permitted by the HEVC profiles exposed for HEIC and the AV1 profiles exposed for AVIF, including 8, 10, and 12-bit and monochrome, YUV 4:2:0, 4:2:2, and 4:4:4 paths.
- full- and limited-range conversion using every valid signaled color-primary, transfer-characteristic, matrix-coefficient, and chroma-sample-position combination, including identity RGB signaling.
- decoding every normative AV1 compression tool that can occur in a conforming AVIF item or sequence; valid syntax cannot terminate in an unsupported branch or silently skip reconstruction.
- a real lossy and lossless AV1 encoder with working quality and effort controls, complete mode decision, prediction, transform, quantization, entropy coding, and legal in-loop filter decisions. A permanently fixed smallest-valid coding subset is not complete.
- decoding every normative HEVC compression tool that can occur in a conforming HEIC image item or sequence across the exposed profiles, including the range-extension tools required for high bit depth and 4:2:2/4:4:4.
- a real HEVC encoder with working lossless/lossy quality and effort controls, complete coding-tree, prediction, transform, quantization, CABAC, deblocking, and sample-adaptive-offset decisions. A permanently fixed smallest-valid coding subset is not complete.
- primary images, alpha auxiliary images, image grids, ICC and CICP color information, Exif, XMP, pixel aspect ratio, clean aperture, rotation, and mirroring.
- independent AVIF interoperability with libavif and libaom, and independent HEIC interoperability with a separately selected HEVC/HEIF implementation.

Gain maps, progressive/layered images, sample transforms, and experimental extension brands require explicit conformance and API decisions. They do not create permission to omit any valid color, compression, or bit-depth path from the PR. The container reader must skip unsupported optional extensions safely and reject an unsupported essential property with a useful error.

## Reference hierarchy

Use the references in this order when behavior differs:

1. The published ISO BMFF, HEIF, HEVC, AV1, AV1-ISOBMFF, and AVIF requirements are normative.
2. The official [AOM AV1 Codec Library](https://aomedia.googlesource.com/aom/) is the primary implementation reference for AV1 decode, encode, high-bit-depth behavior, tests, and optimized scalar/SIMD algorithms. Pin one reviewed commit before porting. Use its scalar C paths as behavioral references and its architecture-specific paths as SIMD references to be expressed with ImageSharp's existing managed intrinsics.
3. The local `D:\GitHub\AOMediaCodec\libavif` checkout is the AVIF container, metadata, color-conversion, grid, alpha, and interoperability oracle. At inspection time it identifies itself as 1.4.2-devel.
4. A separately reviewed, license-compatible HEVC implementation reference must be pinned before HEVC porting begins. The HEVC specification remains normative, and independent HEIC software is required as an interoperability oracle. Do not copy from GPL or otherwise incompatible sources.
5. Existing ImageSharp codecs are the authority for ImageSharp API shape, memory ownership, stream behavior, cancellation, resource limits, pixel conversion, tests, and SIMD dispatch.

The linked ImageSharp discussion establishes the project constraint: the shipped implementation is purely managed and other codec libraries are references, not native runtime dependencies. libaom is the official encoder/decoder implementation reference for AV1, but it does not parse the HEIF container or implement HEVC. libavif dispatches AV1 work to external codec libraries, so it remains an observable AVIF/container oracle rather than the source for every AV1 algorithm.

The AOM source is distributed under the BSD 2-Clause License and the Alliance for Open Media Patent License 1.0. Before porting further code, record the exact upstream file, commit, applicable license/patent notice, and corresponding managed file or method. Audit the existing SVT-AV1-attributed WIP separately rather than relabeling it as libaom-derived. Update `THIRD-PARTY-NOTICES.TXT` before any referenced implementation code is merged. A pure managed HEVC implementation does not remove HEVC patent or licensing obligations, so those must be resolved before the HEIC work is considered releasable.

### Pinned reference and baseline snapshot

The initial post-merge snapshot was established on 2026-08-24:

- the official libaom reference is tag `v3.14.1`, commit `03087864cf4bea6abb0d28f95cf7843511413d8f`, matching the revision selected by the local libavif `ext/aom.cmd` dependency script;
- the local libavif container, color-conversion, and interoperability oracle is commit `092276ce89098ead06db80975173191e5fee1826`, described as `v1.4.2-66-g092276ce`;
- the independently reviewed HEVC implementation and interoperability references remain unresolved and must be pinned before HEVC algorithm work begins;
- `dotnet build ImageSharp.sln -c Release --no-restore -m:1 -v minimal` succeeds with no errors after the upstream compatibility fixes; and
- the existing HEIF-focused test run executes 8,198 cases, with 8,184 passing and 14 failing. Thirteen failures are isolated to the WIP AV1 YUV conversion tests, and one is the existing legacy JPEG HIF reference-image mismatch. Golden artifacts have not been changed.

This snapshot pins or classifies the available references and failures; it does not complete Phase 0. The full WIP provenance map, disabled-test inventory, HEVC reference selection, and feature-state matrix remain required.

### Provenance map in progress

| Managed implementation | Normative behavior | Reviewed implementation reference | Use |
| --- | --- | --- | --- |
| `Av1YuvConverter.ConvertToRgb`, `ConvertFromRgb`, scalar row conversion, and chroma reconstruction | H.273 formulas 20-31 and the identity, YCgCo, and non-constant-luminance matrix formulas; AV1 section 6.4.2 chroma sample positions | libavif `src/reformat.c` and `src/colr.c` at `092276ce89098ead06db80975173191e5fee1826`; libaom `aom/aom_image.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Scalar behavioral oracle for 8-bit full/limited-range conversion. Decode covers monochrome, YUV 4:2:0, 4:2:2, and 4:4:4 with AV1 chroma sample positioning; encode remains YUV 4:4:4 at this snapshot. Later high-bit-depth and SIMD paths must match it. |
| `Av1FrameBuffer` high-bit-depth sample layout and `Av1YuvConverter` 10/12-bit output conversion | AV1 section 6.4.1 bit depth and H.273 sample-range scaling | libaom `aom_scale/yv12config.h`, `av1/common/idct.c`, and `av1/common/reconintra.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f`; libavif `src/avif.c` and `src/reformat.c` at `092276ce89098ead06db80975173191e5fee1826` | Establish two-byte native sample storage with sample-unit strides for 10/12-bit reconstruction and use the same scalar color model at every supported bit depth. |
| `Av1PredictionDecoder` and the scalar DC, directional, Paeth, and smooth intra predictors | AV1 section 7.11.2 intra prediction | libaom `aom_dsp/intrapred.c` and `av1/common/reconintra.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Behavioral oracle for neighbor addressing, directional upsampling, Paeth selection, one-axis smooth normalization, and chroma-from-luma row strides. Existing managed scalar predictors remain the implementation base. The WIP rectangular smooth digest expectations encode width/height-swapped weights and must be replaced only from an independently generated oracle, not regenerated from this implementation. |

This table is intentionally incomplete. Add a row before each additional AV1 or HEVC algorithm is ported or materially reshaped.

## Current implementation assessment

This assessment is based on the current source after the upstream ImageSharp merge and the baseline recorded above. Unless a result is stated explicitly, each item is a source-inspection finding rather than a verified interoperability claim.

### Public integration

- `HeifFormat` combines the HEIF, HEIC, HIF, and AVIF identities and extensions, but the implementation does not yet decode all payloads that contract implies.
- `HeifDecoder` defaults to `Rgb24`, which cannot preserve decoded alpha.
- `HeifMetadata` reports fixed 8-bit, three-component RGB metadata rather than the decoded HEVC/AV1 and item properties.
- `IHeifEncoderOptions` is empty, and the encoder exposes no meaningful quality, speed, lossless, subsampling, bit-depth, or alpha policy.
- HEIF/HEIC/AVIF is absent from the format source-generation list in `_Formats.ttinclude`, so the standard ImageSharp save extensions are not generated.
- Configuration registration exists, but it currently registers capabilities broader than the implementation provides.

### HEIF/ISO BMFF container

- The reader accepts a narrow set of top-level boxes and throws for unknown boxes that should be skipped when they are not essential.
- File type handling checks only a small set of major brands and does not fully evaluate compatible brands.
- Item storage assumes item identifiers can index a list. Item IDs are keys and need not be contiguous or zero-based.
- Item property associations use property indices as collection indices without consistently applying the format's one-based indexing rules. The essential flag and index masks also need correction and version-specific tests.
- Item location handling effectively assumes one extent, convenient box order, and data in the current `mdat`. It does not provide a general, bounded resolver for `idat`, `mdat`, multiple extents, construction methods, and 64-bit offsets.
- The grid decoder selected for a grid item is immediately overwritten by the compression factory result. `GridHeifItemDecoder` then returns a placeholder 1x1 image instead of composing validated tiles.
- HEVC and AV1 configuration, CICP color information, alpha auxiliary items, Exif/XMP, transforms, and several item/property relationships are missing or parsed without affecting output.
- Identify and decode do not share a complete, immutable parsed model, and identify does not consistently validate the file type result.

### HEVC decoder and encoder

- `Heif4CharCode` recognizes `hvc1` image items and `hvcC` configuration, and Identify classifies HEVC fixtures, but `HeifCompressionFactory` has no HEVC item decoder.
- There is no HEVC bitstream parser, CABAC decoder, coding-tree reconstruction, intra/inter prediction, inverse transform, deblocking, sample-adaptive offset, high-bit-depth path, or image-sequence reference-frame implementation.
- There is no HEVC encoder. The current HEIC-branded encoder writes a legacy JPEG payload and therefore cannot provide HEIC output.
- Existing HEVC tests prove container identification only; they do not decode or compare HEIC pixels.

### AV1 decoder

- `Av1Decoder.ReadTile()` checks `tileReader` in the branch where it is known to be null, so the first tile cannot reach reconstruction.
- Decoder state is allocated or replaced at multiple points, making parsed tile state, reconstructed frame state, and ownership unclear.
- The reconstruction pipeline disables loop filtering, CDEF, super-resolution, loop restoration, and padding with constant flags. These are normative stages when signaled, not optional quality improvements.
- Loop restoration, filter intra prediction, palette paths, `show_existing_frame`, reference/CDF state, and other syntax paths contain `NotImplementedException` or equivalent unsupported branches.
- The frame buffer now establishes two-byte native sample storage, logical plane rows, and sample-unit block strides for 10/12-bit frames. The active prediction and block reconstruction path is still limited to 8-bit samples and must be connected to the existing high-bit-depth inverse-transform core.
- `Av1YuvConverter` now consumes the signaled range, supported H.273 matrix coefficients, subsampling, and chroma sample position for 8, 10, and 12-bit output and uses one allocator-backed RGB row. Constant-luminance and chromaticity-derived matrices, ICtCp, and encoder-side subsampling remain incomplete.
- The inverse-transform path allocates arrays in a per-transform hot path.
- No usable end-to-end AV1 SIMD path was found. The most visible 4x4 forward-transform SIMD call is commented out, while the production prediction, transform, filter, and output paths are predominantly scalar.

### AV1 encoder

- `HeifEncoderCore.Encode()` is `async void` but is invoked by a synchronous ImageSharp encoder contract. Work can outlive the call and exceptions cannot be propagated correctly.
- The current container encoder compresses pixels with the ImageSharp JPEG encoder and writes that payload into a HEIC-branded HEIF file. It does not produce AVIF.
- `Av1FrameEncoder.Encode()` is an outline of an SVT-style pipeline rather than an implementation.
- Required mode-decision, block-geometry, forward-transform, token-writing, neighbor-context, palette, intra-block-copy, transform-size, quantization/rate-control, and OBU-writing paths are absent or throw.
- The current encoder test is a self-round-trip through the JPEG-in-HEIF path. It does not prove that the output is AVIF or that an independent decoder can read it.

### Legacy JPEG image items

- A JPEG item decoder exists, and the Fujifilm `.hif` fixture is identified and decoded as a legacy JPEG image item.
- The encoder currently uses JPEG as an accidental fallback for all output rather than as an explicitly selected, correctly branded HEIF image-item codec.
- The legacy JPEG path needs container interoperability tests and explicit public option semantics, but the existing ImageSharp JPEG codec should remain the payload implementation.

### Tests

- The repository contains HEIC, HIF, and AVIF assets, but only the legacy JPEG HIF path reaches a full reference-image comparison.
- HEVC fixtures are identified but not decoded, and there are no HEVC algorithm tests.
- The strongest AV1 integration test only verifies that the first tile produces non-zero luma data.
- Several full-image, inverse-transform, entropy, and frame-header cases are disabled or commented out.
- Existing bitstream, predictor, transform, and entropy unit tests are useful foundations, but many compare two in-tree implementations with the same assumptions.
- There is no decode matrix covering bit depth, subsampling, range, matrix coefficients, alpha, grids, transformations, metadata, truncated data, or resource limits.
- There is no cross-codec encode test in which libavif decodes ImageSharp output, or ImageSharp decodes independently encoded output.

## Architectural direction

### Public format identities, shared internal HEIF container

Expose HEIC and AVIF as distinct public format identities backed by one internal HEIF/ISO BMFF container implementation. Provide format-appropriate decoders, encoders, metadata, options, MIME types, extensions, and generated save methods. Generic `.heif` and `.hif` input must be dispatched by brands and primary item type to HEVC, AV1, or legacy JPEG rather than by extension. Avoid a broad internal namespace rename until the container model is stable.

Generate the standard sync and async `SaveAsHeic` and `SaveAsAvif` overloads through the existing format templates. If a generic `SaveAsHeif` API is retained, its encoder options must require an explicit supported payload codec rather than infer one from an extension. Public XML documentation must describe observable behavior and option effects only.

### Parsed container model

Parse the meta box into an immutable logical model before decoding payloads:

- dictionaries keyed by item ID, not collection position;
- a one-based property table with explicit association records and essential flags;
- resolved item references (`dimg`, `auxl`, `thmb`, and metadata relationships);
- item locations represented as validated extents and construction methods;
- typed properties for `ispe`, `pixi`, `hvcC`, `av1C`, `colr`, `auxC`, `pasp`, `clap`, `irot`, and `imir`;
- bounded views over item payloads, independent of source box order; and
- explicit primary, alpha, grid-tile, thumbnail, Exif, and XMP roles.

Use existing ImageSharp buffered stream and allocation abstractions. Do not copy the whole file into an unbounded byte array. Every length, offset, multiplication, allocation, tile count, and image dimension coming from the file is an external boundary and must be checked against the enclosing box and ImageSharp limits. Internal decode stages should rely on the validated model rather than repeat defensive checks.

### Codec state and sample storage

Keep HEVC and AV1 bitstream state in separate codec implementations. Within each codec, separate parameter/sequence state, frame or picture headers, tile/slice entropy state, reference-frame state, and the reconstructed frame. Give each allocation one owner and a deterministic disposal point.

Represent 8-bit samples with bytes and high-bit-depth samples with unsigned 16-bit storage. Plane dimensions and strides must reflect monochrome and chroma subsampling instead of pretending every plane is full-resolution 4:4:4. Keep scalar reconstruction as the behavioral oracle for every SIMD implementation.

### Decode directly into ImageSharp pixels

Perform chroma upsampling, range expansion, matrix conversion, alpha composition, and pixel packing into allocator-backed row buffers or the destination frame. Use `PixelOperations<TPixel>` and the existing packed pixel conversion paths instead of creating an intermediate `Image<Rgb24>`.

Do not reuse JPEG or WebP color constants merely because those codecs already contain vectorized YUV conversion. Reuse their vector dispatch, lane handling, row processing, and pixel packing patterns only when the signaled HEVC/AV1 range and matrix semantics are preserved.

## Existing ImageSharp code to reuse

| Need | Reuse target | Constraint |
| --- | --- | --- |
| Stream parsing | `BufferedReadStream`, existing endian readers, bounded decoder-core patterns | HEIF box extents remain the source of truth. |
| Memory ownership | `MemoryAllocator`, `IMemoryOwner<T>`, `Buffer2D<T>`, allocator-backed row buffers | No unbounded file-sized arrays or per-block allocations. |
| Pixel output | `PixelOperations<TPixel>`, `Rgba32`, `Rgba64`, existing packed conversion methods | Preserve alpha and high-bit-depth precision. |
| SIMD utilities | `SimdUtils`, `Vector128_`, `Vector256_`, `Vector512_`, `Numerics` helpers | Add codec-specific math only where semantics differ. |
| SIMD structure | JPEG color-converter factories and WebP YUV row converters | Reuse dispatch/tail patterns, not incompatible coefficients. |
| Decode lifecycle | JPEG/WebP decoder cores, ImageSharp cancellation and dimension-limit handling | Identify must not reconstruct pixels. |
| Public format API | PNG, JPEG, and WebP format/decoder/encoder/options/metadata patterns | Keep HEIC, AVIF, and generic HEIF dispatch behavior explicit. |
| Generated save APIs | `_Formats.ttinclude` and existing format templates | Generate `SaveAsHeic` and `SaveAsAvif`; require an explicit codec for generic HEIF output. |
| Metadata | Existing ICC, Exif, XMP, and CICP-related metadata structures where available | Preserve payloads and apply only specified transforms. |
| Test infrastructure | `ReferenceCodec`, image comparers, feature-test helpers, codec test base classes | Include independent artifacts and cross-codec tests. |
| Performance tests | Existing ImageSharp benchmark project and hardware-intrinsics test controls | Benchmark real decode stages and representative images. |

## Implementation phases

Every phase exit gate is an internal prerequisite for the next phase. Only the Phase 10 exit gate together with a fully passing verification matrix marks the PR complete.

### Phase 0: establish a reproducible baseline

Tasks:

1. Build the merged solution in Release and record compile errors and warnings attributable to the WIP.
2. Run only the existing HEIF/HEVC/AV1 tests first, then record disabled tests and unexecuted asset coverage.
3. Pin an official libaom commit for AV1, the local libavif commit for AVIF/container comparison, and the independently reviewed HEVC implementation and interoperability references.
4. Create a provenance map from each WIP codec file to its specification section and exact upstream source. Preserve the current SVT-AV1 origins where applicable and identify which missing paths will use libaom.
5. Convert the completion boundary above into a feature matrix with `unsupported`, `parses`, `decodes`, `encodes`, and `verified independently` states.

Exit gate:

- The post-merge branch has a recorded Release baseline, every existing failure is classified, and all upstream code origins are known before additional porting begins.

### Phase 1: correct the format contract

Tasks:

1. Define distinct HEIC and AVIF public format types over the shared internal HEIF container and register the correct brands, MIME types, and extensions.
2. Add generated `SaveAsHeic` and `SaveAsAvif` APIs and format metadata integration through the same mechanisms as established codecs. Define generic `SaveAsHeif` only if its options require an explicit supported payload codec.
3. Define decoder options using existing `DecoderOptions` behavior, including target pixel type, metadata handling, cancellation, and image-size limits.
4. Define codec-specific encoder options with observable semantics for quality, speed/effort, lossless mode, chroma subsampling, bit depth, alpha quality, and metadata handling. Avoid exposing internal HEVC or AV1 tuning knobs without a stable user-facing meaning.
5. Make `Rgba32` the default 8-bit decode output so alpha is not silently lost.
6. Remove unsupported JPEG 2000, JPEG-XR, JPEG-XS, and AVC capability claims unless those payload codecs are added to the completion matrix. Retain legacy JPEG as an explicit supported HEIF image-item codec.

Exit gate:

- API review confirms that names and documented behavior match existing ImageSharp patterns and promise only the completed HEVC, AV1, and legacy JPEG HEIF payload paths. Capabilities must not be registered before their implementation reaches the final PR gate.

### Phase 2: rebuild the ISO BMFF/HEIF reader around validated items

Tasks:

1. Implement a bounded box reader supporting 32-bit, 64-bit, and to-end box sizes where allowed, with overflow-safe arithmetic and correct parent bounds.
2. Accept the applicable HEIF, HEIC, HEVC-sequence, AVIF, and AVIF-sequence brands through the major or compatible brand rules. Do not enable sequence brands until sequence support is present.
3. Skip unknown non-essential boxes and properties. Reject unknown essential properties attached to a decoded item.
4. Parse meta children independently of physical order and build the item/property/reference model described above.
5. Correct item ID lookup, one-based property indices, association flag masks, full-box versions, and large IDs/offsets.
6. Resolve `idat` and `mdat` item data, multiple extents, construction methods, and 64-bit offsets through bounded item streams.
7. Parse and associate HEVC and AV1 configuration, dimensions, plane information, ICC/CICP color, alpha auxiliary type, grids, transforms, Exif, XMP, and thumbnails.
8. Make Identify return dimensions, bit depth, color type, alpha presence, profiles, and metadata from the parsed model without decoding AV1 tiles.
9. Add malformed-container tests for every external length, offset, count, ID, association, extent, and relationship boundary.

Exit gate:

- The parser resolves each current HEIC, HIF, and AVIF asset into a stable logical model, malformed inputs fail without escaping bounds or allocating attacker-controlled sizes, and Identify has reference-verified metadata and payload classification.

### Phase 3: complete scalar AV1 still-image reconstruction

Implement and verify in dependency order:

1. OBU framing, sequence headers, frame headers, tile groups, byte alignment, and trailing bits.
2. One coherent decoder lifecycle that retains parsed frame and tile state and disposes all buffers deterministically.
3. Tile partitioning, mode information, segmentation, delta quantization, transform-size selection, coefficient token decode, inverse quantization, and inverse transforms.
4. Intra prediction, including every directional, smooth, Paeth, CFL, filter-intra, and palette case permitted by AV1.
5. Lossless and high-bit-depth reconstruction with correct clipping and intermediate precision.
6. Deblocking loop filter.
7. CDEF.
8. Super-resolution scaling.
9. Loop restoration.
10. Frame padding and film-grain synthesis when signaled.

For each item, first add a small scalar unit test against normative or independent vectors, then enable it in the frame pipeline. Remove constant feature-disable flags and unsupported branches only when their replacement is verified. Unsupported syntax must produce a codec-specific invalid-image error; it must never silently skip a normative reconstruction stage.

Exit gate:

- Independently encoded, opaque, single-item AVIF files reconstruct correctly across all AVIF profiles, bit depths, subsampling modes, and normative still-image compression tools. Pixel comparisons are made after applying the same signaled color conversion in the reference path.

### Phase 4: complete scalar HEVC still-image reconstruction

Implement and verify in dependency order:

1. HEVC byte-stream and length-delimited NAL units, `hvcC`, VPS, SPS, PPS, access units, slice headers, and reference-picture-set syntax.
2. One coherent decoder lifecycle that owns parameter sets, picture state, slice/tile entropy state, reference pictures, and reconstructed planes.
3. CABAC arithmetic decoding and every required context transition.
4. Coding-tree, coding-unit, prediction-unit, and transform-unit traversal across all permitted sizes and partition modes.
5. Intra prediction for every luma and chroma mode, including strong intra smoothing and constrained prediction rules.
6. Inter prediction, motion-vector prediction, merge candidates, fractional-sample interpolation, weighted prediction, and reference-picture management required by HEIC image sequences.
7. Scaling lists, inverse quantization, transform skip, every required inverse transform, range-extension precision, and lossless reconstruction.
8. Deblocking and sample-adaptive offset for every signaled luma/chroma and bit-depth path.
9. Tiles, wavefront entry points, dependent slices, and all other parallelization syntax permitted by the exposed profiles.
10. Supplemental enhancement information that changes image presentation or metadata exposed by ImageSharp.

Each subsystem starts with scalar conformance vectors derived from the HEVC specification and the pinned implementation reference. No valid syntax in the exposed HEIC profiles may terminate in an unsupported branch or silently omit a normative reconstruction stage.

Exit gate:

- Independently encoded HEIC files reconstruct correctly across every exposed HEVC profile, chroma format, bit depth, range-extension tool, and normative still-image compression path.

### Phase 5: shared color conversion, alpha, grids, and presentation transforms

Tasks:

1. Implement monochrome, 4:2:0, 4:2:2, and 4:4:4 plane access with every valid signaled chroma sample position.
2. Implement full- and limited-range expansion for 8, 10, and 12-bit samples across every supported plane layout.
3. Implement every non-reserved HEVC/AV1 color-primary, transfer-characteristic, and matrix-coefficient signaling path, including identity conversion, with correct fixed-point rounding and clipping.
4. Decode alpha auxiliary items as monochrome planes, validate dimensions and bit depth, and compose them without losing precision. Define premultiplication behavior from AVIF signaling and ImageSharp's pixel contract.
5. Validate grid tile type, dimensions, properties, order, and canvas coverage. Compose directly into the destination frame, cropping only the permitted right and bottom tile overlap.
6. Apply clean aperture, rotation, mirroring, and pixel aspect ratio according to the container property order and ImageSharp metadata/processing conventions.
7. Preserve ICC, CICP, Exif, and XMP metadata using existing ImageSharp profile types.

Exit gate:

- The complete cross-product of valid HEVC/AV1 bit depths, subsampling modes, ranges, color signaling, alpha, grids, and transforms is covered by focused vectors and representative HEIC/AVIF integration files and matches independently decoded references within a documented conversion tolerance. Integer identity/lossless cases must match exactly.

### Phase 6: implement a real AV1 still-image encoder

Delete the JPEG payload path from the production encoder. Keep the synchronous ImageSharp encoder contract synchronous unless the established base contract provides an async implementation; never use `async void`.

Implement in vertical slices that always produce a decodable AV1 bitstream:

1. RGB/RGBA to AV1 planes for every 8, 10, and 12-bit output, range, matrix, and monochrome/4:2:0/4:2:2/4:4:4 combination permitted by the selected AV1 profile.
2. Sequence, frame, tile-group, and metadata OBU writing for a reduced still picture.
3. A temporary smallest-valid intra-only vertical slice using existing partition, prediction, transform, quantization, coefficient, and entropy structures.
4. Complete block geometry, neighbor/context updates, transform selection and forward transforms, quantization, coefficient tokenization, and range coding.
5. Add mode decision and rate/distortion selection in increasing effort levels. Reuse computed prediction and transform results instead of duplicating analysis across stages.
6. Add lossless mode and validate the exact lossless constraints rather than treating quality 100 as lossless.
7. Add in-loop filter decisions and signaling. A legal choice to disable an encoder feature is distinct from a decoder skipping a signaled feature.
8. Add alpha as an auxiliary AV1 item with independently controllable quality where the public option warrants it.
9. Write the AVIF item graph, extents, `av1C`, pixel information, color properties, metadata, and alpha relationships with correct brands.
10. Make output deterministic for identical pixels, metadata, options, and configuration.
11. Complete the partition, prediction, transform, quantization, entropy, filter, and rate/distortion choices needed for quality and effort settings to provide a genuine compression tradeoff rather than selecting from a fixed coding subset.
12. Exercise every encoder bit-depth, plane-layout, range, color, alpha, lossless/lossy, quality, and effort combination through independent decode.

Every vertical slice must be decoded by libavif before additional compression features are added. Self-round-trip tests are supplementary because matching encoder and decoder bugs can otherwise hide invalid bitstreams.

Exit gate:

- libavif and another independent AV1 decoder accept ImageSharp output across the complete encoder matrix, and ImageSharp reconstructs the same files. Lossless output is pixel-exact; lossy output demonstrates effective quality/effort tradeoffs and meets recorded quality and size expectations without malformed or non-AV1 payloads.

### Phase 7: implement a real HEVC still-image encoder

Implement in vertical slices that always produce a HEVC bitstream accepted by an independent HEIC decoder:

1. Convert ImageSharp pixels into every exposed HEVC bit depth, range, and monochrome/4:2:0/4:2:2/4:4:4 combination.
2. Write VPS, SPS, PPS, slice headers, parameter arrays in `hvcC`, and a smallest-valid intra picture as a temporary vertical slice.
3. Complete coding-tree partitioning, intra prediction selection, forward transforms, scaling/quantization, coefficient scanning, and CABAC encoding.
4. Add real rate/distortion mode decision, quality and effort controls, lossless mode, deblocking decisions, and sample-adaptive-offset decisions.
5. Add alpha auxiliary images, grids, metadata, color properties, image relationships, and correct HEIC brands.
6. Exercise every encoder profile, bit depth, chroma format, range, color, alpha, lossless/lossy, quality, and effort combination through independent decode.

Exit gate:

- Independent HEIC decoders accept ImageSharp output across the complete encoder matrix. Lossless output is pixel-exact; lossy output demonstrates effective quality/effort tradeoffs and meets recorded quality and size expectations.

### Phase 8: HEIC and AVIF image sequences

This phase is part of the same PR completion gate; still-image completion does not make the PR complete.

Tasks:

1. Add HEVC and AVIF sequence brands, tracks, sample tables, item/track relationships, and timing models.
2. Complete AV1 reference frames, frame IDs, order hints, `show_existing_frame`, primary reference state, CDF updates, inter prediction, motion vectors, compound prediction, and temporal units.
3. Complete HEVC decoded-picture-buffer, reference-picture-set, inter-prediction, and random-access behavior required by HEIC sequences.
4. Map timing, repetition, and frame composition to the ImageSharp frame model.
5. Bound retained reference frames for both codecs and dispose them deterministically.
6. Encode standards-compliant HEIC and AVIF sequences. Temporary all-intra vertical slices must be replaced by the complete compression paths before the PR gate.

Exit gate:

- HEIC and AVIF sequence timing, repetition, alpha, metadata, and pixels match independent decoders, and independently encoded inter-frame sequences for both codecs decode correctly.

### Phase 9: SIMD and allocation optimization

SIMD work begins after the corresponding scalar stage has independent correctness tests; it then proceeds alongside subsequent functional phases.

Tasks:

1. Remove known avoidable allocations first: per-transform arrays, the intermediate RGB image, repeated block scratch arrays, and file-sized buffering.
2. Benchmark codec-specific costs for CABAC/range decode, inverse transforms, prediction, motion compensation, deblocking, SAO, CDEF, restoration, chroma upsampling, color conversion, alpha packing, and grid copies.
3. Implement vector paths only for confirmed hot loops, using existing `Vector128`, `Vector256`, and `Vector512` helper and dispatch patterns where supported.
4. Prioritize shared color conversion and pixel packing, chroma upsampling, inverse-transform add-and-clip, common intra/inter predictors, motion compensation, HEVC deblock/SAO, AV1 loop filter/CDEF/restoration, and contiguous grid copies.
5. Port upstream SIMD algorithms only after mapping lane width, signedness, intermediate precision, rounding, saturation, edge extension, and high-bit-depth behavior to the scalar oracle.
6. Keep one scalar implementation as the specification-shaped reference. Vector paths must share tables and constants with it rather than duplicate codec policy.
7. Test scalar and each available hardware path with intrinsics explicitly enabled and disabled, including widths shorter than a vector, exact-vector widths, non-multiples, edges, maximum sample values, and high-bit-depth overflow cases.
8. Remove dead or commented SIMD experiments once a verified production path replaces them.

Exit gate:

- Benchmarks show a material improvement on representative AVIF files, allocation measurements meet an agreed budget, and every vector path is behaviorally identical to the scalar path for integer reconstruction or within the documented color-conversion tolerance.

### Phase 10: hardening, documentation, and release readiness

Tasks:

1. Fuzz the box parser, AV1 OBU parser, HEVC NAL/parser, entropy decoders, and dimension/allocation boundaries using the same safety expectations as established ImageSharp codecs.
2. Test seekable and non-seekable streams, short reads, cancellation, truncated data, unknown optional boxes, unknown essential properties, oversized dimensions, malicious counts, and offset arithmetic overflow.
3. Run the focused HEIF/HEIC/AVIF suite after every final codec edit, then the full ImageSharp suite in Release.
4. Build all supported target frameworks and run packaging/API compatibility checks used by the repository.
5. Update public documentation, format tables, MIME/extension lists, samples, and `THIRD-PARTY-NOTICES.TXT`.
6. Remove placeholder images, legacy JPEG-in-HEIF production paths, stale TODO-only code, disabled tests that now have coverage, and unsupported capability claims.

Exit gate:

- The full Release build and test matrix passes, every valid HEVC/AV1 color/compression/bit-depth entry is implemented, independent HEIC and AVIF interoperability is recorded for the complete feature matrix, the public API has been reviewed, provenance and patent/license obligations are complete, and no advertised feature depends on a placeholder, narrow temporary subset, silent fallback, disabled normative stage, or unsupported valid syntax branch.

## Verification matrix

Every valid combination in the HEVC and AV1 profiles exposed by the final public contract needs focused coverage and representative integration coverage. Each axis needs independently produced HEIC/AVIF inputs and ImageSharp-produced outputs; pairwise and targeted cross-product cases must cover interactions where exhaustive media fixtures would be redundant.

| Area | Required coverage |
| --- | --- |
| Container/payload | HEIC with HEVC, AVIF with AV1, and generic HEIF/HIF with supported HEVC, AV1, or legacy JPEG items; detection by brands/items. |
| Bit depth | Every bit depth permitted by the exposed HEVC and AV1 profiles, including 8, 10, and 12-bit decode and encode. |
| Planes | monochrome, 4:2:0, 4:2:2, and 4:4:4 decode and encode in every valid HEVC/AV1 profile and depth combination. |
| Range | full and limited decode and encode. |
| Color | every valid non-reserved color-primary, transfer-characteristic, matrix-coefficient, and chroma-position signaling path; identity RGB; ICC; CICP defaults and overrides. |
| Alpha | opaque, binary, gradient, different alpha quality, high bit depth, malformed relationship. |
| Structure | single item, multiple extents, `idat`, `mdat`, grids with cropped edge tiles, metadata items. |
| Transform | `pasp`, `clap`, `irot`, `imir`, and valid combinations. |
| AV1 decode tools | every normative transform type/size, predictor, partition, palette, segmentation, quantization, entropy/context, lossless, inter-frame, motion-compensation, deblock, CDEF, super-resolution, restoration, and film-grain path valid in AVIF. |
| AV1 encode compression | real mode decision and rate/distortion selection across partitions, predictions, transforms, quantization, entropy coding, filters, lossless/lossy quality, and effort settings; no permanent fixed coding subset. |
| HEVC decode tools | every normative NAL/parameter/slice, CABAC, coding-tree, intra/inter prediction, motion, transform, quantization, range-extension, lossless, tile/wavefront, deblock, SAO, and reference-picture path valid in the exposed HEIC profiles. |
| HEVC encode compression | real coding-tree, prediction, transform, quantization, CABAC, filter, and rate/distortion decisions across lossless/lossy quality and effort settings; no permanent fixed coding subset. |
| Streams | file, memory, non-seekable, short-read wrapper, cancellation. |
| Failure | truncation at every box/OBU layer, invalid sizes/offsets/counts, unknown essential properties, unsupported profile. |
| Interop | libavif and libaom for AVIF/AV1; a pinned independent HEIC container and HEVC codec implementation for HEIC/HEVC. |
| SIMD | scalar, 128, 256, and 512-bit paths where supported; tails and edge blocks. |

Reference outputs must be versioned artifacts or generated by a pinned reference command whose exact tool version and arguments are recorded. Do not use ImageSharp's own decoder to establish the expected pixels for its encoder, and do not replace final-image assertions with internal buffer or non-zero checks.

No valid HEVC or AV1 color, compression, or bit-depth row may remain `unsupported`, partially implemented, disabled, or deferred when the PR is marked complete.

## Working rules for implementation

- Keep changes vertical and reviewable. A slice should add one behavior, its focused tests, independent evidence, and any required notice update.
- Inspect every owning method and upstream invariant before adding guards. Validate external file data at the parser/model boundary and rely on those established invariants internally.
- Do not extract one-use helpers merely to label code. Extract shared primitives only when they have genuine reuse or remove substantial complexity.
- Put comments at the points where HEVC/AV1 rounding, edge extension, context propagation, or SIMD lane behavior is not evident from the code. Comments should explain why the algorithm has that shape and identify the normative rule and pinned implementation reference.
- Use observable behavior only in public API documentation.
- Do not use reflection, built-assembly probing, native runtime fallbacks, fabricated images, or self-round-trip-only evidence.
- Build and test in Release configuration.

## Recommended implementation order

The critical path is:

1. Baseline and provenance.
2. Public HEIC/AVIF boundaries and the shared parsed HEIF container model.
3. Scalar AV1 still decode, starting with 8-bit 4:2:0 and completing every AVIF profile/tool.
4. Scalar HEVC still decode, starting with 8-bit 4:2:0 and completing every exposed HEIC profile/tool.
5. Shared color, alpha, grids, metadata, and presentation transforms across all bit depths and chroma formats.
6. Real AV1 encoder and libavif/libaom cross-decode across the complete matrix.
7. Real HEVC encoder and independent HEIC cross-decode across the complete matrix.
8. HEIC and AVIF image sequences, including inter-frame decode and encode.
9. Measured SIMD/allocation work integrated after each scalar subsystem stabilizes.
10. Hardening and release gates.

This sequence does not define partial PR completion. The early 8-bit 4:2:0 decoders and smallest-valid encoders are temporary vertical slices, but the PR remains incomplete until all ten steps and the complete verification matrix pass.

Do not begin by optimizing the current end-to-end pipeline: it cannot yet produce a correct HEIC or AVIF image, and several current data structures encode 8-bit 4:4:4 assumptions. Establish the correct scalar storage and behavior first so the reused or ported AOM/HEVC SIMD algorithms have a trustworthy managed oracle.
