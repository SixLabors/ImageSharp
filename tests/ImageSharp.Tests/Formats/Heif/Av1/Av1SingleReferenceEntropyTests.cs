// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive distributions and spatial contexts used to select an AV1 inter block's reference mode and frame.
/// </summary>
[Trait("Format", "Avif")]
public class Av1SingleReferenceEntropyTests
{
    /// <summary>
    /// The quantizer index used to initialize an otherwise unrelated tile entropy encoder.
    /// </summary>
    private const int BaseQIndex = 128;

    /// <summary>
    /// Verifies that the production writer emits every single-reference branch consumed by the decoder.
    /// </summary>
    [Fact]
    public void SingleReferenceWriterRoundTripsEveryReference()
    {
        SingleReferenceWriterRoundTripsEveryReferenceCase((int)Av1ReferenceFrameType.Last);
        SingleReferenceWriterRoundTripsEveryReferenceCase((int)Av1ReferenceFrameType.Last2);
        SingleReferenceWriterRoundTripsEveryReferenceCase((int)Av1ReferenceFrameType.Last3);
        SingleReferenceWriterRoundTripsEveryReferenceCase((int)Av1ReferenceFrameType.Golden);
        SingleReferenceWriterRoundTripsEveryReferenceCase((int)Av1ReferenceFrameType.Backward);
        SingleReferenceWriterRoundTripsEveryReferenceCase((int)Av1ReferenceFrameType.Alternate2);
        SingleReferenceWriterRoundTripsEveryReferenceCase((int)Av1ReferenceFrameType.Alternate);
    }

    private static void SingleReferenceWriterRoundTripsEveryReferenceCase(int referenceFrameValue)
    {
        Av1ReferenceFrameType referenceFrame = (Av1ReferenceFrameType)referenceFrameValue;
        InlineArray8<byte> referenceCountStorage = default;
        Span<byte> referenceCounts = referenceCountStorage;
        referenceCounts[(int)Av1ReferenceFrameType.Last] = 5;
        referenceCounts[(int)Av1ReferenceFrameType.Last2] = 1;
        referenceCounts[(int)Av1ReferenceFrameType.Last3] = 2;
        referenceCounts[(int)Av1ReferenceFrameType.Golden] = 2;
        referenceCounts[(int)Av1ReferenceFrameType.Backward] = 3;
        referenceCounts[(int)Av1ReferenceFrameType.Alternate2] = 3;
        referenceCounts[(int)Av1ReferenceFrameType.Alternate] = 6;

        using Av1SymbolEncoder encoder = new(Configuration.Default, BaseQIndex, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        encoder.WriteSingleReference<Av1SymbolEncoder.SymbolWriteOperation>(ref output, referenceFrame, referenceCounts);

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, BaseQIndex, updateCdf: true);
        Av1ReferenceFrameType decodedReference = ReadSingleReference(ref decoder, referenceCounts);

        Assert.Equal(referenceFrame, decodedReference);
    }

    /// <summary>
    /// Reads one complete single-reference branch through the production semantic entry points.
    /// </summary>
    private static Av1ReferenceFrameType ReadSingleReference(
        ref Av1SymbolDecoder decoder,
        scoped ReadOnlySpan<byte> referenceCounts)
    {
        int context = Av1SymbolContextHelper.GetSingleReferenceBackwardContext(referenceCounts);
        if (decoder.ReadSingleReferenceIsBackward(context))
        {
            context = Av1SymbolContextHelper.GetSingleReferenceAlternateContext(referenceCounts);
            if (decoder.ReadSingleReferenceIsAlternate(context))
            {
                return Av1ReferenceFrameType.Alternate;
            }

            context = Av1SymbolContextHelper.GetSingleReferenceAlternate2Context(referenceCounts);
            return decoder.ReadSingleReferenceIsAlternate2(context)
                ? Av1ReferenceFrameType.Alternate2
                : Av1ReferenceFrameType.Backward;
        }

        context = Av1SymbolContextHelper.GetSingleReferenceLast3OrGoldenContext(referenceCounts);
        if (decoder.ReadSingleReferenceIsLast3OrGolden(context))
        {
            context = Av1SymbolContextHelper.GetSingleReferenceGoldenContext(referenceCounts);
            return decoder.ReadSingleReferenceIsGolden(context)
                ? Av1ReferenceFrameType.Golden
                : Av1ReferenceFrameType.Last3;
        }

        context = Av1SymbolContextHelper.GetSingleReferenceLast2Context(referenceCounts);
        return decoder.ReadSingleReferenceIsLast2(context)
            ? Av1ReferenceFrameType.Last2
            : Av1ReferenceFrameType.Last;
    }
}
