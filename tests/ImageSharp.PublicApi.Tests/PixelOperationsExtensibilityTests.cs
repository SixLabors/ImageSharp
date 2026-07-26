// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.PixelFormats;

namespace SixLabors.ImageSharp.PublicApi.Tests;

/// <summary>
/// Verifies that representation-aware pixel operations can be implemented outside the ImageSharp assembly.
/// </summary>
public class PixelOperationsExtensibilityTests
{
    /// <summary>
    /// Verifies that an external <see cref="PixelOperations{TPixel}"/> subclass can override and invoke every representation-aware hook.
    /// </summary>
    [Fact]
    public void PixelOperationsExposesAllRepresentationHooksToExternalSubclasses()
    {
        ExternalRgba32PixelOperations operations = new();

        operations.InvokeRepresentationHooks();

        Assert.Equal(RepresentationHooks.All, operations.InvokedHooks);
    }

    /// <summary>
    /// Verifies that an external <see cref="AssociatedAlphaPixelOperations{TPixel}"/> subclass can override and invoke every representation-aware hook.
    /// </summary>
    [Fact]
    public void AssociatedAlphaPixelOperationsExposesAllRepresentationHooksToExternalSubclasses()
    {
        ExternalRgba32PPixelOperations operations = new();

        operations.InvokeRepresentationHooks();

        Assert.Equal(RepresentationHooks.All, operations.InvokedHooks);
    }

    [Flags]
    private enum RepresentationHooks
    {
        None = 0,
        ToUnassociatedVector4 = 1 << 0,
        ToAssociatedVector4 = 1 << 1,
        ToUnassociatedScaledVector4 = 1 << 2,
        ToAssociatedScaledVector4 = 1 << 3,
        FromUnassociatedVector4Destructive = 1 << 4,
        FromAssociatedVector4Destructive = 1 << 5,
        FromUnassociatedScaledVector4Destructive = 1 << 6,
        FromAssociatedScaledVector4Destructive = 1 << 7,
        All = ToUnassociatedVector4
            | ToAssociatedVector4
            | ToUnassociatedScaledVector4
            | ToAssociatedScaledVector4
            | FromUnassociatedVector4Destructive
            | FromAssociatedVector4Destructive
            | FromUnassociatedScaledVector4Destructive
            | FromAssociatedScaledVector4Destructive
    }

    /// <summary>
    /// Exercises the representation-aware hooks inherited directly from <see cref="PixelOperations{TPixel}"/>.
    /// </summary>
    private sealed class ExternalRgba32PixelOperations : PixelOperations<Rgba32>
    {
        /// <summary>
        /// Gets the hooks invoked through this external subclass.
        /// </summary>
        public RepresentationHooks InvokedHooks { get; private set; }

        /// <summary>
        /// Invokes every representation-aware operation exposed to derived classes.
        /// </summary>
        public void InvokeRepresentationHooks()
        {
            // Empty spans isolate accessibility and virtual dispatch; conversion behavior is covered by the pixel-operation test suite.
            this.ToUnassociatedVector4(Configuration.Default, ReadOnlySpan<Rgba32>.Empty, Span<Vector4>.Empty);
            this.ToAssociatedVector4(Configuration.Default, ReadOnlySpan<Rgba32>.Empty, Span<Vector4>.Empty);
            this.ToUnassociatedScaledVector4(Configuration.Default, ReadOnlySpan<Rgba32>.Empty, Span<Vector4>.Empty);
            this.ToAssociatedScaledVector4(Configuration.Default, ReadOnlySpan<Rgba32>.Empty, Span<Vector4>.Empty);
            this.FromUnassociatedVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32>.Empty);
            this.FromAssociatedVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32>.Empty);
            this.FromUnassociatedScaledVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32>.Empty);
            this.FromAssociatedScaledVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32>.Empty);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToUnassociatedVector4;

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToAssociatedVector4;

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToUnassociatedScaledVector4;

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToAssociatedScaledVector4;

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32> destination) => this.InvokedHooks |= RepresentationHooks.FromUnassociatedVector4Destructive;

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32> destination) => this.InvokedHooks |= RepresentationHooks.FromAssociatedVector4Destructive;

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32> destination) => this.InvokedHooks |= RepresentationHooks.FromUnassociatedScaledVector4Destructive;

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32> destination) => this.InvokedHooks |= RepresentationHooks.FromAssociatedScaledVector4Destructive;
    }

    /// <summary>
    /// Exercises the representation-aware hooks inherited through <see cref="AssociatedAlphaPixelOperations{TPixel}"/>.
    /// </summary>
    private sealed class ExternalRgba32PPixelOperations : AssociatedAlphaPixelOperations<Rgba32P>
    {
        /// <summary>
        /// Gets the hooks invoked through this external subclass.
        /// </summary>
        public RepresentationHooks InvokedHooks { get; private set; }

        /// <summary>
        /// Invokes every representation-aware operation exposed to derived classes.
        /// </summary>
        public void InvokeRepresentationHooks()
        {
            // Empty spans isolate accessibility and virtual dispatch; conversion behavior is covered by the pixel-operation test suite.
            this.ToUnassociatedVector4(Configuration.Default, ReadOnlySpan<Rgba32P>.Empty, Span<Vector4>.Empty);
            this.ToAssociatedVector4(Configuration.Default, ReadOnlySpan<Rgba32P>.Empty, Span<Vector4>.Empty);
            this.ToUnassociatedScaledVector4(Configuration.Default, ReadOnlySpan<Rgba32P>.Empty, Span<Vector4>.Empty);
            this.ToAssociatedScaledVector4(Configuration.Default, ReadOnlySpan<Rgba32P>.Empty, Span<Vector4>.Empty);
            this.FromUnassociatedVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32P>.Empty);
            this.FromAssociatedVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32P>.Empty);
            this.FromUnassociatedScaledVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32P>.Empty);
            this.FromAssociatedScaledVector4Destructive(Configuration.Default, Span<Vector4>.Empty, Span<Rgba32P>.Empty);
        }

        /// <inheritdoc />
        protected override void ToUnassociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToUnassociatedVector4;

        /// <inheritdoc />
        protected override void ToAssociatedVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToAssociatedVector4;

        /// <inheritdoc />
        protected override void ToUnassociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToUnassociatedScaledVector4;

        /// <inheritdoc />
        protected override void ToAssociatedScaledVector4(Configuration configuration, ReadOnlySpan<Rgba32P> source, Span<Vector4> destination) => this.InvokedHooks |= RepresentationHooks.ToAssociatedScaledVector4;

        /// <inheritdoc />
        protected override void FromUnassociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination) => this.InvokedHooks |= RepresentationHooks.FromUnassociatedVector4Destructive;

        /// <inheritdoc />
        protected override void FromAssociatedVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination) => this.InvokedHooks |= RepresentationHooks.FromAssociatedVector4Destructive;

        /// <inheritdoc />
        protected override void FromUnassociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination) => this.InvokedHooks |= RepresentationHooks.FromUnassociatedScaledVector4Destructive;

        /// <inheritdoc />
        protected override void FromAssociatedScaledVector4Destructive(Configuration configuration, Span<Vector4> source, Span<Rgba32P> destination) => this.InvokedHooks |= RepresentationHooks.FromAssociatedScaledVector4Destructive;
    }
}
