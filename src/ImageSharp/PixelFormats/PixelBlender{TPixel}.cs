// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Buffers;
using System.Numerics;
using SixLabors.ImageSharp.PixelFormats.PixelBlenders;

namespace SixLabors.ImageSharp.PixelFormats;

/// <summary>
/// Abstract base class for calling pixel composition functions
/// </summary>
/// <typeparam name="TPixel">The type of the pixel</typeparam>
public abstract class PixelBlender<TPixel>
    where TPixel : unmanaged, IPixel<TPixel>
{
    /// <summary>
    /// Blend 2 pixels together.
    /// </summary>
    /// <param name="background">The background color.</param>
    /// <param name="source">The source color.</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <returns>The final pixel value after composition.</returns>
    public abstract TPixel Blend(TPixel background, TPixel source, float amount);

    /// <summary>
    /// Blends 2 rows together
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    public void Blend<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        float amount)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 3);
        this.Blend(
            configuration,
            destination,
            background,
            source,
            amount,
            buffer.Memory.Span[..(maxLength * 3)]);
    }

    /// <summary>
    /// Blends 2 rows together using caller-provided temporary vector scratch.
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 3 rows.</param>
    public void Blend<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        float amount,
        Span<Vector4> workingBuffer)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 3, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);
        Span<Vector4> sourceVectors = workingBuffer.Slice(maxLength * 2, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);
        this.ToBlendVector4(configuration, source[..maxLength], sourceVectors);

        this.BlendFunction(destinationVectors, backgroundVectors, sourceVectors, amount);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Blends a row against a constant source color.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    public void Blend(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        float amount)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 2);
        this.Blend(
            configuration,
            destination,
            background,
            source,
            amount,
            buffer.Memory.Span[..(maxLength * 2)]);
    }

    /// <summary>
    /// Blends a row against a constant source color using caller-provided temporary vector scratch.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 2 rows.</param>
    public void Blend(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        float amount,
        Span<Vector4> workingBuffer)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 2, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);

        this.BlendFunction(destinationVectors, backgroundVectors, this.ToBlendVector4(source), amount);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Blends 2 rows together with per-pixel coverage.
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    public void BlendWithCoverage<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        float amount,
        ReadOnlySpan<float> coverage)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 3);
        this.BlendWithCoverage(
            configuration,
            destination,
            background,
            source,
            amount,
            coverage,
            buffer.Memory.Span[..(maxLength * 3)]);
    }

    /// <summary>
    /// Blends 2 rows together with per-pixel coverage using caller-provided temporary vector scratch.
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 3 rows.</param>
    public void BlendWithCoverage<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        float amount,
        ReadOnlySpan<float> coverage,
        Span<Vector4> workingBuffer)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 3, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);
        Span<Vector4> sourceVectors = workingBuffer.Slice(maxLength * 2, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);
        this.ToBlendVector4(configuration, source[..maxLength], sourceVectors);

        this.BlendWithCoverageFunction(destinationVectors, backgroundVectors, sourceVectors, amount, coverage);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Blends a row against a constant source color with per-pixel coverage.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    public void BlendWithCoverage(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        float amount,
        ReadOnlySpan<float> coverage)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 2);
        this.BlendWithCoverage(
            configuration,
            destination,
            background,
            source,
            amount,
            coverage,
            buffer.Memory.Span[..(maxLength * 2)]);
    }

    /// <summary>
    /// Blends a row against a constant source color with per-pixel coverage using caller-provided temporary vector scratch.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 2 rows.</param>
    public void BlendWithCoverage(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        float amount,
        ReadOnlySpan<float> coverage,
        Span<Vector4> workingBuffer)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeBetweenOrEqualTo(amount, 0, 1, nameof(amount));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 2, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);

        this.BlendWithCoverageFunction(destinationVectors, backgroundVectors, this.ToBlendVector4(source), amount, coverage);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Blends 2 rows together
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    public void Blend(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixel> source,
        ReadOnlySpan<float> amount)
        => this.Blend<TPixel>(configuration, destination, background, source, amount);

    /// <summary>
    /// Blends 2 rows together using caller-provided temporary vector scratch.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 3 rows.</param>
    public void Blend(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixel> source,
        ReadOnlySpan<float> amount,
        Span<Vector4> workingBuffer)
        => this.Blend<TPixel>(configuration, destination, background, source, amount, workingBuffer);

    /// <summary>
    /// Blends 2 rows together
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    public void Blend<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        ReadOnlySpan<float> amount)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 3);
        this.Blend(
            configuration,
            destination,
            background,
            source,
            amount,
            buffer.Memory.Span[..(maxLength * 3)]);
    }

    /// <summary>
    /// Blends a row against a constant source color.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    public void Blend(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        ReadOnlySpan<float> amount)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 2);
        this.Blend(
            configuration,
            destination,
            background,
            source,
            amount,
            buffer.Memory.Span[..(maxLength * 2)]);
    }

    /// <summary>
    /// Blends 2 rows together using caller-provided temporary vector scratch.
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 3 rows.</param>
    public void Blend<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        ReadOnlySpan<float> amount,
        Span<Vector4> workingBuffer)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 3, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);
        Span<Vector4> sourceVectors = workingBuffer.Slice(maxLength * 2, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);
        this.ToBlendVector4(configuration, source[..maxLength], sourceVectors);

        this.BlendFunction(destinationVectors, backgroundVectors, sourceVectors, amount);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Blends a row against a constant source color using caller-provided temporary vector scratch.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 2 rows.</param>
    public void Blend(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        ReadOnlySpan<float> amount,
        Span<Vector4> workingBuffer)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 2, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);

        this.BlendFunction(destinationVectors, backgroundVectors, this.ToBlendVector4(source), amount);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Blends 2 rows together with per-pixel coverage.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    public void BlendWithCoverage(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixel> source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage)
        => this.BlendWithCoverage<TPixel>(configuration, destination, background, source, amount, coverage);

    /// <summary>
    /// Blends 2 rows together with per-pixel coverage using caller-provided temporary vector scratch.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 3 rows.</param>
    public void BlendWithCoverage(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixel> source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage,
        Span<Vector4> workingBuffer)
        => this.BlendWithCoverage<TPixel>(configuration, destination, background, source, amount, coverage, workingBuffer);

    /// <summary>
    /// Blends 2 rows together with per-pixel coverage.
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    public void BlendWithCoverage<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 3);
        this.BlendWithCoverage(
            configuration,
            destination,
            background,
            source,
            amount,
            coverage,
            buffer.Memory.Span[..(maxLength * 3)]);
    }

    /// <summary>
    /// Blends a row against a constant source color with per-pixel coverage.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    public void BlendWithCoverage(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));

        using IMemoryOwner<Vector4> buffer = configuration.MemoryAllocator.Allocate<Vector4>(maxLength * 2);
        this.BlendWithCoverage(
            configuration,
            destination,
            background,
            source,
            amount,
            coverage,
            buffer.Memory.Span[..(maxLength * 2)]);
    }

    /// <summary>
    /// Blends 2 rows together with per-pixel coverage using caller-provided temporary vector scratch.
    /// </summary>
    /// <typeparam name="TPixelSrc">the pixel format of the source span</typeparam>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 3 rows.</param>
    public void BlendWithCoverage<TPixelSrc>(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        ReadOnlySpan<TPixelSrc> source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage,
        Span<Vector4> workingBuffer)
        where TPixelSrc : unmanaged, IPixel<TPixelSrc>
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(source.Length, maxLength, nameof(source.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 3, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);
        Span<Vector4> sourceVectors = workingBuffer.Slice(maxLength * 2, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);
        this.ToBlendVector4(configuration, source[..maxLength], sourceVectors);

        this.BlendWithCoverageFunction(destinationVectors, backgroundVectors, sourceVectors, amount, coverage);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Blends a row against a constant source color with per-pixel coverage using caller-provided temporary vector scratch.
    /// </summary>
    /// <param name="configuration"><see cref="Configuration"/> to use internally</param>
    /// <param name="destination">the destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    /// <param name="workingBuffer">Reusable temporary vector scratch with capacity for at least 2 rows.</param>
    public void BlendWithCoverage(
        Configuration configuration,
        Span<TPixel> destination,
        ReadOnlySpan<TPixel> background,
        TPixel source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage,
        Span<Vector4> workingBuffer)
    {
        int maxLength = destination.Length;
        Guard.MustBeGreaterThanOrEqualTo(background.Length, maxLength, nameof(background.Length));
        Guard.MustBeGreaterThanOrEqualTo(amount.Length, maxLength, nameof(amount.Length));
        Guard.MustBeGreaterThanOrEqualTo(coverage.Length, maxLength, nameof(coverage.Length));
        Guard.MustBeGreaterThanOrEqualTo(workingBuffer.Length, maxLength * 2, nameof(workingBuffer.Length));

        Span<Vector4> destinationVectors = workingBuffer[..maxLength];
        Span<Vector4> backgroundVectors = workingBuffer.Slice(maxLength, maxLength);

        this.ToBlendVector4(configuration, background[..maxLength], backgroundVectors);

        this.BlendWithCoverageFunction(destinationVectors, backgroundVectors, this.ToBlendVector4(source), amount, coverage);

        this.FromBlendVector4(configuration, destinationVectors, destination);
    }

    /// <summary>
    /// Converts source pixels to the scaled-vector representation consumed by this blender.
    /// </summary>
    /// <typeparam name="TPixelSource">The source pixel format.</typeparam>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source pixels.</param>
    /// <param name="destination">The destination vectors.</param>
    protected virtual void ToBlendVector4<TPixelSource>(
        Configuration configuration,
        ReadOnlySpan<TPixelSource> source,
        Span<Vector4> destination)
        where TPixelSource : unmanaged, IPixel<TPixelSource>
        => PixelOperations<TPixelSource>.Instance.ToVector4(configuration, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

    /// <summary>
    /// Converts a source pixel to the vector representation consumed by the blend functions.
    /// </summary>
    /// <param name="source">The source pixel.</param>
    /// <returns>The source vector.</returns>
    protected virtual Vector4 ToBlendVector4(TPixel source)
        => source.ToUnassociatedScaledVector4();

    /// <summary>
    /// Converts blend results from this blender's scaled-vector representation to destination pixels.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    /// <param name="source">The source vectors.</param>
    /// <param name="destination">The destination pixels.</param>
    protected virtual void FromBlendVector4(
        Configuration configuration,
        Span<Vector4> source,
        Span<TPixel> destination)
        => PixelOperations<TPixel>.Instance.FromVector4Destructive(configuration, source, destination, PixelConversionModifiers.Scale | PixelConversionModifiers.UnPremultiply);

    /// <summary>
    /// Blend 2 rows together.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    protected abstract void BlendFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        ReadOnlySpan<Vector4> source,
        float amount);

    /// <summary>
    /// Blend a row against a constant source color.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color vector</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    protected abstract void BlendFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        Vector4 source,
        float amount);

    /// <summary>
    /// Blend 2 rows together.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    protected abstract void BlendFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        ReadOnlySpan<Vector4> source,
        ReadOnlySpan<float> amount);

    /// <summary>
    /// Blend a row against a constant source color.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color vector</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    protected abstract void BlendFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        Vector4 source,
        ReadOnlySpan<float> amount);

    /// <summary>
    /// Blend 2 rows together with per-pixel coverage.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    protected virtual void BlendWithCoverageFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        ReadOnlySpan<Vector4> source,
        float amount,
        ReadOnlySpan<float> coverage)
    {
        this.BlendFunction(destination, background, source, amount);

        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = PorterDuffFunctions.BlendWithCoverage(background[i], destination[i], Numerics.Clamp(coverage[i], 0, 1F));
        }
    }

    /// <summary>
    /// Blend a row against a constant source color with per-pixel coverage.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color vector</param>
    /// <param name="amount">
    /// A value between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    protected virtual void BlendWithCoverageFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        Vector4 source,
        float amount,
        ReadOnlySpan<float> coverage)
    {
        this.BlendFunction(destination, background, source, amount);

        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = PorterDuffFunctions.BlendWithCoverage(background[i], destination[i], Numerics.Clamp(coverage[i], 0, 1F));
        }
    }

    /// <summary>
    /// Blend 2 rows together with per-pixel coverage.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source span</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    protected virtual void BlendWithCoverageFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        ReadOnlySpan<Vector4> source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage)
    {
        this.BlendFunction(destination, background, source, amount);

        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = PorterDuffFunctions.BlendWithCoverage(background[i], destination[i], Numerics.Clamp(coverage[i], 0, 1F));
        }
    }

    /// <summary>
    /// Blend a row against a constant source color with per-pixel coverage.
    /// </summary>
    /// <param name="destination">destination span</param>
    /// <param name="background">the background span</param>
    /// <param name="source">the source color vector</param>
    /// <param name="amount">
    /// A span with values between 0 and 1 indicating the weight of the second source vector.
    /// At amount = 0, "background" is returned, at amount = 1, "source" is returned.
    /// </param>
    /// <param name="coverage">A span with coverage values between 0 and 1.</param>
    protected virtual void BlendWithCoverageFunction(
        Span<Vector4> destination,
        ReadOnlySpan<Vector4> background,
        Vector4 source,
        ReadOnlySpan<float> amount,
        ReadOnlySpan<float> coverage)
    {
        this.BlendFunction(destination, background, source, amount);

        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = PorterDuffFunctions.BlendWithCoverage(background[i], destination[i], Numerics.Clamp(coverage[i], 0, 1F));
        }
    }
}
