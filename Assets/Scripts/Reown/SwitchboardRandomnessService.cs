using System;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Reown.AppKit.Unity;
using UnityEngine;
using UnityEngine.Networking;

namespace ChogZombies.Reown
{
    public class SwitchboardRandomnessService : MonoBehaviour
    {
        [Header("Switchboard")]
        [SerializeField] string switchboardContractAddress = "0xB7F03eee7B9F56347e32cC71DaD65B303D5a0E67"; // Monad Mainnet randomness per official docs
        [SerializeField] int chainId = 143; // Monad Mainnet chainId

        [Header("Crossbar")]
        [SerializeField] string crossbarUrl = "https://crossbar.switchboard.xyz";

        [Header("Defaults")]
        [SerializeField] int defaultMinSettlementDelaySeconds = 5;
        [SerializeField] int settlementDelayBufferSeconds = 10;

        [Header("Reown")]
        [SerializeField] int appKitInitTimeoutSeconds = 30;
        [SerializeField] int walletConnectTimeoutSeconds = 120;

        const string SwitchboardAbiJson = "[" +
                                         "{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"randomnessId\",\"type\":\"bytes32\"},{\"internalType\":\"uint64\",\"name\":\"minSettlementDelay\",\"type\":\"uint64\"}],\"name\":\"createRandomness\",\"outputs\":[{\"internalType\":\"address\",\"name\":\"oracle\",\"type\":\"address\"}],\"stateMutability\":\"nonpayable\",\"type\":\"function\"}," +
                                         "{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"randomnessId\",\"type\":\"bytes32\"}],\"name\":\"getRandomness\",\"outputs\":[{\"components\":[{\"internalType\":\"bytes32\",\"name\":\"randId\",\"type\":\"bytes32\"},{\"internalType\":\"uint256\",\"name\":\"createdAt\",\"type\":\"uint256\"},{\"internalType\":\"address\",\"name\":\"authority\",\"type\":\"address\"},{\"internalType\":\"uint256\",\"name\":\"rollTimestamp\",\"type\":\"uint256\"},{\"internalType\":\"uint64\",\"name\":\"minSettlementDelay\",\"type\":\"uint64\"},{\"internalType\":\"address\",\"name\":\"oracle\",\"type\":\"address\"},{\"internalType\":\"uint256\",\"name\":\"value\",\"type\":\"uint256\"},{\"internalType\":\"uint256\",\"name\":\"settledAt\",\"type\":\"uint256\"}],\"internalType\":\"tuple\",\"name\":\"\",\"type\":\"tuple\"}],\"stateMutability\":\"view\",\"type\":\"function\"}," +
                                         "{\"inputs\":[{\"internalType\":\"bytes32\",\"name\":\"randomnessId\",\"type\":\"bytes32\"}],\"name\":\"isRandomnessReady\",\"outputs\":[{\"internalType\":\"bool\",\"name\":\"ready\",\"type\":\"bool\"}],\"stateMutability\":\"view\",\"type\":\"function\"}," +
                                         "{\"inputs\":[{\"internalType\":\"bytes\",\"name\":\"encodedRandomness\",\"type\":\"bytes\"}],\"name\":\"settleRandomness\",\"outputs\":[],\"stateMutability\":\"payable\",\"type\":\"function\"}," +
                                         "{\"inputs\":[],\"name\":\"updateFee\",\"outputs\":[{\"internalType\":\"uint256\",\"name\":\"\",\"type\":\"uint256\"}],\"stateMutability\":\"view\",\"type\":\"function\"}" +
                                         "]";

        [Serializable]
        public class RandomnessResult
        {
            public string RandomnessId;
            public string CreateTxHash;
            public string SettleTxHash;
            public BigInteger Value;
            public BigInteger RollTimestamp;
            public BigInteger MinSettlementDelay;
            public string Oracle;
        }

        async Task EnsureExpectedChainAndContractAsync(CancellationToken ct)
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
            {
                throw new InvalidOperationException($"Wrong network. Expected {expectedChainId} but wallet is on {activeChainId}. Switch to the correct Monad network before requesting VRF.");
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            try
            {
                string bytecode = await global::Reown.AppKit.Unity.WebGl.Wagmi.WagmiInterop.GetBytecodeAsync(switchboardContractAddress);
                Debug.Log($"[VRF] Switchboard bytecode length: {(bytecode == null ? -1 : bytecode.Length)} (prefix={(bytecode != null && bytecode.Length >= 2 ? bytecode.Substring(0, 2) : "")})");
                if (string.Equals(bytecode, "0x", StringComparison.OrdinalIgnoreCase))
                {
                    string chainInfo = string.IsNullOrWhiteSpace(activeChainId) ? "(unknown chain)" : activeChainId;
                    throw new InvalidOperationException($"Switchboard contract has no code at {switchboardContractAddress} on {chainInfo}. This indicates wrong contract address or wrong network/provider.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VRF] Bytecode check failed (WebGL). Continuing to updateFee check. Error: {e.Message}");
            }
#endif

            try
            {
                // Appel de lecture simple pour vérifier que le contrat répond bien sur ce réseau
                _ = await AppKit.Evm.ReadContractAsync<BigInteger>(
                    switchboardContractAddress,
                    SwitchboardAbiJson,
                    "updateFee"
                );
            }
            catch (Exception e)
            {
                string chainInfo = string.IsNullOrWhiteSpace(activeChainId) ? "(unknown chain)" : activeChainId;
#if UNITY_WEBGL && !UNITY_EDITOR
                Debug.LogWarning($"[VRF] Switchboard updateFee check failed at {switchboardContractAddress} on {chainInfo}. Continuing anyway on WebGL. Inner error: {e.Message}");
#else
                throw new InvalidOperationException($"Switchboard contract check failed at {switchboardContractAddress} on {chainInfo}. This usually means the wallet is on the wrong network or the contract address is wrong. Inner error: {e.Message}");
#endif
            }
        }

        public int DeriveSeed(BigInteger value)
        {
            if (value.Sign < 0)
                value = BigInteger.Negate(value);

            var mod = value % int.MaxValue;
            return (int)mod;
        }

        public async Task<RandomnessResult> RequestAndSettleRandomnessAsync(
            int minSettlementDelaySeconds,
            CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            Debug.Log($"[VRF] RequestAndSettleRandomnessAsync start. minSettlementDelaySeconds={minSettlementDelaySeconds}");

            await EnsureAppKitInitializedAsync(ct);
            await EnsureWalletConnectedAsync(ct);
            await EnsureExpectedChainAndContractAsync(ct);

            if (minSettlementDelaySeconds <= 0)
                minSettlementDelaySeconds = defaultMinSettlementDelaySeconds;

            string randomnessId = GenerateRandomnessIdHex();
            byte[] randomnessIdBytes32 = HexToBytes32(randomnessId);

            Debug.Log($"[VRF] createRandomness: randomnessId={randomnessId} minDelay={minSettlementDelaySeconds}s");

            string createTxHash = await AppKit.Evm.WriteContractAsync(
                switchboardContractAddress,
                SwitchboardAbiJson,
                "createRandomness",
                randomnessIdBytes32,
                (ulong)minSettlementDelaySeconds
            );

            Debug.Log($"[VRF] createRandomness tx sent. hash={createTxHash}");

            await AppKit.Evm.GetTransactionReceiptAsync(createTxHash, ct: ct);
            Debug.Log($"[VRF] createRandomness tx confirmed. hash={createTxHash}");
            ct.ThrowIfCancellationRequested();

            Debug.Log("[VRF] getRandomness: fetching request data (rollTimestamp, minDelay, oracle)...");
            var (rollTimestamp, minSettlementDelay, oracle) = await GetRandomnessRequestDataAsync(randomnessIdBytes32, ct);
            Debug.Log($"[VRF] getRandomness request data: rollTimestamp={rollTimestamp} minDelay={minSettlementDelay} oracle={oracle}");

            await WaitForSettlementDelayAsync(rollTimestamp, minSettlementDelay, ct);

            string encodedRandomness = await ResolveEncodedRandomnessFromCrossbarAsync(
                randomnessId,
                rollTimestamp,
                minSettlementDelay,
                oracle,
                ct
            );

            var encodedRandomnessBytes = HexToBytes(encodedRandomness);

            Debug.Log("[VRF] Reading updateFee...");
            BigInteger updateFee = await AppKit.Evm.ReadContractAsync<BigInteger>(
                switchboardContractAddress,
                SwitchboardAbiJson,
                "updateFee"
            );

            Debug.Log($"[VRF] updateFee={updateFee}");
            ct.ThrowIfCancellationRequested();

            Debug.Log("[VRF] Calling settleRandomness...");
            string settleTxHash = await AppKit.Evm.WriteContractAsync(
                switchboardContractAddress,
                SwitchboardAbiJson,
                "settleRandomness",
                updateFee,
                default,
                encodedRandomnessBytes
            );

            Debug.Log($"[VRF] settleRandomness tx sent. hash={settleTxHash}");

            await AppKit.Evm.GetTransactionReceiptAsync(settleTxHash, ct: ct);
            Debug.Log($"[VRF] settleRandomness tx confirmed. hash={settleTxHash}");
            ct.ThrowIfCancellationRequested();

            Debug.Log("[VRF] Reading final randomness via getRandomness...");
            var finalData = await AppKit.Evm.ReadContractAsync<object>(
                switchboardContractAddress,
                SwitchboardAbiJson,
                "getRandomness",
                new object[] { randomnessIdBytes32 }
            );

            BigInteger value;
            BigInteger settledAt;

            if (finalData is JObject obj)
            {
                value = ToBigInteger(obj["value"]);
                settledAt = ToBigInteger(obj["settledAt"]);
            }
            else
            {
                (value, settledAt) = ExtractFinalValue(finalData);
            }

            Debug.Log($"[VRF] Final randomness: value={value} settledAt={settledAt}");
            if (settledAt == BigInteger.Zero)
                throw new Exception("Randomness failed to settle (settledAt == 0)");

            return new RandomnessResult
            {
                RandomnessId = randomnessId,
                CreateTxHash = createTxHash,
                SettleTxHash = settleTxHash,
                Value = value,
                RollTimestamp = rollTimestamp,
                MinSettlementDelay = minSettlementDelay,
                Oracle = oracle,
            };
        }

        async Task EnsureAppKitInitializedAsync(CancellationToken ct)
        {
            if (AppKit.IsInitialized)
                return;

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1, appKitInitTimeoutSeconds));
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

            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(Math.Max(1, walletConnectTimeoutSeconds));
            while (!AppKit.IsAccountConnected)
            {
                ct.ThrowIfCancellationRequested();
                if (DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Wallet not connected (timeout)");
                await Task.Delay(100, ct);
            }
        }

        async Task<(BigInteger rollTimestamp, BigInteger minSettlementDelay, string oracle)> GetRandomnessRequestDataAsync(
            byte[] randomnessIdBytes32,
            CancellationToken ct)
        {
            Debug.Log("[VRF] getRandomness: calling contract to fetch request data...");
            var data = await AppKit.Evm.ReadContractAsync<object>(
                switchboardContractAddress,
                SwitchboardAbiJson,
                "getRandomness",
                new object[] { randomnessIdBytes32 }
            );

            // viem retourne un objet avec propriétés nommées (randId, createdAt, rollTimestamp, minSettlementDelay, oracle, value, settledAt)
            if (data is JObject obj)
            {
                var rollTimestamp = ToBigInteger(obj["rollTimestamp"]);
                var minDelay = ToBigInteger(obj["minSettlementDelay"]);
                var oracle = obj["oracle"]?.ToString();

                if (string.IsNullOrWhiteSpace(oracle))
                    throw new Exception("Oracle address missing from getRandomness (object)");

                return (rollTimestamp, minDelay, oracle);
            }

            // Fallback: ancien format en tableau
            var tuple = ExtractTuple(data);
            if (tuple == null || tuple.Length < 8)
                throw new Exception("Unexpected getRandomness return shape");

            var rollTimestampFallback = ToBigInteger(tuple[3]);
            var minDelayFallback = ToBigInteger(tuple[4]);
            var oracleFallback = tuple[5]?.ToString();

            if (string.IsNullOrWhiteSpace(oracleFallback))
                throw new Exception("Oracle address missing from getRandomness (tuple)");

            return (rollTimestampFallback, minDelayFallback, oracleFallback);
        }

        async Task WaitForSettlementDelayAsync(BigInteger rollTimestamp, BigInteger minDelay, CancellationToken ct)
        {
            long settlementTime = (long)rollTimestamp + (long)minDelay;
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long waitSeconds = Math.Max(0, settlementTime - now + Math.Max(0, settlementDelayBufferSeconds));
            if (waitSeconds <= 0)
            {
                Debug.Log("[VRF] Settlement delay already passed. Continuing without wait.");
                return;
            }

            Debug.Log($"[VRF] Waiting for settlement delay... waitSeconds={waitSeconds} (buffer={settlementDelayBufferSeconds})");
            // Task.Delay peut ne pas reprendre correctement en WebGL (pas de timers/threads fiables).
            // On attend donc de manière coopérative, frame par frame.
            var deadlineUtc = DateTime.UtcNow + TimeSpan.FromSeconds(waitSeconds);
            var nextLogAt = DateTime.UtcNow + TimeSpan.FromSeconds(2);
            while (DateTime.UtcNow < deadlineUtc)
            {
                ct.ThrowIfCancellationRequested();

                if (DateTime.UtcNow >= nextLogAt)
                {
                    var remaining = Math.Max(0, (int)(deadlineUtc - DateTime.UtcNow).TotalSeconds);
                    Debug.Log($"[VRF] Waiting for settlement delay... remaining={remaining}s");
                    nextLogAt = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                }

                await Task.Yield();
            }

            Debug.Log("[VRF] Settlement delay elapsed.");
        }

        async Task<string> ResolveEncodedRandomnessFromCrossbarAsync(
            string randomnessId,
            BigInteger rollTimestamp,
            BigInteger minDelay,
            string oracle,
            CancellationToken ct)
        {
            string url = crossbarUrl.TrimEnd('/') + "/randomness/evm";

            // Important: utiliser le rollTimestamp on-chain.
            // Si on bidouille le timestamp (ex: now-60s), Crossbar peut renvoyer un "encoded"
            // qui ne matche pas la validation du contrat => revert "Wrong oracle".
            long rollTsSeconds = (long)rollTimestamp;
            Debug.Log($"[VRF] Crossbar using rollTimestamp (on-chain). rollTimestamp={rollTsSeconds}");

            const int maxAttempts = 6;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                var body = new
                {
                    chain_id = chainId.ToString(CultureInfo.InvariantCulture),
                    randomness_id = randomnessId,
                    timestamp = rollTsSeconds,
                    min_staleness_seconds = (long)minDelay,
                    oracle = oracle.ToLowerInvariant(),
                };

                string json = JsonConvert.SerializeObject(body);
                using var req = new UnityWebRequest(url, "POST");
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");

                Debug.Log($"[VRF] Crossbar request (attempt {attempt + 1}/{maxAttempts}): url={url} body={json}");

                try
                {
                    string responseText = await SendAsync(req, ct);
                    Debug.Log($"[VRF] Crossbar response received. length={(responseText == null ? 0 : responseText.Length)}");
                    var token = JToken.Parse(responseText);

                    string encoded = token.SelectToken("encoded")?.ToString();
                    if (string.IsNullOrWhiteSpace(encoded))
                        encoded = token.SelectToken("data.encoded")?.ToString();
                    if (string.IsNullOrWhiteSpace(encoded))
                        throw new Exception("Crossbar response missing 'encoded'");

                    return encoded;
                }
                catch (Exception e)
                {
                    bool isTimestampFuture = e.Message != null
                                             && e.Message.IndexOf("TimestampTooFarInTheFuture", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool canRetry = attempt < (maxAttempts - 1) && isTimestampFuture;

                    if (!canRetry)
                        throw;

                    long nowUtcSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    long delta = rollTsSeconds - nowUtcSeconds;
                    int waitSeconds = (int)Mathf.Clamp(15 * (attempt + 1), 15, 180);

                    Debug.LogWarning($"[VRF] Crossbar attempt {attempt + 1} failed with TimestampTooFarInTheFuture. Waiting {waitSeconds}s then retrying with same rollTimestamp={rollTsSeconds}. nowUtc={nowUtcSeconds} delta={delta}. Error: {e.Message}");

                    // Attente (compatible WebGL) avant de retenter.
                    var retryAtUtc = DateTime.UtcNow + TimeSpan.FromSeconds(waitSeconds);
                    while (DateTime.UtcNow < retryAtUtc)
                    {
                        ct.ThrowIfCancellationRequested();
                        await Task.Yield();
                    }
                }
            }

            throw new Exception("Crossbar failed after retries");
        }

        static async Task<string> SendAsync(UnityWebRequest req, CancellationToken ct)
        {
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (req.result != UnityWebRequest.Result.Success)
                throw new Exception(req.error + "\n" + req.downloadHandler?.text);

            return req.downloadHandler.text;
        }

        static string GenerateRandomnessIdHex()
        {
            var bytes = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);

            var sb = new StringBuilder(66);
            sb.Append("0x");
            for (int i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
        }

        static byte[] HexToBytes32(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                throw new Exception("randomnessId is empty");

            if (!hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                throw new Exception("randomnessId must start with 0x");

            string h = hex.Substring(2);
            if (h.Length != 64)
                throw new Exception($"randomnessId must be 32 bytes (64 hex chars), got {h.Length}");

            var bytes = new byte[32];
            for (int i = 0; i < 32; i++)
            {
                string byteHex = h.Substring(i * 2, 2);
                bytes[i] = byte.Parse(byteHex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        static byte[] HexToBytes(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                throw new Exception("hex is empty");

            if (!hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                throw new Exception("hex must start with 0x");

            string h = hex.Substring(2);
            if (h.Length == 0)
                return Array.Empty<byte>();

            if ((h.Length % 2) != 0)
                throw new Exception($"hex length must be even, got {h.Length}");

            var bytes = new byte[h.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                string byteHex = h.Substring(i * 2, 2);
                bytes[i] = byte.Parse(byteHex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture);
            }

            return bytes;
        }

        static object[] ExtractTuple(object data)
        {
            if (data is object[] objArray)
                return objArray;

            if (data is JArray jArray)
                return jArray.ToObject<object[]>();

            return null;
        }

        static (BigInteger value, BigInteger settledAt) ExtractFinalValue(object data)
        {
            var tuple = ExtractTuple(data);
            if (tuple == null || tuple.Length < 8)
                throw new Exception("Unexpected getRandomness return shape");

            var value = ToBigInteger(tuple[6]);
            var settledAt = ToBigInteger(tuple[7]);
            return (value, settledAt);
        }

        static BigInteger ToBigInteger(object o)
        {
            if (o == null)
                return BigInteger.Zero;

            if (o is BigInteger bi)
                return bi;

            if (o is ulong ul)
                return new BigInteger(ul);
            if (o is long l)
                return new BigInteger(l);
            if (o is uint ui)
                return new BigInteger(ui);
            if (o is int i)
                return new BigInteger(i);

            if (o is string s)
            {
                if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    if (BigInteger.TryParse(s.Substring(2), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var hexBi))
                        return hexBi;
                }

                if (BigInteger.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var decBi))
                    return decBi;
            }

            if (o is JValue jv)
                return ToBigInteger(jv.Value);

            throw new Exception($"Cannot convert to BigInteger: {o} ({o.GetType().FullName})");
        }
    }
}
