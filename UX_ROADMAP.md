# UX & Visual Roadmap - Chog Zombies

## Objectif
Rendre le jeu "viral" avec une UX fluide, des visuels accrocheurs, compatible mobile.

---

## 1. UX VRF Loading (PRIORITÉ HAUTE)

### Overlay de chargement VRF
- Écran/overlay pendant :
  - Seed de run
  - Loot boss VRF
- Bloquer les inputs pendant étapes critiques
- États UI explicites (progression) :
  - "Signature du wallet"
  - "Transaction en cours"
  - "Attente settlement (VRF)"
  - "Résolution randomness"
  - "Application du seed / loot"

### Fallback
- Bouton Retry (relancer la requête)
- Bouton Quit (retour menu)
- Mode "offline seed" optionnel

### Micro-détails
- Spinner + progress bar (même "fake progress")
- Texte "Provably random (VRF)" + bouton "Details" (tx hash)
- Animation de révélation (seed/loot) à la fin

---

## 2. Viralité : Boucle Run → Reward → Share → Upgrade

### Écran de fin de run "résumé"
- Seed / niveau atteint / gold / loot obtenu
- "Best run" / "streak"

### Bouton Share (mobile-friendly)
- "J'ai drop [Loot Rare] avec VRF sur Monad"
- Génère une image/card (très viral)

### Système de raretés
- Common / Rare / Epic / Legendary + couleur + FX
- "New!" tag sur loot jamais obtenu
- Collection avec progression (% complété)

---

## 3. Inventaire / Meta-progression

### Version MVP inventaire
- Écran Inventory :
  - Liste des loots possédés (icône, nom, rareté)
  - Détail : stats, effet, niveau
  - Bouton Equip (slots)
- 3–5 slots max au début

### Après chaque run
- "Loot obtenu" (reveal animé)
- "Équipé automatiquement ?" (option)

### Upgrades simples
- Fusion 3→1 (3 commons = 1 rare)
- Ou "level up" via doublons
- Synergies : 2 items d'un set = bonus

---

## 4. "Juice" / Polish Visuel

### Les 10 améliorations à plus fort ROI
1. **Hit feedback** : flash rouge, pop scale, nombre dégâts/crit
2. **VFX** : impact bullets, explosion, gold pickup
3. **SFX** : tir, hit, loot drop (rarity-dependent), UI click
4. **Camera** : petit shake sur gros hit / boss
5. **Animations UI** : ouverture shop, reveal loot, boutons press
6. **Traînées / particules** sur items rares
7. **Background** plus vivant (parallax léger)
8. **Boss intro** (1 sec) + musique/FX
9. **Aim / direction** mieux lisible (indicateurs)
10. **Transitions** (fade, wipe) entre states

---

## 5. Mobile-first

### UX Mobile
- UI scalable (Canvas Scaler correct)
- Safe Area (encoches iPhone)
- Tailles de police lisibles
- Boutons plus gros (48–56dp)
- Éviter petits textes longs pendant VRF
- Haptics sur loot rare, boss kill

### Perf Mobile
- Limiter particules et transparences
- Object pooling pour bullets/VFX
- Limiter PostProcessing lourd
- Éviter trop d'Update coûteux

---

## 6. Roadmap d'implémentation (ordre recommandé)

| # | Tâche | Priorité | Status |
|---|-------|----------|--------|
| 1 | UX VRF loading (overlay + états + retry) | Haute | En cours |
| 2 | Reveal loot animé + SFX par rareté | Haute | Pending |
| 3 | Écran EndRun résumé + bouton Share | Moyenne | Pending |
| 4 | Inventaire MVP (liste + equip slots) | Haute | Pending |
| 5 | Polish combat (hit feedback + VFX + camera) | Moyenne | Pending |
| 6 | Mobile pass (safe area + tailles + perf) | Haute | Pending |

---

## Notes techniques
- UI : UGUI (Canvas/TMP)
- Cible : WebGL d'abord, puis mobile (Android/iOS)
- VRF : Switchboard Crossbar sur Monad

---

## Configuration Unity : VRF Loading UI

### Structure UI à créer dans le Canvas

```
Canvas
└── VRFLoadingOverlay (Panel)
    ├── Background (Image, noir semi-transparent)
    ├── ContentPanel (Panel centré)
    │   ├── Title (TMP_Text) → "Génération du Seed"
    │   ├── Spinner (Image avec rotation) → icône de chargement
    │   ├── ProgressBar (Slider)
    │   │   └── Fill (Image)
    │   ├── StatusText (TMP_Text) → "En attente du wallet..."
    │   ├── DetailsText (TMP_Text, caché par défaut)
    │   └── ButtonsPanel (Horizontal Layout)
    │       ├── RetryButton (Button + TMP_Text)
    │       ├── CancelButton (Button + TMP_Text)
    │       └── DetailsButton (Button + TMP_Text)
    └── VRFLoadingUI (Script)
    └── VRFLoadingUIBridge (Script)
```

### Étapes de configuration

1. **Créer le panel VRFLoadingOverlay**
   - Créer un Panel enfant du Canvas principal
   - Ajouter un CanvasGroup (pour le fade)
   - Stretch pour couvrir tout l'écran
   - Background noir avec alpha ~0.7

2. **Ajouter les éléments UI**
   - Title : TMP_Text en haut, taille ~36
   - Spinner : Image (icône circulaire), va tourner automatiquement
   - ProgressBar : Slider horizontal, désactiver l'interactivité
   - StatusText : TMP_Text centré, taille ~24
   - Boutons : 3 boutons (Retry, Cancel, Details)

3. **Attacher les scripts**
   - Ajouter `VRFLoadingUI` au panel root
   - Ajouter `VRFLoadingUIBridge` au même objet
   - Assigner toutes les références dans l'Inspector

4. **Configurer RunGameController**
   - Dans l'Inspector de `RunGameController`, assigner le champ `VRF Loading UI` vers le `VRFLoadingUIBridge`

### Champs à assigner dans VRFLoadingUI

| Champ | Objet à assigner |
|-------|------------------|
| Overlay Root | Le panel racine VRFLoadingOverlay |
| Canvas Group | Le CanvasGroup du panel |
| Spinner Object | L'image du spinner |
| Progress Bar | Le Slider |
| Progress Fill | L'Image Fill du Slider |
| Title Text | Le TMP_Text du titre |
| Status Text | Le TMP_Text du statut |
| Details Text | Le TMP_Text des détails |
| Retry Button | Le bouton Retry |
| Cancel Button | Le bouton Cancel |
| Details Button | Le bouton Details |

### Comportement attendu

- **Run Seed** : L'overlay apparaît au démarrage de la run (si VRF activé)
- **Boss Loot** : L'overlay apparaît quand on clique sur "VRF Loot"
- **Reroll Seed** : L'overlay apparaît quand on clique sur "Reroll"
- **Progression** : La barre de progression avance selon les étapes VRF
- **Erreur** : Affiche le message d'erreur + bouton Retry
- **Succès** : Auto-hide après 1.5 secondes

---

## Configuration Unity : Loot Reveal UI

### Structure UI à créer

```
Canvas
└── LootRevealOverlay (Panel)
    ├── Background (Image, noir semi-transparent)
    ├── ContentPanel (Panel centré)
    │   ├── ItemFrame (Image) → bordure colorée selon rareté
    │   ├── ItemGlow (Image) → halo lumineux
    │   ├── ItemIcon (Image) → icône du loot
    │   ├── RarityText (TMP_Text) → "LÉGENDAIRE"
    │   ├── ItemNameText (TMP_Text) → nom de l'item
    │   ├── ItemDescriptionText (TMP_Text) → description
    │   └── ButtonsPanel
    │       ├── ContinueButton
    │       └── EquipButton
    └── LootRevealUI (Script)
```

### Particles (optionnel mais recommandé)
- **RevealParticles** : burst de particules au moment du reveal
- **GlowParticles** : particules continues pour les items Rare+

### Audio (optionnel mais recommandé)
- Créer un AudioSource sur le panel
- Assigner 4 clips SFX différents selon la rareté :
  - Common : son simple
  - Rare : son plus brillant
  - Epic : son magique
  - Legendary : son épique avec écho

### Couleurs par rareté (par défaut)
| Rareté | Couleur |
|--------|---------|
| Common | Gris (#B3B3B3) |
| Rare | Bleu (#3380FF) |
| Epic | Violet (#9933CC) |
| Legendary | Or (#FFB333) |

### Comportement
- L'overlay apparaît automatiquement après un boss loot VRF
- Animation : fade in → delay → icon scale up avec pop → texte fade in
- Boutons apparaissent après l'animation
- "Continue" ferme l'overlay
- "Equip" équipe l'item (si système d'inventaire actif)

---

## Configuration Unity : Inventaire UI

### Structure UI à créer

```
Canvas
└── InventoryPanel (Panel)
    ├── Header
    │   ├── TitleText (TMP_Text) → "INVENTAIRE"
    │   ├── CollectionProgressText (TMP_Text) → "Collection: 3/12 (25%)"
    │   └── CloseButton (Button)
    ├── ContentArea
    │   ├── ItemGrid (GridLayoutGroup)
    │   │   └── [InventorySlot Prefab instances]
    │   └── DetailsPanel
    │       ├── DetailIcon (Image)
    │       ├── DetailFrame (Image)
    │       ├── DetailName (TMP_Text)
    │       ├── DetailRarity (TMP_Text)
    │       ├── DetailDescription (TMP_Text)
    │       └── DetailEffect (TMP_Text)
    └── InventoryUI (Script)
```

### Prefab InventorySlot

```
InventorySlot (Button)
├── Frame (Image) → bordure colorée
├── Icon (Image) → icône de l'item
├── Glow (Image) → halo pour items rares+
├── LockedOverlay (Image) → overlay gris pour items non possédés
├── NewBadge (GameObject) → badge "NEW!"
└── InventorySlot (Script)
```

### Champs à assigner dans InventoryUI

| Champ | Description |
|-------|-------------|
| Panel Root | Le panel racine |
| Canvas Group | Pour le fade |
| Item Grid Parent | Le Transform du GridLayoutGroup |
| Item Slot Prefab | Le prefab InventorySlot |
| Details Panel | Le panel de détails |
| Detail Icon/Frame/Name/Rarity/Description/Effect | Éléments du panel détails |
| Collection Progress Text | Texte de progression |
| Close Button | Bouton fermer |

### Comportement
- Affiche tous les items (possédés en couleur, non-possédés en gris)
- Cliquer sur un slot affiche les détails
- Items non-possédés affichent "???" comme nom
- Badge "NEW!" sur le dernier item acquis
- Hover scale sur les slots

---

## Configuration Unity : Polish Combat

### HitFeedbackController (sur les ennemis/boss)

Attacher `HitFeedbackController` sur chaque prefab ennemi :

| Champ | Description |
|-------|-------------|
| Enable Flash | Active le flash blanc au hit |
| Flash Color | Couleur du flash (blanc par défaut) |
| Flash Duration | Durée du flash (0.1s) |
| Enable Scale Pop | Active le "pop" de scale |
| Scale Pop | Multiplicateur de scale (1.15) |
| Scale Pop Duration | Durée du pop (0.1s) |
| Target Renderers | Renderers à flasher (auto-détecté si vide) |
| Scale Target | Transform à scaler (self si vide) |

### CameraShakeController (sur la caméra)

Attacher `CameraShakeController` sur la caméra principale ou son parent :

| Champ | Description |
|-------|-------------|
| Default Intensity | Intensité par défaut (0.15) |
| Default Duration | Durée par défaut (0.2s) |
| Shake Curve | Courbe d'atténuation |
| Max Intensity | Limite max (0.5) |
| Cooldown Between Shakes | Délai minimum entre shakes (0.05s) |

### Intégration automatique
- Les ennemis déclenchent `HitFeedback` à chaque hit
- Le boss déclenche aussi un camera shake léger à chaque hit
- Gros camera shake à la mort du boss

---

## Configuration Unity : Mobile Pass

### SafeAreaHandler

Attacher `SafeAreaHandler` sur le panel racine de l'UI (enfant direct du Canvas) :

```
Canvas
└── SafeAreaContainer (Panel + SafeAreaHandler)
    └── [Tout le contenu UI ici]
```

| Champ | Description |
|-------|-------------|
| Apply On Start | Appliquer au démarrage |
| Update Every Frame | Mettre à jour en continu (rotation) |
| Apply Top/Bottom/Left/Right | Côtés à ajuster |

### MobileUIScaler

Attacher `MobileUIScaler` sur le Canvas (avec le CanvasScaler) :

| Champ | Description |
|-------|-------------|
| Reference Resolution | Résolution de référence (1080x1920) |
| Match Width Or Height | Balance width/height (0.5) |
| Auto Detect Orientation | Ajuste selon l'orientation |
| Min Button Size | Taille min tactile en dp (48) |

### Checklist Mobile

- [ ] Canvas Scaler en mode "Scale With Screen Size"
- [ ] SafeAreaHandler sur le container principal
- [ ] Boutons min 48dp (environ 96px sur écran 2x)
- [ ] Textes min 14sp pour lisibilité
- [ ] Éviter les éléments trop près des bords
- [ ] Tester en mode portrait ET paysage
- [ ] Limiter les particules actives simultanément
- [ ] Utiliser object pooling pour bullets/VFX

### Recommandations de tailles

| Élément | Taille minimum |
|---------|----------------|
| Boutons principaux | 56dp (112px @2x) |
| Boutons secondaires | 48dp (96px @2x) |
| Icônes tactiles | 44dp (88px @2x) |
| Texte corps | 14-16sp |
| Texte titre | 20-24sp |
| Espacement tactile | 8dp entre éléments |
