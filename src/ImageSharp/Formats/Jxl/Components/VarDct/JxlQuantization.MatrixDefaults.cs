// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

namespace SixLabors.ImageSharp.Formats.Jxl.Components.VarDct;

internal static partial class JxlQuantization
{
    /// <summary>
    /// Gets the modes of the seventeen format-defined quantization descriptions.
    /// </summary>
    private static ReadOnlySpan<byte> LibraryMatrixModes => [6, 1, 2, 3, 6, 6, 6, 6, 6, 4, 5, 6, 6, 6, 6, 6, 6];

    /// <summary>
    /// Gets the primary radial band counts of the format-defined descriptions.
    /// </summary>
    private static ReadOnlySpan<byte> LibraryBandCounts => [6, 0, 0, 4, 7, 8, 7, 8, 8, 4, 4, 8, 8, 8, 8, 8, 8];

    /// <summary>
    /// Gets the secondary AFV band counts of the format-defined descriptions.
    /// </summary>
    private static ReadOnlySpan<byte> LibrarySecondaryBandCounts => [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 0, 0, 0, 0, 0];

    /// <summary>
    /// Gets each description's offset in the packed format-defined parameter array.
    /// </summary>
    private static ReadOnlySpan<ushort> LibraryParameterOffsets => [0, 18, 27, 45, 63, 84, 108, 129, 153, 177, 192, 243, 267, 291, 315, 339, 363, 387];

    /// <summary>
    /// Gets the format-defined parameters in component order, with special weights before radial bands.
    /// </summary>
    private static ReadOnlySpan<float> LibraryMatrixParameters =>
    [

        // These single-precision values are the format defaults in libjxl's DequantMatricesLibraryDef.
        // Seeds and special weights already include their factor of sixty-four. Band deltas and multipliers do not.
        // Quantization family 0.
        3.15e+3F, 0F, -0.4F, -0.4F, -0.4F, -2F, 5.6e+2F, 0F, -0.3F, -0.3F, -0.3F, -0.3F, 512F, -2F, -1F,
        0F, -1F, -2F,

        // Quantization family 1.
        2.8e+2F, 3.16e+3F, 3.16e+3F, 6e+1F, 864F, 864F, 18F, 2e+2F, 2e+2F,

        // Quantization family 2.
        3.84e+3F, 2.56e+3F, 1.28e+3F, 6.4e+2F, 4.8e+2F, 3e+2F, 9.6e+2F, 6.4e+2F, 3.2e+2F, 1.8e+2F, 1.4e+2F, 1.2e+2F, 6.4e+2F, 3.2e+2F, 128F, 64F, 32F, 16F,

        // Quantization family 3.
        1F, 1F, 1F, 1F, 1F, 1F, 2.2e+3F, 0F, 0F, 0F, 392F, 0F, 0F, 0F, 112F, -0.25F, -0.25F, -0.5F,

        // Quantization family 4.
        8996.873F, -1.3000778F, -0.4942453F, -0.43909377F, -0.6350102F, -0.9017726F, -1.6162099F, 3191.4836F, -0.67424583F, -0.80745816F,
        -0.4492584F, -0.3586544F, -0.3132239F, -0.37615025F, 1157.504F, -2.0531423F, -1.4F, -0.5068713F, -0.4270873F, -1.4856834F,
        -4.920914F,

        // Quantization family 5.
        15718.408F, -1.025F, -0.98F, -0.9012F, -0.4F, -0.48819396F, -0.421064F, -0.27F, 7305.7637F, -0.8041958F,
        -0.76330364F, -0.5566038F, -0.49785304F, -0.43699592F, -0.40180868F, -0.27321684F, 3803.5317F, -3.0607336F, -2.041327F, -2.023565F,
        -0.54953897F, -0.4F, -0.4F, -0.3F,

        // Quantization family 6.
        7240.7734F, -0.7F, -0.7F, -0.2F, -0.2F, -0.2F, -0.5F, 1448.1547F, -0.5F, -0.5F, -0.5F, -0.2F,
        -0.2F, -0.2F, 506.85413F, -1.4F, -0.2F, -0.5F, -0.5F, -1.5F, -3.6F,

        // Quantization family 7.
        16283.249F, -1.7812846F, -1.6309059F, -1.0382179F, -0.85F, -0.7F, -0.9F, -1.2360638F, 5089.1577F, -0.3200494F,
        -0.3536285F, -0.3034F, -0.61F, -0.5F, -0.5F, -0.6F, 3397.7761F, -0.32132736F, -0.3450762F, -0.7034F, -0.9F,
        -1F, -1F, -1.1754606F,

        // Quantization family 8.
        13844.971F, -0.971138F, -0.658F, -0.42026F, -0.22712F, -0.2206F, -0.226F, -0.6F, 4798.964F, -0.6112531F,
        -0.8377079F, -0.7901486F, -0.26927274F, -0.38272768F, -0.22924222F, -0.20719099F, 1807.2369F, -1.2F, -1.2F, -0.7F,
        -0.7F, -0.7F, -0.4F, -0.5F,

        // Quantization family 9.
        1F, 1F, 1F, 2198.0505F, -0.96269625F, -0.7619425F, -0.65511405F, 764.36554F, -0.926302F, -0.967523F, -0.2784529F, 527.10754F,
        -1.4594386F, -1.4500821F, -1.5843723F,

        // Quantization family 10.
        3072F, 3072F, 256F, 256F, 256F, 414F, 0F, 0F, 0F, 1024F, 1024F, 5e+1F, 5e+1F, 5e+1F, 58F, 0F, 0F, 0F, 384F, 384F, 12F, 12F, 12F, 22F, -0.25F, -0.25F,
        -0.25F, 2198.0505F, -0.96269625F, -0.7619425F, -0.65511405F, 764.36554F, -0.926302F, -0.967523F, -0.2784529F, 527.10754F,
        -1.4594386F, -1.4500821F, -1.5843723F, 2.2e+3F, 0F, 0F, 0F, 392F, 0F, 0F, 0F, 112F, -0.25F, -0.25F, -0.5F,

        // Quantization family 11.
        23966.166F, -1.025F, -0.78F, -0.65012F, -0.19041574F, -0.20819396F, -0.421064F, -0.32733846F, 8380.191F, -0.30419582F,
        -0.36330363F, -0.3566038F, -0.34430745F, -0.33699593F, -0.30180865F, -0.27321684F, 4493.024F, -1.2F, -1.2F, -0.8F,
        -0.7F, -0.7F, -0.4F, -0.5F,

        // Quantization family 12.
        15358.898F, -1.025F, -0.78F, -0.65012F, -0.19041574F, -0.20819396F, -0.421064F, -0.32733846F, 5597.3603F, -0.30419582F,
        -0.36330363F, -0.3566038F, -0.34430745F, -0.33699593F, -0.30180865F, -0.27321684F, 2919.9617F, -1.2F, -1.2F, -0.8F,
        -0.7F, -0.7F, -0.4F, -0.5F,

        // Quantization family 13.
        47932.332F, -1.025F, -0.78F, -0.65012F, -0.19041574F, -0.20819396F, -0.421064F, -0.32733846F, 16760.383F, -0.30419582F,
        -0.36330363F, -0.3566038F, -0.34430745F, -0.33699593F, -0.30180865F, -0.27321684F, 8986.048F, -1.2F, -1.2F, -0.8F,
        -0.7F, -0.7F, -0.4F, -0.5F,

        // Quantization family 14.
        30717.797F, -1.025F, -0.78F, -0.65012F, -0.19041574F, -0.20819396F, -0.421064F, -0.32733846F, 11194.721F, -0.30419582F,
        -0.36330363F, -0.3566038F, -0.34430745F, -0.33699593F, -0.30180865F, -0.27321684F, 5839.9233F, -1.2F, -1.2F, -0.8F,
        -0.7F, -0.7F, -0.4F, -0.5F,

        // Quantization family 15.
        95864.664F, -1.025F, -0.78F, -0.65012F, -0.19041574F, -0.20819396F, -0.421064F, -0.32733846F, 33520.766F, -0.30419582F,
        -0.36330363F, -0.3566038F, -0.34430745F, -0.33699593F, -0.30180865F, -0.27321684F, 17972.096F, -1.2F, -1.2F, -0.8F,
        -0.7F, -0.7F, -0.4F, -0.5F,

        // Quantization family 16.
        61435.594F, -1.025F, -0.78F, -0.65012F, -0.19041574F, -0.20819396F, -0.421064F, -0.32733846F, 22389.441F, -0.30419582F,
        -0.36330363F, -0.3566038F, -0.34430745F, -0.33699593F, -0.30180865F, -0.27321684F, 11679.847F, -1.2F, -1.2F, -0.8F,
        -0.7F, -0.7F, -0.4F, -0.5F,
    ];
}
