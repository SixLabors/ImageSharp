// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

#if !NETCOREAPP3_1
namespace SixLabors.ImageSharp.Common.Helpers
{
    /// <summary>
    /// Polyfill for System.Numerics.BitOperations class, introduced in .NET Core 3.0.
    /// <see href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/Numerics/BitOperations.cs"/>
    /// </summary>
    public static class BitOperations
    {
        /// <summary>
        /// Rotates the specified value left by the specified number of bits.
        /// </summary>
        /// <param name="value">The value to rotate.</param>
        /// <param name="offset">The number of bits to roate with.</param>
        /// <returns>The rotated value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint RotateLeft(uint value, int offset)
            => (value << offset) | (value >> (32 - offset));
    }
}
#endif
