using System;
using System.Collections;
using System.Collections.Generic;

namespace MergerLogicUnitTests.testUtils
{
    internal static class ComparerFactory
    {
        private class LambdaComparer<T> : IComparer<T>, IComparer
        {
            private Func<T?, T?, int> _eqFunc;

            public LambdaComparer(Func<T?, T?, int> compareFunc)
            {
                this._eqFunc = compareFunc;
            }


            public int Compare(T? x, T? y)
            {
                return this._eqFunc(x, y);
            }

            public int Compare(object? x, object? y)
            {
                if (x is null && y is null)
                    return 0;
                else if ((x is null && y?.GetType() == typeof(T)) || (y is null && x?.GetType() == typeof(T)) || (x?.GetType() == typeof(T) && y?.GetType() == typeof(T)))
                {
                    return this._eqFunc((T?)x, (T?)y);
                }
                else
                    throw new NotImplementedException();
            }
        }

        internal static IComparer Create<T>(Func<T?, T?, int> compareFunc)
        {
            return new LambdaComparer<T>(compareFunc);
        }
    }
}
