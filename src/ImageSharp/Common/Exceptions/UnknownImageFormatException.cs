// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

namespace SixLabors.ImageSharp
{
    /// <summary>
    /// The exception that is thrown when the library tries to load
    /// an image which has an unknown format.
    /// </summary>
    public sealed class UnknownImageFormatException : ImageFormatException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnknownImageFormatException"/> class with the name of the
        /// parameter that causes this exception.
        /// </summary>
        /// <param name="errorMessage">The error message that explains the reason for this exception.</param>
        public UnknownImageFormatException(string errorMessage)
            : base(errorMessage)
        {
        }
    }
}
