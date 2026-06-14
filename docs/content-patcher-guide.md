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
          "BuffIds": [ "MyMod.MyBuff" ]
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

| Field | Type | Description |
| --- | --- | --- |
| Key | `string` | The Fashion Sense outfit ID (see [Finding Outfit IDs](#finding-outfit-ids)) |
| `BuffIds` | `string[]` | One or more buff IDs from `Data/Buffs` to apply while this outfit is active |

### Example — single buff

```json
{
  "Action": "EditData",
  "Target": "hierocles.FashionSenseBuffs/Outfits",
  "Entries": {
    "WarmWinterCoat": {
      "BuffIds": [ "PDWKittyMuffs" ]
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
      "BuffIds": [ "PDWShader", "PDWMask" ]
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
    "WarmWinterCoat":  { "BuffIds": [ "PDWKittyMuffs" ] },
    "FullRainGear":    { "BuffIds": [ "PDWShader", "PDWMask" ] },
    "DesertExplorer":  { "BuffIds": [ "PDWUVGoggles" ] },
    "BugProofSuit":    { "BuffIds": [ "PDWBugNet" ] }
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
    "WarmWinterCoat": { "BuffIds": [ "PDWKittyMuffs" ] }
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

---

## Finding Outfit IDs

The outfit ID is the internal name Fashion Sense uses, not the display name shown in its UI.

The easiest ways to find it:

1. **SMAPI console** — Enable SMAPI's debug mode and switch outfits in-game. Fashion Sense Buffs logs the active outfit ID at `TRACE` level each time it changes:

   ```text
   [FashionSenseBuffs] Applied buff 'PDWKittyMuffs' for outfit 'WarmWinterCoat'.
   ```

   If no buff is mapped yet, the ID still appears in the trace log when the outfit-change check runs.

2. **Fashion Sense outfit files** — In a Fashion Sense content pack, the outfit ID is the key used in `preset_outfits.json` or the `Outfits` data asset.

3. **SMAPI console command** — Fashion Sense may expose a console command to list outfit IDs; check its own documentation.

---

## Buff IDs

`BuffIds` accepts any buff ID registered in `Data/Buffs`. These can come from any mod.

**Project Danger Weather buff IDs** (requires `kath.weathering`):

| Buff ID | Effect |
| --- | --- |
| `PDWKittyMuffs` | Cold weather protection |
| `PDWShader` | Sun/UV protection |
| `PDWMask` | General protection |
| `PDWUVGoggles` | UV goggle protection |
| `PDWBugNet` | Bug protection |

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
    { "UniqueID": "PeacefulEnd.FashionSense",        "IsRequired": true },
    { "UniqueID": "hierocles.FashionSenseBuffs",      "IsRequired": true }
  ],
  "UpdateKeys": []
}
```
