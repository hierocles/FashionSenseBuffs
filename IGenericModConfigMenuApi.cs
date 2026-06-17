#nullable disable
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace GenericModConfigMenu;

/// <summary>
/// Trimmed copy of <see href="https://github.com/spacechase0/StardewValleyMods/blob/develop/framework/GenericModConfigMenu/IGenericModConfigMenuApi.cs">IGenericModConfigMenuApi</see>.
/// Only methods used by this mod are included.
/// </summary>
public interface IGenericModConfigMenuApi
{
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);

    void AddParagraph(IManifest mod, Func<string> text);

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string> tooltip = null,
        string fieldId = null);

    void AddPage(IManifest mod, string pageId, Func<string> pageTitle = null);

    void AddPageLink(IManifest mod, string pageId, Func<string> text, Func<string> tooltip = null);

    void AddComplexOption(
        IManifest mod,
        Func<string> name,
        Action<SpriteBatch, Vector2> draw,
        Func<string> tooltip = null,
        Action beforeMenuOpened = null,
        Action beforeSave = null,
        Action afterSave = null,
        Action beforeReset = null,
        Action afterReset = null,
        Action beforeMenuClosed = null,
        Func<int> height = null,
        string fieldId = null);
}
