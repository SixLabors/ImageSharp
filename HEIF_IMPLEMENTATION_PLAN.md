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
| GOOD sequences with lag 0 (the alpha case), speeds 0-6, global motion on, psnr and ssim | identical with a fixed quantizer |
| GOOD `AOM_Q` with the quantizer range 0-63, lag 0 | speeds 0-6 identical (6 frames), speeds 2, 3, 6 identical (40 frames) |
| GOOD lag 35, TPL off, temporal filter off (stage A), speed 6, splash 333x251, 40 frames, ssim | identical (`134a45416`) |
| GOOD lag 35, TPL off, temporal filter on (stage B), speed 6, same case | identical (`6b005d986`) |

Encoder defaults follow avifenc: quality 60, speed 6, full range, BT.601 matrix, and the format of `avifReadImage()`.
Known difference: ImageSharp's JPEG decoder reports an unusual sampling layout as `YCbCrRatio420`, where libavif
falls back to 4:4:4.

Lag-35 comparison stages, with the harness `AV1_SEQ_RATE=lag AV1_SEQ_STAGE=a|b|(empty)` and aomenc
`--lag-in-frames=35`: A adds `--enable-tpl-model=0 --arnr-maxframes=0 --enable-keyframe-filtering=0`, B adds
`--enable-tpl-model=0`, C adds nothing (the libavif default).

## Open items, in order

1. Stage C: TPL in the lag-35 path. The model (`Av1TplModel`, `3b6601d7a`) and its consumers (`4f42d3278`) are
   ported, but `EncodeWithLookahead` never calls `SetupStatistics`; it sets `TplFrame = null` and
   `TplStatisticsReady = false` for every frame and passes `tplValid: false` to `ChooseBaseQIndex`.
   - Call the model at each group start for KF and ARF/GF update types, after the temporal filter, as
     `av1_encode_strategy()` does (`av1_tpl_preload_rc_estimate()`, then `av1_tpl_setup_stats(cpi, 0, ...)`).
   - Build `Av1TplSetupInput` from the driver: the group, an `IAv1TplReferenceMapper` over
     `Av1GoodQualityReferenceStructure`, look-ahead and filtered sources, slot reconstructions, slot display orders
     and pyramid levels, the frame context MV distributions, and the stale 16x16 mode-information grid (the last
     trial or final block written at each position, plus the model's own writes).
   - Feed the results to `ChooseBaseQIndex`, the parent `TplFrame`, `TplStatisticsReady` and the golden boost.
   - Observed in the libaom TPL dump (`D:\tmp\tpl-work\dump-lagc`): with TPL on, the first group is 49 entries
     with its ARF at display 32 (with TPL off it is at 16), and the second model call is at frame 33 with an
     8-entry group. Find which part of the GF length decision depends on `enable_tpl_model`.
   - The TPL-based GF length check (`is_shorter_gf_interval_better()`, with trial filtering) runs only when the
     interval exceeds 16 and `gop_length_decision_method != 3`, which excludes speed 6.
2. Route `HeifEncoderCore` color sequences with `LagInFrames > 0` to `EncodeWithLookahead`, with the real frame
   durations, sample ends and sync flags. Today every sequence takes the lag-0 path, so no user reaches the lag-35
   code.
3. Verify the lag-35 path beyond the one verified case: speeds 0-5 in stages A, B and C; a second clip; 10-bit.
   The high-bit-depth driver does not run the temporal filter yet.
4. `pred_sse` on frames with `cur_frame_force_integer_mv` (screen content): libaom skips the fractional search, so
   `x->pred_sse[ref]` keeps the value of the interpolation or model search. The port always keeps the squared
   error of the search result (`ReferenceModeDecision`, the `bestSingleReferenceSses` update).
5. RT frame control:
   - `enable_ref_short_signaling` below 360p: the writer never sets `frame_refs_short_signaling`.
   - `context_update_tile_id` for multi-tile frames: the encoder never sets it.
   - `partition_direct_merging` (RT speed 8 and up) is not ported. libaom runs it only when the tile does not
     update CDFs; confirm whether any libavif RT configuration reaches it before porting.

## SIMD operator-pattern gaps

Each of these breaks the SIMD-first rule and is a defect. Each gets an operator with scalar, Vector128, Vector256
and Vector512 overloads, one traversal, and a `FeatureTestRunner` test against the scalar libaom definition.
Audited 2026-09-29.

- Scalar: the mask inversion of `RefineCompoundVectors`; `Av1WedgeMask.Fill`;
  `Av1InterIntraMaskBuilder.FillInterIntraMask`.
- Scalar: the energy grid and axis sums of `PruneInterTransformTypes` (`TransformTypeModel`).
- `GetHadamardCost` (scalar 4x4 and quadrant combine); `TransformForModeEstimation` (scalar 4x4, 16x16 combine and
  high-bit-depth path, Vector128-only widening); `HadamardEstimationColumns` (Vector128 only).
- Scalar: `GetDirectionalModeSkipMask`; palette `CalculateCentroids` (1D and 2D). The palette k-means `Accumulate`
  members sum their vector distances with a scalar loop. `CopyPaletteSamples` is Vector128-only for width 8.
- Vector128 only: `FilterTemporalSource`, `ConvolveIntraPartition`. Scalar: the sums and column fills of
  `FillResidueOutsideFrame`.
- Loop restoration: the self-guided statistics and `GetProjectionError` (scalar), the Wiener mean and variance
  pruning (scalar), `Correlate` and the shared self-guided filter (Vector256/Vector128 copies, no Vector512, no
  operator).
- CDEF: `CopyPlane` and `FilterBlock` (no Vector512), `FindDirection` (Vector128 only).
- Scalar: the screen content `Detect` moments, copy, `CountColorsWithThreshold` and `DilateBlock`; the block-copy
  hash; `Av1FlowField.Upscale`.
- `Av1ChromaFromLumaPredictor.Operator.cs` calls `AdvSimd` directly (an unreachable branch).
- Lag-35 path: the first-pass wavelet energy (`GetWaveletEnergy`, `GetHaarAcSad`) is Vector128 only; the TPL
  `EstimateRate` is scalar. The temporal filter has every width.

## Tests

- Add parity tests for the lag-35 path, in proportion to the other formats. HEIF tests stay in proportion to the
  other formats (whole suite about one minute). Current HEIF run: 323/323 pass.
- Add an encoder cancellation test. Alpha, tiles, grids, sequences with timing, metadata/CICP/ICC, non-seekable
  output, option defaults and allocator ownership already have tests (`HeifEncoderTests` and the Av1 tests).

## Performance

- Measure complete encode and decode against the optimized native build; report time, size and allocations.
- Known causes, measured 2026-09-18 and not re-measured since: primitives are 2-5x slower per call than libaom (forward transform, quantizer, trellis, coefficient
  cost), and speed 6 reconstructs chroma candidates and the luma winner where libaom does not.
- `Buffer2D.DangerousGetSingleSpan()` allocates through LINQ `Single()` (still true). Hot paths index
  `FastMemoryGroup[0]`.

## Known limits

- The pinned `aomenc.exe` cannot encode 12-bit 4:4:4, so that format has no reference stream.

## Reference material

- libaom: `D:\GitHub\AOMediaCodec\aom` at the revision above. libavif: `D:\GitHub\AOMediaCodec\libavif`.
- Comparison build: `D:\GitHub\ynse01\aom-06f068d1-build-x64-release` (MSVC 19.51, NASM 3.02, x86-64, runtime CPU
  detection, high bit depth). Where libaom's C and SIMD code disagree, the port follows the x64 SIMD result.
- Production stays fully managed. Native tools, harnesses and comparison artifacts stay outside the repository.

## Final cleanup

- Remove the `DiagnosticSymbolTrace` code from the source (31 references) and the RECON trace
  (`TraceWindowMatches`).
- Delete `tests/ImageSharp.Benchmarks/Codecs/Heif/Native/aom_benchmark.c`, `Native/CMakeLists.txt`,
  `Av1SequenceEncoderBenchmarks`, `Av1ReferenceDecoderVerificationTests` and other temporary native tooling. Review
  exact targets first.
- Review the diff against `upstream/main`. Changes outside `src/ImageSharp/Formats/Heif` go to their own branch
  from `upstream/main` as a separate pull request, raised with the owner first.
