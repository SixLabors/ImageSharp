
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

        public void Add(Pixel p)
        {
            Alpha += p.Alpha;
            Red += p.Red;
            Green += p.Green;
            Blue += p.Blue;
            Weight++;
            Moment += p.Amplitude();
        }

        public void AddFast(ref ColorMoment c2)
        {
            Alpha += c2.Alpha;
            Red += c2.Red;
            Green += c2.Green;
            Blue += c2.Blue;
            Weight += c2.Weight;
            Moment += c2.Moment;
        }
        
        public long Amplitude()
        {
            return (Alpha * Alpha) + (Red * Red) + (Green * Green) + (Blue * Blue);
        }

        public long WeightedDistance()
        {
            return this.Amplitude() / Weight;
        }

        public float Variance()
        {
            var result = Moment - ((float)this.Amplitude() / this.Weight);
            return float.IsNaN(result) ? 0.0f : result;
        }
    }
}