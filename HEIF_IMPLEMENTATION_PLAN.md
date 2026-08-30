# AVIF and AV1 implementation plan

## Goal

Complete a production-quality, fully managed AV1 codec and its bounded AVIF/HEIF image container integration for ImageSharp. The finished work must decode and encode still images and bounded image sequences, preserve source precision, use ImageSharp memory ownership, and provide SIMD-first hot paths with one behaviorally identical scalar fallback.

This plan is the authoritative delivery checklist. A source file, unit test, build, self-roundtrip, or local implementation is not completion evidence by itself.

## Source authority

- AV1 codec syntax, tables, fixed-point arithmetic, prediction, transforms, entropy behavior, filters, and lifecycle behavior must be ported and checked only against libaom commit 03087864cf4bea6abb0d28f95cf7843511413d8f.
- The AV1 specification is the normative behavioral description. It does not authorize copying an implementation from another codec library.
- Existing ImageSharp and JPEG code is the architecture, allocator, SIMD dispatch, pixel conversion, and test-API pattern. It is not an alternate AV1 algorithm source.
- Production code must not load, invoke, install, or fall back to a native codec.
- External artifacts may be retained only as test inputs or expected outputs with recorded provenance. They must never become an implementation source.

## Status notation

- [x] Verified: the current behavior has exact evidence from the pinned reference and the evidence proves the production contract.
- [~] Locally implemented, checkpoint open: production source exists, but current-tree verification is missing or a known audit issue invalidates the checkpoint.
- [ ] Remaining: the production behavior is absent, incomplete, or has not reached its required implementation boundary.

## Current source reconciliation

Reconciled with the worktree on 2026-08-30.

- [~] The bounded container reader, still-image path, sequence parser, AV1 decoder, color pipeline, presentation pipeline, and broad AV1 test suite exist locally.
- [~] The inter-frame decoder contains implementations for single-reference prediction, compound references, inter-intra prediction, selectable compound blending, OBMC, scaled references, local warped motion, and global motion. These downstream paths must not be called verified until the single-reference checkpoint below is corrected and rerun.
- [~] Loop filtering, CDEF, super-resolution, restoration, film grain, layered presentation, alpha composition, and color conversion exist locally. Shared-source cleanup changed the current tree, so final production-path verification is open.
- [~] AV1 writer primitives, forward transforms, symbol encoding, and tile-writing source exist locally, but they are not connected to the public encoder.
- [ ] The public AV1 encoder is not implemented. HeifEncoderCore.Encode throws NotSupportedException when AV1 is selected.
- [x] Removed codec production code, registrations, tests, benchmarks, fixtures, reference outputs, downloaded tools, downloaded source trees, and notices have been manually deleted and verified by the cleanup evidence below.
- [ ] The complete decoder and encoder release matrix is not complete.

## Immediate execution queue

Work must proceed in this order. Do not skip to a later item while an earlier checkpoint is open.

### 1. Finish and verify the AV1-only cleanup

- [x] Remove production types, registrations, constants, parser branches, properties, tests, benchmarks, fixtures, reference outputs, notices, and documentation for removed codec work.
- [x] Remove downloaded non-libaom reference source, tools, generated outputs, and local installations.
- [x] Retain the pinned libaom source and build artifacts required for AV1 verification.
- [x] Retain user-supplied AV1 fixtures and their recorded expected outputs.
- [x] Audit production source, tests, benchmarks, assets, project files, notices, and documentation for stale removed-code references.
- [x] Build the current source targets in Release with restore disabled, build servers disabled, and one MSBuild node.
- [x] Run the focused AV1/container tests needed to prove the cleanup did not damage AVIF behavior.
- [x] Run scoped semantic and StyleCop inspection, whitespace inspection, and git diff --check.
- [x] Record the exact verified evidence in this plan.

Verified cleanup evidence on 2026-08-30:

- Release source builds passed for net10.0 and net11.0 with zero warnings and zero errors. Both builds used `--no-restore`, `--disable-build-servers`, and one MSBuild node.
- The focused net10.0 HEIF decoder, encoder, metadata, sequence-parser, and AV1 reconstruction set passed 221 of 221 tests with zero failures and zero skips.
- The Roslyn compiler and configured StyleCop analyzers accepted the changed production source. Roslynk's `open_solution` entry point was attempted separately but failed before returning a solution handle, so no Roslynk result is claimed.
- The final text and filename audit found no removed-code references outside the unchanged repository and shared-infrastructure `.gitattributes` patterns.
- `git diff --check` passed and neither `.gitattributes` file changed.

### 2. Correct the single-reference inter-frame checkpoint

The following findings are confirmed by direct source inspection and keep the checkpoint open.

- [ ] Correct interpolation-filter syntax in Av1TileReader.
  - Current source treats every global-motion type other than Translation as non-translational.
  - Pinned libaom omits interpolation-filter syntax only when the selected model type is greater than Translation.
  - Identity GLOBALMV blocks of sufficient size must consume switchable-filter symbols.
  - Add production-path syntax coverage using the default Identity model. A test that forces Translation does not prove this rule.
- [ ] Correct both spatial reference-MV extension loops in Av1ReferenceMotionVectors.
  - Current source stops spatial extension when the stack reaches two entries.
  - Pinned libaom extends the stack through MAX_REF_MV_STACK_SIZE, which is eight.
  - Preserve DRL candidates and consume every required DRL symbol.
  - Describe this as spatial single-reference extension, never temporal extension.
- [ ] Replace the contiguous-span dependency in Av1FrameBuffer and all affected inter reconstruction callers.
  - Current GetPaddedPlaneSpan calls DangerousGetSingleSpan.
  - Buffer2D may use multiple memory groups under a constrained allocator.
  - Implement an efficient group-safe row-oriented contract using established ImageSharp Buffer2D access patterns, or prove and enforce a real contiguous-allocation invariant at the allocator boundary.
  - Do not copy planes and do not allocate per block, row, or scanline.
  - Audit direct DangerousGetSingleSpan use in reconstruction, reference-border extension, film grain, copying, and encoder work rather than fixing only one wrapper.
- [ ] Prove the real Av1BlockDecoder.DecodeBlock inter-reconstruction branch.
  - Decode the progressive dependent-frame fixture through the complete public production path.
  - Compare the final frame's native Y, Cb, and Cr planes exactly with pinned libaom output.
  - Compare the final presented image through the established ImageSharp reference-image comparison API.
  - Do not substitute an internal helper test, fake tile reader, non-zero assertion, custom pixel loop, or tolerant comparison.
- [ ] Prove motion-field ownership and lifetime.
  - Track initialization, retained-slot aliases, failure unwinding, presentation ownership, decoder-result ownership, and final disposal.
  - Every allocator-owned object must be returned exactly once.
- [ ] Correct stale documentation.
  - Av1InterFrameModeInfoTests must describe the behavior it actually proves.
  - Do not claim production reconstruction, constrained allocation, ownership, or reference-stack coverage unless the test executes that contract.

Checkpoint gate:

- [ ] Default Identity and Translation GLOBALMV syntax cases pass.
- [ ] Eight-entry spatial extension and DRL syntax cases pass.
- [ ] The exact dependent-frame native-plane comparison passes.
- [ ] The established exact presentation comparison passes.
- [ ] Normal, AVX-512-disabled, AVX-disabled, and scalar FeatureTestRunner configurations pass where supported.
- [ ] Constrained multi-group allocation passes without copying or per-block allocation.
- [ ] Motion-field allocation tracking is balanced across success and failure.
- [ ] Release builds for net10.0 and net11.0 pass with zero errors.
- [ ] Focused Release tests pass with zero failures or skips.
- [ ] Scoped semantic, StyleCop, whitespace, and git diff checks pass.
- [ ] Only after all evidence is recorded may this checkpoint be committed.

### 3. Reverify downstream inter prediction in recorded order

These implementations exist locally but inherit the open single-reference syntax, buffer, and ownership foundation.

- [~] Compound reference selection, paired reference-MV derivation, and equal averaging.
- [~] Inter-intra prediction.
- [~] Distance-weighted compound prediction.
- [~] Wedge compound prediction.
- [~] Difference-weighted compound prediction.
- [~] OBMC.
- [~] Scaled-reference prediction.
- [~] Local warped prediction.
- [~] Non-translational global prediction.
- [~] Inter deblocking decisions and reference/mode deltas.

For every item:

- [ ] Trace syntax and arithmetic to the pinned libaom commit.
- [ ] Execute the real production decoder path.
- [ ] Compare native planes exactly.
- [ ] Compare presentation through the established reference-image API.
- [ ] Run constrained allocator and exactly-once ownership coverage.
- [ ] Run FeatureTestRunner for SIMD and scalar dispatch when the implementation has SIMD.
- [ ] Record focused Release evidence before marking the item verified.

### 4. Close AV1 decoder coverage

Previously verified algorithm checkpoints remain valuable evidence, but the final decoder gate requires a fresh current-tree run after the inter and cleanup corrections.

- [x] Bounded OBU framing, sequence headers, frame headers, tile groups, alignment, and trailing-bit parsing have pinned-reference checkpoint evidence.
- [x] Partition traversal, mode information, segmentation, delta quantization, transform-size selection, coefficient decoding, inverse quantization, and inverse transforms have pinned-reference checkpoint evidence.
- [x] Intra prediction covers directional, DC, smooth, Paeth, chroma-from-luma, filter-intra, and palette families with the established operator architecture.
- [x] Intra-block copy has exact native reconstruction and feature-isolated SIMD evidence.
- [x] Lossless inverse transform, loop filtering, CDEF, super-resolution, restoration, and film grain have focused checkpoint evidence.
- [~] Retained references, CDF snapshots, segmentation maps, global motion, temporal motion fields, and dependent-frame lifecycle exist locally and require current-tree re-verification.
- [~] All-intra and dependent-frame profile fixtures exist for 8, 10, and 12-bit monochrome, 4:2:0, 4:2:2, and 4:4:4 paths.
- [ ] Re-run the exact current-tree native-plane matrix through the production decoder.
- [ ] Re-run the exact current-tree presentation matrix through ImageSharp's established comparison API.
- [ ] Verify malformed/truncated data, frame IDs, reference slots, tile bounds, allocation limits, cancellation, and failure unwinding.
- [ ] Verify still items and bounded sequences from file, memory, non-seekable, and short-read streams.
- [ ] Verify ICC, CICP, alpha, grids, pixel aspect ratio, clean aperture, rotation, mirroring, metadata, and every presented sequence frame.
- [ ] Complete the public AVIF format/API review so registered capabilities match implemented behavior.
- [ ] Remove or reject every valid in-scope AV1 syntax branch that remains silently ignored or unsupported.

Decoder exit gate:

- [ ] Every supported native format and AV1 tool has exact pinned-libaom production-path evidence.
- [ ] Every supported presentation behavior has established reference-image evidence at the correct output precision.
- [ ] No decoder path relies on a native codec, copied plane, per-block allocation, or contiguous memory-group accident.
- [ ] All allocator ownership is deterministic and exactly once.
- [ ] Full focused Release verification is recorded with no false coverage claims.

## AV1 encoder implementation

Writer primitives are not an encoder. The public encoder remains incomplete until it produces independently decodable AV1 payloads and AVIF containers for every exposed option.

### 5. Define and enforce the encoder contract

- [ ] Finalize observable options for quality, effort, lossless mode, bit depth, chroma subsampling, alpha quality, metadata, and bounded sequences.
- [ ] Preserve high-bit-depth source precision through 16-bit RGB and native 10/12-bit component planes.
- [ ] Reject unsupported combinations at the public boundary before writing output.
- [ ] Register only capabilities that the completed encoder proves.

### 6. Build the complete AV1 frame encoder

- [~] SIMD-first RGB-to-native-plane conversion exists locally.
- [~] Forward transform families and transform workspace exist locally.
- [~] Symbol writer, coefficient writer, and tile writer fragments exist locally.
- [ ] Connect a frame-owned encoder lifecycle using ImageSharp allocators and pools.
- [ ] Write compliant temporal delimiter, sequence header, frame header, tile group, metadata, and padding OBUs as required.
- [ ] Implement superblock and partition analysis for every permitted block size and partition.
- [ ] Implement intra mode search, chroma mode search, palette, filter intra, chroma-from-luma, and intra-block copy decisions.
- [ ] Implement inter mode search for bounded sequences, including reference selection and the decoder-supported inter tools.
- [ ] Implement transform-size/type search, forward transform, quantization, coefficient optimization, and lossless behavior.
- [ ] Implement real rate-distortion selection and make quality and effort change work, size, and output quality.
- [ ] Implement tile-local entropy coding and CDF update behavior.
- [ ] Implement legal deblocking, CDEF, restoration, super-resolution, and film-grain signaling decisions.
- [ ] Remove per-transform and per-block managed allocations from active encoder paths.
- [ ] Use descending SIMD dispatch: Vector512, Vector256, Vector128, then scalar.
- [ ] Verify every SIMD operator with FeatureTestRunner and an independent scalar oracle shaped from the same pinned libaom behavior.

### 7. Write complete AVIF output

- [ ] Write the correct AVIF file type, item information, locations, references, properties, AV1 configuration, dimensions, color, alpha, metadata, and media data.
- [ ] Support single images, alpha auxiliary images, grids, multiple extents, and bounded image sequences in the final public scope.
- [ ] Preserve ICC, Exif, and XMP according to encoder options.
- [ ] Write CICP, range, chroma position, bit depth, and subsampling values that match the encoded planes.
- [ ] Apply orientation and clean-aperture behavior consistently with ImageSharp encoder conventions.
- [ ] Stream output through allocator-backed chunked storage without file-sized copies or ToArray materialization.

Encoder exit gate:

- [ ] Pinned libaom accepts every produced AV1 payload.
- [ ] Lossless output is exact at native-plane and final-pixel precision.
- [ ] Lossy output demonstrates recorded quality and effort tradeoffs with absolute size, quality, timing, and allocation evidence.
- [ ] 8, 10, and 12-bit monochrome, 4:2:0, 4:2:2, and 4:4:4 outputs pass.
- [ ] Alpha, grids, metadata, color profiles, transforms, and bounded sequences pass.
- [ ] ImageSharp decode of its own output is supplemental coverage only, never the sole oracle.
- [ ] Public encoding no longer throws for a supported AV1 request.
- [ ] Focused Release and FeatureTestRunner verification passes with exact recorded evidence.

## Architecture rules

- Follow the JPEG color-converter operator architecture exactly.
- Each distinct prediction traversal owns a family-named predictor type.
- The family .Operator.cs file defines the nested static operator contract.
- Each semantic readonly struct belongs to that owner and implements concrete scalar, Vector128, Vector256, and Vector512 arithmetic for the shared traversal.
- Do not place a distinct predictor beneath a broad Av1IntraPredictor or Av1InterPredictor.
- Do not create semantic forwarding wrappers, top-level operator types, hardware-width-named operator types, CRTP contracts, or one file containing unrelated semantic operators.
- Forward transforms belong to Av1ForwardTransformer and its semantic operator files.
- Inverse axis transforms belong to Av1Inverse2dTransformer and its semantic operator files.
- Reconstruction output operators belong to Av1InverseTransformer.
- Shared lane primitives belong only in explicitly named Operations types.
- Dispatch from widest to narrowest supported SIMD width, then execute one scalar tail.
- Keep codec execution sequential. Do not add parallel execution inside the codec.
- Do not allocate per row, block, transform, scanline, or SIMD tail.
- Use ImageSharp allocators and pools. Do not use ToArray to cross an ownership boundary.
- On internal types, use public members when other types consume them; reserve private members for type-local behavior.
- Use established ImageSharp test data, allocator tracking, FeatureTestRunner, and reference-image comparison APIs. Do not build custom substitutes.
- Public XML documentation describes observable behavior only.
- Inline comments explain the pinned numerical rule, ownership boundary, edge extension, entropy ordering, or SIMD shape at technically complex points.
- Every multiline statement or declaration is followed by vertical whitespace.
- Do not edit .gitattributes directly.
- Do not install or download tools without explicit permission.

## Final verification matrix

- [ ] Release source build: net10.0, zero errors.
- [ ] Release source build: net11.0, zero errors.
- [ ] Scoped semantic inspection: zero compiler errors attributable to this work.
- [ ] Focused decoder syntax, reconstruction, ownership, presentation, and malformed-input tests.
- [ ] Focused encoder syntax, payload, container, precision, ownership, and option tests.
- [ ] FeatureTestRunner coverage for normal, narrower SIMD tiers, and scalar fallback.
- [ ] Constrained multi-group allocator coverage with balanced exactly-once returns.
- [ ] Exact native-plane comparisons against pinned libaom.
- [ ] Established final-presentation comparisons at the target pixel precision.
- [ ] Scoped StyleCop and vertical-whitespace inspection.
- [ ] No stale unsupported capability claims or removed-code references.
- [ ] No restore-source failures, background test hosts, detached processes, or crash-report popups.
- [ ] .gitattributes unchanged.
- [ ] git diff --check clean.
- [ ] Documentation records exact commands, counts, fixture hashes, and results.
- [ ] Commit only after the relevant checkpoint is genuinely complete.
- [ ] Do not push.
