﻿using AtraCore.Framework.Caches;
using System.Text.RegularExpressions;

using AtraShared.Caching;
using AtraShared.ItemManagement;
using AtraShared.Menuing;
using AtraShared.Utils;

using StardewModdingAPI.Events;

using StardewValley.Locations;

using StardewValley.Menus;

using xTile.Dimensions;
using xTile.ObjectModel;

using XTile = xTile.Tiles.Tile;
using AtraShared.Utils.Extensions;

namespace GrowableBushes.Framework;
internal static class ShopManager
{
    private const string BUILDING = "Buildings";
    private const string SHOPNAME = "atravita.BushShop";

    private static IAssetName sunHouse = null!;
    private static IAssetName mail = null!;

    private static TickCache<bool> IslandUnlocked = new(() => FarmerHelpers.HasAnyFarmerRecievedFlag("seenBoatJourney"));

    internal static void Initialize(IGameContentHelper parser)
    {
        sunHouse = parser.ParseAssetName("Maps/Sunroom");
        mail = parser.ParseAssetName("Data/mail");
    }

    internal static void OnAssetRequested(AssetRequestedEventArgs e)
    {
        if (e.NameWithoutLocale.IsEquivalentTo(mail))
        {
            e.Edit(static (asset) =>
            {
                asset.AsDictionary<string, string>().Data[SHOPNAME] = I18n.Caroline_Mail();
            });
        }
        else if (e.NameWithoutLocale.IsEquivalentTo(sunHouse))
        {
            e.Edit(
                static (asset) =>
                {
                    IAssetDataForMap? map = asset.AsMap();
                    XTile? tile = map.Data.GetLayer(BUILDING).PickTile(new Location((int)ModEntry.Config.ShopLocation.X * 64, (int)ModEntry.Config.ShopLocation.Y * 64), Game1.viewport.Size);
                    if (tile is null)
                    {
                        ModEntry.ModMonitor.Log($"Tile could not be edited for shop, please let atra know!", LogLevel.Warn);
                        return;
                    }
                    tile.Properties["Action"] = new PropertyValue(SHOPNAME);
                },
                AssetEditPriority.Default + 10);
        }
    }

    internal static void OnDayEnd(DayEndingEventArgs e)
    {
        if (Game1.getOnlineFarmers().Any((farmer) => farmer.eventsSeen.Contains(719926)))
        {
            Game1.addMailForTomorrow(mailName: SHOPNAME, noLetter: false, sendToEveryone: true);
        }
    }

    /// <inheritdoc cref="IInputEvents.ButtonPressed"/>
    internal static void OnButtonPressed(ButtonPressedEventArgs e, IInputHelper input)
    {
        if ((!e.Button.IsActionButton() && !e.Button.IsUseToolButton())
            || !MenuingExtensions.IsNormalGameplay())
        {
            return;
        }

        if (Game1.currentLocation.Name != "Sunroom"
            || Game1.currentLocation.doesTileHaveProperty((int)e.Cursor.GrabTile.X, (int)e.Cursor.GrabTile.Y, "Action", BUILDING) != SHOPNAME)
        {
            return;
        }

        input.SurpressClickInput();

        Dictionary<ISalable, int[]> sellables = new(20);

        foreach (var bushIndex in BushSizesExtensions.GetValues())
        {
            int[] sellData;
            if (bushIndex is BushSizes.Walnut or BushSizes.Harvested && !IslandUnlocked.GetValue())
            {
                continue;
            }
            else if (bushIndex is BushSizes.Medium)
            {
                sellData = new[] { 500, ShopMenu.infiniteStock };
            }
            else
            {
                sellData = new[] { 100, ShopMenu.infiniteStock };
            }

            InventoryBush bush = new(bushIndex, 1);
            _ = sellables.TryAdd(bush, sellData);
        }

        var shop = new ShopMenu(sellables, who: "Caroline") { storeContext = SHOPNAME };

        if (NPCCache.GetByVillagerName("Caroline") is NPC caroline)
        {
            shop.portraitPerson = caroline;
        }
        shop.potraitPersonDialogue = I18n.Shop_Message();
        Game1.activeClickableMenu = shop;
    }
}
