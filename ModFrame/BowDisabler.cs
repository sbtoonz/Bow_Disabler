using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using ServerSync;
using UnityEngine;

namespace Bow_Disabler
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class BowDisabler : BaseUnityPlugin
    {
        private const string ModName = "BowDisabler";
        private const string ModVersion = "1.0";
        private const string ModGUID = "com.odinplus.BowDisabler";
        private static Harmony harmony = null!;
        private static ManualLogSource bowlogger;
        ConfigSync configSync = new(ModGUID) 
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion};
        internal static ConfigEntry<bool> ServerConfigLocked = null!;
        internal static ConfigEntry<bool> disableBows = null!;
        ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }
        ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        public void Awake()
        {
            bowlogger = this.Logger;
            Assembly assembly = Assembly.GetExecutingAssembly();
            harmony = new(ModGUID);
            harmony.PatchAll(assembly);
            ServerConfigLocked = config("1 - General", "Lock Configuration", true, "If on, the configuration is locked and can be changed by server admins only.");
            disableBows = config("1 - General", "Disable bow recipes", true, "Toggle this to disable all bow recipes");
            configSync.AddLockingConfigEntry(ServerConfigLocked);
            disableBows.SettingChanged += DisableToggled;
        }

        private void DisableToggled(object sender, EventArgs e)
        {
            RemoveRecipes(ObjectDB.instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.Awake))]
        [HarmonyPriority(Priority.Last)]
        [HarmonyWrapSafe]
        public static class BowRemoveAwakePatch
        {

            public static void Postfix(ObjectDB __instance)
            {
                RemoveRecipes(__instance);
            }
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        [HarmonyPriority(Priority.Last)]
        [HarmonyWrapSafe]
        public static class BowRemoveOtherDBpatch
        {
            public static void Postfix(ObjectDB __instance)
            {
                RemoveRecipes(__instance);
            }
        }


        private static void RemoveRecipes(ObjectDB objectDB)
        {
            if (objectDB.m_items.Count == 0 || objectDB.GetItemPrefab("Amber") == null)
            {
                bowlogger.LogDebug("Waiting for game to initialize before removing items.");
                return;
            }
            if (objectDB.m_recipes.Count == 0)
            {
                bowlogger.LogDebug("Recipe database not ready for stuff, skipping initialization.");
                return;
            }
            var templist = objectDB.m_recipes;
            foreach (var recipe in templist)
            {
                if(recipe.m_item == null) continue;
                if (recipe.m_item.m_itemData.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow)
                {
                    recipe.m_enabled = !disableBows.Value;
                }
            }
            objectDB.m_recipes = templist;
        }
    }
}
