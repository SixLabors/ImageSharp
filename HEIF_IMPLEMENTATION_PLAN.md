# AV1/AVIF remaining implementation plan

Assessment updated during milestone 1 on 2026-09-16.
This is the remaining-work list, not a record of earlier experiments or passing component tests.
Only one production milestone is active at a time; complete its implementation and verification before advancing.

## Current completion state

The encoder has a working production pipeline, but is not a complete libaom port and has not met acceptance.
Partition search, intra prediction, palette, filter-intra, chroma-from-luma, intra-block copy, inter prediction,
quantization, coefficient refinement, deblocking, CDEF, restoration, entropy coding, and AVIF writing are present.
Inter candidates now precede intra. LAST and a retained GOLDEN picture, compound candidates, inter-intra prediction,
and inter transform refinement exist. These must not be scheduled again as wholly missing features.

The main implementation gap is the controller joining those tools. The current implementation removes
the independent `Effort` option and its partition, transform, interpolation, and global-motion thresholds. It connects
the speed/resolution partition limits, but the complete native controller is still unfinished. Focused runtime verification
of the option transition and corrected chroma paths passes. Sequence reference and entropy handling remain restricted. Separate-encoder sample parity
and end-to-end performance acceptance are still unestablished.
No completion percentage can be justified from the number of implemented primitives or passing tests.

Source evidence for the remaining work (paths below are relative to `src/ImageSharp/Formats/Heif/`):

| Finding | Current source |
| --- | --- |
| `Speed` is now the sole public search control; option validation and affected focused encoding checks pass. | `HeifEncoder.cs`; `Av1/Pipeline/Av1FrameEncoder.cs` |
| Partition search now uses speed/resolution limits instead of effort thresholds. Inter 128x128 leaves are still forced to split. Native breakout and pruning remain incomplete. | `Av1/Pipeline/Av1IntraSuperblockEncoder.ModeDecision.cs:276-343,726-797`; `Av1/Tiling/Av1EncoderSpeedSettings.cs:19-44` |
| Inter runs before intra, but intra is then evaluated without using the inter result to bound its search. | `Av1/Pipeline/Av1IntraSuperblockEncoder.ModeDecision.cs:1043-1229` |
| Single-transform luma searches all transform sizes only for an 8x8 starting transform; deferred refinement retains one preliminary mode. | `Av1/Pipeline/Av1IntraSuperblockEncoder.ModeDecision.cs:2267-2279,2622-2778` |
| Intra-block-copy selection remains limited to 8x8 blocks. | `Av1/Pipeline/Av1IntraSuperblockEncoder.ModeDecision.cs:1166-1167` |
| Frames force error resilience and disable frame-end CDF propagation; temporal reference motion vectors and super-resolution are disabled. | `Av1/Pipeline/Av1FrameEncoder.cs:386-438,527-569` |
| Inter selection searches LAST and GOLDEN. The sequence retains the key picture as GOLDEN and refreshes LAST, rather than implementing the complete reference-update policy. | `Av1/Pipeline/Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:445-573`; `Av1/Pipeline/Av1FrameEncoder.cs:1911-1977` |
| Deblocking, CDEF, and restoration are connected between analysis and packing. Their absence is no longer a valid plan item. | `Av1/Pipeline/Av1TileEncoder.cs:276-363` |
| Candidate quantization selects fast quantization plus refinement or regular quantization. Its complete stage policy still requires reconciliation. | `Av1/Pipeline/Av1TransformBlockEncoder.cs:1394-1479` |
| Screen-content tests compare ImageSharp and Magick decoding the same managed payload. They do not compare separately encoded images. | `tests/ImageSharp.Tests/Formats/Heif/HeifEncoderTests.cs:432-456` (repository-relative) |

The required local libaom checkout is `D:\GitHub\AOMediaCodec\aom`, at verified HEAD
`06f068d1bc9f5159c6efb195ddf2f66e484ba511`, with origin `https://aomedia.googlesource.com/aom`.
Source reconciliation against this checkout is complete for the earlier assessment. Git comparison from `8e7b6a56`
confirms no changes to `speed_features.c`, `intra_mode_search.c`, `intra_mode_search_utils.h`, `nonrd_pickmode.c`,
`rdopt.c`, `rdopt_utils.h`, `tx_search.c`, `encodemb.c`, `palette.c`, `pickrst.c`, `rd.c`, or `encodeframe_utils.c`.
The partition-search change removes an obsolete VMAF tuning branch; it does not change the assessed PSNR search policy.
The controller gaps above therefore remain. Current native evidence: `speed_features.c:504,524,561,571,2296`
selects winner/partition policy; `intra_mode_search.c:1688-1742` retains and refines multiple winners;
`rdopt.c:6408-6410` bounds palette search by the best established cost; `tx_search.c:2084-2229` controls candidate quantization;
`encode_strategy.c:47-104` selects reference refreshes by frame role.

The revision changes relevant to remaining implementation are intra boundary clamping (`av1/common/reconintra.c:1753-1754,1817-1820`),
global-motion geometry/stride/corner eligibility (`av1/encoder/global_motion_facade.c:314-322`, `aom_dsp/flow_estimation/`),
and frame-size-dependent state/storage (`encoder.c`, `allintra_vis.c`, `lookahead.c`) plus wider complexity-AQ arithmetic
(`aq_complexity.c:144-148`). These belong to the boundary, reference-control, and quantization tasks below.
The CDEF change selects module-specific worker counts; it does not alter the sequential search arithmetic.
VMAF tuning removal and native worker scheduling do not require new managed features under the existing PSNR/sequential contract.
Before accepting new comparisons, verify the official main revision and the optimized binary's source/build configuration.
Do not reuse older build settings or measurements as evidence for this checkout.

## 1. Complete encoder decision policy — active

- [ ] Replace the independent effort-driven search restrictions with the native-valued `HeifEncodingSpeed` policy.
  Resolve settings once for the actual coding mode, frame role, resolution, quantizer, and content classification.
  Complete the public option transition and its callers; do not retain two conflicting control systems.
  Implemented: sole speed option, partition bounds, DRL cache indexing, inter partition replay, global-motion
  syntax range, depth-first inter-transform coding, and chroma transform inheritance after luma refinement.
  The latest completed frame/public run passed 181/183. The global-motion vector and four-transform selection
  assertions remain unchanged and failing; both require controller corrections. The four-transform diagnostic
  shows four 16x4 blocks using 16x4 transforms. No decoder entropy, indexing, or buffer exception remains in that run.
  Still required: real-time sequence policy at speeds 7-9, remaining controller settings, and independent verification.
  Frame tests whose names say "native planes" inspect managed syntax; they do not independently compare native planes.
- [ ] Complete partition selection and breakout, block-size eligibility, and transform-size/type search, including
  the forced 128x128 inter split and 8x8-only intra-block-copy boundary. Preserve valid chroma and frame-edge geometry.
  Multi-transform inter chroma is now connected through search, refinement, retained states, reconstruction, and partition replay.
  Full-plane predictions feed strided transform regions without another pixel buffer. Each chroma transform inherits
  the luma type at its own origin and updates the following coefficient contexts. Difference-weighted chroma reuses
  the luma mask, including 4:4:4. Evidence: `encodemb.c:780-826`, `common/blockd.h:1283-1315`,
  and `common/reconinter.c:522-531`; managed `ReferenceModeDecision.cs` and `Operator.cs`.
  The retained-frame regression passes at 16x16 4:2:0 and 64x64 4:2:2/4:4:4; final combined verification passes 181/183,
  with only the two controller assertions listed above failing. Release/.NET 11 builds and Roslynk reports no errors.
  Independent nonzero chroma-residual and compound comparisons remain required before parity is established.
  Resolve the boundary difference in `Av1IntraSuperblockEncoder.ModeDecision.cs:3398-3428`: managed neighbor counts
  use upper bounds only, while current `reconintra.c` clamps both bounds. Establish the caller's reachable counts and
  preserve the required zero-neighbor behavior; do not add speculative guards or claim a reproduced failure from this source difference alone.
- [ ] Reconcile the connected HOG, model-cost, angle, filter-intra, chroma, and palette pruning paths with their complete
  controller. These paths now exist; identify and fix differences in their inputs, ordering, bounds, and termination.
  Corrected chroma variance to read padded blocks and normalize accumulated high-bit-depth differences, and chroma HOG
  to exclude padded samples and apply the subsampling area factor. Evidence: `encodeframe.c:188-213`,
  `aom_dsp/variance.c:308-354`, and `intra_mode_search_utils.h:406-434`. Complete controller parity remains unverified.
- [ ] Complete candidate versus winner evaluation, including retained multiple winners where selected by the speed policy,
  palette state, coefficient optimization, transform refinement, and final reconstruction/context publication.
  Current `av1/encoder/intra_mode_search.c:1688-1742` retains multiple candidates and restores their palette state;
  the managed single preliminary winner does not implement that selected policy.
- [ ] Reconcile quantizer selection, rate-distortion scaling, entropy-cost refresh, skip decisions, and early rejection
  across intra and inter. Pass the established best cost through searches instead of performing unrestricted losing work.

Use `speed_features.c`, `partition_search.c`, `intra_mode_search.c`, `nonrd_pickmode.c`, `rdopt.c`, `tx_search.c`,
`rdopt_utils.h`, and `encodemb.c` in the pinned source to resolve these production decisions together.
Finish with focused public encoding and independent native comparisons; primitive tests alone do not close this milestone.

## 2. Complete frame and sequence control

- [ ] Implement the required reference-slot selection, refresh scheduling, frame roles, entropy inheritance, and
  temporal motion context. Remove forced settings that differ from the reconciled libaom configuration.
  Use the current global-motion eligibility and frame-geometry/storage rules when completing those paths.
  The existing central-window translation search is not the native global-motion estimator; removing its effort gate
  does not establish equivalent model estimation or selection.
  Fixed-size ImageSharp frames do not justify adding arbitrary frame-resize support or copying native defensive checks.
- [ ] Complete motion search, reference/mode ordering, interpolation selection, compound selection, and winner refinement
  for those frame roles. Existing LAST/GOLDEN and compound support is a starting point, not the complete search policy.
- [ ] Connect remaining encoder tools selected by that configuration, including required motion-mode and reference tools.
  Reconcile super-resolution and film-grain configuration/signaling with the supported contract; do not invent tool
  restrictions or count a tool disabled by the matched configuration as a missing mandatory operation.
- [ ] Verify deblocking, CDEF, and restoration selection against independent encoding, including large filter groups,
  high bit depth, frame edges, and sequence reuse. Same-stream reconstruction agreement does not prove selection parity.

## 3. Establish encoder correctness

- [ ] Resolve the conflicting verification record. The previous plan reports both 836 passing focused cases and a
  366/372 run with six interpolation-selection failures. Identify the tested trees/configurations and verify the final
  implementation; do not label the current suite passing or those failures harmless without evidence.
- [ ] Establish separate-encoder comparisons from identical source samples and explicitly reconciled settings.
  Require maximum difference **at most one component unit per decoded sample** between separately encoded results.
  Report maxima and counts above one for every plane/frame; PSNR or averages cannot replace this gate.
- [ ] Separately require **byte-exact decoder output for identical bitstreams**, with maximum error zero and no differing samples.
  Reconcile conversion when comparing presented RGB. Independent decoding of our stream is necessary but does not prove encoder parity.
- [ ] Verify lossless native-plane and public pixel behavior at 8/10/12 bits; monochrome, 4:2:0, 4:2:2, and 4:4:4;
  alpha; known photographic and screen-content inputs; odd/coded edges; tiles; grids; and dependent animation frames.
- [ ] Verify public option precedence, pixel-format conversion, metadata/CICP/ICC, timing/repetition, root-frame behavior,
  cancellation, prefixed/non-seekable output, and balanced allocator ownership through the real encoder.
- [ ] Use established encoder `VerifyEncoder` patterns with independent decoding against the input where appropriate.
  Reference-image tests use `DebugSave` with `CompareToReferenceOutput`, meaningful images, short names, and conventional
  output folders. Keep precision and alpha in the comparison. Do not replace failing acceptance checks with self-decode,
  lower precision, changed expectations, or looser tolerances.

## 4. Close the performance and memory gap

- [ ] After the relevant source correction, measure equivalent complete encode/decode boundaries using the optimized native build.
  Include pixel conversion, output allocation/writing, and disposal. Keep raw AV1 and public AVIF measurements distinct.
- [ ] Report absolute timings, output sizes, per-sample correctness, quality, and allocations with reconciled settings.
  Record reference revision, runtime, CPU, and limitations. Use KiB/MiB for large memory sizes; managed allocation counters
  do not measure native memory. Earlier timings are not measurements of the final tree.
- [ ] Attribute remaining time and memory costs to production paths, then fix demonstrated controller, SIMD, allocation,
  copy, scratch-size, alignment, or lifetime deviations. Preserve closed-generic semantic operators and ImageSharp ownership.
  Do not add isolated search experiments or claim exhaustive search improves quality/performance merely by doing more work.

## 5. Final verification and cleanup

- [ ] Complete affected decoder regression verification and carry forward the previously unresolved tile-list/external-reference
  integration and decoder allocation/dispatch coverage. These were not reassessed by this encoder review and are not closed.
- [ ] After each completed feature, use Roslynk diagnostics and focused Release/.NET 11 Visual Studio VSTest verification.
  Run the complete relevant suite after the final edits, including scalar/SIMD tiers and constrained allocator coverage.
  Do not run .NET 10 and .NET 11 simultaneously, force one test thread, or stop the requested suite on its first failure.
- [ ] Keep production fully managed. At final cleanup, delete tracked temporary native integration, including
  `tests/ImageSharp.Benchmarks/Codecs/Heif/Native/aom_benchmark.c` and `Native/CMakeLists.txt`, plus obsolete adapters,
  generated artifacts, stray READMEs/reports, redundant tests, and discarded component benchmarks.
  Remove `Av1SequenceEncoderBenchmarks` as previously requested; retain required full encode/decode verification.
  Preserve useful native reference tooling outside the repository. Review exact deletion targets before deleting files.
- [ ] Review the complete diff against `upstream/main` for unrelated codec/shared-API changes and accidental files.
  Preserve authorized shared fixes; do not blindly revert user changes. Keep `.gitattributes` unchanged and do not inspect or implement HEVC.
- [ ] Commit verified feature checkpoints after inspecting the staged diff; exclude all new temporary native material.
  Commit final deletions normally, without rewriting history. Publish only work whose actual verification is stated accurately.

History and earlier measurements remain in Git and local artifacts. Update this list as work completes; remove completed tasks
instead of appending another dated audit. Release/.NET 11 builds with analyzers enabled after correcting 55 analyzer
errors. The 76-case public run passed 75 cases; its sole failure passed after correction in the final 24-case focused run.
The public class has not been rerun in full after those final corrections. No new benchmark or separate-encoder parity
measurement has been performed, and milestone 1 remains active.
