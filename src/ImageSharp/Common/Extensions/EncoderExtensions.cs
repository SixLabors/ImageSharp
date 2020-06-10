// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

#if !SUPPORTS_ENCODING_STRING
using System;
using System.Text;

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// Extension methods for the <see cref="Encoder"/> type.
    /// </summary>
    internal static unsafe class EncoderExtensions
    {
        /// <summary>
        /// Gets a string from the provided buffer data.
        /// </summary>
        /// <param name="encoding">The encoding.</param>
        /// <param name="buffer">The buffer.</param>
        /// <returns>The string.</returns>
        public static string GetString(this Encoding encoding, ReadOnlySpan<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return string.Empty;
            }

            fixed (byte* bytes = buffer)
            {
                return encoding.GetString(bytes, buffer.Length);
            }
        }
    }
}
#endif
