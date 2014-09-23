namespace ImageProcessor.Imaging.EdgeDetection
{
    public interface IEdgeFilter
    {
        double[,] HorizontalMatrix { get; }

        double[,] VerticalMatrix { get; }
    }
}
