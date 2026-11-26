// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Pbm;

/// <summary>
/// Extensions methods for <see cref="BufferedReadStream"/>.
/// </summary>
internal static class BufferedReadStreamExtensions
{
    /// <summary>
    /// Skip over any whitespace or any comments and signal if EOF has been reached.
    /// </summary>
    /// <param name="stream">The buffered read stream.</param>
    /// <returns><see langword="false"/> if EOF has been reached while reading the stream; see langword="true"/> otherwise.</returns>
    public static bool SkipWhitespaceAndComments(this BufferedReadStream stream)
    {
        bool isWhitespace;
        do
        {
            int val = stream.ReadByte();
            if (val < 0)
            {
                return false;
            }

            // Comments start with '#' and end at the next new-line.
            if (val == 0x23)
            {
                int innerValue;
                do
                {
                    innerValue = stream.ReadByte();
                    if (innerValue < 0)
                    {
                        return false;
                    }
                }
                while (innerValue is not 0x0a);

                // Continue searching for whitespace.
                val = innerValue;
            }

            isWhitespace = val is 0x09 or 0x0a or 0x0d or 0x20;
        }
        while (isWhitespace);
        stream.Seek(-1, SeekOrigin.Current);
        return true;
    }

    /// <summary>
    /// Reads a decimal value, reports values above the supported integer range without wrapping, and reports a
    /// token that is not a decimal number.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="value">The parsed value, saturated at <see cref="int.MaxValue"/>.</param>
    /// <param name="overflow">True if the decimal value exceeds <see cref="int.MaxValue"/>.</param>
    /// <param name="isValid">
    /// True if the token has at least one digit and ends at whitespace or at EOF. The byte after the digits is
    /// consumed in each case.
    /// </param>
    /// <returns>False if EOF was reached. The final digits are retained in <paramref name="value"/>.</returns>
    public static bool ReadDecimal(this BufferedReadStream stream, out int value, out bool overflow, out bool isValid)
    {
        value = 0;
        overflow = false;
        bool hasDigit = false;
        while (true)
        {
            int current = stream.ReadByte();
            if (current < 0)
            {
                isValid = hasDigit;
                return false;
            }

            int digit = current - 0x30;
            if ((uint)digit > 9)
            {
                // The plain Netpbm formats separate samples with whitespace. Any other byte makes the token not valid.
                // See Netpbm pgm(5), section "Plain PGM", and ppm(5), section "Plain PPM".
                isValid = hasDigit && current is 0x09 or 0x0a or 0x0d or 0x20;
                break;
            }

            hasDigit = true;

            // Keep consuming digits after overflow so the next sample starts at the correct byte.
            // Sample conversion can saturate this value. Header parsing rejects an overflowing field.
            if (value > (int.MaxValue - digit) / 10)
            {
                value = int.MaxValue;
                overflow = true;
            }
            else
            {
                value = (value * 10) + digit;
            }
        }

        return true;
    }
}
