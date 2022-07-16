// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.IO;
using SixLabors.ImageSharp.IO;

namespace SixLabors.ImageSharp.Formats.Pbm
{
    /// <summary>
    /// Extensions methods for <see cref="BufferedReadStream"/>.
    /// </summary>
    internal static class BufferedReadStreamExtensions
    {
        /// <summary>
        /// Skip over any whitespace or any comments.
        /// </summary>
        public static void SkipWhitespaceAndComments(this BufferedReadStream stream)
        {
            bool isWhitespace;
            do
            {
                int val = stream.ReadByte();

                // Comments start with '#' and end at the next new-line.
                if (val == 0x23)
                {
                    int innerValue;
                    do
                    {
                        innerValue = stream.ReadByte();
                    }
                    while (innerValue != 0x0a);

                    // Continue searching for whitespace.
                    val = innerValue;
                }

                isWhitespace = val is 0x09 or 0x0a or 0x0d or 0x20;
            }
            while (isWhitespace);
            stream.Seek(-1, SeekOrigin.Current);
        }

        /// <summary>
        /// Read a decimal text value.
        /// </summary>
        /// <returns>The integer value of the decimal.</returns>
        public static int ReadDecimal(this BufferedReadStream stream)
        {
            int value = 0;
            while (true)
            {
                int current = stream.ReadByte() - 0x30;
                if ((uint)current > 9)
                {
                    break;
                }

                value = (value * 10) + current;
            }

            return value;
        }
    }
}
