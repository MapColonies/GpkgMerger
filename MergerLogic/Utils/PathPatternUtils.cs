using MergerLogic.DataTypes;

namespace MergerLogic.Utils
{
    public class PathPatternUtils
    {
        private string[] parts;
        private string[] keys;
        private Dictionary<string, string> keyValues;

        public PathPatternUtils(string pattern)
        {
            this.parts = new string[4];
            this.keys = new string[3];
            this.keyValues = new Dictionary<string, string>(9);
            this.compilePattern(pattern);
        }

        private void compilePattern(string pattern)
        {
            int lastPartEnd = 0;
            int partIndx = 0;
            int keyIdx = 0;
            bool isOpenedVariable = false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i] == '{')
                {
                    if (keyIdx > 2)
                    {
                        throw new Exception("invalid url pattern. pattern must have exacly 3 variables (coordinates)");
                    }
                    else if (isOpenedVariable)
                    {
                        throw new Exception("invalid url pattern. pattern missing closing '}'");
                    }
                    isOpenedVariable = true;
                    this.parts[partIndx] = pattern.Substring(lastPartEnd, i - lastPartEnd);
                    partIndx++;
                    lastPartEnd = i + 1;
                }
                else if (pattern[i] == '}')
                {
                    if (!isOpenedVariable)
                    {
                        throw new Exception("invalid url pattern. pattern missing openning '{'");
                    }
                    isOpenedVariable = false;
                    this.keys[keyIdx] = pattern.Substring(lastPartEnd, i - lastPartEnd);
                    keyIdx++;
                    lastPartEnd = i + 1;
                }
            }
            if (isOpenedVariable)
            {
                throw new Exception("invalid url pattern. pattern missing closing '}'");
            }
            if (partIndx != 3)
            {
                throw new Exception("invalid url pattern. pattern must have exacly 3 variables (coordinates)");
            }
            this.parts[partIndx] = pattern.Substring(lastPartEnd, pattern.Length - lastPartEnd);
        }
        public string renderUrlTemplate(Coord coords)
        {
            return this.renderUrlTemplate(coords.x, coords.y, coords.z);
        }

        public string renderUrlTemplate(int x, int y, int z)
        {
            this.prepareDictionary(x.ToString(), y.ToString(), z.ToString());
            return $"{this.parts[0]}{this.keyValues[this.keys[0]]}{this.parts[1]}{this.keyValues[this.keys[1]]}{this.parts[2]}{this.keyValues[this.keys[2]]}{this.parts[3]}";
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
