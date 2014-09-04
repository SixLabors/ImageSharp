// --------------------------------------------------------------------------------------------------------------------
// <copyright file="ImageProcessingException.cs" company="James South">
//   Copyright (c) James South.
//   Licensed under the Apache License, Version 2.0.
// </copyright>
// <summary>
//   The exception that is thrown when processing an image has failed.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace ImageProcessor.Common.Exceptions
{
    using System;

    /// <summary>
    /// The exception that is thrown when processing an image has failed.
    /// </summary>
    [Serializable]
    public sealed class ImageProcessingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessingException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public ImageProcessingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessingException"/> class.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic) if no inner exception is specified.</param>
        public ImageProcessingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
