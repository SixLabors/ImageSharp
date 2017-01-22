// <copyright file="DecoderErrorCode.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageSharp.Formats
{
    /// <summary>
    /// Represents "recoverable" decoder errors.
    /// </summary>
    internal enum DecoderErrorCode
    {
        /// <summary>
        /// NoError
        /// </summary>
        NoError,

        /// <summary>
        /// MissingFF00
        /// </summary>
        // ReSharper disable once InconsistentNaming
        MissingFF00,

        /// <summary>
        /// End of stream reached unexpectedly
        /// </summary>
        UnexpectedEndOfStream
    }
}