# Fashion Sense Buffs

Automatically applies buffs when you wear a mapped [Fashion Sense](https://www.nexusmods.com/stardewvalley/mods/9969) outfit. Outfit-to-buff mappings are configurable in `assets/outfits.json` or via [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915) packs from other mods.

A common use case is pairing weather-themed outfits with protection buffs from [Project Danger Weather](https://www.nexusmods.com/stardewvalley/mods/18240) (or any mod that adds entries to `Data/Buffs`).

## Requirements

| Mod | Required |
| --- | --- |
| [SMAPI](https://smapi.io/) 4.0+ | Yes |
| [Fashion Sense](https://www.nexusmods.com/stardewvalley/mods/9969) | Yes |
| [Content Patcher](https://www.nexusmods.com/stardewvalley/mods/1915) | No (only needed for CP packs) |
| [Generic Mod Config Menu](https://www.nexusmods.com/stardewvalley/mods/5098) | No (optional in-game settings UI) |

## Installation

1. Install SMAPI and Fashion Sense.
2. Download **Fashion Sense Buffs** and unzip it into your Stardew Valley `Mods` folder.
3. Launch the game once with SMAPI. The mod creates a config file at `Mods/FashionSenseBuffs/config.json`.

To use pre-made mappings, also install a Content Patcher pack from the [`examples/`](examples/) folder (see [Examples](#examples)).

## How it works

When your active Fashion Sense outfit changes, the mod looks up that outfit's ID in its outfit data and applies the listed buffs. Switching to an unmapped outfit (or clearing your outfit) removes any buffs this mod applied.

- Mappings reload when a save is loaded and each morning, so Content Patcher conditions (season, weather, flags, etc.) stay in sync.
- Outfit IDs are matched case-insensitively.
- Unknown buff IDs are skipped with a SMAPI warning.

## Configuration

### In-game (Generic Mod Config Menu)

If GMCM is installed, open **Mod Options → Fashion Sense Buffs** to:

- Toggle outfit buffs on or off (disabling immediately removes active buffs).
- View all currently loaded outfit → buff mappings.

### `assets/outfits.json`

Edit `Mods/FashionSenseBuffs/assets/outfits.json` to add mappings directly:

```json
{
  "WinterCoat": {
    "BuffIds": [ "PDWKittyMuffs" ]
  },
  "FullRainGear": {
    "BuffIds": [ "PDWShader", "PDWMask" ]
  }
}
```

Each key is a Fashion Sense **outfit ID** (not the display name). Each entry's `BuffIds` array lists buff IDs from `Data/Buffs`.

Built-in data loads at low priority, so Content Patcher `EditData` patches always override these entries.

### Content Patcher

Other mods can patch the shared asset `hierocles.FashionSenseBuffs/Outfits` with `EditData`. See the full guide in [docs/content-patcher-guide.md](docs/content-patcher-guide.md) for data format, conditional patches, removing entries, and manifest templates.

Minimal example:

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

## Finding outfit IDs

The outfit ID is Fashion Sense's internal name, not the label shown in the outfit picker.

1. **SMAPI trace log** — With SMAPI debug logging enabled, switch outfits in-game. When a mapped buff is applied, the log shows the outfit ID:

   ```text
   [Fashion Sense Buffs] Applied buff 'PDWKittyMuffs' for outfit 'WinterCoat'.
   ```

2. **Fashion Sense content packs** — Check the keys in that pack's `preset_outfits.json` or `Outfits` data asset.

## Examples

The [`examples/`](examples/) folder contains sample Content Patcher packs and helper scripts:

| Path | Description |
| --- | --- |
| [`examples/[CP] PDW Outfit Buffs/`](examples/%5BCP%5D%20PDW%20Outfit%20Buffs/) | Maps Fern preset Project Danger Weather outfits to PDW protection buffs. Requires `kath.weathering`. |
| [`examples/[CP] Weather Wonders Outfit Buffs/`](examples/%5BCP%5D%20Weather%20Wonders%20Outfit%20Buffs/) | Similar mappings for Weather Wonders–style outfit names. |
| [`examples/FernPreset/Outfits.json`](examples/FernPreset/Outfits.json) | Generated Fashion Sense outfit preset (see `scripts/generate_fern_outfits.py`). |
| [`scripts/`](scripts/) | Python helpers for generating example CP content from outfit presets. |

Copy a `[CP]` folder into your `Mods` directory to use it as a standalone pack.

## Building from source

Requires [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0) and a local Fashion Sense DLL reference at `reference/Fashion Sense/FashionSense/FashionSense.dll` (copy from your installed Fashion Sense mod).

```bash
dotnet build
```

SMAPI's mod build config copies the output to your game's `Mods` folder when `EnableModDeploy` is configured, or you can copy `bin/Debug/net6.0/` manually.

## License

[MIT](LICENSE)
