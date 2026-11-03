// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.ColorConverters;

/// <summary>
/// Converts reconstructed JPEG XL components to working sRGB before destination pixel packing.
/// </summary>
/// <remarks>
/// The pixel converter owns the row storage. A closed generic converter owns traversal; its operator owns
/// component ordering and arithmetic. Shared color models and ICC transforms own color conversion.
/// Alpha remains separate from device components until the color transform has finished.
/// </remarks>
internal abstract partial class JxlColorConverterBase
{
    /// <summary>
    /// Gets the number of scratch floats required in addition to the destination vector row.
    /// </summary>
    /// <param name="width">The output row width.</param>
    /// <returns>The required scratch length.</returns>
    public abstract long GetBufferLength(int width);

    /// <summary>
    /// Normalizes reconstructed samples and converts one row without changing the source planes.
    /// </summary>
    /// <typeparam name="TSample">The four-byte integer carrier or restored float sample.</typeparam>
    /// <typeparam name="TSampleOperator">The prepared sample-representation operator.</typeparam>
    /// <param name="source">The source planes and separately normalized black and alpha rows.</param>
    /// <param name="destination">The working sRGB pixel vectors.</param>
    /// <param name="scratch">The row scratch returned by GetBufferLength.</param>
    /// <param name="sampleOperator">The prepared integer or floating sample conversion.</param>
    public abstract void ConvertToRgb<TSample, TSampleOperator>(
        in ComponentValues<TSample> source,
        Span<Vector4> destination,
        Span<float> scratch,
        in TSampleOperator sampleOperator)
        where TSample : unmanaged
        where TSampleOperator : struct, JxlSampleConverter.IConversionOperator<TSample>;

    /// <summary>
    /// Borrows one row of reconstructed components without transferring ownership.
    /// </summary>
    /// <typeparam name="TSample">The four-byte source sample type.</typeparam>
    public readonly ref struct ComponentValues<TSample>
        where TSample : unmanaged
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ComponentValues{TSample}"/> struct.
        /// </summary>
        /// <param name="component0">The first source component.</param>
        /// <param name="component1">The second source component, or the repeated luminance row.</param>
        /// <param name="component2">The third source component, or the repeated luminance row.</param>
        /// <param name="black">The normalized black row required by CMYK, or an empty span for other models.</param>
        /// <param name="alpha">The normalized alpha row, or an empty span for opaque output.</param>
        public ComponentValues(
            ReadOnlySpan<TSample> component0,
            ReadOnlySpan<TSample> component1,
            ReadOnlySpan<TSample> component2,
            ReadOnlySpan<float> black,
            ReadOnlySpan<float> alpha)
        {
            this.Component0 = component0;
            this.Component1 = component1;
            this.Component2 = component2;
            this.Black = black;
            this.Alpha = alpha;
        }

        /// <summary>
        /// Gets the first source row.
        /// </summary>
        public ReadOnlySpan<TSample> Component0 { get; }

        /// <summary>
        /// Gets the second source row.
        /// </summary>
        public ReadOnlySpan<TSample> Component1 { get; }

        /// <summary>
        /// Gets the third source row.
        /// </summary>
        public ReadOnlySpan<TSample> Component2 { get; }

        /// <summary>
        /// Gets normalized black components, separate from alpha.
        /// </summary>
        public ReadOnlySpan<float> Black { get; }

        /// <summary>
        /// Gets normalized alpha components, separate from every device channel.
        /// </summary>
        public ReadOnlySpan<float> Alpha { get; }
    }
}
