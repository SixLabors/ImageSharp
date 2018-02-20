namespace SixLabors.ImageSharp.Memory
{
    /// <summary>
    /// Absolutely temporal.
    /// </summary>
    internal interface IGetArray<T>
        where T : struct
    {
        /// <summary>
        /// Absolutely temporal.
        /// </summary>
        T[] GetArray();
    }
}