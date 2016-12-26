namespace ImageSharp.Formats
{
    using System.Buffers;

    internal class CleanPooler<T>
    {
        private static readonly ArrayPool<T> Pool = ArrayPool<T>.Create();

        public static T[] RentCleanArray(int minimumLength) => Pool.Rent(minimumLength);

        public static void ReturnArray(T[] array) => Pool.Return(array, true);
    }
}