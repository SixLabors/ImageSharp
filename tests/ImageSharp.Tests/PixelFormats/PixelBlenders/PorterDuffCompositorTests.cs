// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;

namespace SixLabors.ImageSharp.Tests.PixelFormats.PixelBlenders;

public class PorterDuffCompositorTests
{
    // TODO: Add other modes to compare.
    public static readonly TheoryData<PixelAlphaCompositionMode> CompositingOperators =
        new()
        {
            PixelAlphaCompositionMode.Src,
            PixelAlphaCompositionMode.SrcAtop,
            PixelAlphaCompositionMode.SrcOver,
            PixelAlphaCompositionMode.SrcIn,
            PixelAlphaCompositionMode.SrcOut,
            PixelAlphaCompositionMode.Dest,
            PixelAlphaCompositionMode.DestAtop,
            PixelAlphaCompositionMode.DestOver,
            PixelAlphaCompositionMode.DestIn,
            PixelAlphaCompositionMode.DestOut,
            PixelAlphaCompositionMode.Clear,
            PixelAlphaCompositionMode.Xor
        };

    [Theory]
    [WithFile(TestImages.Png.PDDest, nameof(CompositingOperators), PixelTypes.Rgba32)]
    public void PorterDuffOutputIsCorrect(TestImageProvider<Rgba32> provider, PixelAlphaCompositionMode mode)
    {
        static void RunTest(string providerDump, string alphaMode)
        {
            TestImageProvider<Rgba32> provider
                = BasicSerializer.Deserialize<TestImageProvider<Rgba32>>(providerDump);

            TestFile srcFile = TestFile.Create(TestImages.Png.PDSrc);
            using Image<Rgba32> src = srcFile.CreateRgba32Image();
            using Image<Rgba32> dest = provider.GetImage();
            GraphicsOptions options = new()
            {
                Antialias = false,
                AlphaCompositionMode = Enum.Parse<PixelAlphaCompositionMode>(alphaMode)
            };

            using Image<Rgba32> res = dest.Clone(x => x.DrawImage(src, options));
            string combinedMode = alphaMode;

            if (combinedMode != "Src" && combinedMode.StartsWith("Src", StringComparison.OrdinalIgnoreCase))
            {
                combinedMode = combinedMode[3..];
            }

            res.DebugSave(provider, combinedMode);
            res.CompareToReferenceOutput(provider, combinedMode);
        }

        FeatureTestRunner.RunWithHwIntrinsicsFeature(
            RunTest,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX,
            provider,
            mode.ToString());
    }
}
