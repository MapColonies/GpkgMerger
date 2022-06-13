using MergerLogic.Batching;
using MergerLogic.DataTypes;
using MergerLogic.Utils;

namespace MergerCli
{
    internal class SourceParser : ISourceParser
    {
        private readonly HashSet<string> sourceTypes = new HashSet<string>(new[] { "fs", "s3", "gpkg", "wmts", "tms", "xyz" });
        private readonly IDataFactory _dataFactory;

        public SourceParser(IDataFactory dataFactory)
        {
            this._dataFactory = dataFactory;
        }

        public List<IData> ParseSources(string[] args, int batchSize)
        {
            List<IData> sources = new List<IData>();
            int idx = 2;
            bool isBase = true;
            while (idx < args.Length)
            {
                switch (args[idx].ToLower())
                {
                    case "fs":
                    case "s3":
                    case "gpkg":
                        try
                        {
                            sources.Add(this.ParseFileSource(args, ref idx, batchSize, isBase));
                        }
                        catch
                        {
                            string source = isBase ? "base" : "new";
                            Console.WriteLine($"{source} data does not exist.");
                            Environment.Exit(1);
                        }
                        break;
                    case "wmts":
                    case "xyz":
                    case "tms":
                        try
                        {
                            sources.Add(this.ParseHttpSource(args, ref idx, batchSize, isBase));
                        }
                        catch
                        {
                            string source = isBase ? "base" : "new";
                            Console.WriteLine($"{source} data does not exist.");
                            Environment.Exit(1);
                        }
                        break;
                    default:
                        throw new Exception($"Currently there is no support for the data type '{args[idx]}'");
                }
                isBase = false;
            }
            return sources;
        }

        private IData ParseFileSource(string[] args, ref int idx, int batchSize, bool isBase)
        {
            const int requiredParamCount = 2;
            const int optionalParamCount = 2;
            int paramCount = this.ValidateAndGetSourceLength(args, idx, requiredParamCount, optionalParamCount);
            string sourceType = args[idx];
            string sourcePath = args[idx + 1];
            bool isOneXOne = false;
            GridOrigin? origin = null;
            if (paramCount > requiredParamCount)
            {
                // not using set as it allows optional prams with dynamic values aka. --minZoom 3 
                var optionalParams = args.Skip(idx + requiredParamCount).Take(optionalParamCount).ToArray();
                this.ParseOptionalParameters(sourceType, sourcePath, ref isOneXOne, ref origin, optionalParams);

            }
            idx += paramCount;
            return this._dataFactory.CreateDatasource(sourceType, sourcePath, batchSize, isOneXOne, origin, isBase);
        }

        private IData ParseHttpSource(string[] args, ref int idx, int batchSize, bool isBase)
        {
            const int requiredParamCount = 5;
            const int optionalParamCount = 2;
            int paramCount = this.ValidateAndGetSourceLength(args, idx, requiredParamCount, optionalParamCount);
            string sourceType = args[idx];
            string sourcePath = args[idx + 1];
            string[] bboxParts = args[idx + 2].Split(',');
            int minZoom = int.Parse(args[idx + 3]);
            int maxZoom = int.Parse(args[idx + 4]);
            bool isOneXOne = false;
            GridOrigin? origin = null;
            if (paramCount > requiredParamCount)
            {
                // not using set as it allows optional prams with dynamic values aka. --minZoom 3 
                var optionalParams = args.Skip(idx + requiredParamCount).Take(optionalParamCount).ToArray();
                this.ParseOptionalParameters(sourceType, sourcePath, ref isOneXOne, ref origin, optionalParams);
            }
            Extent extent = new Extent
            {
                minX = double.Parse(bboxParts[0]),
                minY = double.Parse(bboxParts[1]),
                maxX = double.Parse(bboxParts[2]),
                maxY = double.Parse(bboxParts[3])
            };
            idx += paramCount;
            return this._dataFactory.CreateDatasource(sourceType, sourcePath, batchSize, isBase, extent, maxZoom, minZoom, isOneXOne, origin);
        }

        private void ParseOptionalParameters(string sourceType, string sourcePath, ref bool isOneXOne, ref GridOrigin? origin, string[] optionalParams)
        {
            if (optionalParams.Contains("--1x1"))
            {
                isOneXOne = true;
            }
            if (optionalParams.Contains("--UL"))
            {
                origin = GridOrigin.UPPER_LEFT;
            }
            if (optionalParams.Contains("--LL"))
            {
                if (origin != null)
                {
                    throw new Exception($"layer {sourceType} {sourcePath} cant be both UL and LL");
                }
                origin = GridOrigin.LOWER_LEFT;
            }
        }

        private int ValidateAndGetSourceLength(string[] args, int startIdx, int minExpectedParamCount, int optionalParamCount)
        {
            int i = startIdx + 1;
            // check required parameters
            for (; i < startIdx + minExpectedParamCount; i++)
            {
                if (i >= args.Length || this.sourceTypes.Contains(args[i].ToLower()))
                {
                    throw new Exception($"invalid source parameters for {args[startIdx]} {args[startIdx + 1]}");
                }
            }

            // check optional parameters
            for (; i <= startIdx + minExpectedParamCount + optionalParamCount; i++)
            {
                if (this.sourceTypes.Contains(args[i].ToLower()) || i == args.Length -1)
                {
                    return i - startIdx;
                }
            }
            throw new Exception($"invalid source parameters for {args[startIdx]} {args[startIdx + 1]}");
        }
    }
}
