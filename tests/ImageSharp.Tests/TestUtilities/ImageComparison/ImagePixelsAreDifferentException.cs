namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    public class ImagePixelsAreDifferentException : ImagesSimilarityException
    {
        public ImageSimilarityReport[] Reports { get; }

        public ImagePixelsAreDifferentException(IEnumerable<ImageSimilarityReport> reports)
            : base("Images are not similar enough!" + StringifyReports(reports))
        {
            this.Reports = reports.ToArray();
        }

        private static string StringifyReports(IEnumerable<ImageSimilarityReport> reports)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(Environment.NewLine);

            int i = 0;
            foreach (ImageSimilarityReport r in reports)
            {
                sb.Append($"Report{i}: ");
                sb.Append(r);
                sb.Append(Environment.NewLine);
            }
            return sb.ToString();
        }
    }
}