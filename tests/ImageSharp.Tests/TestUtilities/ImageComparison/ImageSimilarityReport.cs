namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class ImageSimilarityReport
    {
        public ImageSimilarityReport(IImageBase expectedImage, IImageBase actualImage, IEnumerable<PixelDifference> differences)
        {
            this.ExpectedImage = expectedImage;
            this.ActualImage = actualImage;
            this.Differences = differences.ToArray();
        }

        public static ImageSimilarityReport Empty =>
            new ImageSimilarityReport(null, null, Enumerable.Empty<PixelDifference>());

        public IImageBase ExpectedImage { get; }

        public IImageBase ActualImage { get; }

        public PixelDifference[] Differences { get; }

        public bool IsEmpty => this.Differences.Length == 0;

        public override string ToString()
        {
            return this.IsEmpty ? "[SimilarImages]" : StringifyDifferences(this.Differences);
        }

        private static string StringifyDifferences(PixelDifference[] differences)
        {
            var sb = new StringBuilder();
            int max = Math.Min(5, differences.Length);

            for (int i = 0; i < max; i++)
            {
                sb.Append(differences[i]);
                if (i < max - 1)
                {
                    sb.Append("; ");
                }
            }
            if (differences.Length >= 5)
            {
                sb.Append("...");
            }
            return sb.ToString();
        }
    }
}