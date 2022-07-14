﻿using AtraCore.Utilities;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace SpecialOrdersExtended.Niceties;

/// <summary>
/// Holds event commands.
/// </summary>
[HarmonyPatch(typeof(Event))]
internal static class EventCommands
{
    private static void AddSpecialOrder(Event @event, GameLocation location, GameTime time, string[] split)
    {
        try
        {
            SpecialOrder order = SpecialOrder.GetSpecialOrder(split[1], Game1.random.Next());
            Game1.player.team.specialOrders.Add(order);
            MultiplayerHelpers.GetMultiplayer().globalChatInfoMessage("AcceptedSpecialOrder", Game1.player.Name, order.GetName());
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod failed while attempting to adding a special order:\n\n{ex}", LogLevel.Error);
        }

        @event.CurrentCommand++;
        @event.checkForNextCommand(location, time);
    }

    [HarmonyPrefix]
    [HarmonyPriority(Priority.VeryHigh)] // Need a high priority to slide in before spacecore, which has an unconditional prefix false.
    [HarmonyPatch(nameof(Event.tryEventCommand))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony convention.")]
    private static bool PrefixTryGetCommand(Event __instance, GameLocation location, GameTime time, string[] split)
    {
        if (split.Length < 2)
        {
            return true;
        }
        else if (split[0].Equals("atravita_addSpecialOrder", StringComparison.OrdinalIgnoreCase))
        {
            AddSpecialOrder(__instance, location, time, split);
            return false;
        }
        return true;
    }
}