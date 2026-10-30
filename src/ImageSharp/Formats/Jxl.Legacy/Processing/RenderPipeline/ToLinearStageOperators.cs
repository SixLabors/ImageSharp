// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats.Jxl.Cms.ToneMapping;
using SixLabors.ImageSharp.Formats.Jxl.Cms.TransferFunctions;
using SixLabors.ImageSharp.Formats.Jxl.Processing.Decoder;

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.RenderPipeline;

internal static class ToLinearStageOperators
{
    /// <summary>
    /// Abstracts applying inverse transfer functions on the RGB channel.
    /// </summary>
    public interface IOperator
    {
        /// <summary>
        /// Applies the inverse transfer function on three RGB channels.
        /// </summary>
        /// <param name="r">Red</param>
        /// <param name="g">Green</param>
        /// <param name="b">Blue</param>
        public void Transform(
            ref Vector<float> r,
            ref Vector<float> g,
            ref Vector<float> b);

        /// <summary>
        /// Creates a new instance of this operator, or uses a shared
        /// instance if the operator doesn't depend on instance data.
        /// </summary>
        /// <param name="outputEncodingInfo">
        /// The output encoding info may contain settings required for
        /// specific transforms, such as inverse gamma or intensity target.
        /// </param>
        /// <returns>
        /// The created operator.
        /// </returns>
        public static abstract IOperator Create(JxlOutputEncodingInfo outputEncodingInfo);
    }

    /// <summary>
    /// Preserves initial vector values as-is.
    /// </summary>
    public sealed class LinearOperator : IOperator
    {
        /// <summary>
        /// Shared instance of this operator.
        /// </summary>
        public static readonly LinearOperator Instance = new();

        /// <inheritdoc />
        static IOperator IOperator.Create(JxlOutputEncodingInfo outputEncodingInfo) => Instance;

        /// <inheritdoc />
        public void Transform(ref Vector<float> r, ref Vector<float> g, ref Vector<float> b)
        {
        }
    }

    /// <summary>
    /// Applies the sRGB transfer function on RGB channels.
    /// </summary>
    public sealed class RgbOperator : IOperator
    {
        /// <summary>
        /// Shared instance of this operator.
        /// </summary>
        public static readonly RgbOperator Instance = new();

        /// <inheritdoc />
        static IOperator IOperator.Create(JxlOutputEncodingInfo outputEncodingInfo) => Instance;

        /// <inheritdoc />
        public void Transform(ref Vector<float> r, ref Vector<float> g, ref Vector<float> b)
        {
            r = JxlSRgbTransferFunction.DisplayFromEncoded(r);
            g = JxlSRgbTransferFunction.DisplayFromEncoded(g);
            b = JxlSRgbTransferFunction.DisplayFromEncoded(b);
        }
    }

    /// <summary>
    /// Uses the Perceptual Quantization (PQ) transfer function.
    /// </summary>
    /// <param name="outputEncodingInfo">Contains the intensity target for the transform.</param>
    public sealed class PqOperator(JxlOutputEncodingInfo outputEncodingInfo) : IOperator
    {
        /// <summary>
        /// The actual PQ transform is handled by this field.
        /// </summary>
        private readonly JxlPqTransferFunction pq = new(outputEncodingInfo.OriginalIntensityTarget);

        /// <inheritdoc />
        static IOperator IOperator.Create(JxlOutputEncodingInfo outputEncodingInfo) => new PqOperator(outputEncodingInfo);

        /// <inheritdoc />
        public void Transform(ref Vector<float> r, ref Vector<float> g, ref Vector<float> b)
        {
            r = this.pq.DisplayFromEncoded(r);
            g = this.pq.DisplayFromEncoded(g);
            b = this.pq.DisplayFromEncoded(b);
        }
    }

    /// <summary>
    /// Uses the Hybrid Log Gamma (HLG) tone mapper and transfer function.
    /// </summary>
    /// <param name="outputEncodingInfo">Contains luminances and the intensity target for the tone mapping.</param>
    public sealed class HlgOperator(JxlOutputEncodingInfo outputEncodingInfo) : IOperator
    {
        /// <summary>
        /// The actual HLG tone mapping is handled by this field.
        /// </summary>
        private readonly JxlHlgOotfToneMapper hlgOotf = JxlHlgOotfToneMapper.ToSceneLight(
            outputEncodingInfo.OriginalIntensityTarget,
            outputEncodingInfo.Luminances);

        /// <inheritdoc />
        static IOperator IOperator.Create(JxlOutputEncodingInfo outputEncodingInfo) => new HlgOperator(outputEncodingInfo);

        /// <inheritdoc />
        public void Transform(ref Vector<float> r, ref Vector<float> g, ref Vector<float> b)
        {
            // Apply inverse transfer function first
            r = JxlHlgTransferFunction.DisplayFromEncoded(r);
            g = JxlHlgTransferFunction.DisplayFromEncoded(g);
            b = JxlHlgTransferFunction.DisplayFromEncoded(b);

            // Then tone map
            this.hlgOotf.Apply(ref r, ref g, ref b);
        }
    }

    /// <summary>
    /// Uses the transfer function described in the ITU-R BT.709 standard.
    /// </summary>
    public sealed class Bt709Operator : IOperator
    {
        /// <summary>
        /// Shared instance of this operator.
        /// </summary>
        public static readonly Bt709Operator Instance = new();

        /// <inheritdoc />
        static IOperator IOperator.Create(JxlOutputEncodingInfo outputEncodingInfo) => Instance;

        /// <inheritdoc />
        public void Transform(ref Vector<float> r, ref Vector<float> g, ref Vector<float> b)
        {
            r = JxlBt709TransferFunction.DisplayFromEncoded(r);
            g = JxlBt709TransferFunction.DisplayFromEncoded(g);
            b = JxlBt709TransferFunction.DisplayFromEncoded(b);
        }
    }

    /// <summary>
    /// Applies a simple gamma/power-law transfer operation independently to the red, green and
    /// blue channels.
    /// </summary>
    /// <param name="outputEncodingInfo">Contains the inverse gamma.</param>
    public sealed class GammaOperator(JxlOutputEncodingInfo outputEncodingInfo) : IOperator
    {
        /// <summary>
        /// The gamma from output encoding info.
        /// </summary>
        private readonly float gamma = 1.0f / outputEncodingInfo.InverseGamma;

        static IOperator IOperator.Create(JxlOutputEncodingInfo outputEncodingInfo) => new GammaOperator(outputEncodingInfo);

        /// <inheritdoc />
        public void Transform(ref Vector<float> r, ref Vector<float> g, ref Vector<float> b)
        {
            r = Vector.ConditionalSelect(
                    Vector.LessThan(r, Vector.Create(1e-5f)),
                    Vector<float>.Zero,
                    JxlSimdUtils.FastPowf(r, Vector.Create(this.gamma)));
            g = Vector.ConditionalSelect(
                    Vector.LessThan(g, Vector.Create(1e-5f)),
                    Vector<float>.Zero,
                    JxlSimdUtils.FastPowf(g, Vector.Create(this.gamma)));
            b = Vector.ConditionalSelect(
                    Vector.LessThan(b, Vector.Create(1e-5f)),
                    Vector<float>.Zero,
                    JxlSimdUtils.FastPowf(b, Vector.Create(this.gamma)));
        }
    }
}
