namespace ImageSharp.Tests.TestUtilities.ImageComparison
{
    using SixLabors.Primitives;

    public class ImageDimensionsMismatchException : ImagesSimilarityException
    {
        public ImageDimensionsMismatchException(Size expectedSize, Size actualSize)
            : base((string)$"The image dimensions {actualSize} do not match the expected {expectedSize}!")
        {
            this.ExpectedSize = expectedSize;
            this.ActualSize = actualSize;
        }

        public Size ExpectedSize { get; }
        public Size ActualSize { get; }
    }
}