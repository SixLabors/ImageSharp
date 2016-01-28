// <copyright file="ImageProcessingException.cs" company="James Jackson-South">
// Copyright (c) James Jackson-South and contributors.
// Licensed under the Apache License, Version 2.0.
// </copyright>

namespace ImageProcessorCore
{
    using System;

    /// <summary>
    /// The exception that is thrown when an error occurs when applying a process to an image.
    /// </summary>
    public class ImageProcessingException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessingException"/> class.
        /// </summary>
        public ImageProcessingException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessingException"/> class with the name of the
        /// parameter that causes this exception.
        /// </summary>
        /// <param name="errorMessage">The error message that explains the reason for this exception.</param>
        public ImageProcessingException(string errorMessage)
            : base(errorMessage)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImageProcessingException"/> class with a specified
        /// error message and the exception that is the cause of this exception.
        /// </summary>
        /// <param name="errorMessage">The error message that explains the reason for this exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (Nothing in Visual Basic)
        /// if no inner exception is specified.</param>
        public ImageProcessingException(string errorMessage, Exception innerException)
            : base(errorMessage, innerException)
        {
        }
    }
}
