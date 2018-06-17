// Copyright (c) Six Labors and contributors.
// Licensed under the Apache License, Version 2.0.

using System;
using System.Buffers;

namespace SixLabors.Memory
{
    /// <summary>
    /// Represents a contigous memory buffer of value-type items.
    /// Depending on it's implementation, an <see cref="IBuffer{T}"/> can (1) OWN or (2) CONSUME the <see cref="Memory{T}"/> instance it wraps.
    /// For a deeper understanding of the owner/consumer model, read the following docs: <br/>
    /// https://gist.github.com/GrabYourPitchforks/4c3e1935fd4d9fa2831dbfcab35dffc6
    /// TODO: We need more SOC here! For owned buffers we should use <see cref="IMemoryOwner{T}"/>.
    /// For the consumption case we should not use buffers at all. We need to refactor Buffer2D{T} for this.
    /// </summary>
    /// <typeparam name="T">The value type</typeparam>
    internal interface IBuffer<T> : IDisposable
        where T : struct
    {
        /// <summary>
        /// Gets the <see cref="Memory{T}"/> ownerd/consumed by this buffer.
        /// </summary>
        Memory<T> Memory { get; }

        /// <summary>
        /// Gets a value indicating whether this instance is owning the <see cref="Memory"/>.
        /// </summary>
        bool IsMemoryOwner { get; }

        /// <summary>
        /// Gets the span to the memory "promised" by this buffer when it's OWNED (1).
        /// Gets `this.Memory.Span` when the buffer CONSUMED (2).
        /// </summary>
        /// <returns>The <see cref="Span{T}"/></returns>
        Span<T> GetSpan();
    }
}