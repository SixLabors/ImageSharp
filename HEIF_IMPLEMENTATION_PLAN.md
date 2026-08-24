# HEIF family implementation plan

## Goal

Complete a production-quality, fully managed still-image HEIF family implementation for ImageSharp. The implementation must support HEIC, HIF/HEIF, and AVIF image files, interoperate with independent encoders and decoders, follow the existing ImageSharp architecture and code style, reuse existing ImageSharp infrastructure wherever its semantics match, and use SIMD for measured hot paths without maintaining a separate behavior model.

HEIF is the shared ISO BMFF-derived container. HEIC carries HEVC image items, AVIF carries AV1 image items, and `.hif`/`.heif` are container extensions whose payload codec must be determined from brands and item types rather than the filename. The completed implementation will support HEVC, AV1, and legacy JPEG image items. Other registered HEIF payload codecs must not be advertised unless they are implemented and independently verified.

## Completion boundary

This plan has one PR completion gate. The phases below are dependency order and internal verification points only; none is a separately releasable or merge-complete subset. The PR is not complete until the complete matrix is implemented and independently verified.

Full completion includes:

- XML documentation for every type and contract in the HEIF implementation, including the AV1 and HEVC codec internals, together with inline comments that explain non-obvious container layouts, bitstream rules, numerical algorithms, SIMD choices, and interoperability constraints;
- correct still-image HEIF, HEIC, and AVIF brand and item-type detection without relying on file extensions, while rejecting sequence/movie brands that are outside this image-codec scope.
- still-image decode and encode for HEVC/HEIC and AV1/AVIF.
- standards-compliant legacy JPEG image-item decode and encode for HEIF/HIF files.
- every bit depth and chroma format permitted by the HEVC profiles exposed for HEIC and the AV1 profiles exposed for AVIF, including 8, 10, and 12-bit and monochrome, YUV 4:2:0, 4:2:2, and 4:4:4 paths.
- full- and limited-range conversion using every valid signaled color-primary, transfer-characteristic, matrix-coefficient, and chroma-sample-position combination, including identity RGB signaling.
- decoding every normative AV1 compression tool that can occur in a conforming, independently decodable AVIF still-image item; valid still-image syntax cannot terminate in an unsupported branch or silently skip reconstruction.
- a real lossy and lossless AV1 encoder with working quality and effort controls, complete mode decision, prediction, transform, quantization, entropy coding, and legal in-loop filter decisions. A permanently fixed smallest-valid coding subset is not complete.
- decoding every normative HEVC compression tool that can occur in a conforming, independently decodable HEIC still-image item across the exposed profiles, including the range-extension tools required for high bit depth and 4:2:2/4:4:4.
- a real HEVC encoder with working lossless/lossy quality and effort controls, complete coding-tree, prediction, transform, quantization, CABAC, deblocking, and sample-adaptive-offset decisions. A permanently fixed smallest-valid coding subset is not complete.
- primary images, alpha auxiliary images, image grids, ICC and CICP color information, Exif, XMP, pixel aspect ratio, clean aperture, rotation, and mirroring.
- independent AVIF interoperability with libavif and libaom, and independent HEIC interoperability with a separately selected HEVC/HEIF implementation.

Gain maps, progressive/layered images, sample transforms, and experimental extension brands require explicit conformance and API decisions. They do not create permission to omit any valid color, compression, or bit-depth path from the PR. The container reader must skip unsupported optional extensions safely and reject an unsupported essential property with a useful error.

## Container scope

The container implementation is a deliberately narrow HEIF still-image reader and writer, not a general ISO BMFF framework. Implement only the box syntax and relationships required to identify, locate, describe, decode, and encode supported HEIF image items, derived image items, auxiliary images, thumbnails, and their image metadata.

In scope are the file type, metadata, item location/data, item information, item properties, item references, primary-item selection, `idat`/`mdat` payload storage, grids, auxiliary alpha, presentation transforms, color properties, and Exif/XMP paths required by HEIC, AVIF, and generic HEIF/HIF still images.

The ImageSharp result is one presented primary still image. Supporting items are decoded only when they are required to construct or describe that result, such as grid tiles, alpha auxiliaries, a selected thumbnail fallback, Exif, or XMP. The implementation does not expose an arbitrary HEIF image collection, burst, animation, timed sequence, or page model, and the encoder writes only the primary image and the supporting image items and metadata selected through the ImageSharp still-image API.

Out of scope are movie and media boxes, tracks, sample tables, timing and edit models, fragments, streaming profiles, sequence playback, sequence brands, and inter-frame reference-picture behavior whose only purpose is HEIC/AVIF animation or video. These surfaces must not be modeled speculatively, registered, or accepted as supported formats. Unknown optional boxes remain bounded and skippable; an unsupported essential image property or unsupported sequence/movie brand must fail with a useful image-format error.

Implementation rule: do not introduce a reusable general-purpose ISO BMFF box hierarchy, track model, or media parser. Add box syntax directly to the bounded HEIF container model only when a supported still-image item, relationship, property, metadata path, or conformance fixture requires it. Each addition must name the image behavior it enables and have a focused image-format test.

An ISO BMFF construct may be added only when all of the following are true:

1. A conforming supported still-image file requires it to produce or describe the primary ImageSharp image.
2. Its owning image item and its effect on the decoded or encoded image are explicit.
3. It can be parsed or written as a bounded part of the existing HEIF image-item model without adding a general box, sample, track, or presentation abstraction.
4. Independent still-image fixtures exercise the behavior it enables.

Encountering a box in libavif, ISO BMFF, or a third-party file is not by itself a reason to port it. Constructs used only by tracks, timed samples, movies, fragments, audio, animation, or arbitrary image collections must be skipped when optional or rejected when essential to the requested presentation.

Codec-configuration rule: parse `av1C` and `hvcC` only as properties of coded still-image items. Validate their image profile, level, bit depth, chroma layout, and parameter-set/OBU declarations against the associated item payload and expose only image metadata needed by ImageSharp. Do not port visual sample entries, sample descriptions, decoder-configuration records for tracks, layer-selection state, sample groups, timing, or any other movie-oriented ISO BMFF surface around those records.

AV1 sequence headers and HEVC VPS/SPS/PPS structures remain in scope because they are codec syntax required to decode a single independently decodable image item. Their presence does not authorize ISO BMFF sequence brands, timed samples, retained playback state, or multi-frame APIs.

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
- the official ITU-T H.274 (V4) recommendation from January 2026 is the normative semantics reference for mastering-display and content color-volume fields reused by still-image item properties; its video-SEI persistence and cancellation behavior is outside this container scope;
- the independently reviewed HEVC implementation and interoperability references remain unresolved and must be pinned before HEVC algorithm work begins;
- `dotnet build ImageSharp.sln -c Release --no-restore -m:1 -v minimal` succeeds with no errors after the upstream compatibility fixes; and
- the existing HEIF-focused test run executes 8,198 cases, with 8,184 passing and 14 failing. Thirteen failures are isolated to the WIP AV1 YUV conversion tests, and one is the existing legacy JPEG HIF reference-image mismatch. Golden artifacts have not been changed.

This snapshot pins or classifies the available references and failures; it does not complete Phase 0. The full WIP provenance map, disabled-test inventory, HEVC reference selection, and feature-state matrix remain required.

### Provenance map in progress

| Managed implementation | Normative behavior | Reviewed implementation reference | Use |
| --- | --- | --- | --- |
| `Av1YuvConverter.ConvertToRgb`, `ConvertFromRgb`, scalar row conversion, chroma reconstruction, and chroma downsampling | ITU-T H.273 (V4) equations 14-16 and 27-84, including limited/full-range scaling, chromaticity-derived equations 39-47, YCgCo equations 51-57, constant-luminance equations 66-75, and the PQ/HLG ICtCp matrices; AV1 section 6.4.2 chroma sample positions | Official ITU-T H.273 (V4) (07/2024); libavif `src/reformat.c` and `src/colr.c` at `092276ce89098ead06db80975173191e5fee1826`; libaom `aom/aom_image.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Scalar behavioral oracle for full/limited-range conversion at 8, 10, and 12 bits. Decode and encode cover identity, YCgCo, coefficient-based NCL, both fixed and chromaticity-derived constant/non-constant-luminance systems, SMPTE ST 2085, and ICtCp across monochrome, YUV 4:2:0, 4:2:2, and 4:4:4 with AV1 chroma sample positioning. Limited-range YCgCo retains the 219-code scale inherited from its R/G/B inputs instead of applying YCbCr's unrelated 224-code chroma range. Chromaticity derivation uses every defined H.273 primary and matches libavif's BT.709 fallback for unspecified or reserved primaries. The ICtCp inverse is derived from the exact H.273 integer matrices rather than an unrelated display conversion. Later SIMD paths must preserve this scalar behavior. |
| `Av1TransferFunctions` | ITU-T H.273 (V4) Table 3 transfer characteristics 1-18 | Official ITU-T H.273 (V4) (07/2024); libavif `src/colr.c` at `092276ce89098ead06db80975173191e5fee1826` | Apply every AV1-signallable transfer function required by constant-luminance and ICtCp color conversion. Retain the H.273 normalized PQ and HLG definitions; do not import libavif's display-oriented 203-nit scaling or HLG OOTF into codec sample interpretation. Use libavif's midpoint convention only for the non-bijective zero code of the two logarithmic curves. |
| `ObuReader.ReadSequenceHeader`, `ReadUncompressedFrameHeader`, decoder-model parsing, and operating-parameter consumption | AV1 sections 5.5.2 through 5.5.4 sequence timing and decoder-model syntax, section 5.9.2 uncompressed frame-header syntax, and section 5.9.31 temporal-point syntax | libaom `av1/decoder/decodeframe.c` functions `av1_read_decoder_model_info`, `av1_read_op_parameters_info`, `read_temporal_point_info`, and `read_uncompressed_header`, plus `common/av1_config.c`, at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Read the normative 32-bit decoding-tick field; consume operating-point buffer delays and the layer-applicable frame removal times needed to keep a non-reduced still-image sequence aligned; and read presentation time only under the normative decoder-model condition. Retain no scheduling or playback behavior from those values and introduce no ISO BMFF timing, track, sample-table, or sequence surface. |
| `Av1FrameBuffer` high-bit-depth sample layout and `Av1YuvConverter` 10/12-bit packed-pixel conversion | AV1 section 6.4.1 bit depth and H.273 sample-range scaling | libaom `aom_scale/yv12config.h`, `av1/common/idct.c`, and `av1/common/reconintra.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f`; libavif `src/avif.c` and `src/reformat.c` at `092276ce89098ead06db80975173191e5fee1826` | Establish two-byte native sample storage with sample-unit strides for 10/12-bit reconstruction and use ImageSharp's existing `Rgb48` pixel-operation paths in both directions so packed-pixel staging does not reduce high-bit-depth samples to eight bits. |
| `Av1PredictionDecoder`, `Av1HighBitDepthPredictor`, `Av1ChromaFromLumaContext`, `Av1PartitionInfo`, and the scalar DC, directional, Paeth, smooth, filter-intra, and chroma-from-luma predictors | AV1 sections 7.11.2 and 7.11.2.3 intra prediction | libaom `aom_dsp/intrapred.c`, `av1/common/reconintra.c`, `av1/common/av1_common_int.h`, `av1/common/blockd.h`, `av1/common/cfl.c`, and `av1/common/cfl.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Behavioral oracle for luma/chroma mode-neighbor addressing, directional upsampling, Paeth selection, smooth normalization, filter-intra taps, high-bit-depth clipping, chroma-from-luma storage/subsampling, and chroma-from-luma row strides. Existing managed scalar tables and predictors remain the implementation base. The WIP rectangular byte-pipeline smooth digest expectations encode width/height-swapped weights and must be replaced only from an independently generated oracle, not regenerated from this implementation. |
| `Av1TileReader` palette mode/color-map parsing, `Av1SymbolDecoder` palette distributions, `Av1BlockModeInfo` palette state, and `Av1PredictionDecoder` palette reconstruction | AV1 sections 5.11.46, 5.11.49, and 7.11.2 palette prediction | libaom `av1/decoder/decodemv.c`, `av1/decoder/detokenize.c`, `av1/decoder/decoder.h`, `av1/common/pred_common.c`, `av1/common/pred_common.h`, and `av1/common/entropymode.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Reuse the existing tile range decoder and frame-owned mode map while porting the normative palette cache merge, high-bit-depth color deltas, tile-adaptive mode/size/index distributions, diagonal color-map traversal, edge padding, and direct palette-sample reconstruction. This is AV1 still-image compression syntax and does not add retained video reference state or any ISO BMFF surface. |
| `Av1LoopFilterKernels`, `Av1LoopFilterContext`, and `Av1LoopFilterDecoder` | AV1 section 7.14 deblocking loop filter | libaom `aom_dsp/loopfilter.c` and `av1/common/av1_loopfilter.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Port the scalar 4-, 6-, 8-, and 14-tap low/high-bit-depth filters, sharpness thresholds, still-frame intra filter-level derivation, transform-edge selection, and plane traversal before enabling the stage. Later SIMD must preserve the scalar result. This is normative AV1 image reconstruction and adds neither generic ISO BMFF models nor retained video reference state. |
| `Av1CdefDecoder`, `Av1CdefKernels`, and CDEF-unit strength storage | AV1 sections 7.15.2 through 7.15.4 constrained directional enhancement filtering | libaom `av1/common/cdef.c`, `av1/common/cdef_block.c`, `av1/common/cdef.h`, and `av1/common/cdef_block.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Port the scalar direction search, variance adjustment, constrained primary/secondary taps, subsampling direction conversion, skipped-8x8 selection, and frame-edge sentinel behavior. Use a frame-owned source snapshot so filtering never consumes already modified samples. This is normative AV1 still-image reconstruction and introduces no ISO BMFF, track, timing, or sequence-playback surface. |
| `Av1SuperResolutionDecoder`, `Av1SuperResolutionKernels`, frame-size derivation, and decoded-image dimensions | AV1 section 7.16 normative super-resolution upscaling | libaom `av1/common/resize.c`, `av1/common/resize.h`, `av1/common/convolve.c`, and `aom_dsp/aom_filter.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Port the fixed 64-phase, 8-tap horizontal filter, phase/step derivation, replicated frame edges, chroma width rounding, signed rounding, and 8/10/12-bit clipping. Reuse ImageSharp's existing cross-platform `Vector128_.MultiplyAddAdjacent` helper for the exact eight-coefficient dot product with a scalar fallback. Generic image resizing is not normative AV1 super-resolution. This adds no track, timing, fragment, animation, or generic ISO BMFF model. |
| `Av1TileReader` loop-restoration unit syntax, `Av1SymbolDecoder` restoration distributions/subexponential codes, and `Av1FrameInfo` unit storage | AV1 section 5.11.57 `read_lr` and `read_lr_unit` syntax | libaom `av1/decoder/decodeframe.c`, `av1/common/restoration.c`, `av1/common/restoration.h`, `av1/common/entropymode.c`, `aom_dsp/binary_codes_reader.c`, and `aom_dsp/recenter.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Decode tile-local switchable/Wiener/self-guided selections, finite reference-subexponential coefficients, chroma Wiener windows, self-guided parameter sets, super-resolution-adjusted unit corners, and the AV1 nearest-unit-count rule into frame-owned per-plane grids. This is compressed still-image syntax and adds no movie, track, timing, fragment, audio, or sequence surface. |
| `Av1WienerFilter` | AV1 sections 7.17.4 and 7.17.5 Wiener restoration filtering and coefficient derivation | libaom `av1/common/restoration.c`, `av1/common/restoration.h`, `av1/common/convolve.c`, and `av1/common/convolve.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Preserve the implicit center-sample contribution, separable horizontal/vertical rounding, bit-depth-dependent 16-bit intermediate range, and final 8/10/12-bit clipping. Reuse `Vector128_.MultiplyAddAdjacent` for the contiguous horizontal eight-tap product with an exact scalar fallback. Keep this scalar/SIMD oracle disabled until restoration stripe boundaries and self-guided filtering are both complete. |
| `Av1SelfGuidedFilter` | AV1 sections 7.17.2 and 7.17.3 self-guided and box-filter processes | libaom `av1/common/restoration.c` and `av1/common/restoration.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Preserve the sixteen normative radius/variance parameter sets, local mean and variance normalization, alternating-row radius-two optimization, decoded projection-coefficient behavior, signed rounding, and 8/10/12-bit clipping. Use caller-owned scratch storage and a scalar oracle before considering a managed SIMD translation of libaom's architecture-specific nonlinear filter kernels. Keep this image-reconstruction stage disabled until restoration stripe boundaries are complete. |
| `Av1LoopRestorationBoundary`, `Av1LoopRestorationDecoder`, and `Av1FrameDecoder` restoration-stage ordering | AV1 section 7.17 loop restoration, including striped boundary semantics | libaom `av1/common/restoration.c`, `av1/common/restoration.h`, `av1/common/resize.c`, and `av1/decoder/decodeframe.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Preserve two deblocked rows at internal 64-luma stripe boundaries before CDEF, apply the existing normative SIMD-backed super-resolution kernel to saved rows when scaled, use post-CDEF/super-resolution samples at frame edges, extend the final restoration unit up to 150 percent of nominal size, and filter from immutable plane snapshots into separate output planes. This is bounded still-image reconstruction state, not retained reference-frame, track, timing, or playback state. |
| `Av1FilmGrainDecoder` and `Av1FilmGrainGaussianSequence` | AV1 section 7.18 film-grain synthesis | libaom `av1/decoder/grain_synthesis.c`, `av1/decoder/grain_synthesis.h`, and `aom_dsp/grain_params.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Preserve the normative 2,048-sample Gaussian sequence, linear-feedback shift register, luma/chroma autoregressive templates, scaling lookup interpolation, 32x32 block selection, boundary overlap, restricted-range clipping, monochrome and 4:2:0/4:2:2/4:4:4 layouts, and 8/10/12-bit arithmetic. Use allocator-owned scratch and runtime-optimized span copies. Apply grain only to the displayed still-image samples after all in-loop filters; reference-frame parameter inheritance remains sequence-playback state and is outside this codec scope. |
| `Av1FrameInfo`, `Av1TileReader`, and `Av1BlockDecoder` transform/coefficient storage | AV1 section 5.11.39 coefficient syntax and section 7.11.2 reconstruction | libaom `av1/decoder/decodetxb.c` and `av1/decoder/decoder.h` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Preserve separate luma and chroma transform coefficients at monotonically advancing per-plane offsets within each superblock so reconstruction consumes the same transform-block order produced by tile parsing. |
| `Av1InverseQuantizer` and `Av1InverseQuantizationLookup` | AV1 section 7.12.3 inverse quantization | libaom `aom_dsp/aom_dsp_common.h`, `av1/common/quant_common.c`, and `av1/decoder/decodetxb.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Select the per-segment matrix level, alias 64-pixel transform dimensions to their adjusted matrices, retain a flat level-15 matrix, and apply the five-bit inverse-matrix weight scale. The large managed lookup remains a single process-wide table. |
| `Av1Inverse2dTransformer` and `Av1InverseTransformerFactory` | AV1 section 7.11.2 inverse transform and reconstruction | libaom `av1/common/av1_inv_txfm1d.c`, `av1/common/av1_inv_txfm2d.c`, and `av1/common/idct.c` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Scalar transform oracle for coefficient-row traversal, intermediate layout, stage ranges, clipping, and high-bit-depth sample addition. The managed 16-bit overload is also used as a parity oracle for the byte overload. |
| `HeifDecoderCore` box extension handling and `HeifDecoderCore`/`HeifEncoderCore` item-property associations | ISO/IEC 14496-12 box extensibility and section 8.11.14 item properties and `ipma` syntax | libavif `src/read.c` and `src/write.c` at `092276ce89098ead06db80975173191e5fee1826` | Skip unrecognized top-level and metadata child boxes, preserve the position of every property in `ipco`, reject an unrecognized property only when its item association marks it essential, associate properties by item ID, and read or write the essential bit plus one-based 7-bit or 15-bit property index according to the full-box flags. Independent HEIC, HIF, and AVIF fixtures provide the reader oracle; container-level identification of encoded output guards the writer independently of pixel roundtripping. |
| `HeifCleanAperture`, `HeifItem` presentation state, and `HeifDecoderCore` transformative-property parsing and application | ISO/IEC 14496-12 section 12.1.4 clean aperture; HEIF image rotation and mirror properties; MIAF section 7.3.6.7 presentation order and section 7.3.9 essential transformative properties | libavif `src/avif.c` clean-aperture conversion, `src/read.c` property parsers and alpha-property validation, and `apps/shared/avifutil.c` transform application at `092276ce89098ead06db80975173191e5fee1826` | Resolve fractional clean-aperture dimensions and center offsets to exact bounded integer pixels, validate the registered rotation/mirror reserved bits, require essential associations, crop after auxiliary-alpha composition, map counter-clockwise HEIF quarter turns to ImageSharp's optimized clockwise rotate modes, then mirror around the signaled axis. Reuse ImageSharp's existing crop, rotation, and flip processors for every pixel type. Retain only the three image-item property values; do not add a generic transform-box or ISO BMFF model. |
| `HeifConstants.IsSupportedFileType`, `HeifImageFormatDetector`, and `HeifDecoderCore.CheckFileTypeBox` | ISO/IEC 14496-12 `FileTypeBox` syntax and the MP4 Registration Authority HEIF/AVIF still-image and sequence brand registrations | libavif `src/read.c` functions `avifParseFileTypeBox`, `avifFileTypeHasBrand`, and `avifFileTypeIsCompatible` at `092276ce89098ead06db80975173191e5fee1826` | Apply one rule to the major and compatible brands, accept the implemented still-image container and payload brands, and reject registered HEVC, AVIF, and JPEG sequence major brands as outside the image-item scope. The decoder validates the complete `ftyp` payload; the fixed-size format detector inspects the available prefix. |
| `HeifDecoderCore.ReadBoxHeader` and `HeifDecoderCore.ParseBoxHeader` | ISO/IEC 14496-12 section 4.2.2 basic box syntax | libavif `src/stream.c` functions `avifROStreamReadBoxHeaderPartial` and `avifROStreamReadBoxHeader` at `092276ce89098ead06db80975173191e5fee1826` | Resolve 32-bit, 64-bit, UUID, and top-level size-zero boxes into content lengths only after validating the complete variable-sized header and the remaining parent boundary. Nested size-zero boxes are invalid; large skips retain 64-bit offsets. |
| `HeifDecoderCore.ParseMetadata` | ISO/IEC 14496-12 `MetaBox` and HEIF item declarations, locations, properties, and associations | libavif `src/read.c` functions `avifParseMetaBox`, `avifMetaFindOrCreateItem`, `avifParseItemLocationBox`, and `avifParseItemPropertiesBox` at `092276ce89098ead06db80975173191e5fee1826` | Index unique recognized metadata children by type and payload location, then parse them in dependency order so physical placement does not control item lookup or property association. Duplicate unique children and truncated full-box headers are invalid. |
| `HeifDecoderCore.ApplyAssociatedMetadata` | HEIF Annex A Exif item data, MIME metadata items, and `cdsc` item references | libavif `src/read.c` function `avifDecoderFindMetadata`, `src/exif.c` function `avifGetExifTiffHeaderOffset`, and the Exif/XMP item writing paths in `src/write.c` at `092276ce89098ead06db80975173191e5fee1826` | Resolve only metadata items whose `cdsc` reference identifies the decoded primary image, validate the Exif TIFF-header offset, and attach Exif or `application/rdf+xml` XMP through ImageSharp's existing profile types. This is a bounded still-image metadata path; it does not introduce a generic ISO BMFF metadata, media, or track model. |
| `HeifDecoderCore` color-property parsing/association, `HeifItem` color profiles, and `Av1Decoder` container color override | ISO/IEC 14496-12 section 12.1.5 color information; HEIF section 6.5.5.1 color-information properties; AV1-ISOBMFF section 2.3.4 configuration semantics | libavif `src/read.c` functions `avifParseColourInformationBox`, `avifReadColorNclxProperty`, and `avifReadColorProperties`, plus `src/write.c` function `avifEncoderWriteColorProperties`, at `092276ce89098ead06db80975173191e5fee1826` | Associate at most one ICC and one `nclx` property with each presented color image item, validate ICC payloads and CICP reserved bits, expose them through ImageSharp's existing profile types, inherit a grid's CICP description only for tiles that do not declare one, and let container CICP values override the matching AV1 sequence-header fields before still-image reconstruction and YUV-to-RGB conversion. Retain only the two image color profiles; do not add a reusable color-box, sample-entry, track, or media model. |
| `HeifPixelAspectRatio`, `HeifItem.PixelAspectRatio`, and `HeifDecoderCore.ApplyItemPixelAspectRatioMetadata` | ISO/IEC 14496-12 section 12.1.4.3 pixel aspect ratio | libavif `src/read.c` function `avifParsePixelAspectRatioBox`, `src/write.c` function `avifEncoderWritePaspProperty`, and the presented-image property selection in `src/read.c` at `092276ce89098ead06db80975173191e5fee1826` | Preserve the two unsigned 32-bit relative spacings on the associated image item, reject zero or duplicate ratios, and map the displayed pixel width-to-height ratio into ImageSharp's existing unitless resolution metadata. Exchange the metadata axes after a quarter-turn presentation rotation and fall back from a derived grid to its first decodable tile only when the grid does not declare `pasp`. This remains one still-image presentation property and introduces no generic transform, sample-entry, or display model. |
| `Av1CodecConfiguration`, `HeifItem.Av1CodecConfiguration`, `Av1HeifItemDecoder`, and AV1 grid configuration checks | AV1-ISOBMFF sections 2.3.3 and 2.3.4 codec-configuration record syntax and semantics; AVIF sections 2.1, 2.2.1, and 2.2.3 AV1 image-item, item-configuration, and HDR metadata constraints; AV1 sections 5.8.3, 5.8.4, 6.7.3, and 6.7.4 HDR metadata syntax and semantics; ISOBMFF mastering-display and content-light image properties; ITU-T H.274 section 8.9 mastering-display field semantics; MIAF section 7.3.11.4.1 grid input constraints | libavif `src/read.c` functions `avifParseCodecConfiguration`, `avifDecoderItemValidateProperties`, `avifReadCodecConfigProperty`, `avifParseContentLightLevelInformation`, and `avifSkipMasteringDisplayColourVolume` at `092276ce89098ead06db80975173191e5fee1826`; libaom `av1/decoder/obu.c` functions `read_metadata`, `read_metadata_hdr_cll`, and `read_metadata_hdr_mdcv` at `03087864cf4bea6abb0d28f95cf7843511413d8f` | Associate exactly one `av1C` property with each decoded `av01` image item, validate the fixed record and its bit depth/chroma fields against the item's AV1 sequence header and optional `pixi` channel depths, require matching configurations across grid tiles, and report the encoded image precision and monochrome shape through `HeifMetadata`. Validate low-overhead OBU framing, require exactly one sequence header in the image item, allow at most one first-position sequence header in `configOBUs`, and compare a repeated header's extension and payload exactly while ignoring only its legal size-field representation. Decode `clli` and `mdcv` as still-image item properties, validate matching HDR CLL and HDR MDCV metadata OBUs from the combined configuration/item sequence, and account for the different primary order and fixed-point precision of the ISOBMFF and AV1 MDCV representations. Expose the effective HDR values without adding sample groups, tracks, or media metadata. Related still-image HDR properties remain required. Consume but do not retain presentation-delay syntax, and introduce no sample entry, sample description, track, timing, or generic decoder-configuration model. |
| `HeifContentColorVolume`, `HeifItem.ContentColorVolume`, and `HeifDecoderCore` content color-volume parsing and presentation | HEIF content color-volume item property; AVIF 1.2 content color-volume requirements; ITU-T H.274 (V4) content colour volume syntax and semantics | libavif `src/read.c` function `avifSkipContentColourVolume` at `092276ce89098ead06db80975173191e5fee1826`; official ITU-T H.274 (V4), January 2026 | Decode only the bounded per-image `cclv` property: require zero cancellation, persistence, and reserved bits; preserve optional signed G/B/R primary coordinates and normalized minimum, maximum, and average luminance values; and validate their registered ranges and ordering. Expose the effective grid-or-tile still-image value through `HeifMetadata`. Do not add SEI persistence, retained video state, tracks, samples, timing, or a generic ISO BMFF color-volume box model. |
| `GridHeifItemDecoder` and `HeifDecoderCore` grid/thumbnail selection | ISO/IEC 23008-12 section 6.6.2.3 image-grid syntax and MIAF grid-cell constraints | libavif `src/read.c` functions `avifParseImageGridBox`, `avifDecoderDataAllocateImagePlanes`, and `avifDecoderDataCopyTileToImage` at `092276ce89098ead06db80975173191e5fee1826` | Parse version-zero 16-bit and 32-bit grid descriptors, preserve row-major `dimg` order, require the declared tile count and one coding format, validate canvas coverage and edge overlap, and crop only the rightmost column and bottom row while copying through ImageSharp pixel buffers. A primary grid whose tile codec is unavailable may use only a decodable thumbnail that explicitly references that grid. |
| `HeifDecoderCore` alpha auxiliary selection/composition and `GridHeifItemDecoder` auxiliary tile ordering | ISO/IEC 23008-12 alpha auxiliary image semantics, `auxC`, `auxl`, `prem`, and per-grid-tile alpha relationships | libavif `src/read.c` functions `avifParseAuxiliaryTypeProperty`, `avifDecoderItemIsAlphaAux`, `avifMetaFindAlphaItem`, and `avifDecoderCheckAlphaProperties`, plus `src/scale.c` box-filter scaling at `092276ce89098ead06db80975173191e5fee1826` | Recognize both registered alpha URNs, decode a direct alpha image/grid or the complete row-major set of per-color-tile alpha auxiliaries, normalize through `L16`, box-resample differing auxiliary dimensions, compose through `Rgba64` and `PixelOperations<TPixel>`, and unassociate `prem` color samples with transparent-black handling. This remains an image-item relationship only; no track or generic media-reference model is introduced. |

This table is intentionally incomplete. Add a row before each additional AV1 or HEVC algorithm is ported or materially reshaped.

## Current implementation assessment

This assessment is based on the current source after the upstream ImageSharp merge and the baseline recorded above. Unless a result is stated explicitly, each item is a source-inspection finding rather than a verified interoperability claim.

### Public integration

- `HeifFormat` combines the HEIF, HEIC, HIF, and AVIF identities and extensions, but the implementation does not yet decode all payloads that contract implies.
- `HeifDecoder` now defaults to `Rgba32`, preserving decoded auxiliary alpha for non-generic loads.
- `HeifMetadata` now reports alpha presence and the corresponding 24/32-bit RGB pixel shape, but complete decoded HEVC/AV1 bit depth, monochrome/chroma layout, color signaling, and profiles remain absent.
- `IHeifEncoderOptions` is empty, and the encoder exposes no meaningful quality, speed, lossless, subsampling, bit-depth, or alpha policy.
- HEIF/HEIC/AVIF is absent from the format source-generation list in `_Formats.ttinclude`, so the standard ImageSharp save extensions are not generated.
- Configuration registration exists, but it currently registers capabilities broader than the implementation provides.

### HEIF/ISO BMFF container

- The bounded reader now handles basic, extended-size, UUID, and permitted top-level to-end boxes, skips unknown optional top-level and metadata children, and rejects child boxes that escape their parent.
- File type handling now evaluates supported still-image major and compatible brands while rejecting registered sequence major brands without adding track or timing support.
- Item IDs are resolved as keys rather than list indices; metadata children are indexed and parsed in dependency order rather than physical order.
- Item property associations now preserve physical `ipco` indices, apply one-based 7-bit or 15-bit indices and essential flags, associate by item ID, and reject arbitrary unknown essential properties.
- Item locations now support bounded file-relative and `idat`-relative storage, multiple ordered extents, versioned item IDs, 0/4/8-byte registered field sizes, and 64-bit offsets. Referenced-item construction method two and external data references remain explicitly unsupported.
- Grid derived-image decoding now parses both registered descriptor widths, resolves the ordered `dimg` cells, validates tile count, coding format, dimensions, canvas coverage, and edge overlap, then composes the output through ImageSharp row buffers. Unsupported grid tile codecs can select only a decodable thumbnail of the same primary grid. Independent AV1 and JPEG grid fixtures are still required, and HEVC grids remain blocked on the HEVC decoder.
- Alpha auxiliary decoding now recognizes `auxC`, `auxl`, and `prem`, supports a direct auxiliary image/grid and the per-color-grid-tile form, preserves normalized alpha through `L16`/`Rgba64`, box-resamples differing plane sizes, and reports alpha presence. HEVC alpha remains blocked on the HEVC image-item decoder, while independent AVIF alpha fixtures are still required against the incomplete AV1 reconstruction pipeline.
- Clean aperture, image rotation, and image mirror properties now validate their registered payloads, exact integer crop geometry, and essential associations; affect Identify dimensions; and reuse ImageSharp's optimized crop/rotate/flip processors after auxiliary alpha composition in the MIAF-defined order. Independent transform vectors must still verify every crop/rotation/mirror/alpha combination.
- Decode now resolves `cdsc`-associated Exif and `application/rdf+xml` XMP items for the primary still image, validates the declared Exif TIFF-header offset, and attaches the payloads through ImageSharp's existing profile types before presentation transforms. Independent AVIF, HEIC, and HIF metadata fixtures and Identify-time profile reporting remain required.
- ICC and `nclx` CICP color properties are now associated with the presented color item instead of global parser state, validated, exposed on Decode and Identify through the existing ImageSharp profiles, and used to override matching AV1 bitstream color fields before still-image color conversion. Independent ICC/CICP fixtures, decoded AV1 bitstream-CICP fallback metadata, ICC conversion coverage, and HEVC integration remain required.
- Pixel aspect ratio now preserves the complete unsigned spacing pair, affects Decode and Identify through ImageSharp's existing unitless resolution metadata, and follows quarter-turn presentation rotation. Independent grid, rotation, and maximum-spacing fixtures remain required.
- AV1 codec configuration is now retained per `av01` image item rather than in decoder-global state. Decode requires the property, validates its fixed record against the item's sequence header and any associated `pixi` channel depths, and requires matching configurations across grid tiles. Identify now reports the configuration's 8/10/12-bit precision and monochrome shape. The optional `configOBUs` sequence is bounded and validated, including its mandatory size fields, first-position/at-most-one sequence-header rule, the image item's exactly-one sequence-header rule, and exact comparison of a repeated configuration header with the item header. Content light-level and mastering-display color-volume information are decoded from the bounded `clli` and `mdcv` image properties and matching AV1 HDR metadata OBUs, with the representations' distinct fixed-point precision, grid/property precedence, and `SkipMetadata` behavior preserved. The still-image `cclv` property preserves optional content primaries and normalized luminance limits without importing its video-SEI state model. Ambient, reference-viewing-environment, and nominal-diffuse-white image properties remain required; HEVC `hvcC` remains unimplemented.
- Several image-item properties and relationships remain missing or parsed without fully affecting output.
- Identify and decode now use the same bounded metadata parser and both validate the complete leading file type box. The parsed state is still mutable and Identify does not yet report the complete bit depth, color, profile, or transform model.

### HEVC decoder and encoder

- `Heif4CharCode` recognizes `hvc1` image items and `hvcC` configuration, and Identify classifies HEVC fixtures, but `HeifCompressionFactory` has no HEVC item decoder.
- There is no HEVC bitstream parser, CABAC decoder, coding-tree reconstruction, still-image intra prediction, inverse transform, deblocking, sample-adaptive offset, or high-bit-depth path.
- There is no HEVC encoder. The current HEIC-branded encoder writes a legacy JPEG payload and therefore cannot provide HEIC output.
- Existing HEVC tests prove container identification only; they do not decode or compare HEIC pixels.

### AV1 decoder

- The single-still `Av1Decoder` path now parses tile state before allocating and reconstructing one independently decodable frame, and it disposes the reconstruction planes after pixel conversion. It deliberately does not retain animation/video reference frames or implement `show_existing_frame` playback state.
- The reconstruction pipeline now records plane-relative transform geometry, preserves tile-local delta-Q and delta-LF predictors, derives segmentation and reference-adjusted filter levels, and runs the exact scalar low/high-bit-depth AV1 4-, 6-, 8-, and 14-tap deblocking kernels in normative vertical-then-horizontal order. It then applies scalar CDEF direction search, luma variance adjustment, primary and secondary constrained taps, chroma direction conversion, high-bit-depth scaling, skipped-block selection, and frame-edge sentinel handling from immutable per-plane snapshots. Active super-resolution derives the Appendix A bounded coded width and applies the exact 64-phase, 8-tap horizontal filter with aligned reconstruction-edge input, 8/10/12-bit clipping, and the existing cross-platform `Vector128_.MultiplyAddAdjacent` helper. Loop restoration follows super-resolution, preserves the required pre-CDEF deblocked context at internal stripes, and applies decoded Wiener or self-guided units from immutable plane snapshots. The visible still-image path then applies the complete self-contained film-grain parameter set after all in-loop filters. Independent 8-, 10-, and 12-bit AVIF vectors exercising every active filter and grain stage remain required before these paths have external pixel-level verification.
- Palette mode now reads the normative luma/chroma mode and size CDFs, neighbor color caches, high-bit-depth color syntax, diagonal color-index maps, clipped-edge padding, and direct sample prediction through the existing reconstruction pipeline. The scalar implementation matches the pinned libaom source, but an independently encoded palette AVIF fixture is still required before this path is independently verified.
- Non-reduced still-image sequence parsing now consumes decoder-model operating parameters, temporal presentation fields, and OBU-layer-applicable buffer-removal fields only to preserve AV1 bit alignment. The scheduling values are not retained, and no movie, track, timing, playback, or generic ISO BMFF surface has been introduced. Existing focused sequence-header coverage exercises only reduced-still syntax, so an independent non-reduced still AVIF vector remains required.
- Loop-restoration unit parsing records tile-local switchable/Wiener/self-guided filter selections and coefficients in frame-owned plane grids, including super-resolution-adjusted unit corners and the corrected conditional 64x64-superblock unit-size bit. The active restoration stage implements the normative unit geometry, striped deblocked boundaries, Wiener filtering, self-guided projection, and 8/10/12-bit clipping, while reusing the existing SIMD-backed super-resolution and adjacent multiply/add primitives. Independently encoded fixtures covering every parameter set, plane layout, bit depth, and frame-edge geometry are still required. Other normative independently decodable still-image syntax paths still contain `NotImplementedException` or equivalent unsupported branches. Tile-local palette CDF adaptation is present; the remaining still-image frame-context behavior requires a separate source audit without introducing sequence playback state.
- The frame buffer now establishes two-byte native sample storage, logical plane rows, and sample-unit block strides for 10/12-bit frames. The active intra-prediction, inverse-transform, and block-reconstruction path selects native 16-bit samples for 10/12-bit frames and has focused pipeline wiring coverage. Chroma-from-luma storage, subsampling, parameter derivation, U/V sharing, and 8/10/12-bit prediction are active; independently encoded high-bit-depth and chroma-from-luma AVIF conformance files are still required.
- `Av1YuvConverter` now consumes the signaled full or limited range, every non-reserved AV1 H.273 matrix coefficient, transfer characteristics where the matrix definition requires them, subsampling, and chroma sample position for 8, 10, and 12-bit output. Its high-bit-depth decode and encode paths use allocator-backed `Rgb48` rows and the existing `PixelOperations<TPixel>` conversions, avoiding the former eight-bit intermediate. Encoder conversion covers monochrome, YUV 4:2:0, 4:2:2, and 4:4:4 with libavif-compatible box averaging. Identity, full/limited-range YCgCo, the fixed non-constant-luminance matrices, both fixed and chromaticity-derived constant/non-constant-luminance systems, SMPTE ST 2085, and PQ/HLG ICtCp are active in both directions. Independent vectors for every matrix, transfer, range, bit depth, sampling layout, and chroma position remain required before the complete color matrix is externally verified.
- The inverse-transform path allocates arrays in a per-transform hot path.
- The production prediction, transform, and nonlinear self-guided paths remain predominantly scalar. Normative super-resolution and Wiener horizontal products now reuse ImageSharp's cross-platform adjacent multiply/add SIMD helper with exact scalar fallbacks; further SIMD work must preserve these scalar reconstruction oracles.

### AV1 encoder

- `HeifEncoderCore.Encode()` now remains synchronous and waits for its temporary JPEG item encoding, so work and exceptions cannot outlive the ImageSharp encoder contract.
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
- The strongest AV1 integration test now drives the single-still decoder through tile parsing, reconstruction, and pixel conversion, but only verifies non-zero output rather than independent reference pixels.
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

Keep HEVC and AV1 bitstream state in separate codec implementations. Within each codec, separate parameter/sequence state, frame or picture headers, tile/slice entropy state, and the reconstructed still image. Do not introduce retained reference-frame or playback state for sequence behavior outside the supported image-item syntax. Give each allocation one owner and a deterministic disposal point.

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

Every phase exit gate is an internal prerequisite for the next phase. Only the Phase 9 exit gate together with a fully passing verification matrix marks the PR complete.

### Phase 0: establish a reproducible baseline

Tasks:

1. Build the merged solution in Release and record compile errors and warnings attributable to the WIP.
2. Run only the existing HEIF/HEVC/AV1 tests first, then record disabled tests and unexecuted asset coverage.
3. Pin an official libaom commit for AV1, the local libavif commit for AVIF/container comparison, and the independently reviewed HEVC implementation and interoperability references.
4. Create a provenance map from each WIP codec file to its specification section and exact upstream source. Preserve the current SVT-AV1 origins where applicable and identify which missing paths will use libaom.
5. Convert the completion boundary above into a feature matrix with `unsupported`, `parses`, `decodes`, `encodes`, and `verified independently` states.
6. Audit every source file under `src/ImageSharp/Formats/Heif`. Document every type and shared contract, and add technical comments wherever the code depends on non-obvious specification syntax, fixed-point arithmetic, transform staging, entropy state, buffer layout, or SIMD behavior. Keep public XML documentation limited to observable API behavior.

Exit gate:

- The post-merge branch has a recorded Release baseline, every existing failure is classified, all upstream code origins are known, and the complete HEIF source tree passes the documentation audit before additional porting begins.

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

### Phase 2: rebuild the bounded HEIF still-image reader around validated items

Tasks:

1. Implement a bounded box reader supporting 32-bit, 64-bit, and to-end box sizes where allowed, with overflow-safe arithmetic and correct parent bounds.
2. Accept the applicable HEIF, HEIC, and AVIF still-image brands through the major or compatible brand rules. Recognize and reject sequence/movie brands without implementing their track surface.
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

1. HEVC byte-stream and length-delimited NAL units, `hvcC`, VPS, SPS, PPS, access units, and the parameter-set and slice-header syntax permitted for independently decodable still-image items.
2. One coherent decoder lifecycle that owns parameter sets, picture state, slice/tile entropy state, and reconstructed planes.
3. CABAC arithmetic decoding and every required context transition.
4. Coding-tree, coding-unit, prediction-unit, and transform-unit traversal across all permitted sizes and partition modes.
5. Intra prediction for every luma and chroma mode, including strong intra smoothing and constrained prediction rules.
6. Scaling lists, inverse quantization, transform skip, every required inverse transform, range-extension precision, and lossless reconstruction.
7. Deblocking and sample-adaptive offset for every signaled luma/chroma and bit-depth path.
8. Tiles, wavefront entry points, dependent slices, and all other parallelization syntax permitted by the exposed still-image profiles.
9. Supplemental enhancement information that changes image presentation or metadata exposed by ImageSharp.

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

### Phase 8: SIMD and allocation optimization

SIMD work begins after the corresponding scalar stage has independent correctness tests; it then proceeds alongside subsequent functional phases.

Tasks:

1. Remove known avoidable allocations first: per-transform arrays, the intermediate RGB image, repeated block scratch arrays, and file-sized buffering.
2. Benchmark codec-specific costs for CABAC/range decode, inverse transforms, still-image prediction, deblocking, SAO, CDEF, restoration, chroma upsampling, color conversion, alpha packing, and grid copies.
3. Implement vector paths only for confirmed hot loops, using existing `Vector128`, `Vector256`, and `Vector512` helper and dispatch patterns where supported.
4. Prioritize shared color conversion and pixel packing, chroma upsampling, inverse-transform add-and-clip, intra predictors, HEVC deblock/SAO, AV1 loop filter/CDEF/restoration, and contiguous grid copies.
5. Port upstream SIMD algorithms only after mapping lane width, signedness, intermediate precision, rounding, saturation, edge extension, and high-bit-depth behavior to the scalar oracle.
6. Keep one scalar implementation as the specification-shaped reference. Vector paths must share tables and constants with it rather than duplicate codec policy.
7. Test scalar and each available hardware path with intrinsics explicitly enabled and disabled, including widths shorter than a vector, exact-vector widths, non-multiples, edges, maximum sample values, and high-bit-depth overflow cases.
8. Remove dead or commented SIMD experiments once a verified production path replaces them.

Exit gate:

- Benchmarks show a material improvement on representative AVIF files, allocation measurements meet an agreed budget, and every vector path is behaviorally identical to the scalar path for integer reconstruction or within the documented color-conversion tolerance.

### Phase 9: hardening, documentation, and release readiness

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
| AV1 decode tools | every normative transform type/size, still-image predictor, partition, palette, segmentation, quantization, entropy/context, lossless, deblock, CDEF, super-resolution, restoration, and film-grain path valid in independently decodable AVIF still-image items. |
| AV1 encode compression | real mode decision and rate/distortion selection across partitions, predictions, transforms, quantization, entropy coding, filters, lossless/lossy quality, and effort settings; no permanent fixed coding subset. |
| HEVC decode tools | every normative NAL/parameter/slice, CABAC, coding-tree, intra prediction, transform, quantization, range-extension, lossless, tile/wavefront, deblock, and SAO path valid in independently decodable still-image items for the exposed HEIC profiles. |
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
8. Measured SIMD/allocation work integrated after each scalar subsystem stabilizes.
9. Hardening and release gates.

This sequence does not define partial PR completion. The early 8-bit 4:2:0 decoders and smallest-valid encoders are temporary vertical slices, but the PR remains incomplete until all nine steps and the complete still-image verification matrix pass.

Do not begin by optimizing the current end-to-end pipeline: it cannot yet produce a correct HEIC or AVIF image, and several current data structures encode 8-bit 4:4:4 assumptions. Establish the correct scalar storage and behavior first so the reused or ported AOM/HEVC SIMD algorithms have a trustworthy managed oracle.
