﻿using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StopRugRemoval.HarmonyPatches;

#if DEBUG // not yet finished implementing....
/// <summary>
/// Patches on GameLocation to allow me to place rugs anywhere.
/// </summary>
[HarmonyPatch(typeof(GameLocation))]
internal class GameLocationPatches
{
    [SuppressMessage("StyleCop", "SA1313", Justification = "Style prefered by Harmony")]
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameLocation.CanPlaceThisFurnitureHere))]
    private static void PostfixCanPlaceFurnitureHere(GameLocation __instance, Furniture __0, ref bool __result)
    {
        try
        {
            if (__result // can already be placed
                || __0.placementRestriction != 0 // someone requested a custom placement restriction, respect that.
                || !ModEntry.Config.Enabled || !ModEntry.Config.CanPlaceRugsOutside // mod disabled
                || __instance is MineShaft || __instance is VolcanoDungeon // do not want to affect mines
                || !__0.furniture_type.Value.Equals(Furniture.rug) // only want to affect rugs
                )
            {
                return;
            }
            __result = true;
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed in attempting to place rug outside in PostfixCanPlaceFurnitureHere.\n{ex}", LogLevel.Error);
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(GameLocation.makeHoeDirt))]
    [SuppressMessage("StyleCop", "SA1313", Justification = "Style prefered by Harmony")]
    private static bool PrefixMakeHoeDirt(GameLocation __instance, Vector2 tileLocation, bool ignoreChecks = false)
    {
        if (ignoreChecks || !ModEntry.Config.PreventHoeingRugs)
        {
            return true;
        }
        (int posX, int posY) = ((tileLocation * 64f) + new Vector2(32f, 32f)).ToPoint();
        foreach (Furniture f in __instance.furniture)
        {
            if (f.furniture_type.Value == Furniture.rug && f.getBoundingBox(f.TileLocation).Contains(posX, posY))
            {
                return false;
            }
        }
        return true;
    }
}

#endif