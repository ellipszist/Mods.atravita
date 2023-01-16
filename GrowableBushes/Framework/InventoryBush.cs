﻿using System.Xml.Serialization;

using AtraCore.Framework.ReflectionManager;

using AtraShared.Utils.Extensions;

using Microsoft.Xna.Framework;

using StardewValley.TerrainFeatures;

namespace GrowableBushes.Framework;

/// <summary>
/// A bush in the inventory.
/// </summary>
[XmlType("Mods_atravita_InventoryBush")]
public sealed class InventoryBush : SObject
{
    [XmlIgnore]
    private int currentSeason = -1;

    [XmlIgnore]
    private Rectangle sourceRect = default;

    /// <summary>
    /// The prefix used for the internal name of these bushes.
    /// </summary>
    internal const string BushPrefix = "atravita.InventoryBush.";

    /// <summary>
    /// The moddata string used to mark the bushes planted with this mod.
    /// </summary>
    internal const string BushModData = "atravita.InventoryBush.Type";

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryBush"/> class.
    /// Constructor for the serializer.
    /// </summary>
    public InventoryBush() : base() {}

    /// <summary>
    /// Initializes a new instance of the <see cref="InventoryBush"/> class.
    /// </summary>
    /// <param name="whichBush">Which bush this inventory bush corresponds to.</param>
    /// <param name="initialStack">Initial stack of bushes.</param>
    public InventoryBush(BushSizes whichBush, int initialStack)
        : base((int)whichBush, initialStack, false, -1, 0)
    {
        if (!BushSizesExtensions.IsDefined(whichBush))
        {
            ModEntry.ModMonitor.Log($"Bush {whichBush.ToStringFast()} doesn't seem to be a valid bush? Setting as smol bush.", LogLevel.Error);
            this.ParentSheetIndex = (int)BushSizes.Small;
        }

        this.bigCraftable.Value = true;
        this.CanBeSetDown = true;
        this.Name = BushPrefix + ((BushSizes)this.ParentSheetIndex).ToStringFast();
        this.Edibility = inedible;
        this.Price = 0;
        this.Category = -15500057; // random negative integer.

        // just to make sure the bush texture is loaded.
        _ = Bush.texture.Value;
    }

    #region reflection

    /// <summary>
    /// Stardew's Bush::shake.
    /// </summary>
    private static readonly BushShakeDel BushShakeMethod = typeof(Bush)
        .GetCachedMethod("shake", ReflectionCache.FlagTypes.InstanceFlags)
        .CreateDelegate<BushShakeDel>();

    private delegate void BushShakeDel(
        Bush bush,
        Vector2 tileLocation,
        bool doEvenIfStillShaking);

    #endregion

    #region placement

    // TODO: actually check like boundaries?
    public override bool canBePlacedHere(GameLocation l, Vector2 tile) => true;

    public override bool placementAction(GameLocation location, int x, int y, Farmer? who = null)
    {
        BushSizes size = (BushSizes)this.ParentSheetIndex;

        Vector2 placementTile = new (x / Game1.tileSize, y / Game1.tileSize);
        Bush bush = new (placementTile, size.ToStardewBush(), location);

        // set metadata.
        switch (size)
        {
            case BushSizes.SmallAlt:
                bush.tileSheetOffset.Value = 1;
                break;
            case BushSizes.Harvested:
                bush.tileSheetOffset.Value = 0;
                break;
            case BushSizes.Medium:
            case BushSizes.Large:
                bush.townBush.Value = false;
                break;
            case BushSizes.Town:
            case BushSizes.TownLarge:
                bush.townBush.Value = true;
                break;
        }

        bush.setUpSourceRect();
        bush.modData.SetEnum(BushModData, size);
        location.largeTerrainFeatures.Add(bush);
        location.playSound("thudStep");
        BushShakeMethod(bush, placementTile, true);
        return true;
    }

    #endregion

    // TODO: draw, draw in menu, draw while holding overhead. SObject has too many draw methods XD
    // placement bounds too!

    #region misc

    /// <inheritdoc />
    public override bool canBeShipped() => false;

    /// <inheritdoc />
    public override bool canBeGivenAsGift() => false;

    /// <inheritdoc />
    public override bool canBeTrashed() => true;

    /// <inheritdoc />
    public override string getCategoryName() => I18n.Category();

    /// <inheritdoc />
    public override Color getCategoryColor() => Color.Green;

    /// <inheritdoc />
    public override bool canBePlacedInWater() => false;

    /// <inheritdoc />
    public override bool canStackWith(ISalable other)
    {
        if (other is not InventoryBush otherBush)
        {
            return false;
        }
        return this.ParentSheetIndex == otherBush.ParentSheetIndex;
    }

    /// <inheritdoc />
    protected override string loadDisplayName() => (BushSizes)this.ParentSheetIndex switch
    {
        BushSizes.Small => I18n.Bush_Small(),
        BushSizes.Medium => I18n.Bush_Medium(),
        BushSizes.Large => I18n.Bush_Large(),
        BushSizes.SmallAlt => I18n.Bush_Small_Alt(),
        BushSizes.Town => I18n.Bush_Town(),
        BushSizes.TownLarge => I18n.Bush_Town_Big(),
        BushSizes.Walnut => I18n.Bush_Walnut(),
        BushSizes.Harvested => I18n.Bush_Harvested(),
        _ => "Error Bush",
    };

    /// <inheritdoc />
    public override string getDescription() => (BushSizes)this.ParentSheetIndex switch
    {
        BushSizes.Small => I18n.Bush_Small_Description(),
        BushSizes.Medium => I18n.Bush_Medium_Description(),
        BushSizes.Large => I18n.Bush_Large_Description(),
        BushSizes.SmallAlt => I18n.Bush_Small_Alt_Description(),
        BushSizes.Town => I18n.Bush_Town_Description(),
        BushSizes.TownLarge => I18n.Bush_Town_Big_Description(),
        BushSizes.Walnut => I18n.Bush_Walnut_Description(),
        BushSizes.Harvested => I18n.Bush_Harvested_Description(),
        _ => "This should have not have happened. What. You should probably just trash this."
    };

    /// <inheritdoc />
    protected override void _PopulateContextTags(HashSet<string> tags)
    {
        tags.Add("category_inventory_bush");
        tags.Add($"id_inventoryBush_{this.ParentSheetIndex}");
        tags.Add("quality_none");
        tags.Add("item_" + this.SanitizeContextTag(this.Name));
    }

    #endregion

    #region helpers

    private static int GetSeason(GameLocation loc)
        => Utility.getSeasonNumber(Game1.GetSeasonForLocation(loc));
    
    // derived from Bush.setUpSourceREct
    private Rectangle GetSourceRectForSeason(int season)
    {
        switch ((BushSizes)this.ParentSheetIndex)
        {
            case BushSizes.Small:
                return new Rectangle(season * 32, 224, 16, 32);
            case BushSizes.SmallAlt:
                return new Rectangle((season * 32) + 16, 224, 16, 32);
            case BushSizes.Medium:
                int y = Math.DivRem(season * 64, Bush.texture.Value.Bounds.Width, out int x);
                return new Rectangle(x, y, 32, 48);
            case BushSizes.Town:
                return new Rectangle(season * 32, 96, 32, 32);
            case BushSizes.Large:
                return season switch
                {
                    0 or 1 => new Rectangle(0, 128, 48, 48),
                    2 => new Rectangle(48, 128, 48, 48),
                    _ => new Rectangle(0, 176, 48, 48),
                };
            default:
                return default;
        }
    }

    #endregion
}
