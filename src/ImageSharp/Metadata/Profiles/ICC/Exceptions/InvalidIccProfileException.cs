// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Metadata.Profiles.Icc
{
    /// <summary>
    /// Represents an error that happened while reading or writing a corrupt/invalid ICC profile
    /// </summary>
    public class InvalidIccProfileException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidIccProfileException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        public InvalidIccProfileException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidIccProfileException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error</param>
        /// <param name="inner">The exception that is the cause of the current exception, or a null reference
        /// (Nothing in Visual Basic) if no inner exception is specified</param>
        public InvalidIccProfileException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}