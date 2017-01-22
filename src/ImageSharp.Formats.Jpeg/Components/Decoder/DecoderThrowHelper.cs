// <copyright file="DecoderThrowHelper.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats.Jpg
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Encapsulates exception thrower methods for the Jpeg Encoder
    /// </summary>
    internal static class DecoderThrowHelper
    {
        /// <summary>
        /// Throws an exception that belongs to the given <see cref="DecoderErrorCode"/>
        /// </summary>
        /// <param name="errorCode">The <see cref="DecoderErrorCode"/></param>
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ThrowExceptionForErrorCode(this DecoderErrorCode errorCode)
        {
            switch (errorCode)
            {
                case DecoderErrorCode.NoError:
                    throw new ArgumentException("ThrowExceptionForErrorCode() called with NoError!", nameof(errorCode));
                case DecoderErrorCode.MissingFF00:
                    throw new MissingFF00Exception();
                case DecoderErrorCode.UnexpectedEndOfFile:
                    throw new EOFException();
                default:
                    throw new ArgumentOutOfRangeException(nameof(errorCode), errorCode, null);
            }
        }

        /// <summary>
        /// Throws an exception if the given <see cref="DecoderErrorCode"/> defines an error.
        /// </summary>
        /// <param name="errorCode">The <see cref="DecoderErrorCode"/></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureNoError(this DecoderErrorCode errorCode)
        {
            if (errorCode != DecoderErrorCode.NoError)
            {
                ThrowExceptionForErrorCode(errorCode);
            }
        }
    }
}