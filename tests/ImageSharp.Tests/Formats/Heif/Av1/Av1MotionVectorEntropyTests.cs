// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Motion;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

/// <summary>
/// Verifies the adaptive AV1 normal and displacement motion-vector entropy contexts.
/// </summary>
[Trait("Format", "Avif")]
public class Av1MotionVectorEntropyTests
{
    /// <summary>
    /// Verifies both motion-vector contexts against every normative forward Q15 default from libaom.
    /// </summary>
    [Fact]
    public void MotionVectorDefaultsMatchLibaom()
    {
        Av1FrameEntropyContext context = new(0);

        Assert.NotSame(context.MotionVector, context.DisplacementVector);
        AssertContextDefaults(context.MotionVector);
        AssertContextDefaults(context.DisplacementVector);
    }

    /// <summary>
    /// Verifies integer, quarter-sample, and eighth-sample syntax consumption and reconstruction.
    /// </summary>
    /// <param name="precisionValue">The numeric motion-vector precision.</param>
    /// <param name="horizontal">Indicates whether the coded delta occupies the horizontal component.</param>
    /// <param name="magnitudeClass">The coded magnitude class.</param>
    /// <param name="integerOffset">The coded integer magnitude offset.</param>
    /// <param name="fractional">The coded fractional symbol, or negative when omitted.</param>
    /// <param name="highPrecision">The coded eighth-sample symbol, or negative when omitted.</param>
    /// <param name="expectedMagnitude">The expected positive component in one-eighth-sample units.</param>
    [Theory]
    [InlineData((int)Av1MotionVectorPrecision.Integer, false, 0, 1, -1, -1, 16)]
    [InlineData((int)Av1MotionVectorPrecision.QuarterSample, true, 0, 0, 2, -1, 6)]
    [InlineData((int)Av1MotionVectorPrecision.EighthSample, false, 0, 1, 1, 0, 11)]
    [InlineData((int)Av1MotionVectorPrecision.EighthSample, true, 1, 1, 3, 1, 32)]
    public void ReadMotionVectorUsesRequestedPrecision(
        int precisionValue,
        bool horizontal,
        int magnitudeClass,
        int integerOffset,
        int fractional,
        int highPrecision,
        int expectedMagnitude)
    {
        Av1MotionVectorPrecision precision = (Av1MotionVectorPrecision)precisionValue;
        Av1MotionVectorContext writerContext = new();
        Av1MotionVectorContext.Component component = horizontal ? writerContext.Horizontal : writerContext.Vertical;
        Av1Distribution trailingDistribution = Av1DefaultDistributions.Drl[1];
        using Av1SymbolWriter writer = new(Configuration.Default, 8, updateCdf: true);

        writer.WriteSymbol(horizontal ? 1 : 2, writerContext.Joint);
        writer.WriteSymbol(false, component.Sign);
        writer.WriteSymbol(magnitudeClass, component.MagnitudeClass);

        if (magnitudeClass == 0)
        {
            writer.WriteSymbol(integerOffset, component.ClassZero);
        }
        else
        {
            // CLASS0_BITS is one, so a nonzero class transmits exactly magnitudeClass integer-offset bits.
            for (int bit = 0; bit < magnitudeClass; bit++)
            {
                writer.WriteSymbol((integerOffset >> bit) & 1, component.OffsetBits[bit]);
            }
        }

        if (precision != Av1MotionVectorPrecision.Integer)
        {
            Av1Distribution fractionalDistribution = magnitudeClass == 0
                ? component.ClassZeroFractional[integerOffset]
                : component.Fractional;

            writer.WriteSymbol(fractional, fractionalDistribution);
        }

        if (precision == Av1MotionVectorPrecision.EighthSample)
        {
            Av1Distribution highPrecisionDistribution = magnitudeClass == 0
                ? component.ClassZeroHighPrecision
                : component.HighPrecision;

            writer.WriteSymbol(highPrecision, highPrecisionDistribution);
        }

        // The trailing decision detects either an omitted precision symbol being consumed or a required one being skipped.
        writer.WriteSymbol(true, trailingDistribution);

        using IMemoryOwner<byte> encoded = writer.Exit();
        Av1FrameEntropyContext decoderContext = new(0);
        uint normalJoint = decoderContext.MotionVector.Joint[0];
        uint displacementJoint = decoderContext.DisplacementVector.Joint[0];
        Av1SymbolDecoder decoder = new(Configuration.Default, encoded.Memory.Span, decoderContext, updateCdf: true);
        Av1MotionVector reference = new(27, -11);

        Av1MotionVector actual = decoder.ReadMotionVector(reference, precision);
        Av1MotionVector expected = horizontal
            ? new Av1MotionVector(reference.Row, reference.Column + expectedMagnitude)
            : new Av1MotionVector(reference.Row + expectedMagnitude, reference.Column);

        Assert.Equal(expected, actual);
        Assert.NotEqual(normalJoint, decoderContext.MotionVector.Joint[0]);
        Assert.Equal(displacementJoint, decoderContext.DisplacementVector.Joint[0]);
        Assert.True(decoder.ReadDrl(1));
    }

    /// <summary>
    /// Verifies that normal and displacement motion vectors never share adaptive distribution state.
    /// </summary>
    [Fact]
    public void NormalAndDisplacementContextsAdaptIndependently()
    {
        Av1FrameEntropyContext context = new(0);
        uint displacementJoint = context.DisplacementVector.Joint[0];
        uint normalFractional = context.MotionVector.Vertical.Fractional[0];

        context.MotionVector.Joint.Update(3);
        context.DisplacementVector.Vertical.Fractional.Update(2);

        Assert.NotEqual(displacementJoint, context.MotionVector.Joint[0]);
        Assert.Equal(displacementJoint, context.DisplacementVector.Joint[0]);
        Assert.Equal(normalFractional, context.MotionVector.Vertical.Fractional[0]);
        Assert.NotEqual(normalFractional, context.DisplacementVector.Vertical.Fractional[0]);
    }

    /// <summary>
    /// Verifies that frame-context copies retain complete motion-vector state without sharing it.
    /// </summary>
    [Fact]
    public void FrameEntropyCopyRetainsIndependentMotionVectorState()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext destination = new(0);
        UpdateState(source.MotionVector, 1, 5);
        UpdateState(source.DisplacementVector, 1, 7);

        destination.CopyFrom(source);

        AssertStateEqual(source.MotionVector, destination.MotionVector);
        AssertStateEqual(source.DisplacementVector, destination.DisplacementVector);

        UpdateState(source.MotionVector, 0, 1);
        UpdateState(source.DisplacementVector, 0, 1);

        AssertStateNotEqual(source.MotionVector, destination.MotionVector);
        AssertStateNotEqual(source.DisplacementVector, destination.DisplacementVector);
    }

    /// <summary>
    /// Verifies that publishing frame state resets every motion-vector update count.
    /// </summary>
    [Fact]
    public void FrameEntropySnapshotResetsMotionVectorUpdateCounts()
    {
        Av1FrameEntropyContext source = new(0);
        Av1FrameEntropyContext snapshot = new(0);
        UpdateState(source.MotionVector, 1, 20);
        UpdateState(source.DisplacementVector, 1, 20);

        source.SnapshotTo(snapshot);

        AssertStateEqual(source.MotionVector, snapshot.MotionVector);
        AssertStateEqual(source.DisplacementVector, snapshot.DisplacementVector);

        // The source retains twenty observations while the snapshot restarts at zero. The same next symbol therefore
        // moves identical thresholds by different amounts only when every new distribution participates in reset.
        UpdateState(source.MotionVector, 0, 1);
        UpdateState(snapshot.MotionVector, 0, 1);
        UpdateState(source.DisplacementVector, 0, 1);
        UpdateState(snapshot.DisplacementVector, 0, 1);

        AssertStateNotEqual(source.MotionVector, snapshot.MotionVector);
        AssertStateNotEqual(source.DisplacementVector, snapshot.DisplacementVector);
    }

    /// <summary>
    /// Verifies one complete motion-vector context against the normative defaults.
    /// </summary>
    /// <param name="context">The context under test.</param>
    private static void AssertContextDefaults(Av1MotionVectorContext context)
    {
        Assert.NotSame(context.Vertical, context.Horizontal);
        AssertDistribution(context.Joint, [4096, 11264, 19328]);
        AssertComponentDefaults(context.Vertical);
        AssertComponentDefaults(context.Horizontal);
    }

    /// <summary>
    /// Verifies one component's complete set of normative defaults.
    /// </summary>
    /// <param name="component">The component under test.</param>
    private static void AssertComponentDefaults(Av1MotionVectorContext.Component component)
    {
        AssertDistribution(component.MagnitudeClass, [28672, 30976, 31858, 32320, 32551, 32656, 32740, 32757, 32762, 32767]);
        Assert.Equal(2, component.ClassZeroFractional.Length);
        AssertDistribution(component.ClassZeroFractional[0], [16384, 24576, 26624]);
        AssertDistribution(component.ClassZeroFractional[1], [12288, 21248, 24128]);
        AssertDistribution(component.Fractional, [8192, 17408, 21248]);
        AssertDistribution(component.Sign, [16384]);
        AssertDistribution(component.ClassZeroHighPrecision, [20480]);
        AssertDistribution(component.HighPrecision, [16384]);
        AssertDistribution(component.ClassZero, [27648]);

        ReadOnlySpan<uint> offsetThresholds = [17408, 17920, 18944, 20480, 22528, 24576, 28672, 29952, 29952, 30720];

        Assert.Equal(offsetThresholds.Length, component.OffsetBits.Length);
        for (int bit = 0; bit < offsetThresholds.Length; bit++)
        {
            uint expected = (uint)Av1Distribution.ProbabilityTop - offsetThresholds[bit];

            Assert.Equal(2, component.OffsetBits[bit].NumberOfSymbols);
            Assert.Equal(expected, component.OffsetBits[bit][0]);
        }
    }

    /// <summary>
    /// Verifies one distribution after conversion from forward to inverse cumulative thresholds.
    /// </summary>
    /// <param name="distribution">The distribution under test.</param>
    /// <param name="forwardThresholds">The normative forward Q15 thresholds.</param>
    private static void AssertDistribution(Av1Distribution distribution, ReadOnlySpan<uint> forwardThresholds)
    {
        Assert.Equal(forwardThresholds.Length + 1, distribution.NumberOfSymbols);
        for (int threshold = 0; threshold < forwardThresholds.Length; threshold++)
        {
            uint expected = (uint)Av1Distribution.ProbabilityTop - forwardThresholds[threshold];

            Assert.Equal(expected, distribution[threshold]);
        }
    }

    /// <summary>
    /// Applies the same observations to every distribution in a motion-vector context.
    /// </summary>
    /// <param name="context">The context to adapt.</param>
    /// <param name="symbol">The coded symbol used for each observation.</param>
    /// <param name="count">The number of observations.</param>
    private static void UpdateState(Av1MotionVectorContext context, int symbol, int count)
    {
        for (int update = 0; update < count; update++)
        {
            context.Joint.Update(symbol);
            UpdateState(context.Vertical, symbol);
            UpdateState(context.Horizontal, symbol);
        }
    }

    /// <summary>
    /// Applies one observation to every distribution in one motion-vector component.
    /// </summary>
    /// <param name="component">The component to adapt.</param>
    /// <param name="symbol">The coded symbol used for the observation.</param>
    private static void UpdateState(Av1MotionVectorContext.Component component, int symbol)
    {
        component.MagnitudeClass.Update(symbol);
        component.ClassZeroFractional[0].Update(symbol);
        component.ClassZeroFractional[1].Update(symbol);
        component.Fractional.Update(symbol);
        component.Sign.Update(symbol);
        component.ClassZeroHighPrecision.Update(symbol);
        component.HighPrecision.Update(symbol);
        component.ClassZero.Update(symbol);

        for (int bit = 0; bit < component.OffsetBits.Length; bit++)
        {
            component.OffsetBits[bit].Update(symbol);
        }
    }

    /// <summary>
    /// Verifies equal adaptive thresholds across two motion-vector contexts.
    /// </summary>
    /// <param name="expected">The expected context.</param>
    /// <param name="actual">The actual context.</param>
    private static void AssertStateEqual(Av1MotionVectorContext expected, Av1MotionVectorContext actual)
    {
        Assert.Equal(expected.Joint[0], actual.Joint[0]);
        AssertStateEqual(expected.Vertical, actual.Vertical);
        AssertStateEqual(expected.Horizontal, actual.Horizontal);
    }

    /// <summary>
    /// Verifies equal adaptive thresholds across two motion-vector components.
    /// </summary>
    /// <param name="expected">The expected component.</param>
    /// <param name="actual">The actual component.</param>
    private static void AssertStateEqual(Av1MotionVectorContext.Component expected, Av1MotionVectorContext.Component actual)
    {
        Assert.Equal(expected.MagnitudeClass[0], actual.MagnitudeClass[0]);
        Assert.Equal(expected.ClassZeroFractional[0][0], actual.ClassZeroFractional[0][0]);
        Assert.Equal(expected.ClassZeroFractional[1][0], actual.ClassZeroFractional[1][0]);
        Assert.Equal(expected.Fractional[0], actual.Fractional[0]);
        Assert.Equal(expected.Sign[0], actual.Sign[0]);
        Assert.Equal(expected.ClassZeroHighPrecision[0], actual.ClassZeroHighPrecision[0]);
        Assert.Equal(expected.HighPrecision[0], actual.HighPrecision[0]);
        Assert.Equal(expected.ClassZero[0], actual.ClassZero[0]);

        for (int bit = 0; bit < expected.OffsetBits.Length; bit++)
        {
            Assert.Equal(expected.OffsetBits[bit][0], actual.OffsetBits[bit][0]);
        }
    }

    /// <summary>
    /// Verifies independently adaptive thresholds across two motion-vector contexts.
    /// </summary>
    /// <param name="expected">The independently adapted context.</param>
    /// <param name="actual">The copied or reset context.</param>
    private static void AssertStateNotEqual(Av1MotionVectorContext expected, Av1MotionVectorContext actual)
    {
        Assert.NotEqual(expected.Joint[0], actual.Joint[0]);
        AssertStateNotEqual(expected.Vertical, actual.Vertical);
        AssertStateNotEqual(expected.Horizontal, actual.Horizontal);
    }

    /// <summary>
    /// Verifies independently adaptive thresholds across two motion-vector components.
    /// </summary>
    /// <param name="expected">The independently adapted component.</param>
    /// <param name="actual">The copied or reset component.</param>
    private static void AssertStateNotEqual(Av1MotionVectorContext.Component expected, Av1MotionVectorContext.Component actual)
    {
        Assert.NotEqual(expected.MagnitudeClass[0], actual.MagnitudeClass[0]);
        Assert.NotEqual(expected.ClassZeroFractional[0][0], actual.ClassZeroFractional[0][0]);
        Assert.NotEqual(expected.ClassZeroFractional[1][0], actual.ClassZeroFractional[1][0]);
        Assert.NotEqual(expected.Fractional[0], actual.Fractional[0]);
        Assert.NotEqual(expected.Sign[0], actual.Sign[0]);
        Assert.NotEqual(expected.ClassZeroHighPrecision[0], actual.ClassZeroHighPrecision[0]);
        Assert.NotEqual(expected.HighPrecision[0], actual.HighPrecision[0]);
        Assert.NotEqual(expected.ClassZero[0], actual.ClassZero[0]);

        for (int bit = 0; bit < expected.OffsetBits.Length; bit++)
        {
            Assert.NotEqual(expected.OffsetBits[bit][0], actual.OffsetBits[bit][0]);
        }
    }
}
