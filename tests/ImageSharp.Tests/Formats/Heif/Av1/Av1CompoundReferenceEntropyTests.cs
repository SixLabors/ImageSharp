// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies AV1 compound-reference selection and compound inter-mode entropy against the current AV1 reference.
/// </summary>
[Trait("Format", "Avif")]
public class Av1CompoundReferenceEntropyTests
{
    /// <summary>
    /// Verifies the encoder's bounded LAST+GOLDEN branch and compound mode against the decoder's semantic readers.
    /// </summary>
    [Fact]
    public void EncoderWritesLastGoldenCompoundReferenceAndMode()
    {
        const int ReferenceModeContext = 2;
        const int CompoundTypeContext = 3;
        const int ModeContext = 68;
        const Av1PredictionMode Mode = Av1PredictionMode.NewNewMotionVector;
        InlineArray8<byte> referenceCountStorage = default;
        Span<byte> referenceCounts = referenceCountStorage;
        referenceCounts[(int)Av1ReferenceFrameType.Last] = 4;
        referenceCounts[(int)Av1ReferenceFrameType.Golden] = 2;

        using Av1SymbolEncoder encoder = new(Configuration.Default, qIndex: 0, updateCdf: true);
        Span<byte> output = encoder.GetTileBuffer();
        Assert.True(encoder.GetLastGoldenCompoundReferenceCost(
            ReferenceModeContext,
            CompoundTypeContext,
            referenceCounts) > 0);

        Assert.True(Av1SymbolEncoder.GetInterCompoundModeCost(encoder.ModeCosts, Mode, ModeContext) > 0);
        encoder.WriteLastGoldenCompoundReference<Av1SymbolEncoder.SymbolWriteOperation>(
            ref output, ReferenceModeContext, CompoundTypeContext, referenceCounts);

        encoder.WriteInterCompoundMode<Av1SymbolEncoder.SymbolWriteOperation>(ref output, Mode, ModeContext);

        using IMemoryOwner<byte> encoded = encoder.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);
        Assert.True(decoder.ReadIsCompoundReference(ReferenceModeContext));
        Assert.False(decoder.ReadCompoundReferenceIsBidirectional(CompoundTypeContext));
        Assert.False(decoder.ReadUnidirectionalCompoundReference(
            Av1SymbolContextHelper.GetUnidirectionalCompoundBackwardContext(referenceCounts),
            decision: 0));

        Assert.True(decoder.ReadUnidirectionalCompoundReference(
            Av1SymbolContextHelper.GetUnidirectionalCompoundLast3OrGoldenContext(referenceCounts),
            decision: 1));

        Assert.True(decoder.ReadUnidirectionalCompoundReference(
            Av1SymbolContextHelper.GetUnidirectionalCompoundGoldenContext(referenceCounts),
            decision: 2));

        Assert.Equal(Mode, decoder.ReadInterCompoundMode(ModeContext));
    }
}
