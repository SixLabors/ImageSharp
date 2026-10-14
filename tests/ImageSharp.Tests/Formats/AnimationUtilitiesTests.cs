// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using System.Numerics;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Tests.TestUtilities;
using SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison;

namespace SixLabors.ImageSharp.Tests.Formats;

public class AnimationUtilitiesTests
{
    /// <summary>
    /// Checks that a missing previous frame compares against the replacement stored in the frame's pixel format.
    /// </summary>
    /// <param name="provider">The existing color fixture decoded without an alpha component.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgb24)]
    public void DeDuplicatePixels_BackgroundUsesFramePixelFormat(TestImageProvider<Rgb24> provider)
    {
        using Image<Rgb24> source = provider.GetImage();
        using Image<Rgb24> result = new(provider.Configuration, source.Width, source.Height);
        using Image<Rgb24> background = new(provider.Configuration, source.Width, source.Height, Color.Transparent.ToPixel<Rgb24>());
        using Image<Rgb24> expected = new(provider.Configuration, source.Width, source.Height);
        ImageFrame<Rgb24> previous = background.Frames.RootFrame;

        // A missing previous frame must behave like a previous frame filled with the same converted replacement.
        // Rgb24 has no alpha channel. Both cases compare against opaque black, without restoring Color.Transparent's alpha.
        (bool Difference, Rectangle Bounds) actualBounds = AnimationUtilities.DeDuplicatePixels(
            provider.Configuration, null, source.Frames.RootFrame, null, result.Frames.RootFrame, Color.Transparent, true, ClampingMode.None);

        (bool Difference, Rectangle Bounds) expectedBounds = AnimationUtilities.DeDuplicatePixels(
            provider.Configuration, previous, source.Frames.RootFrame, null, expected.Frames.RootFrame, Color.Transparent, true, ClampingMode.None);

        Assert.Equal(expectedBounds, actualBounds);
        ImageComparer.Exact.VerifySimilarity(expected, result);
    }

    /// <summary>
    /// Checks that deduplication retains stored associated components and replaces duplicates with the requested background.
    /// </summary>
    /// <typeparam name="TPixel">The associated pixel format.</typeparam>
    /// <param name="provider">The existing image with zero, partial and opaque alpha.</param>
    [Theory]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Rgba32P | PixelTypes.Bgra32P | PixelTypes.Argb32P)]
    [WithFile(TestImages.Webp.Lossless.WithIccpAndAlpha, PixelTypes.Abgr32P | PixelTypes.RgbaVectorP)]
    public void DeDuplicatePixels_PreservesAssociatedSamples<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(PixelTests<TPixel>.RunAssociatedSamples,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic, provider);
    }

    /// <summary>
    /// Checks native copying and duplicate replacement without unassociating the stored samples.
    /// </summary>
    /// <typeparam name="TPixel">The associated pixel format.</typeparam>
    /// <param name="provider">The existing alpha fixture.</param>
    private static void AssertAssociatedSamples<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        using Image<TPixel> source = provider.GetImage();
        using Image<TPixel> result = new(provider.Configuration, source.Width, source.Height);
        ImageFrame<TPixel> frame = source.Frames.RootFrame;

        // Blending disabled must copy the original stored components even when every pixel matches the preceding frame.
        (bool difference, _) = AnimationUtilities.DeDuplicatePixels(
            provider.Configuration, frame, frame, null, result.Frames.RootFrame, Color.Transparent, false, ClampingMode.None);

        Assert.False(difference);
        ImageComparer.Exact.VerifySimilarity(source, result);

        // A partial-alpha replacement must be packed once into TPixel, then copied to every duplicate without changing its association.
        Color background = Color.FromPixel(new Rgba32(128, 64, 32, 128));
        using Image<TPixel> expected = new(provider.Configuration, source.Width, source.Height, background.ToPixel<TPixel>());
        (difference, _) = AnimationUtilities.DeDuplicatePixels(
            provider.Configuration, frame, frame, null, result.Frames.RootFrame, background, true, ClampingMode.None);

        Assert.False(difference);
        ImageComparer.Exact.VerifySimilarity(expected, result);
    }

    /// <summary>
    /// Checks byte and floating-point paths against scalar native-pixel comparison on existing animation frames.
    /// </summary>
    /// <typeparam name="TPixel">The frame pixel format.</typeparam>
    /// <param name="provider">The existing animation fixture.</param>
    [Theory]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgba32 | PixelTypes.Bgra32 | PixelTypes.Argb32 | PixelTypes.Abgr32 | PixelTypes.Rgba64)]
    [WithFile(TestImages.Png.APng, PixelTypes.Rgb24 | PixelTypes.Bgr24 | PixelTypes.L8 | PixelTypes.La16 | PixelTypes.A8)]
    [WithFile(TestImages.Png.APng, PixelTypes.NormalizedByte4 | PixelTypes.NormalizedByte2 | PixelTypes.Byte4)]
    [WithFile(TestImages.Png.APng, PixelTypes.Bgr565 | PixelTypes.Bgra4444 | PixelTypes.Bgra5551)]
    [WithFile(TestImages.Png.FrameOffset, PixelTypes.Rgba32P | PixelTypes.Bgra32P | PixelTypes.Argb32P | PixelTypes.Abgr32P | PixelTypes.RgbaVectorP)]
    [WithFile(TestImages.Png.FrameOffset, PixelTypes.NormalizedByte4P)]
    public void DeDuplicatePixels_MatchesNativeComparison<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        FeatureTestRunner.RunWithHwIntrinsicsFeature(PixelTests<TPixel>.RunNativeComparison,
            HwIntrinsics.AllowAll | HwIntrinsics.DisableAVX512F | HwIntrinsics.DisableAVX | HwIntrinsics.DisableHWIntrinsic, provider);
    }

    /// <summary>
    /// Checks result pixels and bounds for every frame, with and without duplicate replacement.
    /// </summary>
    /// <typeparam name="TPixel">The frame pixel format.</typeparam>
    /// <param name="provider">The existing animation fixture.</param>
    private static void AssertNativeComparison<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        // Decode all APNG frames. The default reference provider can load only the still image.
        using Image<TPixel> source = provider.GetImage(PngDecoder.Instance);

        // Removing one column leaves a remainder at every SIMD width, including the four-pixel byte path.
        source.Mutate(x => x.Crop(new Rectangle(0, 0, source.Width - 1, source.Height)));
        using Image<TPixel> result = new(provider.Configuration, source.Width, source.Height);
        using Image<TPixel> expected = new(provider.Configuration, source.Width, source.Height);
        TPixel background = Color.Transparent.ToPixel<TPixel>();

        foreach (bool blend in new[] { false, true })
        {
            foreach (ClampingMode clamping in new[] { ClampingMode.None, ClampingMode.Even })
            {
                for (int frameIndex = 0; frameIndex < source.Frames.Count; frameIndex++)
                {
                    ImageFrame<TPixel> previous = frameIndex == 0 ? null : source.Frames[frameIndex - 1];
                    ImageFrame<TPixel> current = source.Frames[frameIndex];
                    ImageFrame<TPixel> next = frameIndex + 1 < source.Frames.Count ? source.Frames[frameIndex + 1] : null;
                    (bool Difference, Rectangle Bounds) expectedBounds = CompareNativePixels(previous, current, next, expected.Frames.RootFrame, background, blend);

                    // WebP rounds the origin down to even coordinates without excluding any changed pixels.
                    Rectangle bounds = expectedBounds.Bounds;
                    if (clamping == ClampingMode.Even)
                    {
                        bounds.Width = Math.Min(source.Width, bounds.Width + (bounds.X & 1));
                        bounds.Height = Math.Min(source.Height, bounds.Height + (bounds.Y & 1));
                        bounds.X &= ~1;
                        bounds.Y &= ~1;
                    }

                    (bool Difference, Rectangle Bounds) actual = AnimationUtilities.DeDuplicatePixels(
                        provider.Configuration, previous, current, next, result.Frames.RootFrame, Color.Transparent, blend, clamping);

                    Assert.Equal((expectedBounds.Difference, bounds), actual);
                    ImageComparer.Exact.VerifySimilarity(expected, result);
                }
            }
        }
    }

    /// <summary>
    /// Applies the animation comparison rules directly to native scalar pixels without an intermediate row conversion.
    /// </summary>
    /// <typeparam name="TPixel">The frame pixel format.</typeparam>
    /// <param name="previous">The previous frame, or null to compare against the background.</param>
    /// <param name="current">The frame being compared.</param>
    /// <param name="next">The following frame, or null when none follows.</param>
    /// <param name="result">The result frame.</param>
    /// <param name="background">The replacement in the frame's pixel format.</param>
    /// <param name="blend">Whether matching pixels are replaced.</param>
    /// <returns>The difference flag and changed bounds.</returns>
    private static (bool Difference, Rectangle Bounds) CompareNativePixels<TPixel>(
        ImageFrame<TPixel> previous, ImageFrame<TPixel> current, ImageFrame<TPixel> next, ImageFrame<TPixel> result, TPixel background, bool blend)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        int left = current.Width;
        int right = 0;
        int top = current.Height;
        int bottom = 0;

        for (int y = 0; y < current.Height; y++)
        {
            for (int x = 0; x < current.Width; x++)
            {
                TPixel pixel = current[x, y];
                Vector4 value = pixel.ToVector4();
                Vector4 preceding = previous is null ? background.ToVector4() : previous[x, y].ToVector4();
                bool equal = value == preceding;
                result[x, y] = blend && equal ? background : pixel;

                // Lower next-frame alpha includes this pixel in the bounds even when its color is unchanged.
                // Replacement depends only on equality with the previous frame.
                if (!equal || (next is not null && value.W > next[x, y].ToVector4().W))
                {
                    left = Math.Min(left, x);
                    right = Math.Max(right, x + 1);
                    top = Math.Min(top, y);
                    bottom = Math.Max(bottom, y + 1);
                }
            }
        }

        bool difference = right != 0;

        // Preserve the existing nonempty rectangle for unchanged frames, which still carry animation timing.
        Rectangle bounds = difference ? Rectangle.FromLTRB(left, top, right, bottom) : new Rectangle(current.Width - 1, 0, 1, current.Height);
        return (difference, bounds);
    }

    /// <summary>
    /// Provides non-generic callbacks whose declaring type retains the requested pixel format for RemoteExecutor.
    /// </summary>
    /// <typeparam name="TPixel">The destination pixel format.</typeparam>
    private static class PixelTests<TPixel>
        where TPixel : unmanaged, IPixel<TPixel>
    {
        /// <summary>
        /// Restores the typed provider and runs the assertions with the selected hardware features.
        /// </summary>
        /// <param name="providerDump">The serialized image provider.</param>
        public static void RunAssociatedSamples(string providerDump)
            => AssertAssociatedSamples(FeatureTestRunner.DeserializeForXunit<TestImageProvider<TPixel>>(providerDump));

        /// <summary>
        /// Restores the typed provider and runs the assertions with the selected hardware features.
        /// </summary>
        /// <param name="providerDump">The serialized image provider.</param>
        public static void RunNativeComparison(string providerDump)
            => AssertNativeComparison(FeatureTestRunner.DeserializeForXunit<TestImageProvider<TPixel>>(providerDump));
    }
}
