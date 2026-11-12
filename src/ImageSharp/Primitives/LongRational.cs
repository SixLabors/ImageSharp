// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Globalization;
using System.Text;

namespace SixLabors.ImageSharp;

/// <summary>
/// Represents a fraction with a signed 64-bit numerator and denominator.
/// </summary>
/// <remarks>
/// Stores metadata values without converting them to floating point. The constructor retains both components unchanged.
/// A zero denominator represents an indeterminate value when the numerator is zero, or infinity when it is nonzero.
/// </remarks>
public readonly struct LongRational : IEquatable<LongRational>
{
    /// <summary>
    /// The absolute error accepted by the normal metadata conversion mode.
    /// </summary>
    private const double ConversionTolerance = 0.000001;

    /// <summary>
    /// Initializes a new instance of the <see cref="LongRational"/> struct.
    /// </summary>
    /// <param name="numerator">
    /// The number above the line in a vulgar fraction showing how many of the parts
    /// indicated by the denominator are taken.
    /// </param>
    /// <param name="denominator">
    /// The number below the line in a vulgar fraction; a divisor.
    /// </param>
    public LongRational(long numerator, long denominator)
    {
        this.Numerator = numerator;
        this.Denominator = denominator;
    }

    /// <summary>
    /// Gets the numerator of a number.
    /// </summary>
    public long Numerator { get; }

    /// <summary>
    /// Gets the denominator of a number.
    /// </summary>
    public long Denominator { get; }

    /// <summary>
    /// Gets a value indicating whether this instance is indeterminate.
    /// </summary>
    public bool IsIndeterminate => this.Denominator == 0 && this.Numerator == 0;

    /// <summary>
    /// Gets a value indicating whether this instance is an integer (n, 1)
    /// </summary>
    public bool IsInteger => this.Denominator == 1;

    /// <summary>
    /// Gets a value indicating whether this instance is equal to negative infinity (-1, 0)
    /// </summary>
    public bool IsNegativeInfinity => this.Denominator == 0 && this.Numerator == -1;

    /// <summary>
    /// Gets a value indicating whether this instance is equal to positive infinity (1, 0)
    /// </summary>
    public bool IsPositiveInfinity => this.Denominator == 0 && this.Numerator == 1;

    /// <summary>
    /// Gets a value indicating whether this instance is equal to 0 (0, 1)
    /// </summary>
    public bool IsZero => this.Denominator == 1 && this.Numerator == 0;

    /// <summary>
    /// Determines whether two fractions have identical numerators and denominators.
    /// </summary>
    /// <param name="left">The first fraction.</param>
    /// <param name="right">The second fraction.</param>
    /// <returns>Whether both components match.</returns>
    public static bool operator ==(LongRational left, LongRational right) => left.Equals(right);

    /// <summary>
    /// Determines whether two fractions have different numerators or denominators.
    /// </summary>
    /// <param name="left">The first fraction.</param>
    /// <param name="right">The second fraction.</param>
    /// <returns>Whether either component differs.</returns>
    public static bool operator !=(LongRational left, LongRational right) => !left.Equals(right);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is LongRational longRational && this.Equals(longRational);

    /// <inheritdoc/>
    public bool Equals(LongRational other)
        => this.Numerator == other.Numerator && this.Denominator == other.Denominator;

    /// <inheritdoc/>
    public override int GetHashCode()
        => HashCode.Combine(this.Numerator, this.Denominator);

    /// <inheritdoc/>
    public override string ToString()
        => this.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Converts the numeric value of this instance to its equivalent string representation using
    /// the specified culture-specific format information.
    /// </summary>
    /// <param name="provider">
    /// An object that supplies culture-specific formatting information.
    /// </param>
    /// <returns>The <see cref="string"/></returns>
    public string ToString(IFormatProvider provider)
    {
        if (this.IsIndeterminate)
        {
            return "[ Indeterminate ]";
        }

        if (this.IsPositiveInfinity)
        {
            return "[ PositiveInfinity ]";
        }

        if (this.IsNegativeInfinity)
        {
            return "[ NegativeInfinity ]";
        }

        if (this.IsZero)
        {
            return "0";
        }

        if (this.IsInteger)
        {
            return this.Numerator.ToString(provider);
        }

        StringBuilder sb = new();
        sb.Append(this.Numerator.ToString(provider))
          .Append('/')
          .Append(this.Denominator.ToString(provider));

        return sb.ToString();
    }

    /// <summary>
    /// Converts a floating-point value to a reduced fraction.
    /// </summary>
    /// <param name="value">The <see cref="double"/> to create the instance from.</param>
    /// <param name="bestPrecision">
    /// Whether to retain floating-point precision where the component limits permit it.
    /// Otherwise, the conversion accepts an absolute error of 0.000001.
    /// </param>
    /// <returns>The simplified fraction, or an indeterminate or infinite value for a nonfinite input.</returns>
    /// <remarks>
    /// The denominator is positive for finite results. Finite values outside the numerator's signed 64-bit range
    /// are clamped to that range. NaN becomes 0/0. Positive and negative infinity become 1/0 and -1/0.
    /// </remarks>
    public static LongRational FromDouble(double value, bool bestPrecision)
        => FromDouble(value, bestPrecision, value < 0 ? (ulong)long.MaxValue + 1 : long.MaxValue, long.MaxValue);

    /// <summary>
    /// Converts a value within the component limits of the destination metadata type.
    /// </summary>
    /// <param name="value">The signed value to convert.</param>
    /// <param name="bestPrecision">Whether the result must retain floating-point precision where possible.</param>
    /// <param name="maximumNumerator">The positive numerator magnitude limit selected for the input's sign.</param>
    /// <param name="maximumDenominator">The positive denominator limit.</param>
    /// <returns>The reduced fraction with bounded components.</returns>
    internal static LongRational FromDouble(double value, bool bestPrecision, ulong maximumNumerator, ulong maximumDenominator)
    {
        if (value == 0.0)
        {
            return new LongRational(0, 1);
        }

        if (double.IsNaN(value))
        {
            return new LongRational(0, 0);
        }

        if (double.IsPositiveInfinity(value))
        {
            return new LongRational(1, 0);
        }

        if (double.IsNegativeInfinity(value))
        {
            return new LongRational(-1, 0);
        }

        bool negative = value < 0;
        double target = Math.Abs(value);
        double tolerance = bestPrecision ? 0 : ConversionTolerance;
        ulong numerator;
        ulong denominator;
        if (target >= maximumNumerator)
        {
            numerator = maximumNumerator;
            denominator = 1;
        }
        else if (target <= tolerance || target <= 0.5 / maximumDenominator)
        {
            // Every positive fraction is at least 1/maximumDenominator. At half that value or less,
            // zero is the nearest representable result. Normal mode also accepts zero within its error bound.
            numerator = 0;
            denominator = 1;
        }
        else
        {
            // Decode the exact binary fraction. Inputs here are normal doubles between 2^-64 and 2^63.
            // Their 53-bit significand needs at most 117 denominator bits, so UInt128 holds both components.
            // Integer remainders avoid the rounding error introduced by repeated floating-point reciprocals.
            ulong bits = BitConverter.DoubleToUInt64Bits(target);
            int exponent = (int)((bits >> 52) & 0x7FF) - 1075;
            UInt128 dividend = (bits & 0x000FFFFFFFFFFFFFUL) | 0x0010000000000000UL;
            UInt128 divisor = 1;
            if (exponent < 0)
            {
                divisor <<= -exponent;
            }
            else
            {
                dividend <<= exponent;
            }

            UInt128 exactNumerator = dividend;
            UInt128 exactDenominator = divisor;

            // Consecutive continued-fraction convergents start at 0/1 and 1/0. Each quotient advances
            // numerator and denominator together, rather than counting toward either input magnitude.
            // After the first step, denominators grow at least as fast as the Fibonacci sequence.
            ulong previousNumerator = 0;
            ulong previousDenominator = 1;
            ulong currentNumerator = 1;
            ulong currentDenominator = 0;

            while (true)
            {
                // Limit the quotient before either multiply/add. The caller supplies positive limits,
                // and both preceding convergents fit them, so these unsigned operations cannot overflow.
                // For inputs less than one, the first convergent is 0/1. Its zero numerator
                // places no limit on the next quotient; the denominator limit still applies.
                ulong maximumStep = currentNumerator == 0 ? ulong.MaxValue : (maximumNumerator - previousNumerator) / currentNumerator;
                if (currentDenominator != 0)
                {
                    maximumStep = Math.Min(maximumStep, (maximumDenominator - previousDenominator) / currentDenominator);
                }

                (UInt128 quotient, UInt128 remainder) = UInt128.DivRem(dividend, divisor);
                ulong step = quotient >= maximumStep ? maximumStep : (ulong)quotient;
                numerator = (step * currentNumerator) + previousNumerator;
                denominator = (step * currentDenominator) + previousDenominator;
                double error = GetConversionError(exactNumerator, exactDenominator, numerator, denominator);
                if (error <= tolerance)
                {
                    if (!bestPrecision && step > 1)
                    {
                        // Intermediate convergents approach this result monotonically. Find the first
                        // quotient that meets the normal error bound, retaining a simpler metadata fraction.
                        ulong lower = 1;
                        ulong upper = step;
                        while (lower < upper)
                        {
                            ulong middle = lower + ((upper - lower) / 2);
                            ulong candidateNumerator = (middle * currentNumerator) + previousNumerator;
                            ulong candidateDenominator = (middle * currentDenominator) + previousDenominator;
                            if (GetConversionError(exactNumerator, exactDenominator, candidateNumerator, candidateDenominator) <= tolerance)
                            {
                                upper = middle;
                            }
                            else
                            {
                                lower = middle + 1;
                            }
                        }

                        numerator = (lower * currentNumerator) + previousNumerator;
                        denominator = (lower * currentDenominator) + previousDenominator;
                    }

                    break;
                }

                if (step == maximumStep)
                {
                    // A further quotient would exceed a component limit. Compare the last complete
                    // convergent with this bounded intermediate; both are reduced fractions.
                    if (currentDenominator != 0 && GetConversionError(exactNumerator, exactDenominator, currentNumerator, currentDenominator) <= error)
                    {
                        numerator = currentNumerator;
                        denominator = currentDenominator;
                    }

                    break;
                }

                if (remainder == 0)
                {
                    break;
                }

                previousNumerator = currentNumerator;
                previousDenominator = currentDenominator;
                currentNumerator = numerator;
                currentDenominator = denominator;
                dividend = divisor;
                divisor = remainder;
            }
        }

        // A negative result can have magnitude 2^63. Apply its sign before narrowing so long.MinValue
        // remains representable. Convergents and their intermediates are already reduced.
        return new LongRational((long)(negative ? -(Int128)numerator : numerator), (long)denominator);
    }

    /// <summary>
    /// Calculates the absolute error of a bounded continued-fraction candidate without rounding its integer components first.
    /// </summary>
    /// <param name="exactNumerator">The input's exact binary numerator.</param>
    /// <param name="exactDenominator">The input's exact binary denominator.</param>
    /// <param name="numerator">The candidate's numerator.</param>
    /// <param name="denominator">The candidate's positive denominator.</param>
    /// <returns>The absolute error in the input's units.</returns>
    private static double GetConversionError(UInt128 exactNumerator, UInt128 exactDenominator, ulong numerator, ulong denominator)
    {
        // N*q uses at most 126 bits. For these convergents and their intermediates, |N*q - D*p|
        // is no greater than max(N,D). Thus D*p also fits UInt128, although unrelated fractions might not.
        // Subtract before converting to double: adjacent integers beyond 2^53 must remain distinct.
        UInt128 left = exactNumerator * denominator;
        UInt128 right = exactDenominator * numerator;
        UInt128 difference = left >= right ? left - right : right - left;

        // Dividing in two stages avoids forming D*q, which can exceed UInt128 for a small input.
        return (double)difference / (double)exactDenominator / denominator;
    }

    /// <summary>
    /// Finds the greatest common divisor of two unsigned component magnitudes.
    /// </summary>
    /// <param name="left">The first magnitude.</param>
    /// <param name="right">The second magnitude.</param>
    /// <returns>The common divisor.</returns>
    private static ulong GreatestCommonDivisor(ulong left, ulong right)
    {
        return right == 0 ? left : GreatestCommonDivisor(right, left % right);
    }

    /// <summary>
    /// Simplifies the <see cref="LongRational"/>
    /// </summary>
    /// <returns>The fraction with common integer factors removed, or the unchanged special value.</returns>
    public LongRational Simplify()
    {
        if (this.IsIndeterminate ||
            this.IsNegativeInfinity ||
            this.IsPositiveInfinity ||
            this.IsInteger ||
            this.IsZero)
        {
            return this;
        }

        if (this.Numerator == this.Denominator)
        {
            return new LongRational(1, 1);
        }

        // long.MinValue has magnitude 2^63. Widen before taking absolute values, then retain those
        // magnitudes as unsigned 64-bit integers so the common-divisor calculation cannot overflow.
        ulong gcd = GreatestCommonDivisor((ulong)Int128.Abs(this.Numerator), (ulong)Int128.Abs(this.Denominator));
        if (gcd > long.MaxValue)
        {
            // A divisor of 2^63 requires each component to be either zero or long.MinValue.
            // Reduce those components directly because a positive signed 64-bit divisor cannot hold 2^63.
            return new LongRational(this.Numerator == 0 ? 0 : -1, this.Denominator == 0 ? 0 : -1);
        }

        if (gcd > 1)
        {
            long divisor = (long)gcd;
            return new LongRational(this.Numerator / divisor, this.Denominator / divisor);
        }

        return this;
    }
}
