// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using System.Numerics.Tensors;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using SixLabors.ImageSharp.Formats.Jxl.Components.Entropy;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.PixelFormats;
using Blending = SixLabors.ImageSharp.Formats.Jxl.Components.Decoder.JxlBlending;
using BlendMode = SixLabors.ImageSharp.Formats.Jxl.Components.Decoder.JxlBlending.BlendMode;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Owns a decoded patch dictionary in its encoded application order.
/// </summary>
/// <remarks>
/// Patch syntax follows ISO/IEC 18181-1, Section C.4.5, and libjxl's dec_patch_dictionary.cc.
/// References supply native samples saved before the color transform. This owner retains reference indices, not reference-plane ownership.
/// </remarks>
internal sealed class JxlPatches : IDisposable
{
    /// <summary>
    /// The ten patch contexts in libjxl's patch_dictionary_internal.h, Listing C.2.
    /// </summary>
    private const int ContextCount = 10;

    /// <summary>
    /// The number of distinct reference rectangles.
    /// </summary>
    private const int ReferenceCountContext = 0;

    /// <summary>
    /// The ordinary reference-frame slot for a source rectangle.
    /// </summary>
    private const int ReferenceFrameContext = 1;

    /// <summary>
    /// The source width or height minus one.
    /// </summary>
    private const int PatchSizeContext = 2;

    /// <summary>
    /// The source rectangle's horizontal or vertical origin.
    /// </summary>
    private const int SourcePositionContext = 3;

    /// <summary>
    /// The first destination position for a source rectangle.
    /// </summary>
    private const int DestinationPositionContext = 4;

    /// <summary>
    /// The blend equation for color or one extra channel.
    /// </summary>
    private const int BlendModeContext = 5;

    /// <summary>
    /// A signed destination offset after the first placement.
    /// </summary>
    private const int PositionOffsetContext = 6;

    /// <summary>
    /// The number of placements for a source rectangle minus one.
    /// </summary>
    private const int RepetitionCountContext = 7;

    /// <summary>
    /// The extra-channel index supplying a blend weight.
    /// </summary>
    private const int WeightChannelContext = 8;

    /// <summary>
    /// The optional clamp flag for a weighted or multiplicative blend.
    /// </summary>
    private const int ClampContext = 9;

    private readonly IMemoryOwner<ReferencePatch> referencePatches;
    private readonly IMemoryOwner<Placement> placements;
    private readonly IMemoryOwner<Blending> blendings;
    private readonly int referencePatchCount;
    private readonly int placementCount;
    private readonly int blendingCount;
    private readonly int blendingStride;

    /// <summary>
    /// Indicates whether the validated color records contain source-over or destination-over composition.
    /// </summary>
    private readonly bool usesColorAlphaBlend;

    private InlineArray4<JxlReferenceFrame> references;
    private bool isDisposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlPatches"/> class with exact retained record counts.
    /// </summary>
    /// <param name="allocator">The tracked allocator used by the decoder.</param>
    /// <param name="referencePatchCount">The number of distinct source rectangles.</param>
    /// <param name="placementCount">The total number of destination placements.</param>
    /// <param name="blendingCount">The total number of color and extra-channel blend records.</param>
    /// <param name="blendingStride">One color record followed by one record for each encoded extra channel.</param>
    /// <param name="usesColorAlphaBlend">Whether a validated color record uses above or below alpha composition.</param>
    /// <param name="references">The borrowed reference slots validated while counting records. Unused slots remain unread.</param>
    private JxlPatches(
        MemoryAllocator allocator,
        int referencePatchCount,
        int placementCount,
        int blendingCount,
        int blendingStride,
        bool usesColorAlphaBlend,
        in InlineArray4<JxlReferenceFrame> references)
    {
        this.referencePatchCount = referencePatchCount;
        this.placementCount = placementCount;
        this.blendingCount = blendingCount;
        this.blendingStride = blendingStride;
        this.usesColorAlphaBlend = usesColorAlphaBlend;
        this.references = references;

        // The first parse has established every count. Allocate each retained array once;
        // no reference group or placement can grow a buffer while its payload is decoded.
        try
        {
            this.referencePatches = allocator.Allocate<ReferencePatch>(referencePatchCount);
            this.placements = allocator.Allocate<Placement>(placementCount);
            this.blendings = allocator.Allocate<Blending>(blendingCount);
        }
        catch
        {
            // Construction has not published this owner. Return only the allocations that
            // completed before the failure; normal disposal operates on three initialized owners.
            this.blendings?.Dispose();
            this.placements?.Dispose();
            this.referencePatches?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the dictionary contains any patch placements.
    /// </summary>
    public bool HasAny => this.placementCount != 0;

    /// <summary>
    /// Reads the dictionary before all other features in the byte-aligned DC-global section.
    /// </summary>
    /// <param name="reader">The bounded reader at the first patch entropy-table field.</param>
    /// <param name="frame">The current frame's coding geometry.</param>
    /// <param name="extraChannelCount">The parsed number of encoded extra channels.</param>
    /// <param name="references">The four ordinary reference slots, retained until patch application finishes.</param>
    /// <param name="sectionByteLength">The validated byte length of the enclosing DC-global section.</param>
    /// <param name="allocator">The decoder's tracked allocator.</param>
    /// <param name="cancellationToken">The token checked before parsing and between reference groups and placements.</param>
    /// <returns>The complete dictionary. The reader continues at the first field after its entropy payload.</returns>
    public static JxlPatches Read(
        ref JxlBitReader reader,
        JxlFrameHeader frame,
        int extraChannelCount,
        ReadOnlySpan<JxlReferenceFrame?> references,
        long sectionByteLength,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        long dictionaryStart = reader.BytePosition;
        using JxlEntropyDecoder codes = JxlEntropyDecoder.Read(ref reader, allocator, ContextCount, true);

        // Keep the checkpoint before CreateReader consumes the initial ANS state. The table owner
        // is immutable and can supply fresh readers for both passes, including prefix and LZ77 modes.
        long payloadStart = reader.Position;
        if (codes.UsesPrefixCodes)
        {
            return codes.UsesLz77
                ? ReadDictionary<JxlEntropyDecoder.PrefixLz77Operator>(
                    ref reader, codes, frame, extraChannelCount, references, dictionaryStart, payloadStart, sectionByteLength, allocator, cancellationToken)
                : ReadDictionary<JxlEntropyDecoder.PrefixOperator>(
                    ref reader, codes, frame, extraChannelCount, references, dictionaryStart, payloadStart, sectionByteLength, allocator, cancellationToken);
        }

        return codes.UsesLz77
            ? ReadDictionary<JxlEntropyDecoder.AnsLz77Operator>(
                ref reader, codes, frame, extraChannelCount, references, dictionaryStart, payloadStart, sectionByteLength, allocator, cancellationToken)
            : ReadDictionary<JxlEntropyDecoder.AnsOperator>(
                ref reader, codes, frame, extraChannelCount, references, dictionaryStart, payloadStart, sectionByteLength, allocator, cancellationToken);
    }

    /// <summary>
    /// Applies saved native samples after restoration and before splines and common upsampling.
    /// </summary>
    /// <param name="colors">The distinct mutable native color planes.</param>
    /// <param name="extras">The mutable extra planes in encoded order.</param>
    /// <param name="header">The image-wide extra-channel and alpha declarations.</param>
    /// <param name="size">The visible coding rectangle, excluding VarDCT padding.</param>
    /// <param name="allocator">The tracked allocator for bounded output scratch.</param>
    /// <param name="cancellationToken">The token checked between placements and rows.</param>
    public void Apply(
        ReadOnlySpan<Buffer2D<float>> colors,
        ReadOnlySpan<Buffer2D<float>> extras,
        JxlHeader header,
        Size size,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
    {
        // Four maximum-width registers give 64 samples per planar row, or 256 bytes. Base scratch holds
        // [component outputs | bounded | complement | alpha | contribution | reciprocal], using (componentCount + 5) * 256 bytes.
        // Color alpha composition additionally reserves packed background and foreground blocks of 1024 bytes each.
        // Compute every output before overwriting input: another channel can still need its original background weight.
        int capacity = 4 * Vector512<float>.Count;
        int colorCount = colors.Length;
        int componentCount = colorCount + extras.Length;
        bool hasAlpha = false;
        foreach (JxlExtraChannelMetadata extra in header.Metadata.ExtraChannels)
        {
            hasAlpha |= extra.Type == JxlExtraChannelType.Alpha;
        }

        // The parser derived the mode flag while validating records. Without an alpha channel,
        // both over modes become replacement and cannot reach the packed-color branch.
        int packedLength = this.usesColorAlphaBlend && hasAlpha ? 4 * capacity : 0;
        int componentLength = (componentCount + 5) * capacity;
        using IMemoryOwner<float> owner = allocator.Allocate<float>(componentLength + (2 * packedLength));
        Span<float> workspace = owner.Memory.Span;
        Span<float> bounded = workspace.Slice(componentCount * capacity, capacity);
        Span<float> complement = workspace.Slice((componentCount + 1) * capacity, capacity);
        Span<float> alpha = workspace.Slice((componentCount + 2) * capacity, capacity);
        Span<float> contribution = workspace.Slice((componentCount + 3) * capacity, capacity);
        Span<float> reciprocal = workspace.Slice((componentCount + 4) * capacity, capacity);
        Span<Vector4> backgroundColors = MemoryMarshal.Cast<float, Vector4>(workspace.Slice(componentLength, packedLength));
        Span<Vector4> foregroundColors = MemoryMarshal.Cast<float, Vector4>(workspace.Slice(componentLength + packedLength, packedLength));
        ReadOnlySpan<ReferencePatch> sources = this.referencePatches.Memory.Span[..this.referencePatchCount];
        ReadOnlySpan<Placement> positions = this.placements.Memory.Span[..this.placementCount];
        ReadOnlySpan<Blending> modes = this.blendings.Memory.Span[..this.blendingCount];

        for (int placementIndex = 0; placementIndex < positions.Length; placementIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Placement position = positions[placementIndex];
            ReferencePatch source = sources[position.Source];
            JxlReferenceFrame reference = this.references[source.Reference];
            ReadOnlySpan<Blending> blending = modes.Slice(placementIndex * this.blendingStride, this.blendingStride);
            Rectangle destination = Rectangle.Intersect(new Rectangle(position.Position, source.Rectangle.Size), new Rectangle(Point.Empty, size));
            Buffer2DRegion<float> referenceRed = reference.GetColorChannel(0);
            Buffer2DRegion<float> referenceGreen = reference.GetColorChannel(1);
            Buffer2DRegion<float> referenceBlue = reference.GetColorChannel(2);

            // Valid VarDCT placements can extend into padding. Only the visible intersection
            // reaches later image features; retain the corresponding immutable source offset.
            for (int y = destination.Top; y < destination.Bottom; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int sourceY = source.Rectangle.Y + y - position.Position.Y;
                Span<float> backgroundRed = colors[0].DangerousGetRowSpan(y);
                Span<float> backgroundGreen = colorCount == 1 ? backgroundRed : colors[1].DangerousGetRowSpan(y);
                Span<float> backgroundBlue = colorCount == 1 ? backgroundRed : colors[2].DangerousGetRowSpan(y);
                ReadOnlySpan<float> foregroundRed = referenceRed.DangerousGetRowSpan(sourceY);
                ReadOnlySpan<float> foregroundGreen = referenceGreen.DangerousGetRowSpan(sourceY);
                ReadOnlySpan<float> foregroundBlue = referenceBlue.DangerousGetRowSpan(sourceY);
                for (int x = destination.Left; x < destination.Right; x += capacity)
                {
                    int length = Math.Min(capacity, destination.Right - x);
                    int sourceX = source.Rectangle.X + x - position.Position.X;

                    // Compute extras first. A color blend can replace its selected weight's output
                    // afterward, without changing any background weights still needed by other equations.
                    for (int channel = 0; channel < extras.Length; channel++)
                    {
                        Blending mode = blending[channel + 1];
                        ReadOnlySpan<float> background = extras[channel].DangerousGetRowSpan(y).Slice(x, length);
                        ReadOnlySpan<float> foreground = reference.GetExtraChannel(channel).DangerousGetRowSpan(sourceY).Slice(sourceX, length);
                        ReadOnlySpan<float> backgroundWeight = [];
                        ReadOnlySpan<float> foregroundWeight = [];
                        bool associated = false;
                        bool selectedWeight = channel == mode.AlphaChannel;

                        if (mode.Mode >= BlendMode.BlendAbove)
                        {
                            int weight = mode.AlphaChannel;
                            backgroundWeight = extras[weight].DangerousGetRowSpan(y).Slice(x, length);
                            foregroundWeight = reference.GetExtraChannel(weight).DangerousGetRowSpan(sourceY).Slice(sourceX, length);
                            associated = header.Metadata.ExtraChannels[weight].AlphaRepresentation == PixelAlphaRepresentation.Associated;

                            mode.PrepareWeights(
                                associated,
                                selectedWeight,
                                backgroundWeight,
                                foregroundWeight,
                                bounded[..length],
                                complement[..length],
                                alpha[..length],
                                reciprocal[..length]);
                        }

                        mode.Apply(
                            associated,
                            selectedWeight,
                            background,
                            foreground,
                            backgroundWeight,
                            foregroundWeight,
                            workspace.Slice((colorCount + channel) * capacity, length),
                            bounded[..length],
                            complement[..length],
                            alpha[..length],
                            contribution[..length],
                            reciprocal[..length]);
                    }

                    Blending color = blending[0];
                    ReadOnlySpan<float> colorBackgroundWeight = [];
                    ReadOnlySpan<float> colorForegroundWeight = [];
                    bool colorAssociated = false;

                    if (hasAlpha && color.Mode >= BlendMode.BlendAbove)
                    {
                        int weight = color.AlphaChannel;
                        colorBackgroundWeight = extras[weight].DangerousGetRowSpan(y).Slice(x, length);
                        colorForegroundWeight = reference.GetExtraChannel(weight).DangerousGetRowSpan(sourceY).Slice(sourceX, length);
                        colorAssociated = header.Metadata.ExtraChannels[weight].AlphaRepresentation == PixelAlphaRepresentation.Associated;

                        // All color components use the same weights. Prepare their combined weight and
                        // reciprocal once; each component keeps its own products and output samples.
                        color.PrepareWeights(
                            colorAssociated,
                            false,
                            colorBackgroundWeight,
                            colorForegroundWeight,
                            bounded[..length],
                            complement[..length],
                            alpha[..length],
                            reciprocal[..length]);
                    }
                    else if (!hasAlpha)
                    {
                        // Without a declared alpha channel, libjxl blending.cc copies the reference
                        // for both composition orders. Weighted addition becomes ordinary addition.
                        BlendMode colorMode = color.Mode switch
                        {
                            BlendMode.BlendAbove or BlendMode.BlendBelow => BlendMode.Replace,
                            BlendMode.AlphaWeightedAddAbove or BlendMode.AlphaWeightedAddBelow => BlendMode.Add,
                            _ => color.Mode
                        };

                        color = new Blending(colorMode, color.AlphaChannel, color.Clamp);
                    }

                    if (color.Mode is BlendMode.BlendAbove or BlendMode.BlendBelow)
                    {
                        // Only the top weight is clamped. Pack each complete logical RGB block once;
                        // gray repeats its one plane, and black remains an independently blended extra.
                        ReadOnlySpan<float> backgroundWeight = color.Clamp && color.Mode == BlendMode.BlendBelow
                            ? bounded[..length] : colorBackgroundWeight;
                        ReadOnlySpan<float> foregroundWeight = color.Clamp && color.Mode == BlendMode.BlendAbove
                            ? bounded[..length] : colorForegroundWeight;
                        Span<Vector4> background = backgroundColors[..length];
                        Span<Vector4> foreground = foregroundColors[..length];

                        // Reconstruction stores separate component planes. The shared blender instead
                        // consumes complete [C0, C1, C2, weight] vectors. Join each input once in its
                        // disjoint scratch block; this changes layout without conversion or rounding.
                        // W holds the selected extra-channel weight, never CMYK black or a packed pixel.
                        SimdUtils.InterleaveFloatPlanes(
                            backgroundRed.Slice(x, length),
                            backgroundGreen.Slice(x, length),
                            backgroundBlue.Slice(x, length),
                            backgroundWeight,
                            background);
                        SimdUtils.InterleaveFloatPlanes(
                            foregroundRed.Slice(sourceX, length),
                            foregroundGreen.Slice(sourceX, length),
                            foregroundBlue.Slice(sourceX, length),
                            foregroundWeight,
                            foreground);

                        color.ApplyColors(colorAssociated, background, foreground);
                        Span<float> red = workspace[..length];
                        Span<float> green = colorCount == 1 ? [] : workspace.Slice(capacity, length);
                        Span<float> blue = colorCount == 1 ? [] : workspace.Slice(2 * capacity, length);

                        // Return the three results to planar storage for later patch, spline and
                        // upsampling operations. Discard the blender's W: native combined weights are
                        // retained separately and published only after every input weight has been read.
                        // Gray supplied the same plane three times, so only its first output is needed.
                        SimdUtils.DeinterleaveFloatPlanes(background, red, green, blue, []);
                        if (!colorAssociated)
                        {
                            // The shared kernel returns the weighted numerator. Restore straight
                            // components with the reciprocal prepared from JXL's combined weight.
                            // That multiplier is zero when the combined weight is not positive.
                            // Gray has one stored output plane, so normalize it only once.
                            for (int channel = 0; channel < colorCount; channel++)
                            {
                                Span<float> result = workspace.Slice(channel * capacity, length);
                                TensorPrimitives.Multiply(result, reciprocal[..length], result);
                            }
                        }
                    }
                    else
                    {
                        for (int channel = 0; channel < colorCount; channel++)
                        {
                            ReadOnlySpan<float> background = channel == 0 ? backgroundRed : channel == 1 ? backgroundGreen : backgroundBlue;
                            ReadOnlySpan<float> foreground = channel == 0 ? foregroundRed : channel == 1 ? foregroundGreen : foregroundBlue;

                            color.Apply(
                                colorAssociated,
                                false,
                                background.Slice(x, length),
                                foreground.Slice(sourceX, length),
                                colorBackgroundWeight,
                                colorForegroundWeight,
                                workspace.Slice(channel * capacity, length),
                                bounded[..length],
                                complement[..length],
                                alpha[..length],
                                contribution[..length],
                                reciprocal[..length]);
                        }
                    }

                    if (hasAlpha && color.Mode is BlendMode.BlendAbove or BlendMode.BlendBelow)
                    {
                        alpha[..length].CopyTo(workspace.Slice((colorCount + color.AlphaChannel) * capacity, length));
                    }

                    // Reference planes never alias mutable reconstruction. Commit only after every
                    // component is ready, including a weight replaced by the color-blend equation.
                    for (int channel = 0; channel < colorCount; channel++)
                    {
                        Span<float> output = channel == 0 ? backgroundRed : channel == 1 ? backgroundGreen : backgroundBlue;
                        workspace.Slice(channel * capacity, length).CopyTo(output.Slice(x, length));
                    }

                    for (int channel = 0; channel < extras.Length; channel++)
                    {
                        workspace.Slice((colorCount + channel) * capacity, length).CopyTo(extras[channel].DangerousGetRowSpan(y).Slice(x, length));
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (this.isDisposed)
        {
            return;
        }

        this.blendings.Dispose();
        this.placements.Dispose();
        this.referencePatches.Dispose();

        // These are borrowed frames, not owned planes. Release the managed references without
        // disposing frames whose lifetime still belongs to the decoder's ordinary reference slots.
        this.references = default;
        this.isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Counts and validates the payload, then reads it again into exactly sized retained storage.
    /// </summary>
    /// <typeparam name="TOperator">The entropy mode selected once from the decoded tables.</typeparam>
    /// <param name="reader">The bounded reader before the initial entropy state.</param>
    /// <param name="codes">The table owner shared by both parses.</param>
    /// <param name="frame">The frame's coding geometry.</param>
    /// <param name="extraChannelCount">The encoded extra-channel count.</param>
    /// <param name="references">The available ordinary reference slots.</param>
    /// <param name="dictionaryStart">The byte-aligned beginning of the enclosing section.</param>
    /// <param name="payloadStart">The bit position after the tables and before the initial entropy state.</param>
    /// <param name="sectionByteLength">The enclosing section's validated byte length.</param>
    /// <param name="allocator">The decoder's tracked allocator.</param>
    /// <param name="cancellationToken">The token checked between reference groups and placements.</param>
    /// <returns>The completed record owner, with no retained table or history workspace.</returns>
    private static JxlPatches ReadDictionary<TOperator>(
        ref JxlBitReader reader,
        JxlEntropyDecoder codes,
        JxlFrameHeader frame,
        int extraChannelCount,
        ReadOnlySpan<JxlReferenceFrame?> references,
        long dictionaryStart,
        long payloadStart,
        long sectionByteLength,
        MemoryAllocator allocator,
        CancellationToken cancellationToken)
        where TOperator : struct, JxlEntropyDecoder.IEntropyOperator
    {
        Size codingSize = frame.CodingSize;
        ulong width = (uint)codingSize.Width;
        ulong height = (uint)codingSize.Height;
        if (!frame.Modular)
        {
            // Native frame_dimensions.h pads VarDCT coding planes to complete eight-sample
            // blocks. Modular dimensions remain unpadded. Patch positions use this coding grid.
            width = (width + 7) & ~7UL;
            height = (height + 7) & ~7UL;
        }

        // Each rounded dimension is at most 2^31, so the pixel product and these native
        // source/placement limits fit ulong. Actual retained lengths are checked separately.
        ulong maximumReferences = 1024 + ((width * height) / 4);
        ulong maximumPlacements = maximumReferences * 4;
        int blendingStride = extraChannelCount + 1;

        // One group uses six fields. Each placement uses two coordinates and each blend record
        // uses at most three fields. Cap each term before multiplying: history never exceeds the
        // existing one-megavalue LZ77 window, even when singleton codes expand very short input.
        ulong maximumHistoryBlendings = Math.Min(maximumPlacements, JxlEntropyDecoder.MaximumLz77Distance) * (uint)blendingStride;
        ulong maximumHistoryValues = 1
            + (6 * Math.Min(maximumReferences, JxlEntropyDecoder.MaximumLz77Distance))
            + (2 * Math.Min(maximumPlacements, JxlEntropyDecoder.MaximumLz77Distance))
            + (3 * Math.Min(maximumHistoryBlendings, JxlEntropyDecoder.MaximumLz77Distance));

        int historyLength = TOperator.UsesLz77
            ? (int)BitOperations.RoundUpToPowerOf2((uint)Math.Min(maximumHistoryValues, JxlEntropyDecoder.MaximumLz77Distance))
            : 0;

        using IMemoryOwner<uint>? historyOwner = historyLength == 0 ? null : allocator.Allocate<uint>(historyLength);
        Span<uint> history = historyOwner is null ? [] : historyOwner.Memory.Span[..historyLength];
        (int sourceCount, int placementCount, int blendingCount, bool usesColorAlphaBlend) = ReadPayload<TOperator>(
            ref reader,
            codes,
            history,
            width,
            height,
            extraChannelCount,
            references,
            maximumReferences,
            maximumPlacements,
            [],
            [],
            [],
            cancellationToken);

        // The payload has validated every used slot. Capture the nonnull references once;
        // application indexes only validated slots and never reads an unused default entry.
        InlineArray4<JxlReferenceFrame> borrowedReferences = default;
        for (int slot = 0; slot < references.Length; slot++)
        {
            JxlReferenceFrame? reference = references[slot];
            if (reference is not null)
            {
                borrowedReferences[slot] = reference;
            }
        }

        JxlPatches result = new(allocator, sourceCount, placementCount, blendingCount, blendingStride, usesColorAlphaBlend, in borrowedReferences);
        if (sourceCount == 0)
        {
            // The first pass has already consumed and validated the empty dictionary. There
            // are no records to retain and no reason to seek or initialize a second reader.
            return result;
        }

        try
        {
            // Patches are first in the byte-aligned DC-global section. Restore its original
            // bound, then skip the already parsed table bits. This also works across indexed
            // container fragments without copying payloads or changing BufferedReadStream.
            reader.SeekBytePosition(dictionaryStart);
            reader.BeginSection((uint)sectionByteLength);
            reader.SkipBits((ulong)(payloadStart - (dictionaryStart * 8)));

            Span<ReferencePatch> sourceValues = result.referencePatches.Memory.Span[..sourceCount];
            Span<Placement> placementValues = result.placements.Memory.Span[..placementCount];
            Span<Blending> blendingValues = result.blendings.Memory.Span[..blendingCount];
            ReadPayload<TOperator>(
                ref reader,
                codes,
                history,
                width,
                height,
                extraChannelCount,
                references,
                maximumReferences,
                maximumPlacements,
                sourceValues,
                placementValues,
                blendingValues,
                cancellationToken);

            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Decodes the same patch syntax for the counting pass and the storage pass.
    /// </summary>
    /// <typeparam name="TOperator">The stream's fixed entropy mode.</typeparam>
    /// <param name="reader">The bounded reader before the initial entropy state.</param>
    /// <param name="codes">The immutable entropy tables.</param>
    /// <param name="history">The reusable power-of-two LZ77 history, or empty without LZ77.</param>
    /// <param name="width">The current frame's padded coding width.</param>
    /// <param name="height">The current frame's padded coding height.</param>
    /// <param name="extraChannelCount">The encoded extra-channel count.</param>
    /// <param name="references">The four available ordinary reference slots.</param>
    /// <param name="maximumReferences">The native source-rectangle count limit.</param>
    /// <param name="maximumPlacements">The native destination-placement count limit.</param>
    /// <param name="sources">The exact source-record destination, or empty during counting.</param>
    /// <param name="positions">The exact placement destination, or empty during counting.</param>
    /// <param name="blendingValues">The exact blend-record destination, or empty during counting.</param>
    /// <param name="cancellationToken">The token checked between reference groups and placements.</param>
    /// <returns>The exact record counts and whether a validated color record uses alpha composition.</returns>
    private static (int Sources, int Placements, int Blendings, bool UsesColorAlphaBlend) ReadPayload<TOperator>(
        ref JxlBitReader reader,
        JxlEntropyDecoder codes,
        Span<uint> history,
        ulong width,
        ulong height,
        int extraChannelCount,
        ReadOnlySpan<JxlReferenceFrame?> references,
        ulong maximumReferences,
        ulong maximumPlacements,
        Span<ReferencePatch> sources,
        Span<Placement> positions,
        Span<Blending> blendingValues,
        CancellationToken cancellationToken)
        where TOperator : struct, JxlEntropyDecoder.IEntropyOperator
    {
        JxlEntropyDecoder.Reader symbols = codes.CreateReader(ref reader);
        uint decoded = 0;
        uint sourceCount = ReadValue<TOperator>(ref symbols, ref reader, ReferenceCountContext, history, ref decoded);
        if (sourceCount > maximumReferences)
        {
            throw new InvalidImageContentException("The JPEG XL patch dictionary contains too many source rectangles.");
        }

        if (sourceCount > int.MaxValue)
        {
            throw new InvalidMemoryOperationException("The JPEG XL patch source records exceed the supported buffer length.");
        }

        bool store = !sources.IsEmpty;
        int placementCount = 0;
        int blendingStride = extraChannelCount + 1;
        int blendingCount = 0;
        bool usesColorAlphaBlend = false;
        for (int sourceIndex = 0; sourceIndex < sourceCount; sourceIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            uint referenceIndex = ReadValue<TOperator>(ref symbols, ref reader, ReferenceFrameContext, history, ref decoded);
            if (referenceIndex >= references.Length)
            {
                throw new InvalidImageContentException("The JPEG XL patch selects an invalid reference slot.");
            }

            JxlReferenceFrame? reference = references[(int)referenceIndex];
            if (reference is null || !reference.SavedBeforeColorTransform)
            {
                throw new InvalidImageContentException("The JPEG XL patch requires an available reference saved before the color transform.");
            }

            uint sourceX = ReadValue<TOperator>(ref symbols, ref reader, SourcePositionContext, history, ref decoded);
            uint sourceY = ReadValue<TOperator>(ref symbols, ref reader, SourcePositionContext, history, ref decoded);
            ulong sourceWidth = (ulong)ReadValue<TOperator>(ref symbols, ref reader, PatchSizeContext, history, ref decoded) + 1;
            ulong sourceHeight = (ulong)ReadValue<TOperator>(ref symbols, ref reader, PatchSizeContext, history, ref decoded) + 1;
            if (sourceX + sourceWidth > (uint)reference.Size.Width || sourceY + sourceHeight > (uint)reference.Size.Height)
            {
                throw new InvalidImageContentException("The JPEG XL patch source rectangle exceeds its saved reference.");
            }

            ulong repetitions = (ulong)ReadValue<TOperator>(ref symbols, ref reader, RepetitionCountContext, history, ref decoded) + 1;
            ulong nextPlacementCount = (ulong)placementCount + repetitions;
            if (nextPlacementCount > maximumPlacements)
            {
                throw new InvalidImageContentException("The JPEG XL patch dictionary contains too many placements.");
            }

            if (nextPlacementCount > int.MaxValue)
            {
                throw new InvalidMemoryOperationException("The JPEG XL patch placements exceed the supported buffer length.");
            }

            ulong nextBlendingCount = nextPlacementCount * (uint)blendingStride;
            if (nextBlendingCount > int.MaxValue)
            {
                throw new InvalidMemoryOperationException("The JPEG XL patch blend records exceed the supported buffer length.");
            }

            if (store)
            {
                sources[sourceIndex] = new ReferencePatch(
                    (byte)referenceIndex, new Rectangle((int)sourceX, (int)sourceY, (int)sourceWidth, (int)sourceHeight));
            }

            long x = 0;
            long y = 0;
            for (ulong placement = 0; placement < repetitions; placement++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (placement == 0)
                {
                    x = ReadValue<TOperator>(ref symbols, ref reader, DestinationPositionContext, history, ref decoded);
                    y = ReadValue<TOperator>(ref symbols, ref reader, DestinationPositionContext, history, ref decoded);
                }
                else
                {
                    uint deltaX = ReadValue<TOperator>(ref symbols, ref reader, PositionOffsetContext, history, ref decoded);
                    uint deltaY = ReadValue<TOperator>(ref symbols, ref reader, PositionOffsetContext, history, ref decoded);
                    x += (int)(deltaX >> 1) ^ -(int)(deltaX & 1);
                    y += (int)(deltaY >> 1) ^ -(int)(deltaY & 1);
                }

                // Each preceding position was bounded by the coding dimensions. Adding a signed
                // 32-bit delta in long retains its sign, instead of wrapping an invalid position.
                if (x < 0 || y < 0 || (ulong)x + sourceWidth > width || (ulong)y + sourceHeight > height)
                {
                    throw new InvalidImageContentException("The JPEG XL patch destination rectangle exceeds its coding frame.");
                }

                if (store)
                {
                    positions[placementCount] = new Placement(sourceIndex, new Point((int)x, (int)y));
                }

                for (int component = 0; component < blendingStride; component++)
                {
                    uint modeValue = ReadValue<TOperator>(ref symbols, ref reader, BlendModeContext, history, ref decoded);
                    if (modeValue > (uint)BlendMode.AlphaWeightedAddBelow)
                    {
                        throw new InvalidImageContentException("The JPEG XL patch contains an invalid blend mode.");
                    }

                    BlendMode mode = (BlendMode)modeValue;
                    usesColorAlphaBlend |= component == 0 && mode is BlendMode.BlendAbove or BlendMode.BlendBelow;
                    bool usesAlpha = mode >= BlendMode.BlendAbove;
                    uint alphaChannel = 0;
                    if (usesAlpha && extraChannelCount > 1)
                    {
                        alphaChannel = ReadValue<TOperator>(ref symbols, ref reader, WeightChannelContext, history, ref decoded);
                        if (alphaChannel >= extraChannelCount)
                        {
                            throw new InvalidImageContentException("The JPEG XL patch selects an invalid extra weight channel.");
                        }
                    }

                    // With zero or one extra channel the weight index is implicit. The color
                    // blend has defined no-alpha fallbacks, so the parser must not reject that case.
                    bool clamp = mode >= BlendMode.Multiply && ReadValue<TOperator>(ref symbols, ref reader, ClampContext, history, ref decoded) != 0;
                    if (store)
                    {
                        blendingValues[blendingCount] = new Blending(mode, (ushort)alphaChannel, clamp);
                    }

                    blendingCount++;
                }

                placementCount++;
            }
        }

        // Native validates the final ANS state, not completion of a trailing LZ77 copy.
        // A fresh Reader restarts both states on the second pass; reused history is overwritten
        // before each position can be read, so neither pass needs to clear the history buffer.
        symbols.CheckFinalState();
        return ((int)sourceCount, placementCount, blendingCount, usesColorAlphaBlend);
    }

    /// <summary>
    /// Reads a patch-context integer and advances history only when its entropy mode uses LZ77.
    /// </summary>
    /// <typeparam name="TOperator">The stream's fixed entropy mode.</typeparam>
    /// <param name="symbols">The current entropy state.</param>
    /// <param name="reader">The bounded input.</param>
    /// <param name="context">One of the ten fixed patch contexts.</param>
    /// <param name="history">The reusable LZ77 ring, or empty without LZ77.</param>
    /// <param name="decoded">The number of preceding LZ77 values.</param>
    /// <returns>The decoded unsigned integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadValue<TOperator>(
        ref JxlEntropyDecoder.Reader symbols,
        ref JxlBitReader reader,
        int context,
        Span<uint> history,
        ref uint decoded)
        where TOperator : struct, JxlEntropyDecoder.IEntropyOperator
        => symbols.ReadUInt32<TOperator>(ref reader, context, history, TOperator.UsesLz77 ? decoded++ : 0, 0);

    /// <summary>
    /// Identifies one source rectangle in an ordinary pre-transform reference frame.
    /// </summary>
    private readonly struct ReferencePatch
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ReferencePatch"/> struct.
        /// </summary>
        /// <param name="reference">The validated ordinary reference slot, from zero through three.</param>
        /// <param name="rectangle">The validated source rectangle in that reference's samples.</param>
        public ReferencePatch(byte reference, Rectangle rectangle)
        {
            this.Reference = reference;
            this.Rectangle = rectangle;
        }

        /// <summary>
        /// Gets the ordinary reference slot.
        /// </summary>
        public byte Reference { get; }

        /// <summary>
        /// Gets the source rectangle in reference-sample coordinates.
        /// </summary>
        public Rectangle Rectangle { get; }
    }

    /// <summary>
    /// Associates one encoded destination position with its source rectangle.
    /// </summary>
    private readonly struct Placement
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Placement"/> struct.
        /// </summary>
        /// <param name="source">The source-record index retained for this placement.</param>
        /// <param name="position">The destination origin in the frame's coding grid.</param>
        public Placement(int source, Point position)
        {
            this.Source = source;
            this.Position = position;
        }

        /// <summary>
        /// Gets the source-record index.
        /// </summary>
        public int Source { get; }

        /// <summary>
        /// Gets the destination origin in coding samples.
        /// </summary>
        public Point Position { get; }
    }
}
