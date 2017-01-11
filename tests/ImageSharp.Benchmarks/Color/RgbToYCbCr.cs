namespace ImageSharp.Benchmarks
{
    using System.Numerics;

    using BenchmarkDotNet.Attributes;

    public class RgbToYCbCr
    {
        [Benchmark(Baseline = true, Description = "Floating Point Conversion")]
        public Vector3 RgbaToYcbCr()
        {
            Vector3 v = new Vector3(255);

            float yy = (0.299F * v.X) + (0.587F * v.Y) + (0.114F * v.Z);
            float cb = 128 + ((-0.168736F * v.X) - (0.331264F * v.Y) + (0.5F * v.Z));
            float cr = 128 + ((0.5F * v.X) - (0.418688F * v.Y) - (0.081312F * v.Z));

            return new Vector3(yy, cb, cr);
        }

        [Benchmark(Description = "Scaled Integer Conversion")]
        public Vector3 RgbaToYcbCrScaled()
        {
            int r = 255;
            int g = 255;
            int b = 255;

            // Scale by 1024 and truncate value
            int y0 = 306 * r; // 0.299F
            int y1 = 601 * g; // 0.587F
            int y2 = 116 * b; // 0.114F

            int cb0 = -172 * r; // -0.168736F
            int cb1 = 339 * g; // 0.331264F
            int cb2 = 512 * b; // 0.5F

            int cr0 = 512 * r; // 0.5F
            int cr1 = 428 * g; // 0.418688F
            int cr2 = 83 * b; // 0.081312F

            float yy = ((y0) + (y1) + (y2)) >> 10;
            float cb = 128 + ((((cb0) - (cb1) + (cb2))) >> 10);
            float cr = 128 + ((((cr0) - (cr1) - (cr2))) >> 10);

            return new Vector3(yy, cb, cr);
        }
    }
}