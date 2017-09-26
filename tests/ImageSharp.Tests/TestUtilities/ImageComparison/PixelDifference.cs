namespace SixLabors.ImageSharp.Tests.TestUtilities.ImageComparison
{
    using SixLabors.Primitives;

    public struct PixelDifference
    {
        public PixelDifference(
            Point position,
            int redDifference,
            int greenDifference,
            int blueDifference,
            int alphaDifference)
        {
            this.Position = position;
            this.RedDifference = redDifference;
            this.GreenDifference = greenDifference;
            this.BlueDifference = blueDifference;
            this.AlphaDifference = alphaDifference;
        }

        public PixelDifference(Point position, Rgba32 expected, Rgba32 actual)
            : this(position,
                (int)actual.R - (int)expected.R,
                (int)actual.G - (int)expected.G,
                (int)actual.B - (int)expected.B,
                (int)actual.A - (int)expected.A)
        {
        }

        public Point Position { get; }

        public int RedDifference { get; }
        public int GreenDifference { get; }
        public int BlueDifference { get; }
        public int AlphaDifference { get; }

        public override string ToString() =>
            $"[Δ({this.RedDifference},{this.GreenDifference},{this.BlueDifference},{this.AlphaDifference}) @ ({this.Position.X},{this.Position.Y})]";
    }
}