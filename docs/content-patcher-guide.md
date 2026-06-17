# Fashion Sense Buffs — Content Patcher Guide

Fashion Sense Buffs exposes a patchable data asset that lets you map Fashion Sense outfit IDs to any buff IDs defined in `Data/Buffs`. This works with buffs from any mod — not just Project Danger Weather.

---

## Quick Start

Add Fashion Sense Buffs as a dependency in your `manifest.json`:

```json
{
    "Dependencies": [
        { "UniqueID": "hierocles.FashionSenseBuffs", "IsRequired": true }
    ]
}
```

Then use `EditData` in your `content.json` to register outfit → buff mappings:

```json
{
    "Format": "2.4.0",
    "Changes": [
        {
            "Action": "EditData",
            "Target": "hierocles.FashionSenseBuffs/Outfits",
            "Entries": {
                "MyOutfitId": {
                    "BuffIds": ["MyMod.MyBuff"]
                }
            }
        }
    ]
}
```

---

## Data Format

**Target:** `hierocles.FashionSenseBuffs/Outfits`

The asset is a dictionary. Each entry maps one Fashion Sense outfit to a list of buffs.

| Field           | Type       | Description                                                                                                                                                                               |
| --------------- | ---------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Key             | `string`   | Primary Fashion Sense outfit name for this mapping (see [Finding Outfit IDs](#finding-outfit-ids)). |
| `BuffIds`       | `string[]` | One or more buff IDs from `Data/Buffs` to apply while this outfit is active                                                                                                               |
| `RemoveBuffIds` | `string[]` | Optional. Buff IDs to remove from the player while this outfit is active (e.g. weather debuffs that linger after protection is applied)                                                   |
| `OutdoorsOnly`  | `bool`     | Optional. When `true`, buffs and `RemoveBuffIds` only apply while the player is outdoors. Defaults to `false`. Use for weather protection buffs that should not persist indoors.        |
| `Aliases`       | `string[]` | Optional. Additional outfit names that share this mapping (exact match, case-insensitive). Use for seasonal, indoor, or mod-specific variants — see [Aliases](#aliases).                  |

### Example — single buff

```json
{
    "Action": "EditData",
    "Target": "hierocles.FashionSenseBuffs/Outfits",
    "Entries": {
        "WarmWinterCoat": {
            "BuffIds": ["PDWKittyMuffs"]
        }
    }
}
```

### Example — apply and remove buffs

Use `RemoveBuffIds` when a weather mod applies debuffs before your protection buff is active, or re-applies them on a timer. While the outfit is worn and all `BuffIds` are active, listed debuffs are stripped after weather mods run each tick.

PDW uses opaque internal IDs (e.g. `SomeWeather1` = “Some Weather Huh?” sludge-walk debuff on Heavy Rain). See `examples/[CP] PDW Outfit Buffs/buff-id-reference.txt` for a full mapping. When protection buffs like `PDWShader` are active, PDW normally skips applying the debuff; `RemoveBuffIds` mainly clears debuffs already present before you switched outfit.

```json
{
    "Action": "EditData",
    "Target": "hierocles.FashionSenseBuffs/Outfits",
    "Entries": {
        "AcidRainGear": {
            "BuffIds": ["PDWShader"],
            "RemoveBuffIds": ["Firerain"],
            "OutdoorsOnly": true
        }
    }
}
```

### Example — multiple buffs on one outfit

```json
{
    "Action": "EditData",
    "Target": "hierocles.FashionSenseBuffs/Outfits",
    "Entries": {
        "FullRainGear": {
            "BuffIds": ["PDWShader", "PDWMask"]
        }
    }
}
```

### Example — multiple outfits in one patch

```json
{
    "Action": "EditData",
    "Target": "hierocles.FashionSenseBuffs/Outfits",
    "Entries": {
        "WarmWinterCoat": { "BuffIds": ["PDWKittyMuffs"] },
        "FullRainGear": { "BuffIds": ["PDWShader", "PDWMask"] },
        "DesertExplorer": { "BuffIds": ["PDWUVGoggles"] },
        "BugProofSuit": { "BuffIds": ["PDWBugNet"] }
    }
}
```

---

## Using Content Patcher Conditions

Because the asset is invalidated each morning, CP conditions that change day-to-day (season, weather, year, flags) work as expected.

### Example — only grant a buff in winter

```json
{
    "Action": "EditData",
    "Target": "hierocles.FashionSenseBuffs/Outfits",
    "Entries": {
        "WarmWinterCoat": { "BuffIds": ["PDWKittyMuffs"] }
    },
    "When": {
        "Season": "Winter"
    }
}
```

When the season changes away from winter the entry is removed from the asset on the next morning reload, and the buff is no longer applied.

---

## Removing an Entry

Use `"null"` as the value to delete an entry added by another mod:

```json
{
    "Action": "EditData",
    "Target": "hierocles.FashionSenseBuffs/Outfits",
    "Entries": {
        "SomeOtherModsOutfit": null
    }
}
```

### Example — aliases for outfit name variants

Fashion Sense exposes outfit **names** only — not a separate internal ID. Mods like Farmer 2.0 ESWF use many names for the same weather outfit (`Heavy Rain`, `Heavy Rain Indoor`, `Spring Indoor Rain`, etc.). Map the canonical name once and list the rest in `Aliases`:

```json
{
    "Action": "EditData",
    "Target": "hierocles.FashionSenseBuffs/Outfits",
    "Entries": {
        "Heavy Rain": {
            "BuffIds": ["PDWShader"],
            "RemoveBuffIds": ["SomeWeather1"],
            "OutdoorsOnly": true,
            "Aliases": [
                "Heavy Rain Indoor",
                "Spring Indoor Rain",
                "Summer Indoor Rain",
                "Fall Indoor Rain",
                "Winter Indoor Rain"
            ]
        }
    }
}
```

If SMAPI logs `No buff mapping for outfit '…'`, add that exact string to `Aliases` on the matching entry (or as a new primary key). Lookup is exact match only — no automatic fuzzy resolution.

---

## Aliases

Use `Aliases` when multiple Fashion Sense outfit names should share one buff mapping. Each alias is matched **exactly** (case-insensitive) against the name Fashion Sense reports via its API.

Typical uses:

- **Indoor variants** — `{Weather} Indoor` or `{Season} Indoor {Weather}` (ESWF naming)
- **Seasonal outdoor names** — `Acid Rain Spring`, `Dry Lightning Fall`, etc.
- **Renamed presets** — `Heatwaves Spring` as an alias for a `Heat Wave` entry

The primary dictionary key is also registered as a lookup name. Duplicate alias names across entries log a debug warning; the last loaded entry wins.

Per-mapping outdoors-only overrides can also be toggled on the **Overrides** page in Generic Mod Config Menu without editing Content Patcher packs.

---

## Finding Outfit IDs

The outfit ID is the internal name Fashion Sense uses, not the display name shown in its UI.

The easiest ways to find it:

1. **SMAPI console** — Enable SMAPI's debug mode and switch outfits in-game. Fashion Sense Buffs logs the active outfit ID at `TRACE` level each time it changes:

    ```text
    [FashionSenseBuffs] Applied buff 'PDWKittyMuffs' for outfit 'WarmWinterCoat' (mapping 'WarmWinterCoat').
    ```

    If no mapping exists, a `DEBUG` line is logged once per outfit: `No buff mapping for outfit '…'`.

2. **Fashion Sense outfit files** — In a Fashion Sense content pack, the outfit ID is the key used in `preset_outfits.json` or the `Outfits` data asset.

---

## Buff IDs

`BuffIds` accepts any buff ID registered in `Data/Buffs`. These can come from any mod.

**Project Danger Weather buff IDs** (requires `kath.weathering`):

| Buff ID         | Effect                  |
| --------------- | ----------------------- |
| `PDWKittyMuffs` | Cold weather protection |
| `PDWShader`     | Sun/UV protection       |
| `PDWMask`       | General protection      |
| `PDWUVGoggles`  | UV goggle protection    |
| `PDWBugNet`     | Bug protection          |

For buffs from other mods, check that mod's documentation or `Data/Buffs` entries for the correct ID string.

---

## Load Order

Fashion Sense Buffs provides its built-in `assets/outfits.json` at `AssetLoadPriority.Low`, so any `EditData` patch from a CP pack will always win. You do not need to worry about patch ordering relative to the base data.

If two CP packs both add an entry with the same outfit ID, standard CP conflict resolution applies (last patch wins based on load order).

---

## Minimal manifest.json Template

```json
{
    "Name": "My Outfit Buffs",
    "Author": "YourName",
    "Version": "1.0.0",
    "Description": "Adds buff mappings for my Fashion Sense outfits.",
    "UniqueID": "YourName.MyOutfitBuffs",
    "ContentPackFor": {
        "UniqueID": "Pathoschild.ContentPatcher"
    },
    "Dependencies": [
        {
            "UniqueID": "PeacefulEnd.FashionSense",
            "IsRequired": true
        },
        {
            "UniqueID": "hierocles.FashionSenseBuffs",
            "IsRequired": true
        }
    ],
    "UpdateKeys": []
}
```
