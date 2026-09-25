# ImageSharp v5 HDR and pixel-format conversion plan

This plan replaces the earlier native-versus-scaled codec design. The rules below
apply to all pixel formats and all codecs. This document records the design. It
does not rename the current APIs.

## Current status

- The shared pixel conversion rules, associated floating-point pixel types,
  float-plane SIMD operations, EXR floating-point paths, float TIFF writer,
  default floating-file loads, bounded processor indices, and percentage
  thresholding are implemented on this branch. Unsigned 16-bit and 32-bit TIFF
  color output also preserves its stored sample precision. Float TIFF decoding
  and writing now use bulk pixel operations and TIFF-local SIMD row kernels for
  grayscale, three-component, and four-component samples, including opposite
  byte order and the declared alpha representation.
- Native interoperability checks passed. libtiff decoded 31 ImageSharp-written
  floating-point and unsigned 32-bit TIFF files across the supported compression
  modes. It read their sample-format, component-width, alpha, and compression
  tags; all 9,750 component samples matched after libtiff decompression.
  OpenEXR validated five ImageSharp-written HDR EXR files and decoded them into
  uncompressed copies; all 1,300 channel samples matched, including nonfinite
  values and stored color at zero alpha. Its reader also accepted ImageSharp's
  ZIPS output. The conversion, SIMD, codec, and processor acceptance tests pass
  in Release on both net10.0 and net11.0.
- Rename the scaled APIs only as a separate v5 change after numeric behavior
  and codec interoperability are verified.

## Conversion rules

The generic pixel converter always passes `PixelConversionModifiers.Scale` to
both pixel operations. It does not classify pixel formats or skip the modifier.
Each pixel implementation is responsible for what `Scale` does to its values.
Keep the current API names until the separate v5 API rename. A pixel format's
native vector keeps that format's documented numeric meaning.

| Group | Pixel formats | `Scale` behavior |
| --- | --- | --- |
| Bounded color | A8, Abgr32, Abgr32P, Argb32, Argb32P, Bgr24, Bgr565, Bgra32, Bgra32P, Bgra4444, Bgra5551, L8, L16, La16, La32, Rg32, Rgb24, Rgb48, Rgb96, Rgba1010102, Rgba32, Rgba32P, Rgba64, Rgba128 | Keep each existing mapping to [0, 1]. |
| Unsigned integer vector | Byte4 | Map 0..255 to [0, 1]. |
| Signed integer vector | Short2, Short4 | Map -32768..32767 to [0, 1]. |
| Signed normalized vector | NormalizedByte2, NormalizedByte4, NormalizedByte4P, NormalizedShort2, NormalizedShort4 | Map logical [-1, 1] to [0, 1]. Keep the DirectX endpoint rules. |
| IEEE floating point | HalfSingle, HalfVector2, HalfVector4, HalfVector4P, RgbaHalf, RgbaHalfP, RgbaVector, RgbaVectorP | `Scale` does not change the numeric value. Both native and scaled vectors preserve floating-point samples, including values outside [0, 1]. |

The P suffix means associated alpha. It does not select a different numeric
range. Alpha association remains an explicit conversion modifier.

Alpha conversion depends only on the source and destination representations.
Convert associated to unassociated by dividing color by alpha. Convert
unassociated to associated by multiplying color by alpha, including when alpha
is zero. When both representations match, do not change the alpha association.
Storage rounding is a separate step.

PixelOperations<TPixel>.From<TSourcePixel> stays the one bulk entry point for
conversion between pixel formats:

The base PixelOperations<TPixel>.From method selects the alpha modifiers for
both sides of its vector conversion. Associated destinations provide storage
hooks; they do not run a separate conversion loop. The generated bounded
destination overrides keep their existing ToX dispatch through the source
pixel operations, which supply unassociated scaled values.

| Source | Destination | Rule |
| --- | --- | --- |
| Bounded | Bounded | Keep the existing normalized mapping for both formats. |
| Bounded | Floating point | Normalize the source, then store that number. |
| Floating point | Bounded | Pass the original floating-point values through the shared converter. The bounded destination saturates them when it packs its samples. |
| Floating point | Floating point | Keep the floating-point values. Apply only the requested alpha conversion and destination precision. |

"Bounded" includes the four non-floating groups in the first table. Keep
conversion behavior in the existing pixel APIs. Do not add floating-point
flags, type lists, or nominal-range properties to pixel metadata or pixel
operations. Do not add a separate conversion pipeline.

Examples define the result:

- Byte4 component 128 becomes about 0.502 in RgbaVector.
- RgbaVector component 0.4 becomes byte 102 in Rgba32.
- RgbaVector component 2.5 becomes byte 255 in Rgba32.
- RgbaVector component 2.5 stays 2.5 in RgbaHalf.
- HalfVector4 component 0 becomes 0 in RgbaVector, not 0.5.

The current HalfSingle and HalfVector scaled methods map the full finite
half-precision range to [0, 1]. Replace that rule. It destroys the meaning of zero
and near-zero HDR values. Do not change their DirectX-compatible storage layout.

## Values at the boundaries

`Scale` leaves NaN, infinities, signed zero, and finite HDR values unchanged
in floating-point pixels. A bounded destination applies its existing
saturation and nonfinite rules at its storage boundary. Any change needed to
make those bounded rules consistent belongs in the bounded pixel implementation.

Float-to-float conversion keeps NaN and infinities where the destination can
store them. NaN payload and sign are not guaranteed. A half-precision destination
uses the same conversion as System.Half: nearest value, ties to even, signed
zero, and signed infinity for finite overflow. SIMD and scalar packers must
give the same bits for finite values, infinities, and signed zero.

For associated float pixels, a direct associated conversion must retain
stored color when alpha is zero. This preserves additive EXR pixels.
Unassociation at zero alpha follows the existing Numerics.UnPremultiply rule.
Converting unassociated color into associated storage multiplies color by
alpha, including when alpha is zero. Do not add a zero-alpha exception.
`Scale` must not clamp floating-point color before or after alpha association.

## Image loading and processing

Image.Load without a pixel type must keep EXR samples in a floating-point
pixel image. It must do the same for float TIFF. Image.Load<TPixel> stores
directly in the requested pixel type. A bounded TPixel therefore loses HDR
values at its storage boundary. Neither overload requires a range option.
Choose RgbaHalfP for associated EXR HALF, RgbaVectorP for associated EXR FLOAT
or float TIFF, and the corresponding straight floating-point type when the
file has no associated alpha. Follow the existing PNG decoder pattern: identify
the sample layout, rewind, then dispatch to the generic decoder. This parses
the header or root directory twice but decodes pixel data only once. Do not
decode into a temporary image.

Decode each image once into its final pixel buffer. Do not keep a second
full-image copy of the source samples. EXR has no standard sample minimum or
maximum. TIFF SMinSampleValue and SMaxSampleValue are optional metadata. Do
not use either format's metadata to define pixel-format conversion.

Processors must not assume that `Scale` bounds floating-point values. Keep
the source samples unchanged when deriving a bounded table or histogram index.
Clamp the derived metric to the index domain at the point where that index is
formed, using the existing `Numerics.Clamp` rule: NaN and negative infinity
map to the first entry, positive infinity maps to the last entry. Finite HDR
values outside the index domain use the nearest endpoint. Apply this rule to
all processor-owned value-derived indices, including luminance histograms,
AutoLevel component lookups, and OilPainting intensity bins. Continuous
operations may use finite HDR values without changing their range.

A percentage threshold owns its range. First, scan the selected image region
for finite values of the selected threshold metric. Then set the threshold to
minimum + percentage * (maximum - minimum). The threshold pass then changes
the selected pixels. This work belongs to the processor, not the decoder or
the pixel-format converter. Ignore nonfinite values while finding the range.
If the region has no finite value, write the lower threshold color. If
minimum equals maximum, values equal to it use the upper color.

Use the existing luminance calculation for luminance mode. Do not replace its
weights with new constants. Apply the same range rule to the selected metric
in the other threshold modes. Alpha has zero weight in luminance and is not a
component of the other metrics. IEEE arithmetic still makes luminance NaN
when alpha is NaN; treat that as a nonfinite metric.

## EXR and float TIFF

EXR FLOAT and HALF rows feed the shared bulk float-plane operation. The
generic pixel converter applies the source and destination rules above. The
encoder reads actual float pixel values for float output. It must not pass
them through bounded pixel storage. EXR UINT keeps its existing integer rule
until its own numeric contract is changed.

Float TIFF decode and encode use the same converter. Add a float writer for
grayscale, three-component color, and four-component color. The writer emits
IEEE binary32 samples, SampleFormat Float, matching BitsPerSample entries,
SamplesPerPixel, and ExtraSamples when alpha exists. Keep the selected TIFF
byte order, photometric interpretation, and supported compression rules.
A loaded float TIFF saved with default encoder options must remain float.
A new integer image keeps the existing integer TIFF default.

DirectX compatibility means that RgbaVector and RgbaVectorP keep four float32
components in order. RgbaHalf, RgbaHalfP, HalfVector4, and HalfVector4P keep
four half-precision components in order. The P types change alpha interpretation,
not component layout. No native float conversion inserts an sRGB transfer
function.

## SIMD-first implementation order

1. Define the shared bulk conversion contract and float-plane APIs. Use the
   existing PixelOperations and SimdUtils patterns. Match their guards and
   ownership rules. Compare the JPEG plane methods before writing comments.
2. Implement SIMD kernels and scalar tails against the rules above. Handle
   full vectors and short rows. Explain component layout, shuffles, masks,
   rounding, and tail behavior next to each non-obvious SIMD operation.
3. Make scalar pixel methods and bulk pixel operations agree. Keep existing
   bounded-to-bounded conversion results. Fix every IEEE float type, including
   HalfVector4, before changing codec call sites.
4. Connect EXR and float TIFF to the shared row operations. Add the TIFF float
   writer. Change default floating-file loads to floating pixel storage.
5. Change percentage thresholding to use the selected image region's range.
   Keep this processor work separate from pixel-format conversion.
6. Revisit the proposed scaled API name after the numeric behavior is correct.
   Do not rename the API while changing conversion behavior.

## Acceptance

Check every source/destination group pair. Include zero, 0.4, one, 2.5,
negative finite values, NaN, both infinities, and signed zero. Check each
DirectX layout and the half-precision overflow boundary. Check SIMD widths and
scalar tails against the same expected values. Check float EXR and TIFF
load-save-load values. Check Image.Load without options and Image.Load<TPixel>.
Check the exact TIFF tags and all supported float compression modes. Check
percentage thresholds on images with different observed ranges.

The Release solution builds pass on net10.0 and net11.0. On each framework,
39,805 pixel-format, TIFF, EXR, and processor tests pass with one test skipped;
another 437 SIMD and OilPaint tests pass. The EXR HDR round-trip tests cover
None, ZIP, and ZIPS compression. The native interoperability checks described
above passed against the files produced by the focused codec tests.
