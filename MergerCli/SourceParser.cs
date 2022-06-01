using MergerLogic.Batching;
using MergerLogic.DataTypes;

namespace MergerCli
{
    internal class SourceParser
    {
        private readonly HashSet<string> sourceTypes = new HashSet<string>(new[] { "fs", "s3", "gpkg", "wmts", "tms", "xyz" });

        public List<Data> ParseSources(string[] args, int batchSize)
        {
            List<Data> sources = new List<Data>();
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

        private Data ParseFileSource(string[] args, ref int idx, int batchSize, bool isBase)
        {
            const int requiredParamCount = 2;
            const int optionalParamCount = 2;
            int paramCount = this.ValidateAndGetSourceLength(args, idx, requiredParamCount, optionalParamCount);
            string sourceType = args[idx];
            string sourcePath = args[idx + 1];
            bool isOneXOne = false;
            // not using set as it allows optional prams with dynamic values aka. --minZoom 3 
            var optionalParams = args.Skip(idx + requiredParamCount).Take(optionalParamCount).ToArray();
            if (optionalParams.Contains("--1x1"))
            {
                isOneXOne = true;
            }
            TileGridOrigin? origin = null;
            if (optionalParams.Contains("--UL"))
            {
                origin = TileGridOrigin.UPPER_LEFT;
            }
            if (optionalParams.Contains("--UL"))
            {
                if (origin != null)
                {
                    throw new Exception($"layer {sourceType} {sourcePath} cant be both UL and LL");
                }
                origin = TileGridOrigin.UPPER_LEFT;
            }

            idx += paramCount;
            return Data.CreateDatasource(sourceType, sourcePath, batchSize, isOneXOne, origin, isBase);
        }

        private Data ParseHttpSource(string[] args, ref int idx, int batchSize, bool isBase)
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
            if (paramCount > requiredParamCount)
            {
                // not using set as it allows optional prams with dynamic values aka. --minZoom 3 
                var optionalParams = args.Skip(idx + requiredParamCount).Take(optionalParamCount).ToArray();
                if (optionalParams.Contains("--1x1"))
                {
                    isOneXOne = true;
                }
                TileGridOrigin? origin = null;
                if (optionalParams.Contains("--UL"))
                {
                    origin = TileGridOrigin.UPPER_LEFT;
                }
                if (optionalParams.Contains("--UL"))
                {
                    if (origin != null)
                    {
                        throw new Exception($"layer {sourceType} {sourcePath} cant be both UL and LL");
                    }
                    origin = TileGridOrigin.UPPER_LEFT;
                }
            }
            Extent extent = new Extent
            {
                minX = double.Parse(bboxParts[0]),
                minY = double.Parse(bboxParts[1]),
                maxX = double.Parse(bboxParts[2]),
                maxY = double.Parse(bboxParts[3])
            };
            idx += paramCount;
            return Data.CreateDatasource(sourceType, sourcePath, batchSize, isBase, extent, maxZoom, minZoom, isOneXOne);
        }

        private int ValidateAndGetSourceLength(string[] args, int startIdx, int minExpectedParamCount, int optionalParamCount)
        {
            // check required parameters
            for (int i = startIdx + 1; i < startIdx + minExpectedParamCount; i++)
            {
                if (this.sourceTypes.Contains(args[i].ToLower()))
                {
                    throw new Exception($"invalid source parameters for {args[startIdx]} {args[startIdx + 1]}");
                }
            }

            // check optional parameters
            for (int i = minExpectedParamCount; i <= minExpectedParamCount + optionalParamCount; i++)
            {
                if (this.sourceTypes.Contains(args[i].ToLower()))
                {
                    return i;
                }
            }
            throw new Exception($"invalid source parameters for {args[startIdx]} {args[startIdx + 1]}");
        }
    }
}
