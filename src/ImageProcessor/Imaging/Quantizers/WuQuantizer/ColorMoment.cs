
namespace nQuant
{
    public struct ColorMoment
    {
        public long Alpha;
        public long Red;
        public long Green;
        public long Blue;
        public int Weight;
        public float Moment;

        public static ColorMoment operator +(ColorMoment c1, ColorMoment c2)
        {
            c1.Alpha += c2.Alpha;
            c1.Red += c2.Red;
            c1.Green += c2.Green;
            c1.Blue += c2.Blue;
            c1.Weight += c2.Weight;
            c1.Moment += c2.Moment;
            return c1;
        }

        public static ColorMoment operator -(ColorMoment c1, ColorMoment c2)
        {
            c1.Alpha -= c2.Alpha;
            c1.Red -= c2.Red;
            c1.Green -= c2.Green;
            c1.Blue -= c2.Blue;
            c1.Weight -= c2.Weight;
            c1.Moment -= c2.Moment;
            return c1;
        }

        public static ColorMoment operator -(ColorMoment c1)
        {
            c1.Alpha = -c1.Alpha;
            c1.Red = -c1.Red;
            c1.Green = -c1.Green;
            c1.Blue = -c1.Blue;
            c1.Weight = -c1.Weight;
            c1.Moment = -c1.Moment;
            return c1;
        }

        public static ColorMoment operator +(ColorMoment m, Pixel p)
        {
            m.Alpha += p.Alpha;
            m.Red += p.Red;
            m.Green += p.Green;
            m.Blue += p.Blue;
            m.Weight++;
            m.Moment += p.Distance();
            return m;
        }

        public long Distance()
        {
            return (Alpha * Alpha) + (Red * Red) + (Green * Green) + (Blue * Blue);
        }

        public long WeightedDistance()
        {
            return Distance() / Weight;
        }

        public float Variance()
        {
            var result = Moment - ((float)Distance() / Weight);
            return float.IsNaN(result) ? 0.0f : result;
        }
    }
}