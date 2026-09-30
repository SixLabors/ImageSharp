# v5 ICC Preserve implementation plan

## Contract and scope

Implement the v5 behavior in [issue #3198](https://github.com/SixLabors/ImageSharp/issues/3198#issuecomment-5755302573), whose complete discussion was read with `gh issue view` on 2026-09-30:

1. Decode through the original ICC profile into sRGB working pixels.
2. Retain that profile and the source format's color type in the existing image/frame metadata.
3. When the destination supports the profile and color type, convert sRGB back to that profile during encoding and embed the original profile.
4. Encode without changing the source pixels, palettes or metadata.

CMYK → sRGB → CMYK is one example. The contract applies across ICC-capable formats. The round trip aims to preserve appearance within the limitations of the sRGB intermediate; it cannot recover original channel values or out-of-gamut colors.

`Preserve` changes its decoding behavior. `Convert` already performs conversion and remains a regression baseline. Retain the existing `Convert`/`Compact` profile-removal rules. `Identify` parses metadata without transforming colors. `SkipMetadata` must not suppress ICC parsing, conversion or emission.

The ICC converter supports at most four input/output color channels. [ClutCalculator](src/ImageSharp/ColorProfiles/Icc/Calculators/ClutCalculator.cs) enforces this. Keep that limit and its existing failure behavior. This work does not add generic multichannel conversion.

## Execution rules

**Finish and verify every decoder step D1–D8 before starting any encoder step E1–E12.**

Work on the dedicated branch `js/fix-icc-preserve-3198`. The current working tree contains unfinished changes to [ColorProfileConverterExtensionsPixelCompatible](src/ImageSharp/ColorProfiles/ColorProfileConverterExtensionsPixelCompatible.cs) and [PngEncoderCore](src/ImageSharp/Formats/Png/PngEncoderCore.cs). They are not evidence of completed work and must not be extended before the decoder gate.

For each step: implement its listed change, review the diff, then run only the affected existing tests in Release on `net10.0`. Add coverage only where the changed behavior is not already proved. Record the actual command/result before advancing. Decoder tests must not depend on an unfinished return-transform encoder.

Use existing generic conversion APIs, memory ownership and codec packers. Preserve the existing image conversion overload and `PixelConversionModifiers.Scale` behavior. No encoder base class, conversion-history flag, replacement sRGB metadata profile, color-specific converter methods or palette save-and-restore workaround is required.

## Shared color and metadata decisions

### Color-type representation

Extend [PixelColorType](src/ImageSharp/PixelFormats/PixelColorType.cs) with the missing named color types XYZ, Lab, Luv, Yxy, HSV, HLS and CMY. Compose new types from shared-component flags, keep existing defined component and type values with alpha independent, and place `Other` last. Use the existing [PixelTypeInfo](src/ImageSharp/PixelFormats/PixelTypeInfo.cs) and format-connecting metadata contracts.

Retain source color types through each codec's metadata. Update both image and frame mappings: TIFF Lab currently falls through to `Other` in [TiffMetadata.GetPixelTypeInfo](src/ImageSharp/Formats/Tiff/TiffMetadata.cs) and [TiffFrameMetadata.ToFormatConnectingFrameMetadata](src/ImageSharp/Formats/Tiff/TiffFrameMetadata.cs). Subsampling remains a codec setting.

Centralize the reusable mapping between named color types and ICC data spaces. That mapping must describe the samples at the ICC boundary. For example:

| Codec representation | Samples passed to ICC |
| --- | --- |
| RGB/BGR storage | RGB after channel unpacking |
| Indexed storage | RGB palette entries |
| JPEG YCbCr | RGB after the existing YCbCr conversion |
| JPEG CMYK | The explicit `Cmyk` reader branch inverts and normalizes its four components before ICC. |
| JPEG YCCK | The explicit `Ycck` reader branch inverts and normalizes YCCK, then performs its own YCCK-to-CMYK conversion before ICC. |
| TIFF JPEG CMYK | The explicit `TiffCmyk` branch normalizes non-inverted components before ICC. |
| TIFF JPEG YCCK | The explicit `TiffYccK` branch normalizes non-inverted YCCK and performs its own YCCK-to-CMYK conversion before ICC. |
| TIFF Lab | Lab after TIFF sample normalization |

These boundaries are visible in [JpegColorConverterBase.Icc](src/ImageSharp/Formats/Jpeg/Components/ColorConverters/JpegColorConverterBase.Icc.cs) and the [TIFF photometric decoders](src/ImageSharp/Formats/Tiff/PhotometricInterpretation). Do not infer profile compatibility solely from component count or equate every encoded color type directly with an ICC header value.

### Indexed decoding

Convert palette colors through the source ICC profile **before index expansion**. Build pixel lookup tables and retained palette metadata from those converted colors. Apply transparency/per-pixel alpha through the existing codec rules.

Use the existing typed span converter for palette colors. Retain the available precision until conversion to the requested pixel type; do not stage a 16-bit TIFF palette through 8-bit entries. Pixels expanded through the converted palette are already sRGB and must not receive another image/frame ICC transform.

`AfterImageApply` correctly invalidates palettes after general image processing. Leave that behavior intact.

### Background colors

Background colors retained on a decoded image must use the same sRGB working space as its pixels and palettes, so subsequent canvas initialization, compositing and disposal use consistent colors. This is a shared metadata contract, carried between formats by [FormatConnectingMetadata.BackgroundColor](src/ImageSharp/Formats/FormatConnectingMetadata.cs).

- On decode, convert an explicitly stored background color through the source profile. For palette-backed backgrounds, obtain the color from the already-converted palette; do not transform it again. Keep retained background metadata consistent with the color used for pixels. Preserve alpha and existing background-ignore and transparent-index behavior.
- Identify retains encoded background values, as it does encoded palette values.
- During encoding, keep background colors used for quantization, frame comparison and disposal in sRGB. When writing an explicit background color, return-transform an output copy using the selected output profile. For an indexed background, select its index against the working palette and write that index with the return-transformed output palette; no separate background transform is needed.
- Resolve an encoder background override before the retained metadata value, using the codec's existing default when neither supplies one. Use that same effective working color for frame processing and output serialization.
- Apply the shared profile-selection rules below, including incompatible-profile omission. Leave source metadata unchanged. Reuse the existing generic color conversion API across codec integration points.

### Encoding and profile selection

Resolve the effective output color type using encoder options and format metadata, then select the compatible original profile. Use the same selection for conversion and emission.

| Case | Behavior |
| --- | --- |
| Compatible profile and supported output layout | Convert sRGB to the profile and embed it. |
| Existing sRGB identity check succeeds | Use the normal sample path and retain the profile. |
| No profile | Use the normal encoder path. |
| Destination or explicit output type cannot carry the original profile | Encode the sRGB working pixels through the selected normal writer and omit the incompatible ICC from output only. |
| `SkipMetadata` | Continue ICC handling; filter other metadata according to existing rules. |
| Selected profile's required transform cannot be performed | Preserve the converter's error behavior. Do not emit unconverted samples labelled with that profile. |

A missing ImageSharp writer for an in-scope source layout is implementation work, not justification for silently changing the color type. Shared profile-selection code must include the reusable color-space mapping; a helper that only wraps header equality adds no value.

PNG/APNG, WebP and GIF emit one image-level ICC profile. TIFF resolves it per IFD. ICO/CUR/ANI preserve resource metadata and delegate ICC work to PNG/BMP. Resolve metadata at those existing ownership boundaries and pass the chosen converter into pixel writers, including temporary animation frames.

When transferring profiles between formats, preserve their source scope. An image-wide profile applies to all source frames; an image-level copy of the first TIFF IFD or container resource's profile does not describe later resources. Use the existing source format and frame metadata to distinguish these cases. A destination with one profile uses the root frame's selected profile for all its output frames, which already contain sRGB working pixels.

## Decoder implementation order

### D1. Enable Preserve through the existing selector

**Source:** [IccProfile.TryGetIccProfileForColorConversion](src/ImageSharp/Metadata/Profiles/ICC/IccProfile.SRGB.cs), [DecoderOptions.CanRemoveIccProfile](src/ImageSharp/Formats/DecoderOptions.cs), [ImageDecoder.HandleIccProfile](src/ImageSharp/Formats/ImageDecoder.cs), [ImageDecoderCore.TryConvertIccProfile](src/ImageSharp/Formats/ImageDecoderCore.cs).

- Remove the `Preserve` early return from the selector. It no longer depends on decoder options, so move it to `IccProfile` as an internal static method and update its callers. Keep its parameters, null handling and existing sRGB identity check; do not suppress the static-member analyzer.
- Keep the existing retention/removal policy. It already retains original image/frame ICC for `Preserve`.
- Update [ColorProfileHandling](src/ImageSharp/Formats/ColorProfileHandling.cs) documentation to describe the observable v5 behavior.
- Do not redesign either `TryConvertIccProfile` overload or add format/color-space exclusions to the selector.
- Update the existing v4 Preserve test expectations in this step. In [PngDecoderCore.Identify](src/ImageSharp/Formats/Png/PngDecoderCore.cs), add the missing `EmbeddedColorProfile` case using the existing reader. Verify it with the existing ICC image fixtures. Keep palette values untransformed during Identify.

**Verification:** Existing valid-profile PNG, JPEG and TIFF fixtures must produce the same pixels under `Preserve` and `Convert`, with original ICC retained only as required by each mode. Use [PngDecoderTests](tests/ImageSharp.Tests/Formats/Png/PngDecoderTests.cs)' existing perceptual, CLUT-only and grayscale fixtures to check conversion against reference output and retained profile bytes against Identify. Remove the synthesized PNG/LUT tests and their builders. Do not synthesize replacement images or claim the valid fixtures cover unsupported channel counts. Keep existing no-profile, sRGB and `Compact` coverage. Do not defer tests invalidated by the selector change to D3.

### D2. Complete named color-type metadata

**Source:** [PixelColorType](src/ImageSharp/PixelFormats/PixelColorType.cs), [TiffFrameMetadata](src/ImageSharp/Formats/Tiff/TiffFrameMetadata.cs), [JpegMetadata](src/ImageSharp/Formats/Jpeg/JpegMetadata.cs), [FormatConnectingMetadata](src/ImageSharp/Formats/FormatConnectingMetadata.cs) and [FormatConnectingFrameMetadata](src/ImageSharp/Formats/FormatConnectingFrameMetadata.cs).

- Add the named types listed above and their reusable ICC data-space mappings.
- Update the affected format mappings to report the original decoded layout. Keep TIFF sample depth, alpha and photometric metadata consistent.
- Update both TIFF image and frame mappings named above. Their reverse mappings must consume the incoming color type and component information: `TiffMetadata.FromFormatConnectingMetadata` currently chooses by total bit depth, while `TiffFrameMetadata.FromFormatConnectingFrameMetadata` copies only dimensions. Neither is sufficient to retain a CMYK or Lab layout from another format.
- Preserve existing format-connecting metadata as the cross-format mechanism.
- Complete background transfer in [GifMetadata.FromFormatConnectingMetadata](src/ImageSharp/Formats/Gif/GifMetadata.cs), which currently omits `BackgroundColor` even though its outgoing mapping includes it. Keep working colors unchanged during metadata transfer.

**Verification:** Extend [PixelColorTypeTests](tests/ImageSharp.Tests/PixelFormats/PixelColorTypeTests.cs) and affected format metadata tests for the added values/mappings and alpha composition. Check image and frame TIFF Lab mappings, incoming TIFF color-type/component mappings, and background transfer in both directions between GIF and WebP. Pixel conversion for the missing TIFF paths is verified in D7.

**Implementation status (2026-09-30):** Implemented and source-reviewed; Release/net10.0 build and tests not run at user direction.

### D3. Keep ICC parsing independent of SkipMetadata

**Verified current gaps and changes:**

| Source | Required change |
| --- | --- |
| [BmpDecoderCore.ReadImageHeaders](src/ImageSharp/Formats/Bmp/BmpDecoderCore.cs) gates V5 ICC reading on `!Options.SkipMetadata`. | Remove that suppression for ICC; retain existing offset restoration and ancillary-error handling. |
| [JpegDecoderCore.ProcessApp2Marker](src/ImageSharp/Formats/Jpeg/JpegDecoderCore.cs) exits on `skipMetadata` before reading ICC. | Continue recognizing and assembling ICC APP2 data when other metadata is skipped. |
| [JpegDecoderCore.SetIccMetadata](src/ImageSharp/Formats/Jpeg/JpegDecoderCore.cs) drops a supplied TIFF ICC profile when `skipMetadata` is set. | Retain valid supplied ICC profiles independently of optional metadata. |
| [WebpChunkParsingUtils.ReadIccProfile](src/ImageSharp/Formats/Webp/WebpChunkParsingUtils.cs) passes `ignoreMetadata` into payload reading. | Keep ICC payload reading active in still/animated Decode and Identify; preserve duplicate-profile and chunk-boundary behavior. |
| [TiffDecoderCore.CreateFrameMetadata](src/ImageSharp/Formats/Tiff/TiffDecoderCore.cs) resolves ICC inside the `!skipMetadata` block. | Resolve ICC outside that block while leaving optional EXIF retention inside it. |

**Verification:** Update the existing codec metadata tests that currently expect ICC suppression. Check valid ICC with `SkipMetadata` both ways, plus existing strict/permissive malformed-profile behavior and chunk framing. Include PNG Decode and the Identify path completed in D1.

**Implementation status (2026-09-30):** Implemented and source-reviewed; Release/net10.0 build and tests not run at user direction.

### D4. Convert PNG, BMP and TIFF palettes before expansion

| Format and source | Required change |
| --- | --- |
| [PngDecoderCore.AssignColorPalette / first IDAT / scanline processing](src/ImageSharp/Formats/Png/PngDecoderCore.cs) | After palette and transparency parsing and before the first indexed scanline, convert the palette once. Normal, Adam7 and APNG lookups use those entries. Retain them in `PngMetadata.ColorTable`; exclude this indexed path from the final image ICC call. Keep direct-color conversion through the existing helper. |
| [BmpDecoderCore.Decode / ReadRgbPalette / ReadRgbPaletteWithAlphaMask / ReadRle](src/ImageSharp/Formats/Bmp/BmpDecoderCore.cs) | Build the converted lookup before indexed/RLE expansion and use it consistently in all palette consumers, including skipped-pixel palette backgrounds. Retain matching entries in `BmpMetadata.ColorTable`. Respect three- versus four-byte source entries. For direct-color BMP, add the missing final `TryConvertIccProfile(image)` call. |
| [PaletteTiffColor](src/ImageSharp/Formats/Tiff/PhotometricInterpretation/PaletteTiffColor{TPixel}.cs) and [TiffDecoderCore](src/ImageSharp/Formats/Tiff/TiffDecoderCore.cs) | Convert the normalized vector palette before generating the pixel palette or expanding indices. Generate `PaletteColors` from the converted entries for the existing strip/tile metadata assignments. Keep per-pixel extra-sample alpha handling. Exclude palette output from the final frame ICC call. |

**Verification:** Existing PNG/BMP/TIFF decoder test files: prove converted palette entries agree with decoded pixels and retained metadata; preserve transparency, bit depth and original ICC. Cover the separate normal/interlaced, RLE/alpha-mask and strip/tile paths with existing fixtures where available. A nonidentity profile must expose a missing or repeated transform. Identify retains encoded palette values.

### D5. Preserve and apply the WebP animation profile

**Source:** [WebpDecoderCore](src/ImageSharp/Formats/Webp/WebpDecoderCore.cs) reads ICCP before `ANIM`, then returns directly from the animation decoder. [WebpAnimationDecoder.Decode / Identify](src/ImageSharp/Formats/Webp/WebpAnimationDecoder.cs) constructs fresh image metadata.

- Pass the already-parsed metadata into both animation entry points and use it for the returned result.
- Apply the existing image ICC helper to the completed decoded animation, as the still path already does.
- Retain the original profile and existing loop/frame metadata. Apply the shared background-color contract to `WebpMetadata.BackgroundColor`: its ANIM value currently supplies both retained metadata and frame initialization/disposal pixels. When the completed animation is converted, convert the retained background too, preserving `BackgroundColorHandling.Ignore`.

**Verification:** [WebpDecoderTests](tests/ImageSharp.Tests/Formats/WebP/WebpDecoderTests.cs) and [WebpMetaDataTests](tests/ImageSharp.Tests/Formats/WebP/WebpMetaDataTests.cs): ICCP before ANIM, multiple returned frames, background/disposal pixels and retained background color, Identify, `MaxFrames`, and `SkipMetadata`. Compare color-managed frame pixels, not only profile presence.

### D6. Add GIF ICC parsing and palette conversion

**Format support:** [ICC.1 Annex B.5](https://www.color.org/specification/ICC1v43_2010-12.pdf) defines application identifier `ICCRGBG1`, authentication code `012`, and ordinary data subblocks of at most 255 bytes. GIF supports ICC despite the current codec lacking that path.

**Source:** [GifDecoderCore.ReadApplicationExtension / ReadFrame / SetFrameMetadata / ReadLogicalScreenDescriptorAndGlobalColorTable](src/ImageSharp/Formats/Gif/GifDecoderCore.cs).

- Recognize the ICC application identifier before the generic/Netscape subblock handling; assemble the payload and retain it independently of `SkipMetadata`.
- Prepare the global palette for decoding after profile parsing and before its first use. Convert each local palette before expanding its indices.
- Use the converted entries for both pixel lookup and retained global/local palette metadata. Apply the shared background-color contract by deriving retained background and disposal colors from the converted global table.
- In `ReadFrame`, background lookup currently uses the selected frame table, which can be local. Resolve the logical-screen background against the global table, independently of the local table used for that frame's pixels. Retain the existing transparent-background policy when applying disposal.
- Keep transparent-index handling and animation disposal behavior. Do not run a second image ICC conversion.
- Identify parses ICC and encoded tables without color conversion.

**Verification:** [GifDecoderTests](tests/ImageSharp.Tests/Formats/Gif/GifDecoderTests.cs): split ICC subblocks, global/local table use, transparent entries, background disposal and Identify. Preserve existing extension-integrity rules. Decoder verification must not depend on the new GIF encoder.

### D7. Complete TIFF direct and nested conversion boundaries

**Source:** [TiffDecoderCore.DecodeFrame](src/ImageSharp/Formats/Tiff/TiffDecoderCore.cs) currently limits final conversion to its palette-through-RGBA color-type range. The grayscale values precede that range in [TiffColorType](src/ImageSharp/Formats/Tiff/PhotometricInterpretation/TiffColorType.cs), and their photometric decoders do not apply ICC. CMYK and 16-bit Lab already use typed ICC conversion.

1. **Grayscale:** Apply the existing frame ICC helper after BlackIsZero/WhiteIsZero sample decoding when ICC was not applied inside a nested decoder. Preserve the existing photometric normalization, alpha and sample-depth handling.
2. **8-bit Lab:** Bring interleaved/planar [CieLab8TiffColor](src/ImageSharp/Formats/Tiff/PhotometricInterpretation/CieLab8TiffColor{TPixel}.cs) and [CieLab8PlanarTiffColor](src/ImageSharp/Formats/Tiff/PhotometricInterpretation/CieLab8PlanarTiffColor{TPixel}.cs) onto the existing profile-aware typed conversion pattern used by their 16-bit counterparts. Preserve TIFF's L/a/b normalization.
3. **YCbCr:** In the [YCbCr photometric paths](src/ImageSharp/Formats/Tiff/PhotometricInterpretation/YCbCrConverter.cs), retain the TIFF reference-black/white and coefficient conversion. Apply an RGB profile to the resulting RGB values. If a supported profile describes YCbCr itself, apply it at the normalized YCbCr boundary. Do not apply a YCbCr profile to already converted RGB.
4. **JPEG-compressed TIFF:** Preserve the actual sample contract of each path in [JpegTiffCompression](src/ImageSharp/Formats/Tiff/Compression/Decompressors/JpegTiffCompression.cs), [OldJpegTiffCompression](src/ImageSharp/Formats/Tiff/Compression/Decompressors/OldJpegTiffCompression.cs), their spectral converters and [TiffDecoderOptionsParser](src/ImageSharp/Formats/Tiff/TiffDecoderOptionsParser.cs):
   - Table-based YCbCr intentionally defers its photometric conversion to TIFF. Keep that boundary: do not apply an RGB ICC transform to the still-YCbCr components in the nested spectral converter. Apply ICC at the appropriate TIFF photometric boundary in item 3.
   - Where nested JPEG already produces profile-converted RGB, consume it as RGB and omit a second outer ICC transform. Complete-JPEG decoding currently uses `Image.Load<Rgb24>` without passing the IFD profile; supply the original IFD profile through the existing JPEG decoder so this path uses the same intended profile.
   - For nested grayscale, keep the profile transform ordered after the applicable BlackIsZero/WhiteIsZero sample interpretation and apply it once. Do not select the ICC location from compression type alone.
   - Cover both modern and old JPEG CMYK handling. [CmykTiffColor](src/ImageSharp/Formats/Tiff/PhotometricInterpretation/CmykTiffColor{TPixel}.cs) currently treats modern JPEG output as RGB but does not include OldJPEG in that branch.
   - Retain original IFD photometric/profile metadata even when the internal unpacking layout is RGB.
5. **WebP-compressed TIFF:** [WebpTiffCompression](src/ImageSharp/Formats/Tiff/Compression/Decompressors/WebpTiffCompression.cs) calls the full WebP decoder and copies its RGB pixels into the TIFF strip buffer. When the IFD supplies an ICC profile, let TIFF own its conversion at the RGB boundary; nested decompression must supply the RGB samples before ICC conversion, so an embedded WebP profile does not cause an additional transform. Without an IFD profile, retain the nested decoder's existing profile handling. Preserve standalone WebP conversion and the existing `Preserve` selector contract.

**Verification:** [TiffDecoderTests](tests/ImageSharp.Tests/Formats/Tiff/TiffDecoderTests.cs): existing CMYK issue #3198 and Lab16 baselines; BlackIsZero/WhiteIsZero, Lab8 interleaved/planar, YCbCr, JPEG with/without tables, OldJPEG and WebP-compressed cases; distinct per-IFD ICC. Verify pixel values and original metadata, including nested-profile cases that fail on double conversion.

### D8. Preserve ICC through ICO, CUR and ANI resource assembly

**Source:** [IconDecoderCore.Decode / Identify](src/ImageSharp/Formats/Icon/IconDecoderCore.cs) clones child frame metadata while PNG/BMP ICC belongs to child image metadata. [AniDecoderCore.Decode / Identify](src/ImageSharp/Formats/Ani/AniDecoderCore.cs) constructs fresh output frame metadata and adds ANI metadata.

- Copy each child resource's ICC into that output frame's existing `ImageFrameMetadata.IccProfile`, with its relevant format metadata.
- Preserve those frame profiles when ANI flattens or repeats resources. Handle BMP resources that expose the profile only at image level.
- Apply the same metadata transfer during Identify.
- Leave pixel conversion with the PNG/BMP child decoder.

**Verification:** Existing [ICO](tests/ImageSharp.Tests/Formats/Icon/Ico), [CUR](tests/ImageSharp.Tests/Formats/Icon/Cur) and [ANI](tests/ImageSharp.Tests/Formats/Ani) decoder tests: distinct resource profiles, PNG/BMP children, differently sized entries and repeated ANI frames. Assert each frame's pixels and profile together.

## Decoder verification gate

Before E1:

- D1–D8 are implemented and their affected tests pass on the current tree.
- Preserve/default pixels match the established conversion results; original image/frame ICC and format color types remain available.
- PNG/BMP/TIFF/GIF indexed decoding uses converted tables before expansion; no second transform occurs.
- Explicit and palette-backed background colors agree with sRGB working pixels and survive format-connecting metadata transfer in that space; alpha, ignore and disposal behavior remains intact.
- Still/animated and nested paths retain the correct profile and apply it once.
- SkipMetadata leaves ICC behavior active. Identify retains encoded metadata without conversion.
- Existing Convert/Compact, alpha, precision and processing-invalidation behavior remains covered.
- Profile parsing and unsupported conversion remain distinct; the four-channel limit is unchanged.

Record concrete files changed, test commands and results. A passing PNG subset does not satisfy this gate.

## Encoder implementation order

### E1. Complete shared conversion and output selection before codec integration

**Source:** [ColorProfileConverterExtensionsPixelCompatible](src/ImageSharp/ColorProfiles/ColorProfileConverterExtensionsPixelCompatible.cs), [ColorProfileConverterExtensionsIcc](src/ImageSharp/ColorProfiles/ColorProfileConverterExtensionsIcc.cs), [GrayTrcCalculator](src/ImageSharp/ColorProfiles/Icc/Calculators/GrayTrcCalculator.cs).

- Review the unfinished extraction against the existing image overload. Share its RGB transform and alpha merge with a non-mutating pixel-span overload; keep caller-owned destination/scratch reusable across rows.
- Use PixelOperations' existing extraction modifiers for the required straight samples. Do not change Scale, inspect alpha types manually in codecs, or add codec-local unpremultiplication.
- Preserve floating-point transformed samples until the output packer applies its precision.
- Use the same RGB result for gray packing: the existing gray target calculator already returns the gray value in equal RGB lanes.
- Complete the shared named-color-space mapping and profile compatibility selection described above. Resolve effective codec options before calling it; keep row transforms and emitted profiles tied to the same selection.
- Reuse the existing typed span conversion for CMYK/Lab and palette data. Add no color-type-specific converter API.

**Verification:** Existing ICC regression tests for the shared extraction, then codec tests for the span consumer. No new standalone converter test suite. No per-row converter construction or scratch allocation.

### E2. Complete PNG/APNG output in three reviewed parts

**Source:** [PngEncoderCore](src/ImageSharp/Formats/Png/PngEncoderCore.cs).

1. **Direct samples:** Resolve the output profile in Encode and pass its converter into normal and Adam7 row packing. Keep existing packers/filtering. Use separate loops or a branch around conversion where that makes the data flow clearer; do not disguise always-null arguments as distinct quantization behavior.
2. **Indexed samples:** Quantize sRGB pixels against sRGB palettes. Convert an encoder-owned copy of the final palette in `WritePaletteChunk`, immediately before writing PLTE. Keep indices, transparency and in-memory tables unchanged.
3. **APNG precision:** Fix the demonstrated loss in [AnimationUtilities.DeDuplicatePixels](src/ImageSharp/Formats/AnimationUtilities.cs), which stages and writes rows through Rgba32. Preserve higher-precision input when computing differences and writing temporary frames; retain the existing efficient 8-bit path. Keep the shared algorithm rather than adding a duplicate codec-specific deduplication method.

Use the one emitted image profile for all APNG frame writers. Keep ICC emission independent of SkipMetadata. Ensure cICP/gAMA and ICC describe the selected output; [PNG color-chunk precedence](https://www.w3.org/TR/png-3/) gives cICP priority.

**Verification:** Existing PNG encoder/chunk tests: direct RGB/gray at supported depths, Adam7, retained/new palettes and transparency, APNG with a sub-8-bit difference that the old deduplication loses, profile compatibility/overrides and SkipMetadata. Confirm repeated saves leave input pixels and metadata unchanged.

### E3. Complete BMP row and palette output

**Source:** [BmpEncoderCore.Encode](src/ImageSharp/Formats/Bmp/BmpEncoderCore.cs) receives frame and image metadata; currently ICC emission is gated by `!skipMetadata`.

- Resolve the resource's profile and effective BMP layout at that boundary. Remove ICC suppression under SkipMetadata.
- Convert direct-color rows before existing 16/24/32-bit packing.
- Convert final palette copies before writing indexed tables.
- Use the selected profile for V5 header/profile data and correct offsets, including delegated DIB output.

**Verification:** Existing BMP tests for direct/indexed samples, transparency, V5 profile payload/offsets, SkipMetadata and repeated-save source preservation.

### E4. Integrate WebP lossless and lossy sample collection

**Source:** [Vp8LEncoder.ConvertPixelsToBgra](src/ImageSharp/Formats/Webp/Lossless/Vp8LEncoder.cs), [YuvConversion.ConvertRgbToYuv](src/ImageSharp/Formats/Webp/Lossy/YuvConversion.cs), [WebpEncoderCore](src/ImageSharp/Formats/Webp/WebpEncoderCore.cs).

- Pass the selected image-profile converter into VP8L BGRA collection and VP8 row-pair BGRA collection before YUV conversion.
- Reuse row scratch and include the unpaired final VP8 row.
- Carry the same conversion into animation difference-frame writers and keep ICCP emission matched to it.
- Apply the shared explicit-background output rule at `BitWriterBase.WriteAnimationParameter`, which writes ANIM.
- Resolve the effective background once in `WebpEncoderCore` and pass it to both frame differencing and header writing. Currently the lossless/lossy headers read `WebpMetadata.BackgroundColor`, while both differencing loops use the encoder override or transparent; those paths must agree before the output copy is transformed.

**Verification:** Existing WebP encoder/metadata tests for lossless/lossy, still/animated, odd height, alpha and SkipMetadata. Include retained and explicitly overridden backgrounds with restore-to-background disposal. Compare appearance with loss-appropriate measured tolerance.

### E5. Add GIF ICC and final-palette output

**Source:** [GifEncoderCore](src/ImageSharp/Formats/Gif/GifEncoderCore.cs), ICC Annex B.5 referenced in D6.

- Emit the selected original ICC application extension before image data.
- Quantize in sRGB. Return-transform copies of final global/local palettes when writing them.
- Apply the shared indexed-background output rule when selecting the background index. Keep transparent indices, palette reuse and disposal consistent with the written tables.

**Verification:** Existing GIF encoder tests for extension subblocks, global/local palette output, transparency and animated disposal. Use the completed decoder plus an independent color-managed reference where supported.

### E6. Preserve resource profiles during ICO/CUR/ANI encoding

**Source:** [IconEncoderCore](src/ImageSharp/Formats/Icon/IconEncoderCore.cs) conditionally clones image metadata and copies only PNG frame-format metadata into temporary images. [AniEncoderCore](src/ImageSharp/Formats/Ani/AniEncoderCore.cs) delegates icon/resource output.

- Transfer each resource frame's ICC into the metadata supplied to the delegated codec, including cropped/isolated temporary images and SkipMetadata.
- Ensure a later resource does not inherit the first resource's profile.
- Let PNG/BMP perform profile selection, conversion and emission. Add no separate container ICC transform.

**Verification:** Existing ICO/CUR/ANI encoder tests with distinct profiles, PNG/BMP resources, cropped entries and repeated ANI frames.

### E7. Mirror JPEG's ICC reader in the writing path

**Source:** [JpegColorConverterBase.Icc](src/ImageSharp/Formats/Jpeg/Components/ColorConverters/JpegColorConverterBase.Icc.cs) owns `ConvertToRgbInPlaceWithIcc`. The [decoder spectral converter](src/ImageSharp/Formats/Jpeg/Components/Decoder/SpectralConverter{TPixel}.cs) selects that operation at the color-conversion boundary. The [encoder spectral converter](src/ImageSharp/Formats/Jpeg/Components/Encoder/SpectralConverter{TPixel}.cs) already has the corresponding `ConvertFromRgb` call.

- Add the ICC-aware writing counterpart alongside the reader in `JpegColorConverterBase.Icc`. It converts sRGB input to the selected original profile and then produces the selected JPEG component values. Keep JPEG color-model handling in this color converter.
- Have the encoder spectral converter select the ICC-aware counterpart at its existing `ConvertFromRgb` call. Preserve its row padding, component buffers, subsampling and subsequent spectral processing.
- Make the numeric boundary explicit: ICC works on normalized samples, while the current JPEG encoder's component buffers use the 0–255 sample domain. Prepare ICC input from scaled source pixels and perform the required normalization/denormalization in the ICC-aware path. Do not change the ordinary `UnpackIntoRgbPlanes` contract or quantize the ICC input through its generic Rgba32 extraction first.
- Mirror each explicit reader branch in the writing counterpart: `Grayscale`, `RGB`, `YCbCr`, `Cmyk`, `Ycck`, `TiffCmyk` and `TiffYccK`. Reverse that branch's sample normalization, channel order and encoding operations. YCCK has its own explicit conversion path; implement its inverse explicitly, including K and inversion handling. Keep the distinct TIFF conventions in their corresponding branches.
- Use the existing generic `ColorProfileConverter` API for the profile transform. Reuse conversion state and scratch over rows; keep source image pixels unchanged.
- Resolve the compatible output profile in `JpegEncoderCore` before writing profiles and pass that same selection into spectral conversion. Existing ICC emission already occurs in `WriteProfiles`; keep it independent of SkipMetadata and make it match the selected transform.

**Verification:** Existing JPEG encoder/metadata tests across RGB, YCbCr, luminance, CMYK and YCCK with compatible profiles and explicit overrides. Check component samples/conventions as well as appearance so a matching reader/writer error cannot pass on round-trip agreement alone. Cover baseline and full-image spectral conversion, edge padding, original ICC emission and source preservation.

### E8. Resolve TIFF settings and existing writers per IFD

**Source:** [TiffEncoderCore](src/ImageSharp/Formats/Tiff/TiffEncoderCore.cs) resolves root-frame options before looping frames; [TiffEncoderEntriesCollector](src/ImageSharp/Formats/Tiff/TiffEncoderEntriesCollector.cs) emits image/frame profiles.

- Resolve each frame's photometric type, sample depth/format, compression and selected ICC inside its write scope, with explicit encoder options taking precedence.
- Keep the original encoder options separate from each frame's resolved settings. `SanitizeAndSetEncoderOptions` currently writes resolved values back to the core; those values must not become overrides for the next frame.
- Use the same selected profile for that IFD's conversion and emitted tag. Preserve each source frame/resource profile, including an unprofiled IFD beside a profiled one; do not fall back to the image-level copy of the first resource's profile. Conversely, when the source profile is image-wide, such as APNG, apply it to every output IFD. Follow the shared source-scope rule above.
- Emit or omit ICC directly in the output entry collector. `ProcessIccProfile` currently removes `ExifTag.IccProfile` from the supplied source EXIF profile when its ICC argument is null; output-profile omission must not take that mutation path or allow the original raw EXIF ICC tag to be emitted instead.
- For uncompressed and byte-compressed output, integrate row conversion into existing RGB, grayscale and float writers before final packing. Write converted samples into strip storage without routing through a lower-precision TPixel buffer.
- Convert the final palette copy in [TiffPaletteWriter](src/ImageSharp/Formats/Tiff/Writers/TiffPaletteWriter{TPixel}.cs), preserving ColorMap precision.

**Verification:** Existing TIFF encoder tests for supported integer/float depths, grayscale, RGB, palettes, alpha and distinct per-IFD settings/profiles.

### E9. Add TIFF CMYK output

**Source:** [TiffColorWriterFactory](src/ImageSharp/Formats/Tiff/Writers/TiffColorWriterFactory.cs) currently handles palette/gray and otherwise chooses RGB writers.

- Add a separated CMYK writer using the existing typed conversion.
- Update `SanitizeAndSetEncoderOptions` to retain the supported separated layout and component depth instead of forcing its 32/64-bit cases to RGB. Resolve profile compatibility after the effective layout is established.
- Emit matching photometric, four-component sample-depth and ink tags through the existing collector.
- Keep the fourth CMYK component separate from alpha semantics.

**Verification:** The existing issue #3198 image and valid CMYK fixtures must save as CMYK with the original ICC and preserve appearance. Verify non-JPEG compression first.

### E10. Add TIFF Lab and YCbCr output

- Add Lab writers that invert TIFF's decoded sample normalization and emit matching photometric/sample tags.
- Add YCbCr output using the selected profile boundary and TIFF coefficients/reference ranges; perform chroma packing after color conversion.
- Update `SanitizeAndSetEncoderOptions` for each added layout before integrating its writer; its current integer-depth switch forces these color interpretations to RGB. Keep sample count, component depth, photometric tags and writer selection in agreement.
- Integrate both with per-IFD settings and the existing compression/predictor compatibility rules.

**Verification:** Existing TIFF test conventions for source color-type retention and independently decoded appearance. Complete Lab before YCbCr, and each non-JPEG path before E11.

### E11. Delegate TIFF JPEG output through the typed image path

**Source:** [TiffJpegCompressor.CompressStrip](src/ImageSharp/Formats/Tiff/Compression/Compressors/TiffJpegCompressor.cs) creates an `Image<Rgb24>` with `Image.LoadPixelData<Rgb24>` and passes it to `JpegEncoder`, currently selecting `JpegColorType.Rgb`. The pixel format describes the input image; `JpegColorType` selects the JPEG output representation.

- Supply sRGB pixels and the selected IFD profile to the typed image passed to JPEG encoding. Let the completed JPEG encoder perform the return transform; the TIFF writer must not preconvert those same pixels.
- Pass the supported JPEG color selection corresponding to the requested TIFF output, rather than always selecting RGB.
- Wire the TIFF-specific writing branches from E7 into the nested encoder's frame/color-converter selection. [JpegEncoderCore.FrameConfig](src/ImageSharp/Formats/Jpeg/JpegEncoderCore.FrameConfig.cs) maps public `JpegColorType.Cmyk` and `Ycck` to the ordinary JPEG conventions; selecting those options alone does not select `TiffCmyk` or `TiffYccK`. Keep IFD tags, component encoding and the selected TIFF branch consistent.
- Retain the existing compression/layout compatibility rules. Adding TIFF Lab output does not itself add a Lab mode to `JpegEncoder`.

**Verification:** Existing RGB JPEG-compressed TIFF plus supported gray, YCbCr and CMYK output with ICC. Verify the typed input, selected JPEG representation and IFD profile produce one return transform and matching decoded appearance.

### E12. Verify cross-format output and the complete change

- Use completed named-type mappings and existing format-connecting metadata to select compatible destination layouts, honoring explicit overrides.
- Verify at least RGB-profile PNG → BMP, CMYK TIFF → JPEG/TIFF, and CMYK TIFF → RGB PNG. The last case uses sRGB output and omits the incompatible CMYK profile.
- Verify image-level and per-frame profile selection, repeated saves, source immutability, SkipMetadata, absent/sRGB profiles and converter failures.
- Include an image-wide animation profile saved to multiple TIFF IFDs, and a TIFF/container with a profiled resource followed by an unprofiled resource. Check both profile retention and absence; the first resource's profile must not leak into the second.
- Verify background-color transfer through format-connecting metadata between explicit-color and palette-backed destinations: working colors remain sRGB, emitted colors match the selected output profile, and source metadata remains unchanged.
- Run the affected converter, metadata and codec regressions together on the final reviewed tree in Release/net10.0.

## Formats without a standard ICC embedding path

| Format | Format evidence and behavior |
| --- | --- |
| PBM/PGM/PPM/PAM | The [Netpbm format documentation](https://netpbm.sourceforge.net/doc/pam.html) defines no standard ICC field. Use normal output from sRGB working pixels. |
| TGA | The [TGA format description](https://www.loc.gov/preservation/digital/formats/fdd/fdd000180.shtml) explicitly records no ICC support. |
| QOI | The [QOI specification](https://qoiformat.org/qoi-specification.pdf) defines a fixed header, pixel chunks and end marker without ICC storage. |
| EXR | [OpenEXR standard attributes](https://openexr.com/en/latest/StandardAttributes.html) describe its color metadata without a standard ICC binding. Do not introduce a private ICC convention. |

AVIF and JPEG XL are future consumers of the shared conversion/mapping work, not new codec implementations in this plan.

## Verification quality and completion record

Use existing fixtures, providers and reference conventions. Test both emitted samples and metadata: retaining ICC bytes alone does not prove a transform. For new forward paths, use existing color-managed reference data; for inverse paths, check encoded samples and independently color-managed appearance. Do not treat a reference decoder's raw pixel extraction as proof that it applied ICC.

Use existing image fixtures for decoder coverage. Do not synthesize images, fabricate profiles, add broad converter/alpha test suites, change reference images to conceal failures, or invent tolerances. Measure any required tolerance against the actual codec/reference path.

Implementation is complete only when each decoder and encoder step has a reviewed change and recorded passing verification. Plan review does not establish implementation correctness; existing implementation edits have not been built or tested as part of this review.
