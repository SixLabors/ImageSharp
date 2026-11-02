// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;

namespace SixLabors.ImageSharp.Formats.Jxl.Components.Decoder;

/// <summary>
/// Retains the frame's Gaborish and edge-preserving filter parameters without applying either filter during identification.
/// </summary>
internal sealed class JxlRestorationFilter
{
    /// <summary>
    /// The smallest absolute Gaborish normalization divisor accepted by the native header parser.
    /// </summary>
    private const float MinimumGaborishNormalization = 1E-8F;

    /// <summary>
    /// The smallest modular sigma accepted by the native header parser.
    /// </summary>
    private const float MinimumModularSigma = 1E-8F;

    private float[]? sharpness;

    /// <summary>
    /// Initializes a new instance of the <see cref="JxlRestorationFilter"/> class with the serialized defaults.
    /// </summary>
    private JxlRestorationFilter()
    {
    }

    /// <summary>
    /// Gets the immutable default filter parameters shared by frames with an all-default filter group.
    /// </summary>
    public static JxlRestorationFilter Default { get; } = new();

    /// <summary>
    /// Gets a value indicating whether the frame uses the Gaborish filter.
    /// </summary>
    public bool Gaborish { get; private set; } = true;

    /// <summary>
    /// Gets the horizontal and vertical neighbor weight for each of the three color components.
    /// </summary>
    public Vector3 GaborishWeight1 { get; private set; } = new(1.1F * 0.104699568F);

    /// <summary>
    /// Gets the diagonal neighbor weight for each of the three color components.
    /// </summary>
    public Vector3 GaborishWeight2 { get; private set; } = new(1.1F * 0.055680538F);

    /// <summary>
    /// Gets the edge-preserving filter iteration count, from zero to three.
    /// </summary>
    public byte Iterations { get; private set; } = 2;

    /// <summary>
    /// Gets the edge-preserving filter's eight sharpness factors.
    /// </summary>
    public ReadOnlySpan<float> Sharpness => this.sharpness is null ? [0F, 1F / 7F, 2F / 7F, 3F / 7F, 4F / 7F, 5F / 7F, 6F / 7F, 1F] : this.sharpness;

    /// <summary>
    /// Gets the component multipliers used to measure color distance in the edge-preserving filter.
    /// </summary>
    public Vector3 ChannelScale { get; private set; } = new(40F, 5F, 3.5F);

    /// <summary>
    /// Gets the encoded first-pass zero-flush threshold. The reference filter retains this field but does not apply it to weights.
    /// </summary>
    public float Pass1ZeroFlush { get; private set; } = 0.45F;

    /// <summary>
    /// Gets the encoded second-pass zero-flush threshold. The reference filter retains this field but does not apply it to weights.
    /// </summary>
    public float Pass2ZeroFlush { get; private set; } = 0.6F;

    /// <summary>
    /// Gets the VarDCT quantization multiplier used to derive the filter sigma.
    /// </summary>
    public float QuantizationMultiplier { get; private set; } = 0.46F;

    /// <summary>
    /// Gets the sigma multiplier for filter pass zero.
    /// </summary>
    public float Pass0SigmaScale { get; private set; } = 0.9F;

    /// <summary>
    /// Gets the sigma multiplier for filter pass two.
    /// </summary>
    public float Pass2SigmaScale { get; private set; } = 6.5F;

    /// <summary>
    /// Gets the sum-of-absolute-differences multiplier at filter borders.
    /// </summary>
    public float BorderSadMultiplier { get; private set; } = 2F / 3F;

    /// <summary>
    /// Gets the positive sigma used for modular frames.
    /// </summary>
    public float ModularSigma { get; private set; } = 1F;

    /// <summary>
    /// Reads the filter group in the order defined by libjxl's loop_filter.cc.
    /// </summary>
    /// <param name="reader">The frame bit reader.</param>
    /// <param name="modular">Whether the frame uses modular coding.</param>
    /// <returns>The parsed parameters or the shared immutable defaults.</returns>
    public static JxlRestorationFilter Read(ref JxlBitReader reader, bool modular)
    {
        if (reader.ReadBoolean())
        {
            // All-default headers share this immutable instance. Custom fields are written only into a new frame-owned instance.
            return Default;
        }

        JxlRestorationFilter result = new() { Gaborish = reader.ReadBoolean() };
        if (result.Gaborish && reader.ReadBoolean())
        {
            // The file interleaves axial and diagonal weights by component: X1, X2, Y1, Y2, B1, B2.
            // Keep component vectors for the later SIMD filter without changing any half-precision header value.
            float x1 = reader.ReadHalf();
            float x2 = reader.ReadHalf();
            float y1 = reader.ReadHalf();
            float y2 = reader.ReadHalf();
            float b1 = reader.ReadHalf();
            float b2 = reader.ReadHalf();
            result.GaborishWeight1 = new Vector3(x1, y1, b1);
            result.GaborishWeight2 = new Vector3(x2, y2, b2);

            // Each symmetric three-by-three kernel has center weight one, four axial neighbors and four diagonals.
            // Its normalization divisor must not be near zero. This is libjxl's header validity check, not pixel clamping.
            Vector3 denominator = Vector3.One + (4F * (result.GaborishWeight1 + result.GaborishWeight2));
            if (MathF.Abs(denominator.X) < MinimumGaborishNormalization || MathF.Abs(denominator.Y) < MinimumGaborishNormalization
                || MathF.Abs(denominator.Z) < MinimumGaborishNormalization)
            {
                throw new InvalidImageContentException("The JPEG XL Gaborish weights have an invalid normalization divisor.");
            }
        }

        result.Iterations = (byte)reader.ReadBits(2);
        if (result.Iterations != 0)
        {
            // VarDCT derives edge-filter strength from coefficient quantization and an eight-entry sharpness table.
            // Modular coding omits that table and supplies one explicit sigma after the shared parameter groups.
            if (!modular && reader.ReadBoolean())
            {
                // Allocate only a custom table. The default table refers to constant data and needs no per-frame owner.
                result.sharpness = new float[8];

                for (int i = 0; i < result.sharpness.Length; i++)
                {
                    result.sharpness[i] = reader.ReadHalf();
                }
            }

            if (reader.ReadBoolean())
            {
                // These dimensionless multipliers weight component differences and the first two passes' cutoff rules.
                result.ChannelScale = new Vector3(reader.ReadHalf(), reader.ReadHalf(), reader.ReadHalf());
                result.Pass1ZeroFlush = reader.ReadHalf();
                result.Pass2ZeroFlush = reader.ReadHalf();
            }

            if (reader.ReadBoolean())
            {
                // The quantization multiplier is absent in modular coding. The pass scales and border correction
                // follow in both modes, so changing this field order would misread every later header value.
                if (!modular)
                {
                    result.QuantizationMultiplier = reader.ReadHalf();
                }

                result.Pass0SigmaScale = reader.ReadHalf();
                result.Pass2SigmaScale = reader.ReadHalf();
                result.BorderSadMultiplier = reader.ReadHalf();
            }

            if (modular)
            {
                result.ModularSigma = reader.ReadHalf();
                if (result.ModularSigma < MinimumModularSigma)
                {
                    throw new InvalidImageContentException("The JPEG XL modular filter sigma is too small.");
                }
            }
        }

        // Extensions follow even when the edge-preserving filter has zero iterations. They contain no filter sample data.
        reader.SkipExtensions();
        return result;
    }
}
