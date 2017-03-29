namespace ImageSharp.Benchmarks.Color
{
    using BenchmarkDotNet.Attributes;

    using Colourful;
    using Colourful.Conversion;

    using ImageSharp.Colors.Spaces;

    using ColorSpaceConverter = ImageSharp.Colors.Spaces.Conversion.ColorSpaceConverter;

    public class ColorspaceCieXyzToHunterLabConvert
    {
        private static readonly CieXyz CieXyz = new CieXyz(0.95047F, 1, 1.08883F);

        private static readonly XYZColor XYZColor = new XYZColor(0.95047, 1, 1.08883);

        private static readonly ColorSpaceConverter ColorSpaceConverter = new ColorSpaceConverter();

        private static readonly ColourfulConverter ColourfulConverter = new ColourfulConverter();


        [Benchmark(Baseline = true, Description = "Colourful Convert")]
        public HunterLabColor ColourfulConvert()
        {
            return ColourfulConverter.ToHunterLab(XYZColor);
        }

        [Benchmark(Description = "ImageSharp Convert")]
        public HunterLab ColorSpaceConvert()
        {
            return ColorSpaceConverter.ToHunterLab(CieXyz);
        }
    }
}