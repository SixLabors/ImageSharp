using System;
using System.Numerics;

using GenericImage.Helpers;
using GenericImage.PackedVectors;

namespace GenericImage.PackedVectors
{
    public struct Bgra<TDepth> : IColor<TDepth>
        where TDepth : struct
    {
        private static readonly TDepth[] Components = new TDepth[4];

        public TDepth[] Values => Components;

        public void Add<TColor>(TColor value) where TColor : IColor<TDepth>
        {
            for (int i = 0; i < value.Values.Length; i++)
            {
                this.Values[i] = Operator<TDepth>.Add(this.Values[i], value.Values[i]);
            }
        }

        public void Multiply<TColor>(TColor value) where TColor : IColor<TDepth>
        {
            for (int i = 0; i < value.Values.Length; i++)
            {
                this.Values[i] = Operator<TDepth>.Multiply(this.Values[i], value.Values[i]);
            }
        }

        public void Multiply<TColor>(float value) where TColor : IColor<TDepth>
        {
            for (int i = 0; i < this.Values.Length; i++)
            {
                this.Values[i] = Operator<TDepth>.MultiplyF(this.Values[i], value);
            }
        }

        public void Divide<TColor>(TColor value) where TColor : IColor<TDepth>
        {
            for (int i = 0; i < value.Values.Length; i++)
            {
                this.Values[i] = Operator<TDepth>.Divide(this.Values[i], value.Values[i]);
            }
        }

        public void Divide<TColor>(float value) where TColor : IColor<TDepth>
        {
            for (int i = 0; i < this.Values.Length; i++)
            {
                this.Values[i] = Operator<TDepth>.DivideF(this.Values[i], value);
            }
        }

        public byte[] ToBytes()
        {
            if (typeof(TDepth) == typeof(byte))
            {
                return new[]
                {
                    (byte)(object)this.Values[0],
                    (byte)(object)this.Values[1],
                    (byte)(object)this.Values[2],
                    (byte)(object)this.Values[3]
                };
            }

            return null;
        }

        public void FromBytes(byte[] bytes)
        {
            if (bytes.Length != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(bytes));
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                this.Values[i] = (TDepth)(object)bytes[i];
            }
        }
    }
}
