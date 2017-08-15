namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;

    public class TolerantImageComparer : ImageComparer
    {
        public const float DefaultImageThreshold = 1.0f / (100 * 100 * 255);

        public TolerantImageComparer(float imageThreshold, int pixelThresholdInPixelByteSum = 0)
        {
            this.ImageThreshold = imageThreshold;
            this.PixelThresholdInPixelByteSum = pixelThresholdInPixelByteSum;
        }

        /// <summary>
        /// The maximal tolerated difference represented by a value between 0.0 and 1.0.
        /// Examples of percentage differences on a single pixel:
        /// 1. PixelA = (255,255,255,0) PixelB =(0,0,0,255) leads to 100% difference on a single pixel
        /// 2. PixelA = (255,255,255,0) PixelB =(255,255,255,255) leads to 25% difference on a single pixel
        /// 3. PixelA = (255,255,255,0) PixelB =(128,128,128,128) leads to 50% difference on a single pixel
        /// 
        /// The total differences is the sum of all pixel differences normalized by image dimensions!
        /// 
        /// ImageThresholdInPercents = 1.0/255 means that we allow one byte difference per channel on a 1x1 image
        /// ImageThresholdInPercents = 1.0/(100*100*255) means that we allow only one byte difference per channel on a 100x100 image
        /// </summary>
        public float ImageThreshold { get; }

        /// <summary>
        /// The threshold of the individual pixels before they acumulate towards the overall difference.
        /// For an individual <see cref="Rgba32"/> pixel the value it's calculated as: pixel.R + pixel.G + pixel.B + pixel.A
        /// </summary>
        public int PixelThresholdInPixelByteSum { get; }

        public override ImageSimilarityReport CompareImagesOrFrames<TPixelA, TPixelB>(ImageBase<TPixelA> expected, ImageBase<TPixelB> actual)
        {
            throw new NotImplementedException();
        }
    }
}