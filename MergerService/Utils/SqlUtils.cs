using MergerLogic.Utils;
using Npgsql;

namespace MergerService.Utils
{
    public class SqlUtils
    {
        private static string GetConnectionString()
        {
            string host = Configuration.Instance.GetConfiguration("DB", "host");
            string database = Configuration.Instance.GetConfiguration("DB", "database");
            string username = Configuration.Instance.GetConfiguration("DB", "username");
            string password = Configuration.Instance.GetConfiguration("DB", "password");
            return $"Host={host};Database={database};Username={username};Password={password}";
        }

        public static string GetTask()
        {
            // string cs = GetConnectionString();
            // using (var con = new NpgsqlConnection(cs))
            // {
            //     string sql = "";

            //     using (var cmd = new NpgsqlCommand(sql, con))
            //     {
            //         using (var reader = cmd.ExecuteReader(System.Data.CommandBehavior.SingleRow))
            //         {
            //             reader.Read();

            //         }
            //     }
            // }

            return @"{
                ""Batches"": [
                    {
                        ""maxX"": 312254,
                        ""maxY"": 85399,
                        ""minX"": 312239,
                        ""minY"": 85365,
                        ""zoom"": 18
                    }
                ],
                ""Sources"": [
                    {
                        ""Path"": ""output/temp"",
                        ""Type"": ""FS"",
                        ""Origin"": ""LL"",
                        ""Grid"": ""2X1""
                    },
                    {
                        ""Path"": ""area2.gpkg"",
                        ""Type"": ""GPKG""
                    }
                ]
            }";
        }
    }
}
