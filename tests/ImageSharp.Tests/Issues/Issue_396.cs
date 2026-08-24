// Copyright (c) Six Labors.
// Licensed under the Six Labors Split License.

using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SixLabors.ImageSharp.Tests.Issues;

// https://github.com/SixLabors/ImageSharp.Drawing/discussions/396
public class Issue_396
{
    [Theory]
    [WithFile(TestImages.Png.Issue396Dragon, PixelTypes.Rgba32)]
    public void GeneratesGifForInspection<TPixel>(TestImageProvider<TPixel> provider)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        Image<Rgba32>[] handImages =
        [
            Image.Load<Rgba32>(TestFile.Create(TestImages.Png.Issue396Hand1).Bytes),
            Image.Load<Rgba32>(TestFile.Create(TestImages.Png.Issue396Hand2).Bytes)
        ];

        try
        {
            using Image<TPixel> inputImage = provider.GetImage();
            using Image<Rgba32> outputImage = new(handImages[0].Width, handImages[0].Height);

            const float scaleFactor = 0.6F;
            const float squashStepFactor = 0.22F;

            for (int i = 0; i < handImages.Length; i++)
            {
                using Image<Rgba32> frameImage = new(handImages[i].Width, handImages[i].Height);
                float squashFactorY = 1 - (i * squashStepFactor);

                frameImage.ProcessPixelRows(accessor =>
                {
                    Rgba32 gray = Color.Gray.ToPixel<Rgba32>();

                    for (int y = 0; y < accessor.Height; y++)
                    {
                        accessor.GetRowSpan(y).Fill(gray);
                    }
                });

                Rectangle targetRect = new(
                    (int)((frameImage.Width - (frameImage.Width * scaleFactor)) / 2),
                    (int)(frameImage.Height * (((1 - scaleFactor) / 2) + (scaleFactor * (1 - squashFactorY)))),
                    (int)(frameImage.Width * scaleFactor),
                    (int)(frameImage.Height * scaleFactor * squashFactorY));

                using Image<Rgba32> dragonFrame = inputImage.CloneAs<Rgba32>();
                dragonFrame.Mutate(context => context.Resize(targetRect.Size));

                frameImage.Mutate(context =>
                {
                    context.DrawImage(dragonFrame, targetRect.Location, 1F);
                    context.DrawImage(handImages[i], Point.Empty, 1F);
                });

                ImageFrame<Rgba32> outputFrame = outputImage.Frames.AddFrame(frameImage.Frames.RootFrame);
                GifFrameMetadata gifFrameMetadata = outputFrame.Metadata.GetGifMetadata();
                gifFrameMetadata.FrameDelay = 40;
                gifFrameMetadata.DisposalMode = FrameDisposalMode.RestoreToBackground;
            }

            for (int i = handImages.Length - 1; i > 0; i--)
            {
                outputImage.Frames.AddFrame(outputImage.Frames[i]);
            }

            outputImage.Frames.RemoveFrame(0);
            GifMetadata gifMetadata = outputImage.Metadata.GetGifMetadata();
            gifMetadata.RepeatCount = 0;

            Assert.Equal((handImages.Length * 2) - 1, outputImage.Frames.Count);
            foreach (ImageFrame<Rgba32> frame in outputImage.Frames)
            {
                Assert.Equal(FrameDisposalMode.RestoreToBackground, frame.Metadata.GetGifMetadata().DisposalMode);
            }

            outputImage.DebugSaveMultiFrame(provider, "Issue396-source-frames", appendPixelTypeToFileName: false);
            provider.Utility.SaveTestOutputFile(
                outputImage,
                "gif",
                new GifEncoder(),
                "Issue396-encoded",
                appendPixelTypeToFileName: false,
                appendSourceFileOrDescription: false);

            // Save and decode the GIF so the encoder's optimized frame diffs can be inspected separately.
            using MemoryStream gifStream = new();
            outputImage.SaveAsGif(gifStream);
            gifStream.Position = 0;

            using Image<Rgba32> decodedImage = Image.Load<Rgba32>(gifStream);
            decodedImage.DebugSaveMultiFrame(provider, "Issue396-decoded-frames", appendPixelTypeToFileName: false);

            Assert.Equal(outputImage.Frames.Count, decodedImage.Frames.Count);
        }
        finally
        {
            foreach (Image<Rgba32> handImage in handImages)
            {
                handImage.Dispose();
            }
        }
    }
}
