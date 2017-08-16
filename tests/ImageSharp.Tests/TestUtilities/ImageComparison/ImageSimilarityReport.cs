namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class ImageSimilarityReport
    {
        public ImageSimilarityReport(
            IImageBase expectedImage,
            IImageBase actualImage,
            IEnumerable<PixelDifference> differences,
            float? totalNormalizedDifference = null)
        {
            this.ExpectedImage = expectedImage;
            this.ActualImage = actualImage;
            this.TotalNormalizedDifference = totalNormalizedDifference;
            this.Differences = differences.ToArray();
        }

        public static ImageSimilarityReport Empty =>
            new ImageSimilarityReport(null, null, Enumerable.Empty<PixelDifference>(), null);

        public float? TotalNormalizedDifference { get; }

        public IImageBase ExpectedImage { get; }

        public IImageBase ActualImage { get; }

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
                sb.AppendLine($"Total difference: {this.TotalNormalizedDifference.Value * 100:0.00}%");
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
}