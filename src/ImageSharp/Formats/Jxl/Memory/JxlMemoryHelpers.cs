// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Runtime.InteropServices;

namespace SixLabors.ImageSharp.Formats.Jxl.Memory;

/// <summary>
/// Memory management utilities.
/// </summary>
internal static class JxlMemoryHelpers
{
    /// <summary>
    /// Reinterprets a <see cref="Memory{T}"/>. This method is highly similar to
    /// <see cref="MemoryMarshal.Cast{TFrom, TTo}(Span{TFrom})"/>, which is suited
    /// for <see cref="Span{T}"/> only.
    /// </summary>
    /// <typeparam name="TSourceType">Type of the input memory.</typeparam>
    /// <typeparam name="TOutputType">Type of the output memory.</typeparam>
    /// <param name="memory">Input memory that will be reinterpreted.</param>
    /// <returns>The reinterpreted memory.</returns>
    public static Memory<TOutputType> CastMemory<TSourceType, TOutputType>(Memory<TSourceType> memory)
        where TSourceType : unmanaged
        where TOutputType : unmanaged
    {
        CastingMemoryManager<TSourceType, TOutputType> memoryManager = new(memory);
        return memoryManager.Memory;
    }

    /// <summary>
    /// Creates a <see cref="Memory{T}"/> wrapper over the input <see cref="List{T}"/>
    /// data. While <see cref="CollectionsMarshal.AsSpan{T}(List{T}?)"/> returns a
    /// <see cref="Span{T}"/> wrapper over list data, this method returns a <see cref="Memory{T}"/>
    /// wrapper over list data.
    /// </summary>
    /// <typeparam name="T">The type of the input list.</typeparam>
    /// <param name="list">List that will have a Memory wrapper.</param>
    /// <returns>
    /// A <see cref="Memory{T}"/>. Writing values of this memory will directly affect
    /// values for the underlying List.
    /// </returns>
    /// <remarks>
    /// ⚠️ Using the Memory wrapper after the list's items have been removed or added
    /// is unsafe.
    /// </remarks>
    public static Memory<T> MemoryFromList<T>(List<T> list)
        where T : unmanaged
    {
        ListFactoryMemoryManager<T> lf = new(list);
        return lf.Memory;
    }

    /// <summary>
    /// Returns an empty <see cref="IMemoryOwner{T}"/>.
    /// </summary>
    /// <typeparam name="T">Kind of the memory owner.</typeparam>
    /// <returns>A memory owner whose <see cref="IMemoryOwner{T}.Memory"/> is empty.</returns>
    public static IMemoryOwner<T> GetEmptyMemoryOwner<T>() => EmptyMemoryOwner<T>.Instance;

    /// <summary>
    /// Memory manager used to reinterpret a <see cref="Memory{T}"/> from one type
    /// to another. Similar to <see cref="MemoryMarshal.Cast{TFrom, TTo}(Span{TFrom})"/>
    /// but suited for <see cref="Memory{T}"/>.
    /// </summary>
    /// <typeparam name="TSourceType">Source type of the input memory.</typeparam>
    /// <typeparam name="TOutputType">Destination type of the output memory.</typeparam>
    /// <param name="memory">
    /// Input memory of type <typeparamref name="TSourceType"/>, to be reinterpreted
    /// to <typeparamref name="TOutputType"/>.
    /// </param>
    private sealed class CastingMemoryManager<TSourceType, TOutputType>(Memory<TSourceType> memory)
        : MemoryManager<TOutputType>
        where TSourceType : unmanaged
        where TOutputType : unmanaged
    {
        /// <inheritdoc />
        public override Span<TOutputType> GetSpan() => MemoryMarshal.Cast<TSourceType, TOutputType>(memory.Span);

        /// <inheritdoc />
        public override MemoryHandle Pin(int elementIndex = 0) => default;

        /// <inheritdoc />
        public override void Unpin()
        {
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
        }
    }

    /// <summary>
    /// Memory manager used to create a <see cref="Memory{T}"/> wrapper over a
    /// <see cref="List{T}"/> data. See <see cref="MemoryFromList{T}(List{T})"/>.
    /// </summary>
    /// <typeparam name="T">The kind of the list and the memory.</typeparam>
    /// <param name="list">The input list whose <see cref="Memory{T}"/> wrapper will be created.</param>
    private sealed class ListFactoryMemoryManager<T>(List<T> list)
        : MemoryManager<T>
        where T : unmanaged
    {
        /// <inheritdoc />
        public override Span<T> GetSpan() => CollectionsMarshal.AsSpan(list);

        /// <inheritdoc />
        public override MemoryHandle Pin(int elementIndex = 0) => default;

        /// <inheritdoc />
        public override void Unpin()
        {
        }

        /// <inheritdoc />
        protected override void Dispose(bool disposing)
        {
        }
    }

    /// <summary>
    /// A <see cref="IMemoryOwner{T}"/> that wraps over empty memory.
    /// </summary>
    /// <typeparam name="T">Type of the memory owner.</typeparam>
    private sealed class EmptyMemoryOwner<T> : IMemoryOwner<T>
    {
        /// <summary>
        /// Instance of this empty memory owner.
        /// </summary>
        public static readonly EmptyMemoryOwner<T> Instance = new();

        /// <inheritdoc />
        public Memory<T> Memory => Memory<T>.Empty;

        /// <inheritdoc />
        public void Dispose()
        {
        }
    }
}
