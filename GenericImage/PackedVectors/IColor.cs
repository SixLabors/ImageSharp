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
        void Add<TColor>(TColor value) where TColor : IColor<TDepth>;

        void Multiply<TColor>(TColor value) where TColor : IColor<TDepth>;

        void Multiply<TColor>(float value) where TColor : IColor<TDepth>;

        void Divide<TColor>(TColor value) where TColor : IColor<TDepth>;
    }

    public interface IColor
    {
        void PackVector(Vector4 vector);

        Vector4 ToVector();

        byte[] ToBytes();
    }
}
