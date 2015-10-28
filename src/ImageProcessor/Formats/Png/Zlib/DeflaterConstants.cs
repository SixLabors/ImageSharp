// <copyright file="DeflaterConstants.cs" company="James South">
// Copyright (c) James South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessor.Formats
{
    using System;

    /// <summary>
    /// This class contains constants used for deflation.
    /// </summary>
    public class DeflaterConstants
    {
        /// <summary>
        /// Written to Zip file to identify a stored block
        /// </summary>
        public const int StoredBlock = 0;

        /// <summary>
        /// Identifies static tree in Zip file
        /// </summary>
        public const int StaticTrees = 1;

        /// <summary>
        /// Identifies dynamic tree in Zip file
        /// </summary>
        public const int DynTrees = 2;

        /// <summary>
        /// Header flag indicating a preset dictionary for deflation
        /// </summary>
        public const int PresetDict = 0x20;

        /// <summary>
        /// Sets internal buffer sizes for Huffman encoding
        /// </summary>
        public const int DefaultMemLevel = 8;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int MaxMatch = 258;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int MinMatch = 3;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int MaxWbits = 15;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int Wsize = 1 << MaxWbits;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int Wmask = Wsize - 1;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int HashBits = DefaultMemLevel + 7;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int HashSize = 1 << HashBits;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int HashMask = HashSize - 1;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int HashShift = (HashBits + MinMatch - 1) / MinMatch;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int MinLookahead = MaxMatch + MinMatch + 1;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int MaxDist = Wsize - MinLookahead;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int PendingBufSize = 1 << (DefaultMemLevel + 8);

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int Deflatestored = 0;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int Deflatefast = 1;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public const int Deflateslow = 2;

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public static int MaxBlockSize => Math.Min(65535, PendingBufSize - 5);

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public static int[] GoodLength => new[] { 0, 4, 4, 4, 4, 8, 8, 8, 32, 32 };

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public static int[] MaxLazy => new[] { 0, 4, 5, 6, 4, 16, 16, 32, 128, 258 };

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public static int[] NiceLength => new[] { 0, 8, 16, 32, 16, 32, 128, 128, 258, 258 };

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public static int[] MaxChain => new[] { 0, 4, 8, 32, 16, 32, 128, 256, 1024, 4096 };

        /// <summary>
        /// Internal compression engine constant
        /// </summary>
        public static int[] ComprFunc => new[] { 0, 1, 1, 1, 1, 2, 2, 2, 2, 2 };
    }
}
