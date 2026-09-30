// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats.Heif.Av1.Entropy;
using SixLabors.ImageSharp.Formats.Heif.Av1.Tiling;
using SixLabors.ImageSharp.Formats.Heif.Av1.Transform;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.Formats.Heif.Av1;

[Trait("Format", "Avif")]
public class Av1SymbolContextTests
{
    /// <summary>
    /// The configuration set the other AV1 vector tests use, so every supported width and the
    /// scalar path are all exercised.
    /// </summary>
    private const HwIntrinsics ContextConfigurations =
        HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic;

    /// <summary>
    /// Verifies that every register width and the scalar path derive the scalar contexts for every transform
    /// size and class.
    /// </summary>
    [Fact]
    public void NzMapContextsMatchScalarDerivationAtEveryRegisterWidth()
        => FeatureTestRunner.RunWithHwIntrinsicsFeature(ValidateAllNzMapContexts, ContextConfigurations);

    [Fact]
    public void LowLevelContextEndOfBlockMatchesDefinition()
    {
        for (int y = 1; y < 6; y++)
        {
            for (int x = 1; x < 6; x++)
            {
                int width = 1 << x;
                int height = 1 << y;
                using Av1LevelBuffer levels = new(Configuration.Default, new Size(width, height));
                for (int index = 0; index < width * height; index++)
                {
                    int expected = GetExpectedLowerLevelContextEndOfBlock(x, height, index);
                    Assert.Equal(expected, Av1SymbolContextHelper.GetLowerLevelContextEndOfBlock(levels, levels.GetPosition(index)));
                }
            }
        }
    }

    /// <summary>
    /// Computes the expected lower-level coefficient context at the end of a transform block.
    /// </summary>
    internal static int GetExpectedLowerLevelContextEndOfBlock(int blockWidthLog2, int height, int scanIndex)
    {
        if (scanIndex == 0)
        {
            return 0;
        }

        if (scanIndex <= height << blockWidthLog2 >> 3)
        {
            return 1;
        }

        if (scanIndex <= height << blockWidthLog2 >> 2)
        {
            return 2;
        }

        return 3;
    }

    /// <summary>
    /// Compares the derived contexts with the scalar derivation for every transform size and class.
    /// </summary>
    private static void ValidateAllNzMapContexts()
    {
        for (int size = 0; size < (int)Av1TransformSize.AllSizes; size++)
        {
            for (int transformClass = 0; transformClass < 3; transformClass++)
            {
                ValidateNzMapContexts((Av1TransformSize)size, (Av1TransformClass)transformClass);
            }
        }
    }

    /// <summary>
    /// Compares the derived contexts with the scalar derivation for one transform size and class.
    /// </summary>
    /// <param name="transformSize">The transform size.</param>
    /// <param name="transformClass">The transform class.</param>
    private static void ValidateNzMapContexts(Av1TransformSize transformSize, Av1TransformClass transformClass)
    {
        Av1TransformSize adjusted = transformSize.GetAdjusted();
        int width = adjusted.GetWidth();
        int height = adjusted.GetHeight();
        int[] coefficients = new int[width * height];
        Random random = new((int)transformSize + (17 * (int)transformClass));
        for (int i = 0; i < coefficients.Length; i++)
        {
            // Mostly small levels with a few large ones, like quantized photo residuals.
            int roll = random.Next(16);
            coefficients[i] = roll < 6 ? 0 : roll < 12 ? random.Next(-3, 4) : random.Next(-200, 201);
        }

        using Av1LevelBuffer levels = new(Configuration.Default, new Size(width, height));
        levels.Initialize(coefficients);
        Span<byte> active = levels.GetActiveLevels();
        sbyte[] actual = new sbyte[width * height];
        Av1NzMap.GetNzMapContexts(
            ref active[0], levels.Stride, width, height, transformSize, transformClass, ref actual[0]);

        for (int pos = 0; pos < actual.Length; pos++)
        {
            int expected = Av1SymbolContextHelper.GetLowerLevelsContext(
                ref active[Av1LevelBuffer.GetPaddedIndex(pos, levels.WidthLog2)],
                levels.Stride,
                pos,
                levels.WidthLog2,
                transformSize,
                transformClass);

            Assert.True(
                expected == actual[pos],
                $"size={transformSize} class={transformClass} position={pos}: expected {expected}, actual {actual[pos]}");
        }
    }
}
