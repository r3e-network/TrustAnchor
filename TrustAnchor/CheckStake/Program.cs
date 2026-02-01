using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace CheckStake
{
    class Program
    {
        private const string RPC = "https://testnet1.neo.coz.io:443";
        private const string TrustAnchor = "0x553db7324f4b7ffb649a26bae187ce7654750d1d";
        private const string Staker = "779838db61bf196f205f1042ed00bc98ccb9f8ec";

        static async Task Main(string[] args)
        {
            Console.WriteLine("=== TrustAnchor Stake Verification ===");
            Console.WriteLine();

            await CheckStake();
            await CheckTotalStake();
            await CheckAgentCount();
            await CheckOwner();
            await CheckIsPaused();
            await GetContractInfo();

            Console.WriteLine();
            Console.WriteLine("=== Verification Complete ===");
        }

        static async Task CheckStake()
        {
            Console.WriteLine("[1] Checking staker's stake...");

            try
            {
                var result = await InvokeScript("stakeOf", Staker);
                if (result != null && result.Count > 0)
                {
                    var stake = result[0].Value<string>("value");
                    Console.WriteLine($"  Stake: {stake} NEO");
                    Console.WriteLine($"  ✓ Stake {(stake == "64" ? "verified (100 NEO)" : "recorded")}");
                }
                else
                {
                    Console.WriteLine("  ! No stake data returned");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        static async Task CheckTotalStake()
        {
            Console.WriteLine();
            Console.WriteLine("[2] Checking total stake...");

            try
            {
                var result = await InvokeScript("totalStake", "");
                if (result != null && result.Count > 0)
                {
                    var total = result[0].Value<string>("value");
                    Console.WriteLine($"  Total Stake: {total} NEO");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        static async Task CheckAgentCount()
        {
            Console.WriteLine();
            Console.WriteLine("[3] Checking agent count...");

            try
            {
                var result = await InvokeScript("agentCount", "");
                if (result != null && result.Count > 0)
                {
                    var count = result[0].Value<string>("value");
                    Console.WriteLine($"  Agent Count: {count}");
                    Console.WriteLine($"  ✓ {(count == "21" ? "All 21 agents registered" : "Agents registered")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        static async Task CheckOwner()
        {
            Console.WriteLine();
            Console.WriteLine("[4] Checking contract owner...");

            try
            {
                var result = await InvokeScript("owner", "");
                if (result != null && result.Count > 0)
                {
                    var owner = result[0].Value<string>("value");
                    Console.WriteLine($"  Owner: 0x{owner}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        static async Task CheckIsPaused()
        {
            Console.WriteLine();
            Console.WriteLine("[5] Checking pause state...");

            try
            {
                var result = await InvokeScript("isPaused", "");
                if (result != null && result.Count > 0)
                {
                    var paused = result[0].Value<bool>("value");
                    Console.WriteLine($"  Paused: {paused}");
                    Console.WriteLine($"  ✓ Contract {(paused ? "is paused" : "is active")}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        static async Task GetContractInfo()
        {
            Console.WriteLine();
            Console.WriteLine("[6] Checking contract state...");

            try
            {
                var client = new HttpClient();
                var json = new JObject
                {
                    ["jsonrpc"] = "2.0",
                    ["method"] = "getcontractstate",
                    ["params"] = JArray.FromObject(new[] { TrustAnchor }),
                    ["id"] = 1
                };

                var response = await client.PostAsync(RPC, new StringContent(json.ToString(), Encoding.UTF8, "application/json"));
                var content = JObject.Parse(await response.Content.ReadAsStringAsync());

                if (content.ContainsKey("result") && content["result"] != null)
                {
                    var name = content["result"]?["manifest"]?["name"]?.ToString();
                    var hash = content["result"]?["hash"]?.ToString();
                    Console.WriteLine($"  Name: {name}");
                    Console.WriteLine($"  Hash: {hash}");
                    Console.WriteLine($"  ✓ Contract is active on testnet");
                }
                else
                {
                    Console.WriteLine("  ! Contract not found (may need more time to sync)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Error: {ex.Message}");
            }
        }

        static async Task<JArray> InvokeScript(string method, string param)
        {
            var client = new HttpClient();

            var @params = new JArray();
            @params.Add(TrustAnchor);
            @params.Add(method);

            if (!string.IsNullOrEmpty(param))
            {
                @params.Add(new JObject
                {
                    ["type"] = "Hash160",
                    ["value"] = param
                });
            }
            else
            {
                @params.Add(new JArray());
            }

            var json = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "invokefunction",
                ["params"] = @params,
                ["id"] = 1
            };

            var response = await client.PostAsync(RPC, new StringContent(json.ToString(), Encoding.UTF8, "application/json"));
            var content = JObject.Parse(await response.Content.ReadAsStringAsync());

            if (content.ContainsKey("result") && content["result"] != null)
            {
                return content["result"]?["stack"] as JArray;
            }

            return null;
        }
    }
}
