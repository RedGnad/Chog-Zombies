using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Reown.AppKit.Unity;
using UnityEngine;

namespace ChogZombies.Reown
{
    public class PythEntropyRandomnessService : MonoBehaviour
    {
        [SerializeField] string consumerContractAddress;
        [SerializeField] int chainId = 143;
        [SerializeField] int requestTimeoutSeconds = 120;
        [SerializeField] int pollIntervalMilliseconds = 1000;

        const string ConsumerAbiJson = "[" +
                                       "{\"inputs\":[],\"name\":\"getRequiredFee\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"}," +
                                       "{\"inputs\":[],\"name\":\"requestRunSeed\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"payable\",\"type\":\"function\"}," +
                                       "{\"inputs\":[{\"internalType\":\"address\",\"name\":\"player\",\"type\":\"address\"}],\"name\":\"getLatestRunSeedValue\",\"outputs\":[{\"internalType\":\"bytes32\",\"name\":\"\",\"type\":\"bytes32\"}],\"stateMutability\":\"view\",\"type\":\"function\"}," +
                                       "{\"inputs\":[],\"name\":\"requestBossLootRandom\",\"outputs\":[{\"internalType\":\"uint64\",\"name\":\"\",\"type\":\"uint64\"}],\"stateMutability\":\"payable\",\"type\":\"function\"}," +
                                       "{\"inputs\":[{\"internalType\":\"address\",\"name\":\"player\",\"type\":\"address\"}],\"name\":\"getLatestBossLootRandom\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"ready\",\"type\":\"bool\"},{\"internalType\":\"bytes32\",\"name\":\"value\",\"type\":\"bytes32\"},{\"internalType\":\"uint64\",\"name\":\"sequenceNumber\",\"type\":\"uint64\"}],\"stateMutability\":\"view\",\"type\":\"function\"}" +
                                       "]";

        public async Task<int> RequestRunSeedAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(consumerContractAddress))
                throw new InvalidOperationException("consumerContractAddress not set");

            await EnsureAppKitInitializedAsync(ct);
            await EnsureWalletConnectedAsync(ct);
            EnsureExpectedChain();

            string playerAddress = AppKit.Account.Address;
            Debug.Log($"[Pyth] RequestRunSeedAsync: player={playerAddress} consumer={consumerContractAddress} chainId={chainId}");

            Debug.Log("[Pyth] Calling getRequiredFee...");
            BigInteger fee = await AppKit.Evm.ReadContractAsync<BigInteger>(
                consumerContractAddress,
                ConsumerAbiJson,
                "getRequiredFee"
            );

            Debug.Log($"[Pyth] getRequiredFee returned: {fee}");

            if (fee < 0)
                fee = BigInteger.Zero;

            Debug.Log("[Pyth] Sending requestRunSeed transaction...");
            string txHash = await AppKit.Evm.WriteContractAsync(
                consumerContractAddress,
                ConsumerAbiJson,
                "requestRunSeed",
                fee,
                default,
                Array.Empty<object>()
            );

            Debug.Log($"[Pyth] requestRunSeed tx sent. hash={txHash}");

            await AppKit.Evm.GetTransactionReceiptAsync(txHash, ct: ct);

            Debug.Log("[Pyth] requestRunSeed tx confirmed. Waiting for Entropy callback...");

            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1, requestTimeoutSeconds));
            int attempts = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Pyth Entropy run seed request timed out");

                attempts++;
                if (attempts == 1 || attempts % 10 == 0)
                {
                    var remaining = (int)(deadline - DateTime.UtcNow).TotalSeconds;
                    Debug.Log($"[Pyth] Polling getLatestRunSeedValue (attempt={attempts}, remaining={remaining}s)...");
                }

                string hexValue = await AppKit.Evm.ReadContractAsync<string>(
                    consumerContractAddress,
                    ConsumerAbiJson,
                    "getLatestRunSeedValue",
                    new object[] { playerAddress }
                );

                if (!string.IsNullOrWhiteSpace(hexValue) && !IsZeroHex(hexValue))
                {
                    Debug.Log($"[Pyth] getLatestRunSeedValue returned non-zero: {hexValue}");
                    byte[] bytes = HexToBytes(hexValue);
                    if (bytes != null && bytes.Length > 0)
                    {
                        BigInteger value = ToBigInteger(bytes);
                        int seed = DeriveSeed(value);
                        return seed;
                    }
                }

                await DelayCooperative(pollIntervalMilliseconds, ct);
            }
        }

        public async Task<int> RequestBossLootSeedAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(consumerContractAddress))
                throw new InvalidOperationException("consumerContractAddress not set");

            await EnsureAppKitInitializedAsync(ct);
            await EnsureWalletConnectedAsync(ct);
            EnsureExpectedChain();

            string playerAddress = AppKit.Account.Address;
            Debug.Log($"[Pyth] RequestBossLootSeedAsync: player={playerAddress} consumer={consumerContractAddress} chainId={chainId}");

            Debug.Log("[Pyth] Calling getRequiredFee for boss loot...");
            BigInteger fee = await AppKit.Evm.ReadContractAsync<BigInteger>(
                consumerContractAddress,
                ConsumerAbiJson,
                "getRequiredFee"
            );

            Debug.Log($"[Pyth] getRequiredFee (boss loot) returned: {fee}");

            if (fee < 0)
                fee = BigInteger.Zero;

            Debug.Log("[Pyth] Sending requestBossLootRandom transaction...");
            string txHash = await AppKit.Evm.WriteContractAsync(
                consumerContractAddress,
                ConsumerAbiJson,
                "requestBossLootRandom",
                fee,
                default,
                Array.Empty<object>()
            );

            Debug.Log($"[Pyth] requestBossLootRandom tx sent. hash={txHash}");

            await AppKit.Evm.GetTransactionReceiptAsync(txHash, ct: ct);

            Debug.Log("[Pyth] requestBossLootRandom tx confirmed. Waiting for Entropy callback (boss loot)...");

            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1, requestTimeoutSeconds));
            int attempts = 0;
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    throw new TimeoutException("Pyth Entropy boss loot request timed out");

                attempts++;
                if (attempts == 1 || attempts % 10 == 0)
                {
                    var remaining = (int)(deadline - DateTime.UtcNow).TotalSeconds;
                    Debug.Log($"[Pyth] Polling getLatestBossLootRandom (attempt={attempts}, remaining={remaining}s)...");
                }

                object data = await AppKit.Evm.ReadContractAsync<object>(
                    consumerContractAddress,
                    ConsumerAbiJson,
                    "getLatestBossLootRandom",
                    new object[] { playerAddress }
                );

                string hexValue;
                if (TryExtractBossLootValue(data, out hexValue))
                {
                    Debug.Log($"[Pyth] getLatestBossLootRandom returned ready value: {hexValue}");
                    if (!string.IsNullOrWhiteSpace(hexValue) && !IsZeroHex(hexValue))
                    {
                        byte[] bytes = HexToBytes(hexValue);
                        if (bytes != null && bytes.Length > 0)
                        {
                            BigInteger value = ToBigInteger(bytes);
                            int seed = DeriveSeed(value);
                            return seed;
                        }
                    }
                }

                await DelayCooperative(pollIntervalMilliseconds, ct);
            }
        }

        static bool TryExtractBossLootValue(object data, out string hexValue)
        {
            hexValue = null;
            if (data == null)
                return false;

            if (data is JObject obj)
            {
                bool ready = false;
                JToken readyToken = obj["ready"];
                if (readyToken != null)
                    ready = readyToken.Type == JTokenType.Boolean ? readyToken.Value<bool>() : false;

                string value = obj["value"]?.ToString();
                if (ready && !string.IsNullOrWhiteSpace(value))
                {
                    hexValue = value;
                    return true;
                }
                return false;
            }

            if (data is object[] arr)
            {
                if (arr.Length < 2)
                    return false;

                bool ready = false;
                object readyObj = arr[0];
                if (readyObj is bool b)
                    ready = b;
                else if (readyObj is string s && bool.TryParse(s, out var parsed))
                    ready = parsed;

                string value = arr[1]?.ToString();
                if (ready && !string.IsNullOrWhiteSpace(value))
                {
                    hexValue = value;
                    return true;
                }
                return false;
            }

            if (data is JArray jArr)
            {
                if (jArr.Count < 2)
                    return false;

                bool ready = false;
                JToken readyToken = jArr[0];
                if (readyToken != null && readyToken.Type == JTokenType.Boolean)
                    ready = readyToken.Value<bool>();

                string value = jArr[1]?.ToString();
                if (ready && !string.IsNullOrWhiteSpace(value))
                {
                    hexValue = value;
                    return true;
                }
            }

            return false;
        }

        static async Task DelayCooperative(int milliseconds, CancellationToken ct)
        {
            if (milliseconds <= 0)
            {
                await Task.Yield();
                return;
            }

            var deadlineUtc = DateTime.UtcNow + TimeSpan.FromMilliseconds(milliseconds);
            while (DateTime.UtcNow < deadlineUtc)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }
        }

        async Task EnsureAppKitInitializedAsync(CancellationToken ct)
        {
            if (AppKit.IsInitialized)
                return;

            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(60);
            while (!AppKit.IsInitialized)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("AppKit not initialized (timeout)");
                await Task.Delay(100, ct);
            }
        }

        async Task EnsureWalletConnectedAsync(CancellationToken ct)
        {
            if (AppKit.IsAccountConnected)
                return;

            DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(180);
            while (!AppKit.IsAccountConnected)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Wallet not connected (timeout)");
                await Task.Delay(100, ct);
            }
        }

        void EnsureExpectedChain()
        {
            string expectedChainId = $"eip155:{chainId}";
            string activeChainId = null;
            try
            {
                activeChainId = AppKit.NetworkController?.ActiveChain?.ChainId;
            }
            catch
            {
                activeChainId = null;
            }

            if (!string.IsNullOrWhiteSpace(activeChainId) && !string.Equals(activeChainId, expectedChainId, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Wrong network. Expected {expectedChainId} but wallet is on {activeChainId}.");
        }

        static bool IsZeroHex(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return true;
            if (hex == "0x" || hex == "0X")
                return true;
            int start = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            for (int i = start; i < hex.Length; i++)
            {
                char c = hex[i];
                if (c != '0')
                    return false;
            }
            return true;
        }

        static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return Array.Empty<byte>();
            int start = hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
            int len = hex.Length - start;
            if (len <= 0)
                return Array.Empty<byte>();
            if (len % 2 != 0)
            {
                hex = hex.Insert(start, "0");
                len++;
            }
            byte[] bytes = new byte[len / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                int hi = ParseNybble(hex[start + 2 * i]);
                int lo = ParseNybble(hex[start + 2 * i + 1]);
                bytes[i] = (byte)((hi << 4) | lo);
            }
            return bytes;
        }

        static int ParseNybble(char c)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (c >= 'a' && c <= 'f')
                return c - 'a' + 10;
            if (c >= 'A' && c <= 'F')
                return c - 'A' + 10;
            throw new FormatException("Invalid hex character");
        }

        static BigInteger ToBigInteger(byte[] bigEndian)
        {
            if (bigEndian == null || bigEndian.Length == 0)
                return BigInteger.Zero;
            byte[] tmp = new byte[bigEndian.Length + 1];
            for (int i = 0; i < bigEndian.Length; i++)
                tmp[i] = bigEndian[bigEndian.Length - 1 - i];
            return new BigInteger(tmp);
        }

        static int DeriveSeed(BigInteger value)
        {
            if (value.Sign < 0)
                value = BigInteger.Negate(value);
            BigInteger mod = value % int.MaxValue;
            return (int)mod;
        }
    }
}
