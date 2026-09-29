# AV1/AVIF remaining implementation plan

Updated 2026-09-29. Existing changes must be preserved. Do not create worktrees or reload Roslynk.

## Working rules

- The port transcribes libaom (revision `06f068d1bc9f5159c6efb195ddf2f66e484ba511`) and libavif. The same input
  with the same settings gives the same stream as the pinned x64 `aomenc`, configured as libavif configures libaom.
  A mismatch is a porting bug.
- Find a mismatch with the stream compare and the block dump, then read the libaom function next to the port
  method. The working tree keeps no trace code.
- Build with the analyzers and commit each verified fix at once. Every multi-line statement is followed by a blank
  line.
- Write every new sample or coefficient kernel as an operator first (scalar overload plus one overload per register
  width, one traversal). Measure it against the scalar form before keeping it.
- libaom and libavif are the authority. The managed decoder is not an oracle; check streams with `aomdec`.
- Tests assert observable correctness. A test does not pin a libaom decision to a fixed number unless it states why.

## Verified parity

| Path (libavif configuration) | State |
|---|---|
| Stills: tune=iq all-intra, alpha tune=psnr, lossless; 8/10/12-bit, 4:0:0, 4:2:0, 4:2:2, 4:4:4, odd sizes, speeds 0-9 | 620/620 identical, and each tune=iq feature 60/60 |
| RT sequences, speeds 7-9: tune=ssim, `AOM_CBR` quantizer +/-4, 30 fps time base | splash, 512x384, 10-bit, screen and cut identical, except cut speed 7 (see open items) |
| GOOD sequences with lag 0 (the alpha case), speeds 0-6, global motion on, psnr and ssim | 14/14 identical with a fixed quantizer |
| GOOD `AOM_Q` with the quantizer range 0-63, lag 0 | speeds 3 and 6 identical; speed 0 differs from frame 5 |

Encoder defaults follow avifenc: quality 60, speed 6, full range, BT.601 matrix, and the format of `avifReadImage()`.
Known difference: ImageSharp's JPEG decoder reports an unusual sampling layout as `YCbCrRatio420`, where libavif
falls back to 4:4:4.

## Open items, in order

1. GOOD sequences without alpha: libaom's default 35-frame lag. This is the avifenc default path (speed 6). Port:
   - the look-ahead first pass (`av1_first_pass` in LAP mode) and its statistics;
   - key frame and group decisions (`define_gf_group`, `calculate_gf_length`, `find_next_key_frame`);
   - the GF pyramid (`av1_gop_setup_structure`): frames coded out of display order, `show_existing_frame`, the
     reference refresh of each layer, and the Q-mode quantizer of each layer (`rc_pick_q_and_bounds_q_mode`);
   - the alt-ref temporal filter (`av1_temporal_filter`);
   - the TPL model (`av1_tpl_setup_stats`) and its rdmult and quantizer adjustments;
   - frame-end CDF propagation and error resilience as the lagged configuration sets them.
   First check what `Av1GoodQualityReferenceStructure` already has.
2. GOOD `AOM_Q` lag 0, speed 0, frame 5: at the 32x32 HORZ_4 partition at (128,224), the 32x8 leaf (128,248) picks
   NEAR_NEARMV LAST+LAST2 (rd 3057406). libaom picks NEWMV LAST (rd 3077341) and costs NEAR_NEARMV at 3204275.
3. RT CBR cut speed 7, frame 6 (the scene cut at qindex 72): block (80,64) is inter in libaom and intra in the port.
4. `enable_winner_mode_for_tx_size_srch` at speed 2 below 480p is `boosted ? 0 : 1`; the port tests intra frames
   only.
5. `pred_sse` after a search with integer vectors keeps the filter search value in libaom; the port uses the search.
6. RT frame control: `frames_to_key`, `direct_partition_merging` where it applies, `enable_ref_short_signaling`
   below 360p, and `context_update_tile_id` for multi-tile frames.

## SIMD operator-pattern gaps

Each kernel gets an operator, a traversal, and a `FeatureTestRunner` test against the scalar libaom definition.
Hottest first:

- Compound and inter-intra mask search: the mask inversion of `RefineCompoundVectors`, `Av1WedgeMask.Fill` and
  `Av1InterIntraMaskBuilder.FillInterIntraMask` (libaom reads precomputed masks through
  `av1_get_contiguous_soft_mask`).
- Transform search: the energy grid of `PruneInterTransformTypes` (`get_energy_distribution_finer`).
- Intra estimation: the scalar Hadamard of `GetHadamardCost`, `TransformForModeEstimation`, and the Vector128-only
  `HadamardEstimationColumns`.
- `GetDirectionalModeSkipMask`, the palette k-means `Accumulate` members and `CalculateCentroids`, and
  `CopyPaletteSamples`.
- Per superblock: the Vector128-only `FilterTemporalSource` and `ConvolveIntraPartition`, and
  `FillResidueOutsideFrame`.
- Frame filters: the self-guided and Wiener statistics and errors of the loop restoration search, the shared
  self-guided filter, and the CDEF `CopyPlane`, `FindDirection` and `FilterBlock` width variants.
- Once per frame: the screen content detector moments and copy, the block-copy hash, and `Av1FlowField.Upscale`.
- `Av1ChromaFromLumaPredictor.Operator.cs` names `AdvSimd` outside the vector shims.

Test note: `HwIntrinsics.DisableAVX512F` does not clear `Vector512.IsHardwareAccelerated` on .NET 10.

## Tests

- Update test callers affected by the search, picture-buffer and partition-policy arguments. Do not weaken
  expectations or add test-only production overloads.
- Resolve the failing inter tests against libaom: `ProductionTileSelectsNonRegularInterpolation`,
  `ProductionTileSelectsDualAxisInterpolationHighBitDepth`, `InterBlockCanSkipNonzeroQuantizedResiduals`,
  `IntraBlockCopyCanSkipNonzeroQuantizedResiduals`, `SequenceEncoderWritesSelectedGlobalTranslation`,
  `IndependentFrameAndSequenceModesRetainDifferentMotionPolicies`,
  `ResolutionAndFrameRoleOverrideInitialSecondCandidatePolicy`, `PixelSearchFallsBackToExhaustiveMesh`.
- Cover alpha, tiles, grids and dependent animation. Follow the `VerifyEncoder`, `DebugSave` and
  `CompareToReferenceOutput` patterns. HEIF tests stay in proportion to the other formats (whole suite about one
  minute).
- Verify public option precedence, metadata/CICP/ICC, timing, cancellation, non-seekable output and allocator
  ownership.
- `PaletteColorMapCostDoesNotAllocateAfterEntropyInitialization` failed once under load; cause unknown.

## Performance

- Measure complete encode and decode against the optimized native build; report time, size and allocations.
- Known causes: primitives are 2-5x slower per call than libaom (forward transform, quantizer, trellis, coefficient
  cost), and speed 6 reconstructs chroma candidates and the luma winner where libaom does not.
- `Buffer2D.DangerousGetSingleSpan()` allocates through LINQ `Single()`. Hot paths index `FastMemoryGroup[0]`.

## Known limits

- The pinned `aomenc.exe` cannot encode 12-bit 4:4:4, so that format has no reference stream.

## Reference material

- libaom: `D:\GitHub\AOMediaCodec\aom` at the revision above. libavif: `D:\GitHub\AOMediaCodec\libavif`.
- Comparison build: `D:\GitHub\ynse01\aom-06f068d1-build-x64-release` (MSVC 19.51, NASM 3.02, x86-64, runtime CPU
  detection, high bit depth). Where libaom's C and SIMD code disagree, the port follows the x64 SIMD result.
- Production stays fully managed. Native tools, harnesses and comparison artifacts stay outside the repository.

## Final cleanup

- Remove the `DiagnosticSymbolTrace` code from the source.
- Delete `tests/ImageSharp.Benchmarks/Codecs/Heif/Native/aom_benchmark.c`, `Native/CMakeLists.txt`,
  `Av1SequenceEncoderBenchmarks`, `Av1ReferenceDecoderVerificationTests` and other temporary native tooling. Review
  exact targets first.
- Review the diff against `upstream/main`. Changes outside `src/ImageSharp/Formats/Heif` go to their own branch
  from `upstream/main` as a separate pull request, raised with the owner first.
