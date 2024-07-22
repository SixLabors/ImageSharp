// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using SixLabors.ImageSharp.Memory;
using SixLabors.ImageSharp.Metadata;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp;

/// <content>
/// Adds static methods allowing wrapping an existing memory area as an image.
/// </content>
public abstract partial class Image
{
    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: using this method does not transfer the ownership of the underlying buffer of the input <see cref="Memory{T}"/>
    /// to the new <see cref="Image{TPixel}"/> instance. This means that consumers of this method must ensure that the input buffer
    /// is either self-contained, (for example, a <see cref="Memory{T}"/> instance wrapping a new array that was
    /// created), or that the owning object is not disposed until the returned <see cref="Image{TPixel}"/> is disposed.
    /// </para>
    /// <para>
    /// If the input <see cref="Memory{T}"/> instance is one retrieved from an <see cref="IMemoryOwner{T}"/> instance
    /// rented from a memory pool (such as <see cref="MemoryPool{T}"/>), and that owning instance is disposed while the image is still
    /// in use, this will lead to undefined behavior and possibly runtime crashes (as the same buffer might then be modified by other
    /// consumers while the returned image is still working on it). Make sure to control the lifetime of the input buffers appropriately.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="pixelMemory">The pixel memory.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The metadata is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        Memory<TPixel> pixelMemory,
        int width,
        int height,
        ImageMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(metadata, nameof(metadata));
        Guard.IsTrue(pixelMemory.Length >= (long)width * height, nameof(pixelMemory), "The length of the input memory is less than the specified image size");

        MemoryGroup<TPixel> memorySource = MemoryGroup<TPixel>.Wrap(pixelMemory);
        return new Image<TPixel>(configuration, memorySource, width, height, metadata);
    }

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: using this method does not transfer the ownership of the underlying buffer of the input <see cref="Memory{T}"/>
    /// to the new <see cref="Image{TPixel}"/> instance. This means that consumers of this method must ensure that the input buffer
    /// is either self-contained, (for example, a <see cref="Memory{T}"/> instance wrapping a new array that was
    /// created), or that the owning object is not disposed until the returned <see cref="Image{TPixel}"/> is disposed.
    /// </para>
    /// <para>
    /// If the input <see cref="Memory{T}"/> instance is one retrieved from an <see cref="IMemoryOwner{T}"/> instance
    /// rented from a memory pool (such as <see cref="MemoryPool{T}"/>), and that owning instance is disposed while the image is still
    /// in use, this will lead to undefined behavior and possibly runtime crashes (as the same buffer might then be modified by other
    /// consumers while the returned image is still working on it). Make sure to control the lifetime of the input buffers appropriately.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="pixelMemory">The pixel memory.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        Memory<TPixel> pixelMemory,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory(configuration, pixelMemory, width, height, new ImageMetadata());

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: using this method does not transfer the ownership of the underlying buffer of the input <see cref="Memory{T}"/>
    /// to the new <see cref="Image{TPixel}"/> instance. This means that consumers of this method must ensure that the input buffer
    /// is either self-contained, (for example, a <see cref="Memory{T}"/> instance wrapping a new array that was
    /// created), or that the owning object is not disposed until the returned <see cref="Image{TPixel}"/> is disposed.
    /// </para>
    /// <para>
    /// If the input <see cref="Memory{T}"/> instance is one retrieved from an <see cref="IMemoryOwner{T}"/> instance
    /// rented from a memory pool (such as <see cref="MemoryPool{T}"/>), and that owning instance is disposed while the image is still
    /// in use, this will lead to undefined behavior and possibly runtime crashes (as the same buffer might then be modified by other
    /// consumers while the returned image is still working on it). Make sure to control the lifetime of the input buffers appropriately.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="pixelMemory">The pixel memory.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Memory<TPixel> pixelMemory,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory(Configuration.Default, pixelMemory, width, height);

    /// <summary>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels,
    /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
    /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
    /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
    /// It will be disposed together with the result image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <param name="metadata">The <see cref="ImageMetadata"/></param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The metadata is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        IMemoryOwner<TPixel> pixelMemoryOwner,
        int width,
        int height,
        ImageMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(metadata, nameof(metadata));
        Guard.IsTrue(pixelMemoryOwner.Memory.Length >= (long)width * height, nameof(pixelMemoryOwner), "The length of the input memory is less than the specified image size");

        MemoryGroup<TPixel> memorySource = MemoryGroup<TPixel>.Wrap(pixelMemoryOwner);
        return new Image<TPixel>(configuration, memorySource, width, height, metadata);
    }

    /// <summary>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels,
    /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
    /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
    /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
    /// It will be disposed together with the result image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        IMemoryOwner<TPixel> pixelMemoryOwner,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory(configuration, pixelMemoryOwner, width, height, new ImageMetadata());

    /// <summary>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels,
    /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
    /// The ownership of the <paramref name="pixelMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
    /// meaning that the caller is not allowed to dispose <paramref name="pixelMemoryOwner"/>.
    /// It will be disposed together with the result image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="pixelMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        IMemoryOwner<TPixel> pixelMemoryOwner,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory(Configuration.Default, pixelMemoryOwner, width, height);

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: using this method does not transfer the ownership of the underlying buffer of the input <see cref="Memory{T}"/>
    /// to the new <see cref="Image{TPixel}"/> instance. This means that consumers of this method must ensure that the input buffer
    /// is either self-contained, (for example, a <see cref="Memory{T}"/> instance wrapping a new array that was
    /// created), or that the owning object is not disposed until the returned <see cref="Image{TPixel}"/> is disposed.
    /// </para>
    /// <para>
    /// If the input <see cref="Memory{T}"/> instance is one retrieved from an <see cref="IMemoryOwner{T}"/> instance
    /// rented from a memory pool (such as <see cref="MemoryPool{T}"/>), and that owning instance is disposed while the image is still
    /// in use, this will lead to undefined behavior and possibly runtime crashes (as the same buffer might then be modified by other
    /// consumers while the returned image is still working on it). Make sure to control the lifetime of the input buffers appropriately.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="byteMemory">The byte memory representing the pixel data.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The metadata is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        Memory<byte> byteMemory,
        int width,
        int height,
        ImageMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(metadata, nameof(metadata));

        ByteMemoryManager<TPixel> memoryManager = new(byteMemory);

        Guard.IsTrue(memoryManager.Memory.Length >= (long)width * height, nameof(byteMemory), "The length of the input memory is less than the specified image size");

        MemoryGroup<TPixel> memorySource = MemoryGroup<TPixel>.Wrap(memoryManager.Memory);
        return new Image<TPixel>(configuration, memorySource, width, height, metadata);
    }

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: using this method does not transfer the ownership of the underlying buffer of the input <see cref="Memory{T}"/>
    /// to the new <see cref="Image{TPixel}"/> instance. This means that consumers of this method must ensure that the input buffer
    /// is either self-contained, (for example, a <see cref="Memory{T}"/> instance wrapping a new array that was
    /// created), or that the owning object is not disposed until the returned <see cref="Image{TPixel}"/> is disposed.
    /// </para>
    /// <para>
    /// If the input <see cref="Memory{T}"/> instance is one retrieved from an <see cref="IMemoryOwner{T}"/> instance
    /// rented from a memory pool (such as <see cref="MemoryPool{T}"/>), and that owning instance is disposed while the image is still
    /// in use, this will lead to undefined behavior and possibly runtime crashes (as the same buffer might then be modified by other
    /// consumers while the returned image is still working on it). Make sure to control the lifetime of the input buffers appropriately.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="byteMemory">The byte memory representing the pixel data.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        Memory<byte> byteMemory,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory<TPixel>(configuration, byteMemory, width, height, new ImageMetadata());

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: using this method does not transfer the ownership of the underlying buffer of the input <see cref="Memory{T}"/>
    /// to the new <see cref="Image{TPixel}"/> instance. This means that consumers of this method must ensure that the input buffer
    /// is either self-contained, (for example, a <see cref="Memory{T}"/> instance wrapping a new array that was
    /// created), or that the owning object is not disposed until the returned <see cref="Image{TPixel}"/> is disposed.
    /// </para>
    /// <para>
    /// If the input <see cref="Memory{T}"/> instance is one retrieved from an <see cref="IMemoryOwner{T}"/> instance
    /// rented from a memory pool (such as <see cref="MemoryPool{T}"/>), and that owning instance is disposed while the image is still
    /// in use, this will lead to undefined behavior and possibly runtime crashes (as the same buffer might then be modified by other
    /// consumers while the returned image is still working on it). Make sure to control the lifetime of the input buffers appropriately.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="byteMemory">The byte memory representing the pixel data.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Memory<byte> byteMemory,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory<TPixel>(Configuration.Default, byteMemory, width, height);

    /// <summary>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels,
    /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
    /// The ownership of the <paramref name="byteMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
    /// meaning that the caller is not allowed to dispose <paramref name="byteMemoryOwner"/>.
    /// It will be disposed together with the result image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="byteMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <param name="metadata">The <see cref="ImageMetadata"/></param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The metadata is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        IMemoryOwner<byte> byteMemoryOwner,
        int width,
        int height,
        ImageMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(metadata, nameof(metadata));

        ByteMemoryOwner<TPixel> pixelMemoryOwner = new(byteMemoryOwner);

        Guard.IsTrue(pixelMemoryOwner.Memory.Length >= (long)width * height, nameof(pixelMemoryOwner), "The length of the input memory is less than the specified image size");

        MemoryGroup<TPixel> memorySource = MemoryGroup<TPixel>.Wrap(pixelMemoryOwner);
        return new Image<TPixel>(configuration, memorySource, width, height, metadata);
    }

    /// <summary>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels,
    /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
    /// The ownership of the <paramref name="byteMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
    /// meaning that the caller is not allowed to dispose <paramref name="byteMemoryOwner"/>.
    /// It will be disposed together with the result image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="byteMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        IMemoryOwner<byte> byteMemoryOwner,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory<TPixel>(configuration, byteMemoryOwner, width, height, new ImageMetadata());

    /// <summary>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels,
    /// allowing to view/manipulate it as an <see cref="Image{TPixel}"/> instance.
    /// The ownership of the <paramref name="byteMemoryOwner"/> is being transferred to the new <see cref="Image{TPixel}"/> instance,
    /// meaning that the caller is not allowed to dispose <paramref name="byteMemoryOwner"/>.
    /// It will be disposed together with the result image.
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="byteMemoryOwner">The <see cref="IMemoryOwner{T}"/> that is being transferred to the image.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static Image<TPixel> WrapMemory<TPixel>(
        IMemoryOwner<byte> byteMemoryOwner,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory<TPixel>(Configuration.Default, byteMemoryOwner, width, height);

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: this method relies on callers to carefully manage the target memory area being referenced by the
    /// pointer and that the lifetime of such a memory area is at least equal to that of the returned
    /// <see cref="Image{TPixel}"/> instance. For example, if the input pointer references an unmanaged memory area,
    /// callers must ensure that the memory area is not freed as long as the returned <see cref="Image{TPixel}"/> is
    /// in use and not disposed. The same applies if the input memory area points to a pinned managed object, as callers
    /// must ensure that objects will remain pinned as long as the <see cref="Image{TPixel}"/> instance is in use.
    /// Failing to do so constitutes undefined behavior and will likely lead to memory corruption and runtime crashes.
    /// </para>
    /// <para>
    /// Note also that if you have a <see cref="Memory{T}"/> or an array (which can be cast to <see cref="Memory{T}"/>) of
    /// either <see cref="byte"/> or <typeparamref name="TPixel"/> values, it is highly recommended to use one of the other
    /// available overloads of this method instead (such as <see cref="WrapMemory{TPixel}(Configuration, Memory{byte}, int, int)"/>
    /// or <see cref="WrapMemory{TPixel}(Configuration, Memory{TPixel}, int, int)"/>, to make the resulting code less error
    /// prone and avoid having to pin the underlying memory buffer in use. This method is primarily meant to be used when
    /// doing interop or working with buffers that are located in unmanaged memory.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="pointer">The pointer to the target memory buffer to wrap.</param>
    /// <param name="bufferSizeInBytes">The byte length of the memory allocated.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <param name="metadata">The <see cref="ImageMetadata"/>.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <exception cref="ArgumentNullException">The metadata is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance</returns>
    public static unsafe Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        void* pointer,
        int bufferSizeInBytes,
        int width,
        int height,
        ImageMetadata metadata)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Guard.IsFalse(pointer == null, nameof(pointer), "Pointer must be not null");
        Guard.NotNull(configuration, nameof(configuration));
        Guard.NotNull(metadata, nameof(metadata));
        Guard.MustBeLessThanOrEqualTo(height * (long)width, int.MaxValue, "Total amount of pixels exceeds int.MaxValue");

        UnmanagedMemoryManager<TPixel> memoryManager = new(pointer, width * height);

        Guard.MustBeGreaterThanOrEqualTo(bufferSizeInBytes / sizeof(TPixel), memoryManager.Memory.Span.Length, nameof(bufferSizeInBytes));

        MemoryGroup<TPixel> memorySource = MemoryGroup<TPixel>.Wrap(memoryManager.Memory);
        return new Image<TPixel>(configuration, memorySource, width, height, metadata);
    }

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: this method relies on callers to carefully manage the target memory area being referenced by the
    /// pointer and that the lifetime of such a memory area is at least equal to that of the returned
    /// <see cref="Image{TPixel}"/> instance. For example, if the input pointer references an unmanaged memory area,
    /// callers must ensure that the memory area is not freed as long as the returned <see cref="Image{TPixel}"/> is
    /// in use and not disposed. The same applies if the input memory area points to a pinned managed object, as callers
    /// must ensure that objects will remain pinned as long as the <see cref="Image{TPixel}"/> instance is in use.
    /// Failing to do so constitutes undefined behavior and will likely lead to memory corruption and runtime crashes.
    /// </para>
    /// <para>
    /// Note also that if you have a <see cref="Memory{T}"/> or an array (which can be cast to <see cref="Memory{T}"/>) of
    /// either <see cref="byte"/> or <typeparamref name="TPixel"/> values, it is highly recommended to use one of the other
    /// available overloads of this method instead (such as <see cref="WrapMemory{TPixel}(Configuration, Memory{byte}, int, int)"/>
    /// or <see cref="WrapMemory{TPixel}(Configuration, Memory{TPixel}, int, int)"/>, to make the resulting code less error
    /// prone and avoid having to pin the underlying memory buffer in use. This method is primarily meant to be used when
    /// doing interop or working with buffers that are located in unmanaged memory.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type</typeparam>
    /// <param name="configuration">The <see cref="Configuration"/></param>
    /// <param name="pointer">The pointer to the target memory buffer to wrap.</param>
    /// <param name="bufferSizeInBytes">The byte length of the memory allocated.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <exception cref="ArgumentNullException">The configuration is null.</exception>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static unsafe Image<TPixel> WrapMemory<TPixel>(
        Configuration configuration,
        void* pointer,
        int bufferSizeInBytes,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory<TPixel>(configuration, pointer, bufferSizeInBytes, width, height, new ImageMetadata());

    /// <summary>
    /// <para>
    /// Wraps an existing contiguous memory area of at least 'width' x 'height' pixels allowing viewing/manipulation as
    /// an <see cref="Image{TPixel}"/> instance.
    /// </para>
    /// <para>
    /// Please note: this method relies on callers to carefully manage the target memory area being referenced by the
    /// pointer and that the lifetime of such a memory area is at least equal to that of the returned
    /// <see cref="Image{TPixel}"/> instance. For example, if the input pointer references an unmanaged memory area,
    /// callers must ensure that the memory area is not freed as long as the returned <see cref="Image{TPixel}"/> is
    /// in use and not disposed. The same applies if the input memory area points to a pinned managed object, as callers
    /// must ensure that objects will remain pinned as long as the <see cref="Image{TPixel}"/> instance is in use.
    /// Failing to do so constitutes undefined behavior and will likely lead to memory corruption and runtime crashes.
    /// </para>
    /// <para>
    /// Note also that if you have a <see cref="Memory{T}"/> or an array (which can be cast to <see cref="Memory{T}"/>) of
    /// either <see cref="byte"/> or <typeparamref name="TPixel"/> values, it is highly recommended to use one of the other
    /// available overloads of this method instead (such as <see cref="WrapMemory{TPixel}(Configuration, Memory{byte}, int, int)"/>
    /// or <see cref="WrapMemory{TPixel}(Configuration, Memory{TPixel}, int, int)"/>, to make the resulting code less error
    /// prone and avoid having to pin the underlying memory buffer in use. This method is primarily meant to be used when
    /// doing interop or working with buffers that are located in unmanaged memory.
    /// </para>
    /// </summary>
    /// <typeparam name="TPixel">The pixel type.</typeparam>
    /// <param name="pointer">The pointer to the target memory buffer to wrap.</param>
    /// <param name="bufferSizeInBytes">The byte length of the memory allocated.</param>
    /// <param name="width">The width of the memory image.</param>
    /// <param name="height">The height of the memory image.</param>
    /// <returns>An <see cref="Image{TPixel}"/> instance.</returns>
    public static unsafe Image<TPixel> WrapMemory<TPixel>(
        void* pointer,
        int bufferSizeInBytes,
        int width,
        int height)
        where TPixel : unmanaged, IPixel<TPixel>
        => WrapMemory<TPixel>(Configuration.Default, pointer, bufferSizeInBytes, width, height);
}
