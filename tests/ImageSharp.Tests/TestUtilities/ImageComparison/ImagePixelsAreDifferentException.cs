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
            : base("Images are not similar enough! See 'Reports' for more details! ")
        {
            this.Reports = reports.ToArray();
        }
    }
}