using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace TraitEmulation.Runtime
{
    /// <summary>
    /// A mutable view over a contiguous region of unmanaged structs,
    /// projected through a trait layout at a fixed byte offset with stride.
    /// Analogous to Span&lt;T&gt; but with offset/stride semantics.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public ref struct TraitSpan<TLayout>
        where TLayout : unmanaged
    {
        private readonly ref byte _reference;
        private readonly int _length;
        private readonly int _stride;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan(ref byte reference, int stride, int length)
        {
            _reference = ref reference;
            _stride = stride;
            _length = length;
        }

        public int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length;
        }

        public bool IsEmpty
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _length == 0;
        }

        /// <summary>
        /// Returns a mutable reference to the element at the specified index.
        /// </summary>
        public ref TLayout this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                if ((uint)index >= (uint)_length)
                    ThrowHelper.ThrowIndexOutOfRangeException();
                return ref Unsafe.As<byte, TLayout>(
                    ref Unsafe.AddByteOffset(ref _reference, (nint)(index * _stride)));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout> Slice(int start)
        {
            if ((uint)start > (uint)_length)
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(start * _stride)),
                _stride,
                _length - start);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TraitSpan<TLayout> Slice(int start, int length)
        {
            if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
                ThrowHelper.ThrowArgumentOutOfRangeException();
            return new TraitSpan<TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(start * _stride)),
                _stride,
                length);
        }

        /// <summary>
        /// Copies from this strided span into a contiguous destination.
        /// </summary>
        public void CopyTo(Span<TLayout> destination)
        {
            if ((uint)_length > (uint)destination.Length)
                ThrowHelper.ThrowArgumentException_DestinationTooShort();
            for (int i = 0; i < _length; i++)
            {
                destination[i] = this[i];
            }
        }

        /// <summary>
        /// Fills all elements with the specified value.
        /// Writes through the strided view into the source struct fields.
        /// </summary>
        public void Fill(TLayout value)
        {
            for (int i = 0; i < _length; i++)
            {
                this[i] = value;
            }
        }

        /// <summary>Clears all trait-view fields to default.</summary>
        public void Clear() => Fill(default);

        public TLayout[] ToArray()
        {
            if (_length == 0) return Array.Empty<TLayout>();
            var array = new TLayout[_length];
            CopyTo(array);
            return array;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new(this);

        public ref struct Enumerator
        {
            private readonly ref byte _reference;
            private readonly int _stride;
            private readonly int _length;
            private int _index;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal Enumerator(TraitSpan<TLayout> span)
            {
                _reference = ref span._reference;
                _stride = span._stride;
                _length = span._length;
                _index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                int index = _index + 1;
                if (index < _length)
                {
                    _index = index;
                    return true;
                }
                return false;
            }

            public ref TLayout Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => ref Unsafe.As<byte, TLayout>(
                    ref Unsafe.AddByteOffset(ref _reference, (nint)(_index * _stride)));
            }
        }

        /// <summary>
        /// Implicit conversion to ReadOnlyTraitSpan.
        /// </summary>
        public static implicit operator ReadOnlyTraitSpan<TLayout>(TraitSpan<TLayout> span) =>
            new(ref span._reference, span._stride, span._length);

        public static TraitSpan<TLayout> Empty => default;
    }
}
