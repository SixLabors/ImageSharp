// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Prediction;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive distributions and packed contexts used to select an AV1 single-reference inter mode.
/// </summary>
[Trait("Format", "Avif")]
public class Av1InterModeEntropyTests
{
    /// <summary>
    /// Verifies the exact short-circuit order and symbol polarity of the single-reference inter-mode tree.
    /// </summary>
    [Fact]
    public void ReadInterModeMatchesReference()
    {
        ReadInterModeMatchesReferenceCase((int)Av1PredictionMode.NewMotionVector, 0, -1, -1);
        ReadInterModeMatchesReferenceCase((int)Av1PredictionMode.GlobalMotionVector, 1, 0, -1);
        ReadInterModeMatchesReferenceCase((int)Av1PredictionMode.NearestMotionVector, 1, 1, 0);
        ReadInterModeMatchesReferenceCase((int)Av1PredictionMode.NearMotionVector, 1, 1, 1);
    }

    private static void ReadInterModeMatchesReferenceCase(int expectedMode, int newMvSymbol, int zeroMvSymbol, int refMvSymbol)
    {
        const int modeContext = 77;
        Av1Distribution newMv = Av1DefaultDistributions.NewMv[5];
        Av1Distribution zeroMv = Av1DefaultDistributions.ZeroMv[1];
        Av1Distribution refMv = Av1DefaultDistributions.RefMv[4];
        Av1Distribution drl = Av1DefaultDistributions.Drl[2];
        using Av1SymbolWriter writer = new(Configuration.Default, updateCdf: true);
        Span<byte> output = writer.GetTileBuffer();

        writer.WriteSymbol(ref output, newMvSymbol, newMv);
        if (zeroMvSymbol >= 0)
        {
            writer.WriteSymbol(ref output, zeroMvSymbol, zeroMv);
        }

        if (refMvSymbol >= 0)
        {
            writer.WriteSymbol(ref output, refMvSymbol, refMv);
        }

        // A symbol after the selected leaf proves that the decoder consumed exactly the decisions on that branch.
        writer.WriteSymbol(ref output, true, drl);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, 0, updateCdf: true);

        Assert.Equal((Av1PredictionMode)expectedMode, decoder.ReadInterMode(modeContext));
        Assert.True(decoder.ReadDrl(2));
    }
}
