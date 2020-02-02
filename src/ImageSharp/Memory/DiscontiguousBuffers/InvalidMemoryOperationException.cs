using System;

namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Exception thrown on invalid memory (allocation) requests.
    /// </summary>
    public class InvalidMemoryOperationException : InvalidOperationException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidMemoryOperationException"/> class.
        /// </summary>
        /// <param name="message">The exception message text.</param>
        public InvalidMemoryOperationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="InvalidMemoryOperationException"/> class.
        /// </summary>
        public InvalidMemoryOperationException()
        {
        }
    }
}
