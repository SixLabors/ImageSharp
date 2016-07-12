namespace ImageProcessorCore.Helpers
{
    interface INullOp<T>
    {
        bool HasValue(T value);
        bool AddIfNotNull(ref T accumulator, T value);
    }
    sealed class StructNullOp<T>
        : INullOp<T>, INullOp<T?>
        where T : struct
    {
        public bool HasValue(T value)
        {
            return true;
        }
        public bool AddIfNotNull(ref T accumulator, T value)
        {
            accumulator = Operator<T>.Add(accumulator, value);
            return true;
        }
        public bool HasValue(T? value)
        {
            return value.HasValue;
        }
        public bool AddIfNotNull(ref T? accumulator, T? value)
        {
            if (value.HasValue)
            {
                accumulator = accumulator.HasValue ?
                    Operator<T>.Add(
                        accumulator.GetValueOrDefault(),
                        value.GetValueOrDefault())
                    : value;
                return true;
            }
            return false;
        }
    }
    sealed class ClassNullOp<T>
        : INullOp<T>
        where T : class
    {
        public bool HasValue(T value)
        {
            return value != null;
        }
        public bool AddIfNotNull(ref T accumulator, T value)
        {
            if (value != null)
            {
                accumulator = accumulator == null ?
                    value : Operator<T>.Add(accumulator, value);
                return true;
            }
            return false;
        }
    }
}
