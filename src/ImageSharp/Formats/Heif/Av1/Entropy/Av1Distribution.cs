// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;

/// <summary>
/// Stores and adapts an AV1 inverse cumulative distribution used by the range coder.
/// </summary>
internal sealed class Av1Distribution
{
    /// <summary>
    /// The exclusive upper bound of the Q15 probability domain.
    /// </summary>
    public const int ProbabilityTop = 1 << ProbabilityBitCount;

    /// <summary>
    /// The minimum sub-range reserved for each symbol during range coding.
    /// </summary>
    public const int ProbabilityMinimum = 4;

    /// <summary>
    /// The shift that converts stored Q15 cumulative values to the range-coder precision.
    /// </summary>
    public const int CdfShift = 15 - ProbabilityBitCount;

    /// <summary>
    /// The precision reduction applied before multiplying a cumulative value by the coding range.
    /// </summary>
    public const int ProbabilityShift = 6;

    /// <summary>
    /// The number of fractional bits in a stored cumulative probability.
    /// </summary>
    private const int ProbabilityBitCount = 15;

    /// <summary>
    /// The inverse cumulative thresholds followed by the required zero sentinel.
    /// </summary>
    private InlineArray16<uint> probabilities;

    /// <summary>
    /// The symbol-count contribution to the adaptive update rate.
    /// </summary>
    private readonly int speed;

    /// <summary>
    /// The capped number of observations already incorporated into this distribution.
    /// </summary>
    private int updateCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a binary alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    public Av1Distribution(uint p0)
        : this([p0, 0], 1)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a three-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    public Av1Distribution(uint p0, uint p1)
        : this([p0, p1, 0], 1)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a four-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    public Av1Distribution(uint p0, uint p1, uint p2)
        : this([p0, p1, p2, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a five-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3)
        : this([p0, p1, p2, p3, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a six-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4)
        : this([p0, p1, p2, p3, p4, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a seven-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5)
        : this([p0, p1, p2, p3, p4, p5, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for an eight-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6)
        : this([p0, p1, p2, p3, p4, p5, p6, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a nine-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    /// <param name="p7">The cumulative threshold following symbol seven.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a ten-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    /// <param name="p7">The cumulative threshold following symbol seven.</param>
    /// <param name="p8">The cumulative threshold following symbol eight.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for an eleven-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    /// <param name="p7">The cumulative threshold following symbol seven.</param>
    /// <param name="p8">The cumulative threshold following symbol eight.</param>
    /// <param name="p9">The cumulative threshold following symbol nine.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a twelve-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    /// <param name="p7">The cumulative threshold following symbol seven.</param>
    /// <param name="p8">The cumulative threshold following symbol eight.</param>
    /// <param name="p9">The cumulative threshold following symbol nine.</param>
    /// <param name="p10">The cumulative threshold following symbol ten.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9, uint p10)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a thirteen-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    /// <param name="p7">The cumulative threshold following symbol seven.</param>
    /// <param name="p8">The cumulative threshold following symbol eight.</param>
    /// <param name="p9">The cumulative threshold following symbol nine.</param>
    /// <param name="p10">The cumulative threshold following symbol ten.</param>
    /// <param name="p11">The cumulative threshold following symbol eleven.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9, uint p10, uint p11)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a fourteen-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    /// <param name="p7">The cumulative threshold following symbol seven.</param>
    /// <param name="p8">The cumulative threshold following symbol eight.</param>
    /// <param name="p9">The cumulative threshold following symbol nine.</param>
    /// <param name="p10">The cumulative threshold following symbol ten.</param>
    /// <param name="p11">The cumulative threshold following symbol eleven.</param>
    /// <param name="p12">The cumulative threshold following symbol twelve.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9, uint p10, uint p11, uint p12)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class for a sixteen-symbol alphabet.
    /// </summary>
    /// <param name="p0">The cumulative threshold following symbol zero.</param>
    /// <param name="p1">The cumulative threshold following symbol one.</param>
    /// <param name="p2">The cumulative threshold following symbol two.</param>
    /// <param name="p3">The cumulative threshold following symbol three.</param>
    /// <param name="p4">The cumulative threshold following symbol four.</param>
    /// <param name="p5">The cumulative threshold following symbol five.</param>
    /// <param name="p6">The cumulative threshold following symbol six.</param>
    /// <param name="p7">The cumulative threshold following symbol seven.</param>
    /// <param name="p8">The cumulative threshold following symbol eight.</param>
    /// <param name="p9">The cumulative threshold following symbol nine.</param>
    /// <param name="p10">The cumulative threshold following symbol ten.</param>
    /// <param name="p11">The cumulative threshold following symbol eleven.</param>
    /// <param name="p12">The cumulative threshold following symbol twelve.</param>
    /// <param name="p13">The cumulative threshold following symbol thirteen.</param>
    /// <param name="p14">The cumulative threshold following symbol fourteen.</param>
    public Av1Distribution(uint p0, uint p1, uint p2, uint p3, uint p4, uint p5, uint p6, uint p7, uint p8, uint p9, uint p10, uint p11, uint p12, uint p13, uint p14)
        : this([p0, p1, p2, p3, p4, p5, p6, p7, p8, p9, p10, p11, p12, p13, p14, 0], 2)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class from forward cumulative thresholds.
    /// </summary>
    /// <param name="props">The forward Q15 thresholds followed by a zero sentinel slot.</param>
    /// <param name="speed">The symbol-count contribution to the update rate.</param>
    private Av1Distribution(ReadOnlySpan<uint> props, int speed)
    {
        Span<uint> probabilities = this.probabilities;

        // AV1 range coding consumes inverse cumulative thresholds. The defaults are written in the more readable
        // forward form, so convert every real threshold while leaving the final zero sentinel untouched.
        for (int i = 0; i < props.Length - 1; i++)
        {
            probabilities[i] = ProbabilityTop - props[i];
        }

        this.NumberOfSymbols = props.Length;
        this.speed = speed;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Av1Distribution"/> class with the same probability and adaptation state as another distribution.
    /// </summary>
    /// <param name="source">The distribution state to copy.</param>
    private Av1Distribution(Av1Distribution source)
    {
        ReadOnlySpan<uint> sourceProbabilities = source.probabilities;
        Span<uint> probabilities = this.probabilities;
        sourceProbabilities[..source.NumberOfSymbols].CopyTo(probabilities);

        // The adaptation rate depends on both the alphabet size and prior update count, so copying only the
        // thresholds would make the cloned frame context diverge after its next symbol.
        this.speed = source.speed;
        this.updateCount = source.updateCount;
        this.NumberOfSymbols = source.NumberOfSymbols;
    }

    /// <summary>
    /// Gets the number of symbols represented by the distribution.
    /// </summary>
    public int NumberOfSymbols { get; }

    /// <summary>
    /// Gets an inverse cumulative threshold by symbol index.
    /// </summary>
    /// <param name="index">The zero-based threshold index.</param>
    /// <returns>The Q15 inverse cumulative threshold.</returns>
    public uint this[int index] => this.probabilities[index];

    /// <summary>
    /// Creates an independently adaptable copy of a distribution.
    /// </summary>
    /// <returns>A distribution initialized with the same probabilities and update count.</returns>
    public Av1Distribution CreateCopy() => new(this);

    /// <summary>
    /// Replaces the probability and adaptation state with the state of another distribution having the same alphabet.
    /// </summary>
    /// <param name="source">The distribution state to copy.</param>
    public void CopyFrom(Av1Distribution source)
    {
        // Entropy contexts are created from the same fixed default table shape. Copy only mutable state so resetting a
        // working tile never allocates or replaces the distribution objects referenced by the symbol decoder.
        ReadOnlySpan<uint> sourceProbabilities = source.probabilities;
        Span<uint> probabilities = this.probabilities;
        sourceProbabilities[..source.NumberOfSymbols].CopyTo(probabilities);
        this.updateCount = source.updateCount;
    }

    /// <summary>
    /// Resets the observation count that controls the adaptive update rate without changing probability thresholds.
    /// </summary>
    public void ResetUpdateCount() => this.updateCount = 0;

    /// <summary>
    /// Creates independently adaptable copies of a distribution array.
    /// </summary>
    /// <param name="source">The distributions to copy.</param>
    /// <returns>An array with the same shape and distribution state.</returns>
    public static Av1Distribution[] CreateCopy(Av1Distribution[] source)
    {
        Av1Distribution[] result = new Av1Distribution[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            result[i] = source[i].CreateCopy();
        }

        return result;
    }

    /// <summary>
    /// Creates independently adaptable copies of a two-dimensional jagged distribution array.
    /// </summary>
    /// <param name="source">The distributions to copy.</param>
    /// <returns>An array with the same shape and distribution state.</returns>
    public static Av1Distribution[][] CreateCopy(Av1Distribution[][] source)
    {
        Av1Distribution[][] result = new Av1Distribution[source.Length][];
        for (int i = 0; i < source.Length; i++)
        {
            result[i] = CreateCopy(source[i]);
        }

        return result;
    }

    /// <summary>
    /// Creates independently adaptable copies of a three-dimensional jagged distribution array.
    /// </summary>
    /// <param name="source">The distributions to copy.</param>
    /// <returns>An array with the same shape and distribution state.</returns>
    public static Av1Distribution[][][] CreateCopy(Av1Distribution[][][] source)
    {
        Av1Distribution[][][] result = new Av1Distribution[source.Length][][];
        for (int i = 0; i < source.Length; i++)
        {
            result[i] = CreateCopy(source[i]);
        }

        return result;
    }

    /// <summary>
    /// Adapts the cumulative thresholds after coding one symbol.
    /// </summary>
    /// <param name="value">The zero-based symbol that was coded.</param>
    public void Update(int value)
    {
        // AV1 slows adaptation after 16 and 32 observations. The symbol-count term is precomputed by each overload
        // because every distribution has a fixed alphabet size.
        int rate15 = this.updateCount > 15 ? 1 : 0;
        int rate31 = this.updateCount > 31 ? 1 : 0;
        int rate = 3 + rate15 + rate31 + this.speed;
        int tmp = ProbabilityTop;

        // Switching tmp to zero at the observed symbol moves the thresholds on either side toward the sample while
        // preserving their inverse-cumulative ordering in one pass.
        for (int i = 0; i < this.NumberOfSymbols - 1; i++)
        {
            tmp = i == value ? 0 : tmp;
            uint p = this.probabilities[i];
            if (tmp < p)
            {
                this.probabilities[i] -= (ushort)((p - tmp) >> rate);
            }
            else
            {
                this.probabilities[i] += (ushort)((tmp - p) >> rate);
            }
        }

        int rate32 = this.updateCount < 32 ? 1 : 0;
        this.updateCount += rate32;
    }
}
