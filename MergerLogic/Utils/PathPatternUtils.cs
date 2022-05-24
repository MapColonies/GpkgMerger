using MergerLogic.DataTypes;
using System.Text.RegularExpressions;

namespace MergerLogic.Utils
{
    public class PathPatternUtils
    {
        private string[] _pattern;
        private Dictionary<string, string> keyValues;

        public PathPatternUtils(string pattern)
        {
            this.keyValues = new Dictionary<string, string>(9);
            this.compilePattern(pattern);
        }

        private void compilePattern(string pattern)
        {
            this._pattern = Regex.Split(pattern, "{(x)}|{(X)}|{(TileCol)}|{(y)}|{(Y)}|{(TileRow)}|{(TileMatrix)}|{(z)}|{(Z)}");
            if (this._pattern.Length == 6)
            {
                this._pattern = this._pattern.Append("").ToArray();
            }
            if (this._pattern.Length != 7)
            {
                throw new Exception("invalid url pattern.");
            }
        }
        public string renderUrlTemplate(Coord coords)
        {
            return this.renderUrlTemplate(coords.x, coords.y, coords.z);
        }

        public string renderUrlTemplate(int x, int y, int z)
        {
            this.prepareDictionary(x.ToString(), y.ToString(), z.ToString());
            return $"{this._pattern[0]}{this.keyValues[this._pattern[1]]}{this._pattern[2]}{this.keyValues[this._pattern[3]]}{this._pattern[4]}{this.keyValues[this._pattern[5]]}{this._pattern[6]}";
        }

        private void prepareDictionary(string x, string y, string z)
        {
            this.keyValues["x"] = x;
            this.keyValues["X"] = x;
            this.keyValues["TileCol"] = x;
            this.keyValues["y"] = y;
            this.keyValues["Y"] = y;
            this.keyValues["TileRow"] = y;
            this.keyValues["z"] = z;
            this.keyValues["Z"] = z;
            this.keyValues["TileMatrix"] = z;
        }
    }
}
