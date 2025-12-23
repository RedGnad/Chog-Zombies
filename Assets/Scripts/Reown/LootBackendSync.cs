using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using ChogZombies.Loot;
using global::Reown.AppKit.Unity;

namespace ChogZombies.Reown
{
    /// <summary>
    /// Synchronise l'état des loots (owned / equipped) avec le backend Express
    /// en utilisant l'adresse de wallet comme identifiant.
    /// 
    /// Cette classe reste volontairement simple :
    /// - aucune logique de retry agressive
    /// - pas d'appel automatique : tu choisis quand appeler Pull/Push
    /// </summary>
    public class LootBackendSync : MonoBehaviour
    {
        [Header("Backend")]
        [Tooltip("URL de base de l'API backend (sans slash final). Ex: https://chogzombies-server.onrender.com")] 
        [SerializeField] string apiBaseUrl = "https://YOUR-RENDER-APP.onrender.com";

        [SerializeField] bool logDebug;

        MetaProgressionController _meta;

        [Serializable]
        class LootStateDto
        {
            public string walletAddress;
            public string[] ownedItems;
            public string[] equippedItems;
        }

        void Awake()
        {
            if (_meta == null)
                _meta = FindObjectOfType<MetaProgressionController>();
        }

        /// <summary>
        /// Récupère l'état des loots depuis le backend pour le wallet actuellement connecté (via AppKit).
        /// </summary>
        public void PullForCurrentWallet()
        {
            string wallet = TryGetCurrentWalletAddress();
            if (string.IsNullOrWhiteSpace(wallet))
            {
                if (logDebug)
                    Debug.LogWarning("[LootBackendSync] PullForCurrentWallet: aucun wallet connecté.");
                return;
            }

            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                Debug.LogWarning("[LootBackendSync] apiBaseUrl non configuré.");
                return;
            }

            if (_meta == null)
                _meta = FindObjectOfType<MetaProgressionController>();
            if (_meta == null)
            {
                Debug.LogWarning("[LootBackendSync] MetaProgressionController introuvable dans la scène.");
                return;
            }

            StartCoroutine(PullCoroutine(wallet));
        }

        /// <summary>
        /// Envoie l'état local (PlayerPrefs) vers le backend pour le wallet actuellement connecté.
        /// </summary>
        public void PushForCurrentWallet()
        {
            string wallet = TryGetCurrentWalletAddress();
            if (string.IsNullOrWhiteSpace(wallet))
            {
                if (logDebug)
                    Debug.LogWarning("[LootBackendSync] PushForCurrentWallet: aucun wallet connecté.");
                return;
            }

            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                Debug.LogWarning("[LootBackendSync] apiBaseUrl non configuré.");
                return;
            }

            if (_meta == null)
                _meta = FindObjectOfType<MetaProgressionController>();
            if (_meta == null)
            {
                Debug.LogWarning("[LootBackendSync] MetaProgressionController introuvable dans la scène.");
                return;
            }

            StartCoroutine(PushCoroutine(wallet));
        }

        string TryGetCurrentWalletAddress()
        {
            try
            {
                if (!AppKit.IsInitialized || !AppKit.IsAccountConnected)
                    return null;
                // Aligné sur l'usage dans WalletUI_TMP : on suppose qu'un compte est présent
                // dès lors que IsAccountConnected est vrai.
                var account = AppKit.Account;
                return account.Address;
            }
            catch
            {
                return null;
            }
        }

        IEnumerator PullCoroutine(string walletAddress)
        {
            string url = apiBaseUrl.TrimEnd('/') + "/users/" + walletAddress;
            if (logDebug)
                Debug.Log($"[LootBackendSync] GET {url}");

            using (var request = UnityWebRequest.Get(url))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogWarning($"[LootBackendSync] GET failed: {request.error}");
                    yield break;
                }

                var json = request.downloadHandler.text;
                if (logDebug)
                    Debug.Log($"[LootBackendSync] GET response: {json}");

                LootStateDto dto = null;
                try
                {
                    dto = JsonUtility.FromJson<LootStateDto>(json);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LootBackendSync] Impossible de parser la réponse loot: {e.Message}");
                    yield break;
                }

                if (dto == null)
                    yield break;

                if (_meta == null)
                    _meta = FindObjectOfType<MetaProgressionController>();
                if (_meta == null)
                {
                    Debug.LogWarning("[LootBackendSync] MetaProgressionController introuvable lors du Pull.");
                    yield break;
                }

                var owned = dto.ownedItems ?? Array.Empty<string>();
                var equipped = dto.equippedItems ?? Array.Empty<string>();

                _meta.ApplyRemoteState(owned, equipped);
                if (logDebug)
                    Debug.Log("[LootBackendSync] État loot appliqué depuis le backend.");
            }
        }

        IEnumerator PushCoroutine(string walletAddress)
        {
            if (_meta == null)
                _meta = FindObjectOfType<MetaProgressionController>();
            if (_meta == null)
            {
                Debug.LogWarning("[LootBackendSync] MetaProgressionController introuvable lors du Push.");
                yield break;
            }

            var dto = new LootStateDto
            {
                walletAddress = walletAddress,
                ownedItems = _meta.GetOwnedKeys().ToArray(),
                equippedItems = _meta.GetEquippedKeys().ToArray()
            };

            string json = JsonUtility.ToJson(dto);
            byte[] body = Encoding.UTF8.GetBytes(json);

            string url = apiBaseUrl.TrimEnd('/') + "/users/" + walletAddress + "/loot";
            if (logDebug)
                Debug.Log($"[LootBackendSync] PUT {url} body={json}");

            using (var request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                yield return request.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
                if (request.result != UnityWebRequest.Result.Success)
#else
                if (request.isNetworkError || request.isHttpError)
#endif
                {
                    Debug.LogWarning($"[LootBackendSync] PUT failed: {request.error}");
                    yield break;
                }

                if (logDebug)
                    Debug.Log($"[LootBackendSync] PUT success. Response={request.downloadHandler.text}");
            }
        }
    }
}
