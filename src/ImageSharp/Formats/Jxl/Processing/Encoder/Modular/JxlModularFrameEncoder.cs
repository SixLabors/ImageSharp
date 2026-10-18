// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Processing.Encoder.Modular;

internal static class JxlModularFrameEncoder
{
    public const float SqueezeQualityFactor = 0.35f;

    public const float SqueezeLumaFactor = 1.1f;

    public const float SqueezeQualityFactorXyb = 4.0f;

    public const float SqueezeQualityFactorY = 1.5f;

    /// <summary>
    /// Quantization table for the XYB Squeeze transform.
    /// </summary>
    public static readonly float[][] SqueezeXybQTable =
    [
        [
            163.84f, 81.92f, 40.96f, 20.48f, 10.24f, 5.12f, 2.56f, 1.28f, 0.64f, 0.32f, 0.16f,
            0.08f, 0.04f, 0.02f, 0.01f, 0.005f
        ],  // Y
        [
            1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5f, 0.5f, 0.5f, 0.5f,
            0.5f
        ],  // X
        [
            2048, 1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1, 0.5f, 0.5f, 0.5f,
            0.5f
        ],  // B-Y
    ];

    /// <summary>
    /// Gets the quantization table for the Luma Squeeze transform.
    /// </summary>
    public static ReadOnlySpan<float> SqueezeLumaQTable =>
    [
        163.84f, 81.92f, 40.96f, 20.48f, 10.24f, 5.12f,
        2.56f,   1.28f,  0.64f,  0.32f,  0.16f,  0.08f,
        0.04f,   0.02f,  0.01f,  0.005f
    ];

    /// <summary>
    /// Gets the quantization table for the Chroma Squeeze transform.
    /// </summary>
    public static ReadOnlySpan<float> SqueezeChromaQTable =>
    [
        1024, 512, 256, 128, 64, 32, 16, 8, 4, 2, 1,
        0.5f, 0.5f, 0.5f, 0.5f, 0.5f
    ];
}
