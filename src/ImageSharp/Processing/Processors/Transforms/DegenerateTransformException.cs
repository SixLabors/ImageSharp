// Copyright (c) Six Labors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace SixLabors.ImageSharp.Processing.Processors.Transforms
{
    /// <summary>
    /// Represents an error that occurs during a transform operation.
    /// </summary>
    public sealed class DegenerateTransformException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DegenerateTransformException"/> class.
        /// </summary>
        public DegenerateTransformException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DegenerateTransformException" /> class
        /// with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public DegenerateTransformException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DegenerateTransformException" /> class
        /// with a specified error message and a reference to the inner exception that is
        /// the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception, or a null reference (<see langword="Nothing" /> in Visual Basic) if no inner exception is specified.</param>
        public DegenerateTransformException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
