using BaseX;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using NeosModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DynamicBlendShapeDriverSetup
{
    public class DynamicBlendShapeDriverSetup : NeosMod
    {
        public static ModConfiguration Config;

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> EnableCleanButton = new ModConfigurationKey<bool>("EnableCleanButton", "Enable adding a button to the DynamicBlendShapeDriver component to remove BlendShapes not present on the renderer linked to it.", () => true);

        [AutoRegisterConfigKey]
        private static ModConfigurationKey<bool> EnableSetupButton = new ModConfigurationKey<bool>("EnableSetupButton", "Enable adding a button to the DynamicBlendShapeDriver component to setup all BlendShapes from the renderer linked to it.", () => true);

        public override string Author => "Banane9";
        public override string Link => "https://github.com/Banane9/NeosDynamicBlendShapeDriverSetup";
        public override string Name => "DynamicBlendShapeDriverSetup";
        public override string Version => "1.1.0";

        public override void OnEngineInit()
        {
            Harmony harmony = new Harmony($"{Author}.{Name}");
            Config = GetConfiguration();
            Config.Save(true);
            harmony.PatchAll();
        }

        [HarmonyPatch(typeof(DynamicBlendShapeDriver), nameof(DynamicBlendShapeDriver.BuildInspectorUI))]
        private static class DynamicBlendShapeDriverPatch
        {
            private static readonly MethodInfo updateBlendShapesMethod = typeof(DynamicBlendShapeDriver).GetMethod("UpdateBlendShapes", AccessTools.allDeclared);

            private static HashSet<string> CleanBrokenBlendShapes(DynamicBlendShapeDriver blendShapeDriver)
            {
                var renderer = blendShapeDriver.Renderer.Target;

                if (renderer.BlendShapeWeights.Count < renderer.BlendShapeCount)
                    renderer.BlendShapeWeights.AddRange(Enumerable.Repeat(0f, renderer.BlendShapeCount - renderer.BlendShapeWeights.Count));

                var availableBlendShapes = new HashSet<string>(Enumerable.Range(0, renderer.BlendShapeCount).Select(i => renderer.BlendShapeName(i)));

                blendShapeDriver.BlendShapes.RemoveAll(shape => !availableBlendShapes.Contains(shape.BlendShapeName.Value));

                return new HashSet<string>(blendShapeDriver.BlendShapes.Select(shape => shape.BlendShapeName.Value));
            }

            private static void Postfix(DynamicBlendShapeDriver __instance, UIBuilder ui)
            {
                if (Config.GetValue(EnableSetupButton))
                {
                    var setup = ui.Button("Setup BlendShapes from Renderer", color.Pink);

                    setup.LocalPressed += (bttn, data) =>
                    {
                        if (!(__instance.Renderer.Target is SkinnedMeshRenderer renderer))
                            return;

                        SetupBlendshapesFromRenderer(__instance);
                        updateBlendShapesMethod.Invoke(__instance, null);
                    };
                }

                if (Config.GetValue(EnableCleanButton))
                {
                    var clean = ui.Button("Clean up broken BlendShape entries", color.Pink);

                    clean.LocalPressed += (bttn, data) =>
                    {
                        if (!(__instance.Renderer.Target is SkinnedMeshRenderer renderer))
                        {
                            __instance.BlendShapes.Clear();
                            return;
                        }

                        CleanBrokenBlendShapes(__instance);
                        updateBlendShapesMethod.Invoke(__instance, null);
                    };
                }
            }

            private static void SetupBlendshapesFromRenderer(DynamicBlendShapeDriver blendShapeDriver)
            {
                var renderer = blendShapeDriver.Renderer.Target;

                var existingBinds = CleanBrokenBlendShapes(blendShapeDriver);

                for (var i = 0; i < renderer.BlendShapeCount; ++i)
                {
                    var name = renderer.BlendShapeName(i);
                    if (existingBinds.Contains(name) || renderer.TryGetBlendShape(name).IsDriven)
                        continue;

                    var shape = blendShapeDriver.BlendShapes.Add();
                    shape.BlendShapeName.Value = name;
                    shape.Value.Value = renderer.GetBlendShapeWeight(i);
                }
            }
        }
    }
}