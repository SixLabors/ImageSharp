// <copyright file="WuArrayPool.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Quantizers
{
    using System.Buffers;

    /// <summary>
    /// Provides array pooling for the <see cref="WuQuantizer{TPixel}"/>.
    /// This is a separate class so that the pools can be shared accross multiple generic quantizer instaces.
    /// </summary>
    internal static class WuArrayPool
    {
        /// <summary>
        /// The long array pool.
        /// </summary>
        public static readonly ArrayPool<long> LongPool = ArrayPool<long>.Create(TableLength, 25);

        /// <summary>
        /// The float array pool.
        /// </summary>
        public static readonly ArrayPool<float> FloatPool = ArrayPool<float>.Create(TableLength, 5);

        /// <summary>
        /// The byte array pool.
        /// </summary>
        public static readonly ArrayPool<byte> BytePool = ArrayPool<byte>.Create(TableLength, 5);

        /// <summary>
        /// The table length. Matches the calculated value in <see cref="WuQuantizer{TPixel}"/>
        /// </summary>
        private const int TableLength = 2471625;
    }
}