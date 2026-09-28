// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive AV1 motion-vector entropy syntax.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MotionVectorEntropyTests
{
    /// <summary>
    /// Verifies that integer, quarter-sample, and eighth-sample motion vectors survive a writer and decoder round trip.
    /// </summary>
    [Fact]
    public void WriteMotionVectorRoundTripsRequestedPrecision()
    {
        this.WriteMotionVectorRoundTripsRequestedPrecisionCase((int)Av1MotionVectorPrecision.Integer, 16, -24);
        this.WriteMotionVectorRoundTripsRequestedPrecisionCase((int)Av1MotionVectorPrecision.QuarterSample, 6, -10);
        this.WriteMotionVectorRoundTripsRequestedPrecisionCase((int)Av1MotionVectorPrecision.EighthSample, 11, -17);
    }

    private void WriteMotionVectorRoundTripsRequestedPrecisionCase(
        int precisionValue,
        int rowDelta,
        int columnDelta)
    {
        Av1MotionVectorPrecision precision = (Av1MotionVectorPrecision)precisionValue;
        Av1MotionVector reference = new(27, -11);
        Av1MotionVector value = reference + new Av1MotionVector(rowDelta, columnDelta);
        Av1MotionVectorContext writerContext = new();
        Av1Distribution trailingDistribution = Av1DefaultDistributions.Drl[1];
        using Av1SymbolWriter writer = new(Configuration.Default, 32, updateCdf: true);

        writerContext.Write(writer, value, reference, precision);
        writer.WriteSymbol(true, trailingDistribution);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1FrameEntropyContext decoderContext = new(0);
        Av1SymbolDecoder decoder = new(
            Configuration.Default,
            encoded.Memory.Span,
            decoderContext,
            updateCdf: true);

        Assert.Equal(value, decoder.ReadMotionVector(reference, precision));
        Assert.True(decoder.ReadDrl(1));
    }
}
