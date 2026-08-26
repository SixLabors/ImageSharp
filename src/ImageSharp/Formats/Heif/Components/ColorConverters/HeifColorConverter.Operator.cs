// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace SixLabors.ImageSharp.Formats.Heif.Components;

/// <content>
/// Provides the static operator contract and SIMD traversal used by HEIF color converters. Each lane carries one pixel
/// and the three vectors remain planar component rows throughout conversion. Descending vector widths consume a single
/// shared offset, preserving SIMD execution for the remainder without overlapping stores or requiring row padding.
/// </content>
internal abstract partial class HeifColorConverterBase
{
    /// <summary>
    /// Defines color-model arithmetic for scalar and SIMD lanes in both conversion directions.
    /// </summary>
    /// <remarks>
    /// Operator methods are lane-local and must preserve input order. The closed operator type lets the JIT bind the
    /// matrix or lifting transform once per converter, keeping color-model dispatch outside every row loop.
    /// </remarks>
    internal interface IHeifColorOperator
    {
        /// <summary>
        /// Gets a value indicating whether chroma uses the luma range rather than the centered chroma range.
        /// </summary>
        public static abstract bool ChromaUsesLumaRange { get; }

        /// <summary>
        /// Converts one normalized encoded sample to RGB.
        /// </summary>
        /// <param name="component0">The first encoded component, replaced by red.</param>
        /// <param name="component1">The second encoded component, replaced by green.</param>
        /// <param name="component2">The third encoded component, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void ConvertToRgb(
            ref float component0,
            ref float component1,
            ref float component2,
            in HeifColorConversionParameters parameters);

        /// <summary>
        /// Converts four normalized encoded samples to RGB.
        /// </summary>
        /// <param name="component0">The first encoded component lanes, replaced by red.</param>
        /// <param name="component1">The second encoded component lanes, replaced by green.</param>
        /// <param name="component2">The third encoded component lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void ConvertToRgb(
            ref Vector128<float> component0,
            ref Vector128<float> component1,
            ref Vector128<float> component2,
            in HeifColorConversionParameters parameters);

        /// <summary>
        /// Converts eight normalized encoded samples to RGB.
        /// </summary>
        /// <param name="component0">The first encoded component lanes, replaced by red.</param>
        /// <param name="component1">The second encoded component lanes, replaced by green.</param>
        /// <param name="component2">The third encoded component lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void ConvertToRgb(
            ref Vector256<float> component0,
            ref Vector256<float> component1,
            ref Vector256<float> component2,
            in HeifColorConversionParameters parameters);

        /// <summary>
        /// Converts sixteen normalized encoded samples to RGB.
        /// </summary>
        /// <param name="component0">The first encoded component lanes, replaced by red.</param>
        /// <param name="component1">The second encoded component lanes, replaced by green.</param>
        /// <param name="component2">The third encoded component lanes, replaced by blue.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        public static abstract void ConvertToRgb(
            ref Vector512<float> component0,
            ref Vector512<float> component1,
            ref Vector512<float> component2,
            in HeifColorConversionParameters parameters);

        /// <summary>
        /// Converts one normalized RGB sample to encoded components.
        /// </summary>
        /// <param name="red">The normalized red component.</param>
        /// <param name="green">The normalized green component.</param>
        /// <param name="blue">The normalized blue component.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        /// <param name="component0">The first converted component.</param>
        /// <param name="component1">The second converted component.</param>
        /// <param name="component2">The third converted component.</param>
        public static abstract void ConvertFromRgb(
            float red,
            float green,
            float blue,
            in HeifColorConversionParameters parameters,
            out float component0,
            out float component1,
            out float component2);

        /// <summary>
        /// Converts four normalized RGB samples to encoded components.
        /// </summary>
        /// <param name="red">The normalized red lanes.</param>
        /// <param name="green">The normalized green lanes.</param>
        /// <param name="blue">The normalized blue lanes.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        /// <param name="component0">The first converted component lanes.</param>
        /// <param name="component1">The second converted component lanes.</param>
        /// <param name="component2">The third converted component lanes.</param>
        public static abstract void ConvertFromRgb(
            Vector128<float> red,
            Vector128<float> green,
            Vector128<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector128<float> component0,
            out Vector128<float> component1,
            out Vector128<float> component2);

        /// <summary>
        /// Converts eight normalized RGB samples to encoded components.
        /// </summary>
        /// <param name="red">The normalized red lanes.</param>
        /// <param name="green">The normalized green lanes.</param>
        /// <param name="blue">The normalized blue lanes.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        /// <param name="component0">The first converted component lanes.</param>
        /// <param name="component1">The second converted component lanes.</param>
        /// <param name="component2">The third converted component lanes.</param>
        public static abstract void ConvertFromRgb(
            Vector256<float> red,
            Vector256<float> green,
            Vector256<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector256<float> component0,
            out Vector256<float> component1,
            out Vector256<float> component2);

        /// <summary>
        /// Converts sixteen normalized RGB samples to encoded components.
        /// </summary>
        /// <param name="red">The normalized red lanes.</param>
        /// <param name="green">The normalized green lanes.</param>
        /// <param name="blue">The normalized blue lanes.</param>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        /// <param name="component0">The first converted component lanes.</param>
        /// <param name="component1">The second converted component lanes.</param>
        /// <param name="component2">The third converted component lanes.</param>
        public static abstract void ConvertFromRgb(
            Vector512<float> red,
            Vector512<float> green,
            Vector512<float> blue,
            in HeifColorConversionParameters parameters,
            out Vector512<float> component0,
            out Vector512<float> component1,
            out Vector512<float> component2);
    }

    /// <summary>
    /// Converts an HEIF color model using one operator-driven traversal for all SIMD widths.
    /// </summary>
    /// <typeparam name="TOperator">The color-model-specific arithmetic.</typeparam>
    internal sealed class HeifColorConverter<TOperator> : HeifColorConverterBase
        where TOperator : struct, IHeifColorOperator
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HeifColorConverter{TOperator}"/> class.
        /// </summary>
        /// <param name="parameters">The resolved H.273 conversion parameters.</param>
        /// <param name="isMonochrome">Whether the frame contains only luma samples.</param>
        public HeifColorConverter(in HeifColorConversionParameters parameters, bool isMonochrome)
            : base(in parameters, isMonochrome)
        {
        }

        /// <inheritdoc/>
        public override float ChromaScale => TOperator.ChromaUsesLumaRange ? this.Parameters.LumaScale : this.Parameters.ChromaScale;

        /// <inheritdoc/>
        public override float ChromaBias => TOperator.ChromaUsesLumaRange ? this.Parameters.LumaBias : this.Parameters.ChromaBias;

        /// <inheritdoc/>
        public override void ConvertToRgbInPlace(Span<float> component0, Span<float> component1, Span<float> component2)
        {
            HeifColorConversionParameters parameters = this.Parameters;

            // Row reconstruction owns equally sized planar buffers. As in JPEG, first-element byrefs let each
            // SIMD width share one offset while the closed operator type keeps color-model dispatch out of the loop.
            ref float component0Base = ref MemoryMarshal.GetReference(component0);
            ref float component1Base = ref MemoryMarshal.GetReference(component1);
            ref float component2Base = ref MemoryMarshal.GetReference(component2);
            int length = component0.Length;
            int i = 0;

            if (this.IsMonochrome)
            {
                // Monochrome has no operator arithmetic: expanding the luma range once and copying each SIMD
                // vector to all three planes is cheaper than routing it through a three-component operator.
                if (Vector512.IsHardwareAccelerated && i <= length - Vector512<float>.Count)
                {
                    Vector512<float> bias = Vector512.Create(parameters.LumaBias);
                    Vector512<float> scale = Vector512.Create(parameters.LumaScale);
                    int oneVectorFromEnd = length - Vector512<float>.Count;
                    for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
                    {
                        Vector512<float> value = (Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component0Base, i)) - bias) / scale;
                        Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component0Base, i)) = value;
                        Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component1Base, i)) = value;
                        Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component2Base, i)) = value;
                    }
                }

                if (Vector256.IsHardwareAccelerated && i <= length - Vector256<float>.Count)
                {
                    Vector256<float> bias = Vector256.Create(parameters.LumaBias);
                    Vector256<float> scale = Vector256.Create(parameters.LumaScale);
                    int oneVectorFromEnd = length - Vector256<float>.Count;
                    for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
                    {
                        Vector256<float> value = (Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component0Base, i)) - bias) / scale;
                        Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component0Base, i)) = value;
                        Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component1Base, i)) = value;
                        Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component2Base, i)) = value;
                    }
                }

                if (Vector128.IsHardwareAccelerated && i <= length - Vector128<float>.Count)
                {
                    Vector128<float> bias = Vector128.Create(parameters.LumaBias);
                    Vector128<float> scale = Vector128.Create(parameters.LumaScale);
                    int oneVectorFromEnd = length - Vector128<float>.Count;
                    for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
                    {
                        Vector128<float> value = (Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component0Base, i)) - bias) / scale;
                        Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component0Base, i)) = value;
                        Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component1Base, i)) = value;
                        Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component2Base, i)) = value;
                    }
                }

                for (; i < length; i++)
                {
                    float value = (Unsafe.Add(ref component0Base, i) - parameters.LumaBias) / parameters.LumaScale;
                    Unsafe.Add(ref component0Base, i) = value;
                    Unsafe.Add(ref component1Base, i) = value;
                    Unsafe.Add(ref component2Base, i) = value;
                }

                return;
            }

            float chromaBias = this.ChromaBias;
            float chromaScale = this.ChromaScale;

            // Descending widths preserve vector execution for the remainder left by a wider register. Divide by the
            // signaled ranges directly because multiplying by rounded reciprocals changes exact output-code boundaries.
            if (Vector512.IsHardwareAccelerated && i <= length - Vector512<float>.Count)
            {
                Vector512<float> lumaBias = Vector512.Create(parameters.LumaBias);
                Vector512<float> lumaScale = Vector512.Create(parameters.LumaScale);
                Vector512<float> chromaBiasVector = Vector512.Create(chromaBias);
                Vector512<float> chromaScaleVector = Vector512.Create(chromaScale);
                int oneVectorFromEnd = length - Vector512<float>.Count;
                for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
                {
                    ref Vector512<float> c0 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component0Base, i));
                    ref Vector512<float> c1 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component1Base, i));
                    ref Vector512<float> c2 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component2Base, i));
                    c0 = (c0 - lumaBias) / lumaScale;
                    c1 = (c1 - chromaBiasVector) / chromaScaleVector;
                    c2 = (c2 - chromaBiasVector) / chromaScaleVector;

                    TOperator.ConvertToRgb(ref c0, ref c1, ref c2, in parameters);
                }
            }

            if (Vector256.IsHardwareAccelerated && i <= length - Vector256<float>.Count)
            {
                Vector256<float> lumaBias = Vector256.Create(parameters.LumaBias);
                Vector256<float> lumaScale = Vector256.Create(parameters.LumaScale);
                Vector256<float> chromaBiasVector = Vector256.Create(chromaBias);
                Vector256<float> chromaScaleVector = Vector256.Create(chromaScale);
                int oneVectorFromEnd = length - Vector256<float>.Count;
                for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
                {
                    ref Vector256<float> c0 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component0Base, i));
                    ref Vector256<float> c1 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component1Base, i));
                    ref Vector256<float> c2 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component2Base, i));
                    c0 = (c0 - lumaBias) / lumaScale;
                    c1 = (c1 - chromaBiasVector) / chromaScaleVector;
                    c2 = (c2 - chromaBiasVector) / chromaScaleVector;

                    TOperator.ConvertToRgb(ref c0, ref c1, ref c2, in parameters);
                }
            }

            if (Vector128.IsHardwareAccelerated && i <= length - Vector128<float>.Count)
            {
                Vector128<float> lumaBias = Vector128.Create(parameters.LumaBias);
                Vector128<float> lumaScale = Vector128.Create(parameters.LumaScale);
                Vector128<float> chromaBiasVector = Vector128.Create(chromaBias);
                Vector128<float> chromaScaleVector = Vector128.Create(chromaScale);
                int oneVectorFromEnd = length - Vector128<float>.Count;
                for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
                {
                    ref Vector128<float> c0 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component0Base, i));
                    ref Vector128<float> c1 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component1Base, i));
                    ref Vector128<float> c2 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component2Base, i));
                    c0 = (c0 - lumaBias) / lumaScale;
                    c1 = (c1 - chromaBiasVector) / chromaScaleVector;
                    c2 = (c2 - chromaBiasVector) / chromaScaleVector;

                    TOperator.ConvertToRgb(ref c0, ref c1, ref c2, in parameters);
                }
            }

            // Scalar conversion is reserved for the zero-to-three samples left after the SIMD cascade.
            for (; i < length; i++)
            {
                float c0 = (Unsafe.Add(ref component0Base, i) - parameters.LumaBias) / parameters.LumaScale;
                float c1 = (Unsafe.Add(ref component1Base, i) - chromaBias) / chromaScale;
                float c2 = (Unsafe.Add(ref component2Base, i) - chromaBias) / chromaScale;
                TOperator.ConvertToRgb(ref c0, ref c1, ref c2, in parameters);
                Unsafe.Add(ref component0Base, i) = c0;
                Unsafe.Add(ref component1Base, i) = c1;
                Unsafe.Add(ref component2Base, i) = c2;
            }
        }

        /// <inheritdoc/>
        public override void ConvertFromRgbInPlace(
            Span<float> component0,
            Span<float> component1,
            Span<float> component2,
            float maximumValue)
        {
            HeifColorConversionParameters parameters = this.Parameters;

            // The unpacker supplies three planar RGB rows. These same buffers become the destination component
            // rows after each operator call, so encoding retains JPEG's planar contract without another allocation.
            ref float component0Base = ref MemoryMarshal.GetReference(component0);
            ref float component1Base = ref MemoryMarshal.GetReference(component1);
            ref float component2Base = ref MemoryMarshal.GetReference(component2);
            int length = component0.Length;
            int i = 0;

            // RGB normalization is part of the vector load, and each operator returns planar components through
            // out parameters. This is the same input/output shape used by JPEG's encoder-side color operators.
            if (Vector512.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = length - Vector512<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    Vector512<float> inverseMaximum = Vector512.Create(1F / maximumValue);

                    for (; i <= oneVectorFromEnd; i += Vector512<float>.Count)
                    {
                        Vector512<float> red = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component0Base, i)) * inverseMaximum;
                        Vector512<float> green = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component1Base, i)) * inverseMaximum;
                        Vector512<float> blue = Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component2Base, i)) * inverseMaximum;
                        ref Vector512<float> c0 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component0Base, i));
                        ref Vector512<float> c1 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component1Base, i));
                        ref Vector512<float> c2 = ref Unsafe.As<float, Vector512<float>>(ref Unsafe.Add(ref component2Base, i));

                        TOperator.ConvertFromRgb(red, green, blue, in parameters, out c0, out c1, out c2);
                    }
                }
            }

            if (Vector256.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = length - Vector256<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    Vector256<float> inverseMaximum = Vector256.Create(1F / maximumValue);

                    for (; i <= oneVectorFromEnd; i += Vector256<float>.Count)
                    {
                        Vector256<float> red = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component0Base, i)) * inverseMaximum;
                        Vector256<float> green = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component1Base, i)) * inverseMaximum;
                        Vector256<float> blue = Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component2Base, i)) * inverseMaximum;
                        ref Vector256<float> c0 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component0Base, i));
                        ref Vector256<float> c1 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component1Base, i));
                        ref Vector256<float> c2 = ref Unsafe.As<float, Vector256<float>>(ref Unsafe.Add(ref component2Base, i));

                        TOperator.ConvertFromRgb(red, green, blue, in parameters, out c0, out c1, out c2);
                    }
                }
            }

            if (Vector128.IsHardwareAccelerated)
            {
                int oneVectorFromEnd = length - Vector128<float>.Count;

                if (i <= oneVectorFromEnd)
                {
                    Vector128<float> inverseMaximum = Vector128.Create(1F / maximumValue);

                    for (; i <= oneVectorFromEnd; i += Vector128<float>.Count)
                    {
                        Vector128<float> red = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component0Base, i)) * inverseMaximum;
                        Vector128<float> green = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component1Base, i)) * inverseMaximum;
                        Vector128<float> blue = Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component2Base, i)) * inverseMaximum;
                        ref Vector128<float> c0 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component0Base, i));
                        ref Vector128<float> c1 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component1Base, i));
                        ref Vector128<float> c2 = ref Unsafe.As<float, Vector128<float>>(ref Unsafe.Add(ref component2Base, i));

                        TOperator.ConvertFromRgb(red, green, blue, in parameters, out c0, out c1, out c2);
                    }
                }
            }

            float inverseMaximumScalar = 1F / maximumValue;

            // The shared offset leaves at most three samples for the scalar fallback on SIMD-capable systems.
            for (; i < length; i++)
            {
                float red = Unsafe.Add(ref component0Base, i) * inverseMaximumScalar;
                float green = Unsafe.Add(ref component1Base, i) * inverseMaximumScalar;
                float blue = Unsafe.Add(ref component2Base, i) * inverseMaximumScalar;

                TOperator.ConvertFromRgb(red, green, blue, in parameters, out float c0, out float c1, out float c2);

                Unsafe.Add(ref component0Base, i) = c0;
                Unsafe.Add(ref component1Base, i) = c1;
                Unsafe.Add(ref component2Base, i) = c2;
            }
        }
    }
}
