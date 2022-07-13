using System;
using System.Collections.Generic;

namespace MergerLogicUnitTests.testUtils
{
    internal static class EqualityComparerFactory
    {
        private class LambdaEqualityComparer<T> : IEqualityComparer<T>
        {
            private Func<T?, T?, bool> _eqFunc;

            public LambdaEqualityComparer(Func<T?, T?, bool> compareFunc)
            {
                this._eqFunc = compareFunc;
            }

            public bool Equals(T? x, T? y)
            {
                return this._eqFunc(x, y);
            }

            public int GetHashCode(T obj)
            {
                return 0;
            }
        }

        internal static IEqualityComparer<T> Create<T>(Func<T?, T?, bool> compareFunc)
        {
            return new LambdaEqualityComparer<T>(compareFunc);
        }
    }
}
