namespace ImageSharp.Tests
{
    using System;
    using System.Text;

    using SixLabors.Primitives;

    public class ImagesSimilarityException : Exception
    {
        public ImagesSimilarityException(string message)
            : base(message)
        {
        }
    }

    public class ImageDimensionsMismatchException : ImagesSimilarityException
    {
        public ImageDimensionsMismatchException(Size expectedSize, Size actualSize)
            : base($"The image dimensions {actualSize} do not match the expected {expectedSize}!")
        {
            this.ExpectedSize = expectedSize;
            this.ActualSize = actualSize;
        }

        public Size ExpectedSize { get; }
        public Size ActualSize { get; }
    }

    public class ImagesAreNotEqualException : ImagesSimilarityException
    {
        public ImagesAreNotEqualException(Point[] differences)
            : base("Images are not equal! Differences: " + StringifyDifferences(differences))
        {
            this.Differences = differences;
        }

        public Point[] Differences { get; }
        
        private static string StringifyDifferences(Point[] differences)
        {
            var sb = new StringBuilder();
            int max = Math.Min(5, differences.Length);

            for (int i = 0; i < max; i++)
            {
                sb.Append(differences[i]);
                if (i < max - 1)
                {
                    sb.Append(';');
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