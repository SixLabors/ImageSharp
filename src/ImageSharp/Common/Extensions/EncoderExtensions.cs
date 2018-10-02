// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

#if !NETCOREAPP2_1
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
#if NETSTANDARD1_1
            return encoding.GetString(buffer.ToArray());
#else
            fixed (byte* bytes = buffer)
            {
                return encoding.GetString(bytes, buffer.Length);
            }
#endif
        }
    }
}
#endif