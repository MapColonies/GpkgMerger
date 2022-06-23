using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MergerLogicUnitTests.utils
{
    internal static class DynamicDataGenerator
    {
        public static IEnumerable<object[]> GeneratePrams(params object[][] parameters)
        {
            return BuildObjects(parameters, 0, new List<object>());
        }

        private static IEnumerable<object[]> BuildObjects(object[][] parameters, int idx, List<object> baseRow)
        {
            if (idx == parameters.Length)
            {
                yield return baseRow.ToArray();
                yield break;
            }

            int nextIdx = idx+1;
            foreach (var value in parameters[idx])
            {
                var clone = new List<object>(baseRow);
                clone.Add(value);
                foreach (var item in BuildObjects(parameters, nextIdx,clone))
                {
                    yield return item;
                }
            }
            
        }
    }
}
