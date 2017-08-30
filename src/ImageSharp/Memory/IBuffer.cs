using System;

namespace SixLabors.ImageSharp.Memory
{
    /// <inheritdoc />
    /// <summary>
    /// Represents a contigous memory buffer of value-type items "promising" a <see cref="T:System.Span`1" />
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    internal interface IBuffer<T> : IDisposable
        where T : struct
    {
        /// <summary>
        /// Gets the span to the memory "promised" by this buffer
        /// </summary>
        Span<T> Span { get; }
    }
}