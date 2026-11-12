// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl;

/// <summary>
/// Describes the precision of JPEG XL integer or floating-point samples.
/// </summary>
/// <remarks>
/// Integer samples use 1 to 31 bits. Floating-point samples contain one sign bit, 2 to 8 exponent bits and 2 to 23 mantissa bits.
/// The default struct value has zero precision and cannot be assigned as an image or extra-channel bit depth.
/// </remarks>
public readonly struct JxlBitDepth : IEquatable<JxlBitDepth>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JxlBitDepth"/> struct.
    /// </summary>
    /// <param name="bitsPerSample">The total sample precision, including any sign and exponent bits.</param>
    /// <param name="exponentBits">The exponent precision, or zero for unsigned integer samples.</param>
    /// <exception cref="ArgumentOutOfRangeException">The precision is not supported by JPEG XL.</exception>
    public JxlBitDepth(byte bitsPerSample, byte exponentBits)
    {
        if (exponentBits == 0)
        {
            Guard.MustBeBetweenOrEqualTo((int)bitsPerSample, 1, 31, nameof(bitsPerSample));
        }
        else
        {
            // JPEG XL floating-point samples contain one sign bit, 2 to 8 exponent bits and 2 to 23 mantissa bits.
            // Validate the pair here so metadata cannot combine individually valid values into an invalid sample layout.
            Guard.MustBeBetweenOrEqualTo((int)exponentBits, 2, 8, nameof(exponentBits));
            Guard.MustBeBetweenOrEqualTo(bitsPerSample - exponentBits - 1, 2, 23, nameof(bitsPerSample));
        }

        this.BitsPerSample = bitsPerSample;
        this.ExponentBits = exponentBits;
    }

    /// <summary>
    /// Gets the total sample precision, including any sign and exponent bits.
    /// </summary>
    public byte BitsPerSample { get; }

    /// <summary>
    /// Gets the exponent precision. Zero indicates unsigned integer samples.
    /// </summary>
    public byte ExponentBits { get; }

    /// <summary>
    /// Determines whether two sample layouts are equal.
    /// </summary>
    /// <param name="left">The first layout.</param>
    /// <param name="right">The second layout.</param>
    /// <returns>Whether both precisions are equal.</returns>
    public static bool operator ==(JxlBitDepth left, JxlBitDepth right) => left.Equals(right);

    /// <summary>
    /// Determines whether two sample layouts differ.
    /// </summary>
    /// <param name="left">The first layout.</param>
    /// <param name="right">The second layout.</param>
    /// <returns>Whether either precision differs.</returns>
    public static bool operator !=(JxlBitDepth left, JxlBitDepth right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(JxlBitDepth other) => this.BitsPerSample == other.BitsPerSample && this.ExponentBits == other.ExponentBits;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is JxlBitDepth other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(this.BitsPerSample, this.ExponentBits);
}
