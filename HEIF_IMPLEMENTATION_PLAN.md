# AV1/AVIF remaining implementation plan

Updated 2026-09-25. Milestone 1 is complete for still pictures. Next: inter frames (milestone 2).
Existing changes must be preserved. Do not create worktrees or reload Roslynk.

## Working rule

Build and run the `FullyQualifiedName~Formats.Heif` tests after each change. Compare the failed-test set with the
previous run before the next change. Do not accumulate unverified edits. The earlier rule that delayed builds and
tests until milestones 1 and 2 were complete is withdrawn: it let about 12,500 uncompiled lines accumulate.

## Authority

libaom and libavif are the authority for every behavior. The managed decoder is a port and is not an oracle.
When the encoder, the decoder, or a test disagree, read the reference source and cite the file and line.
Check encoder streams with the reference decoder (`aomdec`), and check the managed decoder against `aomdec`
on the same stream. `Av1ReferenceDecoderVerificationTests` does both when `IMAGESHARP_AOMDEC` holds the path of
`aomdec.exe`. It is temporary native tooling and is removed at final cleanup.

Tests assert observable correctness: reference-decoder agreement, encoder/decoder reconstruction equality,
valid syntax, invariants, allocation behavior, and option normalization. A test does not assert that a mode,
partition, cost, or byte count equals a fixed number unless it states the reason.

## Reference configuration gap, found 2026-09-18

With the pinned libaom 3.15 and libavif, a lossy color still image (non-identity matrix) uses `tune=iq` by default
(`libavif/src/codec_aom.c:737-781`). Alpha uses `tune=psnr`. Lossless sets no tune. `tune=iq` sets
(`av1_cx_iface.c:1953-1993`): quantization matrices with the all-intra level formula, sharpness 7, the QM-PSNR
distortion metric, adaptive
CDEF, chroma delta-q, variance-boost delta-q, anti-aliasing-aware screen detection, and adaptive sharpness.
Variance boost forces 64x64 superblocks (`encoder_utils.c:978-981`). libavif also uses a separate
quality-to-quantizer table for `tune=iq` (`codec_aom.c:603-648`). The port implements none of these, and it maps
quality with the non-IQ formula. Until this is ported, output matches libavif only for `tune=psnr` settings.
The superblock rule in `Av1FrameEncoder.CreateSequenceHeader` equals `av1_select_sb_size`
(`encoder_utils.c:1017-1039`) for one thread without delta-q modes.

## Verified state, 2026-09-25

Still pictures are byte-identical to the pinned x64 `aomenc.exe` in all 620 comparison cases.
The reference is the optimized x64 build. Where libaom's C and SIMD code disagree, the port follows the SIMD result.

Each case encodes the same Y4M source with the port and with `aomenc --usage=2 --passes=1 --end-usage=q
--lag-in-frames=0 --tile-columns=0 --tile-rows=0 --threads=1 --limit=1`. The two streams must be equal byte for byte,
and the decoded block decisions must be equal.

| Set | Cases |
|---|---|
| Bike at 512, 333 and 1000 pixels, 4:2:0 8-bit, qIndex 32/64/128/200, speeds 0-9 | 100 |
| Splash, Ducky, Calliphora (400 pixels) and synthetic screen content (320 pixels), qIndex 64 and 160 | 80 |
| The same four images, qIndex 32 and 255 | 80 |
| Bike 512: 4:2:0 10-bit, 4:4:4 8/10-bit, 4:2:2 8/10-bit, monochrome; qIndex 64 and 128 | 120 |
| Bike 512, 4:2:0 12-bit, qIndex 64 and 128 | 20 |
| Bike 512, lossless (qIndex 0), 4:2:0 and 4:4:4 | 20 |
| Bike at odd sizes 333x251 and 127x97, qIndex 64 and 200 | 40 |
| The four images in 4:4:4 8-bit and 4:2:0 10-bit, qIndex 64 and 160 | 160 |

All sets use speeds 0-9. The HEIF suite passes (10,518 tests) and the analyzer build has no errors.

Corrections verified in this period (each one moved a failing case to byte-identical):

- The final encode marks a copied or inter block as skipped when it keeps no coefficient (`av1_encode_sb`).
- `predict_skip_txfm` and `set_skip_txfm` are ported for inter and intra-block-copy luma.
- Chroma compares the cost of an uncoded residual with the SSE that `search_tx_type` reports. That SSE is in the
  transform domain when the distortion is measured there.
- Still pictures at speed 7 follow `av1_rd_use_partition`: every leaf is searched, finished subtrees are
  encoded as a dry run, and then the superblock is encoded. The dry run resets a luma transform type to DCT when
  it has no coefficients, and the final encode uses that DCT.
- The inter and intra-block-copy transform-type loop has the `adaptive_txb_search_level` and `skip_tx_search`
  exits of `search_tx_type`.
- The 16x16 Hadamard combine in `av1_block_yrd` wraps in 16 bits, as `hadamard_16x16_avx2` does. The C code
  does not wrap there, so 10-bit and 12-bit screen content at speed 9 differs between the C and x64 builds.

Known limits and open items:

- The pinned `aomenc.exe` cannot encode 12-bit 4:4:4 (`AV1E_SET_CHROMA_SUBSAMPLING_X` fails before the bit depth
  is set). That format has no reference stream.
- Two inter-search rules are not yet verified, because still pictures do not reach them:
  `EvaluateInterTransform` discards the transform-domain SSE that `try_tx_block_no_split` uses for
  `zero_blk_rd`, and `EncodePredictionLossyCandidate` measures every candidate in pixels under distortion
  policy 1, where `search_tx_type` measures candidates in the transform domain and only the winner in pixels.
- `PaletteColorMapCostDoesNotAllocateAfterEntropyInitialization` failed once under load and passed in all
  later runs. The cause is not yet known.
- The working tree still has temporary trace code (`Av1TileWriter`, `Av1IntraSuperblockEncoder.ReferenceModeDecision`),
  and the libaom tree has trace instrumentation. Both are removed at final cleanup.
- The comparison harness is scratch code outside the repository. It is copied into the test project to run and is
  never committed.

## Verified state, 2026-09-17 (historical)

- `src/ImageSharp`, the test project, and the benchmark project compile for net11.0. `src/ImageSharp` has zero
  analyzer errors and warnings for net10.0 and net11.0. The test and benchmark projects are not yet checked with analyzers.
- The uncommitted tree did not compile at takeover: missing `using` lines, missing `InlineArray` sizes, duplicate
  locals, one missing argument, one `uint` conversion, and 292 StyleCop errors. 68 test call sites were out of date.
- HEIF tests, current tree: 10,265 pass, 114 fail. The binary built 30 minutes after `ad7bc64ec` fails 94 of the same
  tests, and 92 failures are common. The common failures include encoder/decoder reconstruction mismatches
  (`CdefPreserves*`, `RestorationPreserves*`), index errors in `Av1TileWriter.EncodeTransformCoefficientRegion`, and
  mode-selection expectations. The 22 new failures come from the uncommitted edits.
- All 79 public `HeifEncoderTests` pass on both trees.
- Corrected: the asymmetric-partition stage measured source variance for blocks whose midpoints are outside the
  frame (131 failures). libaom requires `av1_blk_has_rows_and_cols` for that stage.
- Still-image timing, Bike, quality 75, one thread, cold run: 512x512 Level0 19.7 s and Level6 575 ms, against
  `aomenc --allintra` 5.0 s and 184 ms. The 19x to 29x figures in the benchmark README are the three-frame inter sequence.
- Level0 profile: `GetCoefficientCost` 24% self, `OptimizeCoefficients` 21% self, luma palette search 30% inclusive.
- Corrected: `Av1LevelBuffer` resolved `Memory.Span` and divided by the width for each neighbor read. It now follows
  `get_padded_idx` and `av1_txb_init_levels`. 512x512 Level0 is 14.8 s and Level6 is 501 ms. Output is unchanged.
- Corrected: palette map costing used the decoder's sorted color-order context for every sample. It now follows
  `av1_fast_palette_color_index_context` (`tokenize.c:30-170`). 512x512 Level6 is 419 ms. The 12 AVIF files that
  the tests save are byte-identical before and after.
- Known deviations in the luma transform-type loop (`GetUniformLumaCandidateCost`), not yet corrected. Each one
  changes output, so correct them one at a time against `av1_search_tx_type` (`tx_search.c:2050-2400`):
  1. Distortion always comes from an inverse transform and a pixel comparison. libaom uses transform-domain
     distortion at all-intra speed 1 and higher (`tx_domain_dist_level`, `tx_search.c:2162-2290`), with one final
     pixel-domain measurement for level 1.
  2. The rate is computed twice: `OptimizeCoefficients` and then `GetCoefficientCost`. `av1_optimize_b` returns the rate.
  3. Corrected for luma: a candidate is rejected on its rate before reconstruction
     (`RDCOST(rdmult, rate_cost, 0) > best_rd`). Output is byte-identical. The gain is small at quantizer 16
     (512x512 Level0 14.2 s to 13.9 s). The chroma and inter loops still reconstruct every candidate.
  4. An empty block measures prediction error again for each type. libaom reuses `block_sse`.
  5. Audit of 2026-09-18 against `search_tx_type` (`tx_search.c:2062-2400`) found more missing rules in the same
     loop. The block error, `block_mse_q8`, and the trellis decision are computed once for each transform block
     in libaom (`tx_search.c:2108-2154`); the port computes them again for each transform type inside
     `EncodeLossyCandidate` with a scalar loop. `adaptive_txb_search_level` (`tx_search.c:2360-2365`, all-intra
     level 1 at speed 0 and level 2 from speed 1) and `skip_tx_search` (`tx_search.c:2369`, all-intra from
     speed 1) are absent. `av1_optimize_txb` returns the final rate (`txb_rdopt.c:548-559`), so libaom makes no
     second cost pass after trellis. `Av1TransformBlockEncoder.GetTransformError` already equals
     `dist_block_tx_domain` (`tx_search.c:1115-1154`, `MAX_TX_SCALE` is 1).
  6. `ShouldOptimizeCoefficients` shifts the SATD by `2 - scale`; `skip_trellis_opt_based_on_satd`
     (`tx_search.c:1964-1994`) shifts by `MAX_TX_SCALE - scale`, which is 1, 0, or -1 (a left shift). Only the
     all-intra speeds 4 and above use the SATD gate. The block MSE gate uses the visible samples only and no
     rounding (`av1_pixel_diff_dist`, `tx_search.c:125-151`); the port uses the padded block with rounding.
- Corrected 2026-09-18: the luma transform-type loop now follows `search_tx_type` (block error and trellis gate
  once per block from the visible samples, transform-domain distortion by the `tx_domain_dist` policy of the
  evaluation stage, rate returned by the trellis pass, rate-only rejection with `>`, `adaptive_txb_search_level`
  and `skip_tx_search` exits, one reconstruction of the winner). New speed settings:
  `DefaultTransformDomainDistortion`, `ModeTransformDomainDistortion`, `WinnerTransformDomainDistortion`,
  `SkipTransformSearchAfterEmptyBlock`; `InterAdaptiveTransformSearchLevel` is 0 for good-quality speed 6+.
  HEIF suite unchanged (13 failures), `aomdec` verification passes. Sizes moved 0.2-0.4% toward `aomenc`.
  Bike 512 speed 0 23.0 s to 20.0 s, speed 3 5.7 s to 4.4 s, speed 6 566 ms to 519 ms (cold, in-test).
- Work-count comparison, 2026-09-18 (temporary counters in a libaom worktree
  `D:\GitHub\ynse01\aom-06f068d1-counters`, build `aom-06f068d1-build-x64-counters`, and `Av1WorkCounters` in the
  port, both to be removed): at speed 0 the port and libaom do the same work. Transform-type iterations 11.73M
  against 11.66M, forward transforms 12.74M against 12.65M, pixel-domain distortions 9.75M against 8.91M, chroma
  candidates 327K against 342K, palette candidates 132K against 133K, partition blocks 36.2K against 34.6K. The
  time gap (16.1 s against 5.3 s with tiered compilation off) is per-operation speed of the primitives. libaom
  per call: forward transform 35 ns, quantizer 10 ns, trellis 179 ns, inverse transform with SSE 69 ns, intra
  prediction 42 ns, coefficient cost 218 ns. Port (BenchmarkDotNet, 8x8 / 16x16): forward DCT 88 / 294 ns,
  quantizer 18 / 49 ns, inverse with SSE 173 / 456 ns, coefficient cost 454 / 1288 ns, trellis 560 / 1502 ns,
  DC prediction 87 / 131 ns. Speed 6 also has work excesses: 100K pixel-domain reconstructions against 0 (chroma
  candidates and the luma winner; libaom reconstructs only when a later transform block predicts from it,
  `recon_intra`), 2.3x coefficient-cost calls, 1.5x trellis calls.
- Causes seen in the JIT output of `OptimizeCoefficients` (7.5 KB of code): the inline budget is exhausted, so
  `Av1RateDistortion.GetCost`, `GetOptimizationCoefficientRate`, `GetScale`, `GetAdjusted`,
  `GetTransformSizeContext` and `get_CoefficientCosts` are calls; static array access goes through
  `CORINFO_HELP_GET_GCSTATIC_BASE`; locals spill to the stack. libaom precomputes `base_cost` with level
  differences and `lps_cost` with Golomb costs per context. The forward transform builds an
  `Av1Transform2dFlipConfiguration` per call (the unused forward stage ranges are now removed) and runs five
  memory passes (load, column, transpose, row, transpose, store); libaom keeps 8x8 and 16x16 in registers.
- Size and PSNR baseline, 2026-09-18, managed against `aomenc --allintra` on identical source planes
  (`rdcompare.py`, same quantizer index and speed, one thread). Bike 512x512 4:2:0 8-bit, quantizer 64:
  speed 0 30,407 bytes / 45.89 dB in 23.0 s against 30,443 / 45.85 in 5.0 s; speed 3 30,510 / 45.75 in 5.7 s
  against 30,510 / 45.67 in 1.2 s; speed 6 31,529 / 45.28 in 566 ms against 31,372 / 45.14 in 159 ms; speed 9
  41,899 / 44.37 in 53 ms against 36,442 / 44.13 in 22 ms. Calliphora 300x200 speed 0: 24,781 / 42.07 in 7.8 s
  against 24,725 / 42.03 in 2.0 s. Sizes agree within 1% up to speed 6; speed 9 is 15% larger. Time is 3.5x to
  4.7x. The managed times include JIT warm-up of the first case.
- Corrected 2026-09-18, each verified with the reference source and with `aomdec`:
  - Chroma edge availability did not apply `scale_chroma_bsize` or the `yd > 0` rule
    (`reconintra.c:1742,1775-1784`). Sub-8x8 chroma blocks predicted from the wrong edges, and the stream
    did not match the encoder reconstruction.
  - The non-RD intra path stored lossless 4x4 transforms in depth-first order. Intra transforms are coded in
    raster order inside each 64x64 unit (`encodemb.c:660-708`). Level9 lossless output was not lossless.
  - `Av1EncoderPartitionTree.Reset` compared the required length with the previous layout, not with the owner
    capacity, and allocated again after each clipped superblock.
  - Test fixtures used `disallow4x4AllFrames: true` with searches that produce 4xN blocks. Production derives
    that flag from `MinimumPartitionSize`; the fixtures now use 4x4 mode storage.
- Open, inter path (milestone 2): `ProductionTileSelectsNonRegularInterpolation`,
  `ProductionTileSelectsDualAxisInterpolationHighBitDepth`, `InterBlockCanSkipNonzeroQuantizedResiduals`,
  `IntraBlockCopyCanSkipNonzeroQuantizedResiduals`, `SequenceEncoderWritesSelectedGlobalTranslation`,
  `IndependentFrameAndSequenceModesRetainDifferentMotionPolicies`,
  `ResolutionAndFrameRoleOverrideInitialSecondCandidatePolicy`, and `PixelSearchFallsBackToExhaustiveMesh` fail.
  Each one asserts an inter decision. Check each against libaom before a change to the test or to production.
- `Buffer2D.DangerousGetSingleSpan()` uses LINQ `Single()` and allocates. The HEIF code calls it in 48 places.
  Hot paths must index `FastMemoryGroup[0]`, as `Av1TransformBlockEncoder.GetPlaneSpan` does.

## Reference and acceptance

- Local libaom: `D:\GitHub\AOMediaCodec\aom`, verified revision
  `06f068d1bc9f5159c6efb195ddf2f66e484ba511`.
- Local libavif: `D:\GitHub\AOMediaCodec\libavif`.
- Temporary optimized comparison build: `D:\GitHub\ynse01\aom-06f068d1-build-x64-release`.
  MSVC 19.51.36256.0, NASM 3.02, x86-64, runtime CPU detection, high-bit-depth support,
  SSE2/SSE4.1/AVX2/AVX512 enabled, WebM I/O and libyuv disabled.
  Verify current official main and build settings again before acceptance measurements.
- Production remains fully managed. Native sources, libraries, tools, integration, and generated comparison
  artifacts are temporary local material. Existing tracked native tooling is deleted at final cleanup.
- Separately encoded results must differ by at most one component unit per decoded sample under reconciled settings.
  Report maxima and counts above one, per plane/frame. PSNR and averages do not replace this requirement.
- Identical-bitstream decoder output must be byte-exact: maximum zero and no differing samples.
  Same-stream decoder agreement does not establish separate-encoder parity.
- End-to-end performance includes conversion, allocation, output writing, and disposal.
  Historical measurements and earlier test passes do not verify this tree.

## 1. Complete encoder decision policy — complete for still pictures

The items below are verified for still pictures by the 620 comparison cases. The unchecked items stay open
only for inter frames, where milestone 2 supplies the frame roles.

- [ ] Finish speed policy for the actual coding mode, frame role, resolution, quantizer, and content.
  The sole public control is `HeifEncodingSpeed`; do not reintroduce effort thresholds.
  Real-time inter dispatch now connects temporal source filtering, variance partitions, single-reference
  candidates, motion/filter search, residual estimates, intra and screen alternatives, and final encoding.
  Key frames now use their own hybrid intra selection, palette admission, and variance thresholds.
  Source owners rotate without frame copies; temporal SADs, activity, chroma sensitivity, and low-variance
  flags feed block decisions. All current edits are unverified.
  The production frame interface now passes a borrowed reference table and an availability mask.
  Real-time ALTREF single modes and LAST/ALTREF compound modes consume that table; sequence owners
  still supply LAST/GOLDEN until milestone 2 adds the remaining refresh roles.
  Real-time screen intra-block-copy now uses palette admission, hash lookup, the fixed twelve-position
  fallback, estimated luma/chroma costs, and selected-copy encoding. These edits are unverified.
  Evidence: `nonrd_pickmode.c:1587-1774,1935-1965,2455-2565,2669-2901`.
  Remaining: complete full-search reference/compound policy, cadence-dependent scene thresholds,
  and remaining frame-role policy.
  Frame scheduling and reference refresh are milestone 2 dependencies; do not silently restrict the policy.
  Evidence: `nonrd_pickmode.c:103-289,1775-1984,1985-2233,2330-2565,2670-3340`;
  `nonrd_opt.c:126-332,462-970`; `speed_features.c:1544-1850,1945-2247`;
  `var_based_part.c:149-347,425-973,1105-1218,1601-1967`;
  `ratectrl.c:3280-3440`; `encodeframe.c:1084-1207`; `mcomp.c:1975-2290`.
  Leaf partition merge comparison is connected through existing saved mode/transform contexts.
  Merge storage retains square candidates only; losing trials restore coefficient and partition edges.
  Direct merging is gated on disabled CDF updates; current production keeps CDF updates enabled.
  Reconcile that gate with milestone 2 frame policy before adding an inactive branch.
  Evidence: `partition_search.c:2540-2947,3092-3132`;
  `encodeframe_utils.c:671-690`. No build, diagnostics, tests, or benchmarks have run.
- [ ] Complete partition selection, breakout, pruning, and frame-edge geometry.
  Partition limits are connected. The forced 128x128 inter split has been removed.
  Fixed rate/distortion breakout, mode/size/quantizer-dependent rectangle pruning, split stopping position,
  active frame-edge exceptions, and invalid NONE/SPLIT termination are written but unverified.
  Evidence: `speed_features.c:352,444,477,540,923-925,3048-3067`;
  `partition_search.c:3514-3526,4267-4300,4430-4502,4586-4649,5861-5865`; `encodeframe_utils.c:767-821`.
  Trained NONE breakout is now connected with complete low/high-resolution models, speed thresholds,
  bit-depth scaling, feature normalization, Q9 output reduction, and cached source variance. Unverified.
  Evidence: `partition_strategy.c:1529-1645`; `partition_model_weights.h:2624-3437`;
  `partition_search.c:4270-4299,4483-4496`; `speed_features.c:738-822,896-905,1123,1188,1379`.
  Square-only limits now apply to partition eligibility, preserving required frame-edge rectangles.
  Empty-residual NONE pruning now distinguishes extended partitions from ordinary rectangles, with
  ordinary-frame, size, quantizer, and inherited-motion conditions. Unverified.
  Evidence: `partition_search.c:3985-3997,5708-5713,5867-5903`;
  `speed_features.c:175-182,211-241,315,790-821,909-918,1058-1063,2298`.
  The cost/variance rectangle classifier is connected where the after-SPLIT model is disabled.
  Recursive search publishes each child's unsplit cost rather than its winning partition cost;
  the five size models retain their output reduction and probability thresholds. Unverified.
  Evidence: `partition_search.c:4345-4361,4468,4576`; `partition_strategy.c:1129-1218`;
  `partition_model_weights.h:3440-3893`; `ml.c:143-158`.
  Extended-partition size limits now include content, quantizer, resolution, boosted-frame, and
  current-winner rules. They retain fallback evaluation when no valid partition exists. Unverified.
  Evidence: `speed_features.c:510-511,1019-1020,1436-1437,2957-2992`;
  `partition_search.c:4007-4029,4139-4158`.
  Superblock search now retries when no valid partition survives, propagating required-valid-partition
  state through child searches and removing the corresponding optional shape restrictions. Unverified.
  Evidence: `partition_search.c:3451-3478,5781-5793,5961-5970`.
  Quantizer-dependent rectangle bounds, minimum four-strip width, and asymmetric direction/cost
  pruning are connected. Rectangular child costs are retained alongside split-child unsplit costs;
  asymmetric masks are resolved once before the family search. Unverified.
  Evidence: `partition_strategy.c:1730-1760,1901-1990`; `partition_search.c:4160-4165`;
  `speed_features.c:537-543,1380-1382,1493-1496,2994-3005`.
  The four asymmetric partition classifiers are connected after preliminary direction/cost pruning.
  They consume the latest coding-block variance and retained subblock costs, preserve Q9 reduction,
  and replace the candidate mask with the union of selected classes. Source variance is now computed
  once in the common block-decision entry for intra and inter blocks. Unverified.
  Evidence: `partition_strategy.c:1221-1324,1993-2006`; `partition_model_weights.h:25-1319`;
  `partition_search.c:907-920`.
  Four-strip classification is connected for 16x16, 32x32, and 64x64 parents: normalized and
  unnormalized models, strip-variance features, resolution/speed thresholds, and winner-direction
  policy. Parent source variance is retained across the partition stages rather than rescanned.
  Unverified. Evidence: `partition_strategy.c:1325-1523`; `partition_model_weights.h:1318-2616`;
  `partition_search.c:4186-4208`; `speed_features.c:210,237,271,780,817,908,2319`.
  Child rectangle win flags now propagate through recursive search. Four-strip and asymmetric
  pruning consume those flags, child unsplit choices, and quantizer-dependent vote thresholds.
  Unverified. Evidence: `partition_search.c:3366-3367,3634-3645,4034-4057`;
  `partition_strategy.c:1870-1900,2007-2028`; `speed_features.c:446,476,1304,1378`.
  Sub-8x8 pruning now follows content, speed, quantizer, and neighbor rules. Recursive partition
  entry establishes the current block's neighbor geometry before pruning; required-valid retries
  bypass this optional restriction. Unverified. Evidence: `partition_strategy.c:1760-1779`;
  `speed_features.c:541-542,1438-1439,3091-3096`.
  Simple-motion storage now shares the worker allocation and is sized to the sequence superblock,
  including 4x4 leaves. Spatial starting vectors initialize each superblock; whole/quarter/half-block
  searches share cached features and propagate full-sample starts to children. Final fractional
  predictors use the regular filter before measuring model errors. Unverified.
  After-SPLIT termination, motion-based NONE termination, pre-search split selection, and directional
  rectangle pruning are connected with their trained models, normalization, thresholds, feature order,
  child geometry, and speed/quantizer/frame-role gates. Unverified.
  Evidence: `partition_strategy.c:345-815,976-1127,1791-1829,2635-2682`;
  `motion_search_facade.c:992-1105`; `partition_model_weights.h:3898-7049,7265-8118`;
  `partition_search.c:4305-4361`; `speed_features.c:1122-1127,1181-1187,1299-1306,1375-1378,2922-2954`.
  Motion-based maximum-partition prediction and four-strip direction pruning are connected.
  The maximum-size model uses full-sample 16x16 searches, direct/relaxed/variance-adaptive decisions,
  frame-role exclusions, and configured size bounds. Four-strip comparison includes partition-symbol
  cost and preserves both directions on ties. The translation-cost span is bounded before the appended
  simple-motion storage so block resets cannot overwrite cached partition features. Unverified.
  Evidence: `partition_strategy.c:816-974`; `partition_strategy.h:173-242`;
  `partition_search.c:4059-4128,4214-4221`; `speed_features.c:738-746,1018-1039,1189,1306`.
  Intra convolution partition pruning is connected: five valid convolution layers, cached branch outputs,
  two-hidden-layer classifiers, quantizer normalization, per-resolution thresholds, and screen-content rules.
  Four SIMD lanes compute independent output channels; input-channel/kernel accumulation order is retained.
  Normalized input and first-layer output borrow existing arithmetic scratch; the four branch outputs share
  the worker allocation and survive child mode trials. Unverified.
  Evidence: `partition_strategy.c:157-342,1777-1789`; `partition_cnn_weights.h`;
  `cnn.c:615-647,1080-1193`; `partition_search.c:3346-3351`; `speed_features.c:387-388,512-513,1178-1179`.
  All-intra 4x4 variance-range rules now force splitting across flat/detail boundaries and suppress
  rectangles at the configured speed. The same normalized variance calculation serves superblock
  rate weighting and partition pruning. Unverified. Evidence: `partition_search.c:5582-5622,5720-5735,5800-5836`;
  `speed_features.c:539`.
  Alternate-reference selection remains dependent on milestone 2 supplying that frame role.
  Full-block prediction, multiple transform roots, subsampled 64x64-region coefficient order, reconstruction,
  retained states, and partition replay are written but unverified.
  Evidence: `tx_search.c:3440-3539,3810-3905`; `encodemb.c:780-826`; `bitstream.c:1537-1564`.
- [ ] Finish transform-size/type and candidate/default/winner policy.
  Uniform intra depth search, retained intra winners/palette maps, coefficient-refinement stages, mixed inter
  trees, root-cost history, split models, and transform-type axis models are written.
  Frame-role transform probabilities, default distributions, nonempty selected-transform counts, per-frame
  updates, probability pruning, candidate forced-type thresholds, and inter winner eligibility are written.
  The existing sequence controller supplies key/last roles; the remaining role scheduling belongs to milestone 2.
  Counts come from final packing, not repeated partition trials. Both luma-only and all-plane prediction
  bounds are retained for their corresponding candidate gates. These changes are unverified.
  Estimated-transform-RD pruning is written: regular quantization, Laplacian coefficient cost,
  transform-domain error, stable ordering, full small-set probes, and separated-axis large-set probes.
  Intra and inter transform search consume the resulting candidate mask and order. Unverified.
  Complete remaining family/winner ordering and frame-role policy.
  Evidence: `encoder_utils.c:44-183`; `encodeframe.c:2523-2578`; `tx_search.c:1055-1372,1759-1934`;
  `txb_rdopt.c:689-744,785-822`; `rdopt_utils.h:464-538,607-699`.
  Evidence: `tx_search.c:1368-1762,2410-2722,2826-3140,3440-3899`;
  `tx_prune_model_weights.h`; `rdopt.c:3746-3896`; `rdopt_utils.h:607-699`.
- [ ] Finish intra/inter decision bounds, filtering, palette placement, and context publication together.
  Partition leaf bounds now reach intra, palette, single/compound inter, retained-transform, and displacement
  winner comparisons. Failed leaves return before publishing retained contexts; invalid modes do not enter
  intra winner refinement or skip-symbol accumulation. Unverified.
  Filter-intra pruning now uses the post-palette winner and requires a valid intra result.
  All-intra source/reconstruction variance weighting follows completed transform-grid selection; source 4x4
  log variances share worker storage, reset per superblock, and also serve partition and rate-weight decisions.
  Raw rate and distortion remain available for later block accumulation. Unverified.
  Evidence: `partition_search.c:3146-3169`; `rdopt.c:3306-3317,3518-3584`;
  `intra_mode_search.c:119-224,288,1188-1232,1640,1660-1743`.
  CfL now omits alpha estimation for a full-range search and rejects a fixed alpha pair before residual work
  when it is invalid or its syntax already exceeds the bound. Alpha signaling is included in the retained
  residual rate and preserved during chroma winner refinement. Unverified.
  Evidence: `intra_mode_search.c:643-858`; `intra_mode_search_utils.h:572-619`.
  Chroma transform traversal now receives the mode/angle bound, preserves per-transform rounded costs,
  and retains prediction error for the combined U/V rejection. Zero-angle failure stops that directional
  mode; even-angle trials retain their wider bounds. Chroma palette uses the same traversal limits.
  Corrected the missing red coefficient-rate local in the single-transform path. Unverified.
  Evidence: `intra_mode_search.c:495-587`; `tx_search.c:3119-3140,3702-3790`.
  Chroma directional HOG rejection now applies only where angle search is enabled. Luma's empty-interior
  histogram still reaches the model rather than bypassing its bias scores. Unverified.
  Evidence: `intra_mode_search.c:957-978`; `intra_mode_search_utils.h:149-185,406-459`.
  Inter runs first. Its cost now reaches intra syntax rejection and uniform transform search.
  Inter-frame filter-intra follows DC; intra pictures retain the later filter stage.
  Intra reference-selection rate is included before candidate comparison.
  Skip mode now follows family selection/refinement. It evaluates prediction error directly without transform search,
  uses the fixed default interpolation filter, and compares both skip-mode syntax costs at that stage.
  Lossless acceptance retains unrounded sample equality. Evidence: `rdopt.c:1823-1865,3605-3714`.
  The selected inter luma cost and chroma residual-only rejection are connected. Intra winner refinement now
  follows family selection in inter frames and refines the chosen UV coefficients without repeating UV mode/alpha search.
  Inter-frame palette follows winner refinement, preserves cached UV choices, and restores retained syntax/transform states
  using shared scratch storage. These edits are unverified.
  Single- and multi-transform luma now share the predictor-level controller. Each predictor completes transform
  selection before empty-residual ranking, DC/filter grouping, and per-mode rejection. Replaced duplicate paths
  are removed.
  Retained inter candidates now use tile-scoped learned models or curve-fit estimates, stable cost ordering,
  prediction-error gating, per-mode search counts, and first-winner limits before full residual search.
  Models reset at tile start and fit at single-tile superblock boundaries; worker storage retains syntax instead of candidate pixels.
  Prepared residual subtraction now uses complete coding-block dimensions. These edits remain unverified.
  Inter-intra now ranks smooth predictors before fixed-transform estimation, selects wedge masks with unrounded
  weighted error, applies mode reuse and variance gates, and refines NEWMV against the fixed intra predictor/mask.
  Full/fractional motion traversal now accepts that fixed blend; compound searches do not use single-reference mesh fallback.
  The selected blend alone reaches the complete residual search. These changes are unverified, including signed
  high-bit-depth normalization and scratch lifetimes. The new compound error loops still need SIMD work and measurement.
  Evidence: `compound_type.c:262-299,393-429,461-518,520-743,782-874`;
  `motion_search_facade.c:759-878`; `mcomp.c:676-733,1827-1834`; `wedge_utils.c:19-66`.
  Compound-motion arithmetic now handles equal-weight predictors as well as masks. Average/distance-weighted NEWMV
  trials now refine their searched reference components, with alternating joint search, repeated-center termination,
  three-step eight-neighbor refinement, fractional search, and per-reference mask inversion. Changes remain unverified.
  Both compound mode families now select one blend with luma estimates before full residual search.
  The shared controller includes mask models, transform estimates, motion refinement, average-candidate pruning,
  repeated predictor-pair records, and mask/type reuse. The 64-entry record cache shares the worker allocation.
  Single-reference modes now visit the available references in mode order, retaining one vector stack per reference.
  Compound modes share one ordered loop after the single-reference modes; static and searched vectors no longer
  use separate loops. NEW-NEW precedes mixed NEW modes, with each mode's DRL entries kept together.
  The shared interpolation controller now serves single and compound modes. It retains per-block decisions,
  frame-context probabilities, reference-slot usage, modeled single-reference costs, and configured compound
  rejection before/after filter search. Switchable interpolation no longer excludes compound candidates.
  Search uses visible-plane models, luma-only policy, bounded reuse, neighbor prediction, small-block ordering,
  sharp-filter policy, and clamped motion phases. Selected prediction copies cover only active plane extents.
  Mixed compound modes now use neighbor/reference-winner gates, configured near-mode omission, and repeated
  DRL-vector rejection. Single-result ranking now merges translation and filter-model order, applies the
  configured candidate limits, and preserves nearest/near candidates whose compound vector stacks differ.
  Configured single-candidate interpolation omission and two-filter winner search are connected.
  Winner search uses the full-block linear error model, updates filter syntax costs with the chosen residual,
  and lets chroma reconstruction follow the chosen luma tree.
  Filter trials reuse unchanged luma/chroma estimates and prediction views independently.
  Duplicate-vector syntax rejection and near-vector translation preselection are connected for single
  and compound modes. Their DRL syntax bounds precede motion/residual work.
  Single-reference candidates now include the single/compound choice symbol that final packing already emits.
  Compound search excludes block dimensions below eight samples, as required by the syntax.
  Adaptive mode thresholds are connected: quantizer/size scaling, per-reference mode multipliers, Q5 history,
  superblock-row reset, single/compound rejection, and updates after the final block winner. History uses the
  existing worker allocation. Empty-residual intra rejection and maximum intra block size are also connected
  for the current controller without temporal lookahead. These changes remain unverified.
  Temporal lookahead features for the intra classifier depend on milestone 2's frame controller; they are not implemented.
  Evidence: `rd.c:528-544,578-596,1257-1497`; `rd.h:49-55,330-336`;
  `encodeframe.c:1249`; `rdopt.c:4419-4451,5254-5262,5884-5948,6464-6468`.
  Remaining: mode-pruning/motion-mode controls and frame-role reconciliation.
  Evidence: `rdopt.c:1107-1159,2078-2309,4709-4940,5951-5962`;
  `rdopt.c:2755-2857,3780-3825,6247-6258`; `model_rd.h:130-160`; `common/blockd.h:65-67`.
  Evidence: `interp_search.c:20-120,153-260,275-640,674-818`;
  `encoder_utils.c:1087-1128`; `encodeframe.c:2672-2731`; `bitstream.c:628-649`;
  `rdopt.c:2634-2672,3140-3212,4943-5036,5240-5310,6280-6303`;
  `speed_features.c:853,891,1066-1069,1131,1206,1260,1390,1483,1637,1680`.
  Evidence: `compound_type.c:30-113,209-390,738-781,876-978,1082-1764`;
  `rdopt.c:2582-2614`; `rdopt_utils.h:408-447`; `speed_features.c:779-968,1140-1447`.
  Evidence: `mcomp.c:676-733,1696-1781`; `mcomp_structs.h:32-35`; `motion_search_facade.c:546-755`.
  Frame-role policy, motion-mode winner handling, and the remaining mode-order/pruning controls still require completion.
  Evidence: `rdopt.c:353-490,1660-1751,5362-5558,6101-6106`; `rdopt_utils.h:298-304,408-447`;
  `encodeframe.c:1043-1048,1540`; `speed_features.c:856-858,892,936-939,970,1129,1265,1327,1993-1996,2072,2116`.
  Do not substitute exhaustive mode evaluation for these decisions.
  Evidence: `intra_mode_search.c:1301-1395,1688-1742`; `rdopt.c:5680-5817,6367-6410`.
- [ ] Finish source reconciliation of quantizer selection, rate-distortion scaling, entropy-cost refresh,
  skip decisions, and early rejection across every mode family.
  The shared rate multiplier now accepts the frame update role, including golden/alternate weighting.
  Block decisions and global-motion selection now include the luma DC delta, matching the existing filter callers.
  Production callers use `GetRateMultiplier`; old test callers must be updated in milestone 3. Unverified.
  Evidence: `rd.c:370-444,802-809`.
- [ ] Complete intra-block-copy integration.
  Written: 4x4–128x128 square CRC32C hash levels, bucket insertion/capping order, configured maximum hash size,
  shared full-pixel search, actual coding-block dimensions, and shared residual/reconstruction/replay.
  Fast search selects 4x4/8x8/16x16; full search no longer has the invented 8x8 restriction.
  The obsolete lossless rejection is removed; reversible residuals use the 4x4 grid.
  Mixed-tree publication preserves leaf contexts. Coefficient presence is retained independently of rounded distortion.
  Block-copy pixel search now consumes the frame-selected search step instead of deriving a separate
  frame-size-only value. Displacement candidates use the full transform search bound and are compared
  with the retained intra winner afterward. Unverified. Evidence: `rdopt.c:3440-3499`.
  Remaining: finish controller integration and verify all geometries and allocation lifetimes in milestone 3.
  Evidence: `hash_motion.c`; `mcomp.c:1839,1908-1978`; `rdopt.c:3306-3509`;
  `encodeframe.c:2263-2284`; `common/blockd.h:372-374`.
- [ ] Finish boundary/controller reconciliation for HOG, model-cost, angle, filter-intra, chroma, and palette.
  Fast-intra palette SAD now uses 4x4-unit normalization; prediction-unit estimation publishes the latest
  unit's skip decision while accumulating rate and distortion. Unverified.
  Evidence: `nonrd_pickmode.c:1775-1919`; `nonrd_opt.c:126-332,605-668`; `nonrd_opt.h:114-117`.
  Fast-intra palette search now uses block-size-group DC cost and compares the completed skip-adjusted
  palette rate against the retained estimate. A rejected palette restores the original transform size
  and predictor before reconstruction. Unverified. Evidence: `intra_mode_search.c:1122-1180`;
  `nonrd_pickmode.c:1910-1931`.
  Chroma variance/HOG precision and area scaling, angle/CfL order, and intra neighbor-count clamping are written.
  Their presence does not establish complete controller parity.
  Evidence: `encodeframe.c:188-213`; `aom_dsp/variance.c:308-354`;
  `intra_mode_search_utils.h:406-434`; `reconintra.c:1753-1754,1817-1820`.

Managed owners are `Av1IntraSuperblockEncoder.*`, `Av1EncoderSpeedSettings`,
`Av1TransformBlockEncoder`, `Av1IntraBlockCopySearchIndex`, `Av1MotionSearchBase.*`,
`Av1EncoderBlockWorkspace`, `Av1EncoderPictureBuffer`, and `Av1TileWriter`.
Proceed directly to milestone 2 when this implementation is complete.

## 2. Complete frame and sequence control — active

The sequence harness encodes the same frames with the port's sequence encoder and with `aomenc`
(`--usage=1 --passes=1 --end-usage=cbr --min-q=Q --max-q=Q --threads=1`) and compares the streams frame by frame.
Current state: 8-bit 4:2:0 sequences are byte-identical to `aomenc` on every frame for speeds 7, 8 and 9:
512x384 bike (3 and 12 frames at q64 and q128; 30 frames static, slow pan and fast pan), 333x251 splash and
320x180 ducky (20 frames, q64), 1280x720 and 1920x1080 calliphora (q128, 4x2 pan), 10-bit 4:2:0 and 8-bit 4:4:4
(12 frames), synthetic screen content, a scene cut, and a 100-frame static sequence past the 80-frame golden
interval. The still-picture regression stays at 620/620.

- [x] Real-time one-layer reference structure (`av1_set_rtc_reference_structure_one_layer`): slot selection,
  refresh, GOLDEN interval, ALTREF lag, `get_ref_frame_flags` duplicate removal, primary reference and per-slot
  CDF inheritance, saved and projected temporal motion fields.
- [x] Real-time inter tools: motion-mode signaling and warped-probability pruning, global motion off, winner
  prediction rebuild, `model_skip_for_sb_y_large`, intra skip in inter frames, CBR projection NEWMV search
  (`av1_int_pro_motion_estimation`), `try_merge`/`calc_do_split_flag` with residual-spread pruning, preceding-frame
  merge rdmult, and no partition CDF or context updates during non-RD analysis.
- [x] Longer sequences: GOLDEN refresh (`av1_adjust_gf_refresh_qp_one_pass_rt`, frames_since_golden), temporal
  MV projection, the warped cut-off, compound pruning by single-reference variance, `newmv_diff_bias`, the CDEF
  skip from block color sensitivity, compound global filter syntax, full-sample rounding of search starts and
  neighbor vectors, and the running chroma mode of the intra estimate.
- [x] Other sizes: odd frame edges (skipped inter blocks code the largest transform), 64x64 superblocks for
  real-time up to 720p, ALTREF as the variance-partition reference, the regular/smooth filter search, a
  switchable frame filter at the start of every frame, and two memory faults found on the way (the 2-D
  interpolation scratch size and intra-estimate edges that aliased live inter predictions).
- [x] High bit depth and 4:4:4 sequences, screen content, scene cuts, 1080p (128x128 superblocks), and the
  periodic golden refresh, with copy_frame_prob_info() at every golden refresh under extra_prune_warped.
- [ ] Port the remaining real-time frame control: frames_to_key, `direct_partition_merging` where it applies, and
  `enable_ref_short_signaling` below 360p. Handle `context_update_tile_id` for multi-tile frames.
- [ ] GOOD usage (speeds 0-6) sequences. libavif codes a sequence with alpha without lag and a sequence without
  alpha with libaom's default lag (ARF groups). Start with lag 0 (`aomenc --usage=0 --end-usage=q --cq-level=Q
  --min-q=Q --max-q=Q --lag-in-frames=0`).
  - [x] Key frame: byte-identical at speeds 0-6 (splash 333x251, q64). This needed the sequence flag
    `enable_interintra_compound`, GOOD `tx_domain_dist_thres_level` and `adaptive_txb_search_level`, the
    block-origin skip context of `predict_dc_only_block`, DC-only blocks (`av1_xform_dc_only`), the visible-MSE
    trellis gate, and `do_border_pad` (true-frame-size visible dimensions and `fill_residue_outside_frame` in the
    type search, the intra model cost, the CFL alpha estimate and the final encode).
  - [ ] Inter frames: the GOOD reference structure (all references on slot 0 after a key frame, no primary
    reference), the reference mode, skip mode, transform mode and `fix_interp_filter` decisions, then the `rdopt`
    inter mode search. The current inter search is not a transcription: it crashed on a compound transform-size
    order bug, and GOOD streams with its affine global motion fail in aomdec (s0/s2/s5, frame 4).
  - [ ] The border padding of the inter paths (`rdopt` model cost and subtraction, the inter transform tree,
    skip mode distortion), and the predicted-skip context of the inter and chroma transform searches.
  - [ ] Lagged ARF groups, and the warped/OBMC search. Remove forced error resilience and disabled frame-end CDF
    propagation where they differ from the selected configuration.
- [ ] Replace the central-window translation search with the required global-motion estimator and selection policy.
  Use current eligibility, corner, stride, frame geometry, and storage rules.
- [ ] Reconcile quantization/rate control, super-resolution, and film-grain configuration/signaling.
  Do not count tools disabled by matched settings as missing mandatory work or invent unsupported restrictions.
- [ ] Reconcile deblocking, CDEF, and restoration selection, including high bit depth, frame edges, large groups,
  and sequence reuse. All three filters are connected; do not schedule them again as wholly absent.

## 3. Establish correctness

- [ ] Update existing test callers affected by required search, picture-buffer, and partition-policy arguments.
  The full block-workspace constructor now also requires the sequence superblock size for motion-tree storage.
  Block-copy candidate search now requires the frame-selected motion-search step.
  Do not weaken expectations or add test-only production overloads.
- [ ] Use Roslynk diagnostics, then Release/.NET 11 Visual Studio VSTest.
  Inspect the established launch configuration; avoid duplicate Path/PATH keys and popup-producing launch paths.
  Do not run .NET 10 and .NET 11 together, force one test thread, or stop the requested suite on its first failure.
- [ ] Resolve prior failures and contradictory verification records against the final tree.
  Earlier frame/public results included a global-motion assertion and interpolation-selection failures.
- [ ] Establish independent separate-encoder parity with identical sources and explicitly reconciled settings.
  Done for still pictures: byte-identical streams in 620 cases (see "Verified state, 2026-09-25").
  Remaining: inter frames and `tune=iq` settings.
- [ ] Separately establish byte-exact same-stream decoding.
- [ ] Cover lossless and lossy 8/10/12-bit samples, monochrome and all chroma formats, alpha, meaningful photographic
  and screen-content inputs, odd/coded edges, tiles, grids, and dependent animation.
  Done for still pictures: lossless and lossy 8/10/12-bit, monochrome, 4:2:0, 4:2:2, 4:4:4, photographic and
  screen content, odd sizes. Remaining: alpha, tiles, grids, and dependent animation.
- [ ] Verify public option precedence, pixel conversion, metadata/CICP/ICC, timing/repetition, root-frame behavior,
  cancellation, prefixed/non-seekable output, and allocator ownership.
- [ ] Follow established encoder `VerifyEncoder` patterns with independent decoding against the input.
  Image tests use `DebugSave` and `CompareToReferenceOutput`, short names, meaningful images, and conventional folders.
  Preserve precision and alpha. Do not conceal failures with self-decoding, quantization, or relaxed assertions.

## 4. Close performance and memory gaps

- [ ] Measure equivalent complete encode/decode boundaries with the optimized temporary native tools.
  Keep raw AV1 and public AVIF measurements distinct.
- [ ] Report absolute timings, sizes, maximum errors, counts above the limit, and allocations.
  Record revision, runtime, CPU, settings, and limitations; use KiB/MiB for large memory sizes.
- [ ] Fix demonstrated controller, SIMD, allocation, copy, scratch-size, alignment, and lifetime deviations.
  Preserve ImageSharp closed-generic operators and ownership. Do not add isolated search experiments.

## 5. Final verification and cleanup

- [ ] Complete affected decoder regressions, including unresolved tile-list/external-reference integration
  and allocation/dispatch coverage.
- [ ] Run the complete relevant suite after final edits, including scalar/SIMD and constrained allocators.
- [ ] Delete tracked temporary native integration at final cleanup:
  `tests/ImageSharp.Benchmarks/Codecs/Heif/Native/aom_benchmark.c`, `Native/CMakeLists.txt`,
  obsolete adapters, generated artifacts, stray reports/READMEs, redundant tests, and component benchmarks.
  Remove `Av1SequenceEncoderBenchmarks`; retain full encode/decode measurements.
  Preserve useful native tooling outside the repository. Review exact deletion targets first.
- [ ] Review the entire diff against `upstream/main` for unrelated codecs/shared APIs and accidental files.
  Preserve authorized shared fixes. Do not inspect/implement HEVC or edit `.gitattributes`.
- [ ] Commit verified feature checkpoints after inspecting staged contents. Exclude temporary native material.
  Commit final deletions normally; do not rewrite history. Push only with an accurate verification account.
