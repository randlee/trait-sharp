using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TraitEmulation.Runtime
{
    /// <summary>
    /// A mutable 2D view over a contiguous region of unmanaged structs,
    /// projected through a trait layout. Provides row/column indexing over
    /// data stored in row-major order.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public ref struct TraitSpan2D<TLayout>
        where TLayout : unmanaged
    {
        private readonly ref byte _reference;
        private readonly int _width;
        private readonly int _height;
        private readonly int _stride;
        private readonly int _rowStride;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan2D(ref byte reference, int stride, int width, int height)
        {
            _reference = ref reference;
            _stride = stride;
            _width = width;
            _height = height;
            _rowStride = stride * width;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private TraitSpan2D(ref byte reference, int stride, int width, int height, int rowStride)
        {
            _reference = ref reference;
            _stride = stride;
            _width = width;
            _height = height;
            _rowStride = rowStride;
        }

        public int Width
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _width;
        }

        public int Height
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _height;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _width * _height;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _width == 0 || _height == 0;
        }

        public ref TLayout this[int row, int col]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)row >= (uint)_height || (uint)col >= (uint)_width)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref Unsafe.As<byte, TLayout>(
                    ref Unsafe.AddByteOffset(ref _reference,
                        (nint)(row * _rowStride + col * _stride)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout> GetRow(int row)
        {
            if ((uint)row >= (uint)_height)
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(row * _rowStride)),
                _stride,
                _width);
        }

        public TraitSpan2D<TLayout> Slice(int rowStart, int colStart, int height, int width)
        {
            if ((uint)rowStart > (uint)_height || (uint)height > (uint)(_height - rowStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            if ((uint)colStart > (uint)_width || (uint)width > (uint)(_width - colStart))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan2D<TLayout>(
                ref Unsafe.AddByteOffset(ref _reference,
                    (nint)(rowStart * _rowStride + colStart * _stride)),
                _stride,
                width,
                height,
                _rowStride);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout> AsSpan() =>
            new(ref _reference, _stride, _width * _height);

        /// <summary>
        /// Fills all elements across all rows with the specified value.
        /// </summary>
        public void Fill(TLayout value)
        {
            for (int r = 0; r < _height; r++)
            {
                var row = GetRow(r);
                row.Fill(value);
            }
        }

        public void Clear() => Fill(default);

        public RowEnumerator EnumerateRows() => new(this);

        public ref struct RowEnumerator
        {
            private readonly TraitSpan2D<TLayout> _span;
            private int _row;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal RowEnumerator(TraitSpan2D<TLayout> span)
            {
                _span = span;
                _row = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => ++_row < _span._height;

            public TraitSpan<TLayout> Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => _span.GetRow(_row);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public RowEnumerator GetEnumerator() => this;
        }

        public static implicit operator ReadOnlyTraitSpan2D<TLayout>(TraitSpan2D<TLayout> span) =>
            new(ref span._reference, span._stride, span._width, span._height);

        public static TraitSpan2D<TLayout> Empty => default;
    }
}
