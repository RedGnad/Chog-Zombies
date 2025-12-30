# VRF Dual-Provider : Plan d’implémentation de la vérification joueur

> **Providers utilisés aujourd’hui**
>
> - **Pyth Entropy** – consumer `ChogZombiesEntropyConsumer` (`0x3A04d36eeF559F5E42BD2e7896AA691ceDEcEa9e`)  
>   Documentation officielle : [docs.pyth.network/randomness/vrf](https://docs.pyth.network/price-feeds/randomness/vrf)
> - **Switchboard VRF** (fallback) – contrat randomness `0xB7F03eee7B9F56347e32cC71DaD65B303D5a0E67` + Crossbar API  
>   Documentation officielle : [docs.switchboard.xyz](https://docs.switchboard.xyz) (sections *Randomness*, *Crossbar*)

## 1. Objectifs

1. Permettre à un joueur de vérifier, run par run, que le seed/loot provient bien d’un tirage VRF valide (Pyth en priorité, Switchboard en fallback).  
2. Fournir un mécanisme de **replay** : rejouer localement un run depuis le `randomness` publié on-chain.  
3. Exposer suffisamment de métadonnées pour que n’importe qui puisse auditer depuis un block explorer ou un script CLI.

## 2. Architecture cible

```
               +-------------------------------+
               |    RunGameController          |
               |-------------------------------|
               | - choisit provider (Pyth/SB)  |
               | - stocke VRFMetadata          |
               +-------------------------------+
                           |
        +------------------+------------------+
        |                                     |
 +--------------+                      +------------------+
 | PythEntropy  |                      | Switchboard VRF  |
 | Randomness   |                      | Service          |
 +--------------+                      +------------------+
        |                                     |
        v                                     v
  Consumer contract                    Switchboard contract
  + events `EntropyFulfilled`          + tx `createRandomness` / `settleRandomness`
  + storage `getLatestRunSeedValue`    + storage `getRandomness`

```

### Méga-structure partagée (côté Unity / backend)

```csharp
struct VrfMetadata {
    string provider;            // "pyth" | "switchboard"
    string context;             // "run_seed" | "boss_loot"
    string randomnessId;        // bytes32 ou hash tx selon provider
    string requestTxHash;       // tx Pyth request* ou SB createRandomness
    string settleTxHash;        // tx Pyth callback (si dispo) ou SB settle
    string consumerContract;    // adresse utilisée
    string playerWallet;        // 0x...
    string randomnessHex;       // bytes32/uint256 utilisé pour semer Unity
    int    derivedSeed;         // seed int injecté dans gameplay
    long   blockTimestamp;      // pour tri & replay
}
```

- **Stockage local** : scriptable `RunHistory` + sauvegarde JSON pour inspection.  
- **Backend** : push dans `LootBackendSync` après résolution VRF pour permettre consultation hors client.

## 3. Flux de vérification côté joueur

| Étape | UX | Technique |
|-------|----|-----------|
|1|Dans l’écran *Run Summary*, afficher un encart **“Provable Randomness”** avec provider, seed dérivé, boutons “Voir sur Monad Explorer” & “Copier la preuve”.|UI lit `VrfMetadata`.|
|2|Bouton “Rejouer ce seed” → relance le jeu en mode sandbox avec `derivedSeed`.|\_DevMode active un flag `ForceSeed`.|
|3|Option “Exporter preuve (.json)” pour partage communautaire.|Écrit `VrfMetadata` sur disque.|

## 4. Procédure de vérification Pyth (guide à afficher/Documenter)

1. **Ouvrir la transaction `requestRunSeed/requestBossLootRandom`** (hash fourni).  
   - Confirmer qu’elle cible `ChogZombiesEntropyConsumer`.  
2. **Lire l’événement `EntropyFulfilled`** dans le block explorer ou via `cast logs`.  
   - Paramètres clés : `requestId`, `randomness` (bytes32), `caller`.  
3. **Vérifier le stockage**  
   ```bash
   cast call 0x3A04...Ea9e "getLatestRunSeedValue(address)(bytes32)" <playerWallet>
   ```  
   La valeur retournée doit correspondre à `randomnessHex`.
4. **Reproduction locale**  
   - Lancer `chogz --replay --provider=pyth --seed=<randomnessHex>` pour régénérer le niveau et confirmer le loot obtenu.

## 5. Procédure de vérification Switchboard (fallback)

1. **Transaction `createRandomness`**  
   - Fournie par `requestTxHash`. Vérifier `randomnessId` (logged).  
2. **Transaction `settleRandomness`**  
   - Hash `settleTxHash`. Assurer que l’oracle signé est celui reçu de `getRandomness`.  
3. **Lecture on-chain**  
   ```bash
   cast call 0xB7F03e...0E67 "getRandomness(bytes32)((bytes32,uint256,address,uint256,uint64,address,uint256,uint256))" <randomnessId>
   ```  
   - Champ `value` = `randomnessHex`.  
4. **Validation Crossbar**  
   - Facultatif : re-appeler `https://crossbar.switchboard.xyz/randomness/evm` avec `randomness_id` pour récupérer `encoded` et comparer au payload on-chain (cf docs Switchboard).  
5. **Rejeu**  
   - `chogz --replay --provider=switchboard --randomness-id=<id>` pour vérifier les drops.

## 6. Gestion du fallback & synchronisation

1. **Priorité Pyth**. Si succès → stocker `provider="pyth"` et invalider cache Switchboard.  
2. **Fallback Switchboard** uniquement si Pyth échoue ou est désactivé.  
3. **Replays**  
   - Pyth: `derivedSeed = Hash(randomness, levelIndex, contextNonce)`  
   - Switchboard: `derivedSeed = vrfService.DeriveSeed(value)` déjà implémenté.  
4. **Backend**  
   - Endpoint `POST /vrf-events` qui accepte `VrfMetadata`.  
   - Endpoint `GET /vrf-events/:runId` pour afficher la preuve depuis un dashboard web.

## 7. UX & Dev Tasks

1. **UI**  
   - Carte “Provably Fair” (icône + provider).  
   - Boutons : `Voir la transaction`, `Copier la preuve`, `Rejouer ce seed`.  
   - Banner “VRF pending” quand Switchboard attend la settlement delay.
2. **CLI / Debug**  
   - Commande `Tools/ReplayLastSeed` dans l’éditeur Unity.  
3. **Docs publiques**  
   - Page “How to Verify” reprenant les étapes §4 & §5 avec captures d’écran.

## 8. Checklist d’implémentation

1. [ ] Ajouter une classe `VrfProofStore` (Singleton) qui reçoit les métadonnées depuis `RunGameController`.  
2. [ ] Étendre `LootBackendSync.PushForCurrentWallet` pour inclure `VrfMetadata`.  
3. [ ] Créer un prefab UI `VRFProofPanel`.  
4. [ ] Implémenter le mode Replay (menu debug + param CLI).  
5. [ ] Rédiger documentation utilisateur (FR/EN) et lier depuis le menu principal.

## 9. Références officielles

- **Pyth Entropy** : `https://docs.pyth.network/price-feeds/randomness/vrf`  
- **Switchboard Randomness** : `https://docs.switchboard.xyz/randomness/overview`  
- **Crossbar API** : `https://docs.switchboard.xyz/products/crossbar`  

Ces sources devront être consultées à chaque évolution pour rester aligné sur les meilleures pratiques (gestion des frais, timestamps, limites de staleness).
