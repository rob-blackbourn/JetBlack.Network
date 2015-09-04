using System;
using System.Threading;

namespace JetBlack.Network.Common
{
    public static class DisposableValue
    {
        public static DisposableValue<T> Create<T>(T value, IDisposable disposable)
        {
            return new DisposableValue<T>(value, disposable);
        }
    }

    public struct DisposableValue<T> : IDisposable, IEquatable<DisposableValue<T>>
    {
        private IDisposable _disposable;

        public static readonly DisposableValue<T> Empty;

        public DisposableValue(T value, IDisposable disposable)
            : this()
        {
            Value = value;
            _disposable = disposable;
        }

        public T Value { get; private set; }

        public override int GetHashCode()
        {
            return Equals(Value, default(T)) ? 0 : Value.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is DisposableValue<T> && Equals((DisposableValue<T>)obj);
        }

        public bool Equals(DisposableValue<T> other)
        {
            return Equals(Value, other.Value) && Equals(_disposable, other._disposable);
        }

        public static bool operator ==(DisposableValue<T> a, DisposableValue<T> b)
        {
            return a.Equals(b);
        }

        public static bool operator !=(DisposableValue<T> a, DisposableValue<T> b)
        {
            return !a.Equals(b);
        }

        public void Dispose()
        {
            var disposable = Interlocked.CompareExchange(ref _disposable, null, _disposable);
            if (disposable != null)
                disposable.Dispose();
        }
    }
}
