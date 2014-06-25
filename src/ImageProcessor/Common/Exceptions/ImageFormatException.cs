// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageFormatException.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The exception that is thrown when loading the supported image format types has failed.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Common.Exceptions
{
    using System;

    /// <summary>
    /// The exception that is thrown when loading the supported image format types has failed.
    /// </summary>
    [Serializable]
    public sealed class ImageFormatException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFormatException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ImageFormatException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageFormatException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public ImageFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
