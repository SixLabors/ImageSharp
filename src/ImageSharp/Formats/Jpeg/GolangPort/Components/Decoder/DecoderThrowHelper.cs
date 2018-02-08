// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Runtime.CompilerServices;

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// Encapsulates exception thrower methods for the Jpeg Encoder
    /// </summary>
    internal static class DecoderThrowHelper
    {
        /// <summary>
        /// Throws an exception that belongs to the given <see cref="OrigDecoderErrorCode"/>
        /// </summary>
        /// <param name="errorCode">The <see cref="OrigDecoderErrorCode"/></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowExceptionForErrorCode(this OrigDecoderErrorCode errorCode)
        {
            switch (errorCode)
            {
                case OrigDecoderErrorCode.NoError:
                    throw new ArgumentException("ThrowExceptionForErrorCode() called with NoError!", nameof(errorCode));
                case OrigDecoderErrorCode.MissingFF00:
                    throw new MissingFF00Exception();
                case OrigDecoderErrorCode.UnexpectedEndOfStream:
                    throw new EOFException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, null);
            }
        }

        /// <summary>
        /// Throws an exception if the given <see cref="OrigDecoderErrorCode"/> defines an error.
        /// </summary>
        /// <param name="errorCode">The <see cref="OrigDecoderErrorCode"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureNoError(this OrigDecoderErrorCode errorCode)
        {
            if (errorCode != OrigDecoderErrorCode.NoError)
            {
                ThrowExceptionForErrorCode(errorCode);
            }
        }

        /// <summary>
        /// Throws an exception if the given <see cref="OrigDecoderErrorCode"/> is <see cref="OrigDecoderErrorCode.UnexpectedEndOfStream"/>.
        /// </summary>
        /// <param name="errorCode">The <see cref="OrigDecoderErrorCode"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureNoEOF(this OrigDecoderErrorCode errorCode)
        {
            if (errorCode == OrigDecoderErrorCode.UnexpectedEndOfStream)
            {
                errorCode.ThrowExceptionForErrorCode();
            }
        }

        /// <summary>
        /// Encapsulates methods throwing different flavours of <see cref="ImageFormatException"/>-s.
        /// </summary>
        public static class ThrowImageFormatException
        {
            /// <summary>
            /// Throws "Fill called when unread bytes exist".
            /// </summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void FillCalledWhenUnreadBytesExist()
            {
                throw new ImageFormatException("Fill called when unread bytes exist!");
            }

            /// <summary>
            /// Throws "Bad Huffman code".
            /// </summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void BadHuffmanCode()
            {
                throw new ImageFormatException("Bad Huffman code!");
            }

            /// <summary>
            /// Throws "Uninitialized Huffman table".
            /// </summary>
            [MethodImpl(MethodImplOptions.NoInlining)]
            public static void UninitializedHuffmanTable()
            {
                throw new ImageFormatException("Uninitialized Huffman table");
            }
        }
    }
}