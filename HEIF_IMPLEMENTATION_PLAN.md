# AVIF and AV1 implementation plan

## ICC codec regression correction: 2026-09-09

The previous 10,487-case selection omitted JPEG, PNG, TIFF, and WebP ICC decoder tests. It was not
adequate verification of the shared ICC interpolation change. Ubuntu ARM64/net10 CI run 34293831444,
job 102286178786, exposed ten failures. The same ten failures reproduce on Windows/net11:
`icc-ci-local-baseline-r1` passed 15 of 25 selected cases and failed ten.

With explicit user authorization, ten stale PNG references were replaced with fresh DebugSave outputs
from the current interpolation implementation. No comparer tolerance or production arithmetic was changed.
The PNG/WebP perceptual outputs had 2,342 RGB components differing by one from each old reference.
The old JPEG references differed by up to 29 and TIFF by up to seven. These are not Ubuntu-only differences.
Comparing the downloaded CI output with the fresh Windows output instead gives exact RGB for PNG/WebP;
JPEG has 4, 4, 6, and 6 one-unit component differences, and TIFF has 72 and 66. No component exceeds one.
Those small platform differences fit the existing comparer tolerances. The updated images are managed
regression baselines for the selected interpolation behavior, not independent native-decoder evidence.

AVIF ICC inputs now have dedicated Decode_WhenColorProfileHandling methods instead of being included
in the general Decode corpus method. Existing profile, pixel, alpha, and sequence assertions are retained.
The corresponding 23 reference PNG paths were renamed without changing their pixel content.

Roslynk reported zero errors. Both test projects built in Release/net11. After restoring 59 accidentally
overwritten references and removing 205 unintended reference copies, full-suite VSTest execution of
ImageSharp.Tests and ImageSharp.PublicApi.Tests with no filter passed 58,053 tests, skipped 11, and failed
zero (58,064 total; 3.1977 minutes). Only the ten authorized ICC reference updates and 23 HEIF reference
renames remain in the reference-folder diff. This result verifies the regression suite, not complete AV1 parity.

## Managed checkpoint verification: 2026-09-09

ICC interpolation selection is committed as `9aeed10da`. The region conversion, container metadata,
chroma selection, and sample-rounding changes passed final combined verification.
Earlier unresolved-checkpoint notes below describe their state at the recorded investigation date.

The first combined Release/net11 VSTest run (`managed-checkpoint-r1`) passed 10,448 of 10,487 cases.
Its 39 failures exposed test expectations and reference files that had not followed the completed refactor:

- Encoded YUV range/matrix assertions now inspect encoded metadata; decoded RGB metadata is asserted as full-range identity.
- The chroma-position test explicitly selects Bilinear and retains its expected pixels.
- Allocation-return checks again use the allocator's existing unique allocation IDs. Array hash codes collided;
  they did not establish duplicate disposal. TestMemoryAllocator.cs is unchanged from HEAD.
- Three old CDEF, restoration, and super-resolution PNGs now use the corresponding existing public decoder
  references. Each was independently checked against the stored avifdec 16-bit output after 8-bit PNG conversion:
  all three comparisons reported zero differing pixels. These are not promoted managed outputs.
- Presentation comparisons again call DebugSave before CompareToReferenceOutput.

The final source edit passed Roslynk diagnostics with zero errors and a Release/net11 build with zero errors.
The combined rerun (`managed-checkpoint-r2`) passed all 10,487 cases in 2.1611 minutes through Visual Studio
VSTest on Release/net11. This covers HEIF/AV1, ICC, Flip/Rotate, and allocator tests. Test tolerances were not widened.
FlipProcessor and RotateProcessor now match upstream/main and are included with their migrated HEIF callers.
JPEG, PNG, ImageDecoderCore, DecoderOptions, and IccProfile also match upstream/main.
The five explicitly accepted ICC PNGs remain
managed regression baselines as documented below; they do not independently prove native parity.

The unused Hadamard primitive and its two tests remain outside this checkpoint. Temporary native benchmark
changes and integration files are also excluded. Previously committed native tooling is reserved for the
user-requested final deletion commit. Complete encoder control-flow parity and end-to-end performance remain open.

## ICC interpolation selection: 2026-09-09

ColorConversionOptions.IccInterpolationMethod now exposes Auto (default), Trilinear, and Tetrahedral.
The choice is passed through converter construction and applies to both source and target profiles.
Trilinear selects the previous three-channel calculation and four-channel multilinear interpolation;
the four-channel implementation uses local values without per-pixel allocations or stackalloc.
The Unicolour comparisons explicitly select Trilinear because both local versions 6.0.0 and
8.0.0-1-g3c888f0 use multilinear interpolation. Expected values and tolerances remain unchanged.
Release/net11 Visual Studio VSTest icc-interpolation-options-r1 passed all 63 selected cases,
including all 27 full-profile comparisons and the existing CLUT/LUT calculator tests.
This resolves the 21 Unicolour failures described below. Following explicit user approval,
the four HEIF ICC comparisons now use managed-output regression references: the singular,
grid, alpha PNGs and both sequence frame PNGs were promoted from a fresh DebugSave run.
These preserve the accepted floating-point ICC results instead of the LittleCMS-quantized results.
Release/net11 Visual Studio VSTest icc-promoted-r1 passed all four exact image comparisons.
These five PNGs are managed regression baselines, not independent native-decoder evidence.
Roslynk reported zero errors before the successful Release build. No decoder option was added;
the new setting is available through the shared ColorConversionOptions API.

Earlier investigation and verification:

ClutCalculator now uses tetrahedral interpolation for three-channel device-input tables, and linear
blending of two tetrahedral slices for four-channel tables. Lab-indexed output/linking tables retain
trilinear interpolation, selected when constructing the LUT pipeline. The Vector4 contract remains;
the unused N-channel implementation and its scratch allocations were removed. Constructor channel
checks now enforce the existing one-to-four-channel calculator capacity. No span API was added.
The selection follows local LittleCMS revision ab329ad5ce09dbb1f3547b6c126031aca606eb42:
src/cmsintrp.c:623-714,1039-1078 and src/cmsio1.c:581-624,776. Comments explain arithmetic without source citations.

The large perceptual ICC interpolation difference was reproduced independently from the embedded table bytes:
sequence frame 01, pixel (488,212), changed from RGB (150,192,163) to (0,222,51), matching the unchanged reference.
After the interpolation change, raw RGB comparisons reported maximum errors of one for the singular,
grid, and both sequence outputs; no RGB component exceeded one. Differing component counts were
3511, 3508, 2072, and 2072 respectively. The alpha fixture had 11 differing green components,
each one unit. These measurements do not establish byte-exact acceptance or alpha-plane parity.

Release/net11 VSTest icc-tetrahedral-r3: 115 cases, 90 passed, 25 failed. All 22 new mathematical
interpolation cases and eight existing CLUT cases passed. Four HEIF exact image comparisons and
21 full-profile comparisons remain failing. Full-profile tests currently use Unicolour; their expected
values and tolerances have not been changed. Further source comparison and independently justified
test updates remain required. LittleCMS's integer CLUT evaluation can quantize inside an otherwise
floating-point transform (src/cmslut.c:444-455); production precision must not be reduced to mimic this.
No reference PNGs were changed for this fix. The native source, build, and transicc utility remain outside
ImageSharp. This work is not a verified commit checkpoint or complete ICC/codec acceptance.

The ICC source at 79ecb74135ad47bac7d42692905a079839b7e105 supplies both trilinear and tetrahedral
interpolation. Matching a different interpolation choice does not establish that its original algorithm
was defective. The four-channel algorithm change also remains a separate policy choice.

Rounding investigation: sequence frame 01 pixel (420,39) is RGB (1,0,2) in ActualOutput and (2,0,2)
in ReferenceOutput. The native 16-bit pre-ICC PNG contains RGB (360,0,455). Evaluating the embedded
A2B0 table and target profile in double precision from that same input gives red 1.3962556266 in
eight-bit code units. Rounding the interpolated PCS values to 16 bits instead gives red 1.5442997191;
the unoptimized LittleCMS transicc transform independently reports 1.5443. Those values round to
the observed managed and reference values respectively. This demonstrates intermediate-quantization
error for this sample, not an incorrect final rounding rule. It does not attribute every remaining
mismatch or establish precision relative to the original unquantized YUV-to-RGB result.
The profile's mft2 reader allocates a 16-bit CLUT (LittleCMS src/cmstypes.c:2395), whose float evaluator
rounds inputs and returns integer-interpolated output normalized back to float (src/cmslut.c:83-99,445-455,565-566).
Production precision, test tolerances, and reference images were not changed for this investigation.

## Non-ICC 12-bit rounding investigation: 2026-09-09

The two 12-bit fixture files have identical SHA256
3bf9f91da471749e7df639ba7945d4d94c1c3e3968c26f3619fbbcfc92790576. They contain the same five-frame
64x64 limited-range YUV422 sequence with alpha and no ICC profile. Existing outputs for
colors-animated-12bpc-keyframes-0-2-3 were compared as raw RGB and alpha independently:
frames 0-2 are exact; frame 3 has five differing RGB components; frame 4 has four differing RGB
components and 33 differing alpha samples. Maximum error is one in each component; none exceeds one.

The RGB differences are reproduced at all nine locations by evaluating the two source arithmetic orders
on the same native-decoded YUV samples. HeifSampleConversion.ReconstructChromaRowBilinear blends sample
values before HeifColorConverter normalizes them. Libavif src/reformat.c:836-841 normalizes each sample
before weighted blending. Weights and sample positions agree. Single-precision intermediate rounding
moves values across the final half-unit boundary. Relative to a double-precision calculation with the
BT.601 coefficients, managed rounding agrees at five of these nine components and native rounding at four.
Neither path is uniformly more accurate. The inspected libavif checkout is v1.4.2-76-g66663952;
comparison with v1.4.2, used by avifdec, shows no changes to this interpolation arithmetic.

Alpha has a separate demonstrated arithmetic defect: HeifPlanarAlphaCompositor.cs:120-122 multiplies
by a single-precision reciprocal. A 12-bit alpha of 2047 should scale to 32759.49816849817. Reciprocal
multiplication produces 32759.5 and packs as 32760, while direct division produces 32759.498046875
and packs as 32759. Existing frame 4 pixel (60,3) contains those respective actual/reference alpha values.
Libavif src/alpha.c:93-97 uses direct division. The initial investigation made no production edits.

Implemented normalization during the existing SIMD sample loads, before chroma interpolation. Bilinear
reconstruction retains the four separate products in closest/horizontal/vertical/diagonal addition order,
including duplicated boundary samples. The color-model traversal consumes normalized components without
normalizing them again. Alpha uses direct division in the same loader. Existing scratch rows are reused;
no buffers, overloads, expected images, or comparison tolerances were added or changed.

Release/net11 VSTest heif-12bit-order-r2 passed both exact five-frame decoder comparisons (plus four
integration cases selected by the filename filter). The broader corpus exposed a scalar-tail luma
double-normalization introduced during this change; that was corrected before final verification.
After the final source edit, Roslynk reported zero errors, the Release/net11 build succeeded, and
heif-normalization-corpus-r2 passed all 54 public Decode corpus cases. Both 12-bit sequences now have
zero RGBA component error against their unchanged references, including alpha. DebugSave refreshed the
ActualOutput images. Temporary theory enumeration was restored to its previous setting after testing.
This verifies the decode corpus, not the separate ICC-conversion tests or complete codec acceptance.

## Container range correction: 2026-09-08

AVIF item and sequence RGB conversion now uses the container CICP range when present, falling back to the
decoded AV1 range otherwise. The conversion receives the range explicitly; reconstruction and reference-frame
color state are unchanged. Alpha conversion and encoder conversion continue to use their coded ranges.
This matches libavif src/read.c:6909-6926, which retains the container range despite a conflicting sequence header.
Irvine_CA.avif now passes the unchanged exact reference-image comparison in Release/net11 VSTest
heif-irvine-range-r1 (one test, one pass). Its ActualOutput PNG was refreshed. The other six previously failing
cases were not rerun for this change. Broader sequence/alpha regression verification remains outstanding.

## Reference regeneration: 2026-09-08

Regenerated the 138 PNG frames for the 19 previously failing decoder tests using one AVIF decoding pipeline:
libavif avifdec 1.4.2, dav1d 1.5.3, one worker, explicit 16-bit RGB PNG output, and all frame indices.
Chroma upsampling is explicitly nearest for 8-bit sources and bilinear for the two 12-bit animations.
Requesting 16-bit RGB bypasses libavif's libyuv conversion path. ImageMagick 7.1.2-31 writes the final PNGs;
Rgba32 references use 8-bit output and Rgba64 references retain 16-bit output. ImageMagick does not decode AVIF here.
For the four ICC cases only, ImageMagick/LittleCMS converts the embedded source ICC profile to the exact
CompactSrgbV4Profile bytes using perceptual intent, before final 8-bit output. These cases still have a 16-bit
integer RGB intermediate before ICC conversion; no claim of a floating-point-only reference pipeline is made.
Native tooling, intermediates, and the previous-reference backup remain outside this repository in
D:/GitHub/ynse01/av1-takeover-20260905/unified-references-20260908.
The reference images were regenerated independently; no managed decoded pixels or weakened assertions were used.
Release/net11 VSTest heif-unified-references-r1 ran only the 19 previous failures: 12 passed, 7 failed, 14.5724 seconds.
Remaining pixel mismatches still require investigation. This is not decoder byte-exact acceptance or libaom parity.

## Goal

Complete a production-quality, fully managed AV1 codec and its bounded AVIF/HEIF image container integration for ImageSharp. The finished work must decode and encode still images and bounded image sequences, preserve source precision, use ImageSharp memory ownership, and provide SIMD-first hot paths with one behaviorally identical scalar fallback.

This plan is the authoritative delivery checklist. A source file, unit test, build, self-roundtrip, or local implementation is not completion evidence by itself.

Checkpoint handling: work stays in the existing checkout. Do not create or use worktrees; the user reported a crash.
Commit completed, verified features regularly, with native reference material and temporary integration excluded.
Cleanup commit `e38989d63` restored three JPEG production files and the PNG encoder exactly to upstream/main.
Cleanup commit `93f48a480` restored DecoderOptions, ImageDecoderCore, IccProfile, Point, and PointTests,
included the PixelOperations formatting correction, and migrated dependent HEIF integrity, ICC serialization,
and coordinate-shift callers. Its 16-file scope excludes component-domain ICC and region conversion changes.
The restored Point implementation passed 55 focused Release/net11 tests (`point-upstream-cleanup-r1.trx`).
FlipProcessor and RotateProcessor restorations remain coupled to the decoder-region caller changes: committing
only their helper removals would break the committed HEIF decoder. The region/ICC checkpoint remains unresolved.

## Required final cleanup and deletion commit

2026-09-08 chroma-mode correction: HeifDecoder now follows the specialized-options API used by JPEG.
HeifDecoderOptions.ChromaUpsampling has three values: Auto (default), NearestNeighbor, and Bilinear.
Auto chooses nearest-neighbour for 8-bit source chroma and bilinear for higher source bit depths.
The selection reaches still items, grid children, and sequence presentation. Nearest-neighbour writes
directly into component rows without interpolation scratch; bilinear retains its existing SIMD sampling path.
Existing helper callers now pass the mode explicitly. The existing bilinear test explicitly selects Bilinear;
its expected pixels and every golden remain unchanged. Roslynk reported zero compiler errors.
Release/net11 build passed; heif-chroma-modes-r1 ran 126 tests: 107 passed, 19 exact image comparisons failed.
The explicit bilinear test passed and all 125 public decoder tests ran and regenerated their PNGs.
Remaining comparisons are unresolved; passing the bilinear component check is not end-to-end parity evidence.
Explicit-mode end-to-end references, all SIMD-tier checks, and performance verification remain outstanding.

2026-09-08 nearest-neighbour presentation change: the user selected chroma sample replication,
matching JPEG's ScaledCopyTo behavior. HeifPlanarColorConverter now selects the chroma row by
the absolute source coordinate and subsampling shift. HeifSampleConversion widens and duplicates
samples directly into the exact output row with 512/256/128-bit SIMD and a scalar tail.
Odd crop origins retain the second half of the first pair; odd right edges write only the requested pixel.
Removed vertical/horizontal weighted interpolation, its scratch rows, and full-width crop reconstruction.
No intermediate rounding, golden changes, or comparison-tolerance changes were introduced.
Release/net11 build succeeded with zero errors and 1,011 existing test-project warnings.
HeifDecoderTests run heif-nearest-decoder-r1 completed all 125 cases: 106 passed, 19 exact comparisons
failed. ActualOutput PNGs were regenerated through DebugSave. The existing ImageMagick/libavif PNG
references have unreconciled presentation conversions and do not establish libaom parity.
The old component test explicitly requiring bilinear chroma is obsolete under the user's new contract;
it has not been rewritten or used as acceptance evidence. No speed improvement has been measured.

2026-09-08 container mismatch correction: local libaom decodeframe.c:4167-4242 reads range from the
sequence header and reads chroma sample position only for non-monochrome 4:2:0. Removed the container
range-equality rejection in Av1Decoder; the coded range is retained. Av1CodecConfiguration.Validate now
compares chroma sample position only when that field exists in the coded color configuration. Other
configuration comparisons and structural validation remain. XnConvert's av1C bytes 81 20 02 00 declare
4:4:4 with a sample-position value of 2; that unused value caused its former rejection. Both original
fixtures' extracted AV1 payloads decoded successfully with local aomdec. Final Release/net11 build passed
with zero warnings/errors; heif-decoder-container-position-r4 ran all 125 HeifDecoderTests: 108 passed,
17 exact pixel comparisons failed, no decoder rejection failures. XnConvert passes its exact comparison;
Irvine now decodes and saves PNG output but still differs from its reference. Reference pixels and test
assertions were not changed. These two corrections do not establish that all container checks match the
reference, nor do they resolve the remaining color-conversion comparisons.

2026-09-08 reference-output checkpoint: all 54 corpus cases in HeifDecoderTests.Decode now have named PNG
references, including every frame of both 12-bit animations. The missing 19 high-depth still references
were produced with official libavif 1.4.2 avifdec/dav1d 1.5.3 as 16-bit PNGs. Four ICC conversion cases
use native 16-bit RGB PNG intermediates with embedded source ICC profiles, then ImageMagick 7.1.2-31
LittleCMS conversion to the same CompactSrgbV4 target profile. Preservation/compaction/metadata-skip
references reuse the independent unconverted reference pixels. Native binaries/intermediates stay outside
the repository. Existing mismatching references were not replaced and exact assertions remain unchanged.
Release/net11 full HeifDecoderTests run heif-decoder-all-references-r2 completed: 125 tests, 107 passed,
18 failed, no missing-reference failures. Remaining failures require source-led investigation; these PNGs
do not establish codec completeness. ActualOutput contains DebugSave/DebugSaveMultiFrame decoder images.
The temporary runner no longer overrides test parallelism and does not stop on failure. Only net11 runs;
the user's serialization constraint concerns simultaneous target-framework runs, not individual tests.

Test-convention correction is active: decoder verification must use independently produced reference images,
and encoder verification must use TestImageProvider/VerifyEncoder with the registered independent decoder.
Calculated expected-pixel code is not a replacement for reviewable decoder reference images.
Remove ad hoc raw-output generation and its generated files, preserving meaningful assertions.

Commit 7b5e7d4dc registers ImageMagick for AVIF reference decoding through the existing test infrastructure.
Exact VerifyEncoder checks passed for 8-bit RGBA and 10/12-bit RGBA64. The reference adapter retains native
precision and normalizes ImageMagick's left-aligned 10/12-bit samples to the Rgba64 range. No tolerance changed.
The migrated test methods remain uncommitted with their decoder-refactor dependencies. The 8-bit end-to-end
encoder test now uses the original Ducky, Bike, and Splash PNG fixtures, with exact ImageMagick verification
and DebugSave PNG output. Raw-output writers have been removed from the HEIF/AV1 test sources. The legacy
ActualOutput/Heif directory was deleted; HeifEncoderTests and HeifDecoderTests were emptied before the requested
Release/net11 serialized codec rerun. The calculated ICC pixel oracle has been removed. ICC conversion tests
now compare against independently decoded pixels, but those exact comparisons remain failing and unresolved.
The first full codec run passed 8,837 cases and stopped on a progressive-layer reference-image mismatch.
The failing helper called provider.GetImage(), which selects the registered reference decoder; four conformance
paths now explicitly select HeifDecoder.Instance so that decoder assertions exercise the managed codec.
The encoder cases now include Png.Transparency and use existing 10/12-bit RGBA TIFF fixtures for the high-depth
test, preserving exact Rgba64 comparison. All six known-image encoder cases passed in codec-clean-output-r2;
that broad run stopped on an allocation-log hash-uniqueness assertion after 8,857 passes. Further runs are
restricted to the decoder and encoder classes. No golden image or comparison tolerance has changed.
The Windows AVIF colour/alpha disagreement remains unresolved despite the ImageMagick encoder checks passing.
ICC reference conversion now selects the same CompactSrgbV4 target profile as production. Exact decoded-pixel
comparison still fails; ImageMagick's integer RGB intermediate is not a reason to quantize our floating-point
RGB before ICC. The attempted pre-ICC rounding was reverted and its absence verified in both scalar and SIMD paths.
PNG debug output now explicitly uses PngEncoder to retain ICC metadata, and single-frame cases use DebugSave
rather than DebugSaveMultiFrame. Old output directories must be cleared before final class reruns.
Returned still and sequence CICP metadata now describes full-range RGB, retaining source primaries and transfer
when ICC is not converted. The preserve-profile PNG export passed in heif-icc-preserve-metadata-r1.
After correcting DebugSave overload selection and clearing stale output directories, heif-decoder-convention-r5
passed ten cases and stopped on the exact Ducky ICC pixel comparison. Single images now produce .png files;
the sequence alone produces a frame directory. The decoder class has not completed.
The ICC output path is being corrected to retain floating-point RGBA through final TPixel conversion, using
the shared pixel operations and existing row scratch ownership. The earlier byte/Rgba64 intermediate could
discard precision for floating-point destination formats. This change is not yet runtime-verified and does
not establish the cause or resolution of the independent ICC comparison mismatch.

Perform this cleanup at the end of implementation and verification, before final delivery. It is part of the
task, not optional follow-up work. Update this checklist and the active milestone as changes and verification land.

- [ ] Review all task-added files against the final production implementation and acceptance requirements.
- [ ] Delete temporary native reference source, integration code, libraries, executables, build output, and
  generated comparison artifacts from the repository. Preserve needed local reference tooling outside the repository.
  `tests/ImageSharp.Benchmarks/Codecs/Heif/Native/aom_benchmark.c` is already tracked from `a7f0fca6b`;
  include its deletion and other tracked temporary reference material in the final cleanup commit.
- [ ] Delete stray task READMEs, redundant reports, temporary notes, and documentation for discarded approaches.
  Retain only documentation needed for the delivered codec and its supported verification workflow.
- [ ] Delete obsolete or unnecessary tests, including tests for rejected implementations, unrequested features,
  and implementation details that do not establish a required contract. Preserve independent acceptance coverage.
  Do not delete or weaken a failing test merely to conceal an unresolved production or verification defect.
- [ ] Delete obsolete component benchmarks, including `Av1SequenceEncoderBenchmarks`, and their stale references.
  Retained performance verification must measure full encode/decode with equivalent end-to-end boundaries.
- [ ] Inspect the final diff against upstream/main for unrelated codec changes, accidental files, and temporary
  tooling. Inspect the staged changes before committing the reviewed deletions.
- [ ] Commit the deletions normally at the end. The user explicitly chose this approach: no history rewriting
  and no force-push to remove earlier native-tooling commits.

## Earlier checkpoint evidence

The probability-storage checkpoint is `a658a2cb7`; adaptive syntax and retained palette tokens are `d8f6a1de3`.
The final supporting entropy, mode-grid, frame-buffer, and intra-copy run passed 2,085 tests with zero failures.
The encoder deblocking comparison covered 102,390 samples in 12 streams at 8/10/12 bits and 400/420/422/444:
maximum difference zero, differing samples zero, and samples exceeding one zero. These are same-bitstream
reconstruction checks; they do not establish parity between separately configured encoders or performance acceptance.

The decoder backlog was reverified after the final motion-cost edit in the existing checkout: Release .NET 11
built with zero errors and 1,009 existing warnings; serialized VSTest passed 10,104/10,104 AV1, public HEIF
encoder, and sequence-parser cases in 3.3497 minutes (`decoder-checkpoint-r1.trx`). The reconstruction tests
compare actual native-precision planes sample by sample with zero tolerance. Fresh optimized-native decoding
also matched the retained restoration and film-grain references over 15 streams and 8,500,087 samples:
maximum error zero, differing samples zero, and samples exceeding one zero. Temporary native tooling and the
uncommitted Hadamard-screening experiment remain excluded. Decoder-wide coverage and performance acceptance
remain open; these results verify the recorded cases and do not establish complete codec conformance.

The inter-mode ordering checkpoint evaluates inter candidates before intra and retains only the winning syntax
and transform choices across those trials. Final inter reconstruction regenerates the selected transforms using
existing scratch storage; empty planes retain prediction without re-quantization. Equal-cost intra candidates
do not replace the preceding inter winner. The source basis is `av1/encoder/rdopt.c:6355-6399,6491-6494`
and `av1/encoder/partition_search.c:474-503`. This corrects ordering and winner publication, but does not implement
the reference's full intra gating, TPL decisions, winner refinement, larger inter partitions, or additional references.
After the final code edit, Release .NET 11 built with zero errors and zero warnings; serialized VSTest passed
379/379 focused encoder, superblock, intra-copy, and public HEIF cases (`inter-first-r2.trx`, 41.5269 seconds).
Fresh optimized-native comparisons matched 86,859 samples across 39 motion streams and 477,243 samples across
three larger row-refresh streams. Encoder reconstruction also matched native decoding over 102,390 samples in
12 deblocking streams. Each comparison reported maximum error zero, differing samples zero, and counts exceeding
one zero. These checks establish the recorded same-bitstream behavior, not separate-encoder parity or performance.

Acceptance criteria are separate for encoding and decoding:

- Encoder parity permits at most one component unit per sample when comparing separately encoded results
  from identical source samples and explicitly reconciled settings. Report maximum errors and counts exceeding one.
- Decoder output must be byte exact against the reference for the same bitstream, precision, and output conversion.
  No one-unit tolerance applies to decoding. Report every differing sample and byte count; the required count is zero.
- Historical decoder reports of zero samples exceeding one are insufficient by themselves. A recorded maximum
  error of zero establishes sample equality only for the stated comparison scope, not complete decoder correctness.

## JPEG scope correction: 2026-09-07

- Removed legacy JPEG item decoding and encoding, compression selection, container brands, and dedicated HEIF JPEG tests.
- Removed the single-value compression-method enum and its encoder, metadata, and item-decoder properties. Encoding selects AV1 directly; still output always writes AVIF brands.
- Restored all JPEG production files to upstream/main, including earlier branch changes to metadata writing and spectral pixel packing.
- Restored the PNG cICP writer to upstream/main. No codec-specific production changes remain outside HEIF/AV1 against upstream/main.
- Removed the added spectral pixel converter and HEIF JPEG adapter. The HEIF implementation has no JPEG codec dependency.
- General container tests retain their assertions and now generate an opaque AV1 item instead of a JPEG item.
- The decoder-region callers now compile: Roslynk reports zero compiler errors across the loaded solution.
- Release .NET 11 test assembly preparation passed. Runtime verification is in progress and the refactor is not yet a verified checkpoint.
- The Compact ICC regression is corrected: HEIF invokes ICC conversion only for Convert mode. The subsequent decoder run passed 71 cases.
- The earlier ICC implementation processed packed destination pixels using two scratch rows. It was rejected
  and deleted. Component-domain ICC conversion is now implemented before pixel packing, as recorded below;
  its exact-output verification remains unresolved. Earlier ICC test results do not verify the replacement.
- Deblocking transform sizes now reside in block metadata: one selected size and sixteen inline variable-transform entries.
  The separate frame-sized luma/chroma maps and their ownership class are removed. Runtime verification after this change is pending.
- Historical JPEG-backed verification below does not establish acceptance for the remaining AV1-only implementation.

## Active production milestone: complete decoder-region refactor

Complete the still-image, grid, sequence, alpha, crop, rotation, and mirroring paths using exact destination regions.
Keep JPEG and other codec implementations unchanged against upstream/main. Retain the existing shared L16 SIMD implementation.
The generic RGB packer must accept exact-length regions, consistent with its existing scalar and SIMD implementations.
Complete existing caller migration and focused runtime verification before committing this checkpoint.
No new test infrastructure, extra decoder overloads, or component-test results substitute for native decoder parity.

### Transform and ICC integration: 2026-09-08, verification in progress

- The float/ICC output path now packs directly into the final region, removing its temporary TPixel row
  and subsequent WriteRow copy. Destination origins and integer row/column increments are resolved once.
  Contiguous output retains bulk pixel packing; reversed rows and columns currently use scalar final packing.
  This is not completion of the optimized traversal: tiled/SIMD placement and end-to-end performance remain
  unverified, and the outstanding ICC oracle mismatch below still prevents a verified region/ICC checkpoint.
  Roslynk reported zero errors after this edit. Release/net11 preparation succeeded with 1,008 warnings and
  zero errors; serialized VSTest passed 38 sequence cases (direct-region-sequences-r1.trx) and 70 public
  encoder/grid cases (direct-region-grid-r1.trx). These runs do not verify the outstanding ICC mismatch.
- Rotation/mirroring matrices are prepared outside pixel loops. Row traversal uses integer destination increments;
  crop and grid callers supply exact source and destination rectangles.
- ICC conversion now operates on reconstructed float components before final pixel packing, using the existing
  ColorProfileConverter span APIs and configured allocator. Still images, grid tiles, and sequence frames carry
  the selected profile through the same conversion boundary. Compact and Preserve retain source colors.
- Native auxiliary alpha rows are normalized/resampled before RGB unassociation and ICC conversion. Alpha is
  packed only with the final pixels; the ICC path does not read back or process a packed image. Unassociation uses
  planar Vector512/256/128 arithmetic without scalar alpha extraction or broadcasts.
- Scalar unassociation clips zero-alpha RGB just as the vector paths do, avoiding a row-width-dependent result
  when reconstructed RGB is outside the nominal device range.
- After that final alpha edit, Release/net11 built with zero warnings/errors and `alpha-final-public-r1.trx`
  passed all 70 public encoder/grid cases. Subsequent matrix-parameter and formatting corrections have Roslynk
  verification only: all three Matrix3x2 parameters and six calls now pass by value, with zero compiler errors
  and no production warnings. No runtime run is claimed for those later edits.
- The cropped alpha resizer's odd-column tail now selects its kernel using the destination X offset.
- Release/net11 preparation succeeded. The first runtime launch selected no tests because its filter contained
  literal quotes. The corrected serialized VSTest run stopped after six passes and one ICC lookup failure.
  Reconstructed RGB exceeded the device lookup interval; clipping was added during ICC interleaving without
  quantizing through packed pixels. The corrected Release/net11 build succeeded with zero warnings/errors.
- `icc-transform-regions-r6.trx` passed ten cases and stopped on the existing alpha/ICC exact comparison.
  Its expected image applies ICC to an already packed eight-bit preserved decode; the new component path
  applies ICC before that quantization. Alpha-equality assertions passed, but RGB comparison failed. The test
  and expected values remain unchanged; the numerical contract must be resolved before this checkpoint closes.
- JPEG's source path retains float normalization and profile conversion (`JpegColorConverterBase.Icc.cs:72-80,134`,
  `JpegColorConverter.Packing.cs:48-50,80-82`, `ColorProfiles/YCbCr.cs:153-160`). It does not justify adding an
  eight-bit rounding stage solely to reproduce the old packed-image test oracle.
- Independent focused runs passed 70 public encoder/grid cases (`transform-grid-regions-r1.trx`), 38 sequence
  cases (`transform-sequence-regions-r1.trx`), and 12 retained native-reference presentation cases at 8/10/12 bits
  and monochrome/420/422/444 (`native-presented-regions-r1.trx`). The last group uses exact image comparison
  across configured intrinsic paths. These passing groups do not clear the outstanding ICC failure.
- No benchmark or codec-wide parity claim follows from these edits. Earlier ICC results used the rejected
  post-packing implementation and do not verify this implementation.

## Remaining encoder milestone: motion search

The overall codec goal remains unresolved. After the decoder refactor, complete the integrated encoder motion-search path, including configuration,
allocation geometry, rate costs, candidate and winner state, full-pixel search, fractional refinement, and production
verification before advancing. The following dependency result does not close that milestone.

- The user resolved the speed-setting question: replace the invented Effort 0-10 scale with the reference's
  cpu-used setting as an enum. Preserve values 0-9 and the native direction, with higher values selecting faster
  encoding. Do not reverse-map or alias effort values. Use descriptive PascalCase names; the independent-frame
  coding mode is named IntraOnly in managed code. `HeifEncodingSpeed` now defines Level0-Level9.
  The public Speed property now passes these values unchanged into sequence motion search. The old Effort
  property still controls unmigrated intra and global-motion policies; replacing those policies remains required.
- The non-realtime reference default is zero (`av1/av1_cx_iface.c:255-256`); validation permits 0-9 outside
  realtime mode (`:781-782`). The value is assigned directly to encoder speed (`:1401`).
- Frame storage now takes an explicit luma border. Still images retain 64 samples; the byte and high-bit-depth
  sequence owners use the selected superblock width plus 32. Source, reconstruction, and reference owners use the
  same geometry. Native evidence: `av1/encoder/encoder_utils.h:1051-1064`,
  `av1/av1_cx_iface.c:3280-3282,3513-3531`, and `av1/encoder/encoder.c:2684-2694`.
  Managed changes are in `Av1EncoderFrame.GetPlaneBufferSize`, `Av1EncoderFrameBuffer`, and both sequence
  constructors in `Av1FrameEncoder`. Motion consumers read the physical border from the retained plane view.
- Release .NET 11 build passed with zero errors and 1,009 warnings. The focused frame, superblock, intra-copy,
  and transform run passed all 275 cases in 21.1628 seconds. Existing edge-replication and exact-owner tests now
  include 96- and 160-sample borders while retaining every 64-sample expectation.
  Evidence: `D:\GitHub\ynse01\av1-takeover-20260905\inter-frame-border-r1.trx`.
- At the border-only checkpoint, search still used the old effort-dependent controller. The production
  integration recorded below supersedes that controller. No separate-encoder parity measurement has been run.
- The 12 regenerated moving-color sequence streams match optimized native decoding at all 21,348 samples:
  maximum error zero, differing samples zero, and counts above one zero. This is same-stream evidence only.
- `Av1MotionSearchSettings` now resolves the motion-policy dependency from the enum, coding mode, dimensions,
  quantizer, boosted-frame role, and content classification. The production integration below consumes these settings.
  The reference initialization and override order is in `speed_features.c:2345-2377,2709-2739,2670-2685`,
  with independent-frame policies at `:345-618`, sequence policies at `:1097-1516`, dimension policies at
  `:169-342,713-1089`, and quantizer-selected patterns at `:2999-3050`. Block-size pattern changes are in
  `motion_search_facade.h:97-144`; mesh patterns are in `speed_features.c:25-46`. Verification and consumption
  by the full motion-search path remain required. This type is not a completed encoder architecture.
- The settings/border dependency run passed 312/312 tests in 20.7714 seconds under Release .NET 11
  (`motion-settings-border-r1.trx`). These assertions check policy boundaries and allocation geometry only.
- Inter motion rates now use worker-lifetime tables capturing the tile distributions at superblock entry.
  `Av1MotionVectorCosts.cs:35-204` builds all signed component costs by magnitude recurrence;
  `Av1EncoderBlockWorkspace` appends exactly 131,072 integer elements (512 KiB) only for inter workers.
  The sequence owner retains this storage across frames; still-image workers retain their previous allocation.
  Native evidence: `block.h:763-793`, `encoder_alloc.h:57-75`, `encodemv.c:125-250`, `rd.c:687-705`,
  and `encodeframe_utils.c:1663-1675`. Motion-cost refresh now follows the configured superblock, row, or
  evenly spaced row-set cadence. Levels 3 and above use rows; levels 5 and above below 720p use row sets.
  Evidence: `speed_features.c:1002,1315`, `encodeframe_utils.c:1556-1589,1628-1630,1663-1675`.
  `Av1MotionSearchSettings` resolves the policy and `Av1TileEncoder.ProcessTiles` applies it to the actual
  serial traversal, initializing each tile even when CDF adaptation is disabled. The frame-height rounding
  yields evenly distributed update rows for both 64- and 128-sample superblocks, including short final tiles.
  Release .NET 11 passed with zero errors and 1,009 existing warnings after the final edit. VSTest passed
  324/324 focused frame, motion-policy, and superblock cases. Optimized native decoding matched all 39
  compact streams (86,859 samples), plus three 129x273 multi-row streams (477,243 samples): maximum error
  zero, differing samples zero, samples exceeding one zero. These checks do not establish encoder parity
  or a measured performance improvement. Mode and coefficient cost-refresh lifecycles remain unresolved.
- Inter-mode rate calculation now applies the missing rounded 108/128 weight to motion-vector rate alone.
  Evidence: `mcomp.c:306-312`, `rd.h:46`, `motion_search_facade.c:535-542`; managed owning method is
  `Av1IntraSuperblockEncoder.ReferenceModeDecision.GetInterModeRate`. Search still requires the separate
  SAD and variance domains; this mode-rate correction does not reconcile the old search controller.
- After correcting a member-order analyzer failure, the Release .NET 11 build passed with zero errors and
  1,009 warnings. `motion-costs-r1.trx` passed 132/132 tests in 11.5927 seconds. New tests compare every
  representable component at integer, quarter-, and eighth-sample precision against independent syntax
  traversal, before and after adaptation, and verify snapshot retention and exact allocation ownership.
  No separate-encoder parity or performance conclusion follows from this verification.
- Frame-relative search bounds are now integrated into the existing inter full-pixel and fractional path.
  `Av1MotionVector.cs:115-168` computes distinct-prediction limits, reference-centered range intersections,
  inward full-pixel rounding, and reserved-endpoint exclusion using `Rectangle` with exclusive upper edges.
  Native evidence: `mcomp.h:203-228,341-357` and `mcomp.c:233-264`. The old search-order policy remains open.
- Final focused verification after the motion-state edits passed 185/185 cases in 12.7853 seconds
  (`motion-state-final-r1.trx`), following a successful Release .NET 11 build with zero errors and 1,009 warnings.
  The 12 freshly regenerated sequence streams again matched native decoding at all 21,348 samples, maximum
  error zero, differing samples zero, and samples exceeding one zero. This does not establish separate-encoder parity.
- Corrected a further numerical defect in high-bit-depth full-pixel intra-copy search. The shared SAD operators
  deliberately return unnormalized differences; the controller now truncates each complete SAD by 2 or 4 bits
  before adding eight-bit-domain motion cost. Native evidence: `encoder_utils.h:157-172`; managed evidence:
  `Av1IntraBlockCopySearchIndex.cs:689-691,797-893,895-1003,1005-1027`. Initial candidates, diamond candidates,
  mesh batches, and tails use the same normalization. The existing raw sample-operator contract is preserved.
- `PixelSearchNormalizesSadBeforeComparingMotionRate` verifies the actual search winner at 10 and 12 bits.
  Its two candidates deliberately reverse their SAD-plus-rate ordering if normalization is omitted; the test
  asserts that arrangement before invoking production search. The final focused run passed 138/138 cases in
  12.5530 seconds (`motion-sad-normalization-r1.trx`), after a successful Release .NET 11 build with zero errors
  and 1,009 warnings. Fresh native comparison again reported maximum error zero across all 21,348 samples.
  Full search policy, inter SAD/variance domains, and separate-encoder parity remain unresolved.

- The user has authorized regular commits after completed features. Inspect every staged diff and exclude
  temporary native integration and generated artifacts. Checkpoint `d78734dc7` contains the verified high-depth
  intra-copy SAD correction only; its regression tests remain with the pending explicit-border constructor changes.
- Rectangular SAD and residual-moment traversals now extend the existing residual operators without allocating
  a residual plane. They cover full and alternate rows, independent strides, vector-width tails, and wide squared
  totals for 128x128 twelve-bit blocks. Source contracts: `aom_dsp/variance.c:268-356` and
  `av1/encoder/encoder_utils.h:157-172`. The fixed 8x8 entry points are preserved. The new rectangular entry
  points are not yet consumed by the production motion controller and do not establish larger inter-block support.
- After correcting six comment-spacing analyzer errors, Release .NET 11 built with zero errors and 1,009 warnings.
  `rectangular-motion-metrics-r1.trx` passed 289/289 cases in 24.4180 seconds, including scalar comparisons
  across hardware paths, both signs at maximum precision, full-row and alternate-row costs, frame ownership,
  intra-copy, and sequence encoding. The 12 newly regenerated streams again matched native decoding across
  21,348 samples with maximum error zero. No encoder parity or performance conclusion follows.
- Full-pixel and fractional controller comparison now includes `mcomp.c:1005-1294,1311-1920,2482-3366`.
  Full-pixel SAD rates use an integer-rounded reference vector, whereas variance and fractional costs retain
  the subpixel reference (`mcomp.c:317-386`). Search retains winner variance, SSE, motion cost, a second
  candidate, and the five-position neighboring cost list; these are inputs to subsequent candidate refinement.
  The regular fractional tree uses directional ties, a selected diagonal, and conditional second-level probes;
  the two pruned trees have different first-step and follow-up decisions. These paths remain to be integrated.

- The complete single-reference full-pixel controller is now implemented in
  `Av1MotionSearchBase.FullPixelSearch.Search` (`Av1MotionSearchBase.cs:146-243`), with diamond restarts,
  pattern walks, mesh passes, alternate-row reliability fallback, separate SAD/variance rates, and retained
  winner/second-candidate/neighborhood state. Its byte and word operators consume the existing rectangular
  residual traversal. It is not yet called by the production frame encoder: fractional search, native-valued
  configuration plumbing, frame/block starting-candidate state, and final winner refinement remain open.
- `Av1MotionSearchSites.Configure` (`Av1MotionSearchSites.cs:63-153`) retains the six distinct site shapes and
  stride-relative offsets in the existing worker owner. Each configuration uses 794 integers, matching the
  eight-byte site and 22-stage/17-slot layout in `mcomp_structs.h:42-54`; six configurations add 18.61 KiB.
  Fast diamond variants share the big-diamond shape. Configuration is rebuilt only after a stride change.
  Pixel kernels are vectorized, but arbitrary three/four-candidate batches still require integration.
- Independent native verification calls the official `av1_make_default_fullpel_ms_params` and
  `av1_full_pixel_search` from the optimized static library. Temporary adapter sources, binaries, and results
  are outside the repository in `D:\GitHub\ynse01\av1-takeover-20260905`. Native source rows are copied into
  32-byte-aligned rows with identical pixel contents because optimized eight-bit SAD performs aligned source
  loads (`aom_dsp/x86/sad4d_sse2.asm:152,165`); the managed inputs intentionally have odd strides.
  The adapter first required a command-quoting correction, then exposed an eight-bit source-alignment access
  violation. The aligned-row contract was corrected before accepting comparison results.
- The first valid comparison found two hexagon disagreements. Source tracing identified the interior
  four-site-group path: `mcomp.c:1061-1076,1130-1147` passes a remainder count to the scalar helper whose
  loop bound is exclusive (`:947-967`). Interior six-site stages therefore omit their trailing two sites;
  the boundary path includes them. The managed controller now preserves that observed decision path.
  No claim that extra candidate evaluation improves the encoder is made.
- Final Release .NET 11 build passed with zero errors and 1,009 warnings.
  `full-pixel-controller-final-r1.trx` passed 315/315 cases in 20.7391 seconds. The freshly exported
  324 native comparisons cover all nine search methods, 16x16 and 64x64 blocks, 8/10/12-bit samples,
  exact/perturbed predictions, fractional spatial references, clamped starts, mesh-triggering error, and
  alternate-row fallback. Winner, second candidate, variance, squared error, motion cost, and five neighboring
  costs all matched exactly: zero mismatched cases, zero differing fields, and maximum field difference zero.
  Evidence: `motion-reference-comparison.json`. This verifies this controller subset, not encoder parity,
  complete motion search, the full codec, or performance.
- Fractional refinement now implements the regular, pruned, and more-pruned trees in
  `Av1MotionSearchBase.FractionalSearch.Search` (`Av1MotionSearchBase.Fractional.cs:136-275`).
  It preserves integer winner statistics, strict directional ties, conditional second-level probes, quadratic
  half-sample selection, precision stops, and the three retained centers used to terminate duplicate paths.
  Native evidence: `mcomp.c:2615-2897,2971-3000,3026-3350`. The new controller is not yet wired into frame encoding;
  scaled references, compound prediction, and frame/block candidate coordination remain open.
- Search interpolation reuses the existing direct SIMD convolution routines through
  `Av1TranslationalInterPredictor.PredictForSearch` (`Av1TranslationalInterPredictor.Search.cs:25-239`).
  Each Q7 pass rounds and clips to sample precision. The second pass consumes the clipped first pass, unlike
  the final reconstruction path. The packed prediction aliases consumed rows of the 128-column intermediate.
  Native evidence: `reconinter_enc.c:462-595` and `common/filter.h:271-279`. Required worker capacity is
  17,408 samples (17 KiB for bytes, 34 KiB for words), from `encoder.c:982-992`; production ownership plumbing
  remains in progress. This does not enlarge the existing ten-buffer 8x8 inter workspace.
- After correcting ten argument-formatting analyzer errors and one test brace-spacing warning, Release .NET 11
  built with zero errors and 1,009 warnings. `fractional-controller-final-r1.trx` passed 329/329 focused tests
  in 23.6178 seconds. Expanded native verification matched all 486 full-pixel cases and all 1,944 fractional
  cases exactly, including 8x8, 16x16, and 64x64 blocks at 8/10/12 bits. Fractional coverage includes all three
  policies, all three tap selections, four precision stops, retained/recomputed starting statistics, absent/present
  integer costs, and repeated-path termination. All 18 published fractional fields had maximum difference zero
  and mismatch count zero (`fractional-reference-comparison.json`, outside the repository).
  These comparisons establish the stated unscaled search subset only; encoder parity and performance remain unproven.
- Checkpoint `e9babd55a` commits only the rectangular error-metric API and its independent scalar/SIMD tests
  after the final focused run. The staged diff was inspected and contained no temporary native material.
- Production caller comparison identifies the next required integration dependencies:
  `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:1248-1385` still uses effort-derived radius and
  one eight-direction pass per stage, while `motion_search_facade.c:151-283` derives stages from frame motion
  history, spatial-reference magnitude, configured site radii, and the caller's search-range limit.
  Frame history is initialized/consumed in `encoder.c:2010-2042`, updated from encoded new vectors in
  `encodemv.c:268-272` and `bitstream.c:3953-3967`, and combined with spatial context from `rd.c:1173-1203`.
  This is missing control/state functionality, not a proposed performance heuristic.
- `SelectInterBlock` still gathers candidates in advance (`ReferenceModeDecision.cs:555-596`) and lacks the
  coordinated TPL/start-candidate and DRL-result state in `motion_search_facade.c:47-118,174-258,344-392,499-534`.
  The reference selects up to two weighted full-pixel starts and retains the second winner for fractional
  refinement (`:294-323,403-438`). Under the slower policy, the two refined winners are compared using
  estimated transform rate-distortion (`:439-481`), so selecting solely by search variance would be incorrect.
- The required transform estimator is `tx_search.c:3142-3265`: DCT transforms, regular quantization,
  coefficient rate, transform-domain distortion, and skip/non-skip decisions with retained entropy contexts.
  Existing `Av1ForwardQuantizer.QuantizeLossy` (`Av1ForwardQuantizer.cs:29-59`) provides fast quantization
  only. The regular quantizer and transform estimator are now implemented as described below; their consumption
  by the production motion controller remains required instead of the current reconstruction-SSE cost.
  Parameter evidence: `av1_quantize.c:582-631`; arithmetic evidence:
  `aom_dsp/quantize.c:109-169,262-316`; transform-error scaling: `tx_search.c:1115-1154`.

- Checkpoint `d5d4d74b1` adds regular quantization through the existing closed-generic quantizer traversal:
  `Av1ForwardQuantizer.Regular.cs:25-175` and its byte/high-bit-depth semantic operators. It preserves
  zero-bin rounding, sharpness, signed reciprocal correction, byte-source magnitude saturation, dequantization,
  and scan end positions. No per-transform allocation or coefficient copy is introduced.
- Checkpoint `869340d68` adds `Av1TransformBlockEncoder.EstimateInterTransform`
  (`Av1TransformBlockEncoder.cs:1256-1408`), which composes
  DCT/reversible transforms, regular/lossless quantization, coefficient rate, local entropy-edge updates,
  whole-block skip selection, and partial-estimate termination. It visits multiple transforms within prediction
  blocks up to 128x128 and reuses the caller's coefficient workspace. Only two 32-byte local context arrays are
  added. `GetTransformError` widens SIMD lanes before squaring, rounds high-depth totals before transform
  scaling, and uses the scale-zero/one/two factors of one-quarter/one/four. The scaling constant is one
  (`av1/common/idct.h:34`), not two; normalization is in `rdopt.c:789-806`.
- Source reconciliation confirms this estimator uses no quantization matrices or coefficient optimization:
  `tx_search.c:3199-3205` calls `av1_setup_quant`, whose matrix pointers are null (`encodemb.c:478-496`).
  This is specific to the winner estimate and does not resolve missing optimization/matrix policy elsewhere.
  The estimate retains coefficient-skip rate for all-empty blocks and excludes the block skip-header rate
  from returned statistics, while including that rate in the decision (`tx_search.c:3236-3265`).
- Independent temporary tooling calls the complete optimized `av1_estimate_txfm_yrd`, with default tile
  probabilities, explicit matching rate multiplier/header costs, source-minus-prediction inputs, bit depth,
  quantizer, sharpness, block/transform geometry, and incoming coefficient contexts. A first comparison exposed
  invalid test-generated sign contexts packed above bit six instead of bit three. Input generation was corrected;
  production rate arithmetic was unchanged. Regular-quantizer comparison separately reconciles native
  column-major coefficients with the established managed raster layout before invoking native quantization.
- Hardware exports now retain separate results for each actual vector width. This exposed an inert .NET 11
  `EnableAVX512F` test switch; `FeatureTestRunner` now maps that request to `EnableAVX512` on .NET 11.
  All four executed paths (scalar, 128, 256, 512) independently match optimized native results: 5,184 regular
  quantizer cases with zero differing quantized/dequantized coefficients or end positions, and 1,248 complete
  transform estimates with zero differences in rate, distortion, original energy, skip selection, and final cost.
  Earlier reports that only requested DisableAVX512F do not establish execution of the 256-bit path on .NET 11.
- Final Release .NET 11 build passed with zero errors and 1,009 existing warnings.
  `transform-estimate-final-r2.trx` passed 379/379 focused cases in 29.1507 seconds, including the shared
  feature runner, frame/superblock paths, coefficient entropy, transform blocks, and motion search.
  Evidence remains outside the repository in `D:\GitHub\ynse01\av1-takeover-20260905`:
  `regular-quantization-comparison.json` and `transform-estimate-comparison.json`.
  At the transform-estimator checkpoint, the production encoder still called the old effort-dependent search.
  The subsequent production integration below connects frame/block coordination, DRL state, storage, and configuration.
  No benchmark, encoder parity, or full-codec completion claim follows from these dependency results.

- Checkpoint `105d76954` adds `Av1MotionSearchBase.SingleReference.cs`, which coordinates spatial/temporal starts,
  duplicate-start history, frame/block step selection, full-pixel winners, DRL pruning, fractional refinement,
  final interpolation and transform-RD selection, and retained results across three differential-reference choices.
  It borrows source/reference planes and caller-owned arithmetic buffers; it adds no per-candidate owner.
  `CollectStartingCandidates` retains partial temporal analysis, groups full-sample positions into rounded
  eight-sample cells, weights the spatial start, and orders completed temporal votes by descending weight.
  Reference control-flow evidence is `motion_search_facade.c:47-118,120-544`; normal coding disables the optional
  full-pixel cost list (`speed_features.c:2360`, `encoder.h:4240-4244`), so the controller passes an empty span.
  Full-result DRL pruning resolves from `speed_features.c:781,890,971,997`, including the inclusive 480-line boundary.
- Temporary tooling now calls the actual optimized `av1_single_motion_search`, its final predictor, and its
  transform estimator. With reconciled block inputs, cost tables, speed policy, frame/block step parameters,
  reference choices, temporal analysis, and header costs, all 2,646 decisions match exactly across 8/10/12-bit
  samples, 8/16/64-square blocks, seven speed levels, integer/fractional motion, and incomplete temporal analysis.
  All compared vector components, syntax rates, skip decisions, start counts, and retained full-search values
  have maximum difference zero and zero mismatches (`single-motion-comparison.json`, temporary directory above).
  Final .NET 11 Release VSTest run `single-motion-final-r3.trx` passed 467/467 focused cases in 33.9823 seconds;
  the final build had zero errors and 1,009 existing warnings. The complete full-pixel and fractional comparisons
  also match exactly in all 486 and 1,944 cases respectively, with zero maximum errors and zero mismatch counts.
  This run used the host SIMD configuration; it does not independently establish each disabled-intrinsic path.
  The complete staged checkpoint was reviewed before committing; it contains 18 managed source/test files and
  excludes temporary native source, integration, libraries, executables, builds, and generated comparison data.
- Production integration now replaces FindInterMotionVector and its SSE/radius loop in
  Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:490-1022. NEWMV uses the coordinated controller with
  frame-relative bounds, live reference-stack rates, local coefficient contexts, predictor filters, and retained DRL results.
  The existing LAST-frame candidate path now follows nearest/new/near/global ordering and no longer gates
  nearest, near, or new motion on Effort. Reference order: rdopt.c:111-143. Reference-dependent range reduction
  follows rdopt.c:1240-1271; frame/spatial steps follow encoder.c:2010-2042 and rd.c:1130-1203.
  Prediction storage borrows the inactive intra workspace beyond the retained inter candidates, with 17,408
  samples available and no additional owner. Byte and ushort block operators use the existing motion operator family.
- Av1TileEncoder.Encode:246-303 resolves frame policy and initializes retained motion history. Actual packed
  NEWMV syntax updates the absolute whole-sample maximum in Av1TileWriter.WriteModesBlock. Trials and
  inherited/global vectors do not contribute. Reference evidence: encodemv.c:251-273 and bitstream.c:3953-3967.
  Source classification is now carried separately from palette/intra-copy syntax eligibility; the detector
  includes the eight-sample-aligned source extent. Reference evidence: encoder.c:2047-2110,2445-2484 and
  aom_scale/generic/yv12config.c:247-248. Existing Effort-based screen-tool eligibility remains a deviation.
- Release .NET 11 build passed with zero errors and 1,009 existing warnings. The final focused run
  motion-production-r2.trx passed 494/494 cases in 36.0448 seconds. Thirty-nine regenerated streams exercise
  all ten speed levels at 8/10/12 bits, three-frame 4:2:0 prediction, and additional 4:2:2/4:4:4 cases.
  Optimized native decoding and managed decoding match byte-for-byte for all 117 frames and 86,859 samples:
  maximum error zero, differing samples zero, samples exceeding one zero (production-motion-decoder-comparison.json).
  The first run stopped at an old Effort-0 fixture that assumed GLOBALMV was the only available inter candidate.
  The fixture now supplies an explicit entropy history favoring GLOBALMV and checks its cost against every
  competing mode; all preexisting expected skip/rate/distortion/coefficient/pixel assertions remain intact.
- This remains the same active production milestone. Scaled references, production temporal analysis,
  broader block/reference support, complete mode pruning and ordering, and mode/coefficient cost refresh
  remain open. Still-image and intra/global-motion Effort policies are not migrated. The production decoder
  comparisons establish same-bitstream equality only, not separate-encoder parity, performance, or codec completion.

## Takeover audit: 2026-09-05

The earlier checked boxes and measurements below are historical checkpoint reports, not accepted conclusions about
the current encoder or complete decoder. The fresh production-path audit is still in progress. The first decoder
measurement after source-led corrections is recorded below for 2026-09-07; no encoder benchmark has been run during
the takeover. The complete 738-file upstream diff has not yet received a line-by-line audit.

### Reference and worktree evidence

- Live `git ls-remote` identifies official `https://aomedia.googlesource.com/aom` main as
  `d565eec60f084421fa34fc0534b760c6452b6a6c`. The export at `D:\GitHub\ynse01\aom-d565eec6-source`
  was compared with that revision's official archive: 1,522 files present, 18 byte-identical, 1,504 differing only
  by CRLF versus LF, and zero remaining content differences. The archive is temporary and outside this repository.
- Live ImageSharp main and local `upstream/main` both resolve to `adb982081a7e89a824f873f7f286f517e04f80dd`.
  The starting HEAD is `a7f0fca6b01d0d498862aba66e024942393c1614`; the index is empty. The initial worktree
  contains 13 modified files and two untracked benchmark files. Preserve all existing work while correcting demonstrated defects.
- The optimized native build is `D:\GitHub\ynse01\aom-d565eec6-build-x64-release`: Ninja, MSVC x64,
  Release `/O2 /Ob2 /DNDEBUG`, runtime CPU dispatch, decoder, encoder, and high-bit-depth support.
  Generated `config/aom_config.h` enables SSE2, SSE4.1, AVX2, and AVX512. The cache's zero-valued HAVE entries
  do not describe the generated configuration. The version header reports 3.15.0 but does not independently identify a commit.
- Commit `a7f0fca6b` already contains temporary native integration:
  `tests/ImageSharp.Benchmarks/Codecs/Heif/Native/aom_benchmark.c`, its `CMakeLists.txt`,
  `LibaomBenchmarkEncoder.cs`, and the referencing sequence benchmark. The current untracked decoder benchmark
  and adapter wrapper are also temporary integration. Do not stage or commit them. Removal from existing commits
  or deletion of local reference files requires a separate, concrete proposal; no history rewrite or deletion is authorized here.
  Existing generated conformance fixtures also require classification before any cleanup proposal.

### Confirmed implementation deviations

Managed paths below are relative to the repository; reference paths are relative to the verified libaom export.
Line numbers describe the inspected starting tree, before subsequent corrections.

| Class | Managed evidence | Official reference evidence | Finding |
| --- | --- | --- | --- |
| Missing functionality | `Av1FrameEncoder.cs:372-408`, under `src/ImageSharp/Formats/Heif/Av1/Pipeline` | `av1/encoder/encoder.c:641-646`; `av1/av1_cx_iface.c:287-288,1284-1286,1561-1562` | Sequence setup unconditionally disables CDEF, restoration, and intra-edge filtering. These are not equivalent to the reference's configured tool decisions. |
| Architectural deviation | `Av1FrameEncoder.cs:508-542,1551-1557` | `av1/encoder/encode_strategy.c:168-230,1664-1669` | Every frame is error resilient, refreshes all slots, disables frame-end CDF publication, and resets probabilities. The reference selects retained primary-reference state. |
| Missing functionality | `Av1IntraSuperblockEncoder.ModeDecision.cs:185-245`; `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:526-566` | `av1/encoder/partition_search.c:3320` onward; `av1/encoder/rdopt.c:6196-6236` | Inter frames retain a fixed 8x8 partition tree and search only LAST. Larger partitions and additional reference roles are not implemented by this path. |
| Architectural deviation, partly corrected | `Av1IntraSuperblockEncoder.ModeDecision.cs:606-753` | `av1/encoder/rdopt.c:6355-6399,6491-6494`; `av1/encoder/partition_search.c:474-503` | Inter now precedes intra and the final winner is reconstructed after competition. Full intra gating, pruning state, bounds, and winner refinement remain incomplete. |
| Architectural deviation | `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:576-617` | `av1/encoder/rdopt.c:111-142` | Managed single-reference mode order is NEAREST, NEAR, GLOBAL, NEW. The reference default order is NEAREST, NEW, NEAR, GLOBAL across eligible references. |
| Architectural deviation | `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:1278-1414` | `av1/encoder/mcomp.c`; caller policy in `av1/encoder/motion_search_facade.c` | The managed radius is an effort-shifted value capped by its border; each scale visits eight offsets once. This is a simplified search controller whose full reference-policy reconciliation remains open. |
| Missing functionality | `Av1TransformBlockEncoder.cs:1047-1097` | `av1/encoder/encodemb.c:842-885` | Lossy transform coding ends at fast quantization. Reference coding selects quantization with trellis policy and can optimize coefficients before reconstruction. Native primitive arithmetic alone does not establish encoder parity. |
| Architectural deviation | `Av1IntraSuperblockEncoder.ModeDecision.cs:1581-1655` | `av1/encoder/intra_mode_search_utils.h:622-657`; `av1/encoder/intra_mode_search.c:467-491,1597-1619` | The 8x8 SATD screen imports only the 1.5-best threshold. The reference also maintains ranked candidates and quantizer/neighbor-dependent pruning, and owns the surrounding mode/transform decisions. Contrary to an initial audit hypothesis, this revision's `intra_model_rd` does return raw SATD. No modeled-RD-versus-SATD numerical defect is established. |
| Missing functionality | `Av1TransformBlockEncoder.cs:695-837` | `av1/common/reconintra.c:958-986,1204-1243` | Encoder directional prediction bypasses edge preparation and upsampling. Flipping the sequence flag alone would make encoder reconstruction disagree with its emitted syntax. |
| Verification gap | `Av1InverseTransformerFactory.cs:47-60,98-112`; `Av1InverseTransformTests.cs:402-532` | Sparse inverse dispatch in `av1/common/idct.c` and `av1/common/x86` | The uncommitted decoder path specializes DC-only DCT; other lossy EOB values still use full transforms. Its new tests compare against the managed full transform, not an independent native oracle. Complete sparse dispatch and SIMD reconciliation remain open. |

Two reconstruction-input defects were established and corrected during this audit:

- Rectangular intra transforms require width plus height samples on each extended edge. The starting encoder
  prepared twice the width above and twice the height to the left
  (`Av1IntraSuperblockEncoder.ModeDecision.cs:1466-1534,2583-2677`;
  `Av1IntraSuperblockEncoder.ChromaModeDecision.cs:179-182,1153-1223`).
  This could expose unprepared scratch samples to directional prediction. Reference
  `av1/common/reconintra.c:1149-1184,1451-1488,1817-1820` copies the available adjacent edge and repeats its endpoint
  through the width-plus-height extent. Luma and chroma now reuse the existing edge-preparation method, and tiled
  candidate preparation follows the same extent without adding storage or an allocation.
- Trial and final geometry discarded the enclosing partition, and all encoder directional edge-availability calls
  passed `None` (`Av1IntraSuperblockEncoder.ModeDecision.cs:397-418,858-921,1434-1464,2557-2581` in the starting tree).
  Reference `av1/common/reconintra.c:158-192,343-379` selects different availability tables for mixed vertical
  partitions; `av1/decoder/decodeframe.c:1392-1416` distinguishes split child nodes from mixed-partition leaves.
  Both trial and final setup now retain the terminal partition in existing mode information, and luma, chroma,
  and tiled prediction consume it. Split children retain their own implicit `None` leaf state.

The isolated 8x8 Hadamard pruning hook has been removed from production mode selection. Its primitive and existing
component tests remain uncommitted in the worktree. Removing the unsupported hook does not complete the remaining
encoder controller or establish a quality or performance improvement.

Disabled tools, limited search, and different decision order can also change reconstructed samples without
producing an invalid bitstream. They are separate from the reconstruction-input defects above.

### Architecture and verification findings

Reference-edge correction checkpoint `578ec34d9`, verified on 2026-09-05:

- Final Release .NET 11 build after removing the screening hook: zero errors and zero reported warnings
  on that incremental build. The preceding test compilation reported 1,009 existing warnings.
- Visual Studio VSTest 18.9, .NET 11 preview 7, serialized collections, one test thread, stop-on-failure:
  251/251 cases passed in `Av1EncoderFrameTests`, `Av1IntraSuperblockEncoderTests`, and `HeifEncoderTests`.
  The final report is `D:\GitHub\ynse01\av1-takeover-20260905\no-screen-final.trx`.
- `RectangularIntraReferencesExtendTheLastAvailableSample` checks explicit reference-edge samples at
  8/10/12 bits for both rectangle orientations and available/unavailable extensions, including output sentinels.
- `ProductionMixedPartitionsPreserveReconstructionOrder` requires actual mixed partitions and compares the live
  mapped encoder partition state and retained reconstruction with production decoding. Its two emitted 32x32
  monochrome streams also match the optimized libaom decoder: 2,048 luma samples, maximum error 0,
  and zero samples exceeding one.
- The twelve freshly regenerated two-frame color streams cover 8/10/12-bit 4:2:0, 4:2:2, and 4:4:4:
  managed and optimized native decoding agree on all 21,348 Y/U/V samples, maximum error 0, zero exceeding one.
  Per-frame and per-plane counts are in `D:\GitHub\ynse01\av1-takeover-20260905\decoder-comparison.json`.
- These are bounded reconstruction and same-bitstream decoder checks. They do not prove separately encoded
  output parity, complete encoder control flow, or decoder-wide conformance. No benchmark was run.
  Temporary launch scripts, native comparison output, logs, and reports remain local and are excluded from commits.

Tile-reader construction ownership correction, verified on 2026-09-05:

- At checkpoint `330d4c4ea`, `Av1TileReader.cs:279-316` acquired frame syntax storage before constructing
  above/left neighbor contexts. Failure in the above constructor bypassed cleanup; failure in the left
  constructor returned only the above context. Both leaked the completed `FrameInfo` owner. This is a
  demonstrated lifetime defect, independent of reconstruction quality and encoder search completeness.
- Reference `av1/common/alloccommon.c:370-408,411-452` keeps partial above-context allocations reachable
  from common state; decoder destruction calls `av1_remove_common` (`av1/decoder/decoder.c:239`), which
  frees those contexts (`alloccommon.c:501-506`). Native contexts are reused until dimensions outgrow them
  (`av1/decoder/decodeframe.c:5125-5135`); the managed per-frame allocation lifetime remains a separate
  architectural deviation. This correction only restores cleanup at the existing managed owning boundary.
- The new regression failed when the ninth allocator request was rejected: all eight successful frame-state
  allocations had no matching return (`tile-ownership-red.trx`). Serialized VSTest stopped on that failure.
  The existing first catch now also covers above-context construction, and the left-context catch returns
  both preceding owners. No new buffer, owner, guard, or native dependency was introduced.
- Four standalone-reader cases cover monochrome/color and 64/128 superblocks, reject every allocator request
  in turn, and require exactly one return per successful allocation. After the final edit, the focused tiling,
  reference-store, reference-motion, and decoder conformance set passed **137/137** through serialized
  Release .NET 11 Visual Studio VSTest in 1.8500 minutes (`tile-ownership-final.trx`). The final build had
  zero warnings/errors; Roslynk reported zero compiler errors. Reports remain in the temporary directory
  `D:\GitHub\ynse01\av1-takeover-20260905`, outside the repository. No benchmark was run; this does not
  establish separate-encoder sample parity or complete decoder ownership/reference-lifetime equivalence.

Sequence-construction ownership correction, verified subsequently on 2026-09-05:

- At checkpoint `578ec34d9`, `Av1FrameEncoder.cs:1492-1560,1668-1713,1778-1824` allocated common state and
  frame owners without unwinding partial construction. `Av1EncoderPictureBuffer.cs:87-164` and
  `Av1SymbolEncoder.cs:270-274` had the same problem inside their multi-owner constructors. A failed constructor
  never returns an instance to the caller's `using` statement. This is a demonstrated lifetime defect, independent
  of encoder quality. Reference `av1/encoder/encoder.c:1480-1499` clears compressor state and invokes
  `av1_remove_compressor` if construction fails.
- The new production-factory regression failed before the correction: rejecting the second allocation left the
  first allocation unreturned. The first failure stopped VSTest as configured; evidence is
  `D:\GitHub\ynse01\av1-takeover-20260905\allocation-failure-before.trx`.
- Common sequence state, both sample-width frame constructors, picture state, and symbol state now unwind
  completed owners at their own construction boundaries. Picture context views borrow its two owners
  (`Av1NeighborArrayUnit.cs:65-70,135-141`), so failed picture construction releases those owners directly.
  Common cleanup avoids dispatching into derived frame disposal before derived construction begins.
  Nullable disposal checks are restricted to owners that can be absent after an allocation failure; no new
  owners, buffers, copies, or native dependencies were introduced.
- Six color/alpha cases at 8/10/12 bits reject every allocator request in turn and require exactly one return
  for every earlier successful request. All six pass. The final affected frame, superblock, and public encoder
  set passes 257/257 cases through serialized Release .NET 11 Visual Studio VSTest with stop-on-failure.
  The report is `D:\GitHub\ynse01\av1-takeover-20260905\ownership-final.trx`.
- The final incremental Release .NET 11 build reports zero warnings and errors; Roslynk reports zero compiler
  errors. The twelve regenerated color sequences and two mixed-partition streams were compared again with the
  optimized native decoder: 23,396 samples, maximum error 0, zero samples exceeding one. This remains bounded
  same-stream decoder/reconstruction evidence, not separate-encoder parity. No benchmark was run.

- PNG resolves options before converted metadata and sanitizes incompatible output combinations
  (`src/ImageSharp/Formats/Png/PngEncoderCore.cs:1632-1675`). TIFF follows the same precedence and converts
  unsupported combinations (`src/ImageSharp/Formats/Tiff/TiffEncoderCore.cs:111-175,371-457`).
  HEIF's generic pixel input must retain that conversion contract; source pixel type is not an eligibility gate.
- JPEG's closed `JpegColorConverter<TOperator>` owns traversal and calls semantic static operator arithmetic
  (`src/ImageSharp/Formats/Jpeg/Components/ColorConverters/JpegColorConverter.Operator.cs:144-250`).
  Shared prediction work must follow that family boundary and existing ownership APIs.
- Encoder block scratch already shares mode and inter storage by their non-overlapping lifetimes
  (`Av1EncoderBlockWorkspace.cs:31-103`). The sequence constructor retains picture, coefficient, block,
  entropy, conversion, and frame state (`Av1FrameEncoder.cs:1492-1646`). Exact sizing, failure unwinding,
  alignment, reference parity, and allocation attribution still need the complete native comparison.
- Two decoders agreeing on one managed bitstream establishes only decoding agreement for that bitstream.
  The benchmark's photographic setup checks that agreement; it does not compare separately encoded outputs.
  Its native output uses ImageSharp color conversion, so it is not an independent RGB conversion oracle.
- Historical separate-encoder Y/U/V maxima of 40/30/53 fail the required one-component-unit limit.
  Counts exceeding one were not supplied with those historical figures. Neither those figures nor the recorded
  1,363.09/71.73 ms timing pair is a new measurement of a subsequently edited tree.
- VSTest logs identify Visual Studio 18.9 x64 and `.NETCoreApp,Version=v11.0`. Future runs must deduplicate
  child environment keys case-insensitively, disable collection parallelism, stop on failure, and run serially.
  Absence of an observed dialog is not evidence that no popup occurred.

Ordinary-intra skip investigation at checkpoint `c778217a9`:

- Managed `Av1IntraSuperblockEncoder.ModeDecision.cs:622-667,758-829,1291-1396` scans retained empty
  transforms, computes their rate again, and can replace ordinary intra coefficient syntax with block skip.
  `Av1TileWriter.cs:2628-2648` explicitly describes this as a departure from current libaom.
- Verified reference `av1/encoder/rdopt.c:3516-3578` assigns ordinary intra rate including
  `skip_txfm_cost[skip_ctx][0]` and sets `skip_txfm = 0` before separate IBC evaluation.
  Its intra-in-inter-frame path does the same at `rdopt.c:5772-5801`.
  Final coding enforces this at `av1/encoder/partition_search.c:2122`.
  Reference `av1/common/blockd.h:372-374` counts IBC as inter for this decision.
- This is an encoder-policy deviation, not an invalid-bitstream claim. Suppressing empty transform symbols
  changes block-skip and coefficient probability adaptation for subsequent blocks even when current pixels match.
  Managed `Av1TileWriter.cs:825-852,1100-1143` consumes the selected flag in symbol order and publishes neighbors.
- The regression `PreservesIntraNonSkipForAllZeroTransforms` asserts the reference policy while retaining
  all zero-EOB and precomputed-versus-live syntax checks. It failed on the fixed-DC traversal before correction:
  mono intra returned `Skip = true` (`intra-skip-before.trx` in the local takeover report directory).
  The first affected run then exposed a second path: `Av1IntraSuperblockEncoder.cs:214-285` independently
  marked zero-coefficient blocks skipped, so its exact byte comparison with the corrected live path failed.
  Both paths now preserve ordinary-intra non-skip, and their exact byte comparison is retained.
  These changes enforce the demonstrated reference contract; no pixel tolerance was changed.
- Automatic approval review rejected a combined production patch and deletion of the old helper-specific
  entropy test. A second review rejected their removal after verification as weakened coverage.
  The unused helper and its test remain unchanged; no further removal was attempted.
  With that helper and test still present, the corrected production, fixed-DC, and public encoder paths pass
  258/258 serialized Release .NET 11 VSTest cases (`intra-skip-r2.trx`). Decoded mode assertions cover ordinary
  intra syntax in key/inter frames at 8/10/12 bits and all three color subsampling formats; repeated-frame
  tests still require skipped inter blocks. The build has zero errors and 1,009 existing test-project warnings,
  with none in the changed files; Roslynk reports zero compiler errors.
  Optimized native decoding of freshly regenerated streams matches 23,396 samples: maximum error 0 and zero
  samples exceeding one. This is bounded same-stream evidence, not separate-encoder parity.
  Roslynk now finds only the old entropy test referencing the obsolete skip helper; no production caller remains.
  No benchmark was run, and the full controller, coefficient optimization, and separate-encoder gates remain open.

Coefficient optimization and evaluation-stage investigation:

- `av1/encoder/encodemb.c:208-224,474-563,831-887` selects quantization and coefficient optimization
  from segment and evaluation policy before publishing coefficient context and reconstruction.
- `av1/encoder/rdopt_utils.h:608-709` distinguishes default, mode, and winner evaluation: transform
  pruning, default transform use, skip/DC prediction, distortion domain, coefficient optimization threshold,
  and transform-size search differ by stage. Changing stage invalidates cached RD results.
- `av1/encoder/txb_rdopt.c:400-560` consumes plane/block RD scaling, transform/EOB/context costs,
  quantized and original coefficients, dequantization and matrices. It can lower coefficients, move EOB,
  and select an empty transform; it updates coefficients, EOB, entropy context, and rate together.
  Importing this primitive without those callers and their state would leave the controller deviation unresolved.
- `aom/aomcx.h:215-221`, `av1/av1_cx_iface.c:781-782,1401-1409`, and
  `av1/encoder/speed_features.c:2709-2776` show usage-specific CPU settings and feature initialization.
  ImageSharp's 0-10 effort scale has not yet been reconciled with these policies. No new effort mapping is assumed.

Further quantization, distortion, and final-packing source comparison on 2026-09-05:

- `Av1ForwardQuantizer.cs:88-236` and `Av1ForwardQuantizer.Operator.cs:98-316` implement the no-matrix
  fast quantizers. The rounding factor 64 agrees with `av1/encoder/av1_quantize.c:609-651` at sharpness zero;
  the regular quantizer's factor 48 is not evidence of a fast-quantizer rounding defect. The managed path
  still lacks the complete quantizer-selection/optimization and quantization-matrix policy. Regular quantization
  with sharpness is now implemented for the motion-winner estimator; its production integration remains open.
- `Av1TransformBlockEncoder.cs:1175-1225` always ends lossy coefficient selection at fast quantization.
  Reference `av1/encoder/tx_search.c:2064-2391` derives trellis eligibility from segment, evaluation stage,
  normalized residual energy, and transformed SATD; chooses fast or regular quantization; measures coefficient
  rate before reconstruction; selects transform-domain or pixel-domain distortion; retains the winning
  coefficient buffer, EOB, type, and entropy context; then reconstructs intra neighbors. These are coupled
  state transitions, not interchangeable standalone numerical primitives.
- `Av1IntraSuperblockEncoder.ModeDecision.cs:166-168` stores coded planar views.
  `Av1EncoderFrame.cs:109-119` assigns those views the aligned dimensions. Consequently
  `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:984-987` includes coded alignment in its model bounds.
  An initial suspicion that this always violates reference clipping was rejected after following
  `av1/encoder/encoder.h:4291-4309`: the reference also uses aligned dimensions when `do_border_pad` is false.
  Its conditional true-frame policy is selected at `av1/encoder/encoder.c:4560-4568` and consumed by
  `av1/encoder/rdopt_utils.h:361-401` and `av1/encoder/model_rd.h:256-303`. That policy is missing here.
  The misleading visible-only comment was corrected; no arithmetic change or numerical-defect claim is justified
  without reconciling that policy and its delta-Q/TPL prerequisites. High-bit-depth SSE rounding in the current
  caller matches `av1/encoder/model_rd.h:70-108`.
- `Av1TileEncoder.cs:294-329` selects each block and writes its entropy data immediately.
  `Av1TileWriter.cs:820-834,1070-1097` consumes the current palette and map, and
  `Av1EncoderSuperblockWorkspace.cs:94-105` clears reusable decisions between superblocks.
  Frame coefficients and EOB/type state survive in `Av1EncoderCoefficientBuffer.cs:32-59,117-150`, but these
  alone cannot reproduce all final block syntax after frame-wide filter selection.
- Reference `av1/encoder/encoder.c:2787-2838,2891-2911,3787-3813` selects/applies deblocking, CDEF,
  and restoration after reconstruction and before final bitstream packing. It preserves restoration boundary
  rows before and after CDEF. Its palette path tokenizes selected maps during coding
  (`av1/encoder/tokenize.c:182-225,264-278`) and packs those retained tokens later
  (`av1/encoder/bitstream.c:1516-1535`). `TokenExtra` occupies one byte per palette sample;
  `av1/encoder/tokenize.h:105-135` and `av1/encoder/encodeframe.c:1413-1430` size and retain that frame storage.
  The managed single-pass lifetime is a controller/ownership deviation that must be resolved together with
  delayed packing. Simply flipping CDEF/restoration flags or saving only probability state is insufficient.
- This comparison ran no benchmark or runtime test and establishes no new numerical or performance result.

Further controller-state findings after `b2aee3036` on 2026-09-05:

- Reference `av1/encoder/block.h:239-259`, `av1/encoder/rdopt.h:315-328`, and
  `av1/encoder/partition_search.c:1555-1556` retain the winning reference-MV stack, weights, count, mode context,
  and global vectors for final packing. `av1/encoder/bitstream.c:1062-1089,1133-1158,1251-1264` consumes that
  retained state for inter-mode, DRL, MV, and IBC symbols. `Av1TileWriter.cs:928-975` currently rebuilds the stack
  at the immediate-write boundary. Delaying the write requires preserving its decision-time state, not assuming
  a rebuild against a completed frame grid is equivalent.
- Reference `av1/common/av1_common_int.h:1775-1856` derives final partition structure from the retained mode grid.
  A second frame-sized partition-tree copy is therefore not required by the reference architecture.
- `Av1SymbolEncoder.cs:424-480,1183-1334` calculates candidate costs from live adaptive distributions.
  Reference `av1/encoder/rd.c:82-130,602-668` fills distinct mode/coefficient cost tables, including marginal
  coefficient costs needed by optimization. `av1/encoder/encodeframe_utils.c:1556-1692` updates mode,
  coefficient, MV, and displacement-vector costs at separately configured superblock/row/tile frequencies;
  `av1/encoder/speed_features.c:2385-2387` starts the inter cost policies at superblock frequency.
  Updating CDFs after each selected block is not the same as refreshing every RD cost after that block.
  This missing cost-state lifecycle is an architectural deviation. Its contribution to time or quality has
  not been measured; no isolated cache or invented effort-dependent refresh policy has been introduced.
- Reference `av1/encoder/rd.c:687-853` initializes the complete cost state and combines control and speed-feature
  update frequencies. `av1/av1_cx_iface.c:391-394,1342-1348` defaults controls to superblock updates and disables
  MV-cost updates for all-intra configuration. These are configured policies, not a public-effort mapping.
  `av1/encoder/encoder_alloc.h:58-88` omits the main MV-cost allocation for all-intra; displacement costs are
  initialized only when needed (`rd.c:843-851`). Worker costs are separately owned when their update policy
  requires independent state (`av1/encoder/ethread.c:1610-1641`). Managed sizing and lifetime must follow those
  usage boundaries rather than adding every table to every frame or candidate.

CDF symbol-cost floor correction, verified after `37541f7f3` on 2026-09-05:

- `Av1ProbabilityCost.cs:47-52` previously passed each CDF interval directly to raw probability conversion.
  Native `av1/encoder/cost.c:30-48` first floors symbol mass at `EC_MIN_PROB` (4, `aom_dsp/entcode.h:21`).
  Raw conversion has a different domain and still permits 1 (`av1/encoder/cost.h:32-42`). Conflating the two
  overcharged sufficiently rare symbols by up to 1,024 rate units, or two bits.
- CDF symbol conversion now applies the existing range-coder minimum before raw conversion. Gathered edge
  partition costs (`Av1SymbolEncoder.cs:969-980,1003-1014`) use that same symbol boundary, matching native
  `av1/encoder/partition_search.c:3419-3449`. Existing raw-probability behavior and tests remain unchanged.
- The new zero-mass middle-interval regression failed before correction: expected 6,656, actual 7,680
  (`symbol-floor-red.trx`). Cases at masses 0 through 4 and 8 exercise both ordinary CDF and gathered-symbol
  conversion. They do not replace the missing RD-cost refresh lifecycle or coefficient optimizer.
- Final Release .NET 11 build: zero errors and 1,009 existing warnings; Roslynk: zero compiler errors.
  Serialized Visual Studio VSTest passes 2,283/2,283 in 27.0448 seconds (`symbol-floor-final.trx`), covering
  entropy, intra-superblock, encoder-frame, and public HEIF encoder tests.
- Optimized libaom decoding was repeated after the final edit for 23 palette, eight partition, and twelve color
  sequence streams: 38,973 samples, maximum error 0, and zero samples exceeding one. Per-plane reports remain
  in the temporary takeover directory. These are bounded same-bitstream checks, not separate-encoder parity,
  a quality improvement claim, or performance acceptance. No benchmark was run.

Frame/block RD and decoder filter follow-up after `aa2ecf690`:

- The two base multiplier formulas and high-bit-depth normalization in `Av1RateDistortion.cs:122-158` match
  native `av1/encoder/rd.c:371-444` for their represented key/ordinary-inter roles with default PSNR tuning.
  They do not implement the surrounding frame-role, layer/boost, tuning, or block adjustments
  (`rd.c:447-463,802-809`, `av1/encoder/partition_search.c:596-658`). In particular, ALLINTRA derives a
  superblock modifier from the range of subblock variances (`partition_search.c:5721-5734`).
  `Av1IntraSuperblockEncoder.ModeDecision.cs:172-176` uses only the segment-zero base quantizer and intra flag.
  These missing policies remain architectural deviations; no isolated multiplier adjustment was introduced.
- The default `disable_trellis_quant = 3` (`av1/av1_cx_iface.c:291`) means
  `NO_ESTIMATE_YRD_TRELLIS_OPT`, not final-pass-only optimization (`speed_features.c:2493-2513`).
  `encodemb.h:157-162` and `tx_search.c:2084-2085,2216-2229` still allow optimization during transform
  candidate evaluation under that default. Control value 2 selects final-pass-only behavior. A winner-only
  optimizer would therefore leave the default production path incomplete.
- The complete optimizer traversal at `av1/encoder/txb_rdopt.c:18-560` and its cost/dequantization helpers
  (`txb_rdopt_utils.h:39-204`) were followed through level lowering, EOB replacement, empty-transform selection,
  signed DC handling, plane/precision/tuning scaling, and joint rate/EOB/context publication. It retains only
  three nonzero positions for the EOB phase, then changes traversal after that phase's bound is exceeded.
  That bound belongs to this complete traversal; it is not a general candidate-pruning threshold.
  Managed `Av1SymbolEncoder.cs:1183-1334` evaluates an unchanged coefficient vector and
  `Av1TransformBlockEncoder.cs:1175-1225` stops at fast quantization. Existing scratch can support parts of the
  arithmetic, but cost-state policy, evaluation stages, mutation, and reconstruction must be integrated together.
- The caller policy also changes quantization, not only the decision to invoke trellis:
  `tx_search.c:1964-2000,2147-2156,2216-2229` derives the MSE/SATD gates from the active evaluation stage,
  switches between fast and regular quantization, then optimizes before distortion/reconstruction.
  Threshold tables and default/mode/winner selection live in `speed_features.c:76-100` and `rd.h:353-381`.
  Managed candidate reconstruction currently happens before `GetCoefficientCost`
  (`Av1TransformBlockEncoder.cs:187-251`, `Av1IntraSuperblockEncoder.ModeDecision.cs:2494-2550`).
  Adding optimization to that later cost call would reconstruct twice or leave candidate pixels stale.
- The optional residual border policy is selected in `encoder.c:4559-4568`: GOOD mode, objective delta-Q,
  TPL enabled, no AQ/segmentation/ROI/QP sweep/ducky path, and sharpness other than three.
  `encodemb.c:80-173` fills outside-visible residuals using a whole-block mean, per-axis mean, or zero,
  depending on transform type. Thus border residual reuse across transform candidates is conditional.
  This policy is not an unconditional replacement for coded-edge replication, and is not enabled for ALLINTRA.
  No isolated border-padding rule was added; its configuration and candidate integration remain open.
- Transform pixel-error normalization and the modeled-rate skip comparison were also traced through
  `Av1TransformBlockEncoder.cs:577-590`, `Av1RateDistortion.cs:259-307`, native
  `av1/encoder/model_rd.h:70-106,162-199`, and `av1/encoder/tx_search.c:979-1051`.
  No additional numerical defect was established in those formulas. The conditional border policy and
  transform-domain/winner evaluation stages remain unresolved as recorded above.
- The complete `Av1CdefDecoder.cs` frame/unit traversal was compared with `av1/common/cdef.c:29-478`:
  unfiltered top/left context, coded-edge sentinels, skipped-block lists, luma direction ownership, and chroma
  reuse are present. The managed sequential traversal reads the still-unmodified bottom row directly; native
  retains bottom lines for its worker-capable traversal. This inspection establishes no decoder-wide or SIMD
  completeness claim. Decoder CDEF storage remains an operation-scoped owner, not native reusable worker state.

Retained-state and cost-policy follow-up after `ef8b1a823`:

Implementation in progress on 2026-09-06: symbol and tile traversal now support separate write and CDF-update
operations. `Av1TileEncoder.cs:239-381` completes block analysis across all tiles before a second traversal
packs their bytes. `Av1TileWriter.BlockEncoding.cs:70-151` derives partitions from the completed mode grid
and loads retained prediction parameters. Palette tokens, selected motion references, and coefficient contexts
have production writers and consumers. `Av1PictureControlSet.cs:127-154` resets entropy edges without clearing
those decisions. Final frame filter selection and the complete reference search/rate controller remain missing.

Allocation evidence for the picture-state extension:

- `av1/encoder/encodeframe.c:1413-1430` allocates palette tokens only when screen-content tools are allowed,
  using `MAX_SB_SIZE_LOG2`, rather than the selected frame superblock size. `tokenize.h:42-46,105-135` stores
  one byte per token and reserves `ceil(width/128) * ceil(height/128) * 128 * 128 * min(2, planeCount)` bytes.
  The managed extension uses this maximum-superblock rounding. Color token storage is 128 KiB
  at 256x256 and 15.9375 MiB at 3840x2160 for color; monochrome requires half those amounts.
- `Av1EncoderPictureBuffer.cs:75-333,368-390` already owns a combined picture-state allocation and disposes it
  on constructor failure and normal disposal. New regions are non-owning slices of that same allocation;
  they add no allocator call, per-candidate rent, or independently disposable owner. All byte-length products
  and sums use checked arithmetic. Existing fixed-geometry sequence reuse retains the allocation across frames.
- Final prediction parameters use a separate eight-byte `Av1EncoderBlockStruct` region; the frequently read
  `Av1MacroBlockModeInfo` entry remains eight bytes. Block palette entries use the existing 50-byte
  `Av1EncoderPaletteInfo` and the existing mode-allocation count.
  Motion contexts use that same count only when motion-vector state is allocated. Native frame extensions use
  mode-allocation geometry too (`encoder_alloc.h:36-57`) and retain four usable candidate entries, weights,
  count, global vectors, and mode context (`block.h:245-259`). The 28-byte compact managed context covers the
  currently implemented single-reference/IBC syntax; compound and complete reference-controller parity remain open.
- Palette token capacity bounds all block tokens because coded blocks partition each superblock, each active
  palette plane emits at most one token per luma sample, and there are at most two palette planes. Token writes
  must stay in their assigned superblock region. Existing coefficient storage uses the same raster superblock
  decomposition (`Av1EncoderCoefficientBuffer.cs:32-59`).

- `Av1EncoderTransformBlockState.cs:12-43` uses four bytes for EOB and byte-sized transform type, leaving
  one padding byte. Reference `av1/encoder/encodetxb.c:593-625,714-730` records the neighboring skip context
  in bits 0-3 and DC-sign context in bits 4-5 beside each EOB. Its packer consumes those retained contexts
  (`encodetxb.c:296-306,410-421`). The managed structure can represent that state without increasing its size,
  and both analysis and packing now use the spare byte. The broader verification run below covers these paths.
- Reference palette-token allocation is conditional on non-statistics coding with screen-content tools allowed
  (`av1/encoder/encodeframe.c:1413-1430`). `tokenize.h:105-135` reserves up to two full-resolution planes in
  maximum-superblock-rounded storage. Tokens retain the selected context and color-order rank, including the
  first raw index (`tokenize.c:174-225,264-278`, `bitstream.c:353-368`); keeping only palette colors is insufficient.
- `Av1EncoderPictureBuffer.Reset` still clears the complete mode grid and packed state between frames.
  The separate `ResetEntropyContexts` boundary preserves retained syntax between analysis and packing.
- Native cost defaults are explicit controls as well as speed features: `av1/av1_cx_iface.c:391-394,550-553`
  differs between default and realtime configurations. `rd.c:724-758,824-851` combines controls with speed policy
  and initializes frame costs; `encodeframe_utils.c:1629-1689` suppresses block refresh when CDF updates are
  disabled. No new managed effort mapping or isolated cost-refresh threshold was introduced.

Verification of the staging change:

- The focused frame/sequence, intra-superblock, coefficient/entropy, and HEIF encoder run passed 2,411 cases
  (`encoder-staging-focused-r3.trx`). Native decoding matched 12 regenerated color sequences (21,348 samples),
  eight partition streams (16,640 samples), and 23 palette streams (985 samples), with maximum error zero and
  zero differing samples. These are same-bitstream/reconstruction checks, not separate-encoder parity.
- The first broad run stopped on the old packed-state allocation-size assertion. The ownership test now checks
  the additional fixed-width regions, exact pointer offsets, conditional allocation, clean storage, and return
  of the same two owners. The existing compact-mode eight-byte assertion remains unchanged.
- All 13 storage/token checks pass (`encoder-staging-storage-tokens-r3.trx`). The new token test poisons the
  color map after analysis, verifies unchanged packed bytes and matching adaptive costs, and compares analysis
  output with an empty range coder. It covers both palette planes with CDF updates enabled and disabled.
- Intermediate failures exposed an unconditional four-entry read from a shorter candidate-weight span and an
  initial expansion of compact mode storage; both production defects were fixed. Test-development failures
  involved size arithmetic and unsupported InlineArray value equality; clean storage now uses byte-span checks.
- Final Release/.NET 11 build: zero errors, 1,009 existing warnings. The broad run passed all 9,379 cases
  (`encoder-staging-production-r2.trx`, 2.6931 minutes), including the final storage and token tests.
  No benchmark or claim of complete codec correctness, reference-controller parity, or performance improvement.

Further filter-controller source comparison on 2026-09-06:

- `Av1LoopFilterBase.cs:75-265` now owns the shared boundary traversal and level arithmetic. Decoder and encoder
  operators supply their existing mode state; both call the same byte/high-bit-depth deblocking operators.
  `Av1LoopFilterEncoder.cs:24-140` consumes the retained encoder grid and component planes without allocating a
  second mode map or copying samples. `Av1TileEncoder` applies the signaled levels after analysis and before
  packing. Automatic level selection is still missing; production frame configuration still leaves levels zero.
- Native `av1/common/av1_loopfilter.c:197-215` selects lossless 4x4, luma block/variable-inter transforms, and
  maximum chroma transforms. The encoder currently retains a uniform luma transform per block. Variable inter
  transform trees and superblock filter-delta encoding remain unresolved capabilities, not new rejection rules.
- The shared decoder traversal passed 26 focused tests (`shared-loop-filter-r1.trx`). Restoration and film-grain
  native reference checks covered 8,500,087 samples with maximum error zero and zero differing samples.
  New encoder reconstruction tests cover 8/10/12-bit monochrome and 4:2:0/4:2:2/4:4:4 at 33x137, distinct
  component/direction levels, sharpness, and reference deltas. Their final verification is still in progress.
- All 12 encoder-filtering cases pass (`encoder-loop-filter-r2.trx`). Optimized native decoding of their
  exported bitstreams matches retained reconstruction byte-for-byte: 102,390 samples, maximum error zero,
  zero differing samples, and zero errors above one. These remain same-bitstream comparisons. Subsequent
  shared-traversal cleanup retains each resolved block mode through level derivation, avoiding another grid
  lookup and mode-structure copy. Final verification after that cleanup passed 138 focused cases
  (`loop-filter-and-sequence-final-r1.trx`, 9.9613 seconds) and all 9,429 explicitly selected AV1, HEIF encoder,
  and sequence-parser cases (`av1-sequence-final-r1.trx`, 2.6884 minutes). The Release .NET 11 build had zero
  errors and 1,009 existing warnings. The 12 regenerated filtering streams again matched native decoding
  exactly across 102,390 samples. This verifies the selected-level application, not automatic level selection.
- The six restoration and nine film-grain reference streams were decoded again with the optimized native
  executable after the final run: 8,500,087 samples, maximum error zero and zero differing samples. The managed
  conformance path uses `Av1ReconstructionConformanceTests.AssertNativePlanesEqual` (`:4053-4164`) and
  `AssertSampleEqual` (`:4187-4284`): it compares every native sample, checks the complete reference length, and
  fails on any unequal value. These assertions use no one-unit tolerance. Native reference-file validation and
  managed comparison to those same files form bounded decoder evidence, not a separate-encoder comparison.
- `av1/encoder/picklpf.c:62-209,348-401` searches filter levels from previous-frame levels, caches per-level SSE,
  applies directional bias and step reduction, restores the complete coded plane after each candidate, and
  searches combined luma before separate luma directions and chroma. The trial buffer is reused by the encoder.
- `picklpf.c:211-347,403-467` also depends on explicit filter controls, tuning/sharpness, speed-selected search
  methods, frame layers, and retained reference levels. Those policies cannot be replaced with a fixed quantizer
  formula or a newly invented effort threshold. `speed_features.c:2545-2548` supplies the full-image search
  defaults; speed-specific overrides still need to be reconciled with the complete encoder configuration.
- `encoder.c:2868-2923,3786-3815` places level selection and deblocking before CDEF/restoration and final
  packing. `picklpf.c:348-367` retains a bordered unfiltered frame and reuses it across trials/frames. This
  owner has not been added yet; the current application stage requires no additional frame buffer.
- `encoder_utils.h:1051-1064` uses 64-pixel borders for non-resized all-intra, selected-superblock-width plus
  32 for inter sequences, and a separate resizing border. Managed `Av1EncoderFrame.cs:28` still fixes the
  border at 64 for both roles. This is an allocation/layout deviation; no performance claim follows from it.
- At this historical checkpoint public Effort remained 0-10 and the configuration question was pending.
  The user subsequently required the native cpu-used setting as an enum; see the active milestone above.

Sequence recovery failure found during broader verification on 2026-09-06:

- `shared-loop-filter-production-r1.trx` stopped after 9,373 passes on
  `HeifSequenceParserTests.DecodeSkipsInvalidAv1SampleWhenImageDataErrorsAreIgnored`. Earlier 9,379-case runs
  selected AV1 plus HEIF encoder tests, not sequence parser tests. They did not establish this behavior.
- The failure preceded codec dispatch: `HeifDecoderCore.FindSequencePrimaryItem` rejected the usable track
  because the synthetic file had no still primary item. Current [libavif track selection](https://github.com/AOMediaCodec/libavif/blob/main/src/read.c#L5754-L5869)
  selects track configuration and samples independently of primary-item availability. The
  [AVIF sequence specification](https://aomediacodec.github.io/av1-avif/#avif-image-sequence-brands) still describes
  MIAF's image-item requirement for conforming files; accepting a usable track is decoder tolerance, not a claim
  that the synthetic file conforms. Production sequence output continues to include its primary image item.
- Identification and decoding now use the first timed sample as the animated root when no still item is
  available. Existing independent primary roots retain their presentation, metadata, and frame behavior.
  No expected output or failure assertion was weakened. The existing recovery test now also checks every
  retained RGBA pixel against the valid standalone image and asserts animated-root metadata.
- The initial targeted parser/encoder run passed 119 cases (`sequence-primary-recovery-r1.trx`). After the final
  pixel assertions, both the 138-case focused run and the 9,429-case AV1/encoder/sequence-parser run passed.
  The recovery regression now establishes the retained pixels and animated-root metadata as well as frame count.

Motion-controller follow-up:

- `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:1275-1412` bounds an effort-scaled staged search by
  a fixed 64-pixel border. `:1426-1496` uses prediction SSE and full mode/vector RD cost for both integer and
  fractional search. Native `mcomp.c:72-240,317-386` carries frame-relative MV limits, search sites, mesh policy,
  separate SAD/error-per-bit scaling, and subpixel stop/iteration policy. Enlarging the allocation alone would
  not correct that controller. The active milestone now includes the allocation correction as a dependency;
  the complete controller replacement remains required.
- Roslyn references for `Av1ForwardTransformer.GetHadamard8x8Cost` currently resolve only to two tests. The
  retained primitive is not on the production search path; its tests do not establish encoder pruning behavior.

Mode and transform controller follow-up on 2026-09-06:

- Current `Av1IntraSuperblockEncoder.ModeDecision.cs:613-625,708-726,743-753` completes luma and chroma intra
  selection before inter evaluation. Native `rdopt.c:6196-6333` evaluates ordered inter candidates while retaining
  reference costs, skip costs, prediction SSE, motion candidates, and estimated transform results. It then runs
  motion refinement and deferred transform search (`:6338-6358`), gates and evaluates intra (`:6367-6375`), and
  refines winners (`:6383-6389`). Reordering one call without those retained dependencies would not reproduce
  this controller.
- Native `rdopt.c:3746-3892` restores each winner's mode and references, selects winner-stage transform policy,
  recomputes luma/chroma transforms, revisits skip, replaces the old transform rates, and retains an improved
  result. Managed `EncodeBlock` has no corresponding refinement phase. This is missing functionality, separate
  from numerical defects in individual transforms.
- `Av1TransformBlockEncoder.cs:1175-1225` always ends lossy coefficient decisions at fast quantization. Native
  `tx_search.c:2084-2191,2216-2235` selects trellis by segment, evaluation stage, normalized residual MSE and
  transform SATD, then optimizes before distortion evaluation and coefficient-rate pruning. Final intra encoding
  also optimizes before inverse reconstruction (`encodemb.c:825-886`) and only then publishes entropy context
  (`:912-945`). Adding an optimizer after managed reconstruction or packing would be the wrong integration point.
- Native `rdopt_utils.h:607-709` distinguishes default, candidate, and winner evaluation, changes transform and
  coefficient policy together, and invalidates cached RD records when the stage changes. `speed_features.c:2810-2846`
  derives those staged parameters from the profile. GOOD speed zero already enables partition, reference, and
  transform pruning (`:1113-1172`); full enumeration is not its baseline architecture.
- GOOD's filter selection remains full-image at baseline, uses coarse inter search from cpu-used 3
  (`speed_features.c:1364-1365`), and disables independent luma-direction search from 4 (`:1425`). ALLINTRA's
  later quantizer-based choice is a different profile decision. Public Effort mapping remains unresolved.

Entropy-cost and frame-controller source trace on 2026-09-06:

- `Av1SymbolEncoder.cs:1444-1595` measures coefficients directly from its live distributions. Native
  `rd.c:602-684` fills coefficient snapshots, including base-level decrement costs and cumulative base-range
  costs plus decrement deltas. `txb_rdopt_utils.h:112-162,226-238` uses those deltas when comparing a coefficient
  with its one-level reduction. A copied probability graph or an optimizer attached after reconstruction would
  not reproduce this cost architecture.
- Native mode snapshots cover partition, luma/chroma, palette, transform, angle, segment, reference, and motion
  syntax together (`rd.c:82-322`). Frame initialization reconciles codec controls with speed policy and fills
  the required cost families (`:723-851`); superblock refresh respects disabled adaptation and each family's
  update boundary (`encodeframe_utils.c:1621-1692`). The managed live-CDF queries have no equivalent boundary.
- Current still-frame ownership is in `Av1FrameEncoder.cs:604-775`; sequence ownership is in `:1493-1921`.
  Both call the tile encoder through `:977-1049`, which currently contains analysis, selected deblocking, and
  packing. The sequence then extends borders and swaps reconstruction/reference owners. The existing source,
  reconstruction, coefficient, picture, and operation workspaces must be retained when adding the missing
  frame-level decisions; replacing them with decoder state or extra frame copies is not required.
- Before the sparse follow-up below, the decoder inverse factory forwarded every non-DC lossy case into full transforms
  (`Av1InverseTransformerFactory.cs:47-60,98-112`). The 256-bit driver uses eight Int32 lanes and traverses the
  complete height, including the uncoded half of 64-point inputs (`Av1Inverse2dTransformer.cs:385-524`).
  Native low-bit-depth AVX2 selects separate sparse row/column functions and limits transformed row batches from
  EOB bounds (`av1/common/x86/av1_inv_txfm_avx2.c:1611-1708`;
  `av1_inv_txfm_ssse3.h:171-220`). Default, horizontal-identity, and vertical-identity scans have different
  bounds. This is an established dispatch/traversal gap; its timing contribution is unmeasured on the current tree.

Sparse inverse-transform follow-up on 2026-09-06:

- Native high-bit-depth dispatch selects separate axis kernels for one, eight, sixteen, and thirty-two supported
  inputs (`av1/common/x86/highbd_inv_txfm_avx2.c:4081-4179`). Four-point axes fall back to their complete
  narrower kernels (`:4214-4230`; `highbd_inv_txfm_sse4.c:5503-5715`). High-bit-depth mixed identity paths
  retain a complete identity axis and use conservative support on the other axis (`:5226-5361`); the low-bit-depth
  paths use the row/column scan bounds in both dimensions (`av1_inv_txfm_avx2.c:1796-1889`).
- All 36 stored managed coefficient-scan arrays were compared with their native arrays after converting native
  column-major indices into managed raster indices: 5,936 entries, zero differences. Native diagonal support
  tables and identity bounds are in `av1_inv_txfm_ssse3.h:96-219`. The larger-transform matrix-scan aliases in
  the managed dispatch table occur outside the legal transform sets; they are not evidence of a reachable decoder
  numerical defect. `Av1ScanOrder.InverseScan` currently has no production references.
- `Av1Transform2dFlipConfiguration.cs:382-502` now carries EOB-derived support bounds into the factory dispatch.
  `Av1Inverse2dTransformer.cs:167-458` selects closed generic sparse DCT/ADST operators for both axes.
  Thirteen operator variants cover DCT lengths 8/16/32/64 and ADST lengths 8/16, including the coded 32-input
  network for 64-point DCTs. Zero branches are removed while preserving surviving rotations, rounding, and
  stage clamps. DC DCT variants retain their cosine scaling and uniform inverse-stage clamp.
- Each operator declares its input prefix in the existing operator interface. The scalar, 128-bit, and 256-bit
  traversals (`Av1Inverse2dTransformer.cs:515-896`) process only potentially nonzero row batches, initialize every
  input consumed by a complete second-axis operator, and continue writing the entire active destination.
  Existing caller-owned workspace and independently strided prediction/output contracts are retained.
- Release .NET 11 build after the final relevant edit: zero errors and 1,009 existing warnings. The focused
  inverse-transform suite passed 996/996 cases (`sparse-transform-focused-r1.trx`). New checks cover every
  permitted scan prefix, sparse scalar/128-bit/256-bit operators against complete scalar arithmetic, poisoned
  unused inputs and scratch, and production reconstruction at EOB support transitions. These are managed
  arithmetic and dispatch checks. The final native-backed reconstruction suite passed 93/93 cases
  (`sparse-transform-reconstruction-r1.trx`). These fixtures cover the existing production reconstruction corpus;
  they do not independently enumerate every sparse arithmetic input or establish encoder parity.
- Remaining architecture gaps include packed 16-bit low-bit-depth arithmetic, native intermediate layout/sizing,
  and further SIMD coverage. The existing full raster workspace is not the native compact intermediate layout.
  No performance improvement or complete decoder/encoder parity is claimed, and no benchmark has been run.

Inverse-transform storage follow-up on 2026-09-06:

- The decoder previously reserved the forward-transform maximum: 11,520 Int32 values, or 45 KiB.
  `Av1TransformWorkspace.cs:49-64` now distinguishes inverse storage: two axis vectors and one raster,
  with a maximum of 5,120 Int32 values, or 20 KiB. This removes 25 KiB from each block decoder's
  existing contiguous workspace owner; it adds no owner or allocator request.
- All 75 inverse scalar/128-bit/256-bit operator overloads consume their input before writing stage scratch.
  The traversal now aliases those lifetimes and sizes vectors from the active dimensions
  (`Av1Inverse2dTransformer.cs:533-544,698-709,842-853`). Native high-bit-depth traversal also uses
  in-place axis storage with local kernel stages (`av1/common/x86/highbd_inv_txfm_avx2.c:4112,4138-4146`).
  The managed raster layout and Int32 low-bit-depth arithmetic still differ; the allocation reduction is
  not a claim of complete native storage or SIMD parity.
- Operator parity checks now exercise aliased input/stage storage, and the production sparse reconstruction
  checks use the exact inverse workspace requirement. The owner test checks its reduced exact size.
  After the final edit, Release .NET 11 built with zero errors and 1,009 existing warnings; Roslynk reported
  zero compiler errors. The focused inverse/owner run passed 1,000/1,000 in 6.2844 seconds
  (`inverse-storage-focused-r1.trx`). The broader AV1, HEIF encoder, and sequence-parser run then passed
  9,908/9,908 in 2.7773 minutes (`inverse-storage-production-r1.trx`), including the reconstruction fixtures.
  These are correctness checks, not codec timing measurements or separate-encoder acceptance.

Film-grain presentation storage follow-up on 2026-09-06:

- The two presentation-copy branches previously allocated the full 288-sample prediction border
  (`Av1Decoder.cs:899-906,952-959` before this change). Native output creation requests even visible dimensions,
  no border, and 16-byte row alignment (`av1/av1_dx_iface.c:768-795`; `aom/src/aom_image.c:128-186,217-224`).
  The synthesized output is separate from the ungrained reference. This is a demonstrated allocation-role
  deviation, not an unmeasured claim that a particular SIMD kernel is faster.
- `Av1FrameBuffer.cs:255-259,562-612` now creates a presentation layout with those dimensions and row strides
  in the existing single owner. Monochrome continues to omit unused chroma planes. The byte count describes
  managed plane storage, not native allocator alignment overhead or total process memory. For a 100x60
  eight-bit 4:2:0 presentation, the calculated plane storage changes from 660 KiB to 9.84375 KiB.
- `CopyVisibleTo` preserves destination origins, strides, and allocation capacity (`Av1FrameBuffer.cs:276-325`)
  while copying the active picture. Both newly decoded and shown-existing grained frames use the compact
  layout; retained references keep their original bordered storage. Color conversion consumes independently
  located rows through `Av1PlanarSampleBuffer.cs:100-131`, so no additional pixel-conversion buffer was added.
- Removing the border exposed empty overlap rectangles that the old padding had made addressable.
  The new overlap-edge regression failed with an out-of-range span at `Av1FilmGrainDecoder.cs:525`
  (`grain-presentation-overlap-red.trx`). Native calls carry zero height/width for these regions and its sample
  loops do no work (`grain_synthesis.c:1133-1166,1252-1305`). The managed region owner now forms sample spans
  only for nonempty vertical strips/interiors, while preserving all boundary blending and outgoing state.
- Tests cover exact owner size/return, separate origins and strides, untouched padding, and 144 grain cases
  at clipped overlap edges across 8/10/12-bit monochrome and 4:2:0/4:2:2/4:4:4. These compare compact output
  with bordered managed output; they are not independent native vectors for every edge case. The reference
  ownership test now compares actual visible samples instead of comparing differently sized whole buffers.
- After the final relevant edit, Release .NET 11 built with zero errors and 1,009 existing warnings;
  Roslynk reported zero compiler errors. The focused frame/reference run passed 55/55 in 3.0082 seconds
  (`grain-presentation-overlap-fixed.trx`). The AV1, HEIF encoder, and sequence-parser run passed 9,932/9,932
  in 2.8026 minutes (`grain-presentation-production-final.trx`), including the existing exact native-plane
  grain fixtures and hardware fallbacks. Their retained references were previously verified against the same
  optimized native revision: 3,113,847 samples, maximum error 0, zero nonzero errors. No new native arithmetic
  result, codec timing, or separate-encoder parity is claimed. No benchmark was run.

Active-frame allocation validation follow-up on 2026-09-06:

- The decoder rejected a valid small frame when its sequence maximum would require an oversized contiguous
  allocation. `Av1Decoder.ValidateSequence` and the frame-buffer constructor both validated sequence maxima
  even though `ReadTile` already allocates the active upscaled width and height. Native ordinary frame setup
  reads the size override and allocates that active frame (`av1/decoder/decodeframe.c:1995-2049`). The separate
  maximum-sized neutral-reference allocation at `:4854-4904` is corruption recovery, not ordinary frame setup.
- The regression preserves the original 4x4 Orange fixture's tile bytes and writes a full sequence header
  declaring 65,536x65,536 maxima with an explicit 4x4 frame override. Before correction it failed at the
  sequence-capacity check (`active-frame-size-red.trx`). The optimized native decoder accepts both the original
  and expanded-maximum payloads and produces identical output: 24 samples per payload, maximum error 0,
  zero differing samples and bytes. The reference build has `CONFIG_SIZE_LIMIT=0`.
- `Av1FrameBuffer.cs:231-250` now validates the requested allocation dimensions. `Av1Decoder.ReadTile:767-774`
  performs the same active-frame capacity check before allocating syntax state. Sequence-only validation
  continues to reconcile codec configuration and color declarations. Requests that actually exceed the
  contiguous allocation limit remain rejected; no allocation limit or malformed-input assertion was weakened.
- Final Release .NET 11 build: zero errors and 1,009 existing warnings; Roslynk: zero compiler errors.
  Header/lifecycle/frame-buffer verification passed 80/80 in 2.9384 seconds (`active-frame-size-focused.trx`).
  The final reconstruction suite passed 93/93 in 1.9015 minutes (`active-frame-size-reconstruction.trx`).
  Fresh optimized-native comparison matches the corrected managed output for both payloads on every byte;
  per-plane counts are in temporary `active-frame-size-comparison.json`. This is bounded decoder evidence,
  not separate-encoder parity or a performance result.

Restoration boundary ownership and precision follow-up on 2026-09-06:

- Architectural deviation: boundary rows were allocated in each frame's completion operation and always
  widened to ushort. Native `av1/common/alloccommon.c:299-348` retains above/below buffers in decoder state,
  allocates all sequence planes when restoration is used, aligns each row to 32 samples after adding four
  samples on each horizontal side, and replaces owners when their physical byte length changes. Stripe
  allocation uses the mode-info-aligned luma height for every plane. Native decoder destruction releases
  these buffers (`av1/av1_dx_iface.c:122-137`, `av1/common/alloccommon.c:350-366`).
- `Av1LoopRestorationBoundary.cs:81-149` now owns those buffers for the decoder session, with physical byte
  storage at both sample precisions and the corresponding aligned layout. The frame/header arguments are
  borrowed by each save operation; no reconstructed frame or header remains referenced by boundary storage.
  Its save/copy methods at `:360-445` preserve byte or ushort samples directly, including super-resolution,
  and replicate horizontal context. The allocation-failure path retains successfully acquired owners for
  normal session disposal. `Av1Decoder` owns/disposes this object and passes it through `Av1FrameDecoder`.
- Fourteen focused cases passed in 2.6187 seconds (`restoration-boundary-focused.trx`): all bit-depth/chroma
  combinations, exact contents across multiple stripes, same-size reuse, growth/shrink replacement, and
  exactly-once returns after initial/replacement allocation failure. Release .NET 11 build: zero errors,
  1,009 existing warnings; Roslynk: zero compiler errors. Reconstruction and ownership verification passed
  162/162 in 1.8952 minutes (`restoration-boundary-reconstruction.trx`). A further independent-plane regression
  reuses one decoder across 8/8/10/12/8-bit fixtures, exercising unchanged size, changed size, changed precision
  at identical byte size, and return to byte samples. After that final test edit, the Release build again had
  zero errors and 1,009 warnings; all 15 focused boundary/sequence cases passed in 3.6690 seconds
  (`restoration-boundary-sequence-final.trx`). Fresh optimized native decoding matches the six retained
  restoration/super-resolution fixtures: 5,386,240 samples, maximum error 0, zero nonzero errors and zero
  errors above one. Managed tests independently compare current output to those same native reference planes.
- At this checkpoint, `Av1LoopRestorationDecoder.DecodePlane:102-233` still rented a per-plane output and
  filter scratch, copied each bordered processing block into ushort storage, and narrowed filtered 8-bit
  output. The later physical-sample correction below replaces those block copies. Native
  `restoration.c:252-383` temporarily replaces and restores stripe
  boundary rows in the source. Its destination frame persists with a 32-sample border
  (`restoration.c:1069-1138`, `restoration.h:30`); its shared filter scratch and line-save buffers persist
  (`alloccommon.c:299-310`). Correcting boundary ownership does not resolve these other paths.
- Native self-guided scratch is not just its two output arrays: `restoration.c:718-862` also uses local
  source and intermediate arrays, while `:863-898` places its two outputs 161,588 integers apart.
  `restoration.h:74-92` reserves about 1.233 MiB for those two outputs despite processing 64-sample chunks.
  The optimized AVX2 path differs from scalar scratch ownership: `x86/selfguided_avx2.c:547-637` allocates
  four aligned intermediate/integral planes per filter call and frees them afterward; the coefficient and
  integral phases correspond to managed `Av1SelfGuidedFilter.Operations.cs:33-117`. Managed scratch combines
  these with the two outputs in caller-owned storage sized to the actual processing block. Their complete
  layouts and lifetimes must remain distinct in the comparison. No performance benefit has been measured,
  and no benchmark was run.

Wiener traversal follow-up on 2026-09-06:

- SIMD coverage/architecture finding: the previous `Av1WienerFilter` reduced an eight-tap SIMD dot product
  to one horizontal output and performed every vertical dot product scalarly. Native optimized filtering
  processes independent columns together in both passes (`av1/common/x86/highbd_wiener_convolve_avx2.c:28-245`,
  `av1/common/x86/wiener_convolve_avx2.c:43-242`). Native scalar equations and intermediate clipping were
  traced in `av1/common/convolve.c:1350-1519`. No numerical defect was established in the prior arithmetic.
- `Av1WienerFilter.cs:57-116` now sends both passes through one closed generic traversal
  (`Av1WienerFilter.Operator.cs:172-329`). The semantic `WienerOperator` at `.WienerOperator.cs:17-162`
  implements symmetric tap arithmetic at scalar/128/256/512 widths. The driver owns row/column access,
  complete vector bounds, width selection, coefficient broadcasts, and one scalar remainder.
  The operator widens before symmetric additions and retains signed 32-bit sums through rounding/clipping.
- The implicit center contribution is incorporated into each kernel before filtering. First-pass bias,
  intermediate clipping, the 12-bit first-pass shift of five, and final bias removal remain at their
  original stages. The source and intermediate layouts and requested scratch size are unchanged.
  Portable lane order remains sequential; it does not reproduce x86-specific lane permutations.
  Wider dispatch is not evidence of an end-to-end performance improvement.
- New focused verification uses a separate complete eight-slot, 64-bit scalar calculation across legal
  coefficient extrema, mixed signs, both pass orderings, 8/10/12-bit precision, vector boundary widths,
  odd heights, clipping, output padding and scratch sentinels. The first focused run failed its explicit
  intermediate-clipping coverage assertion: random/checkerboard inputs had not reached that boundary.
  No compared sample had mismatched before the assertion. The failing remote test process subsequently
  timed out after 60 seconds (`wiener-vector-focused.trx`); its failure-reporting log also contains an
  access-denied message. The identified child process IDs were absent after the failed test completed.
  The test now adds sparse impulses and inverse impulses to force both clipping endpoints and runs the
  matrix in the VSTest host before the restricted hardware child runs. No assertion or expected output was
  weakened. The corrected matrix passed (`wiener-vector-coverage-fixed.trx`, 3.2988 seconds total test time).
  Release .NET 11 build: zero errors, 1,009 existing warnings. Final reconstruction/ownership/row-filter
  verification passed 164/164 in 1.8839 minutes (`wiener-vector-reconstruction.trx`), including the six
  restoration/super-resolution streams compared to byte-exact native planes. Their fresh optimized-native
  agreement remains 5,386,240 samples with maximum error 0 and zero differing samples. The filter matrix
  runs the current hardware path before restricted hardware configurations and scalar fallback; unavailable
  host instruction sets cannot be established by feature-disable runs. No performance comparison was run.
  At this checkpoint, byte-source integration, restoration output/scratch lifetime, and per-block widening
  remained open. The subsequent correction below addresses sample storage and stripe dataflow; this
  traversal verification does not establish encoder parity.

Restoration physical-sample and stripe dataflow correction on 2026-09-06:

- Architectural deviation: the prior controller materialized every bordered processing block as ushort
  samples, then copied/narrowed each result into a per-plane output. Native
  `av1/common/restoration.c:252-383,985-1051` filters the original physical sample storage after saving and
  replacing only three context rows above/below each stripe, then restores those rows before another unit
  reads them. The fixed six-row save area is 4.594 KiB
  (`av1/common/restoration.h:211-219`), irrespective of sample precision.
- `Av1LoopRestorationDecoder.cs:77-110` now selects byte/ushort once for the complete frame traversal.
  `:120-236` extends the existing reconstruction border, allocates output at physical sample precision,
  and copies the completed plane back after all units consume its original samples.
  `:259-402` saves/replaces/restores the bounded stripe context and gives both filters direct strided
  views of the source and destination. It no longer materializes bordered blocks or widens frame rows.
  `Av1LoopRestorationBoundary.cs:354-365` exposes the saved rows including horizontal context.
- Both filter families retain their existing arithmetic scales and scratch representation while carrying
  the selected physical sample type through source reads and output writes. Shared register operations in
  `Av1RestorationSampleOperations.cs:20-253` load only the samples owned by each batch, widen unsigned values
  in registers, and narrow only after clipping. No numerical mismatch had been established in the prior
  ushort arithmetic; this corrects storage and traversal rather than changing the filter equations.
- Release .NET 11 build: zero errors, 1,009 existing warnings, 31.83 seconds. Independent Wiener full-kernel
  and self-guided direct-window tests now include byte-backed sources/destinations, row padding and outer
  sentinels. Both passed with their available hardware and scalar configurations
  (`restoration-physical-kernels.trx`, 4.0156 seconds). Final reconstruction, ownership, and filter
  verification passed 165/165 in 1.9481 minutes (`restoration-physical-reconstruction.trx`). The six
  restoration/super-resolution native-plane fixtures are byte exact with the edited managed decoder.
  Fresh optimized libaom decoding independently matches those retained planes across 5,386,240 samples:
  maximum error 0, nonzero errors 0, errors above one 0 (`restoration-comparison.json`).
- At this checkpoint, output, line-save, and arithmetic workspace still rented per active plane. The later
  output-frame correction below addresses destination lifetime/layout; scratch remains unresolved against
  `alloccommon.c:299-310`. The distinct
  optimized self-guided temporary allocations described above must not be conflated with its shared output
  workspace. No performance comparison or encoder parity claim follows from this change.

Restoration output-frame lifetime follow-up on 2026-09-06:

- Architectural deviation: restored output was rented and returned for every active plane. Native
  `av1/common/restoration.c:1069-1138` retains the destination frame in decoder state, uses a 32-sample
  border, and copies only restored planes back after all filtering. Its allocation grows only when the
  required byte capacity increases; new storage is cleared, while reuse does not clear or copy previous
  samples (`aom_scale/generic/yv12config.c:59-267`).
- `Av1FrameBuffer.cs:281-332` now creates/resizes restoration output using its existing single-owner
  plane abstraction. The shared layout calculation at `:628-685` retains eight-sample coded alignment,
  32-sample luma row alignment, subsampled chroma strides, and the restoration border. Growth releases the
  previous allocation before renting the new one. A failed rent leaves the target empty and recoverable
  on a subsequent resize; no second complete output frame is retained during growth.
- `Av1Decoder` owns this output independently of reference/presentation frames and disposes it with the
  session. `Av1FrameDecoder.CompleteFrame` prepares it after super-resolution and before restoration.
  `Av1LoopRestorationDecoder.DecodeFrame:101-139` publishes only active restored planes after all units,
  preserving its source until filtering finishes. The output no longer rents per plane.
- Monochrome continues to allocate only luma through the established managed frame abstraction; the native
  generic destination allocator reserves chroma even in this case. No unused monochrome chroma owner was
  introduced. This is an explicit sizing difference, not an assertion that allocation layouts are identical.
  Line-save and filter arithmetic scratch still need their separate lifetime comparison.
- Two Release builds stopped on StyleCop enum ordering before tests: first an enum after methods, then a
  constructor after the enum. The enum is now between constructors and properties; Roslyn's analyzer pass
  reports zero errors. Fourteen focused layout/ownership/sequence-change cases passed
  (`restoration-output-ownership.trx`, 3.6731 seconds), including unchanged-layout view reuse, growth,
  shrinkage, byte/ushort transitions, clean new allocations, preserved reused storage, and recovery after
  allocation failure with exactly-once returns. Two new assertion-style warnings were corrected without
  changing their checks. Final Release .NET 11 build: zero errors, 1,009 existing warnings, 13.67 seconds.
  Broader reconstruction/ownership/filter verification after that final edit passed 178/178 in
  1.9329 minutes (`restoration-output-final.trx`), including byte-exact native restoration fixtures.

Restoration stripe-save lifetime follow-up on 2026-09-06:

- Native `av1/common/alloccommon.c:299-310,350-366` retains one line-save allocation for the decoder session.
  `restoration.h:211-219` specifies six rows of 392 ushort slots, 4.594 KiB total, even for byte samples.
  The managed controller previously included that storage in every active plane's temporary Wiener owner.
- `Av1LoopRestorationBoundary` now owns that fixed save area alongside its preserved boundary rows,
  initializes ownership before saving deblocked rows, shares it with every restoration plane, and returns it
  at session disposal. The per-plane Wiener owner now contains only its convolution intermediate.
- Existing exact allocation/content tests include the additional session owner, its fixed size, and reuse
  across dimension changes. Failure cases now target the first save-area allocation and the initial/replacement
  below-row allocations after it. Exactly-once return assertions remain in place. Release .NET 11 build:
  zero errors, 1,009 existing warnings, 33.80 seconds. All 51 frame-buffer and sequence-change cases passed
  (`restoration-save-ownership.trx`, 3.8032 seconds). Final reconstruction/ownership/filter verification
  passed 179/179 (`restoration-save-final.trx`, 1.9193 minutes).

Self-guided work-row alignment correction on 2026-09-06:

- The managed integral/coefficient rows reserved multiples of sixteen integers, although the widest
  implemented arithmetic batch contains eight. The optimized reference uses eight-integer row alignment
  after its border and row-separation columns (`av1/common/x86/selfguided_avx2.c:547-637`).
  `Av1SelfGuidedFilter.GetBufferStride` now retains that eight-lane alignment. At 64x64 this changes the
  combined managed scratch request from 138.5 KiB to 129.625 KiB. The native shared output and per-call
  temporary allocations remain distinct from this combined managed workspace; their lifetimes are unresolved.
- The independent direct-window matrix now includes 32x32 and 64x64 processing blocks and poisons scratch
  inside two outer sentinels for both physical sample types. An initial Release build stopped on SA1515
  because a comment followed an expression-bodied method declaration without the required spacing.
  The explanation now sits inside the method body. The corrected Release .NET 11 build passed with zero
  errors and 1,009 existing warnings (35.64 seconds). The expanded filter matrix passed
  (`restoration-alignment-kernel.trx`, 3.2735 seconds), followed by 179/179 reconstruction/ownership/filter
  cases (`restoration-alignment-final.trx`, 1.9455 minutes). No benchmark or encoder parity check was run.

Additional production-path source checks on 2026-09-06:

- Restoration stage lifetime follow-up: `Av1LoopRestorationDecoder.cs:26-139` now follows the existing
  session-owned CDEF stage pattern. It owns the restored output and grows/reuses the two managed arithmetic
  workspaces; borrowed source/header/unit state stays in call parameters. `Av1FrameDecoder.CompleteFrame`
  invokes the retained stage, and `Av1Decoder` disposes it with the session. Native persistent self-guided
  outputs are allocated in `av1/common/alloccommon.c:299-305` and freed at `:350-366`. Native AVX2 temporary
  integral/coefficient allocations (`x86/selfguided_avx2.c:547-637`) and Wiener stack intermediates
  (`x86/wiener_convolve_avx2.c:43-242`, `x86/highbd_wiener_convolve_avx2.c:28-245`) remain a separate layout
  and lifetime difference: managed code retains these bounded workspaces to avoid block-level rents and
  oversized stack storage. This is not a claim of identical native allocation layout or measured speed.
  The sequence regression first failed its early-return assertion (`restoration-scratch-before.trx`).
  After correction it requires one retained 4,544-ushort workspace and one 33,184-integer workspace across
  all five 8/8/10/12/8-bit frames, byte-exact native-plane output, and exactly-once disposal. Six additional
  cases reject each initial/growing output or scratch rent and verify retry, unchanged-input copying,
  steady-capacity reuse, and balanced returns. All seven passed (`restoration-scratch-failure.trx`, 3.5712
  seconds); final reconstruction/ownership/filter verification passed 185/185 (`restoration-scratch-final.trx`,
  1.9700 minutes). Final Release .NET 11 build: zero errors, 1,009 existing warnings, 8.39 seconds.
  Roslynk reports zero compiler errors. No benchmark or separate-encoder parity check was run.

- Frame-header serialization is already after analysis: both `Av1TileEncoder` sample-type constructors complete
  `Encode` before `Av1FrameEncoder` calls the OBU writer; `GetTileData:239-244` only exposes retained bytes.
  The fact that `ObuWriter.WriteFrameObu:116-142` writes header scratch before querying tile lengths therefore
  does not establish another ordering defect. Automatic filter selection remains missing as documented above.
- Tile-list decoding remains missing functionality. `ObuReader.cs:388-391` rejects it, while native
  `av1/decoder/obu.c:479-591,1073-1092` supports its camera-header/external-reference path when normal-only
  tile mode is disabled. The optimized reference has `CONFIG_NORMAL_TILE_MODE=0`. The existing rejection test
  proves the current restriction, not implementation of that mode; its container/API integration is unresolved.

CDEF controller and storage follow-up on 2026-09-06:

- Encoder CDEF remains missing. `Av1FrameEncoder.cs:372-408` disables it, and `Av1TileEncoder.cs:261-270`
  currently runs only selected deblocking between analysis and packing. Native `encoder.c:2777-2825` preserves
  restoration boundaries, searches CDEF, applies it, performs super-resolution, preserves the later boundaries,
  and searches restoration. Enabling the sequence flag or calling a filter primitive does not implement that path.
- The complete CDEF search was read in `av1/encoder/pickcdef.c` and `pickcdef.h`. Search excludes wholly skipped
  units and groups 128-wide/high blocks (`pickcdef.h:173-218`, `pickcdef.c:523-647`). Distortion covers only the
  listed non-skipped blocks; high-bit-depth squared error is shifted after accumulation
  (`pickcdef.c:236-261,397-518`). Luma directions are reused across strengths and chroma
  (`:543-617`). Joint strength selection includes per-unit index bits and frame strength literals; distortion is
  scaled by 16 before RD comparison (`:897-973`), followed by per-unit selections, adaptive decisions, and strength
  remapping (`:976-1099`). These dependencies remain absent from production encoding.
- Search distortion arrays and unit indices are allocated per search and freed afterward
  (`pickcdef.c:655-680,1099`), while the search-context object persists (`:891-894`). Application buffers have a
  different lifetime: `av1/common/alloccommon.c:192-278` retains them, replaces changed sizes, and releases them
  when CDEF is disabled. The decoder invokes that boundary after its final tile (`decodeframe.c:5396-5402`).
  These two allocation lifetimes must not be conflated when integrating encoder search and filtering.
- The managed application stage previously rented its entire scratch region inside each `DecodeFrame`
  (`Av1CdefDecoder.cs:145-157` before this edit). It now belongs to `Av1Decoder` and accepts frame inputs
  per call, retaining no frame references. Equal storage requirements reuse the owner, resizing returns the old
  allocation, and disabling CDEF or disposing the decoder releases it. Filter arithmetic and traversal are unchanged.
- Remaining application differences are explicit: managed scratch still uses a 68-sample stride and two-column
  border, two saved top-row slots, and block-local sample-width dispatch. Native application uses its aligned
  source/line/column layouts and a separately captured bottom border
  (`av1/common/cdef.c:161-253,369-421`; `alloccommon.c:209-231`). This lifetime correction does not establish
  matching sizing, complete shared encoder/decoder traversal, SIMD coverage, or a measured timing improvement.
- The production reuse test decodes 8-bit 4:2:0 twice, then 10/12-bit 4:4:4, then returns to the smaller frame.
  It checks live allocator identities, exact-once returns on resize/disposal, and every sample against native
  references. All 11 focused cases passed after the final edit (`cdef-lifetime-focused-r1.trx`, 13.4479 seconds).
  Release .NET 11 compiled with zero errors and 1,009 existing warnings. The final wider reconstruction run passed
  all 93 cases (`cdef-lifetime-reconstruction-r1.trx`, 1.8393 minutes), including reference frames, restoration,
  film grain, intrinsic fallbacks, and constrained allocators. Roslynk reports zero compiler errors.
- Fresh optimized-native decoding of the three CDEF streams matches 3,219,456 reference samples exactly:
  maximum error zero, zero differing samples, zero exceeding one, and zero differing bytes. The comparison script,
  output, and `cdef-comparison.json` remain outside the repository. No benchmark or encoder-parity claim follows.
- The remaining quantizer-dependent profile policy was read in `speed_features.c:2892-3134`. It changes motion
  search, partition eligibility, coefficient optimization, winner transforms, and restoration-unit size using frame
  role, dimensions, screen-content state, and qindex together. Public Effort mapping is still unresolved; copying
  any one threshold would not establish the required encoder policy.

Decoder intra-block-copy traversal correction on 2026-09-06:

- Before this edit, `Av1BlockDecoder.cs:1211-1277` rebuilt the source coordinates and dispatched prediction for
  every transform. Native `av1/decoder/decodeframe.c:681-693,852-879,971-973` predicts the complete block planes
  before residual traversal. Plane dimensions are at least four samples (`av1/common/av1_common_int.h:1343-1355`);
  displacement validity covers the full source rectangle and decoded-region delay
  (`av1/common/mvref_common.h:279-337`). This establishes the traversal boundary without a timing hypothesis.
- Prediction now runs once for each participating block plane, before the transform loop, using the existing
  byte/high-bit-depth predictor and frame spans. Transform reconstruction and CfL publication keep their order.
  No new owner, buffer, copy of a frame, search policy, or rejection rule was introduced.
- The native-fixture regression now requires actual IBC blocks with multiple luma transforms at every tested
  precision, in addition to comparing every sample. All 25 focused cases passed after the final edit
  (`ibc-plane-prediction-r1.trx`, 12.6576 seconds), including half-sample chroma production tests and the official
  extreme-displacement sequence. Release .NET 11 compiled with zero errors and 1,009 existing test warnings.
- Fresh optimized-native comparison of the three 512x256 4:4:4 streams and the two-frame official sequence
  matched all 7,400,448 retained reference samples: maximum error zero, zero differing samples, and zero differing
  bytes. The local comparison script, extracted payloads, and report are outside the repository. Final wider
  reconstruction verification passed all 92 cases (`ibc-plane-reconstruction-r1.trx`, 1.8239 minutes), including
  native precision, presentation, intrinsic tiers, sequence references, and constrained allocator cases.
  No encoder-parity or measured-performance claim follows from this change.

Decoder coefficient-stage correction after `b305e6e89`, verified on 2026-09-05:

- The source trace established an architectural deviation: `Av1SymbolDecoder.cs:1415-1459` published a
  count prefix and scan-ordered quantized levels; `Av1TileReader.cs:1148-1157` packed those variable-length
  groups. `Av1BlockDecoder.cs:126-153,1349-1399` then used another superblock-sized, all-plane workspace
  to dequantize and reorder every transform during reconstruction. Both buffers were cleared separately.
  The native entropy traversal dequantizes each signed level directly into its coefficient region
  (`av1/decoder/decodetxb.c:116-165,279-312`); EOB belongs to separate metadata
  (`av1/common/blockd.h:452-461`). Native region cursors advance by nominal transform area
  (`av1/decoder/decodeframe.c:274-279`), independently of EOB.
- Parsing now publishes dequantized coefficients directly and records EOB in `Av1TransformInfo`. Each plane's
  parser/reconstruction cursor advances by nominal transform area, including skipped transforms. Frame state
  reserves 16 coefficient slots per 4x4 unit, with no count prefix. Reconstruction consumes that storage
  directly; its second coefficient workspace and inverse-quantization pass are removed. These changes span
  the production parser, transform descriptors, frame storage, and reconstruction caller, rather than adding
  a disconnected native primitive.
- Quantization state moves to the parser. Mode syntax establishes delta-Q before `Residual` updates the
  segment/plane values, matching `decodeframe.c:1172-1221`. Transform-local parameters preserve matrix
  bypass, weighted-quantizer rounding, the 24-bit product mask, transform scaling before sign, and signed
  precision clipping (`Av1InverseQuantizer.cs:92-135`, `decodetxb.c:52-58,298-312`). The entropy context still
  uses the masked quantized magnitude and original DC sign (`Av1SymbolDecoder.cs:1424-1476`). No additional
  allocator-owned buffer or native production dependency was added.
- The coefficient capacities decrease by 25.5 KiB for a 64x64 4:2:0 superblock configuration and 102 KiB for
  128x128 4:2:0: this combines removal of the second workspace with removal of count-prefix capacity.
  These are source-derived coefficient-buffer sizes, excluding descriptor/object overhead, not measured
  total memory or a timing improvement. No benchmark was run.
- Existing entropy tests now check the published dequantized raster values, including sparse and beyond-EOB
  zeros, against fixed reference qindex-23 DC/AC values. Four added matrix/arithmetic cases use explicit
  8/10/12-bit reference values, matrix bypass for identity/one-dimensional transforms, lossless bypass,
  asymmetric precision limits, product-mask wraparound, and sign-after-scaling rounding. The old matrix test
  checked lengths only. These component cases do not establish complete signaled-matrix bitstream coverage.
- After the production edit, serialized Release .NET 11 Visual Studio VSTest passed **9,371/9,371** AV1 and
  public HEIF encoder cases in 2.6933 minutes (`coefficient-stage-final.trx`). After adding the fixed-value
  tests, a focused set passed **130/130** in 3.0217 seconds (`coefficient-stage-last-edit.trx`). Following final
  whitespace cleanup, the checkpoint set passed **159/159** in 5.5189 seconds (`coefficient-stage-checkpoint.trx`).
  The final build had zero errors and the existing 1,009 warnings; Roslynk reported zero compiler errors.
  No production behavior changed after the broad run, and no retained reference samples were altered.
- Optimized current-reference redecoding matched the retained restoration and film-grain references across
  **8,500,087** samples, maximum error **0**, zero exceeding one; those references also passed the managed
  conformance tests. Twelve regenerated color sequences matched another **21,348** native samples exactly.
  Reports are `restoration-comparison.json`, `film-grain-comparison.json`, and `decoder-comparison.json` in
  `D:\GitHub\ynse01\av1-takeover-20260905`, outside the repository. This is bounded same-bitstream evidence,
  not separately encoded output parity, complete decoder conformance, or a performance acceptance result.
- Remaining architecture differences are explicit: the managed reader still parses a complete superblock
  before reconstruction (`Av1TileReader.ReadTile`, `Av1FrameDecoder.DecodePartition`) and clears its complete
  coefficient regions before reuse. Native single-thread decoding interleaves parsing/reconstruction through
  visitors (`decodeframe.c:935-958,2746-2765,2792-2801`), clears only through the maximum populated raster
  position after inverse transform (`:154-164`), and separates parsing/reconstruction for row workers with
  different buffer lifetimes (`:3244-3277`). Those traversal, clearing, and worker-lifetime differences remain
  open; this checkpoint does not claim that changing coefficient representation completes them.

Decoder CDF storage correction in progress on 2026-09-07:

- `Av1Distribution.cs:39,274-303,328-335,395-422` stored sixteen inverse cumulative thresholds as unsigned
  32-bit values. Their complete domain is 0 through 32768. Native `aom_dsp/prob.h:29,110-137` uses unsigned
  16-bit storage and wider arithmetic during adaptation. The managed field and copy views now use the existing
  `InlineArray16<ushort>`; initialization narrows after inverse conversion. The indexer and update arithmetic
  retain their wider types. The threshold region is 32 bytes instead of 64 bytes per distribution.
  Alphabet size, counters, object identity, copying boundaries, and CDF adaptation are unchanged.
- This follows a current-tree decoder measurement after the residual-skip verification below, using the existing
  benchmark and optimized native adapter. Both paths include construction, parsing, reconstruction, RGB48 output
  allocation/conversion, and disposal. The native path uses the same ImageSharp color converter, so packed output
  equality does not independently validate that converter. Both inputs passed exact packed-sample comparisons.
- Before the CDF-width edit, the three-iteration Short job measured 13.739 ms managed versus 5.133 ms native
  for `libavif-kodim23-8b.bit`, and 24.848 ms versus 9.263 ms for `libavif-cosmos1650-10b.bit`.
  The approximately 2.68x gap remains open. Managed allocation counts were 933.1 KiB and 1014.38 KiB;
  native allocations are not included in the managed counter. No historical speedup is established by this run.
  CPU model lookup and power-plan configuration were denied by the environment. The baseline report is
  `D:\GitHub\ynse01\av1-takeover-20260905\decoder-current-short\20260907-011627`.
  BenchmarkDotNet 0.15.8 used the in-process toolchain, .NET 11 preview 7, x64, one coding thread, three warmups,
  and three measured iterations. Production assembly SHA-256 was
  `4264B2B47188E8B5B15B24E55D9B5CDA468C407D9F48EE02F928DB977BBFDE5A`, matching the verified test assembly.
- The complete CDF object graph still differs from the flat native frame context (`av1/common/entropymode.h:71`
  onward). `Av1FrameEntropyContexts.cs:31-37,99-121` retains three active graphs plus independently owned
  reference snapshots, and `Av1FrameEntropyContext.cs:140-207` constructs each distribution separately.
  Reducing threshold width does not resolve graph shape, cost-table policy, or the remaining codec controller.
- Decoder coefficient and transform-state storage is already bounded to one active superblock
  (`Av1FrameInfo.cs:250-338,651-698`, `Av1SuperblockInfo.cs:50-94`). Reference-state transfer retains motion and
  segmentation data, not that scratch (`Av1FrameInfo.MotionField.cs:491-581`). Native single-thread decode also
  uses one superblock buffer (`decoder.h:45-108`, `decodeframe.c:2504-2524,2794`), retained in worker state.
  Managed frame-owned allocation lifetime remains different; no per-reference coefficient-copy defect was found.
- Release .NET 11 builds after the CDF-width edit with zero errors and zero reported warnings (24.25 seconds).
  All 2,298 focused entropy cases pass (`cdf-width-focused.trx`, 4.6192 seconds), followed by all 9,997 AV1 and
  selected HEIF cases (`cdf-width-production.trx`, 2.9274 minutes). Roslynk reports zero compiler/analyzer errors.
  No tests or expected output were changed. Fresh optimized native checks preserve exact agreement across
  8,521,435 established restoration, film-grain, and regenerated moving-color samples: maximum error zero,
  no differing samples, and zero errors above one. The retained-reference comparison and direct regenerated-output
  comparison boundaries are unchanged from the residual-skip checkpoint below.
- The post-edit Dry and Short benchmark jobs both pass exact packed output for the two existing inputs.
  Across 2,494,464 RGB48 components, maximum error is zero, with no differing samples or errors above one.
  Both decoders use the shared ImageSharp converter. Independently re-decoding those inputs also reproduces their
  1,904,640 retained native plane samples exactly (`decoder-benchmark-inputs.json`).
  Current managed/native means are 13.296/5.099 ms for Kodak and 25.419/9.448 ms for Cosmos.
  Managed allocations are 715.97/797.24 KiB, about 217 KiB less per input than before the storage correction.
  Three iterations and the recorded timing variance do not establish a speedup or close the performance gate.
  Full results and error intervals: `decoder-current-short\20260907-012459` in the temporary takeover directory.
  The post-edit production assembly SHA-256 is
  `8469641ACE0351349694D766BB9395DD14800CA3BDFF30E78EF74A0C9D1CE7F5` in both the test and benchmark outputs.
- Further CDF-layout comparison: native `av1/common/entropymode.h:71-167` embeds differently sized alphabets,
  their zero sentinels, and their observation counters into the frame context. `decoder/decoder.c:110-116`
  allocates its base/default contexts once with 32-byte alignment. Tile completion copies the selected state,
  resets counters, and publishes independent retained-frame state (`decoder/decodeframe.c:5488-5507`).
  Managed `Av1SymbolReader.cs:112-123` and `Av1SymbolWriter.cs:139-145` hold live distribution aliases;
  `Av1FrameEntropyContext.cs:545-690` copies/reset counts through those stable objects. Tests explicitly check
  independent snapshot adaptation (`Av1EntropyTests.cs:70-119`, `Av1MotionModeEntropyTests.cs:98-194`).
  Flattening storage must preserve those aliases and independent counters together. No distribution-type,
  frame-ownership, or CDF-layout refactor beyond the verified width correction has been applied.

Encoder residual-skip correction in progress on 2026-09-07:

- The inter block decision required an empty quantized transform on every plane before considering prediction-only
  reconstruction. This excludes valid lower-cost skip decisions with nonzero coefficients. The starting path was
  `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:1230-1266`; its IBC caller has the same restriction at
  `:327-356`. Native `av1/encoder/tx_search.c:3875-3898` forces skip for an all-empty result and otherwise
  compares residual syntax plus distortion against prediction-only SSE for lossy blocks, including ties.
  Both inter and IBC use that decision (`av1/encoder/rdopt.c:1734,3490`).
- The bounded inter correction now measures prediction-only SSE before transform scratch is reused, normalizes
  high-depth error with rounding, retains four fractional distortion bits, compares costs without shared prediction
  syntax, and publishes prediction samples with cleared coefficients and default transform state when skip wins
  (`EvaluateInterCandidate:1020-1243`, `EvaluateInterPlane:1547-1724` in the current file).
  IBC now uses the same residual decision at `SelectIntraBlockCopy:318-367`. No search order, effort mapping, partition policy,
  reference role, quantizer, buffer allocation, or encoded-sample acceptance threshold changed.
- The new regression initially failed at qindex 64 and perturbation amplitude 4: skip residual cost 938,631 versus
  coded residual cost 1,001,301, with nonzero coefficients in every legal transform
  (`residual-skip-red.trx`). The full quantizer/perturbation matrix remains and now runs at 8, 10, and 12 bits.
  Its oracle computes prediction SSE and rounded costs explicitly, and checks selected rate, distortion, cost,
  cleared state, and byte-exact retained prediction. Transform arithmetic and entropy primitives are shared with
  production, so this establishes the block-decision regression only, not independent encoder parity.
- Automatic approval review rejected the initial combined inter/IBC replacement as too broad, and separately
  rejected narrowing the test to one pinned input. Neither rejected edit was applied. The safer inter-only patch
  passed a content check and was accepted; the original test matrix was retained and expanded to all three depths.
  The expanded test initially failed compilation because the generic span assertion selected an incompatible
  overload (CS9244); comparing the same complete sample spans as bytes resolved that without changing expectations.
- The inter-only focused encoder set passes 251/251 (`residual-skip-inter-focused.trx`, 21.9169 seconds).
  After the precision-test expansion, Release .NET 11 builds with zero errors and 1,009 existing warnings;
  all three precision cases pass (`residual-skip-precision.trx`, 2.8044 seconds).
  Twelve regenerated moving-color streams match optimized native decoding across 21,348 samples, maximum
  error zero, no differing samples, and zero errors above one. Final verification is recorded below.
  No benchmark or separate-encoder parity measurement has been run.
- A separate IBC regression now reaches production pixel candidate search using completed reconstructed blocks
  before the target superblock. After correcting its coefficient-buffer width, it failed at qindex 64 and amplitude 4:
  skip residual cost 940,455 versus coded residual cost 1,005,028, with nonzero coefficients in every legal transform
  (`residual-skip-ibc-setup-corrected-red.trx`). The subsequent IBC-only correction was accepted by automatic review.
  The first post-fix rate check then exposed a missing target-grid mapping in the test setup; adding the same
  `MapModeInfoBlock` operation used by production traversal corrected the displacement lookup. No expected value
  or tolerance was relaxed. All six inter/IBC precision cases and the two existing full-RD/half-chroma IBC cases pass
  (`residual-skip-reference-mapped.trx`, 8/8, 3.4124 seconds).
  The final build has zero errors and 1,009 existing warnings (10.76 seconds); Roslynk reports zero compiler
  and analyzer errors. All 9,997 AV1 and selected HEIF cases pass after the final C# edit
  (`residual-skip-production.trx`, 2.7919 minutes).
  Fresh optimized-native checks match all 8,521,435 established samples exactly: maximum error zero,
  zero differing samples, and zero errors above one. The restoration and film-grain scripts re-decode the
  retained references also exercised by the current managed tests; the moving-color script compares
  regenerated managed output directly. These checks do not establish separately encoded output parity.

Decoder coefficient reuse correction in progress on 2026-09-06:

- Neighbor-context storage-width correction in progress on 2026-09-07: managed above/left entropy, partition,
  and transform contexts used `int` entries. Native `av1/common/entropy.h:51-52,80` and
  `av1/common/enums.h:168,522` define byte-sized entries. Partition masks require five bits, transform extents
  reach 128, and coefficient contexts pack three saturated level bits with sign class 0/1/2, for a maximum of 23
  (`av1/common/txb_common.h:274-280`, `av1/decoder/decodetxb.c:316-319`).
  The two existing context owners and their tile-reader/entropy-decoder spans now use bytes.
  Coefficients and arithmetic retain their existing precision. Explicit casts occur only after these established bounds.
  At 3840-pixel width with three planes, the above-context surface changes from 18.75 KiB to 4.6875 KiB;
  owner count, region strides in entries, and allocation lifetime are unchanged. This is a storage-size correction,
  not measured performance evidence or completion of the remaining context-lifetime work.
  Existing coefficient regressions now derive packed edge contexts independently from original quantized values.
  Their first run exposed an incorrect six-bit assumption in the newly added test and comments (expected 127,
  actual 15). Direct inspection confirmed that both managed and native constants use three bits; the new expectations
  and comments were corrected to that contract. Production packing arithmetic and pixel expectations were not changed.
  All 181 focused coefficient-entropy, tiling, and frame-buffer cases now pass
  (`byte-neighbor-contexts-focused-corrected.trx`, 5.0877 seconds).
  Final Release .NET 11 build: zero errors, 1,009 existing warnings, 31.86 seconds.
  All 9,991 AV1 and selected HEIF cases pass (`byte-neighbor-contexts-production.trx`, 2.8460 minutes).
  Fresh optimized-native comparisons remain exact across the established 8,521,435-sample corpus:
  maximum error zero, zero differing samples, and zero errors above one.

- Above-context reset correction on 2026-09-07: `Av1ParseAboveNeighbor4x4Context.Clear` previously reset
  only the visible tile width and used that same width for every coefficient plane. Native
  `av1/common/av1_common_int.h:1594-1628` rounds the width to the current superblock extent and scales
  chroma coefficient widths by horizontal subsampling. Managed above contexts are tile-relative
  (`Av1TileReader.ParseTransformBlock:1282-1285`), so the equivalent reset starts at local offset zero.
  The correction now clears that padded extent, preserving neutral contexts for nominal transform reads
  beyond a clipped tile edge. A poisoned-context regression failed at the first padded transform entry
  before the correction (expected 64, actual 128; `above-context-reset-before.trx`).
  Eight cases cover both superblock sizes and monochrome/4:2:0/4:2:2/4:4:4; they also verify untouched
  regions beyond each plane's required extent. All 66 focused tiling/compound cases pass
  (`above-context-reset-focused.trx`, 5.6818 seconds). Final incremental Release .NET 11 build:
  zero errors and zero reported warnings, 24.79 seconds. All 9,991 AV1 and selected HEIF cases pass
  (`above-context-reset-production.trx`, 2.8410 minutes). Fresh optimized-native comparisons remain exact
  across the established 8,521,435-sample corpus: maximum error and differing-sample count both zero.
  This establishes the reset-state defect; a newly failing independently encoded tile fixture has not
  been established. Decoder-wide conformance and performance claims remain unsupported.

- Reconstruction workspace lifetime follow-up: native `av1/decoder/decoder.h:45-134` retains block scratch
  on decoder worker state. `av1/decoder/decodeframe.c:3483-3518,5339-5357` allocates prediction scratch when
  its physical sample capacity changes, and `av1/decoder/decoder.c:237` releases it at decoder destruction.
  Managed `Av1BlockDecoder` previously rented its combined inverse/prediction/CfL owner in each frame's constructor
  and returned it through `Av1FrameDecoder.Dispose`. The owner now belongs to `Av1Decoder.ReadTile:777-897`
  and `Dispose:1061-1076`; frame/block reconstruction borrow `Memory<short>` without an ownership flag or wrapper.
  `Av1BlockDecoder.GetWorkspaceLength:138-150` preserves the existing layout and sequence-dependent capacity.
  A larger requirement releases the old owner before renting the replacement; a failed rent leaves no stale owner.
  Smaller subsequent frames reuse the larger capacity. Native and managed scratch layouts remain different:
  native has separate motion, convolution, mask, and OBMC allocations, while managed prediction families share
  one short-based working region. This lifetime correction does not establish identical allocation layout or speed.
  Component callers now supply and dispose their own workspace; existing exact sizing and one-rent assertions remain.
  Production checks cover initial/growth allocation rejection, retry, 64/128 superblock transitions, return to the smaller
  size, exact constant lossless output, and balanced exactly-once returns. The independent restoration sequence also
  requires the same reconstruction owner across repeated frames and 8/10/12-bit sequence changes.
  Seven focused cases pass (`reconstruction-workspace-recovery.trx`, 5.5893 seconds).
  Latest Release .NET 11 build: zero errors, 1,009 existing warnings, 8.44 seconds; Roslynk reports zero compiler errors.
  All 9,983 AV1 and selected HEIF cases pass (`reconstruction-workspace-production.trx`, 2.8023 minutes).
  Fresh optimized-native checks after that run remain exact across 8,521,435 samples: maximum error zero,
  zero differing samples, and zero errors above one. The comparison corpus and retained-plane versus direct-plane
  distinction are the same as the CfL follow-up below; no timing or separate-encoder parity claim follows.
  Coefficient and transform-descriptor owners still belong to frame syntax storage,
  and above/left parser contexts remain frame-local; those worker/common-state lifetime deviations remain open.

- Inter/IBC CfL storage follow-up: `Av1BlockDecoder.EndBlock:1465-1503` now stores luma once after every
  residual in the block has been reconstructed. Ordinary intra storage remains per transform.
  `Av1TileReader.ParseBlock` invokes this completion phase after `Residual`; the pre-parsed component entry
  point uses the same phase. The final parsed luma descriptor supplies the transform alignment for the visible
  block extent. Evidence: native `av1/decoder/decodeframe.c:844-850,1037,1064-1124` and
  `av1/common/cfl.c:406-436`. `Av1ChromaFromLumaContext.Store:94-194` shares its existing subsampling kernel
  between transform dimensions and explicit completed-block dimensions; no additional sample storage was added.
  Twelve new cases poison the CfL surface, prove it is untouched before completion, then independently calculate
  Q3 samples and check untouched regions across 8/10/12 bits, 4:2:0/4:2:2, and visible/extended transform heights.
  The first regression failed before the production correction (expected poison -32768, actual Q3 sample 20).
  A test initially attempted to assign a private-set property; the inaccessible assignment was removed before
  execution. Final Release .NET 11 build: zero errors, 1,009 existing warnings, 8.64 seconds.
  All 58 focused tiling/compound cases pass (`cfl-block-store-focused.trx`, 5.5436 seconds).
  All 9,981 AV1 and selected HEIF cases pass (`cfl-block-store-production.trx`, 2.8205 minutes).
  Fresh optimized native comparisons cover 8,521,435 samples: 5,386,240 restoration samples, 3,113,847 film-grain
  samples, and 21,348 samples from twelve regenerated moving-color streams. Maximum error, differing-sample count,
  and count above one are all zero. Restoration/film-grain tests compare the edited decoder to the same retained
  planes independently checked with aomdec; moving-color comparisons directly compare regenerated managed/native
  planes. These checks do not establish encoder parity or a performance improvement.

- Transform interleaving follow-up: `Av1TileReader.ParseBlock:959-1019` now publishes complete modes and
  transform geometry before residual parsing, preserving its assigned map index in the borrowed partition state.
  `Residual:1056-1208` calls reconstruction immediately after each transform's EOB is written.
  `Av1BlockDecoder.BeginBlock:252-1275` predicts inter/IBC planes before residuals;
  `DecodeTransform:1283-1457` performs ordinary intra prediction, inverse reconstruction, populated-prefix clearing,
  and the existing luma-context update. Native ordering is in `av1/decoder/decodeframe.c:283-310,935-1037,1172-1228`.
  The existing pre-parsed block API uses these same methods for component tests; production no longer walks
  a second block/transform list. Source/frame ownership and coefficient capacities remain unchanged.
  The test stub poisons unread EOB fields at block entry and checks during each transform callback that its
  EOB is populated while all later transforms remain poisoned. It also requires every callback before the next block.
  This verifies traversal timing independently of the final decoded output.
  An initial build stopped on three blank lines before closing braces left by the method split; these were corrected.
  Final Release .NET 11 build: zero errors, 1,009 existing warnings, 32.49 seconds. Forty-six tiling/compound checks
  passed (`transform-interleaving-focused.trx`, 5.6221 seconds), followed by 9,969/9,969 AV1 and selected HEIF tests
  (`transform-interleaving-production.trx`, 2.7762 minutes). Fresh optimized native decoding agrees with retained
  restoration/film-grain planes across 8,500,087 samples; tests compare the edited managed decoder to those same planes.
  Twelve regenerated moving-color streams add 21,348 directly compared managed/native samples. Combined maximum
  error is zero, with zero differing samples and zero errors above one (`restoration-comparison.json`,
  `film-grain-comparison.json`, `decoder-comparison.json` in the temporary takeover directory).
  At this checkpoint, inter-block CfL storage remained per luma transform,
  while native `decodeframe.c:844-850,1037` stores once after the block's residuals. Native `cfl.c:421-436` rounds
  the visible block extent to the last selected transform dimensions before storing. The follow-up above addresses
  that storage phase. No performance or encoder
  parity claim follows from these reconstruction checks.

- Subsequent parsed-block traversal correction: native `av1/decoder/decodeframe.c:1172-1228,2746-2801`
  reconstructs while walking parsed partitions. The managed production reader previously parsed every block
  in a superblock, then `Av1FrameDecoder.DecodePartition` walked their records again. `Av1TileReader` now
  begins reconstruction at superblock entry and invokes `IAv1FrameDecoder.DecodeBlock` immediately after
  publishing each parsed record. `Av1FrameInfo.UpdateModeInfo` returns that existing stored record by reference;
  reconstruction clears its borrowed palette-map views after consumption. No new buffer or copy was introduced.
  The first build exposed the unchanged `IAv1FrameDecoder` contract; the interface and its existing test stub
  were then updated together. A new total-count assertion initially mistook `FrameInfo.ModeInfoCount` capacity
  for parsed records. It now sums the tile's actual per-superblock record counts without changing pixel expectations.
  Assertions inside the callbacks require zero records at superblock entry and exactly the currently visited
  number of published records at each block callback, proving that later blocks have not been parsed first.
  All 17 tiling checks passed (`block-traversal-focused-fixed.trx`, 2.9108 seconds), followed by 9,969/9,969 AV1
  and selected HEIF tests (`block-traversal-production.trx`, 2.7630 minutes). Final Release .NET 11 build:
  zero errors, 1,009 existing warnings, 8.90 seconds. Individual-transform parse/reconstruct interleaving remains
  open at this checkpoint; `decodeframe.c:935-958,283-310` consumes each transform before reading the next.
  These are reconstruction/traversal checks, not encoder parity or performance evidence.

- Official main was rechecked through a live request to the official Gitiles endpoint and remains
  `d565eec60f084421fa34fc0534b760c6452b6a6c`. The cached web response was older and was not used as revision evidence.
  A fresh direct Gitiles request on 2026-09-07 confirms the same revision. The first request was blocked by
  sandbox socket permissions; the allowed direct retry succeeded. The web tool again returned an older July
  response, which was excluded from current-reference evidence.
- The preceding coefficient-stage checkpoint left a clearing-lifetime deviation open. Native
  `av1/decoder/decodetxb.c:135-150,279-284` records the largest populated raster index independently of EOB;
  `av1/decoder/decodeframe.c:154-164` clears that prefix after inverse reconstruction. EOB alone cannot bound
  the prefix because the scan can visit a larger raster index earlier than its final nonzero symbol.
- The managed sign/dequantization loop now records that index in `Av1TransformInfo.MaximumCoefficientIndex`.
  `Av1BlockDecoder.DecodeBlock` clears through it after reconstruction. `Av1TileReader.ReadTile` retains whole-plane
  clearing only for syntax-only parsing, whose contract exposes coefficients without invoking reconstruction.
  Initial allocator storage is already clean (`Av1FrameInfo.cs:289-292`); no additional coefficient buffer or owner
  was introduced. Skipped and all-zero transforms reset their residual metadata at the owning parse boundaries.
- Focused entropy tests check the populated raster bound, including sparse input, and reuse a nonempty descriptor
  for an all-zero transform. Existing tile reconstruction cases now assert that all three coefficient scratch planes
  are zero afterward in both byte and high-bit-depth paths. These assertions supplement exact reference comparisons;
  they do not establish full decoder correctness or a measured performance improvement.
- Release .NET 11 build passed with zero errors and the existing 1,009 warnings. Serialized Visual Studio VSTest
  passed 125/125 focused entropy, tiling, and frame-buffer cases (`coefficient-clearing-focused.trx`), followed by
  9,375/9,375 AV1 and public HEIF encoder cases in 2.6811 minutes (`coefficient-clearing-production.trx`).
  Fresh optimized-native checks matched all 8,500,087 retained restoration/film-grain samples and 21,348 regenerated
  color-sequence samples: maximum error zero and zero differing samples. The retained fixtures also passed the
  managed exact-plane assertions; this is bounded same-bitstream evidence, not separate-encoder parity.
- A subsequent ownership correction removes the second dequantization context constructed by `Av1TileReader`.
  `Av1InverseQuantizer` had already constructed identical values, then discarded its own context when the reader
  supplied the duplicate to `UpdateDequant`. The quantizer now retains and updates its original context. The frame
  setup and conditional delta-Q updates still follow `decodeframe.c:1865-1906,1200-1217`. After this final production
  edit, the Release .NET 11 rebuild passed with zero errors and 1,009 existing warnings; serialized Visual Studio
  VSTest passed 222/222 focused cases including the independent reconstruction corpus in 1.8621 minutes
  (`coefficient-lifetime-final.trx`). Roslynk reports zero compiler errors and the existing 34 compiler warnings.
  No benchmark has run.
- Whole-superblock parsing before reconstruction and worker lifetimes remain unresolved. This change addresses
  clearing ownership only; it does not claim completion of the encoder or decoder architecture.

Film-grain decoder source comparison after `ef8b1a823`:

- The complete template generation, random state, autoregression, scaling interpolation, overlap traversal,
  noise application, and native-sample load/store paths were compared with `av1/decoder/grain_synthesis.c`.
  Managed `Av1FilmGrainDecoder.cs:874-1172,1204-1231` matches the represented rules in native `:429-629`;
  all 2,048 Gaussian entries also match exactly. No new arithmetic defect was established in this comparison.
- Managed noise application processes chroma before luma (`Av1FilmGrainNoise.cs:109-174`), retaining ungrained
  luma for chroma scaling. Native `grain_synthesis.c:685-745,803-862` uses that same ordering. The two-component
  luma average is horizontal only; vertical chroma subsampling selects a row rather than averaging two rows.
  Restricted identity-matrix chroma uses luma's upper endpoint, and high-depth lookup interpolates below entry 255.
- `Av1FilmGrainDecoder.cs:698-857` and native `grain_synthesis.c:1252-1376` exclude already-applied overlap
  strips and retain right/bottom grain boundaries. The managed plane span retains existing padded storage
  (`:208-216`), including addresses for empty interiors at clipped edges; no new guard or scratch plane was added.
- Grain presentation preserves references in `Av1Decoder.cs:891-909,945-1002`: refreshed frames receive a
  separate presentation copy, and unreferenced shown frames can be grained in place. `CopyVisibleTo` also copies
  active geometry (`Av1FrameBuffer.cs:248-258`). This source trace does not complete the decoder-wide lifetime audit.
- SIMD dispatch remains an open architecture/performance issue. `Av1FilmGrainNoise.cs:180-238,388-522,947-955`
  uses AVX2 gather or a high-depth-only portable path with separate width overloads. `Av1FilmGrainOverlap.cs:142-175`
  additionally gates 512-bit processing on `Vector<int>.Count`. Neither gate has fresh end-to-end evidence here.
  Comments claiming that scalar reads are slower, interpolation repays them, or `Vector<T>` establishes processor
  execution width were corrected. Runtime dispatch and arithmetic were not changed; no improvement is claimed.
- After the final comment edit, the Release .NET 11 build completed with zero errors and 1,009 existing warnings;
  Roslynk reported zero compiler errors. Serialized Visual Studio VSTest passed 5/5 focused film-grain/reference
  cases in 8.4512 seconds (`film-grain-audit.trx`), including existing hardware-fallback and constrained-allocation checks.
- Current optimized libaom regenerated seven still references and the two ten-frame official sequence references.
  All 3,113,847 decoded samples match the retained references exactly: maximum error 0, zero samples exceeding one.
  Per-frame/per-plane results are in temporary `film-grain-comparison.json`. Those references are also used by the
  passing managed tests. This is bounded same-bitstream decoder evidence, not complete conformance or encoder parity.
  No benchmark ran, and no fixture, native integration, or generated comparison output was added to the repository.

RD accumulation correction after `4c7880d`:

- Numerical defect: before this correction, `Av1IntraSuperblockEncoder.ModeDecision.cs:349-381` added
  rounded child costs, `:727-731` added rounded luma/chroma costs, and `:1193-1212` added separately rounded
  block syntax. Reference `av1/encoder/rd.h:32-34,208-233` keeps raw rate and distortion; rectangle and split
  accumulation in `partition_search.c:3487-3504,4605-4607` rounds the combined rate. For multiplier 128,
  two rates of 2 cost 1 jointly but cost 2 when rounded separately; two rates of 1 show the opposite error.
- `Av1RateDistortionStatistics.cs:9-61` now carries raw rate, distortion, and the comparison cost through
  spatial, tiled, filter-intra, palette, CfL, IBC, and inter winners. Mode/transform comparisons retain their
  existing strict tie rules and scalar bounds. Bounded-out split candidates retain an invalid sentinel;
  only valid selected statistics are accumulated. Existing per-transform raw accumulation is preserved.
- Partition evaluation now combines those statistics in `Av1IntraSuperblockEncoder.ModeDecision.cs:336-390`;
  ordinary block syntax and luma/chroma aggregation use the same retained raw inputs. This adds value state,
  not a buffer or allocation. It does not implement native partition pruning, mode order, cost refresh,
  quantization stages, reference control, or adaptive rate multipliers. No performance improvement is claimed.
- Seven focused regressions pass in Release .NET 11 through serialized Visual Studio VSTest
  (`rd-statistics-focused.trx`, 2.6557 seconds). Four production block cases independently recount selected
  syntax and pixel-domain SSE for monochrome/color and zero/nonzero residuals. Three fixed arithmetic cases
  cover both rounding directions and 64-bit distortion.
- After the final C# edit, the Release .NET 11 build completed with zero errors and 1,009 existing warnings;
  Roslynk reported zero compiler errors. Serialized Visual Studio VSTest passed 2,259 entropy,
  intra-superblock, HEIF encoder, and reconstruction-conformance cases in 2.1328 minutes
  (`rd-statistics-final.trx`), then 132 encoder-frame cases in 18.7250 seconds (`rd-statistics-frames.trx`).
  The seven new cases are included in those totals. Tests encoding the existing effort policy only verify
  current behavior; their success does not validate that policy against libaom.
- Optimized libaom decoding of the regenerated eight partition, 23 palette, and twelve color-sequence
  streams matches all 38,973 samples exactly: maximum error 0, zero samples exceeding one. This is
  same-bitstream reconstruction evidence. Separate-encoder acceptance remains unmet; no benchmark ran.

Range-writer output-capacity correction, verified after `93aba785f`:

- `Av1SymbolWriter.cs:213-214,339` before correction sliced a fixed initial allocation for finalization
  and eight-byte flushes. Reference `aom_dsp/entenc.c:78-91,270-285` grows capacity when either needs
  more room. The packet estimate in `Av1FrameEncoder.cs:544-563` does not replace that range-coder policy.
  This is a demonstrated writer-capacity deviation; no production frame overflowing that estimate was established.
- The existing owner now grows at those two boundaries. Word flushes double the current tile capacity and add
  eight bytes; finalization reserves its exact terminating length. Reallocation preserves finalized preceding
  tiles and the current completed prefix, including bytes that can receive a backward carry. Pending bits stay
  in the range state. The old owner is returned only after successful allocation/copy, and reset reuses capacity.
  Existing frame aggregation remains; this does not complete native deferred-packing or worker ownership parity.
- Before correction, the zero-capacity consecutive-tile regression failed in `Normalize` with
  `ArgumentOutOfRangeException` (`writer-growth-red.trx`); VSTest stopped on that first failure.
  Five small initial capacities now preserve three consecutive tiles byte-for-byte against sufficient-capacity
  encoding, with CDF adaptation enabled/disabled. Existing native carry assertions cover two additional
  finalization-growth capacities. Allocation limits independently exercise failure at both growth boundaries,
  and allocation identities verify every successful owner is returned exactly once.
- Final Release .NET 11 incremental build: zero errors and warnings; preceding test compilation:
  1,009 existing warnings. Roslynk reports zero compiler errors. Serialized Visual Studio VSTest passes
  2,292/2,292 entropy, intra-superblock, encoder-frame, and HEIF encoder cases in 26.9726 seconds
  (`writer-growth-final.trx`), including the twelve focused writer cases.
- Current optimized libaom decoding of the regenerated 23 palette, eight partition, and twelve color-sequence
  streams matches all 38,973 samples exactly: maximum error 0, zero samples exceeding one.
  These are bounded same-bitstream checks, not separate-encoder parity or a timing/quality improvement.
  No benchmark was run. All temporary native comparison output and test reports remain excluded from commits.

Restoration processing-unit correction:

- Before correction, `Av1LoopRestorationDecoder.cs:147-166,309-358` sized its bordered source, Wiener
  intermediate, and eight-bit output bridge for a whole restoration-unit stripe, including an absorbed tail.
  Only the self-guided branch split horizontally into processing units. Native
  `av1/common/restoration.c:389-408,904-963,987-1054` dispatches both filters in 64-luma-sample processing
  units with chroma subsampling applied. This is a traversal/sizing deviation, not a demonstrated pixel defect.
- Both branches now share that bounded traversal and scratch sizing. Source context crosses every chunk and
  restoration-unit edge; replication remains restricted to the frame edge. Stripe-boundary rows retain their
  deblocked provenance. Existing kernels accept the exact tail width, whereas native Wiener SIMD rounds its
  final write into padded storage. Frame output ownership and the self-guided statistics boundaries are retained.
- For a luma plane at least 384 samples wide with a nominal 256-sample restoration unit, the combined ushort
  scratch request calculated from the source falls from 155.47 KiB to 26.72 KiB at eight bits, and from
  107.47 KiB to 18.72 KiB at 10/12 bits. These figures exclude the destination plane and integer self-guided
  scratch. They are allocation-formula results, not measured process memory or a timing improvement.
- Final verification passes nine serialized Release .NET 11 VSTest cases in 16.0008 seconds
  (`restoration-grid-final-r2.trx`), including native-plane restoration, both filter
  types, all three precisions, 4:2:0/4:2:2/4:4:4, super-resolution, and hardware fallbacks. No tests or expected
  outputs were changed. The final incremental build reports zero errors and warnings; the preceding compilation
  reported 1,009 existing warnings. Roslynk reports zero compiler errors.
- All six retained restoration references were independently regenerated with the current optimized native
  decoder and match exactly: 5,386,240 Y/U/V samples, maximum error 0, zero samples exceeding one.
  Per-plane results and payload sizes are in the temporary `restoration-comparison.json`. This verifies the
  provenance of the exact references used by the tests; it does not establish separate-encoder parity.
- `Av1WienerFilter.cs:96-134,165-188` still computes one horizontal output using a vector dot product and
  traverses vertical outputs scalarly. The optimized reference instead processes multiple outputs per vector
  (`av1/common/x86/wiener_convolve_avx2.c`). That SIMD/traversal architecture and the eight-bit output bridge
  remain open. No benchmark was run, and temporary native output and scripts remain excluded from commits.

Palette coded-boundary correction, verified after `b2aee3036` on 2026-09-05:

- The encoder clipped luma/chroma palette search to visible frame dimensions
  (`Av1IntraSuperblockEncoder.PaletteModeDecision.cs:46-49`,
  `Av1IntraSuperblockEncoder.ChromaPaletteModeDecision.cs:54-57` before correction), and
  `Av1TileWriter.cs:1085-1086` omitted palette symbols outside those visible dimensions. This is a numerical
  and syntax defect, separate from the conditional distortion-model border policy described above.
- Reference palette search (`av1/encoder/palette.c:555-597,787-804`), tokenization
  (`av1/encoder/tokenize.c:229-241`), and decoding (`av1/decoder/detokenize.c:65-77`) all use
  `av1/common/blockd.h:1517-1557`. Its distances come from coded mode-info dimensions
  (`av1/common/av1_common_int.h:1358-1364`), not visible-pixel dimensions or the optional RD border policy.
  The managed decoder already follows those coded distances (`Av1PartitionInfo.cs:190-194`,
  `Av1TileReader.cs:2720-2744`). Search, rate evaluation, and writing now agree on that same extent.
- The existing 5x3 luma regression checked retained reconstruction without decoding its payload. Extending it
  to decode every case exposed a truncated tile entropy stream (`palette-bounds-red.trx`). An earlier assertion
  incorrectly read `FrameBuffer` after `Decode` had released the native planes; that test mistake was corrected
  using `DecodeFrameBuffer` and is not codec-failure evidence (`palette-bounds-before.trx`).
- Existing palette/color/EOB/reconstruction assertions remain. Added cases cover clipped/transposed luma at
  10/12 bits and 4:4:4, 4:2:2, and 4:2:0 chroma, including one-pixel source axes. The same boundary correction
  prevents those subsampled axes from creating zero-length palette input. No defensive rejection, new owner,
  extra production buffer, or change to search effort thresholds was introduced.
- Final Release .NET 11 build: zero errors, 1,009 existing warnings. A preceding build stopped on a missing
  blank line before a comment; it was corrected before verification. Roslynk reports zero compiler errors.
  Serialized Visual Studio VSTest passes 190/190 cases in 18.4734 seconds: intra-superblock encoder,
  HEIF encoder, and AV1 palette cases (`palette-bounds-final.trx`).
- Optimized official libaom decodes all 23 regenerated palette payloads with exact agreement against retained
  encoder reconstruction: 985 visible Y/U/V samples, maximum error 0, zero samples exceeding one unit.
  Per-plane results and output sizes are in `D:\GitHub\ynse01\av1-takeover-20260905\palette-comparison.json`.
  All generated streams, raw planes, scripts, and native output remain temporary and excluded from commits.
  This is same-bitstream reconstruction verification, not separate-encoder parity or performance acceptance.

Explicit grid-sampling conversion correction, verified after `5f7bad3a6` on 2026-09-05:

- `HeifEncoderCore.Sequence.cs:97-109` promoted incompatible odd-grid sampling only when the public option
  was unspecified; `HeifEncoderCore.cs:1078-1081` then rejected the same dimensions for explicit 4:2:0/4:2:2.
  The established TIFF conversion contract (`TiffEncoderCore.cs:371-444`) resolves unsupported option
  combinations before encoding, including explicit choices. The existing HEIF fallback already demonstrates
  that these source dimensions can be preserved through 4:4:4 conversion.
- The extended existing public grid regression first passed default sampling and then failed on explicit
  4:2:0 with that exact exception. VSTest stopped on the failure; see `grid-sampling-before.trx` in the local
  takeover report directory. Option resolution now promotes explicit and default incompatible sampling alike
  before matrix/profile selection. The redundant private grid guard was removed: its only caller obtains the
  resolved settings first (`HeifEncoderCore.cs:1040-1062`), and multi-frame AV1 input follows the sequence path.
- All original descriptor, extent, edge-replication, hidden-item, reference-order, and exact-pixel assertions
  remain. Four cases additionally inspect both emitted cell headers and preserve the source profile object.
  Final serialized Release .NET 11 Visual Studio VSTest passes 67/67 HEIF encoder cases in 6.2836 seconds
  (`D:\GitHub\ynse01\av1-takeover-20260905\grid-sampling-final.trx`). The final source build reports zero errors
  and warnings; the preceding test compilation reports 1,009 existing warnings. Roslynk reports zero compiler errors.
  No benchmark or separate-encoder measurement was run. Decoder rejection tests for invalid input grids remain unchanged.

Identity-matrix option correction after `fff06700d`:

- Unsupported restriction: `HeifEncoderCore.Sequence.cs:129-156` required BT.709/sRGB metadata for every
  identity-matrix 4:4:4 encode and otherwise substituted BT.601. Reference
  `av1/encoder/bitstream.c:2457-2494` permits other primaries/transfer descriptions with an explicit range
  bit. The special BT.709/sRGB branch alone infers full range. Reference validation requires unsubsampled
  identity planes (`av1/av1_cx_iface.c:921-928,1005-1013`), not that special color description.
- The production writer already has the correct syntax branches (`ObuWriter.cs:397-446`), and the shared
  semantic identity operator already maps G/B/R with the luma range on every plane
  (`HeifColorConverter.IdentityOperator.cs:17-139`). Option resolution now preserves valid identity
  descriptions and their explicit range; only BT.709/sRGB identity normalizes limited to full range.
  Existing conversion for incompatible sampling remains. No converter, buffer, or public API was added.
- Before correction the first public regression failed with expected Identity versus emitted BT.601
  (`identity-profile-red.trx`); VSTest stopped on that failure. After the final edit, Release .NET 11
  built with zero errors/warnings and Roslynk reported zero compiler errors. Serialized Visual Studio
  VSTest passed 81/81 HEIF encoder cases in 6.4887 seconds (`identity-profile-final.trx`).
- Fourteen new cases cover BT.2020/PQ full/limited range at 8/10/12 bits, stills/sequences, and special
  sRGB range inference. They inspect emitted syntax, container metadata, preserved source metadata,
  and every decoded RGB component with a one-unit limit for range conversion. Optimized libaom decoding
  matches all 4,032 independently calculated GBR samples exactly: maximum error 0, zero samples exceeding
  one (`identity-comparison.json`, temporary and outside the repository). This is bounded conversion and
  same-bitstream evidence, not separate-encoder parity or performance evidence. No benchmark ran.

Color-conversion boundary correction after checkpoint `f7bd907d6`, verified on 2026-09-05:

- `HeifEncoderCore.Sequence.cs:74-194` resolved output sampling and preserved reversible YCgCo matrix metadata,
  including when default 4:2:0 or requested 4:2:2 was incompatible. The shared converter's established contract
  rejects that combination at `HeifColorConversionParameters.cs:265-282`. The public save regression failed
  on default sampling with that exact exception (`matrix-fallback-before.trx`).
- PNG and TIFF resolve incompatible options through conversion at `PngEncoderCore.cs:1640-1654` and
  `TiffEncoderCore.cs:378-444`. HEIF now extends its existing identity-matrix fallback to incompatible
  YCgCo-Re/Ro sampling, converts with BT.601, and writes the matching matrix metadata. Explicit 4:4:4 remains
  eligible for the existing reversible operator. This changes encoder option resolution, not decoder acceptance.
- Seven public cases retain the original profile object and values, inspect decoded matrix/range metadata,
  and require byte-identical output to the same packed pixels explicitly encoded with the fallback matrix.
  The cases include the original identity fallback and YCgCo-Re/Ro with default, 4:2:0, and 4:2:2 sampling.
- A separate lifetime defect existed at `Av1FrameEncoder.cs:1404-1434`: conversion parameters were resolved
  after renting row storage. The internal-factory regression confirmed that rejected conversion had already
  made one allocator request (`conversion-allocation-before.trx`). Resolution now precedes storage allocation;
  the regression requires both allocation and return logs to remain empty. No new guard or owner was added.
- Final Release .NET 11 build: zero reported warnings and errors on the incremental build. Roslynk reports
  zero compiler errors. Serialized Visual Studio VSTest with stop-on-failure passes 265/265 affected cases
  (`conversion-final.trx` in the local takeover report directory).
  The regenerated color and partition streams again match optimized native decoding on all 23,396 samples,
  maximum error 0 and zero exceeding one. No benchmark or separate-encoder parity comparison was run.

Color/output source coverage and remaining limits:

- `Av1YuvConverter.cs:24-135,175-274` dispatches byte/high-bit-depth, complete/cropped/scaled output, and alpha
  through shared HEIF adapters. `HeifPlanarColorConverter.cs:39-320,329-870` was read through both traversals:
  conversion owns reusable row scratch, interpolates chroma before matrix conversion, and uses existing
  `PixelOperations<TPixel>` packing/unpacking or Rgb48/Rgba64 conversion.
- `HeifColorConverter.Operator.cs:164-431` uses closed semantic operators and descending SIMD widths.
  These architecture observations are not proof that every H.273 operator or pixel format is numerically correct.
  Independent color-conversion and complete SIMD coverage remain open.
- The reference codec interface exposes native planes and strides (`aom/aom_image.h:284-292`).
  The temporary comparison adapter supplies I420 using the managed RGB conversion. Its output cannot independently
  validate that RGB conversion, even when both codec decoders agree on native planes.

Intra-reference frame-extent correction after checkpoint `182f39ae5`, verified on 2026-09-05:

- Source comparison found that extension availability and extension length had been conflated.
  `Av1IntraSuperblockEncoder.ChromaModeDecision.cs:1211-1240` and
  `Av1IntraSuperblockEncoder.ModeDecision.cs:2393-2449` previously copied a complete adjacent extent whenever
  its coding-order availability flag was true. Reference `av1/common/reconintra.c:1737-1742,1817-1820`
  also clips the available count to the remaining coded frame extent, then repeats the final available
  sample in the edge preparation at `reconintra.c:1149-1184`.
- The new 56x56 production partition case failed before the correction with `ArgumentOutOfRangeException`
  at the top-right copy (`edge-extent-before.trx`: two existing 32x32 cases passed, then execution stopped
  on the new failure). This is an implementation defect, not a search-performance hypothesis.
  Bottom-edge reads also require the bound: `Buffer2DRegion{T}.cs:89-97` limits row width but resolves the
  row index against the backing buffer, whose encoder border can contain samples outside the coded region.
- Both shared luma/chroma references and tiled candidate references now bound adjacent samples by the
  plane's coded extent before endpoint repetition. The existing availability rules and allocation ownership
  remain the governing contracts; no new guard, rejection policy, owner, or scratch buffer was introduced.
- The rectangular reference cases retain all twelve earlier checks and add six explicit clipped-extent
  cases across 8, 10, and 12 bits and both orientations. They use a larger backing buffer with distinct
  values beyond the coded region, fixed expected edge sequences, and destination sentinels.
  The production mixed-partition test retains both 32x32 orientations and adds both 56x56 orientations.
- Final Release net11.0 build: zero errors and 1,009 existing warnings. Serialized Visual Studio VSTest:
  **273/273 passed** in `edge-extent-final.trx` (20.3789 seconds), covering encoder frames, intra-superblocks,
  HEIF encoder contracts, and the retained empty-transform cost-helper test. Roslynk reports zero compiler errors.
- Fresh optimized-reference decoding of four regenerated partition streams matches all 8,320 retained luma samples.
  Twelve regenerated moving color streams match all 21,348 Y/U/V samples. Combined maximum error is **0** across
  **29,668** samples, with **0** samples exceeding one. These are same-bitstream decoder/reconstruction comparisons;
  they do not establish separate-encoder parity or performance. No benchmark was run.

Intra-edge integration after checkpoint `2424ff9f9`, verified on 2026-09-05:

- Reference `av1/av1_cx_iface.c:333,1561-1562` enables intra-edge filtering by default and propagates it to
  sequence configuration (`av1/encoder/encoder.c:641-647`). `Av1FrameEncoder.cs:402` now enables that syntax.
  CDEF and restoration remain disabled and unresolved. The starting-tree findings above remain historical evidence.
- Encoder `Av1TransformBlockEncoder.cs:739-800,875-937` now prepares directional edges before prediction.
  Luma mode trials, selected-mode transform refinement, split luma transforms, tiled planes, and chroma candidates
  propagate both the sequence flag and the neighboring smooth-mode class. Raw references remain separate from
  candidate copies; filtering does not mutate references used by subsequent mode or transform trials.
- Neighbor selection at `Av1IntraSuperblockEncoder.ChromaModeDecision.cs:1035-1081` follows native
  `av1/common/av1_common_int.h:1359-1415` for the luma units that own subsampled chroma neighbors and
  `reconintra.c:958-986` for smooth-mode classification. Inter winners can retain a previous intra trial's UV field
  (`Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:925-933`), so that field is only meaningful for an intra neighbor.
- `Av1IntraEdgePreparation.cs:39-116` shares the complete corner/filter/upsampling order between encoder and decoder.
  Native `reconintra.c:1132-1147,1204-1243,1512-1548` defines the missing-sole-edge early return and directional
  preparation. Strength thresholds follow `reconintra.c:989-1026`; half-sample selection follows `reconintra.h:148-155`.
  The shared code preserves a missing sole edge's constant value rather than interpolating its distinct corner.
- `Av1IntraEdgeFilter` and `Av1IntraEdgeUpsampler` have separate closed generic traversals and semantic readonly
  operators, with descending 512/256/128-bit widths and scalar tails. Smoothing uses rounded nonnegative kernels;
  upsampling uses signed [-1,9,9,-1] arithmetic, rounding, clipping, and linear interleaving. Native definitions are
  `reconintra.c:1028-1082,1349-1381`. Inline comments explain endpoint padding, lane ordering, bounds, and scaling.
- Each candidate borrows existing transform scratch (`Av1EncoderBlockWorkspace.cs:143-144`) until prediction and
  residual formation finish. Two 160-sample edges retain native prefix sizing; only required edges are copied.
  Smoothing uses 132 samples including three endpoint padding positions. Upsampling needs exactly the native
  19 samples, including corner and endpoint extension; vector reads no longer require a larger padded window.
  Decoder scratch is 4,548 short samples (about 8.88 KiB), replacing its previous 4,576-sample workspace.
  No new owner or per-candidate allocation was added. This source-level sizing result is not a timing claim.
- Existing independent scalar kernel tests now cover all SIMD tiers, lengths around lane boundaries, extrema,
  and exact scratch capacities. Eight added preparation cases distinguish smooth-neighbor thresholds and missing
  sole edges in both orientations. Production tests assert the emitted sequence flag; mixed-partition tests retain
  the four unfiltered cases and add four filtered cases without weakening partition or reconstruction assertions.
- Final Release net11.0 build: zero errors and 1,009 existing warnings. Roslynk: zero compiler errors.
  Serialized Visual Studio VSTest passed **314/314** in `intra-edge-final.trx` (30.7293 seconds), including encoder
  frames, intra-superblocks, transform-block contracts, predictor SIMD tiers, native decoder fixtures and fallbacks,
  HEIF encoder contracts, and the retained empty-transform cost-helper test.
- Fresh optimized-reference decoding of eight regenerated partition streams matches all 16,640 retained luma samples.
  Twelve regenerated moving color streams match all 21,348 Y/U/V samples. Combined maximum error is **0** across
  **37,988** samples, with **0** exceeding one. These remain bounded same-bitstream reconstruction comparisons;
  separate-encoder sample parity, complete decoder coverage, and end-to-end performance are still unverified.
  No benchmark was run. Temporary scripts, native output, and reports remain outside the commit.

### Required completion gates

Frame-context investigation after checkpoint `1639550a0`:

- `Av1SymbolEncoder.cs:355-369` resets tile CDFs to quantizer-band defaults. `Av1TileEncoder.cs:281-337`
  does that between tiles and retains only their output offsets and lengths. Reference
  `av1/encoder/encodeframe.c:1456` initializes each tile from the unchanged frame context, then
  `av1/encoder/bitstream.c:4074-4079` signals the largest encoded tile as the update source.
  `av1/encoder/encoder.c:4489-4495` copies that tile's CDFs, resets observation counters, and stores them
  with the reconstructed reference frame. Encoder publication is missing in the managed path.
- Primary-reference selection is coupled to the reference-role and frame-layer controller
  (`av1/encoder/encode_strategy.c:168-230`), not simply the last encoded frame. The managed encoder retains
  only the preceding reconstruction and always writes global-motion models relative to identity
  (`ObuWriter.cs:1206-1226`). Enabling primary-reference reuse requires reconciling those inherited models,
  loop-filter deltas, segmentation, refresh slots, and entropy state together. No encoder frame-context flag
  or reference policy was changed during this investigation.
- Existing `Av1FrameEntropyContexts.BeginFrame`, `Av1FrameEntropyContext.CopyFrom`, and `SnapshotTo`
  already implement decoder base/working/published state and counter reset. They are existing reusable
  contracts to consider when implementing encoder publication; a new ownership framework is not justified.

Independent loop-filter delta entropy correction, verified on 2026-09-05:

- The context-family comparison found a numerical decoder defect. Before correction,
  `Av1SymbolDecoder.cs:921-924` always read `DeltaLoopFilterAbsolute`, including the per-channel loop at
  `Av1TileReader.cs:3065-3067`. Native `av1/decoder/decodemv.c:749-765` instead selects independent
  `delta_lf_multi_cdf[lf_id]` distributions for multi-delta syntax and the shared CDF otherwise.
  Defaults are in `av1/common/entropymode.c:844-851`; counter reset is in `av1/common/entropy.c:166-169`.
- A new regression encodes independent channel histories using explicit reference defaults and signed magnitude
  syntax from `av1/encoder/bitstream.c:323-353`. The original decoder failed on the third symbol, returning
  **-1 instead of -2** (`delta-lf-before.trx`, stopped on the first failure).
- Four independent distributions now participate in prototype construction, deep copying, default restoration,
  and frame snapshot counter reset. The existing tile loop passes its parsed multi-delta flag and channel index
  to the symbol reader. No per-symbol owner, allocation, new guard, or rejection policy was introduced.
- Tests cover four color channels, two monochrome channels, shared-delta syntax, disabled CDF adaptation,
  signed escape magnitudes, independent copies, counter reset, and unchanged shared-delta defaults.
  Final Release net11.0 build: zero errors and 1,009 existing warnings; Roslynk: zero compiler errors.
  Serialized Visual Studio VSTest passed **2,095/2,095** in `delta-lf-final.trx` (1.8771 minutes), covering
  entropy tests, frame-context lifecycle tests, and the AV1 reconstruction conformance suite.
- Current libaom normal encoding sets `DEFAULT_DELTA_LF_MULTI` to zero (`av1/common/enums.h:73`,
  `av1/encoder/encodeframe.c:2357`). Existing native output must not be assumed to exercise multi-delta syntax.
  A complete independently authored multi-delta bitstream remains a verification gap. This correction does not
  establish encoder parity or complete decoder correctness. No benchmark was run.

Loop-filter level clipping correction after checkpoint `77f535828`, verified on 2026-09-05:

- Following decoded delta values into deblocking found another numerical defect. At the preceding checkpoint,
  `Av1LoopFilterDecoder.cs:373-390` clipped the reference adjustment before adding the mode adjustment.
  Native `av1/common/av1_loopfilter.c:95-101,182-187` clips their combined result once, both for per-block
  delta-LF and for the precomputed frame-level table. Opposite adjustments must be allowed to cancel before clipping.
- The production-frame regression failed before correction: base level 1, reference delta -63, and mode delta +63
  should retain level 1, but premature clipping produced level 63. An expected sample of 100 became 104 at the first
  differing position (`loop-level-before.trx`); later edge samples also differed. VSTest stopped on that first failure.
  The local runner then failed to print xUnit's Unicode arrows under cp1252. The saved report was inspected and the
  runner's stdout encoding corrected; the failed test was not rerun before the implementation change.
- `Av1LoopFilterDecoder.GetFilterLevel` now adds both adjustments using the scale derived from the original level,
  then clips once. Existing base/delta-LF and segmentation clipping remain in their normative order.
  No allocation, ownership, or syntax policy changed.
- The existing production-frame test and its three expected outputs are retained. Four new cases exercise cancelling
  deltas at both limits, with delta-LF present and absent, through `DecodeFrame` and an independent scalar filter.
  Final Release net11.0 incremental build reported zero errors and zero warnings; the earlier test compilation
  reported 1,009 existing warnings. Roslynk reports zero compiler errors.
- Serialized Visual Studio VSTest passed **11/11** in `loop-level-final.trx` (5.7519 seconds): deblocking scalar/SIMD
  definitions, production-frame delta cases, and native deblocking, CDF-update, profile, and all-intra fixtures.
  The earlier 2,095-case report applies to the preceding checkpoint. No benchmark or separate-encoder parity
  measurement was run, and the complete multi-delta bitstream verification gap remains open.

Motion-controller investigation continued after correction checkpoint `578ec34d9`:

- Managed `Av1IntraSuperblockEncoder.ReferenceModeDecision.cs:1278-1493` uses the same normalized squared-error
  plus complete mode/vector RD cost for integer and fractional candidates. The integer operators at
  `Av1IntraSuperblockEncoder.Operator.cs:466-493,985-1014` compute squared error, not SAD or centered variance.
- Reference `av1/encoder/mcomp.c:72-130,184-238,314-384,644-664` separates full-pixel SAD cost, variance cost,
  SAD-per-bit scaling, error-per-bit scaling, and their motion limits. Its full-pixel dispatcher
  (`mcomp.c:1768-1903`) owns the configured diamond/hexagonal/pattern search and conditional mesh search,
  including downsampled-SAD fallback.
- Reference `av1/encoder/motion_search_facade.c:150-324,347-488` derives the starting search step, considers
  configured start candidates, retains a second full-pixel candidate, prunes repeated dynamic-reference searches,
  and can refine and compare both candidates. Fractional search
  (`mcomp.c:3266-3337`) has configured precision, iteration count, repeated-position tracking, and a second-level
  check. The managed one-ring-per-scale controller does not implement that path.
- The next motion implementation must reconcile configuration, limits, start-candidate lifetime, search costs,
  full-pixel traversal, fractional traversal, and winner publication together. Substituting SAD or variance alone,
  increasing the radius, or adding isolated search points would not establish that contract. No benchmark or
  motion-search implementation change has been made from this follow-up investigation.

- Reference good-quality speed policy is layered rather than a radius lookup. The defaults at
  `av1/encoder/speed_features.c:2353-2364` select NSTEP, full eighth-pixel precision, two subpixel iterations,
  and eight-tap search. Good-quality overrides at `speed_features.c:1248-1250,1308-1312,1370-1409` change
  iteration count, search range, full-pixel and fractional methods, second-candidate refinement, and mesh pruning.
  Resolution-dependent speed-six overrides at `speed_features.c:1029-1076` also select block-size-dependent
  search and reference-candidate pruning. These source observations do not make existing managed effort values
  equivalent to native cpu-used values.
- Encoder intra-edge filtering requires the complete candidate prediction path. Reference
  `av1/common/reconintra.c:958-986,1204-1243,1512-1548` derives neighbour-dependent strength, filters the corner
  and required edges, then upsamples before directional prediction. The decoder already performs these stages
  at `src/ImageSharp/Formats/Heif/Av1/Prediction/Av1PredictionDecoder.cs:915-966`. Encoder luma, chroma, and
  tiled candidates must consume the same filtered-reference contract before the sequence flag can be enabled.
  The decoder's chroma smooth-neighbour test at `Av1PredictionDecoder.cs:1820-1837` does not repeat the native
  inter-block check, but `Av1TileReader.cs:2037-2042` resets every inter block's UV mode to DC. That owning
  invariant prevents stale smooth modes; no redundant guard or numerical defect is justified here.
- Sparse inverse dispatch remains incomplete. Reference
  `av1/common/x86/highbd_inv_txfm_avx2.c:4088-4180` derives separate horizontal and vertical nonzero extents
  from EOB and chooses low-one, low-eight, low-sixteen, or full DCT/ADST axis kernels as applicable.
  Managed `Av1InverseTransformerFactory.cs:47-60,98-112` only distinguishes DC-only DCT from the full lossy
  transform. The DC arithmetic at `Av1Inverse2dTransformer.cs:44-57` retains separate axis scaling and rounding;
  agreement with the managed full path still does not independently establish all native sparse cases.

- [ ] Finish the full production-path and complete upstream-diff audit, including conversion, animation,
  ownership, filters, decoder SIMD, and independent validity of the claimed tests.
- [ ] Reconcile frame configuration and encoder decision policy with the reference before isolated pruning changes.
- [ ] Implement missing tools and complete reference, partition, motion, transform, coefficient, and winner decisions.
- [ ] Compare separately encoded results from identical source samples with explicitly reconciled settings.
  Report maximum absolute error and counts exceeding one for every component and every frame.
  Encoder parity permits at most one component unit per sample; PSNR and average error cannot replace it.
- [ ] Verify byte-exact decoder output against the reference for identical bitstreams and reconciled output conversion.
  Report maximum error and counts of differing samples and bytes. Every count and maximum error must be zero.
- [ ] Verify each final relevant edit with focused serialized Release .NET 11 Visual Studio VSTest and independent
  native production-output checks. Compilation and component tests do not close codec completeness.
- [ ] Run equivalent end-to-end benchmarks only after the relevant source comparison justifies the next change.
  Retain output sizes, absolute times, per-sample errors, memory units, reference configuration, and limitations.
- [ ] Inspect the staged diff before every verified checkpoint commit and exclude all temporary native integration,
  codec sources, binaries, build directories, and generated comparison artifacts. Do not push.

## Source authority

- AV1 codec syntax, tables, fixed-point arithmetic, prediction, transforms, entropy behavior, filters, encoder decisions, and lifecycle behavior must be ported and checked only against the current `main` branch of the official libaom checkout at the verified `D:\GitHub\ynse01\aom-d565eec6-source` export.
- Libaom is the sole external codec implementation source. Do not use HM, libheif, FFmpeg, GPAC, SVT-AV1, dav1d, libgav1, or any other codec implementation as an algorithm, arithmetic, output, or architecture reference.
- Existing ImageSharp and JPEG code is authoritative only for ImageSharp architecture, allocator ownership, SIMD dispatch, pixel conversion, and test API patterns. It is not an alternate AV1 algorithm source.
- Production code must not load, invoke, install, or fall back to a native codec.
- Existing independent container files may be used only as interoperability inputs. Native AV1 expected output must be generated by the current libaom `main` checkout, and no independent decoder output may substitute for it.

Reference checkout evidence refreshed on 2026-09-05:

- The current encoder source comparison uses the official libaom `main` revision `d565eec60f084421fa34fc0534b760c6452b6a6c`, exported at `D:\GitHub\ynse01\aom-d565eec6-source`.
- The earlier generic reference decoder was `D:\GitHub\ynse01\aom-d565eec6-build-generic2\aomdec.exe`; the fresh correction comparisons use the optimized x64 Release build recorded above. Its CMake cache identifies the current source export above, and it reports version 3.15.0. On 2026-09-05 it accepted both frames of each retained-reference sequence at efforts five, seven, eight, and nine. This establishes syntax acceptance for those four streams, not complete interpolation or codec conformance.

## Status notation

- [x] Recorded checkpoint: the associated dated report claims focused verification. Historical marks outside the takeover section have not been accepted by the fresh audit and do not establish current-tree completeness.
- [~] Locally implemented, checkpoint open: production source exists, but current-tree verification is missing or a known audit issue invalidates the checkpoint.
- [ ] Remaining: the production behavior is absent, incomplete, or has not reached its required implementation boundary.

## Current source reconciliation

Reconciled with the worktree on 2026-09-05.

- [~] The bounded container reader, still-image path, sequence parser, AV1 decoder, color pipeline, presentation pipeline, and broad AV1 test suite exist locally.
- [x] The inter-frame decoder has verified checkpoints through inter deblocking decisions and
  reference/mode deltas.
- [~] Loop filtering, CDEF, super-resolution, restoration, film grain, layered presentation, alpha composition, and color conversion exist locally. Shared-source cleanup changed the current tree, so final production-path verification is open.
- [~] AV1 writer primitives, forward transforms, symbol encoding, and tile-writing source are connected to the public encoder for bounded still-image and all-intra sequence AVIF color with optional auxiliary alpha output.
- [~] The public AV1 encoder has local single-image, grid, lossless sequence, and LAST_FRAME lossy sequence paths. Current-tree verification remains open. Additional references, compound prediction, remaining inter tools, orientation handling, and default format registration remain open.
- [x] Patented codec production code, registrations, tests, benchmarks, fixtures, reference outputs, and notices were manually deleted and committed by `78a74d448`.
- [x] Remaining task-created HM, HEVC, libheif, GPAC, Nokia, FFmpeg, Pillow HEIF, libavif-build, and libjpeg-build directories were traced to their creation commands in the recovered Codex session history and deleted on 2026-08-31. The user-provided repositories and all libaom-only source, build, and reference data were left untouched.
- [x] The PNG metadata-suppression fix and three HEIF/AV1 diagnostic-save call-site corrections passed the exact 34 net11.0 ARM CI cases and were committed with the single-reference checkpoint as `54bb6cbe59bd113058854a3ee31448cf61f462ca`. They are infrastructure evidence, not decoder or encoder completion evidence.
- [ ] The complete decoder and encoder release matrix is not complete.

## Immediate execution queue

Historical interpolation-search reports from before the takeover follow. Their timings and test counts apply only to the trees identified by those reports. The takeover audit and required completion gates above determine current work.

- [~] The earlier elementary-stream decoder comparison included parsing, reconstruction, output allocation, RGB conversion, and disposal for both ImageSharp and optimized current-main libaom. Eight-bit Kodak and ten-bit Cosmos inputs match every `Rgb48` sample before timing. Initial warmed means are 15.130 versus 5.423 ms and 27.655 versus 10.246 ms respectively. These expose an open decoder gap; they are not container-load measurements. Reproduction and limitations are recorded in the benchmark README.
- [~] Uncommitted shared inverse reconstruction uses EOB to select a DC-only DCT path, preserving both axis roundings, rectangular normalization, input clamps, and final clipping through the existing semantic output operators. Vector512 output was added to that operator contract. Every transform size, signed boundary, padded separate/in-place destination, and supported sample precision is checked against the full transform. The post-change photographic encoder payload remained byte-identical; decoder RGB output remained exact against libaom. Short decoder measurements do not yet establish a statistically significant improvement.
- [ ] The isolated fixed-8x8 Hadamard screening hook was removed in correction checkpoint `578ec34d9`. The following is a historical experiment, not an active implementation or an accepted improvement. Existing tensor and transpose APIs vectorize the operation over frame-reused scratch; no per-candidate allocation or custom hardware-width operator was introduced. A five-warmup, ten-measurement repeat records 1,363.09 ms versus native 71.73 ms, about 34% faster than the initial managed baseline but still approximately 19x behind native. Output increased slightly to 11.577 KiB and aggregate native-plane PSNR declined from 37.124 to 37.069 dB. This tradeoff does not close the performance/compression gate. Larger-block screening, top-ranked pruning, transform bounds, winner refinement, filter decisions, and allocation attribution remain open. Evidence: `artifacts/BenchmarkDotNet/av1-screen-verified-short-20260905/20260905-140358`.
- [~] The earlier screening/DC/native-profile/moving-color subset passed 25 cases in each of three separately configured VSTest hardware tiers. Normal-path verification passes 84 screening/DC/superblock cases and 165 frame/public encoder cases. The photographic benchmark now additionally requires exact ImageSharp/libaom RGB agreement across all three dependent frames before timing. No expected image or golden output was changed. Release test/benchmark builds have zero errors and their existing 1,009/39 warning baselines. Evidence and the corrected process-local VSTest environment handling are recorded in `tests/ImageSharp.Benchmarks/Codecs/Heif/README.md` and `artifacts/TestResults/av1-screen-20260905`.

- [~] The earlier reference benchmark measured RGB-to-OBU boundaries, including pixel conversion for every frame on both sides. A benchmark-only C adapter links the optimized current-main libaom build in-process and borrows the existing converted plane storage; production remains fully managed. Setup and file I/O are excluded equally. Native quantizer bounds are fixed to the managed base index, with independent speed settings and explicit size/quality reporting. Reproduction, native build provenance, lifetime documentation, and exact output hashes are in `tests/ImageSharp.Benchmarks/Codecs/Heif/README.md`.
- [ ] The corrected benchmark exposes a substantial remaining performance and compression gap. For three photographic 256x256 frames, ImageSharp effort seven takes 2,053.31 ms and writes 11.54 KiB at 37.124 dB aggregate native YUV PSNR; current-main libaom cpu-used six takes 70.67 ms and writes 8.27 KiB at 38.942 dB. Both include RGB conversion and both measured outputs decode to all three complete frames. This is fixed-base-quantizer evidence, not equal-quality evidence. The managed path records 9.38 MiB of managed allocations per operation, which still requires attribution; native memory is not measured by that counter. The Short-run evidence is `artifacts/BenchmarkDotNet/av1-sequence-rgb-fixed-q-short-20260905/20260905-131149`. Power-plan and CPU-query warnings remain documented. The new benchmark files build in net11.0 Release and have no Roslyn compiler/analyzer diagnostics. Do not close the interpolation-performance gate or advance to additional reference tools until the gap is addressed.
- [x] The requested in-progress tree was committed as `433afd1a9` before further encoder work. That commit is a checkpoint, not a claim of completed interpolation or codec delivery.
- [x] The subsequent partition/interpolation/lossless correction was committed as `7cf7fc4` after the 225-case affected encoder run and exact current-main native comparison.
- [x] Odd-sized color sequence verification exposed and corrected two further production defects. Empty inter luma transforms now retain the inferred DCT type before chroma inherits it; normalizing only during writing was too late. Region-major coefficient writing now rounds chroma end coordinates in 4x4 units, preserving the shared minimum chroma transform on sub-8x8 partitions instead of truncating it away. Both rules match current libaom's transform-type inference and `av1_write_intra_coeffs_mb` region bounds. The corrections add no allocation or sample copy.
- [x] All 12 moving-color sequence cases pass with SIMD enabled and disabled: 8/10/12-bit 4:2:0 at effort eight, and all three bit depths across 4:2:0/4:2:2/4:4:4 at effort nine. The 23x19 sources require actual inter motion and subsampled chroma phases. Current-main libaom decodes all 24 emitted frames with exact native Y/U/V equality. Evidence: `artifacts/TestResults/av1-interpolation-color-sequence-20260905/color-sequence-r3.trx`, `color-sequence-scalar-r3.trx`, and the adjacent raw plane outputs under `tests/Images/ActualOutput/Heif/Av1/SequenceEncoderPreservesNativeColorPlanesWithSubpixelMotion`. The affected public/frame/superblock set passes 237 cases; the expanded encoder/entropy set passes 2,524, with no failures or skips. Evidence: `artifacts/TestResults/av1-interpolation-partition-broad-20260905/color-broad-r3.trx` and `color-encoder-entropy-r3.trx`. The final Release build and Roslyn compiler/analyzer passes have no errors. This closes the identified subsampled inter syntax/reconstruction gaps, not the remaining end-to-end performance and complete codec release matrix.
- [x] Production interpolation verification now forces Smooth and Sharp at effort eight, both dual-filter axis orders at effort nine, and native 10/12-bit two-axis half-sample motion. The tests assert actual retained filter symbols, vectors, exact reconstruction, and no allocator rent during tile coding. The sequence fixture now uses the retained-reference decode contract; the still-image buffer-transfer API intentionally releases the reference map and cannot decode dependent samples in succession.
- [x] This verification exposed a live partition traversal defect: after search changed an earlier node's child count, a later node could consume an unrelated entry from the initial flat 8x8 skeleton. Unsearched intra partitions now derive their default from the current block size, preserving the geometry-driven traversal used by current libaom. Wide and tall lossless regressions cover clipped parents and superblock boundaries at efforts nine and ten.
- [x] The affected frame encoder, intra-superblock encoder, and public HEIF encoder set passes all 225 cases on the corrected tree with zero failures or skips. Evidence: `artifacts/TestResults/av1-interpolation-partition-broad-20260905/partition-broad-r3.trx`. This is the affected encoder surface, not the full codec release matrix.
- [x] Lossless tiled luma and chroma search no longer evaluate unsignaled angle deltas on 4x8/8x4 coding blocks; tiled chroma also respects the ordinary effort limits. Partition trials publish lossless chroma coefficient contexts at 4x4 transform granularity. The extended native RGB-plane regression exposed the chroma angle defect as a real lossless mismatch reproduced by libaom, and passes after the correction without changing expected samples.
- [x] All 20 focused partition/interpolation/lossless cases pass with hardware intrinsics enabled and all 20 pass with them disabled through serialized net11.0 Release Visual Studio VSTest with stop-on-failure. Current-main libaom `d565eec60f084421fa34fc0534b760c6452b6a6c` decodes all 20 emitted streams (28 frames) with exact native-plane equality against the retained reconstruction or lossless source planes. Evidence: `artifacts/TestResults/av1-interpolation-partition-interpolation-20260905/partition-interpolation-r3.trx`, `partition-interpolation-scalar-r3.trx`, and `artifacts/av1-partition-interpolation-net11-20260905-r3.log`. Roslyn compiler and analyzer passes report no errors or changed-file warnings. Subsampled inter-plane coverage, broader current-tree verification, and end-to-end performance remain open.
- [x] The 8x8 single-candidate SAD, four-candidate SAD, and variance paths now share closed-generic traversal in `Av1ResidualBuilder`. Its existing byte/ushort residual operators own scalar and SIMD arithmetic; the superblock operators no longer duplicate these row loops or hardware dispatch. The four-candidate path retains one source load/conversion per row, exact eight-sample loads preserve final-row bounds, and both variance moments retain native precision until the existing normalization boundary. Six known-result cases cover 8/10/12-bit signed extrema, distinct source/prediction rows and strides, unaligned starts, exact final-row lengths, and four-candidate output order. Together with nine existing intra-block-copy cases and the extended zero-allocation test, all 16 pass with hardware intrinsics enabled and all 16 pass with them disabled. All 58 public HEIF encoder cases also pass. Verification used serialized net11.0 Release Visual Studio VSTest with stop-on-failure; evidence is `artifacts/TestResults/av1-search-metrics-20260905/search-metrics-r2.trx`, `search-metrics-scalar-r2.trx`, and `search-metrics-encoder-r2.trx` in that directory. The exact Release build has zero errors and the existing 1,009 warnings; Roslyn reports no diagnostics in the changed files. This verifies the search-metric refactor, not the remaining interpolation conformance or end-to-end performance work.
- [~] Effort eight searches the common regular, smooth, and sharp interpolation families; efforts nine and ten enable independent vertical/horizontal filter selection. Lower efforts retain the fixed regular-filter path.
- [~] Filter ranking follows the curve-fit prediction-error model in the official `d565eec60f084421fa34fc0534b760c6452b6a6c` source, before full transform search. The existing rate-distortion type owns the static model tables and paired portable SIMD cubic evaluation. Visible-plane SSE uses the existing SIMD residual reduction, including native-bit-depth normalization and cropped edges.
- [~] Candidate and retained prediction views alternate within the existing inter workspace. At most one normalization copy per active plane retains the chosen predictor for transform search; no new pixel owner, coefficient owner, per-block rent, or reconstructed-frame copy is introduced. Zero-phase axes retain only the cheapest signaled filter instead of repeating equivalent prediction trials.
- [~] The selected filters, skip flags, segment, and primary reference fit the original seven-byte block-mode record and eight-byte macroblock record. Filter costing and writing use the live tile CDFs. Encoder and decoder share the context-combination mapping, and encoder search and writing share filter-symbol eligibility.
- [~] The earlier 67-case net11.0 Release set covered model curve samples and skip decisions, 8/10/12-bit quantizer normalization, exact entropy bytes and live adaptation, packed-field independence, tile-boundary contexts, syntax eligibility, and sequence-header signaling. Its 12 model cases also passed with hardware intrinsics disabled. The four flat retained-reference sequences were accepted by current libaom but did not force non-regular filters. The production, allocation, and exact native-plane evidence above extends that coverage; subsampled inter planes and end-to-end timing remain required before closing this checkpoint.
- [x] Focused runtime verification exposed an invalid low-effort inter-frame header: `force_integer_mv` was set while screen-content tools were disabled, making the writer omit the high-precision flag that a conforming reader expects. The frame encoder now retains the inferred false flag and controls integer-only search through the existing effort boundary. All four retained-reference cases assert the parsed precision, quantizer, and filter fields and decode both frames; current libaom accepts the same saved streams.
- [x] The preceding interpolation checkpoint's affected net11.0 Release run passed 2,494 cases with zero failures or skips through one serialized Visual Studio VSTest process with stop-on-failure enabled. It combined 2,269 entropy cases, 167 frame/superblock/transform/picture-storage cases, and 58 public HEIF encoder cases. That build had zero errors and the existing 1,009 test-project warnings, with none in the changed files. Evidence: `artifacts/TestResults/av1-interpolation-20260905/verified-encoder-entropy-r10.trx` and `artifacts/av1-interpolation-net11-release-20260905-r10.log`. This predates the search-metric refactor above and is not the complete current-tree decoder/encoder release matrix.
- [x] Allocation regressions now reflect the implemented lifetimes instead of the former layout: all seven trailing tile-state integers are proved contiguous within the picture's second allocator owner; coefficient level, context, and output owners are proved allocated at construction, reused across costing, writing, finalization, and frame resets, and returned exactly once. The two-owner picture and three-owner symbol-encoder limits are retained.
- [x] Grid regressions use valid 4:2:2/4:2:0 syntax and the inferred monochrome subsampling flags used by production configuration. Odd-dimension and undersized-cell failures assert their grid-specific messages, so malformed AV1 fixtures or an earlier configuration mismatch cannot satisfy those tests. Smaller right/bottom color and alpha cells, separate primary roots, lossless sequences, metadata, and public precision/sampling cases pass in the 58-case set above.

Work must proceed in this order. Do not skip to a later item while an earlier checkpoint is open.

### 1. Finish and verify the AV1-only cleanup

- [x] Remove production types, registrations, constants, parser branches, properties, tests, benchmarks, fixtures, reference outputs, notices, and documentation for removed codec work.
- [x] Remove downloaded non-libaom reference source, tools, generated outputs, and local installations.
- [x] Retain the official current-main libaom checkout and libaom-only build artifacts required for AV1 verification.
- [x] Retain user-supplied AV1 fixtures and their recorded expected outputs.
- [x] Audit production source, tests, benchmarks, assets, project files, notices, and documentation for stale removed-code references.
- [x] The cleanup and cICP tree built in Release for net10.0 and net11.0 with restore disabled, build servers disabled, and one MSBuild node.
- [x] The exact 34 net11.0 ARM CI failures pass after the cICP correction, and the subsequent single-reference checkpoint set passes on net10.0 and net11.0.
- [x] Roslynk, scoped StyleCop, whitespace, and `git diff --check` accepted the cleanup and cICP checkpoint.
- [x] The cleanup and cICP evidence was recorded and committed with the single-reference checkpoint.

Historical cleanup evidence from 2026-08-30, retained with its limitation:

- Release source builds passed for net10.0 and net11.0 with zero warnings and zero errors. Both builds used `--no-restore`, `--disable-build-servers`, and one MSBuild node.
- The focused net10.0 HEIF decoder, encoder, metadata, sequence-parser, and AV1 reconstruction set passed 221 of 221 tests with zero failures and zero skips. It did not execute the net11.0 diagnostic-save path that later failed in CI.
- The Roslyn compiler and configured StyleCop analyzers accepted the changed production source. Roslynk's `open_solution` entry point was attempted separately but failed before returning a solution handle, so no Roslynk result is claimed.
- The tracked-source text and filename audit found no removed-code references outside the unchanged repository and shared-infrastructure `.gitattributes` patterns. A later history reconstruction found ignored task-created reference directories that this audit missed; those directories were deleted on 2026-08-31.
- `git diff --check` passed and neither `.gitattributes` file changed.

Current cICP failure correction evidence from 2026-08-31:

- The failure was not decoded HEIF metadata. `PngEncoderCore.WriteCicpChunk` ignored `PngChunkFilter.ExcludeAll`, so diagnostic PNG saves attempted to write a non-identity source matrix that PNG cannot represent.
- `PngEncoderCore` now honors the existing `SkipMetadata` contract for cICP, and the three affected HEIF/AV1 diagnostic saves explicitly use `PngEncoder { SkipMetadata = true }`. Actual comparisons and decoded-image metadata assertions remain unchanged.
- The direct embedded-ICC case and every row of the 12-case profile matrix passed: 13 of 13 net11.0 Release cases.
- The exact 34 cases reported by CI passed: 34 of 34 net11.0 Release cases, with zero failures and zero skips.
- Roslynk reported zero compiler errors after the fix, and `git diff --check` passed.
- The remaining four branch-introduced `GC.AllocateUninitializedArray` calls are removed from AV1 configuration, pixel-information, XMP, and Exif ownership boundaries. Each retained value still receives exactly one array and one copy because its source span belongs to pooled storage; no second materialization was introduced. The exact net11 Release rebuild remains at 1,005 warnings and zero errors, 166 focused configuration and metadata cases pass, and all 9,181 HEIF tests pass through direct VSTest.

Recovered task-history evidence from 2026-08-31:

- The primary session beginning on 2026-08-24 was reopened from task ID `01a03239-831b-7831-84e7-7f6947279ccb`: 96,777 records, 295 turn contexts, 211 compactions, 190 user messages, 1,920 assistant messages, and 13,671 tool calls.
- The continuation beginning on 2026-08-27 was reopened from task ID `01a04314-f1c6-7133-b1bc-5c74a94dd714`: 61,129 records at the audit point, 166 turn contexts, 96 compactions, 223 user messages, 1,113 assistant messages, and 8,942 tool calls.
- The restored first session records the user selecting official AOM/libaom as the AV1 source after the ImageSharp discussion was inspected. It does not authorize another codec implementation as an AV1 source and does not authorize importing a patented codec.
- The restored tool calls identify the exact creation commands for the non-libaom source, tool, and output directories removed on 2026-08-31. No directory was selected for deletion from its name alone.
- The recovered Git sequence establishes that `78a74d448` removed the patented codec implementation and `92fa7a8ca` merged the later upstream ImageSharp changes. The current branch and worktree, not an older summary, remain authoritative.

### 2. Correct the single-reference inter-frame checkpoint

The checkpoint is complete through `c4b4e4e0386328dea574a884b6fa36c360ad5a9b`. It replaces frame-sized palette maps with fixed decoder-session scratch, reconstructs each superblock before reusing that scratch, and passes the ownership, documentation, full AV1 test, and Release source-build gates on both target frameworks.

- [x] Reconcile interpolation-filter syntax in `Av1TileReader` with current libaom `main`.
  - Current libaom `av1_is_interp_needed` calls `is_nontrans_global_motion`, whose loop rejects only `TRANSLATION`. Identity GLOBALMV therefore omits switchable-filter symbols.
  - Current `Av1TileReader` uses the same non-Translation classification. The existing Identity test leaves sentinel filter symbols unread, while the Translation test consumes them.
  - No production change is required. The focused test describes only the syntax behavior it proves.
- [x] Reconcile both spatial single-reference extension loops in `Av1ReferenceMotionVectors` with current libaom `main`.
  - Current libaom `setup_ref_mv_list` stops both loops at `MAX_MV_REF_CANDIDATES`, which is two. `MAX_REF_MV_STACK_SIZE`, which is eight, is the stack capacity used by the earlier direct and temporal candidate collection; it is not the stop condition for these two extension loops.
  - Current `Av1ReferenceMotionVectors` uses the same two-entry stop condition and retains an eight-entry stack for earlier candidates and DRL selection.
  - No production change is required. This remains spatial single-reference extension, not temporal extension.
- [x] Establish and enforce the contiguous frame-plane invariant used by `Av1FrameBuffer` and inter reconstruction.
  - One ImageSharp allocator owner now contains the aligned Y, U, and V storage, matching libaom's frame-buffer ownership while non-owning `Buffer2D` views preserve ImageSharp's row API. Coded dimensions are aligned to eight samples, the luma stride is aligned to 32 samples, and chroma strides and heights are derived from that luma layout exactly once. A 4K eight-bit 4:2:0 frame owner occupies about 17.3 MiB.
  - The single owner removes the previous three-rent constructor and its allocation-cleanup `try/catch`. `Av1FrameBuffer` rejects external geometry whose complete aligned frame reaches the contiguous `int.MaxValue` boundary before allocation, making every direct `DangerousGetSingleSpan` call an enforced owner invariant.
  - `ConstructorRequestsContiguousPaddedPlanes` proves that a frame larger than the allocator's group capacity remains one group. `ConstructorUsesOneFrameOwnerForAllPaddedPlanes` proves exact one-rent Y/U/V ownership and exactly-once return. `ConstructorRejectsPaddedPlaneThatCannotBeContiguous` proves that an unrepresentable frame is rejected before allocation, and the high-bit-depth stride regression proves the 608-sample libaom layout for a three-pixel coded row.
  - The complete HEIF/AV1 namespace passes 8,808 of 8,808 direct net11 VSTest cases in Release after the physical layout change. The production path performs no plane copy and no per-block, per-row, or per-scanline allocation.
- [x] Prove the real `Av1BlockDecoder.DecodeBlock` inter-reconstruction branch.
  - Decode the progressive dependent-frame fixture through the complete public production path.
  - Compare the final frame's native Y, Cb, and Cr planes exactly with current-main libaom output.
  - Compare the final presented image through the established ImageSharp reference-image comparison API.
  - Do not substitute an internal helper test, fake tile reader, non-zero assertion, custom pixel loop, or tolerant comparison.
- [x] Prove motion-field ownership and lifetime after the current reconstruction-timing change.
  - Track initialization, retained-slot aliases, failure unwinding, presentation ownership, decoder-result ownership, and final disposal.
  - Every allocator-owned object must be returned exactly once.
- [x] Correct stale documentation for the current worktree.
  - Av1InterFrameModeInfoTests must describe the behavior it actually proves.
  - Do not claim production reconstruction, constrained allocation, ownership, or reference-stack coverage unless the test executes that contract.

Checkpoint gate:

- [x] Default Identity-omission and Translation-consumption GLOBALMV syntax cases pass in the focused current-tree run.
- [x] Two-entry spatial single-reference extension passes; current-main source inspection confirms the separate eight-entry overall stack capacity and DRL access.
- [x] The exact dependent-frame native-plane comparison passes.
- [x] The established exact presentation comparison passes.
- [x] Normal, AVX-512-disabled, AVX-disabled, and scalar FeatureTestRunner configurations pass where supported.
- [x] Constrained allocation preserves the enforced single-group plane invariant without copying or per-block allocation.
- [x] Motion-field allocation tracking is balanced across success and failure on net10.0 and net11.0.
- [x] Release source builds pass for net10.0 and net11.0 with zero warnings and zero errors.
- [x] The complete AV1 namespace passes 8,732 of 8,732 tests on net10.0 and net11.0 with zero failures or skips.
- [x] Roslynk reports zero compiler errors; scoped analyzer inspection reports no diagnostics introduced by the current changes; `git diff --check` passes.
- [x] The completed checkpoint was committed as `54bb6cbe59bd113058854a3ee31448cf61f462ca` with author and committer `James Jackson-South <james_south@hotmail.com>`.
- [x] The palette-memory follow-up was committed as `c4b4e4e0386328dea574a884b6fa36c360ad5a9b` with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified single-reference checkpoint evidence on 2026-08-31:

- The current-main `aomdec` was rebuilt directly from `D:\GitHub\AOMediaCodec\aom` and identified itself as `3.15.0-13-g441c439b99`.
- Decoding the 72-byte progressive payload with `--all-layers`, one thread, and row multithreading disabled produced 2,178 YUV444 color samples. All samples in both layers match the first three planes of the stored YUV444-alpha reference exactly.
- `DecodeProgressiveSingleMatchesReference` executes the production decoder through FeatureTestRunner and compares the complete presented `Rgba32` image with `CompareToReferenceOutput(ImageComparer.Exact, provider)`. The redundant manual alpha loop was removed.
- `DecodeProgressiveSingleWithConstrainedAllocator` executes the same production reconstruction with a 1,024-byte allocator group capacity and verifies that every allocation is returned exactly once.
- `MotionFieldsFollowAliasesAndPresentationOwnership`, `MotionFieldAllocationFailureUnwindsTileReaderOwnership`, `DecodeProgressiveSingleTracksMotionFieldOwnership`, and the reference-store replacement, reset, and transfer tests cover initialization, aliases, presentation ownership, decoder-result ownership, failure unwinding, repeated disposal, and exactly-once final returns in the current worktree.
- The current worktree passes the four-case palette set, seven-case ownership set, and 29-case syntax, plane, and production reconstruction set on both target frameworks. The complete AV1 namespace passes 8,732 of 8,732 tests on net10.0 and net11.0 with zero failures or skips.
- Release source builds passed for net10.0 and net11.0 with zero warnings and zero errors.
- Roslynk reported zero compiler errors. The scoped changed-file analyzer inspection reported no StyleCop diagnostics attributable to this checkpoint; its only remaining match is the pre-existing xUnit cancellation warning in an unrelated `HeifDecoderTests` method.
- `git diff --check` passed, and neither `.gitattributes` file changed.

Exact verification commands, run directly in the foreground from `D:\GitHub\ynse01\ImageSharp`:

```powershell
$env:MSBUILDUSESERVER = '0'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:DOTNET_CLI_HOME = 'D:\GitHub\ynse01\ImageSharp\.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_DbgEnableMiniDump = '0'
$env:COMPlus_DbgEnableMiniDump = '0'
$env:DOTNET_EnableCrashReport = '0'
$env:COMPlus_EnableCrashReport = '0'

$heifCheckpointFilter = 'FullyQualifiedName~Av1InterFrameModeInfoTests.ReadInterFrameModeInfoReadsInterpolationFilters|FullyQualifiedName~Av1InterFrameModeInfoTests.IdentityGlobalMotionOmitsInterpolationFilters|FullyQualifiedName~Av1ReferenceMotionVectorsTests.BuildReversesOppositeDirectionExtensionCandidate|FullyQualifiedName~Av1FrameBufferTests|FullyQualifiedName~Av1ReferenceFrameStoreTests.MotionFieldsFollowAliasesAndPresentationOwnership|FullyQualifiedName~Av1ReferenceFrameStoreTests.MotionFieldAllocationFailureUnwindsTileReaderOwnership|FullyQualifiedName~Av1ReferenceFrameStoreTests.PartialReplacementPreservesSharedOwner|FullyQualifiedName~Av1ReferenceFrameStoreTests.FinalReplacementReleasesDisplacedOwner|FullyQualifiedName~Av1ReferenceFrameStoreTests.ResetReleasesUniqueOwnersAndClearsSlots|FullyQualifiedName~Av1ReferenceFrameStoreTests.TakeOutputTransfersPlanesAndReleasesOtherReferences|FullyQualifiedName~Av1ReconstructionConformanceTests.DecodeProgressiveSingleMatchesReference|FullyQualifiedName~Av1ReconstructionConformanceTests.DecodeProgressiveSingleWithConstrainedAllocator|FullyQualifiedName~Av1ReconstructionConformanceTests.DecodeProgressiveSingleTracksMotionFieldOwnership'

dotnet build src\ImageSharp\ImageSharp.csproj -c Release -f net10.0 --no-restore --disable-build-servers -m:1 --no-incremental --nologo --verbosity:minimal
dotnet build src\ImageSharp\ImageSharp.csproj -c Release -f net11.0 --no-restore --disable-build-servers -m:1 --no-incremental --nologo --verbosity:minimal
dotnet test tests\ImageSharp.Tests\ImageSharp.Tests.csproj -c Release -f net10.0 --no-restore --disable-build-servers -m:1 --filter $heifCheckpointFilter --logger 'console;verbosity=minimal'
dotnet test tests\ImageSharp.Tests\ImageSharp.Tests.csproj -c Release -f net11.0 --no-restore --disable-build-servers -m:1 --filter $heifCheckpointFilter --logger 'console;verbosity=minimal'
```

```powershell
$aomVcVars = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\VC\Auxiliary\Build\vcvars64.bat'
$aomCmake = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
$aomEnvironment = & cmd.exe /d /s /c "`"$aomVcVars`" >nul && set"
foreach ($aomEntry in $aomEnvironment)
{
    $aomParts = $aomEntry -split '=', 2
    if ($aomParts.Length -eq 2)
    {
        [Environment]::SetEnvironmentVariable($aomParts[0], $aomParts[1], 'Process')
    }
}

& $aomCmake --build artifacts\reference\aom-generic --target aomdec --config Release --parallel 1
& 'artifacts\reference\aom-generic\aomdec.exe' --codec=av1 --rawvideo --all-layers --threads=1 --row-mt=0 --output='artifacts\reference\aom-generic\progressive-current-main-all-layers.yuv' 'tests\Images\Input\Heif\Av1\Conformance\libavif-progressive-draw-points-8b.bit'
```

### 3. Reverify downstream inter prediction in recorded order

The single-reference syntax, buffer, reconstruction, and ownership foundation is verified by `54bb6cbe59bd113058854a3ee31448cf61f462ca`. Reverify the existing downstream implementations in this exact order, treating each as locally implemented but unverified until its current-main evidence is recorded.

- [x] Compound reference selection, paired reference-MV derivation, and equal averaging.
- [x] Inter-intra prediction.
- [x] Distance-weighted compound prediction.
- [x] Wedge compound prediction.
- [x] Difference-weighted compound prediction.
- [x] OBMC.
- [x] Scaled-reference prediction.
- [x] Local warped prediction.
- [x] Non-translational global prediction.
- [x] Inter deblocking decisions and reference/mode deltas.

Verified equal-average compound checkpoint evidence on 2026-08-31:

- [x] Refreshed the clean official libaom `main` checkout and audited the observed revision
  `441c439b9916474cac15d2822af47a9ad70674a8`. Reference selection and compound mode syntax match
  `read_comp_reference_type` and `read_ref_frames` in `av1/decoder/decodemv.c`; contexts match
  `av1/common/pred_common.c`; paired reference-MV construction and eight-entry extension match
  `process_compound_ref_mv_candidate` and `setup_ref_mv_list` in `av1/common/mvref_common.c`.
- [x] Audited equal-average reconstruction against `av1/common/convolve.c` and
  `av1/common/convolve.h`. Corrected the unscaled 10/12-bit translational path so both references
  retain libaom's no-round compound intermediates until the sole final average and clipping step,
  including the larger first-round shift required for 12-bit horizontal intermediates.
- [x] Added descending Vector512, Vector256, Vector128, and scalar high-bit-depth traversal to the
  existing semantic compound-prediction operator families. No per-block, per-row, or per-scanline
  allocation or copy was added.
- [x] Added FeatureTestRunner coverage for 10/12-bit copy, horizontal, vertical, and separable
  subpixel prediction at widths 9, 17, 33, and 65, with an independent no-round bilinear oracle,
  row-padding sentinels, and explicit scalar comparison.
- [x] Added a complete `Av1BlockDecoder.DecodeBlock` 10/12-bit half-sample regression whose expected
  result comes from the scalar no-round pipeline. The selected vector differs by one sample from the
  obsolete round-each-reference behavior, so the test proves the production branch selection.
- [x] Refreshed the official libaom `main` remote immediately before verification and decoded the
  fixture's 5,465-byte AV1 `mdat` payload with current `aomdec`, one thread and row threading
  disabled. All 19 frames decoded; the final 19,200 YUV444 samples have SHA-256
  `E79D2F49C260B1AC9B1B9BBBB2D611126AFD3B241DA389EB9E7BD4EA0ED42080` and match the retained native
  reference with zero differing samples.
- [x] The real 19-frame production sequence requires decoded equal-average compound blocks, compares
  the final native Y, U, and V planes exactly, compares final RGBA presentation through ImageSharp's
  established reference-output API, and repeats the complete decode with a 1,024-byte constrained
  tracked allocator and exactly-once return checks.
- [x] The focused Release checkpoint set passes 31/31 on net10.0 and 31/31 on net11.0, with zero
  failures or skips. Scoped analyzer and whitespace verification pass for every changed C# file,
  Roslynk reports zero compiler errors and no diagnostics in the changed files, and `git diff --check`
  passes. `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `4075a0844836e863a93cb2e2f3ca42d202c7df1b`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified inter-intra checkpoint evidence on 2026-08-31:

- [x] Audited syntax against current libaom `av1/decoder/decodemv.c` and
  `av1/common/blockd.h`. ImageSharp applies the same sequence enable, skip-mode, block-size, and
  single-reference gates, reads the same four-mode CDF, and reads wedge syntax only within libaom's
  wedge-supported `BLOCK_8X8` through `BLOCK_32X32` range.
- [x] Audited reconstruction against `ii_weights1d`, `ii_size_scales`,
  `build_smooth_interintra_mask`, and `combine_interintra` in current
  `av1/common/reconinter.c`. The ImageSharp weights, plane-size scaling, smooth-mask direction,
  complemented destination orientation, wedge sign, subsampling, and final 6-bit blend match. No
  production change was required.
- [x] The mask tests cover all four inter-intra modes, complemented orientation, row-padding
  sentinels, and the 32-wide curve. FeatureTestRunner covers byte and high-bit-depth selectable
  blending under SIMD and scalar dispatch, and complete `Av1BlockDecoder.DecodeBlock` tests execute
  smooth inter-intra reconstruction at 8, 10, and 12 bits.
- [x] Extracted the fixture's 5,327-byte AV1 `mdat` payload and decoded it with the refreshed current
  libaom `aomdec`, using one thread with row threading disabled. All 19 frames decoded. The final
  19,200 YUV444 samples have SHA-256
  `E8B776C2751DC30CA838931A4B74535FC6E681179568A1278747A38CFF2E5BFA` and match the retained
  native reference with zero differing samples.
- [x] The real production sequence requires both smooth and wedge inter-intra blocks, compares the
  final native Y, Cb, and Cr planes exactly, and compares final RGBA presentation through
  ImageSharp's established reference-output API. Its constrained 1,024-byte tracked-allocator run
  proves motion-field allocation and exactly one return for every allocation.
- [x] The focused Release checkpoint set passes 50/50 on net10.0 and 50/50 on net11.0, with zero
  failures or skips. Scoped analyzer and whitespace verification pass for both changed C# files.
  Roslynk reports zero compiler errors and no diagnostics in the changed files, `git diff --check`
  passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `18b1c881271a3494489ca6f410ab140544902e2d`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified distance-weighted compound checkpoint evidence on 2026-08-31:

- [x] Audited reference-distance quantization against `quant_dist_weight` and
  `quant_dist_lookup_table` in current libaom `av1/common/common_data.h`, and audited order-hint
  distance selection and forward/backward reference assignment against
  `av1_dist_wtd_comp_weight_assign` in `av1/common/reconinter.c`.
- [x] Audited reconstruction against current libaom `av1/common/convolve.c`. Corrected the production
  10/12-bit subpixel path, which incorrectly finalized its two no-round compound intermediates with an
  equal average instead of the signaled distance weights. The fixed path applies libaom's 4-bit weighted
  shift before bias removal, final rounding, and clipping.
- [x] Added descending Vector512, Vector256, Vector128, and scalar traversal to the existing semantic
  distance-weighted intermediate predictor family. Unsigned widening preserves the biased 12-bit
  intermediate range. No per-block, per-row, or per-scanline allocation or copy was added.
- [x] Added FeatureTestRunner coverage for every current-libaom distance-weight class in both reference
  orders, and for 10/12-bit copy, horizontal, vertical, and separable subpixel prediction at widths 9,
  17, 33, and 65, with an independent no-round oracle and row-padding sentinels.
- [x] Added a complete `Av1BlockDecoder.DecodeBlock` 10/12-bit half-sample regression that selects the
  13:3 distance weights through real order hints. Its first reconstructed sample differs from the old
  equal-average result, so the test proves the corrected production branch is executed.
- [x] Extracted the fixture's 5,372-byte AV1 `mdat` payload and decoded it with the refreshed current
  libaom `aomdec`, using one thread with row threading disabled. All 19 frames decoded. The final 19,200
  YUV444 samples have SHA-256
  `E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match the retained native
  reference with zero differing samples.
- [x] The real 19-frame production sequence requires decoded distance-weighted compound blocks, compares
  the final native Y, Cb, and Cr planes exactly, compares final RGBA presentation through ImageSharp's
  established reference-output API, and repeats the complete decode with a 1,024-byte constrained
  tracked allocator and exactly-once return checks.
- [x] The focused Release checkpoint set passes 44/44 on net10.0 and 44/44 on net11.0, with zero failures
  or skips. Scoped analyzer and whitespace verification pass for every changed C# file. Roslynk reports
  zero compiler errors and no diagnostics in the changed files, `git diff --check` passes, and
  `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `7e2de7a2c25852acc374b17936a1a644464f77f3`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified wedge compound checkpoint evidence on 2026-08-31:

- [x] Audited mask generation against current libaom `tools/gen_wedge_masks_data.py` and
  `av1/common/reconinter.c`, including the master prototypes, direction transforms, block-size
  codebooks, sign flips, offsets, and luma/chroma mask sampling. ImageSharp's generated masks match
  those definitions; only stale “pinned” documentation required correction.
- [x] Audited reconstruction against current libaom `aom_dsp/blend_a64_mask.c`. The high-bit-depth
  d16 path applies the Q6 mask to both no-round intermediates before bias removal, the sole final
  rounding step, and clipping.
- [x] Corrected the production high-bit-depth intermediate eligibility gate, which admitted only
  equal-average blocks and made the distance-weighted and wedge no-round finalizers unreachable.
  Average, distance-weighted, and wedge subpixel blocks now retain both intermediates until their
  signaled finalizer; difference-weighted blending remains excluded for its next ordered checkpoint.
- [x] Added high-bit-depth traversal to the existing semantic mask-blend predictor and readonly
  operator family with descending Vector512, Vector256, Vector128, and scalar dispatch. Unsigned
  widening preserves the biased 12-bit intermediate range. No per-block, per-row, or per-scanline
  allocation or copy was added.
- [x] Extended FeatureTestRunner coverage with an independent Q6 mask oracle across 10/12-bit copy,
  horizontal, vertical, and separable subpixel prediction, widths 9, 17, 33, and 65, all mask weights
  from 0 through 64, and row-padding sentinels. A complete `Av1BlockDecoder.DecodeBlock` regression
  verifies the current-libaom 8x8 wedge mask and the production no-round branch.
- [x] Extracted the fixture's 5,374-byte AV1 `mdat` payload and decoded it with refreshed current
  libaom `aomdec`, using one thread with row threading disabled. All 19 frames decoded. The final
  19,200 YUV444 samples have SHA-256
  `E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match the retained native
  reference with zero differing samples.
- [x] The real 19-frame production sequence requires both wedge-mask orientations, compares final
  native Y, Cb, and Cr planes exactly, compares final RGBA presentation through ImageSharp's
  established reference-output API, and repeats the complete decode with a 1,024-byte constrained
  tracked allocator and exactly-once return checks.
- [x] The focused Release checkpoint set passes 35/35 on net10.0 and 35/35 on net11.0, with zero
  failures or skips. Scoped analyzer and whitespace verification pass for every changed C# file.
  Roslynk reports zero compiler errors, `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `9883a24dc319e16b471f68be632d4f62f2c1cd5e`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified difference-weighted compound checkpoint evidence on 2026-08-31:

- [x] Audited syntax against current libaom `av1/decoder/decodemv.c`. ImageSharp applies the same
  masked-compound enable and block-size gates, selects difference-weighted compound directly when wedge
  is unavailable, and reads the same one-bit type-38 mask orientation.
- [x] Audited mask generation and reconstruction against current libaom `av1/common/reconinter.c` and
  `aom_dsp/blend_a64_mask.c`. The d16 path rounds the absolute intermediate difference by the
  convolution and bit-depth shift, scales it by 1/16, adds the type-38 base, clamps or inverts the mask,
  and then blends the original no-round intermediates before final rounding and clipping. Chroma reuses
  the luma-derived mask through rounded subsampling.
- [x] Corrected the production 10/12-bit subpixel eligibility gate, which previously rounded both
  references before difference-mask construction and blending. Difference-weighted blocks now use the
  existing semantic intermediate mask-builder and mask-blend predictor/operator families through the
  sole final rounding step. No new operator family, per-block allocation, or copy was introduced.
- [x] Renamed the stale “pinned formula” test and extended FeatureTestRunner's independent oracle across
  current-libaom regular and d16 mask arithmetic, both mask orientations, 8/10/12-bit samples, widths
  that cross every Vector512, Vector256, Vector128, and scalar boundary, subpixel phases, and row-padding
  sentinels.
- [x] Added a complete `Av1BlockDecoder.DecodeBlock` regression for 10/12-bit half-sample prediction
  and both type-38 orientations. Its expected mask and reconstruction are calculated directly from the
  current-libaom equations, independently of the production mask builder and finalizer.
- [x] Extracted the fixture's 5,358-byte AV1 `mdat` payload and decoded it with refreshed current
  libaom `aomdec`, using one thread with row threading disabled. All 19 frames decoded. The final
  19,200 YUV444 samples have SHA-256
  `E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match the retained native
  reference with zero differing samples.
- [x] The real 19-frame production sequence requires both difference-mask orientations, compares final
  native Y, Cb, and Cr planes exactly, compares final RGBA presentation through ImageSharp's established
  reference-output API, and repeats the complete decode with a 1,024-byte constrained tracked allocator
  and exactly-once return checks.
- [x] The focused Release checkpoint set passes 37/37 on net10.0 and 37/37 on net11.0, with zero
  failures or skips. Scoped analyzer and whitespace verification pass for every changed C# file.
  Roslynk reports zero compiler errors, `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `fb4c64474e1ced4067a42731384f3b5ad4212a2f`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified OBMC checkpoint evidence on 2026-08-31:

- [x] Audited motion-mode syntax against current libaom `av1/decoder/decodemv.c`,
  `av1/common/blockd.h`, `av1/common/reconinter.c`, `av1/common/obmc.h`, and
  `av1/common/reconinter_template.inc`. ImageSharp applies the same switchable-mode, skip,
  single-reference, inter-intra, minimum-size, overlappable-neighbor, fixed-global-motion, scaled
  reference, and projection-sample gates and reads the matching binary or three-way CDF.
- [x] Audited above and left neighbor traversal, 4x4 pairing, neighbor caps, chroma suppression,
  prediction rectangles, interpolation filters, first-reference selection, mask tables, and blend
  order against current libaom. The existing semantic mask-blend predictor remains the correct
  SIMD-first traversal; no OBMC-specific operator family, allocation, or copy was introduced.
- [x] Corrected the unscaled neighbor far-edge UMV clamp. After converting libaom's neighbor-relative
  motion-vector limits to an absolute source coordinate, the prediction extent cancels from the
  right and bottom limits; the previous code counted it twice.
- [x] Extracted the fixture's 5,387-byte AV1 `mdat` payload at AVIF offset 1,065 and decoded it with
  refreshed current libaom `aomdec`, using one thread with row threading disabled. All 19 frames
  decoded. The final 19,200 YUV444 samples have SHA-256
  `E8CAA650F1571C5B9CACAF8C06E1DDF5F5D2ED35F65F1C34377076C573425899` and match the retained native
  reference with zero differing samples.
- [x] The production sequence asserts decoded OBMC mode state, compares final native Y, Cb, and Cr
  planes exactly, compares final RGBA presentation through ImageSharp's established reference-output
  API under normal and scalar FeatureTestRunner dispatch, and repeats reconstruction with a 1,024-byte
  constrained tracked allocator. Direct `DecodeBlock` tests cover above-then-left blending at
  8/10/12-bit and 4:2:0 and 4:2:2 chroma geometry.
- [x] Renamed the stale pinned-reference test and its established reference-output PNG together. The
  PNG SHA-256 remains
  `D2CB388C9092EF17C4F0382C0150DD30D6F9D0EE247FF45AB5D7D4D312CEB23C`; only its contract-derived
  filename changed.
- [x] The focused Release checkpoint set passes 18/18 on net10.0 and 18/18 on net11.0, with zero
  failures or skips. Scoped analyzer and whitespace verification pass for every changed C# file.
  Roslynk reports zero compiler errors, `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `7e7e3cbe6438d63926b31d966795d2652e221939`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified scaled-reference checkpoint evidence on 2026-08-31:

- [x] Audited reference-size validation and variable-scale coordinates, filters, edge extension, convolution
  rounding, and compound intermediates against current libaom `av1/common/scale.c`,
  `av1/decoder/decodeframe.c`, and `av1/common/convolve.c`. The frame boundary accepts the same
  half-to-sixteen-times dimension range and requires at least one compatible selected reference.
- [x] Corrected the production scaled-compound branch. It previously rounded each scaled reference into
  native pixels before blending; current libaom retains both `CONV_BUF_TYPE` values with
  `COMPOUND_ROUND1_BITS` equal to seven and performs one final rounding after the selected compound blend.
- [x] Kept native-pixel and compound output in the existing `Av1ScaledInterPredictor` traversal with
  semantic `NativeOperator` and `CompoundOperator` output contracts. The closed generic traversal shares
  variable-phase arithmetic across byte and ushort sources, dispatches Vector512, Vector256, Vector128,
  then scalar, and adds no per-block allocation or copy.
- [x] Added independent FeatureTestRunner oracles for native and no-round compound output across 8, 10,
  and 12 bits, variable phases, all interpolation families, reduced kernels, vector tails, and destination
  padding. A complete `Av1BlockDecoder.DecodeBlock()` regression covers scaled compound prediction across
  all, AVX-512-disabled, AVX-disabled, and scalar configurations and proves the vector differs from an
  incorrectly early-rounded blend.
- [x] Decoded the 2,195-byte layered payload with refreshed current libaom `aomdec`, using one thread,
  row threading disabled, all layers selected, and raw 8-bit output. The 40x40 YUV444 base and 80x80
  YUV444 dependent frames total 24,000 samples with SHA-256
  `DD219E41B52C6C9343A92CD0A2D451DF57B73B25F10124811675B4CB2F8D666F`; both match their retained
  native references with zero differing samples.
- [x] The production tests compare both native frames exactly, compare selected-layer and final RGBA
  presentation through ImageSharp's established reference-output API, and repeat both paths with a
  1,024-byte constrained tracked allocator whose allocations have balanced exactly-once returns.
- [x] Renamed the two stale pinned-reference tests and their contract-derived PNGs together. Their Git blob
  identifiers remain unchanged, and their SHA-256 values remain
  `DC4C6DBE6BD92C5FCE1E3E23700AFA603EF04ED02EDD336213EBBA1E3BD84BA0` and
  `678C5E5D4650EA6F0C590302E7DB9E3C6608851BC577453DA4A6837BDB4D3AF3`.
- [x] The focused Release checkpoint set passes 10/10 on net10.0 and 10/10 on net11.0, with zero failures
  or skips. Scoped analyzer and whitespace verification pass for every changed C# file. Roslynk reports
  zero compiler errors, `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `658a9cd1b6e22806decbae923da8800bca03a09e`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified local warped-prediction checkpoint evidence on 2026-08-31:

- [x] Refreshed the clean official libaom `main` checkout and audited the observed revision
  `441c439b9916474cac15d2822af47a9ad70674a8`. Motion-mode eligibility and CDF selection match
  `read_motion_mode` in `av1/decoder/decodemv.c`; above, left, top-left, and top-right spatial projection
  samples and threshold selection match `findSamples` and `selectSamples` in
  `av1/common/mvref_common.c`; affine fitting, shear reduction, phase derivation, filters, rounding,
  clipping, and invalid-model fallback match `av1/common/warped_motion.c` and
  `av1/common/reconinter.c`.
- [x] Mechanically compared all 1,544 ImageSharp and independent-test warped-filter coefficients against
  current libaom's `av1_warped_filter`; both comparisons have zero differences. The separate scalar test
  transcription covers 8-, 10-, and 12-bit luma and subsampled-chroma coordinates, tail widths, destination
  stride preservation, libaom's 12-bit round adjustment, and AVX-512, AVX, 128-bit, and scalar dispatch
  through `FeatureTestRunner`.
- [x] Extracted the fixture's exact 2,310-byte AV1 `mdat` payload at AVIF offset 997. Its SHA-256 is
  `644D04FE1D1A32BB7A3856AD7EB49CF1EFDE0AC845E55BEAC4170F72353F2391`. Current official
  libaom decoded both 256x256 YUV444 frames with one thread, row threading disabled, and all layers enabled.
  The complete Y4M SHA-256 is
  `8FDC5D46014F5E5A7455A83643AB6F0DA66FC5A984E72A43F8C75BAD8271C299`; the final
  frame's 196,608 native samples have SHA-256
  `47B2AB39BF3B9DA15C1EC59840F964DFDF227760947F6E1295FB38A84555F75C` and match the
  retained native reference with zero differences.
- [x] The real two-frame fixture exercises `Av1BlockDecoder.DecodeBlock()`, requires decoded
  `WARPED_CAUSAL` state and the expected multi-sample affine model, compares final native Y, U, and V
  planes exactly, compares the retained final presentation through ImageSharp's established reference-output
  API, and passes through intrinsic and scalar dispatch. The 1,024-byte constrained tracked-allocator path
  passes with motion-field allocations present and balanced exactly-once returns.
- [x] Renamed the stale pinned-reference test and its contract-derived PNG together without changing the PNG
  bytes. Its SHA-256 remains
  `4490D62FB6679378E92CACA48427359091AD2106BE49FC1A3848F78BE03BEEB1`.
- [x] The focused Release checkpoint set passes 4/4 on net10.0 and 4/4 on net11.0, with zero failures or
  skips. Scoped analyzer verification passes for both changed C# files. Roslynk reports zero compiler errors,
  `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `27a522424fe7aaea25078e705d71a501da110727`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified non-translational global-prediction checkpoint evidence on 2026-08-31:

- [x] Audited global-motion syntax, coefficient decoding, previous-reference recentering, shear validation,
  motion-vector projection, and warped-prediction eligibility against current official libaom `main` at the
  observed revision `441c439b9916474cac15d2822af47a9ad70674a8`. The implementation matches
  `read_global_motion_params`, `read_global_motion_model`, `gm_get_motion_vector`, `is_global_mv_block`,
  and the WARP_PRED selection in `av1/common/reconinter.c`.
- [x] Corrected high-bit-depth compound warped/global prediction to retain both references in libaom's
  unsigned no-round compound domain. Current `get_conv_params_no_round`, `av1_warp_plane`, and
  `av1_highbd_warp_affine_c` require the 12-bit first-round adjustment while retaining a seven-bit second
  round; native clipping now occurs only after the compound blend.
- [x] The independent scalar libaom transcription validates native and no-round compound output for byte,
  8-bit, 10-bit, and 12-bit sources, including tail widths and destination-stride preservation. All cases pass
  through AVX-512, AVX, 128-bit, and scalar dispatch with `FeatureTestRunner`. Direct
  `Av1BlockDecoder.DecodeBlock()` coverage validates `GLOBAL_GLOBALMV` compound reconstruction at all
  supported bit depths.
- [x] Extracted the fixture's exact 38,475-byte AV1 `mdat` payload at AVIF offset 997. Its SHA-256 is
  `6AC7EC9984B1FF5C00403D7E3858441E9CEE75128F7414101D06DEEE59A351D0`. Current
  official libaom decoded both 256x256 YUV444 frames with one thread, row threading disabled, and all layers
  enabled. The complete Y4M SHA-256 is
  `84754DE0B9FABC4F3F8F344C848183EC17B625BFD87E4519C3D8AD7DEFD20F2C`; the final
  frame's 196,608 native samples have SHA-256
  `FEC89E2DE7496980389806B194425042F3800C7BAA817249D1A51D44A2B37A8E` and match the
  retained native reference with zero differences.
- [x] The real two-frame fixture exercises the production decoder, requires decoded non-translational global
  motion, compares final native Y, U, and V planes exactly, compares the retained presentation through
  ImageSharp's established reference-output API, and passes the constrained tracked-allocator path.
- [x] Renamed the stale pinned-reference test and its contract-derived PNG together without changing the PNG
  bytes. Its SHA-256 remains
  `F7D27ABF79450DFA311F72106FD1DA80997EABC0937F2F5578EF627119FF83B0`, and Git
  attributes select the LFS filter and diff driver.
- [x] The focused Release checkpoint set passes 11/11 on net10.0 and 11/11 on net11.0, with zero failures or
  skips. Scoped analyzer verification passes for all six changed C# files. Roslynk reports zero compiler
  errors, `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `25295683d39a2336e9b98484c9fd54f33107ea66`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

Verified inter-deblocking checkpoint evidence on 2026-08-31:

- [x] Audited frame-level loop-filter syntax and primary-reference inheritance against
  `setup_loopfilter` in current `av1/decoder/decodeframe.c`; per-superblock delta-LF parsing and
  prediction against `read_delta_q_params` in `av1/decoder/decodemv.c`; and default reference/mode
  deltas against `av1/common/entropymode.c` at observed current-main revision
  `441c439b9916474cac15d2822af47a9ad70674a8`.
- [x] Audited filter-level derivation, segmentation adjustment, reference scaling, global/non-global
  mode classes, skipped-transform prediction-unit decisions, transform-edge selection, kernel length,
  sharpness limits, and vertical-then-horizontal traversal against `get_filter_level`,
  `set_lpf_parameters`, `av1_filter_block_plane_vert`, `av1_filter_block_plane_horz`, and
  `av1_thread_loop_filter_rows`. No production arithmetic change was required.
- [x] Added direct production `Av1LoopFilterDecoder.DecodeFrame()` coverage using adjacent skipped
  16x8 inter blocks split into 8x8 transforms. An independent scalar oracle proves that internal
  transform edges remain untouched and the prediction-unit edge uses current-libaom levels 17 for
  LAST/GLOBALMV, 21 for LAST/NEWMV, and 22 for GOLDEN/GLOBALMV. Existing `FeatureTestRunner`
  coverage continues to verify every filter width at 8, 10, and 12 bits under intrinsic and scalar
  dispatch.
- [x] Current official libaom decoded the retained 20,750-byte 8-bit, 37,169-byte 10-bit, and
  23,769-byte 12-bit elementary streams with one thread, row threading disabled, raw output, and their
  native output depths. The generated native files match the retained references byte for byte. Their
  output SHA-256 values are
  `8DDE2EEC742C39F0579C29AE84CBA0FE01522A9008ADCB2CFFCCEC0295D18141`,
  `9A59DD92A0C579F942ACCA8281EBD0465DC848BE200A4D2FF57EAFF589445F6C`, and
  `EF712BE32AF7CF0A95C5C41BDCC51AFC05A4AB7C047383F5F65EDAD2BB986712`.
- [x] Reused the already current-main scaled-reference sequence as the real inter checkpoint. It
  requires an inter frame with reference/mode-delta processing enabled, nonzero chroma filter levels,
  intra, inter, and skipped-inter blocks; compares both decoded native frames exactly; compares final
  presentation through ImageSharp's established reference-output API; and passes constrained tracked
  allocation with balanced returns.
- [x] Removed an obsolete SVT-AV1 design link from mode-map documentation. Current official libaom
  remains the sole external codec implementation source.
- [x] The focused Release checkpoint set passes 6/6 on net10.0 and 6/6 on net11.0, with zero failures
  or skips.
- [x] Scoped analyzer verification passes for all four changed C# files. Roslynk reports zero compiler
  errors, `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] The completed checkpoint was committed as `fcb502e4960cc7b8efb06b6f060e2c73a913a2bf`
  with author and committer `James Jackson-South <james_south@hotmail.com>`.

For every item:

- [ ] Trace syntax and arithmetic to the current libaom `main` tree.
- [ ] Execute the real production decoder path.
- [ ] Compare native planes exactly.
- [ ] Compare presentation through the established reference-image API.
- [ ] Run constrained allocator and exactly-once ownership coverage.
- [ ] Run FeatureTestRunner for SIMD and scalar dispatch when the implementation has SIMD.
- [ ] Record focused Release evidence before marking the item verified.

### 4. Close AV1 decoder coverage

Previously verified algorithm checkpoints remain valuable evidence, but the final decoder gate requires a fresh current-tree run after the inter and cleanup corrections.

- [x] Bounded OBU framing, sequence headers, frame headers, tile groups, alignment, and trailing-bit parsing have been re-audited and verified against current libaom `main`.
- [x] Partition traversal, mode information, segmentation, delta quantization, transform-size selection, coefficient decoding, inverse quantization, and inverse transforms have been re-audited and verified against current libaom `main`.
- [x] Intra prediction covers directional, DC, smooth, Paeth, chroma-from-luma, filter-intra, and palette families with the established operator architecture.
- [x] Intra-block copy has exact native reconstruction and feature-isolated SIMD evidence.
- [x] Lossless inverse transform, loop filtering, CDEF, super-resolution, restoration, and film grain have focused checkpoint evidence.
- [x] Retained references, CDF snapshots, segmentation maps, global motion, temporal motion fields, and dependent-frame lifecycle have been re-audited and verified against current libaom `main`.
- [x] The 12-case all-intra profile matrix covers every valid 8, 10, and 12-bit monochrome, 4:2:0, 4:2:2, and 4:4:4 combination. Dependent-frame coverage is recorded separately above.
- [x] At this historical checkpoint, the native-plane matrix passed through the production decoder on net10.0 and net11.0. The normal-dispatch and FeatureTestRunner fallback methods passed 2 of 2 focused tests on each target. These results do not verify subsequently edited trees.
- [x] At this historical checkpoint, the presentation matrix passed 12 of 12 cases through ImageSharp's established reference-image API on net10.0 and net11.0. Current verification is recorded in the dated takeover entries above.
- [x] Verify malformed/truncated data, frame IDs, reference slots, tile bounds, allocation limits, cancellation, and failure unwinding.
- [x] Verify still items and bounded sequences from file, memory, non-seekable, and short-read streams.
- [~] Verify ICC, CICP, alpha, grids, pixel aspect ratio, clean aperture, rotation, mirroring, metadata, and every presented sequence frame. Grid validation now requires the first cell to be at least 64 samples on both axes, enforces even output and cell dimensions along each subsampled AV1 chroma axis, and requires every cell to cover its row-major output region without exceeding the first cell's dimensions. This accepts the smaller right and bottom cells supported by the writer while also accepting uniform coded cells whose final row and column are cropped to the grid descriptor. Auxiliary alpha uses the same cropped overlap, so padded cells cannot write outside the final frame. Production regressions cover smaller right, bottom, and bottom-right color and alpha cells; Roslynk compiler and scoped analyzer diagnostics are clean, while runtime verification of the current tree remains pending.
- [x] Complete the public AVIF format/API review so registered capabilities match implemented behavior.
- [ ] Implement every valid in-scope AV1 syntax branch. Rejecting an unsupported valid branch does not complete it; tile-list decoding and its external-reference integration remain unresolved.

Verified negative-path and frame-identifier gate evidence on 2026-08-31:

- [x] A two-frame lossless frame-identifier sequence was generated and decoded with the clean official
  libaom `main` checkout at observed revision `441c439b9916474cac15d2822af47a9ad70674a8`.
  Both decoded frames match the source Y, Cb, and Cr samples exactly.
- [x] `DecodeFrameIdentifiersMatchReference` executes the production decoder through FeatureTestRunner,
  compares both native frames exactly, and proves the second frame is dependent with a changed current
  frame identifier. The current-frame, reference-delta, stale-slot, and refreshed-slot identifier logic
  was audited against the same current `main` source.
- [x] The focused negative-path set passes 46 of 46 cases on net10.0 and 46 of 46 on net11.0, with zero
  failures or skips. It covers truncated palette entropy, malformed-following-OBU recovery, parser
  lifecycle failure, overflowing and invalid tile bounds, reference-slot ownership and transfer,
  constrained multi-group allocation, motion-field allocation failure unwinding, and frame identifiers.
- [x] The established paused-stream cancellation suite now includes AVIF. It verifies cancellation at
  0%, 30%, and 70% of both file and memory streams, plus pre-cancelled identification, on both targets.
- [x] The completed checkpoint was committed as
  `7f0e08126b3354e8f1eb45886f0d572006ae27de` with author and committer
  `James Jackson-South <james_south@hotmail.com>`.

Verified bounded-OBU checkpoint evidence on 2026-08-31:

- [x] Audited `av1/decoder/obu.c`, `av1/decoder/decodeframe.c`, `av1/common/obu_util.c`,
  `av1/common/tile_common.c`, `aom/src/aom_integer.c`, and `aom_dsp/bitreader_buffer.c` in the
  clean official libaom `main` checkout. Both `HEAD` and `origin/main` resolved to the observed
  revision `441c439b9916474cac15d2822af47a9ad70674a8`; this is verification evidence, not a pin.
- [x] The bounded container scanner and production OBU reader now agree with current libaom on ignored
  reserved header fields and the shared unsigned 32-bit LEB128 limit.
- [x] Sequence-header validation now rejects undefined level indices, initial display delays above ten,
  frame identifiers above sixteen bits, zero timing units, the UVLC overflow sentinel, and invalid
  identity-matrix profile or subsampling combinations at the owning syntax boundary.
- [x] Frame and tile parsing now rejects `show_existing_frame` in a combined `OBU_FRAME`, the all-slots
  intra-only refresh mask, inner tile columns below current libaom's super-resolution-aware minimum,
  overflowing or out-of-bounds tile sizes, and empty final tile payloads.
- [x] The still-image writer now emits the required zero tile-bound-presence bit for a multi-tile combined
  `OBU_FRAME`, matching current libaom's single-tile-group encoder path.
- [x] `ObuFrameHeaderTests` and `ObuFrameLifecycleTests` cover the corrected syntax through the real
  bounded parser. The focused parser set passes 50 of 50 cases on net10.0.
- [x] The final focused production set passes 55 of 55 cases on net10.0 and 55 of 55 on net11.0, with zero
  failures or skips. It includes exact final-layer and selected-layer native planes, exact established
  reference-image presentation, constrained allocator ownership, malformed-following-OBU recovery, and
  FeatureTestRunner normal, AVX-512-disabled, AVX-disabled, and scalar execution.
- [x] A fresh direct foreground current-main `aomdec` run decoded both progressive layers with one thread
  and row multithreading disabled. All 2,178 Y, U, and V samples match the retained YUV444-alpha reference;
  the alpha plane is excluded from the AV1 native-plane comparison.
- [x] The current-libaom production reference test and its established PNG were renamed together. The PNG
  bytes remain unchanged at SHA-256
  `0758C17DC36E38AEE9F4389A335C2BF332AB91E4C79D7B0B22994FDDD0FD1605`, both paths resolve to
  `diff=lfs`, and `.gitattributes` was not edited.
- [x] Release source builds pass for net10.0 and net11.0 with zero warnings and zero errors. Roslynk reports
  zero compiler errors, and scoped production and test analyzer verification reports no changes.
- [x] The completed checkpoint was committed as `243524c2c0b52a49d8d161fab806ab092cabe47c` with author
  and committer `James Jackson-South <james_south@hotmail.com>`.

Verified partition, mode, segmentation, quantization, and transform checkpoint evidence on 2026-08-31:

- [x] Audited partition traversal and chroma representability against `read_partition` and the subsampled
  plane-size rejection in current libaom `av1/decoder/decodeframe.c`; spatial segment-ID decoding and
  corruption handling against `read_segment_id` in `av1/decoder/decodemv.c`; delta-Q syntax, resolution,
  arithmetic, and clamping against `read_delta_qindex` and `read_delta_q_params` in the same file.
- [x] Audited selected and variable transform-size traversal against `read_tx_size`, `read_tx_size_vartx`,
  and transform-block traversal in `av1/decoder/decodeframe.c`; coefficient syntax and arithmetic against
  `av1_read_coeffs_txb` in `av1/decoder/decodetxb.c`; inverse quantization and transform application against
  current `av1/decoder/decodeframe.c`, `av1/common/idct.c`, and the current libaom transform test oracle.
  The observed clean `HEAD` and `origin/main` revision was
  `441c439b9916474cac15d2822af47a9ad70674a8`; this is verification evidence, not a pin.
- [x] Partition decoding now rejects an invalid partition subsize and a block size that cannot represent the
  current subsampled chroma plane. Spatial segmentation rejects decoded IDs above the active segment range.
  Focused tests exercise both current-libaom corruption boundaries through the production tile reader.
- [x] Coefficient entropy decoding uses one allocator-owned maximum-size `Av1LevelBuffer` per tile reader.
  Each transform resets and clears only its active padded geometry, so no transform creates an allocation.
  Allocation tracking over all eight minimum- and maximum-quantizer frames proves exactly one coefficient
  scratch allocation per frame and exactly-once return after decoder disposal.
- [x] Palette index maps use one allocator-backed 32 KiB decoder-session owner with non-owning 128x128 luma
  and chroma views. Each parsed superblock is reconstructed before either view is reused, and each block
  clears only its transient `Buffer2DRegion` after prediction. The fixed session cost replaces the former
  full-frame maps without copies, fragmented memory groups, constructor rollback, or per-block allocations.
  The one-rent ownership regression and native palette reconstruction pass on net11.0, the complete HEIF/AV1
  namespace passes 8,808 of 8,808 direct VSTest cases in Release, and the four-case palette set passes with
  exact native and presentation output, truncated-entropy rejection, and balanced exactly-once disposal.
- [x] `Av1BlockModeInfo` is value storage, removing the managed object allocation formerly created for every
  decoded coding block. Explicit `ModeInfoIndex` values preserve libaom's mode-info identity semantics at
  prediction-unit loop-filter edges, and the frame map now uses integer offsets so more than 65,535 decoded
  blocks cannot wrap its lookup identity.
- [x] Current official libaom reproduced the 39-frame all-intra reference and all four 8/10-bit minimum- and
  maximum-quantizer references byte for byte. The production tests compare every native sample exactly,
  cover every intra mode and seven selected transform types, execute SIMD and scalar paths through
  `FeatureTestRunner`, and exercise the quantizer sequences under constrained tracked allocation.
- [x] Current official libaom decoded the 42-byte palette payload into the retained 1,089-byte YUV444
  reference at SHA-256 `E05F7C0DF06ECCF0E43869D1D7B03DAA1D635ACD26A766F8940899BE18D53251`.
  The exact native test requires luma and chroma palette syntax. The established reference-output test uses
  the unchanged presentation PNG at SHA-256
  `1148EBF6AA4B0F2D069D5E9B9605F6FB2A315E525F18016CDCAE23EFDD81DA84`, whose renamed path still
  resolves to `diff=lfs`; `.gitattributes` was not edited.
- [x] The exact final AV1 namespace passes 8,732 of 8,732 cases on net10.0 and 8,732 of 8,732 cases on
  net11.0, with zero failures or skips. Release source builds pass for net10.0 and net11.0 with zero warnings
  and zero errors. Roslynk reports zero compiler errors, and scoped analyzer verification reports no changes.
- [x] The completed checkpoint was committed as `57a3f6668e39d0934e7b6b8d37a3dc2a5adc88f0` with author
  and committer `James Jackson-South <james_south@hotmail.com>`.

Verified retained-frame lifecycle checkpoint evidence on 2026-08-31:

- [x] Audited primary-reference entropy selection, independent per-tile CDF starts, context-update-tile
  publication, segmentation-map inheritance, reference-map refresh, and show-existing key-frame reset
  against current libaom `av1/decoder/decodeframe.c`, `av1/decoder/decodemv.c`,
  `av1/decoder/decoder.c`, and `av1/common/entropymode.c`.
- [x] Audited retained motion-vector cells, reference-side classification, projection source ordering,
  projection limits, and reference-frame publication against `av1_copy_frame_mvs`,
  `av1_calculate_ref_frame_side`, `motion_field_projection`, and `av1_setup_motion_field` in current
  libaom. Same-role primary-reference global-motion inheritance remains covered by the exact current-main
  global-warp fixture. The observed clean `HEAD` and `origin/main` revision was
  `441c439b9916474cac15d2822af47a9ad70674a8`; this is verification evidence, not a pin.
- [x] Current official libaom decoded the retained `cdfupdate`, `mfmv`, `svc-L2T1`, `svc-L1T2`, and
  `svc-L2T2` streams with one thread, row threading disabled, and eight-bit output depth. Their generated
  Y4M files match the retained references byte for byte at SHA-256
  `4FBFF73FF0DE2D9084DAE557D1D4BD677B0486516525BF4D327D2D795D5A7779`,
  `F7DB607694818C19E62FD9A27F53E1A3E2D00B72C39C0430C1B26399CC76777D`,
  `7A427631ECBF144F435AA4612F1201415FB1A9BCF9A67BA010AEF830B0C3AB81`,
  `4012DE2D4AFD095E7BB68EAE18B50B0674781BB4971CECABC0E5471E63373ED3`, and
  `1ABB981CFF76BA9557DA437B258D8A95FCA755DED8E3949D857E8388AB1D6AE3`.
- [x] Existing allocation-tracking tests exercise initialization, retained-slot aliases, allocation-failure
  unwinding, presentation ownership, decoder-result ownership, repeated disposal, and final exactly-once
  return of reference frames, frame-owned motion fields, entropy snapshots, and segmentation maps.
- [x] The focused Release checkpoint set passes 54 of 54 cases on net10.0 and 54 of 54 cases on net11.0,
  with zero failures or skips. It includes exact native CDF-update, motion-field, spatial-layer,
  temporal-layer, spatial-temporal-layer, progressive dependent-frame, and global-warp production paths,
  plus constrained allocator coverage.
- [x] Release source builds pass for net10.0 and net11.0 with zero warnings and zero errors. Roslynk
  reports zero compiler errors, scoped analyzer verification reports no changes, `git diff --check`
  passes, and `.gitattributes` is unchanged.

Final decoder allocation, lifetime, precision, architecture, and test-validity audit evidence on 2026-09-01:

- [x] Refreshed the official libaom remote and audited against observed `origin/main`
  `976867526367f571a1c09b994066af8364aed781`. The intervening external-rate-controller commit does
  not change `av1/decoder`, `av1/common`, `aom_dsp`, or the AV1 decoder build definition.
- [x] CDEF now uses one bounded 64x64-unit bordered source workspace, two preserved top-row slots per
  plane, preserved left columns, and unit-local direction and variance storage. This replaces the
  frame-wide source copy and frame-wide direction maps while retaining libaom's unit traversal and
  cross-plane luma-direction lifetime.
- [x] Loop restoration now retains the required immutable source and separate destination, but stores the
  full destination in native sample width. Eight-bit filtering narrows only bounded unit output after
  clipping, while high-bit-depth filtering writes directly to the native `ushort` destination.
- [x] Reference-to-presentation copying now copies visible native rows only. Padding remains destination
  owned, and the ownership tests mutate a copied visible sample rather than unrelated padding.
- [~] The historical audit did not establish that every remaining decoder allocation or copy is required.
  Frame planes use contiguous storage and the listed workspaces are allocator owned, but the takeover audit
  has since found and corrected excess frame-local workspace lifetimes and wider-than-required neighbor
  contexts. Remaining parser-context, coefficient/TU storage, dispatch, and output ownership paths still
  require source comparison; see the current audit evidence above.
- [x] Block reconstruction now uses one exact-size signed-short owner for inverse quantization, inverse
  transform, compound prediction, convolution, and chroma-from-luma scratch. Even-length slices provide
  the integer workspaces without another rent. Monochrome reserves no chroma coefficients, and 4:2:0,
  4:2:2, and 4:4:4 reserve two symmetric chroma planes at their coded subsampling. This replaces three
  constructor rents and their catch-all rollback path; exact allocation length, coefficient span length,
  and exactly-once return pass for all four layouts, with 549 adjacent reconstruction tests passing direct
  net11 VSTest in Release.
- [~] Tile-list decoding is not implemented. The explicit rejection test only establishes that restriction;
  the camera-header/external-reference path and its container integration remain unresolved. Reserved and
  metadata OBUs have bounded framing and trailing-bit checks. Existing precision tests cover represented
  reconstruction and presentation paths, not complete AV1 feature support.
- [x] Predictor traversal remains split into semantic readonly operator families. The planar sample
  adapter and transform-block context are value types, and Release construction sites use `default`
  without null-forgiving suppression.
- [x] The net11.0 Release test project builds with zero errors. Roslynk reports zero compiler errors,
  `git diff --check` passes, and `.gitattributes` is unchanged.
- [x] Visual Studio 18.9 VSTest ran the complete `Formats.Heif.Av1` namespace with collection
  parallelism disabled and stop-on-failure enabled: 8,746 of 8,746 cases passed. The touched
  `HeifDecoderTests` and `HeifSequenceParserTests` add 104 of 104 passing integration cases.
  Focused CDEF, restoration, film-grain, copy-ownership, and reference-isolation runs also pass 15 of
  15 cases. The historical report recorded successful process exits; it did not independently establish absence of Windows application-error dialogs.

Final decoder stream, presentation, and public-registration evidence on 2026-09-01:

- [x] Real AV1 still-item and timed-sequence files decode identically from a file stream, memory stream,
  non-seekable stream, and a seekable stream limited to three bytes per read. All eight stream rows pass
  through public format detection and production decoding, comparing every presented frame exactly.
- [x] A two-frame production sequence applies a centered clean-aperture crop, counter-clockwise rotation,
  mirroring, pixel-aspect-ratio metadata, and CICP metadata to every frame. The complete five-frame real
  auxiliary-alpha sequence composes non-opaque alpha and retains timing, Exif, and XMP for every frame.
- [x] The fixed-header detector accepts both compact and extended-size leading file-type boxes. Default
  configuration registers the implemented HEIF decoder and detector but no longer advertises the
  incomplete HEIF encoder.
- [x] Visual Studio 18.9 VSTest, serialized with stop-on-failure enabled, passes the 12 of 12 new
  stream/presentation/registration cases and the complete current `HeifDecoderTests` plus
  `HeifSequenceParserTests` set with the registration contract: 115 of 115. The final explicit
  no-encoder registration assertion passes 1 of 1 after its final edit.
- [x] The net11.0 Release test project builds with zero errors, Roslynk reports zero compiler errors,
  `git diff --check` passes, and `.gitattributes` is unchanged. The historical report recorded normal VSTest exits and no surviving test host; that does not establish absence of Windows application-error dialogs.

SIMD traversal consistency evidence on 2026-09-02:

- [x] The shared `Numerics` vector-count helpers now cover same-lane spans and all fixed hardware widths.
  AV1 decoder and current encoder hot paths use those helpers for complete-vector traversal instead of
  repeating local modulo or last-vector calculations. Reverse-source indexing and algorithm-specific
  partial-output groups remain explicit because they are not vector-count calculations.
- [x] Forward quantization, palette prediction, and scaled inter prediction construct width-specific SIMD
  constants only when at least one vector batch will execute. Narrower dispatch tiers consume only the
  remainder left by wider tiers before the scalar tail.
- [x] The net11.0 Release production assembly builds with zero warnings and zero errors. The test project
  builds with zero errors while retaining the existing repository warning set. Roslynk reports zero
  compiler errors, `git diff --check` passes, and `.gitattributes` is unchanged.
  Foreground VSTest passes 63 of 63 focused quantizer, forward-transform, CDEF, restoration, palette,
  intra, inter, film-grain, and super-resolution cases.

Decoder exit gate:

- [ ] Re-establish current-main native evidence for every supported AV1 tool through the complete production path.
- [ ] Re-establish presentation behavior against independent reference images at the correct output precision.
- [ ] Complete the decoder audit for managed execution, copies, per-block allocation, and segmented memory.
- [ ] Verify allocator ownership and exceptional-path disposal throughout the decoder.
- [ ] Record focused Release verification of the final tree; earlier checkpoint results do not close these gates.

## AV1 encoder implementation

Writer primitives are not an encoder. The public encoder remains incomplete until its complete decision and reconstruction paths follow the reference, every exposed option has production-path verification, and separately encoded output satisfies the one-unit per-sample acceptance limit.

### 5. Define and enforce the encoder contract

- [x] Use official libaom `main` at `d565eec60f084421fa34fc0534b760c6452b6a6c` as the encoder syntax, probability-model, transform, quantization, filtering, and bitstream reference.
- [x] Use the existing PNG, TIFF, and JPEG encoders as the ImageSharp architecture reference: generic `Image<TPixel>` input, encoder options taking precedence over converted format metadata and codec defaults, allocator-owned temporary storage, and deterministic disposal.
- [x] Treat source pixel type, source alpha representation, and decoded source bit depth as conversion inputs, never as output-eligibility checks. Do not pre-scan pixels before encoding.
- [x] Resolve output configuration once from explicit encoder options, converted `HeifMetadata`, and AV1 defaults in that order. Sanitize only combinations that cannot describe a legal requested output, and never write resolved values back to source metadata.
- [~] Finalize observable options for quality, effort, lossless mode, bit depth, chroma subsampling, alpha quality, metadata, and bounded sequences. Sequence repeat-count options override converted HEIF metadata like the existing animated encoders. AV1 sequences now follow the existing animated-image contract: the primary item reuses the first sync sample when the root is animated, while an excluded root is encoded once as the independent primary image and the sequence begins at frame index one. Final verification remains open.
- [x] Preserve high-bit-depth source precision through 16-bit RGB and native 10/12-bit component planes.
- [~] HEIF is registered through the default configuration module. Keep public AV1 capability claims limited to the paths covered by the encoder verification matrix until the remaining encoder work is complete.

Encoder data-flow contract:

1. Resolve immutable frame and sequence output settings before allocating codec state.
2. Convert each generic `ImageFrame<TPixel>` once through `PixelOperations<TPixel>` and the SIMD-first HEIF planar converter into native 8, 10, or 12-bit planes. Alpha is encoded as an auxiliary image when requested by the resolved output contract; it is not discarded through a source scan.
3. Reuse allocator-owned plane, row, block, transform, quantization, entropy, and reconstruction workspaces for the complete frame. No active path may allocate per row, block, transform, scanline, or SIMD tail.
4. Analyze and encode tiles directly from those planes, retaining reconstructed reference frames only for the bounded sequence lifetime.
5. Build OBU headers in bounded allocator-backed scratch and stream entropy-coded tile owners and container extents directly. Every ownership transfer is explicit, every owner is disposed exactly once, and no `ToArray` or file-sized copy crosses a layer boundary.
6. Iterate image frames using ImageSharp frame metadata and format-connecting metadata. Root-frame-only behavior is permitted only for an explicitly static output contract.

Encoder verification contract:

- Exercise source pixel formats independently from requested AV1 bit depth, chroma subsampling, alpha, and lossless/lossy mode.
- Run every SIMD operator through FeatureTestRunner at Vector512, Vector256, Vector128, and scalar tiers against an independent scalar oracle shaped from the same libaom revision.
- Cover discontiguous allocator buffers, constrained memory groups, cancellation, non-seekable output, multiple extents, auxiliary alpha, and bounded sequences.
- Validate produced AV1 payloads with current-main libaom and compare native planes before using ImageSharp self-decode as supplemental container coverage.

### 6. Build the complete AV1 frame encoder

- [~] SIMD-first RGB-to-native-plane conversion now feeds eight-bit and high-bit-depth bordered AV1 source frames directly, preserving ImageSharp's arbitrary packed-pixel input contract without an intermediate full-frame native-plane copy.
- [~] Auxiliary-alpha encoding now follows the same packed-pixel conversion boundary without scanning pixel contents or cloning the image. Source alpha is converted through ImageSharp's 16-bit pixel contract, deinterleaved with descending Vector512, Vector256, Vector128, and scalar traversal through the shared vector-count helpers, then scaled and rounded once by the existing native-sample writer directly into the final bordered monochrome source frame. One operation-wide allocator owner provides the packed and planar row views; there is no frame-sized alpha staging allocation or second owner. Exact 12-bit precision, physical border extension, the single 12-bytes-per-pixel row rent, and balanced return pass through the production converter. The complete 47-case frame-encoder set passes direct foreground net11 Release VSTest, and current-main `aomdec` at `a40ed1ea9e4ecc3df58a5bccb76623f2c94ae727` accepts the generated 8-, 10-, and 12-bit monochrome payloads. AVIF auxiliary item properties, references, and public activation remain open.
- [~] Forward transform families, transform workspace, and an allocation-free DC intra block boundary exist locally. For eight-bit and high-bit-depth samples, the composed boundary now follows current libaom's encoder order: predict into the reconstruction plane, subtract prediction from source, transform, quantize into separate qcoeff and dqcoeff storage, retain EOB and transform type, and inverse-transform only when EOB is nonzero so later blocks consume decoder-identical references. Prediction and subtraction retain their SIMD-first operators, independent source and reconstruction strides are preserved, and no frame-sized or per-block buffer is introduced. The block boundary consumes the real bordered encoder-plane regions and indexes their one-segment owner directly; this preserves physical row strides without a row copy and avoids the per-call enumerator allocation exposed by the initial array-only test. One reusable 61 KiB allocator owner supplies tightly packed residual, aligned transform-coefficient, dequantized-coefficient, and transform scratch spans across transform blocks; quantized coefficients write directly to the retained frame coefficient owner instead of being duplicated. A fixed 8x8 DC-intra superblock baseline now traverses the same recursive preorder and frame-edge pruning as the tile writer, gathers left references into that reusable block workspace, writes luma and chroma coefficient-owner slices in the writer's exact consumption order, and updates the caller-owned reconstruction planes for subsequent predictions. Stage-by-stage scalar-oracle, physical-border, retained-syntax, superblock-to-writer synchronization, high-bit-depth precision, and steady-state zero-allocation coverage passes 8 of 8 through direct net11 VSTest in Release. This is a legal fixed baseline, not complete partition or mode analysis.
- [~] Production coding now analyzes all tiles before packing. Analysis retains modes, coefficients, palette tokens, motion contexts, and reconstruction; selected deblocking runs before entropy edges are reset for packing. The current frame controller still does not select deblocking levels or select/apply CDEF and restoration. The dated takeover checks above establish only their stated reconstruction and syntax behavior.
- [~] A non-owning encoder-frame view now separates visible conversion regions from coded regions and performs complete left, top, right, bottom, and corner extension across each bordered plane. Current libaom uses 8-sample-aligned coded dimensions, a 32-sample-aligned luma stride with chroma stride derived from it, and a 64-pixel luma border for non-resized all-intra encoding. One operation-ready frame owner now rents the aligned Y, U, and V storage contiguously, exposes non-owning `Buffer2D` plane views, and returns the rent exactly once. A 4K 4:2:0 frame occupies about 13.0 MiB at 8-bit or 26.0 MiB at 10/12-bit; source and reconstruction therefore remain distinct frame owners rather than adding a full-frame copy. The corrected tests use this real ownership path and verify the exact 54 KiB 64x64 4:2:0 rent. The frame-encoder operation now instantiates matching source and reconstruction owners with ordinary `using` lifetimes and converts packed pixels directly into the source owner before extension.
- [~] Temporal delimiter, sequence header, frame header, combined-frame tile-group writing, uniform multi-tile layout, and reduced and non-reduced frame operations now exist locally. The remaining codec-tool and verification work is tracked below.
- [~] Implement superblock and partition analysis for every permitted block size and partition. Efforts zero through eight deliberately split every in-frame node to 8x8 blocks. Effort nine performs recursive live rate-distortion selection at complete 8x8 and 16x16 nodes, while effort ten extends the same search to complete 32x32, 64x64, and 128x128 nodes. Candidate order matches current libaom: `PARTITION_NONE`, `PARTITION_SPLIT`, `PARTITION_HORZ`, `PARTITION_VERT`, the four asymmetric partitions, then `PARTITION_HORZ_4` and `PARTITION_VERT_4`; the two 1-to-4 partitions are excluded at 128x128 as required by current libaom. Invalid chroma geometries are excluded before evaluation. Each candidate saves and restores the exact partition, coefficient, transform, and palette neighbor edges in one aligned block-workspace owner; trials neither allocate nor copy probability state. Recursive split trials publish each selected child's decoded mode, transform, coefficient, and palette contexts before evaluating its next sibling. Coefficient contexts are published per retained transform rather than broadcasting the first transform over an entire partition leaf. Large luma and chroma leaves are evaluated as bounded-64, raster-ordered transform tiles in the existing aligned workspace, and each winning plane is copied to retained storage once. Production picture state retains the compact 8x8 mode allocation below effort nine and explicitly selects 4x4 allocation granularity when sub-8x8 partitions are enabled. Effort-dependent pruning remains.
- [~] Implement intra mode search, palette, filter intra, chroma-from-luma, and intra-block copy decisions. Live luma search now covers all 13 zero-angle base modes and all six nonzero adjustments for each of the eight directional modes. Joint spatial chroma search covers the same 61 candidates, combines both chroma planes in one rate-distortion decision, and preserves the winning shared angle adjustment. Chroma-from-luma now searches the complete signed alpha alphabet from reconstructed luma and retains its joint U/V syntax. Filter-intra now searches all five predictors after ordinary luma modes. Palette entropy, retained state, production syntax, luma and paired chroma palette selection, screen-content activation, and joint intra-block-copy mode selection exist, but their full reference decision policy and separate-encoder parity remain unverified.
- [~] Implement inter mode search for bounded sequences, including reference selection and the decoder-supported inter tools. The sequence encoder retains the preceding reconstruction and, from effort six, searches a bounded full-pixel frame translation against that LAST_FRAME reference. Candidate discovery uses the existing SIMD-first squared-error kernels over a central analysis window, validates the winner over the complete coded luma plane, and charges its exact uncompressed-header bit count in the inter-frame rate-distortion domain. Pure translation is signaled as an identity-scale rotation/zoom model, matching current libaom's workaround for the AV1 translation-only axis defect. Each 8x8 inter-frame block first retains the complete intra candidate, then compares NEARESTMV, all three legal NEARMV dynamic-list entries, GLOBALMV, and all three legal NEWMV dynamic-list entries against it with live intra/inter, single-reference, mode, DRL, differential-vector, skip, transform, coefficient, and distortion costs. The initial NEWMV search retains the reference's cheap prediction-error stage, but every surviving mode now owns a complete transform, coefficient, skip, and distortion evaluation before mode selection; selected and candidate workspace views exchange ownership only on strict improvement. Inter trials remain in the existing shared workspace until they strictly beat the intra result, so losing trials require no backup buffer or copy. The tile writer emits the matching DRL path and normative context-selected `LAST_FRAME` reference tree instead of forcing every block through a segmentation feature. The selected DRL index reuses the filter-intra byte because those block syntax branches are mutually exclusive, preserving the existing packed state size. Packed short vectors reuse the existing picture-state owner. Effort six keeps a full-pixel fast path; effort seven refines each selected NEWMV through half- and quarter-pixel eight-tap prediction; effort eight adds the final eighth-pixel stage. The frame header advertises the matching precision, and the shared motion-vector entropy path emits fractional and high-precision symbols only when that precision permits them. Roslynk reports no compiler or scoped analyzer diagnostics, but runtime verification is pending. Additional retained reference pictures, compound prediction, and the remaining inter tools remain.
- [~] Current-libaom `av1_quantize_fp_no_qmatrix` arithmetic is implemented as a closed generic forward-quantizer family with Vector512, Vector256, Vector128, and scalar paths, raster-order output, coded 64-point coefficient limits, and scan-order EOB selection. High-bit-depth paths widen before multiplying instead of applying the eight-bit coefficient clamp. Lossless blocks use the AV1 4x4 Walsh-Hadamard transform, exact lossless quantization and dequantization, four-by-four-only transform syntax, and non-skipped residual coding. Transform search and coefficient optimization remain.
- [~] Complete reference-led rate-distortion selection remains open. The managed implementation evaluates spatial, filter-intra, palette, chroma-from-luma, and intra-block-copy candidates, but its effort thresholds, partition policy, intra-before-inter ordering, transform pruning, coefficient optimization, and winner refinement are not reconciled with the reference. Evaluating additional candidates and passing the existing tests do not establish encoder parity.
- [~] The current Effort 0–10 behavior is an unreconciled implementation policy. Its DC-only lower tier and progressive activation of directions, transforms, screen-content tools, and larger partitions were not derived from the complete native profile controller. Existing effort tests verify that current behavior only. GOOD/ALLINTRA cpu-used mapping and the dependent frame-size, quantizer, frame-role, and staged search rules remain unresolved; the native default is not exhaustive enumeration.
- [~] Encoder rate accounting uses live adaptive CDFs and shared symbol-cost operations. Native coding also uses staged coefficient/mode/motion cost snapshots whose refresh policy must be reproduced. The corrected fixed-point arithmetic and bounded syntax tests do not establish that current candidate costs, ordering, transform choices, or reconstruction match the complete native encoder. Deferred packing is implemented; staged pruning, coefficient optimization before inverse reconstruction, and final winner refinement remain open.
- [~] The tile writer now publishes one packed coefficient context per covered 4x4 edge unit and derives luma/chroma skip plus DC-sign contexts from the complete transform edges using current-libaom units. Partition, transform, and coefficient neighbor state retains only the above and left context regions used by current libaom; the unused third top-left region, its granularity state, and its unused sentinel are removed. One picture owner packs segmentation, every tile's partition, luma, chroma, and transform edges, CDEF state, preceding quantizer, and encoded payload bounds into one clean byte allocation with typed non-owning views; together with the separately typed packed mode-information owner, the complete picture state uses two allocator rents rather than seven. Each encoded tile has independent neighbor and probability state while sharing the bounded output owner. Earlier aligned-length and balanced-return coverage exists; runtime allocation verification of the current multi-tile layout remains pending.
- [~] Encoder mode information now uses a frame-owned integer alias grid over a packed 8-byte value allocation, matching current libaom's `mi_grid_base` and `mi_alloc` relationship without a managed object or reference per 4x4 entry. The visible dimensions are aligned to eight luma samples, the grid stride and allocated row count are aligned to 32 mode-information units, and optional 8x8 allocation granularity reduces the value store in both dimensions exactly as current libaom does. One clean ImageSharp byte owner contains both independently typed regions, reducing libaom's two allocation lifetimes to one without a copy. At 4K, the 4x4 layout occupies about 6.0 MiB in total; the 8x8 layout occupies about 3.0 MiB. Exact geometry, clean allocation, typed lengths, aligned mapping, untouched row padding, and exactly-once return pass 4 of 4 direct net11 VSTest cases in Release. Every coded 4x4 cell covered by square, rectangular, or clipped edge blocks maps to its owning allocation entry before context-dependent symbols are written. Packed syntax, relative neighbor lookup, full block mapping, writer traversal, entropy, and OBU coverage pass 1,947 of 1,947 direct net11 VSTest cases in Release; complete mode decision still remains.
- [~] The superblock decision and palette-map workspace uses one reusable 40.3 KiB ImageSharp allocator owner. Its aligned 8.3 KiB decision region contains 1,024 explicitly packed 8-byte final-block entries and the 341 preorder partition bytes required by a complete 128x128-through-8x8 quadtree; its remaining 32 KiB contains the fixed 128x128 luma and chroma palette maps. This combines storage held separately by libaom; exact lifetime and size reconciliation remains part of the fresh allocation audit, and fewer owners alone does not establish an improvement. Palette colors have their own current-block value and are copied only to the picture edges that later blocks can reference, so enabling palette mode does not add 50 bytes to every final-block entry. Construction and the explicit per-superblock reset initialize every syntax field, including the nonzero sentinel that disables filter-intra prediction; pooled quantizer, prediction, partition, and current-palette bytes cannot leak into the next decision pass. Roslynk reports zero compiler errors for the current one-owner refactor; runtime allocation verification remains pending.
- [~] Finalized transform coefficients and packed EOB/type state now use raster-ordered, per-superblock plane segments matching current libaom's coefficient-pool geometry. One ImageSharp allocator owner replaces libaom's separate coefficient, EOB, and entropy-context allocations while preserving the full 1024 luma and 256-per-chroma 4x4 state capacity of a 128x128 4:2:0 superblock. The fixed 8x8 DC-intra traversal populates the owner's quantized coefficient and state slices while updating the caller-owned reconstruction plane directly, and a real tile-writer integration check proves that both sides consume identical luma and chroma areas. Complete mode decision still remains.
- [~] Tile partition writing now follows current libaom's recursive `write_modes_sb` preorder traversal and `update_ext_partition_context` edge updates directly. Bottom-edge blocks use the horizontal-alike partition CDF and right-edge blocks use the vertical-alike CDF; byte-exact regressions cover both paths after the previous calls were found reversed. Lossless chroma-from-luma availability now uses the subsampled plane block size shared with the decoder instead of the lossy 32x32 limit, preserving the correct UV-mode alphabet for each segment. The obsolete SVT-derived global geometry catalog and its unimplemented lookup are removed; transform geometry is derived in libaom's bounded 64x64 residual order, fixed intra transform-size symbols use the reference depth and neighbor contexts, and each derived transform size is persisted to the frame-owned mode information before the entropy snapshot and coefficient traversal consume it. Frame-edge and segmentation syntax use mode-information units, and 128x128 CDEF units use libaom's 0-to-3 indexing and first-block strength ownership. The focused transform-state regression passes 3 of 3 direct net11 VSTest cases in Release. Writer, entropy, and OBU coverage passes 1,957 of 1,957 direct net11 VSTest cases in Release, with 20 of 20 focused encoder and decoder chroma-from-luma cases. Partition and mode analysis still need to populate these retained decisions; variable inter-transform syntax remains part of later inter-frame support.
- [ ] Implement legal deblocking, CDEF, restoration, super-resolution, and film-grain signaling decisions.
- [~] The coefficient symbol encoder allocates its bounded level and raster-context workspaces with the encoder state and reuses them for every transform and sequence sample, matching current libaom's fixed-geometry compressor lifetime instead of renting scratch during the first coded frame. Its range coder matches current libaom's 64-bit coding window, bulk big-endian byte flush, and backward carry propagation while using one byte of allocator scratch per estimated output byte instead of the former 16-bit pre-carry storage. Production tiles finalize consecutively inside one encoder-owned bounded output allocation; the OBU writer consumes non-owning tile slices synchronously before the encoder is reset or disposed, so no payload owner transfer, second rent, or full-tile copy occurs. Exact-length test callers retain the copying overload. In-memory allocation coverage proves reset-to-offset and reset-to-zero reuse the same output owner; runtime allocation verification of the complete production multi-tile and sequence paths remains pending.
- [~] The planar conversion, DC intra prediction, residual construction, forward transform, and forward quantizer use descending SIMD dispatch: Vector512, Vector256, Vector128, then scalar. Residual construction matches current libaom's exact source-minus-prediction arithmetic for 8-bit and high-bit-depth planes, preserves independent row strides and unaligned starts, and writes directly into caller-owned signed-short storage without allocation. Candidate distortion reuses that residual workspace and widens signed 12-bit lanes before vector squaring, accumulating exact full-block SSE in 64-bit scalar storage. The composed block path delegates arithmetic to those closed operators and adds no allocation. Apply the same rule to every later hot-path family.
- [~] Residual tests verify misaligned planes, independent source, prediction, and destination strides, SIMD remainders, untouched padding, 8-bit, 10-bit, and 12-bit precision, every operator width independently of host acceleration, the scalar fallback, and zero per-transform allocations.
- [~] The unused coefficient-shape transform facade and its unimplemented N2, N4, and DC-only branches are removed. Finalized block encoding now follows the complete-transform path that current libaom uses before fast quantization; later rate-distortion search may add proven coefficient optimization without exposing inactive runtime throws.
- [~] Forward-quantizer FeatureTestRunner and zero-allocation tests compare every hardware tier with an independent scan-order scalar oracle shaped from current-main libaom. Both passed direct net11 VSTest in Release.
- [~] The combined-frame writer now completes the byte-counted uncompressed frame header before starting the optional multi-tile tile-group flag, matching current libaom's separate frame-header and tile-group writers. A non-uniform two-tile round trip verifies the explicit boundaries, both tile payloads, and complete stream consumption through direct net11 VSTest in Release.
- [~] The first internal frame-to-OBU operation encodes 8-, 10-, and 12-bit monochrome reduced still pictures through the production tile writer and production decoder. Coefficient context initialization now stores `min(abs(level), 127)`, matching current libaom; the previous signed clamp converted every negative transform coefficient to zero and selected invalid nonzero-map distributions. Signed dense and sparse entropy round trips, direct level-buffer saturation coverage, and eight constant/gradient frame cases pass 52 of 52 direct net11 VSTest cases in Release. Current-main `aomdec` accepts all eight emitted payloads. After winner-mode transform refinement, their decoded-frame MD5 values are `d09ea148582b9c93fa78e59426193bbc` (16x16 8-bit constant), `b83eedd5a84428f0120130253b30bdaa` (16x16 8-bit gradient), `f949f7422913e83dff07ee5e0a5087d3` (8x8 8-bit constant), `ae7233a94558978934469dcc4da764dd` (8x8 8-bit gradient), `09223b227f3abc3134d0a3ea15f70c0a` (8x8 10-bit constant), `539aab0e6e14bcaec271febfa8e25444` (8x8 10-bit gradient), `73117a8fc102e5d028f82444fc4d15ab` (8x8 12-bit constant), and `6936a2b62d7220dfb12f3763bb49965d` (8x8 12-bit gradient). This is an independently decodable baseline, not completion evidence for chroma, alpha, options, containers, or the public encoder.
- [x] The exact net11 Release rebuild completed at the established 1,005-warning repository baseline with zero errors. The complete HEIF/AV1 namespace passes 8,838 of 8,838 direct VSTest cases with zero failures or skips. Roslynk reports zero compiler errors and no diagnostics in the five changed C# files; `git diff --check` passes and `.gitattributes` is unchanged.
- [~] The same internal frame operation now produces 4:2:0, 4:2:2, and 4:4:4 payloads at 8, 10, and 12 bits. Twenty-one color cases cover constant and spatially varying input at aligned dimensions plus odd 13x11 visible dimensions for every chroma geometry. The production decoder consumes every payload, the decoded output retains non-neutral chroma, and current-main `aomdec` accepts all 29 monochrome and color outputs. After live spatial chroma mode selection, implicit chroma-transform correction, winner-mode luma-transform refinement, exhaustive chroma-from-luma alpha selection, and filter-intra search, the odd-dimension decoded-frame MD5 values are `9985f05790d2c9f5f28723ef86d5b89b` (4:2:0), `2ba2f1d0fcfef60394a5175553c7cb8b` (4:2:2), and `6aa7a2ed0dbf76ad2ec0c222585272d0` (4:4:4). This proves legal current-libaom payload syntax across native plane geometries; it does not yet prove target quality or native-plane equality with an independently encoded reference.
- [x] Spatial chroma candidates now use the implicit transform derived from the selected UV mode and active transform set, matching current libaom's `intra_mode_to_tx_type` and `av1_get_tx_type` behavior. The same shared derivation is consumed by the decoder, so coefficient scan order, entropy contexts, inverse reconstruction, and encoder rate estimates cannot drift between the two paths. The previous DCT-DCT candidate transform could produce syntactically accepted streams whose non-DC chroma coefficients were interpreted under a different implicit transform. Six production mode-decision cases retain nonzero U and V coefficients and assert the selected transform state across 4:2:0, 4:2:2, and 4:4:4; fifteen exact mapping cases cover every intra mode, reduced sets, and the 32x32 DCT-only fallback. The focused contract passes 21 of 21 direct net11 VSTest cases, the complete HEIF/AV1 namespace passes 8,947 of 8,947, the exact Release rebuild remains at 1,005 warnings and zero errors, and current-main `aomdec` accepts all 29 regenerated payloads.
- [~] Luma mode selection now evaluates each of its 61 mode-and-angle candidates with the mode-derived default transform used by current libaom's fast intra path. It then refines only the winning mode across all seven transform types permitted by the 8x8 intra set in transform-enum order. This removes the fixed DCT-DCT limitation while avoiding a 61-by-7 expansion; each trial includes live transform-type and coefficient rate, reconstructed pixel-domain distortion, and the existing aligned reusable block workspace. Eighteen exact-prediction production cases prove DCT-DCT wins equal-cost ties in reference order even when the first pass used a different default, while the 72x72 textured traversal proves a non-DCT transform with nonzero coefficients reaches retained syntax. Current-main `aomdec` accepts all 29 regenerated payloads. Special-mode transform-size coverage, full partition search, and broader effort-dependent joint mode/transform search remain.
- [x] Chroma-from-luma mode decision now reuses the decoder's SIMD-first 4:2:0, 4:2:2, and 4:4:4 reconstructed-luma preparation and prediction kernels for both byte and high-bit-depth encoder operators. The constant DC predictor for each chroma plane is computed once and its sample refills every alpha candidate, matching libaom's per-plane DC cache instead of rebuilding the same edge average 33 times. Each block uses 512 bytes of fixed stack scratch for the maximum 8-row predictor surface plus 792 bytes for complete U/V rate and distortion tables; no allocator owner, managed object, frame copy, or persistent buffer was added. Live probability costs exactly mirror current libaom's joint-sign ownership and conditional magnitude symbols. Nine production cases independently derive exact CfL targets from decoder-visible reconstructed luma at 8, 10, and 12 bits, and three entropy cases cover two nonzero signs plus each single-zero-plane form. The exact net11 Release rebuild remains at 1,005 warnings and zero errors, all 8,959 HEIF/AV1 tests pass, and current-main `aomdec` at `a40ed1ea9e4ecc3df58a5bccb76623f2c94ae727` accepts all 29 regenerated payloads.
- [x] Filter-intra mode decision now runs after ordinary luma modes in current-libaom order, evaluates all five recursive predictors, and refines each predictor across every legal 8x8 transform in transform-enum order. Strictly-better replacement preserves ordinary-mode and filter-mode tie order. Each filter prediction and its source residual are prepared once and reused across transform candidates, avoiding repeated recursive prediction while retaining SIMD-first predictor and subtraction operators. The stack cost is 192 bytes for eight-bit samples or 256 bytes for high-bit-depth samples; no allocator owner or managed buffer was added. Fifteen production cases force every filter mode at 8, 10, and 12 bits and prove retained filter syntax, zero-residual reconstruction, and the DCT-DCT equal-cost transform tie. The decoded-frame MD5 values selected by this checkpoint are `d7d68803763b95827483f14515281d3a` for the 8x8 10-bit gradient, `3f7e34d44c65d7797ad26b5cd4c35bf4` for the 8x8 12-bit gradient, and `9985f05790d2c9f5f28723ef86d5b89b`, `2ba2f1d0fcfef60394a5175553c7cb8b`, and `6aa7a2ed0dbf76ad2ec0c222585272d0` for the odd 4:2:0, 4:2:2, and 4:4:4 gradients. The exact net11 Release rebuild remains at 1,005 warnings and zero errors, 18 focused filter-intra, predictor-reference, syntax-cost, and allocation cases pass, all 8,974 HEIF/AV1 tests pass, and current-main `aomdec` at `a40ed1ea9e4ecc3df58a5bccb76623f2c94ae727` accepts all 29 regenerated payloads.
- [~] The historical ordinary-intra empty-transform skip experiment was superseded by the source-led correction above. Production ordinary-intra blocks now remain non-skipped, including all-zero transforms. The obsolete helper and its direct test remain after automatic review rejected their removal; they have no production caller. The earlier 8,975-case report and 29 decodable payloads established neither reference-controller parity nor current-tree correctness.
- [~] Palette entropy coding now mirrors current libaom's adaptive luma-mode, chroma-mode, palette-size, and spatial color-index distributions, together with its truncated-binary uniform code used by palette colors. The complete mutable palette probability graph is created once on first palette search or write, so the current palette-disabled frame path retains zero palette allocations. Three focused regressions cover every legal 2-through-8 color alphabet and every defined mode, size, and color-index context; all 1,928 entropy cases and all 8,978 HEIF/AV1 cases pass direct net11 Release VSTest. The exact Release rebuild remains at 1,005 warnings and zero errors. This checkpoint adds the exact entropy foundation only: palette candidate generation, retained color and index storage, mode decision, map tokenization, and production syntax remain incomplete, and no generated payload changed.
- [~] Luma and chroma palette-color coding now matches current libaom's neighbor-cache flags, sorted delta representation, wrapped V-plane deltas, strict delta-versus-raw V selection, and fixed-point color-rate model at 8, 10, and 12 bits. Encoder costing and emission use only fixed stack spans, including explicitly initialized cache-membership state, and steady-state color costing allocates zero managed bytes. The decoder consumes the same bounded color-syntax primitive after the tile reader derives its neighbor cache, removing duplicated color parsing without changing retained palette ownership. Nine focused syntax, exact palette decode, constrained-allocation, truncation, presentation, and allocation cases pass; all 1,933 entropy cases and all 8,983 HEIF/AV1 cases pass direct net11 Release VSTest. The exact Release rebuild remains at 1,005 warnings and zero errors. Retained encoder palette colors, neighbor caches, color-index maps, candidate generation, and production palette selection remain incomplete, and the compact 8-byte frame mode entries were not enlarged.
- [~] Palette color-index map coding now shares the exact current-libaom neighbor weights, stable color ordering, five context classes, first-index uniform code, and diagonal wavefront between encoder costing, encoder writing, and decoder parsing. The decoder's stack-allocated context scores are explicitly cleared before accumulation, removing an invalid dependency on uninitialized stack contents. Costing and writing use a closed generic operation while the shared driver owns traversal and context derivation, so the semantic operations remain independent of map layout and tail handling. The path adds no retained state or per-call managed allocation. Its allocation regression now runs one complete unmeasured hot-path window before measuring an independent 1,000-call steady-state window, so tiered-runtime transitions cannot make the full parallel suite report a one-time allocation as a recurring operation cost. Twelve focused map, exact palette decode, padding, trailing-bit, and allocation cases pass; all 1,941 entropy cases and all 8,991 HEIF/AV1 cases pass direct net11 Release VSTest. The exact Release rebuild remains at 1,005 warnings and zero errors. Production payloads remain unchanged because palette selection is still disabled; retained colors, neighbor caches, index-map storage, candidate generation, and production palette mode decision remain incomplete.
- [~] Retained encoder palette state and production palette writing now mirror current libaom's 50-byte palette-mode contents, separate luma and shared-chroma sizes, three eight-color planes, above-and-left sorted cache, 64-sample above-cache boundary, mode contexts, palette colors, color-index maps, and syntax order. The current block keeps one inline value in the reusable superblock workspace; only the 4x4-granularity top and left picture edges retain copies for later blocks. For a 3840x2160 tile these edges occupy about 73.4 KiB instead of about 6.2 MiB for a 50-byte palette value attached to every 8x8 mode allocation. Luma and chroma index maps occupy a fixed 32 KiB region of the single 40.3 KiB superblock-workspace owner. That owner is allocated with encoder state, matching libaom's compressor-state lifetime while removing libaom's separate palette allocation and cleanup path. The compact final-block decision region remains about 8.3 KiB. The writer caps map traversal to the coded plane count, writes maps before transform syntax, and publishes palette edges only after the current block has consumed preceding contexts. The previous eight focused size, alignment, ownership, cache-boundary, round-trip, map-consumption, and edge-publication cases passed with all 114 palette cases, all 1,942 entropy cases, and all 8,996 HEIF/AV1 cases through direct foreground net11 Release VSTest. The current one-owner refactor has zero Roslynk compiler errors; runtime verification remains pending. The current source reference is official libaom main at `d565eec60f084421fa34fc0534b760c6452b6a6c`.
- [~] Luma palette clustering now follows current libaom's one-dimensional search primitive exactly: equal-interval midpoint initialization, first-color tie order, rounded centroid means, deterministic empty-cluster replacement, the 50-iteration limit, and retention of the preceding state when distortion increases. Nearest-color assignment dispatches Vector512, Vector256, Vector128, then scalar through ImageSharp's shared vector-count helpers. Wider dispatch alone does not establish an end-to-end performance improvement. The primitive uses only bounded stack scratch and introduces no allocator rent, managed array, or per-row copy. Three independent tests cover exact centroid convergence, initialization order, 12-bit nearest-color distortion, destination bounds, and every hardware-intrinsic tier. The complete AVIF set passes 8,930 of 8,930 cases and the HEIF set passes 230 of 230 cases through direct foreground net11 Release VSTest. The exact net11 Release rebuild reports 1,050 solution warnings and zero errors, and Roslynk reports zero compiler errors. Candidate enumeration, palette-cache snapping, transform RD selection, and production activation remain in the open luma-palette checkpoint.
- [~] Live luma palette selection now follows current libaom's dominant-color and one-dimensional K-means candidate families, cache-bias threshold, sorted duplicate removal, active-edge map extension, and strict winner tie order. It evaluates both candidate families at every legal 2-through-8 size without the reference's speed-dependent pruning, then evaluates every legal transform. This is a controller deviation; evaluating more candidates does not establish improved quality, speed, or reference parity. Candidate storage remains bounded stack memory; the reusable maps come from the fixed encoder-lifetime superblock workspace, so palette search cannot introduce a first-use allocation. A production tile test proves that full 8x8 and clipped 5x3 blocks at 8 and 12 bits select exact colors and indices, extend the visible edges through coded padding, reconstruct every sample without coefficients, and emit a nonempty tile. The complete 57-case intra-superblock set, 8,931-case AVIF set, and 230-case HEIF set pass direct foreground net11 Release VSTest. The exact Release test-project build reports 1,992 baseline warnings and zero errors; Roslynk reports zero compiler errors and no touched-file analyzer warnings. Production frame activation remains gated until chroma palette mode and its rate accounting are complete.
- [~] Paired chroma palette clustering now preserves current libaom's squared two-component distance, first-centroid tie order, independently rounded U/V means, paired deterministic empty-cluster replacement, preceding-state retention on increased distortion, and 50-iteration limit. The source planes remain separate, with Vector512, Vector256, Vector128, then scalar dispatch through ImageSharp's shared vector-count helpers. An end-to-end improvement over the native implementation has not been established. Three independent tests cover exact paired convergence, midpoint initialization, 12-bit distance and index parity, untouched destination bounds, and every intrinsic tier. The exact Release test-project build reports 1,992 baseline warnings and zero errors; the focused three-case set, complete 8,934-case AVIF set, and complete 230-case HEIF set pass direct foreground net11 Release VSTest. Roslynk reports zero compiler errors and no touched-file analyzer warnings. Candidate integration and production activation remain in the open chroma-palette checkpoint.
- [~] Live paired chroma palette selection now follows current libaom's complete 2-through-8 color-size search, U-plane neighbor-cache snapping, stable U-ordered color pairs, shared U/V index map, implicit DCT-DCT transform, and strict rate-distortion winner replacement. It omits the reference's early header-cost pruning, keeps planar U/V source data separate, and reuses the existing prediction, residual, transform, quantization, and reconstruction operators. Omitting pruning is an unresolved decision-policy deviation, not an established improvement. The production tile regression proves both palette-mode probability branches, exact paired colors and indices, coefficient-free reconstruction, and nonempty syntax. The complete 58-case intra-superblock set, 8,935-case AVIF set, and 230-case HEIF set pass direct foreground net11 Release VSTest. The exact Release test-project build reports 1,992 baseline warnings and zero errors; Roslynk reports zero compiler errors and no touched-file analyzer warnings. Production frame activation remains the next checkpoint.
- [x] Production screen-content activation now matches current libaom's default good-quality detector: it scans only complete 16x16 luma blocks, normalizes palette samples to eight bits, admits 2-through-4-color blocks, and uses the reference's strict greater-than-ten-percent frame-area threshold. The same pass accumulates centered sums and squared sums at native precision, applies libaom's exact 10-bit and 12-bit variance rounding, and enables intra-block copy only when positive rounded per-pixel variance exceeds its strict one-twelfth frame-area threshold. A 256-bit stack bitset and fifth-color early exit replace libaom's larger per-block histogram without a second source scan or allocation. The adaptive sequence flag remains enabled and both frame flags are fixed before picture-state allocation. Focused regressions prove strict palette-threshold equality, high-bit-depth normalization, the exact variance rounding boundary, five-color rejection, emitted frame-header activation, actual production IBC selection, and production decode. The exact Release test-project build reports 1,992 baseline warnings and zero errors; all 9,242 non-HEVC HEIF/AV1 cases pass direct foreground net11 Release VSTest. Current-main `aomdec` at `a40ed1ea9e4ecc3df58a5bccb76623f2c94ae727` accepts all 31 payloads regenerated by the current test tree, including an actual IBC-coded 328x16 stream with decoded MD5 `677435e5af39c930af1178f91c34af6a`. Roslynk reports zero compiler errors and no touched-file analyzer warnings.
- [~] Intra-block-copy rate accounting now uses the live frame-local flag and displacement-vector distributions without copying or adapting either context during candidate measurement. Displacement-vector costing and writing share one closed symbol operation over the exact current-libaom joint, sign, magnitude-class, class-zero, and integer-offset syntax; final mode evaluation applies libaom's 120/128 displacement-rate weight with nearest-integer rounding. Independent fixed costs cover all four joint states, both signs, class zero, and large offset classes before adaptive writes, followed by an encoder/decoder round trip through the same sequence. Encoder and decoder reference-vector derivation now share the exact eight-candidate spatial scan, independent nearest and outer-region ranking, top-right partition geometry, clamping, and tile-relative fallback. Selected vectors use a naturally aligned pair of signed 16-bit components packed into the existing picture-state owner only when intra-block copy is permitted; a 3840x2160 frame retains 130,560 vectors in 510 KiB while leaving the compact 8-byte mode allocation unchanged. The tile writer derives the same reference and emits the retained vector without another allocation or copy. Coefficient costing and writing now select the inter transform sets and frame-local probability tables required by intra-block copy; independent tests verify every legal symbol against the exact default inter distribution and round-trip full and reduced sets from 4x4 through 32x32. Legal 8x8 hash discovery now indexes every visible source origin, including unaligned origins, in libaom's coarse-to-fine insertion order with the same 256-candidate bucket cap. A separable rolling hash fills one packed picture-lifetime workspace before reconstruction, then reuses that workspace for integer candidate links; exact wide or SIMD block comparison rejects hash collisions, and SIMD variance uses libaom's eight-bit normalization at 8, 10, and 12 bits. Power-of-two bucket arrays scale down with small images and stop at the reference's 16-bit limit, avoiding libaom's fixed six-size pointer table; the 3840x2160 search index occupies about 32.2 MiB and introduces no additional owner or frame copy. Above and left search rectangles, integer displacement legality, strict tie order, and live raw displacement rate follow current libaom. Motion-candidate ranking uses libaom's undiscounted probability cost and exact variance-domain error-per-bit scaling, separately from the later 120/128 final-mode discount. The allocation-free full-pixel core now follows current libaom's NSTEP search: it clamps the spatial reference to each legal region, traverses the fixed 15-stage radii and site order, skips equivalent centered 210-pixel stages, repeats progressively shorter paths, and compares their winners in the normalized variance domain. Paths above the speed-zero screen-content threshold continue through libaom's 256-pixel, one-pixel-step exhaustive mesh. Four adjacent byte or high-bit-depth candidates share each SIMD source load, strict row-major tie ordering is retained, and the final legal tail column remains searchable where libaom's current four-wide remainder loop omits it. Byte and high-bit-depth operators compute each 8x8 absolute difference with Vector128 before scalar fallback; high-bit-depth SAD remains in its native sample scale while its quantizer-derived rate multiplier uses libaom's normalized AC step. Production mode decision now derives the same spatial displacement reference used by the writer, deduplicates hash and full-pixel finalists in search order, and evaluates every surviving vector through complete luma and chroma transform RD. This differs from libaom's preliminary-error pruning by permitting a hash and pixel finalist from the same search region to compete using final syntax and reconstruction costs. It is an unresolved controller deviation, with no established quality or performance improvement. Prediction is prepared once per plane and vector, including integer or half-sample chroma phase, then reused across every legal inter transform without an allocator rent or frame copy. The joint comparison includes the live intra-block-copy flag, discounted displacement rate, skip flag, coefficient syntax, and normalized Y/U/V distortion. The historical implementation required an empty transform and a strictly lower skip cost; the 2026-09-07 residual-skip correction replaces that rule with prediction-only residual RD, including ties. Conventional intra and earlier vectors still retain mode-selection tie precedence. Winning reconstruction, coefficients, transform state, DC modes, cleared palette/filter/CfL state, and displacement are copied once into the existing retained stores. Production regressions force the path at 8 and 12 bits and force 4:2:0 horizontal half-sample chroma with an unaligned reference. The former bulk local workspace occupied 2.75 KiB for byte samples or 3.375 KiB for high-bit-depth samples. Prediction, candidate, winning reconstruction, residual, and coefficient scratch now occupy one naturally aligned 3.125 KiB extension of the existing frame-reused block-workspace owner, matching libaom's reusable macroblock-scratch lifetime without adding an allocation; only the 128-byte reference, weight, and finalist arrays remain on the stack. The net11 Release solution build reports zero errors; all 2,082 focused transform, entropy, intra-block-copy, intra-superblock, and frame-encoder cases and all 9,242 non-HEVC HEIF/AV1 cases pass through direct foreground VSTest, with tiered compilation disabled only for the full allocation-sensitive suite. The frame flag remains authoritative through tile coding. Complete adaptive search activation and reference-controller parity are unverified.
- [x] The expanded checkpoint exposed a pre-existing transform-block test that asserted uninitialized pooled padding was zero. The test now initializes the complete physical luma plane with a sentinel and proves the block operation leaves both adjacent padding samples unchanged. The exact net11 Release rebuild remains at 1,005 baseline warnings and zero errors, the focused allocator-order set passes 30 of 30 cases, and the complete HEIF/AV1 namespace passes 8,859 of 8,859 direct VSTest cases with zero failures or skips.
- [x] Combined-frame OBU output now counts the byte-aligned frame and tile-group headers, non-final tile-size fields, and owned tile payloads before emitting the OBU size. It retains only the small allocator-owned header scratch and writes each entropy-coded tile span directly from its detached owner, removing the second file-sized allocator rent and complete-payload copy. A 64 KiB regression proves exactly one sub-payload-sized byte rent with a balanced return and verifies the exact streamed tile tail; the existing two-tile round trip proves size-prefix and ordering parity. The focused writer and production-frame set passes 32 of 32 direct net11 VSTest cases, current-main `aomdec` accepts all 29 generated native-format payloads, and the complete HEIF/AV1 namespace passes 8,860 of 8,860 cases with zero failures or skips.
- [ ] The earlier fixed-block skip checkpoint was not equivalent to libaom's ordinary-intra policy.
  Its all-zero-EOB conjunction produced valid streams, but ordinary intra retains non-skip syntax in the
  reference. The takeover correction above aligns both fixed-DC traversal and live mode selection.
  Earlier decoder acceptance, unchanged frame hashes, and one-byte size reductions did not establish
  encoder-policy parity; the earlier 8,862-case result is historical evidence only.
- [x] Operation-wide allocation tracking now exercises a real 64x64 12-bit 4:4:4 frame through packed-pixel conversion, both native frame owners, picture and coefficient state, reusable block workspaces, entropy coding, OBU framing, and a non-seekable destination. It proves exactly one 60 KiB tile-output reservation from current libaom's all-intra 2.5x rule and balanced exactly-once returns for every tracked allocation before the operation completes. The focused ownership case passes 1 of 1 and the complete HEIF/AV1 namespace passes 8,863 of 8,863 direct net11 VSTest cases with zero failures or skips.
- [x] HEIF box offsets are now counted from the start of the encoded file instead of reading `Stream.Position`. This preserves ISO BMFF file-relative `iloc` offsets when the destination begins at a nonzero position and permits non-seekable output. Decoder item extents and image-sequence chunk offsets now resolve from that same file origin rather than the backing stream origin. Real legacy-JPEG HEIF round trips cover non-seekable output and a prefixed destination, while current-position AV1 decode covers both a still item and a five-frame sequence. All 96 encoder/decoder cases and all 38 sequence-parser cases pass direct net11 Release VSTest; the Release build remains at the established 1,005-warning baseline with zero errors.

- [~] Uniform luma transform search currently changes ownership and enumeration at managed effort thresholds. Those thresholds and winner-only shortcuts are not validated native policy. The implementation reuses prediction/residual workspace and retains trial coefficient contexts and reconstruction, but candidate-stage versus winner-stage transform policy and cache invalidation still need the complete reference controller. Historical decodability and component-test results do not establish encoder parity or performance.

- [x] Intra-block-copy transform search now prepares motion compensation and subtraction once per plane, alternates the existing candidate and selected work buffers whenever a transform improves, and performs at most one final normalization copy into the caller-owned selected span. This matches current libaom's pointer-swap ownership without adding an allocation or a third reconstruction buffer. Inline documentation now records the scratch lifetime, strict transform tie order, skip-rate replacement, unsplit transform-root syntax, joint-plane winner retention, and final publication boundary. The focused Release encoder and intra-block-copy set passes 17 of 17 cases, the complete non-HEVC HEIF/AV1 namespace passes 9,301 of 9,301 cases with zero failures or skips, and current-main `aomdec` accepts the regenerated effort-five and effort-six intra-block-copy streams.

- [x] Uniform four-by-four luma transform search now uses two compact reconstruction and coefficient views already available in the aligned mode-decision workspace. Legal transform trials write into the non-winning view and exchange span ownership only on strict rate-distortion improvement; the selected coefficients and strided reconstruction mosaic are published once after the type search so the next raster transform sees the required decoded edge. This removes reconstruction and coefficient copies on every improving transform without adding storage, changing tie order, or repeating a transform. All four representative palette, filter-intra, and effort-eight output hashes are unchanged, the focused Release set passes 13 of 13 cases, and current-main `aomdec` accepts every checked stream.

- [x] Candidate distortion now follows the reference separation between immutable source residuals and reconstructed-pixel error. The forward-transform boundary accepts a read-only residual, so all transform types for one prediction reuse that block directly instead of copying it into transform scratch before every trial. The shared residual API now measures strided source-versus-reconstruction squared error with documented Vector512, Vector256, Vector128, and scalar traversal through ImageSharp's vector-count helpers, eliminating the former residual destination write and second reduction pass. The same path covers ordinary intra, filter intra, palette, chroma-from-luma, split transforms, and intra-block copy at 8, 10, and 12 bits without an allocation. Independent scalar, stride, tail, intrinsic-tier, and zero-allocation coverage passes with the 140-case focused encoder set; the complete non-HEVC HEIF/AV1 namespace passes 9,301 of 9,301. Representative palette, filter-intra, and effort-eight output hashes remain byte-identical, and current-main `aomdec` accepts every checked stream.

- [x] Lossless still-image coding now follows current libaom's qindex-zero path without introducing a per-block allocation or a second frame buffer. The forward 4x4 Walsh-Hadamard transform and its transpose into the entropy pipeline's row-major coefficient order are allocation-free at Vector512, Vector256, Vector128, and scalar tiers; lossless quantization reconstructs the original transform coefficient exactly. The mode decision fixes lossless transforms to DCT-DCT syntax and four-by-four blocks, disables transform skip for nonzero residuals, and excludes the fixed-eight-by-eight intra-block-copy search that cannot represent the required lossless transform grid. The frame coefficient owner reserves the exact worst-case 2,048 transform states needed by both 128x128 4:4:4 chroma planes without adding an allocation. The color configuration derives its plane count from the monochrome flag, so high-bit-depth direct-frame writers and readers cannot retain contradictory mutable state. Public 8-bit, 10-bit, and 12-bit color and auxiliary-alpha round trips are pixel exact on their native sample lattices; direct 10-bit and 12-bit 4:4:4 frame round trips are also exact at native-plane precision. The Release build completes with zero errors, Roslynk reports zero compiler errors, and the complete non-HEVC HEIF/AV1 namespace passes 9,081 of 9,081 through one foreground VSTest run. An independently built generic `aomdec` from current official libaom `main` at `d565eec60f084421fa34fc0534b760c6452b6a6c` accepts all 59 current payloads, including the public color and auxiliary-alpha lossless streams at every supported precision.

### 7. Write complete AVIF output

- [~] The encoder-side AV1 codec configuration is now derived directly from the encoded sequence header and writes the fixed four-byte `av1C` record with empty `configOBUs`. The image payload retains the required sequence header, so the property introduces no sequence-header allocation, retention, or copy. Four production-header cases cover main, high, and professional profiles; 8-, 10-, and 12-bit precision; monochrome, 4:2:0, 4:2:2, and 4:4:4 sampling; exact fixed bytes; decoder reparsing; and header/property equivalence through direct net11 Release VSTest. Property-container emission and public AVIF activation remain open.
- [~] AV1 image properties now write `ispe`, `pixi`, `av1C`, `colr`, and `auxC` in current AVIF item order. Only `av1C` is essential; color and alpha items retain independent property sets and the registered alpha auxiliary type. The property container reacquires its span after nested expansion before patching `ipco`, removing the prior stale-buffer write, and selects compact or 15-bit `ipma` indices from the property count rather than the unrelated item count. A forced-growth color-plus-alpha case validates every property payload and association byte; a separate 43-item, 129-property case proves indices 127 through 129 and the extended essential bit. Both pass direct foreground net11 Release VSTest. Complete AVIF assembly remains open.
- [~] Explicit public AV1 encoding now writes a still-image AVIF with `avif` major brand, compatible `avif`, `mif1`, and `miaf` brands, one primary color item, an optional alpha auxiliary item, `auxl` from alpha to color, independent item properties, absolute version-one `iloc` extents, and a shared `mdat`. Quality uses current libaom's quantizer-to-qindex mapping with public quality 100 deliberately clamped from lossless qindex 0 to qindex 4. Effort controls the implemented search stages, and the resolved value is required explicitly by every internal frame, tile, and mode-decision operation rather than repeated as optional defaults. Encoder options take precedence over source metadata for 8-, 10-, and 12-bit monochrome, 4:2:0, 4:2:2, and 4:4:4 output. Alpha derives from the source pixel type without scanning pixels, and incompatible identity-matrix metadata is normalized without mutating the source image.
- [~] The production path writes color and alpha payloads sequentially through allocator-backed chunked storage, supports non-seekable and prefixed destinations, and does not materialize a complete file or payload copy. Uniform encoder-side `pixi` depth is written directly without allocating per-item channel-depth arrays; decoder-side non-uniform channel depths remain supported. The Release test project builds with zero errors, all 39 encoder cases pass, the complete non-HEVC HEIF namespace passes 9,277 of 9,277, and current official libaom accepts all 47 generated payloads.
- [x] Still-image AVIF metadata preservation now writes an unrestricted ICC `colr/prof` property before the independent `colr/nclx` property, Exif and XMP as separate `mdat` items, and one `cdsc` relationship from each metadata item to the primary color item. Exif stores the exact big-endian TIFF-header offset required by the HEIF item syntax; XMP uses the `mime` item type and `application/rdf+xml` content type. Existing ICC and XMP storage is read synchronously and copied once into final encoder storage rather than cloned into an intermediate array. `SkipMetadata` suppresses all three profile types while retaining the CICP values required to describe the encoded planes.
- [x] Exact container tests verify every emitted item declaration, name, MIME content type, `cdsc` relationship, Exif offset and payload, XMP payload, ICC/CICP property order, compact association byte, propertyless metadata exclusion, decoded profile value, and both `SkipMetadata` branches. The final HEIF encoder set passes 44 of 44 and the complete JPEG encoder set passes 257 of 257 through direct foreground net11 Release VSTest. The complete non-HEVC HEIF namespace passes 9,282 of 9,282 with no failure, crash, or detached test host, and current official libaom accepts all 47 current generated AV1 payloads.
- [x] A code-wide production HEIF/AV1 stack-storage audit, excluding HEVC, removed every block-sized, variable-length, or repeatedly nested scratch buffer. Spatial luma and chroma, filter-intra, chroma-from-luma, luma and chroma palette selection, and K-means iteration now use typed views over 642 signed-integer elements, about 2.51 KiB, at the start of the shared inter-prediction region. Those searches are sequential for one block, so the block-workspace owner does not grow and no rent, copy, or additional lifetime is introduced. CDEF directions, variances, and its 64-entry block list now append 1 KiB to the existing bounded operation owner instead of occupying hidden inline or explicit stack arrays. No remaining `stackalloc` depends on block dimensions, sample count, or runtime length; the largest remaining individual span is 128 bytes, and the remaining sites are fixed syntax, SIMD-lane, filter-tap, plane-metadata, or small candidate storage. The exact-owner test now proves the mode, palette, and reference-prediction views share one allocation. Roslynk reports zero compiler errors and no diagnostics in the changed files, the Release test-project build completes with the established 1,992 warnings and zero errors, 81 of 81 focused cases pass, and the complete non-HEVC HEIF/AV1 namespace passes 9,282 of 9,282 through one foreground net11 VSTest run.
- [~] Bounded public image-sequence output now emits an `avis` movie with version-one movie, track, and media headers; AV1 visual sample entries; exact run-length-compressed timing; per-sample sizes; 64-bit chunk offsets; and an explicit sync-sample table. The file type includes the required `miaf` compatibility brand, and every sequence now has the MIAF primary image item emitted by current libavif: normal sequences share the first sync-sample extent without another encode or copy, while separate-root sequences retain the still root as the primary image and begin timed samples at frame index one. The decoder allocates one final `Image<TPixel>`: the root is either the first timed sample or the separately decoded primary item, and each visible timed sample is decoded directly into a frame owned by that image. Exact quarter-turn presentation uses one frame-sized reusable pre-rotation buffer rather than a second image or a separately built frame collection. Color and optional auxiliary alpha use independently configured AV1 tracks linked by `auxl`. Lossless samples remain independently decodable key pictures and repeat the sequence header required for random access. Lossy continuation samples use LAST_FRAME inter prediction through the existing SIMD translational predictor; one track-scoped encoder session reuses its source allocation, packed-to-planar row storage and color converter, frame-sized coefficient storage, fixed-geometry picture and frame-header syntax state, tile/superblock/entropy cursors, block arithmetic workspace, complete probability graph, bounded tile-output owner, and OBU-header owner, and swaps two complete reconstruction buffers so the preceding decoded frame becomes the next reference without a plane copy. Sequence samples signal `still_picture=0` and use the complete non-reduced sequence and frame-header prefixes required for a multi-frame coded sequence. Frame payloads are written once into contiguous allocator-backed chunks per track. One compact managed table retains only offset, length, and duration for both tracks, and the bounded `moov` owner is patched once after its final size is known, so prefixed and non-seekable destinations require neither seeking nor a file-sized copy. The media timescale uses the exact representable least common multiple of animated frame-delay denominators and a documented microsecond fallback; zero delays become the smallest legal positive duration. Public lossless color-and-alpha round trips preserve all frames, distinct 24, 25, and 30 fps delays, finite or infinite repetition, ICC, Exif, and XMP metadata, while a separate case proves prefixed non-seekable output. Per-tile CDEF preset, preceding-quantizer state, and payload bounds now occupy one aligned region in the reusable allocator-owned picture buffer rather than separate managed arrays for every frame. The last verified net11 Release checkpoint completed with the established 1,005 warnings and zero errors, all 48 HEIF encoder cases passed, and the complete non-HEVC HEIF/AV1 namespace passed 9,317 of 9,317 through foreground VSTest. Current official libaom `main` at `d565eec60f084421fa34fc0534b760c6452b6a6c` accepted all 66 raw AV1 payloads regenerated by that suite. Verification of the current primary-item, separate-root, non-reduced sequence-header, retained-reference continuation, grid implementation, block-local motion search, conversion-row, probability, picture, frame-header, tile-cursor, tile-output, and OBU-header reuse, and multi-tile output is pending. Additional reference roles and compound prediction remain open.
- [~] Oversized still-image encoding now writes a derived AVIF grid when either source dimension exceeds the AV1 frame-header limit. Cells are encoded row-major from source rectangles without cropping to temporary images. Every coded cell uses the same at-most-65,536-sample extent for current-reader interoperability, with edge replication supplying the AVIF minimum 64-sample dimension and any final-row or final-column crop. Color and optional alpha grids use hidden AV1 items, ordered `dimg` references, one shared property set per plane, and independent descriptor payloads. The item-property length calculation counts every reused `ipma` association while retaining one `ipco` property definition. Grid dimensions that are odd along a requested subsampled axis promote the resolved color sampling to 4:4:4, for both explicit and default options. Exact descriptor, hidden-flag, reference-order, property-reuse, cell-padding, and public round-trip coverage is present, and Roslynk reports zero compiler errors. Runtime verification remains pending.
- [~] Write the correct AVIF file type, item information, locations, references, properties, AV1 configuration, dimensions, color, alpha, metadata, and media data.
- [~] Support single images, alpha auxiliary images, grids, multiple extents, and bounded image sequences in the final public scope.
- [x] Preserve ICC, Exif, and XMP according to encoder options.
- [~] Write CICP, range, chroma position, bit depth, and subsampling values that match the encoded planes.
- [x] Encode the current ImageSharp pixel lattice without inventing HEIF clean-aperture, rotation, or mirror properties. The HEIF decoder materializes those container transforms before returning an image, while ImageSharp encoders consistently preserve explicit Exif metadata without implicitly changing the pixels.
- [~] Stream output through allocator-backed chunked storage without file-sized copies or ToArray materialization.

Encoder exit gate:

- [ ] Current-main libaom accepts the payloads regenerated from the final tree.
- [ ] Reverify lossless output at public pixel and native-plane precision for 8-, 10-, and 12-bit output.
- [ ] Encoder parity: separately encoded lossy results differ by no more than one component unit per sample with reconciled settings; report maxima and counts exceeding one.
- [ ] Record equivalent end-to-end absolute timing, output size, quality, and allocation evidence after the source audit.
- [ ] 8, 10, and 12-bit monochrome, 4:2:0, 4:2:2, and 4:4:4 outputs pass.
- [ ] Alpha, grids, metadata, color profiles, transforms, and bounded sequences pass.
- [ ] ImageSharp decode of its own output is supplemental coverage only, never the sole oracle.
- [ ] Verify every exposed encoding combination through established ImageSharp conversion behavior.
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
- Inline comments explain the current-libaom numerical rule, ownership boundary, edge extension, entropy ordering, or SIMD shape at technically complex points.
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
- [ ] Exact native-plane comparisons against current-main libaom.
- [ ] Established final-presentation comparisons at the target pixel precision.
- [ ] Scoped StyleCop and vertical-whitespace inspection.
- [ ] No stale unsupported capability claims or removed-code references.
- [ ] No restore-source failures, background test hosts, detached processes, or crash-report popups.
- [ ] .gitattributes unchanged.
- [ ] git diff --check clean.
- [ ] Documentation records exact commands, counts, current-main reference revision evidence, and results.
- [ ] Commit only after the relevant checkpoint is genuinely complete.
- [ ] Do not push.
