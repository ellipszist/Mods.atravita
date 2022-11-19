﻿using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AtraBase.Toolkit;
using AtraBase.Toolkit.Extensions;
using AtraCore.Framework.ItemManagement;
using AtraCore.Framework.ReflectionManager;
using AtraCore.Models;
using AtraShared.ConstantsAndEnums;
using AtraShared.Utils.Extensions;
using AtraShared.Utils.HarmonyHelper;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Netcode;

using StardewValley.Objects;

namespace AtraCore.HarmonyPatches.DrawPrismaticPatches;

#warning - finish this.

/// <summary>
/// Draws things with a prismatic tint or overlay.
/// </summary>
[HarmonyPatch]
internal static class DrawPrismatic
{
    private static readonly SortedList<ItemTypeEnum, Dictionary<int, Lazy<Texture2D>>> PrismaticMasks = new();
    private static readonly SortedList<ItemTypeEnum, HashSet<int>> PrismaticFull = new();

    #region LOADDATA

    /// <summary>
    /// Load the prismatic data.
    /// Called on SaveLoaded.
    /// </summary>
    internal static void LoadPrismaticData()
    {
        Dictionary<string, DrawPrismaticModel>? models = AssetManager.GetPrismaticModels();
        if (models is null)
        {
            return;
        }

        PrismaticFull.Clear();
        PrismaticMasks.Clear();

        foreach (DrawPrismaticModel? model in models.Values)
        {
            if (!int.TryParse(model.Identifier, out int id))
            {
                id = DataToItemMap.GetID(model.ItemType, model.Identifier);
                if (id == -1)
                {
                    ModEntry.ModMonitor.Log($"Could not resolve {model.ItemType}, {model.Identifier}, skipping.", LogLevel.Warn);
                    continue;
                }
            }

            // Handle the full prismatics.
            if (string.IsNullOrWhiteSpace(model.Mask))
            {
                if (!PrismaticFull.TryGetValue(model.ItemType, out HashSet<int>? set))
                {
                    set = new();
                }
                set.Add(id);
                PrismaticFull[model.ItemType] = set;
            }
            else
            {
                // handle the ones that have masks.
                if (!PrismaticMasks.TryGetValue(model.ItemType, out Dictionary<int, Lazy<Texture2D>>? masks))
                {
                    masks = new();
                }
                if (!masks.TryAdd(id, new(() => Game1.content.Load<Texture2D>(model.Mask))))
                {
                    ModEntry.ModMonitor.Log($"{model.ItemType} - {model.Identifier} appears to be a duplicate, ignoring", LogLevel.Warn);
                }
                PrismaticMasks[model.ItemType] = masks;
            }
        }
    }
    #endregion

    #region Helpers
    [MethodImpl(TKConstants.Hot)]
    private static bool ShouldDrawAsFullColored(this Item item)
    => item.GetItemType() is ItemTypeEnum type && PrismaticFull.TryGetValue(type, out HashSet<int>? set)
        && set.Contains(item.ParentSheetIndex);

    [MethodImpl(TKConstants.Hot)]
    private static Color ReplaceDrawColorForItem(Color prevcolor, Item item)
        => item.ShouldDrawAsFullColored() ? Utility.GetPrismaticColor() : prevcolor;

    [MethodImpl(TKConstants.Hot)]
    private static Texture2D? GetColorMask(this Item item)
        => item.GetItemType() is ItemTypeEnum type && PrismaticMasks.TryGetValue(type, out Dictionary<int, Lazy<Texture2D>>? masks)
            && masks.TryGetValue(item.ParentSheetIndex, out Lazy<Texture2D>? mask) ? mask.Value : null;

    private static void DrawColorMask(Item item, SpriteBatch b, Rectangle position, float drawDepth)
    {
        if (item.GetColorMask() is Texture2D texture)
        {
            b.Draw(texture, position, null, Color.White, 0f, Vector2.Zero, SpriteEffects.None, drawDepth);
        }
    }

    private static void DrawSObjectAndAlsoColorMask(
        SpriteBatch b,
        Texture2D texture,
        Vector2 position,
        Rectangle? sourceRectangle,
        Color color,
        float rotation,
        Vector2 origin,
        float scale,
        SpriteEffects effects,
        float layerDepth,
        SObject obj)
    {
        b.Draw(texture, position, sourceRectangle, color, rotation, origin, scale, effects, layerDepth);
        if (obj.GetColorMask() is Texture2D tex)
        {
            b.Draw(tex, position, null, Color.White, rotation, origin, scale, effects, layerDepth);
        }
    }
    #endregion

    #region SOBJECT

    /// <summary>
    /// Prefixes SObject's drawInMenu function in order to draw things prismatic-ally.
    /// </summary>
    /// <param name="__instance">SObject instance.</param>
    /// <param name="color">Color to make things.</param>
    [UsedImplicitly]
    [HarmonyPrefix]
    [MethodImpl(TKConstants.Hot)]
    [HarmonyPatch(typeof(SObject), nameof(SObject.drawInMenu))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony Convention.")]
    private static void PrefixSObjectDrawInMenu(SObject __instance, ref Color color)
    {
        try
        {
            if (__instance.ShouldDrawAsFullColored())
            {
                color = Utility.GetPrismaticColor();
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed in drawing prismatic item\n\n{ex}", LogLevel.Error);
        }
        return;
    }

    [UsedImplicitly]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SObject), nameof(SObject.drawInMenu))]
    private static IEnumerable<CodeInstruction>? TranspileSObjectDrawInMenu(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
    {
        try
        {
            ILHelper helper = new(original, instructions, ModEntry.ModMonitor, gen);
            helper.AdjustUtilityTextColor();

            return helper.Render();
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod crashed while transpiling {original.GetFullName()}\n\n{ex}", LogLevel.Error);
            original.Snitch(ModEntry.ModMonitor);
        }
        return null;
    }

    [UsedImplicitly]
    [HarmonyPostfix]
    [MethodImpl(TKConstants.Hot)]
    [HarmonyPatch(typeof(SObject), nameof(SObject.drawInMenu))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony Convention.")]
    private static void PostfixSObjectDrawInMenu(
        SObject __instance,
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth)
    {
        try
        {
            if (__instance.GetColorMask() is Texture2D texture)
            {
                spriteBatch.Draw(
                    texture: texture,
                    position: location + (new Vector2(32f, 32f) * scaleSize),
                    sourceRectangle: new Rectangle(0, 0, 16, 16),
                    color: Utility.GetPrismaticColor() * transparency,
                    rotation: 0f,
                    origin: new Vector2(8f, 8f) * scaleSize,
                    scale: scaleSize * 4f,
                    effects: SpriteEffects.None,
                    layerDepth: layerDepth);
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed in drawing prismatic mask\n\n{ex}", LogLevel.Error);
        }
        return;
    }

    [UsedImplicitly]
    [HarmonyTranspiler]
    [HarmonyPatch(typeof(SObject), nameof(SObject.drawWhenHeld))]
    private static IEnumerable<CodeInstruction>? TranspileSObjectDrawWhenHeld(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
    {
        try
        {
            // lots of places things are drawn here.
            ILHelper helper = new(original, instructions, ModEntry.ModMonitor, gen);

            // first one is the bigcraftable, second one is the nonbigcraftable
            for (int i = 0; i < 2; i++)
            {
                helper.FindNext(new CodeInstructionWrapper[]
                {
                (OpCodes.Call, typeof(Color).GetCachedProperty(nameof(Color.White), ReflectionCache.FlagTypes.StaticFlags).GetGetMethod()),
                })
                .Advance(1)
                .Insert(new CodeInstruction[]
                {
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(ReplaceDrawColorForItem), ReflectionCache.FlagTypes.StaticFlags)),
                })
                .FindNext(new CodeInstructionWrapper[]
                {
                    (OpCodes.Callvirt, typeof(SpriteBatch).GetCachedMethod(nameof(SpriteBatch.Draw), ReflectionCache.FlagTypes.InstanceFlags, new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) } )),
                })
                .ReplaceInstruction(
                    instruction: new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(DrawSObjectAndAlsoColorMask), ReflectionCache.FlagTypes.StaticFlags)),
                    keepLabels: true)
                .Insert(new CodeInstruction[] { new(OpCodes.Ldarg_0) });
            }

            helper.Print();
            return helper.Render();
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod crashed while transpiling {original.GetFullName()}\n\n{ex}", LogLevel.Error);
            original.Snitch(ModEntry.ModMonitor);
        }
        return null;
    }
#warning - the other draw method, the draw when held method....

    [UsedImplicitly]
    [HarmonyTranspiler]
    [SuppressMessage("SMAPI.CommonErrors", "AvoidNetField:Avoid Netcode types when possible", Justification = "Used for matching only.")]
    [HarmonyPatch(typeof(SObject), nameof(SObject.draw), new[] { typeof(SpriteBatch), typeof(int), typeof(int), typeof(float) } )]
    private static IEnumerable<CodeInstruction>? TranspileSObjectDraw(IEnumerable<CodeInstruction> instructions, ILGenerator gen, MethodBase original)
    {
        try
        {
            // lots of places things are drawn here.
            ILHelper helper = new(original, instructions, ModEntry.ModMonitor, gen);

            // bigcraftables block is first. Look for the single alone draw statement NOT in a conditional.
            // and bracket it.
            helper.FindNext(new CodeInstructionWrapper[]
            { // if (base.parentSheetIndex == 272)
                OpCodes.Ldarg_0,
                (OpCodes.Ldfld, typeof(Item).GetCachedField(nameof(Item.parentSheetIndex), ReflectionCache.FlagTypes.InstanceFlags)),
                OpCodes.Call, // op_Implicit
                (OpCodes.Ldc_I4, 272),
                OpCodes.Bne_Un,
            })
            .Advance(4)
            .StoreBranchDest()
            .AdvanceToStoredLabel()
            .Advance(-1)
            .FindNext(new CodeInstructionWrapper[]
            {
                OpCodes.Ldarg_1,
                (OpCodes.Ldsfld, typeof(Game1).GetCachedField(nameof(Game1.bigCraftableSpriteSheet), ReflectionCache.FlagTypes.StaticFlags)),
                SpecialCodeInstructionCases.LdLoc,
            })
            .Advance(2);

            var destination = helper.CurrentInstruction.Clone();

            helper.FindNext(new CodeInstructionWrapper[]
            {
                (OpCodes.Call, typeof(Color).GetCachedProperty(nameof(Color.White), ReflectionCache.FlagTypes.StaticFlags).GetGetMethod()),
            })
            .Advance(1)
            .Insert(new CodeInstruction[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(ReplaceDrawColorForItem), ReflectionCache.FlagTypes.StaticFlags)),
            })
            .FindNext(new CodeInstructionWrapper[]
            {
                SpecialCodeInstructionCases.LdLoc,
                (OpCodes.Callvirt, typeof(SpriteBatch).GetCachedMethod(nameof(SpriteBatch.Draw), ReflectionCache.FlagTypes.InstanceFlags, new[] { typeof(Texture2D), typeof(Rectangle), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(SpriteEffects), typeof(float) } )),
            });

            var layerDepth = helper.CurrentInstruction.Clone();

            helper.Advance(2)
            .Insert(new CodeInstruction[]
            {
                new (OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                destination,
                layerDepth,
                new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(DrawColorMask), ReflectionCache.FlagTypes.StaticFlags)),
            });

            // alright! Now to deal with normal SObjects
            helper.FindNext(new CodeInstructionWrapper[]
            {
                OpCodes.Ldarg_0,
                (OpCodes.Ldfld, typeof(SObject).GetCachedField(nameof(SObject.fragility), ReflectionCache.FlagTypes.InstanceFlags)),
                OpCodes.Call,
                OpCodes.Ldc_I4_2,
                OpCodes.Beq,
            })
            .Advance(4)
            .StoreBranchDest()
            .AdvanceToStoredLabel();

            helper.FindNext(new CodeInstructionWrapper[]
            {
                (OpCodes.Call, typeof(Color).GetCachedProperty(nameof(Color.White), ReflectionCache.FlagTypes.StaticFlags).GetGetMethod()),
            })
            .Advance(1)
            .Insert(new CodeInstruction[]
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(ReplaceDrawColorForItem), ReflectionCache.FlagTypes.StaticFlags)),
            })
            .FindNext(new CodeInstructionWrapper[]
            {
                (OpCodes.Callvirt, typeof(SpriteBatch).GetCachedMethod(nameof(SpriteBatch.Draw), ReflectionCache.FlagTypes.InstanceFlags, new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) } )),
            })
            .ReplaceInstruction(
                instruction: new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(DrawSObjectAndAlsoColorMask), ReflectionCache.FlagTypes.StaticFlags)),
                keepLabels: true)
            .Insert(new CodeInstruction[] { new(OpCodes.Ldarg_0) });

            // and the held item. Kill me now.
            helper.FindNext(new CodeInstructionWrapper[]
            { // must skip past the sprinkler section first.
                OpCodes.Ldarg_0,
                (OpCodes.Callvirt, typeof(SObject).GetCachedMethod(nameof(SObject.IsSprinkler), ReflectionCache.FlagTypes.InstanceFlags)),
                OpCodes.Brfalse,
            })
            .Advance(2)
            .StoreBranchDest()
            .AdvanceToStoredLabel()
            .FindNext(new CodeInstructionWrapper[]
            {
                OpCodes.Ldarg_0,
                (OpCodes.Ldfld, typeof(SObject).GetCachedField(nameof(SObject.heldObject), ReflectionCache.FlagTypes.InstanceFlags)),
                (OpCodes.Callvirt, typeof(NetFieldBase<SObject, NetRef<SObject>>).GetCachedProperty("Value", ReflectionCache.FlagTypes.InstanceFlags).GetGetMethod()),
                OpCodes.Brfalse,
            })
            .Copy(3, out var codes)
            .FindNext(new CodeInstructionWrapper[]
            {
                (OpCodes.Call, typeof(Color).GetCachedProperty(nameof(Color.White), ReflectionCache.FlagTypes.StaticFlags).GetGetMethod()),
            })
            .Advance(1)
            .Insert(codes.ToArray())
            .Insert(new CodeInstruction[]
            {
                new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(ReplaceDrawColorForItem), ReflectionCache.FlagTypes.StaticFlags)),
            })
            .FindNext(new CodeInstructionWrapper[]
            {
                (OpCodes.Callvirt, typeof(SpriteBatch).GetCachedMethod(nameof(SpriteBatch.Draw), ReflectionCache.FlagTypes.InstanceFlags, new[] { typeof(Texture2D), typeof(Vector2), typeof(Rectangle?), typeof(Color), typeof(float), typeof(Vector2), typeof(float), typeof(SpriteEffects), typeof(float) } )),
            })
            .ReplaceInstruction(
                instruction: new(OpCodes.Call, typeof(DrawPrismatic).GetCachedMethod(nameof(DrawSObjectAndAlsoColorMask), ReflectionCache.FlagTypes.StaticFlags)),
                keepLabels: true)
            .Insert(codes.Select(c => c.Clone()).ToArray());

            // helper.Print();
            return helper.Render();
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Mod crashed while transpiling {original.GetFullName()}\n\n{ex}", LogLevel.Error);
            original.Snitch(ModEntry.ModMonitor);
        }
        return null;
    }

    #endregion

    #region RING

    [UsedImplicitly]
    [HarmonyPrefix]
    [MethodImpl(TKConstants.Hot)]
    [HarmonyPatch(typeof(Ring), nameof(Ring.drawInMenu))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony Convention.")]
    private static void PrefixRingDrawInMenu(Ring __instance, ref Color color)
    {
        try
        {
            if (__instance.ShouldDrawAsFullColored())
            {
                color = Utility.GetPrismaticColor();
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed in drawing prismatic ring\n\n{ex}", LogLevel.Error);
        }
        return;
    }

    [UsedImplicitly]
    [HarmonyPostfix]
    [MethodImpl(TKConstants.Hot)]
    [HarmonyPatch(typeof(Ring), nameof(Ring.drawInMenu))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony Convention.")]
    private static void PostfixRingDrawInMenu(
        Ring __instance,
        SpriteBatch spriteBatch,
        Vector2 location,
        float scaleSize,
        float transparency,
        float layerDepth)
    {
        try
        {
            if (__instance.GetColorMask() is Texture2D texture)
            {
                spriteBatch.Draw(
                    texture: texture,
                    position: location + (new Vector2(32f, 32f) * scaleSize),
                    sourceRectangle: new Rectangle(0, 0, 16, 16),
                    color: Utility.GetPrismaticColor() * transparency,
                    rotation: 0f,
                    origin: new Vector2(8f, 8f) * scaleSize,
                    scale: scaleSize * 4f,
                    effects: SpriteEffects.None,
                    layerDepth: layerDepth);
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed in drawing prismatic mask\n\n{ex}", LogLevel.Error);
        }
        return;
    }

    #endregion

    #region BOOTS

    [UsedImplicitly]
    [HarmonyPrefix]
    [MethodImpl(TKConstants.Hot)]
    [HarmonyPatch(typeof(Boots), nameof(Boots.drawInMenu))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony Convention.")]
    private static void PrefixBootsDrawInMenu(Boots __instance, ref Color color)
    {
        try
        {
            if (__instance.ShouldDrawAsFullColored())
            {
                color = Utility.GetPrismaticColor();
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed in drawing prismatic boots\n\n{ex}", LogLevel.Error);
        }
        return;
    }

    [UsedImplicitly]
    [HarmonyPostfix]
    [MethodImpl(TKConstants.Hot)]
    [HarmonyPatch(typeof(Boots), nameof(Boots.drawInMenu))]
    [SuppressMessage("StyleCop.CSharp.NamingRules", "SA1313:Parameter names should begin with lower-case letter", Justification = "Harmony Convention.")]
    private static void PostfixBootsDrawInMenu(
    Ring __instance,
    SpriteBatch spriteBatch,
    Vector2 location,
    float scaleSize,
    float transparency,
    float layerDepth)
    {
        try
        {
            if (__instance.GetColorMask() is Texture2D texture)
            {
                spriteBatch.Draw(
                    texture: texture,
                    position: location + (new Vector2(32f, 32f) * scaleSize),
                    sourceRectangle: new Rectangle(0, 0, 16, 16),
                    color: Utility.GetPrismaticColor() * transparency,
                    rotation: 0f,
                    origin: new Vector2(8f, 8f) * scaleSize,
                    scale: scaleSize * 4f,
                    effects: SpriteEffects.None,
                    layerDepth: layerDepth);
            }
        }
        catch (Exception ex)
        {
            ModEntry.ModMonitor.Log($"Failed in drawing prismatic mask\n\n{ex}", LogLevel.Error);
        }
        return;
    }

    #endregion
}
