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
    /// Read a decimal text value and signal if EOF has been reached.
    /// </summary>
    /// <param name="stream">The buffered read stream.</param>
    /// <param name="value">The read value.</param>
    /// <returns><see langword="false"/> if EOF has been reached while reading the stream; <see langword="true"/> otherwise.</returns>
    /// <remarks>
    /// A 'false' return value doesn't mean that the parsing has been failed, since it's possible to reach EOF while reading the last decimal in the file.
    /// It's up to the call site to handle such a situation.
    /// </remarks>
    public static bool ReadDecimal(this BufferedReadStream stream, out int value)
        => stream.ReadDecimal(out value, out _);

    /// <summary>
    /// Reads a decimal value and reports values above the supported integer range without wrapping.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <param name="value">The parsed value, saturated at <see cref="int.MaxValue"/>.</param>
    /// <param name="overflow">True if the decimal value exceeds <see cref="int.MaxValue"/>.</param>
    /// <returns>False if EOF was reached. The final digits are retained in <paramref name="value"/>.</returns>
    public static bool ReadDecimal(this BufferedReadStream stream, out int value, out bool overflow)
    {
        value = 0;
        overflow = false;
        while (true)
        {
            int current = stream.ReadByte();
            if (current < 0)
            {
                return false;
            }

            current -= 0x30;
            if ((uint)current > 9)
            {
                break;
            }

            // Keep consuming digits after overflow so the next sample starts at the correct byte.
            // Sample conversion can saturate this value. Header parsing rejects an overflowing field.
            if (value > (int.MaxValue - current) / 10)
            {
                value = int.MaxValue;
                overflow = true;
            }
            else
            {
                value = (value * 10) + current;
            }
        }

        return true;
    }
}
