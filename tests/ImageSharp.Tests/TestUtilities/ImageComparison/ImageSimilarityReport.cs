namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using SixLabors.ImageSharp.PixelFormats;

    public class ImageSimilarityReport
    {
        protected ImageSimilarityReport(
            object expectedImage,
            object actualImage,
            IEnumerable<PixelDifference> differences,
            float? totalNormalizedDifference = null)
        {
            this.ExpectedImage = expectedImage;
            this.ActualImage = actualImage;
            this.TotalNormalizedDifference = totalNormalizedDifference;
            this.Differences = differences.ToArray();
        }
        public object ExpectedImage { get; }

        public object ActualImage { get; }

        // TODO: This should not be a nullable value!
        public float? TotalNormalizedDifference { get; }

        public string DifferencePercentageString => this.TotalNormalizedDifference.HasValue
                                                  ? $"{this.TotalNormalizedDifference.Value * 100:0.0000}%"
                                                  : "?";

        public PixelDifference[] Differences { get; }

        public bool IsEmpty => this.Differences.Length == 0;

        public override string ToString()
        {
            return this.IsEmpty ? "[SimilarImages]" : this.PrintDifference();
        }

        private string PrintDifference()
        {
            var sb = new StringBuilder();
            if (this.TotalNormalizedDifference.HasValue)
            {
                sb.AppendLine($"Total difference: {this.DifferencePercentageString}");
            }
            int max = Math.Min(5, this.Differences.Length);

            for (int i = 0; i < max; i++)
            {
                sb.Append(this.Differences[i]);
                if (i < max - 1)
                {
                    sb.Append("; ");
                }
            }
            if (this.Differences.Length >= 5)
            {
                sb.Append("...");
            }
            return sb.ToString();
        }
    }

    public class ImageSimilarityReport<TPixelA, TPixelB> : ImageSimilarityReport
        where TPixelA : struct, IPixel<TPixelA>
        where TPixelB : struct, IPixel<TPixelB>
    {
        public ImageSimilarityReport(
            ImageFrame<TPixelA> expectedImage,
            ImageFrame<TPixelB> actualImage,
            IEnumerable<PixelDifference> differences,
            float? totalNormalizedDifference = null)
            : base(expectedImage, actualImage, differences, totalNormalizedDifference)
        {
        }

        public static ImageSimilarityReport<TPixelA, TPixelB> Empty =>
            new ImageSimilarityReport<TPixelA, TPixelB>(null, null, Enumerable.Empty<PixelDifference>(), 0f);

        public new ImageFrame<TPixelA> ExpectedImage => (ImageFrame<TPixelA>)base.ExpectedImage;

        public new ImageFrame<TPixelB> ActualImage => (ImageFrame<TPixelB>)base.ActualImage;
    }
}