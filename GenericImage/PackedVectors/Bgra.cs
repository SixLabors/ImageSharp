using System;
using System.Numerics;

namespace GenericImage.PackedVectors
{
    public struct Bgra<TDepth> : IColor4<TDepth>
        where TDepth : struct
    {
        public TDepth X { get; set; }

        public TDepth Y { get; set; }

        public TDepth Z { get; set; }

        public TDepth W { get; set; }

        public void Add<TColor>(TColor value) where TColor : IColor<TDepth>
        {
            throw new NotImplementedException();
        }

        public void Multiply<TColor>(TColor value) where TColor : IColor<TDepth>
        {
            throw new NotImplementedException();
        }

        public void Multiply<TColor>(float value) where TColor : IColor<TDepth>
        {
            throw new NotImplementedException();
        }

        public void Divide<TColor>(TColor value) where TColor : IColor<TDepth>
        {
            throw new NotImplementedException();
        }

        public void PackVector(Vector4 vector)
        {
            throw new NotImplementedException();
        }

        public Vector4 ToVector()
        {
            throw new NotImplementedException();
        }

        public byte[] ToBytes()
        {
            throw new System.NotImplementedException();
        }
    }
}
