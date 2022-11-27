using MergerLogic.DataTypes;
using System.Text.RegularExpressions;

namespace MergerLogic.Utils
{
    public class PathPatternUtils : IPathPatternUtils
    {
        private string[] _pattern;
        private readonly Dictionary<string, string> _keyValues;

        public PathPatternUtils(string pattern)
        {
            this._keyValues = new Dictionary<string, string>(9);
            this.CompilePattern(pattern);
        }

        private void CompilePattern(string pattern)
        {
            this._pattern = Regex
                .Split(pattern, "{(x)}|{(X)}|{(TileCol)}|{(y)}|{(Y)}|{(TileRow)}|{(TileMatrix)}|{(z)}|{(Z)}")
                .Where(str => !string.IsNullOrEmpty(str)).ToArray();

            if (this._pattern.Length == 6)
            {
                // add empty value to the end in case the patten ends with parameter and not constant
                // e.g. https://mapHost/xyz/png/myLayer/{z}/{x}/{y}
                // string as the render function requires length 7 array to work properly due to optimizations.
                this._pattern = this._pattern.Append("").ToArray();
            }
            if (this._pattern.Length != 7)
            {
                throw new Exception("invalid url pattern.");
            }
        }
        public string RenderUrlTemplate(Coord coords)
        {
            return this.RenderUrlTemplate(coords.X, coords.Y, coords.Z);
        }

        public string RenderUrlTemplate(int x, int y, int z)
        {
            this.prepareDictionary(x.ToString(), y.ToString(), z.ToString());
            return $"{this._pattern[0]}{this._keyValues[this._pattern[1]]}{this._pattern[2]}{this._keyValues[this._pattern[3]]}{this._pattern[4]}{this._keyValues[this._pattern[5]]}{this._pattern[6]}";
        }

        private void prepareDictionary(string x, string y, string z)
        {
            this._keyValues["x"] = x;
            this._keyValues["X"] = x;
            this._keyValues["TileCol"] = x;
            this._keyValues["y"] = y;
            this._keyValues["Y"] = y;
            this._keyValues["TileRow"] = y;
            this._keyValues["z"] = z;
            this._keyValues["Z"] = z;
            this._keyValues["TileMatrix"] = z;
        }
    }
}
