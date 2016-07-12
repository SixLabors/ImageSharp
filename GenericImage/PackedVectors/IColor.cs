namespace GenericImage.PackedVectors
{
    using System.Numerics;

    public interface IColor4<T> : IColor<T>
        where T : struct
    {
        T X { get; set; }

        T Y { get; set; }

        T Z { get; set; }

        T W { get; set; }
    }

    public interface IColor<TDepth> : IColor
        where TDepth : struct
    {
        TDepth[] Values { get; }

        void Add<TColor>(TColor value) where TColor : IColor<TDepth>;

        void Multiply<TColor>(TColor value) where TColor : IColor<TDepth>;

        void Multiply<TColor>(float value) where TColor : IColor<TDepth>;

        void Divide<TColor>(TColor value) where TColor : IColor<TDepth>;

        void Divide<TColor>(float value) where TColor : IColor<TDepth>;

        void FromBytes(byte[] bytes);

        byte[] ToBytes();
    }

    public interface IColor
    {


    }
}
