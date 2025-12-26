// SPDX-License-Identifier: MIT
pragma solidity ^0.8.0;

// Interfaces officielles Pyth Entropy, cf. docs:
// https://docs.pyth.network/entropy/generate-random-numbers-evm
import { IEntropyConsumer } from "@pythnetwork/entropy-sdk-solidity/IEntropyConsumer.sol";
import { IEntropyV2 } from "@pythnetwork/entropy-sdk-solidity/IEntropyV2.sol";

/// @title ChogZombiesEntropyConsumer
/// @notice Contrat consommateur Pyth Entropy pour Chog Zombies (Monad EVM)
/// @dev Ce contrat suit le pattern documenté par Pyth Entropy pour requestV2 / entropyCallback.
contract ChogZombiesEntropyConsumer is IEntropyConsumer {
    /// @notice Contrat Pyth Entropy V2 sur la chaîne (adresse à prendre dans la chainlist Pyth)
    IEntropyV2 public entropy;

    /// @dev Type de tirage (run seed, boss loot, etc.)
    enum Purpose {
        Unknown,
        RunSeed,
        BossLoot
    }

    struct RandomnessInfo {
        bytes32 value;
        uint64 sequenceNumber;
        Purpose purpose;
        address requester;
        bool ready;
    }

    /// @dev Mapping séquence -> infos de randomness
    mapping(uint64 => RandomnessInfo) public randomnessBySequence;

    /// @dev Dernier tirage "run seed" demandé par joueur
    mapping(address => uint64) public latestRunSeedRequestByPlayer;

    /// @dev Dernier tirage "boss loot" demandé par joueur
    mapping(address => uint64) public latestBossLootRequestByPlayer;

    event RandomnessRequested(
        address indexed requester,
        uint64 indexed sequenceNumber,
        Purpose purpose
    );

    event RandomnessReady(
        address indexed requester,
        uint64 indexed sequenceNumber,
        Purpose purpose,
        bytes32 randomNumber
    );

    /// @param entropyAddress Adresse du contrat Entropy V2 sur Monad (cf. docs Pyth chainlist)
    constructor(address entropyAddress) {
        require(entropyAddress != address(0), "entropy address required");
        entropy = IEntropyV2(entropyAddress);
    }

    /// @notice Demande un random pour la seed de run.
    /// @dev Suivit par entropyCallback quand le nombre aléatoire est prêt.
    function requestRunSeed() external payable returns (uint64) {
        uint256 fee = entropy.getFeeV2();
        require(msg.value >= fee, "insufficient fee");

        uint64 seq = entropy.requestV2{value: fee}();

        randomnessBySequence[seq] = RandomnessInfo({
            value: bytes32(0),
            sequenceNumber: seq,
            purpose: Purpose.RunSeed,
            requester: msg.sender,
            ready: false
        });

        latestRunSeedRequestByPlayer[msg.sender] = seq;

        emit RandomnessRequested(msg.sender, seq, Purpose.RunSeed);

        // Remboursement éventuel de l'excédent
        if (msg.value > fee) {
            unchecked {
                payable(msg.sender).transfer(msg.value - fee);
            }
        }

        return seq;
    }

    /// @notice Demande un random pour le loot de boss.
    function requestBossLootRandom() external payable returns (uint64) {
        uint256 fee = entropy.getFeeV2();
        require(msg.value >= fee, "insufficient fee");

        uint64 seq = entropy.requestV2{value: fee}();

        randomnessBySequence[seq] = RandomnessInfo({
            value: bytes32(0),
            sequenceNumber: seq,
            purpose: Purpose.BossLoot,
            requester: msg.sender,
            ready: false
        });

        latestBossLootRequestByPlayer[msg.sender] = seq;

        emit RandomnessRequested(msg.sender, seq, Purpose.BossLoot);

        if (msg.value > fee) {
            unchecked {
                payable(msg.sender).transfer(msg.value - fee);
            }
        }

        return seq;
    }

    /// @inheritdoc IEntropyConsumer
    /// @notice Callback appelé par le contrat Entropy quand le random est prêt.
    /// @dev NE DOIT PAS revert, cf. docs Pyth.
    function entropyCallback(
        uint64 sequenceNumber,
        address provider,
        bytes32 randomNumber
    ) internal override {
        // On récupère l'entrée pré-créée lors de la requête.
        RandomnessInfo storage info = randomnessBySequence[sequenceNumber];

        // Si aucune entrée n'existait (cas inattendu), on en crée une minimale.
        if (info.sequenceNumber == 0 && info.purpose == Purpose.Unknown) {
            info.sequenceNumber = sequenceNumber;
            info.purpose = Purpose.Unknown;
        }

        info.value = randomNumber;
        info.ready = true;

        emit RandomnessReady(info.requester, sequenceNumber, info.purpose, randomNumber);

        // `provider` est disponible si tu veux tracer quel provider a servi la requête.
        provider; // éviter un warning de variable non utilisée
    }

    /// @inheritdoc IEntropyConsumer
    function getEntropy() internal view override returns (address) {
        return address(entropy);
    }

    /// @notice Retourne le dernier tirage de seed de run pour un joueur.
    function getLatestRunSeed(
        address player
    ) external view returns (bool ready, bytes32 value, uint64 sequenceNumber) {
        uint64 seq = latestRunSeedRequestByPlayer[player];
        if (seq == 0) {
            return (false, bytes32(0), 0);
        }
        RandomnessInfo storage info = randomnessBySequence[seq];
        return (info.ready, info.value, seq);
    }

    /// @notice Retourne le dernier tirage de loot de boss pour un joueur.
    function getLatestBossLootRandom(
        address player
    ) external view returns (bool ready, bytes32 value, uint64 sequenceNumber) {
        uint64 seq = latestBossLootRequestByPlayer[player];
        if (seq == 0) {
            return (false, bytes32(0), 0);
        }
        RandomnessInfo storage info = randomnessBySequence[seq];
        return (info.ready, info.value, seq);
    }

    /// @notice Expose la fee actuelle requise par Entropy.
    function getRequiredFee() external view returns (uint256) {
        return entropy.getFeeV2();
    }

    /// @notice Vue simplifiée: retourne uniquement la valeur du dernier tirage de run pour un joueur.
    function getLatestRunSeedValue(address player) external view returns (bytes32) {
        uint64 seq = latestRunSeedRequestByPlayer[player];
        if (seq == 0) {
            return bytes32(0);
        }
        RandomnessInfo storage info = randomnessBySequence[seq];
        if (!info.ready) {
            return bytes32(0);
        }
        return info.value;
    }
}
