namespace ImageSharp.Benchmarks.Color
{
    using BenchmarkDotNet.Attributes;

    using Colourful;
    using Colourful.Conversion;

    using ImageSharp.Colors.Spaces;

    using ColorSpaceConverter = ImageSharp.Colors.Spaces.Conversion.ColorSpaceConverter;

    public class ColorspaceConvert
    {
        private static readonly CieXyz CieXyz = new CieXyz(0.95047F, 1, 1.08883F);

        private static readonly XYZColor XYZColor = new XYZColor(0.95047, 1, 1.08883);

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        private static readonly ColourfulConverter ColourfulConverter = new ColourfulConverter();


        [Benchmark(Baseline = true, Description = "Colourful Convert")]
        public LMSColor SystemDrawingColorEqual()
        {
            return ColourfulConverter.ToLMS(XYZColor);
        }

        [Benchmark(Description = "ImageSharp Convert")]
        public Lms ColorEqual()
        {
            return ColorSpaceConverter.ToLms(CieXyz);
        }
    }
}
