// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp.Formats.Jpeg.GolangPort.Components.Decoder
{
    /// <summary>
    /// Represents "recoverable" decoder errors.
    /// </summary>
    internal enum OrigDecoderErrorCode
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