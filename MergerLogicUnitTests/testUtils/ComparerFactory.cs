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
                else if ((x is null && y is T) || (y is null && x is T) || (x is T && y is T))
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
