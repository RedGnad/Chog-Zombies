using UnityEngine;

namespace ChogZombies.Loot
{
    public static class RunMetaEffects
    {
        /// <summary>
        /// Probabilité supplémentaire qu'un groupe d'ennemis tué fasse apparaître un coin physique.
        /// Valeur en [0,1].
        /// </summary>
        public static float CoinDropChancePerEnemy { get; private set; }

        /// <summary>
        /// Nombre moyen de coins supplémentaires à placer sur la carte (en plus du layout de base).
        /// Peut être fractionnaire (par ex. 0.5 ⇒ 50% de chance d'un coin en plus).
        /// </summary>
        public static float ExpectedExtraCoinsOnMap { get; private set; }

        /// <summary>Bonus multiplicatif appliqué à la portée effective des projectiles.</summary>
        public static float ProjectileRangeBonus { get; private set; }

        /// <summary>Bonus de puissance appliqué pour ce run (run-only).</summary>
        public static float StartRunPowerBonus { get; private set; }

        /// <summary>Bonus de puissance de départ permanent (méta).</summary>
        public static float PersistentStartPowerBonus { get; private set; }

        /// <summary>Rayon supplémentaire d'attraction des coins pour ce run.</summary>
        public static float CoinMagnetRadiusBonus { get; private set; }

        /// <summary>Rayon supplémentaire permanent de pickup de coins.</summary>
        public static float PersistentCoinPickupRadiusBonus { get; private set; }

        /// <summary>Bonus de luck appliqué aux tirages de loot pendant le run.</summary>
        public static float RunLootLuckBonus { get; private set; }

        /// <summary>Bonus de luck permanent pour tous les runs.</summary>
        public static float PersistentLootLuckBonus { get; private set; }

        /// <summary>Dégâts de contact infligés par seconde.</summary>
        public static float ContactDamagePerSecond { get; private set; }

        /// <summary>Dégâts par seconde de l'aura de pointes.</summary>
        public static float SpikeAuraDamagePerSecond { get; private set; }

        /// <summary>Bonus de rayon ajouté à l'aura de pointes.</summary>
        public static float SpikeAuraRadiusBonus { get; private set; }

        /// <summary>Puissance cumulée des drones gardiens.</summary>
        public static float GuardianDronePower { get; private set; }

        /// <summary>Charges d'Aegis disponibles pour ce run.</summary>
        public static int AegisCharges { get; private set; }

        public static void Reset()
        {
            CoinDropChancePerEnemy = 0f;
            ExpectedExtraCoinsOnMap = 0f;
            ProjectileRangeBonus = 0f;
            StartRunPowerBonus = 0f;
            CoinMagnetRadiusBonus = 0f;
            RunLootLuckBonus = 0f;
            ContactDamagePerSecond = 0f;
            SpikeAuraDamagePerSecond = 0f;
            SpikeAuraRadiusBonus = 0f;
            GuardianDronePower = 0f;
            AegisCharges = 0;
            // Les bonus permanents sont recalculés via l'équipement méta au début d'un run.
            PersistentStartPowerBonus = 0f;
            PersistentCoinPickupRadiusBonus = 0f;
            PersistentLootLuckBonus = 0f;
        }

        public static void AddCoinDropChance(float delta)
        {
            float before = CoinDropChancePerEnemy;
            CoinDropChancePerEnemy = Mathf.Clamp01(CoinDropChancePerEnemy + delta);
            Debug.Log($"[MetaEffects] Coin drop chance {before:P0} -> {CoinDropChancePerEnemy:P0} (delta={delta:+0.##;-0.##;0}).");
        }

        public static void AddExpectedExtraCoinsOnMap(float delta)
        {
            ExpectedExtraCoinsOnMap = Mathf.Max(0f, ExpectedExtraCoinsOnMap + delta);
        }

        public static void AddProjectileRangeBonus(float delta)
        {
            ProjectileRangeBonus = Mathf.Max(0f, ProjectileRangeBonus + delta);
        }

        public static void AddStartRunPower(float delta)
        {
            StartRunPowerBonus = Mathf.Max(0f, StartRunPowerBonus + delta);
        }

        public static float ConsumeStartRunPowerBonus()
        {
            float bonus = StartRunPowerBonus;
            StartRunPowerBonus = 0f;
            return bonus;
        }

        public static void AddPersistentStartPower(float delta)
        {
            PersistentStartPowerBonus = Mathf.Max(0f, PersistentStartPowerBonus + delta);
        }

        public static void AddCoinMagnetRadius(float delta)
        {
            CoinMagnetRadiusBonus = Mathf.Max(0f, CoinMagnetRadiusBonus + delta);
        }

        public static void AddPersistentCoinPickupRadius(float delta)
        {
            PersistentCoinPickupRadiusBonus = Mathf.Max(0f, PersistentCoinPickupRadiusBonus + delta);
        }

        public static void AddRunLootLuck(float delta)
        {
            RunLootLuckBonus = Mathf.Max(0f, RunLootLuckBonus + delta);
        }

        public static void AddPersistentLootLuck(float delta)
        {
            PersistentLootLuckBonus = Mathf.Max(0f, PersistentLootLuckBonus + delta);
        }

        public static void AddContactDamage(float delta)
        {
            ContactDamagePerSecond = Mathf.Max(0f, ContactDamagePerSecond + delta);
        }

        public static void AddSpikeAuraDamage(float delta)
        {
            SpikeAuraDamagePerSecond = Mathf.Max(0f, SpikeAuraDamagePerSecond + delta);
        }

        public static void AddSpikeAuraRadius(float delta)
        {
            SpikeAuraRadiusBonus = Mathf.Max(0f, SpikeAuraRadiusBonus + delta);
        }

        public static void AddGuardianDronePower(float delta)
        {
            GuardianDronePower = Mathf.Max(0f, GuardianDronePower + delta);
        }

        public static void AddAegisCharges(int delta)
        {
            AegisCharges = Mathf.Max(0, AegisCharges + delta);
        }
    }
}
