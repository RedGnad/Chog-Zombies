# Plan d'implémentation des nouvelles familles d'items

| ID | Famille | Type | Effet attendu | Notes de gameplay |
| --- | --- | --- | --- | --- |
| 1 | Scope | Run-only (Common → Mythic) | Bonus de portée/dégâts à distance, scaling par tier | Peut agir sur falloff, damage multiplier sur ennemis éloignés, ou élargir hitbox de projectiles. |
| 2 | Apple | Run-only (unique tier) | +1 puissance (ou dmg fixe) appliqué instantanément au démarrage du run | Consommé à l'activation, pas stocké côté méta. |
| 3 | Golden Apple | Méta (Legendary/Mythic) | +1 puissance de départ permanente (stackable avec cap) | Ajouter un cap (ex: 3) pour éviter l'explosion. |
| 4 | Magnet | Run-only (Common) | Aimant qui attire les coins autour du joueur pendant le run | Radius constant, VFX simple. |
| 5 | Magnetic Core | Méta (Rare → Legendary) | Augmente définitivement le rayon de pickup de base | Bonus additif par tier. |
| 6 | Lucky Charm | Run-only (multi tiers) | Chance accrue de loot/rarété du boss sur ce run | S'applique lors du roll de loot boss. |
| 7 | Ancient Talisman | Méta (Epic → Mythic) | Petit bonus permanent de luck globale | S'applique à tous les rolls (boss + loot tables). |
| 8 | Coin Bag | Run-only (Common → ???) | 50% (actuel) de chance qu'un ennemi droppe un coin | **Déjà implémenté.** |
| 9 | Bank Contract | Méta (Rare → Legendary) | Augmente légèrement les chances d'avoir 1-2 coins en plus sur la map | **Déjà implémenté.** |
| 10 | Spiked Boots | Méta (toutes raretés) | Dégâts de contact autour du joueur, scaling par tier | Ajouter un collider/overlap damage sur PlayerCombatController. |
| 11 | Guardian Drone | Méta (toutes raretés) | Drones offensifs orbitant autour du joueur | Créer prefab(s) + contrôleur. |
| 12 | Aegis Halo | Méta (Rare → Mythic) | Charges d'annulation de perte d'armée par run | Implémenter nouveau type AegisCharges. |

## Step 1 – Nouveaux LootEffectType

| Enum value | Description | Items concernés |
| --- | --- | --- |
| `RangeDamageBonus` | Bonus de dégâts/portée contre ennemis lointains | Scope |
| `StartRunPowerBoost` | +power instantané au start du run | Apple |
| `PersistentStartPower` | +power de départ permanent | Golden Apple |
| `CoinMagnetRadius` | Aimant temporaire (run) | Magnet |
| `PersistentCoinPickupRadius` | Aimant permanent | Magnetic Core |
| `RunLootLuck` | Bonus de luck pour les rolls de run | Lucky Charm |
| `PersistentLootLuck` | Bonus permanent de luck | Ancient Talisman |
| `ContactDamage` | Aura de dégâts (méta) | Spiked Boots |
| `GuardianDrone` | Génération de drones offensifs | Guardian Drone |
| `AegisCharges` | Charges d'annulation de perte de puissance | Aegis Halo |

### Tâches techniques Step 1
1. Étendre `LootEffectType` avec les valeurs ci-dessus.
2. Ajouter les champs/structures nécessaires dans `PlayerLootController` + systèmes associés (ex: `RunMetaEffects` ou nouveaux singletons) pour stocker ces valeurs.
3. Préparer des hooks/événements dans `PlayerCombatController`, `RunGameController`, `BossLoot`, etc., pour appliquer les nouveaux effets.
