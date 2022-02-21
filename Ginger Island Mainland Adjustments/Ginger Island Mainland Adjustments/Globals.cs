﻿using System.Reflection;
using AtraShared.Schedules;

namespace GingerIslandMainlandAdjustments;

/// <summary>
/// Class to handle global variables.
/// </summary>
internal static class Globals
{
    // Values are set in the Mod.Entry method, so should never be null.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Gets SMAPI's logging service.
    /// </summary>
    internal static IMonitor ModMonitor { get; private set; }

    /// <summary>
    /// Gets or sets mod configuration class.
    /// </summary>
    internal static ModConfig Config { get; set; }

    /// <summary>
    /// Gets SMAPI's reflection helper.
    /// </summary>
    internal static IReflectionHelper ReflectionHelper { get; private set; }

    /// <summary>
    /// Gets SMAPI's Content helper.
    /// </summary>
    internal static IContentHelper ContentHelper { get; private set; }

    /// <summary>
    /// Gets SMAPI's mod registry helper.
    /// </summary>
    internal static IModRegistry ModRegistry { get; private set; }

    /// <summary>
    /// Gets SMAPI's helper class.
    /// </summary>
    internal static IModHelper Helper { get; private set; }

    /// <summary>
    /// Gets the instance of the schedule utility functions.
    /// </summary>
    internal static ScheduleUtilityFunctions UtilitySchedulingFunctions { get; private set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

    /// <summary>
    /// Gets a reference to  of Child2NPC's ModEntry.IsChildNPC.
    /// </summary>
    /// <remarks>Null if C2NPC is not installed or method not found.</remarks>
    internal static Func<NPC, bool>? IsChildToNPC { get; private set; }

    /// <summary>
    /// Initialize globals, including reading config file.
    /// </summary>
    /// <param name="helper">SMAPI's IModHelper.</param>
    /// <param name="monitor">SMAPI's logging service.</param>
    internal static void Initialize(IModHelper helper, IMonitor monitor)
    {
        Globals.ModMonitor = monitor;
        Globals.ReflectionHelper = helper.Reflection;
        Globals.ContentHelper = helper.Content;
        Globals.ModRegistry = helper.ModRegistry;
        Globals.Helper = helper;

        try
        {
            Globals.Config = helper.ReadConfig<ModConfig>();
        }
        catch
        {
            Globals.ModMonitor.Log(I18n.IllFormatedConfig(), LogLevel.Warn);
            Globals.Config = new();
        }

        UtilitySchedulingFunctions = new(
            monitor: Globals.ModMonitor,
            reflectionHelper: Globals.ReflectionHelper,
            getStrictTiming: Globals.GetStrictTiming,
            gOTOINFINITELOOP: I18n.GOTOINFINITELOOP,
            gOTOSCHEDULENOTFOUND: I18n.GOTOSCHEDULENOTFOUND,
            gOTOILLFORMEDFRIENDSHIP: I18n.GOTOILLFORMEDFRIENDSHIP,
            gOTOSCHEDULEFRIENDSHIP: I18n.GOTOSCHEDULEFRIENDSHIP,
            sCHEDULEPARSEFAILURE: I18n.SCHEDULEPARSEFAILURE,
            sCHEDULEREGEXFAILURE: I18n.SCHEDULEREGEXFAILURE,
            nOREPLACEMENTLOCATION: I18n.NOREPLACEMENTLOCATION,
            tOOTIGHTTIMELINE: I18n.TOOTIGHTTIMELINE,
            rEGEXTIMEOUTERROR: I18n.REGEXTIMEOUTERROR);
    }

    /// <summary>
    /// Tries to get a handle on Child2NPC's IsChildNPC.
    /// </summary>
    /// <returns>True if successful, false otherwise.</returns>
    internal static bool GetIsChildToNPC()
    {
        if (ModRegistry.Get("Loe2run.ChildToNPC") is null)
        {
            ModMonitor.Log($"Child2NPC not installed - no need to adjust for that.", LogLevel.Trace);
            return false;
        }
        if (Type.GetType("ChildToNPC.ModEntry, ChildToNPC")?.GetMethod("IsChildNPC", new Type[] { typeof(Character) }) is MethodInfo childToNPCMethod)
        {
            IsChildToNPC = (Func<NPC, bool>)Delegate.CreateDelegate(typeof(Func<NPC, bool>), childToNPCMethod);
            return true;
        }
        ModMonitor.Log("IsChildNPC method not found - integration with Child2NPC failed.", LogLevel.Warn);
        return false;
    }

    /// <summary>
    /// Gets whether or not strict timing is desired here.
    /// </summary>
    /// <returns>True for use strict timing, false otherwise.</returns>
    internal static bool GetStrictTiming()
        => Globals.Config.EnforceGITiming;
}
