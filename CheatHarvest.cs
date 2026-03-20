#nullable disable
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using ModSettings;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using Il2Cpp;


using Object = UnityEngine.Object;

// MOD 
[assembly: MelonInfo(typeof(CheatHarvest.CheatHarvestMain), "CheatHarvest", "1.0.1", "HZB1130")]
[assembly: MelonGame("Hinterland", "TheLongDark")]

namespace CheatHarvest
{
    public class CheatHarvestMain : MelonMod
    {
        private static int gearLayerMask;
        private static int harvestableLayerMask = -1;
        private static bool usePhysicsForHarvest = false;

        public override void OnInitializeMelon()
        {
            gearLayerMask = 1 << 17;
            Settings.OnLoad();
            MelonCoroutines.Start(DetectHarvestableSetup());
        }

        private IEnumerator DetectHarvestableSetup()
        {
            yield return new WaitForSeconds(5f);

            Harvestable[] all = Object.FindObjectsOfType<Harvestable>();
            if (all.Length == 0)
                yield break;

            int withCollider = 0;
            int totalChecked = 0;

            foreach (var h in all)
            {
                if (h == null || h.gameObject == null)
                    continue;

                totalChecked++;

                if (h.GetComponent<Collider>() != null)
                {
                    withCollider++;

                    if (withCollider == 1)
                        harvestableLayerMask = 1 << h.gameObject.layer;
                }
            }

            float percentage = totalChecked > 0 ? (float)withCollider / totalChecked * 100f : 0f;
            usePhysicsForHarvest = percentage > 50f;
        }

        public override void OnUpdate()
        {
            if (Settings.options == null)
                return;

            if (!Input.GetKeyDown(Settings.options.pickUpKey))
                return;

            var playerManager = GameManager.GetPlayerManagerComponent();
            if (playerManager == null)
                return;

            Transform playerTransform = GameManager.GetPlayerTransform();
            if (playerTransform == null)
                return;

            Vector3 playerPos = playerTransform.position;
            float radius = Settings.options.GetPickupRadius();

            if (Settings.options.pickupGear)
                PickupGearItems(playerPos, radius, playerManager);

            if (Settings.options.harvestPlants)
                HarvestPlants(playerPos, radius);

            if (Settings.options.searchContainers)
                AutoLootContainers(playerPos, radius);
        }

        // =========================================================
        // 地面物品
        // =========================================================
        private void PickupGearItems(Vector3 playerPos, float radius, PlayerManager playerManager)
        {
            HashSet<int> processed = new HashSet<int>();

            Collider[] colliders = Physics.OverlapSphere(playerPos, radius, gearLayerMask, QueryTriggerInteraction.Collide);

            if (colliders == null)
                return;

            foreach (Collider col in colliders)
            {
                if (col == null)
                    continue;

                GearItem gear = col.GetComponentInParent<GearItem>();
                if (gear == null)
                    continue;

                int id = gear.gameObject.GetInstanceID();
                if (processed.Contains(id))
                    continue;

                if (!gear.enabled || gear.m_InPlayerInventory || !gear.gameObject.activeInHierarchy)
                    continue;

                if (!Settings.options.showPreviouslyPickedItems && gear.m_BeenInPlayerInventory)
                    continue;

                if (ShouldHideGear(gear.name))
                    continue;

                processed.Add(id);
                playerManager.ProcessPickupItemInteraction(gear, false, false, false);
            }
        }

        private bool ShouldHideGear(string gearName)
        {
            switch (Settings.options.hideItemsFilter)
            {
                case HideItemsFilter.Stone:
                    return gearName.Contains("GEAR_Stone");
                case HideItemsFilter.Stick:
                    return gearName.Contains("GEAR_Stick");
                case HideItemsFilter.StoneAndStick:
                    return gearName.Contains("GEAR_Stone") || gearName.Contains("GEAR_Stick");
                default:
                    return false;
            }
        }

        // =========================================================
        // 植物
        // =========================================================
        private void HarvestPlants(Vector3 playerPos, float radius)
        {
            List<Harvestable> list = new List<Harvestable>();

            if (usePhysicsForHarvest)
                HarvestPlantsWithPhysics(playerPos, radius, list);
            else
                HarvestPlantsWithFindAll(playerPos, radius, list);

            if (list.Count == 0)
                return;

            PatchHarvestableInspect.AddHarvestablesToSkip(list);

            foreach (var h in list)
                h.Harvest();
        }

        private void HarvestPlantsWithPhysics(Vector3 playerPos, float radius, List<Harvestable> list)
        {
            Collider[] colliders = Physics.OverlapSphere(playerPos, radius, harvestableLayerMask);

            if (colliders == null)
                return;

            HashSet<int> processed = new HashSet<int>();

            foreach (var col in colliders)
            {
                Harvestable h = col.GetComponentInParent<Harvestable>();
                if (h == null)
                    continue;

                int id = h.gameObject.GetInstanceID();
                if (processed.Contains(id))
                    continue;

                if (!IsHarvestableValid(h))
                    continue;

                processed.Add(id);
                list.Add(h);
            }
        }

        private void HarvestPlantsWithFindAll(Vector3 playerPos, float radius, List<Harvestable> list)
        {
            var all = Object.FindObjectsOfType<Harvestable>();
            float sqr = radius * radius;

            foreach (var h in all)
            {
                if (h == null || h.gameObject == null)
                    continue;

                if ((h.transform.position - playerPos).sqrMagnitude > sqr)
                    continue;

                if (!IsHarvestableValid(h))
                    continue;

                list.Add(h);
            }
        }

        private bool IsHarvestableValid(Harvestable h)
        {
            return !h.m_Harvested &&
                   h.gameObject.activeInHierarchy &&
                   h.RegisterAsPlantsHaversted;
        }

        // =========================================================
        // 容器
        // =========================================================
        private void AutoLootContainers(Vector3 playerPos, float radius)
        {
            var all = Object.FindObjectsOfType<Container>();

            foreach (var c in all)
            {
                if (c == null)
                    continue;

                if (Vector3.Distance(playerPos, c.transform.position) > radius)
                    continue;

                if (c.CanNeverBeOpened() || c.IsLocked() || c.IsSafeLocked())
                    continue;

                if (Settings.options.skipSearchedContainers && c.IsInspected())
                    continue;

                AutoLootContainer(c);
            }
        }

        private void AutoLootContainer(Container c)
        {
            if (c.m_NotPopulated)
                c.InstantiateContents();

            var items = new Il2CppSystem.Collections.Generic.List<GearItem>();
            c.GetItems(items);

            var inv = GameManager.GetInventoryComponent() as Inventory;
            if (inv == null)
                return;

            foreach (var item in items)
            {
                if (item == null)
                    continue;

                inv.AddGear(item, false);
                c.RemoveGear(item);
            }

            c.MarkAsInspected();
            c.UpdateContainer();
        }
    }

    // =========================================================
    // Harmony Patch
    // =========================================================
    [HarmonyPatch(typeof(Harvestable), nameof(Harvestable.EnterInspectMode))]
    internal class PatchHarvestableInspect
    {
        private static HashSet<int> skipIDs = new HashSet<int>();

        public static void AddHarvestablesToSkip(List<Harvestable> list)
        {
            skipIDs.Clear();
            foreach (var h in list)
                skipIDs.Add(h.gameObject.GetInstanceID());
        }

        static bool Prefix(Harvestable __instance)
        {
            if (__instance == null || __instance.gameObject == null)
                return true;

            int id = __instance.gameObject.GetInstanceID();
            if (!skipIDs.Contains(id))
                return true;

            skipIDs.Remove(id);

            var pm = GameManager.GetPlayerManagerComponent();
            if (pm == null)
                return false;

            for (int i = 0; i < __instance.m_NumPrimary; i++)
            {
                pm.InstantiateItemInPlayerInventory(__instance.m_GearPrefab);
            }

            if (__instance.m_SecondGearPrefab != null)
            {
                for (int i = 0; i < __instance.m_NumSecondary; i++)
                {
                    pm.InstantiateItemInPlayerInventory(__instance.m_SecondGearPrefab);
                }
            }

            return false;
        }
    }

    // =========================================================
    // 枚举
    // =========================================================
    public enum HideItemsFilter
    {
        None,
        Stone,
        Stick,
        StoneAndStick
    }

    // =========================================================
    // 设置类（已完全修复）
    // =========================================================
    internal class CheatHarvestSettings : JsonModSettings
    {
        [Section("Key / 按键")]
        [Name("Pickup Key / 拾取按键")]
        public KeyCode pickUpKey = KeyCode.LeftAlt;

        [Section("Range / 范围")]
        [Name("Pickup Radius (Units) / 拾取半径（个位）")]
        [Slider(0, 99)]
        public int pickupRadiusA = 10;

        [Name("Pickup Radius (Hundreds) / 拾取半径（百位）")]
        [Slider(0, 20)]
        public int pickupRadiusB = 0;

        [Section("Functions / 功能")]
        [Name("Pick Up Ground Items / 拾取地面物品")]
        public bool pickupGear = false;

        [Name("Harvest Plants / 采摘植物")]
        public bool harvestPlants = false;

        [Name("Search Containers / 搜索容器")]
        public bool searchContainers = false;

        [Section("Items / 物品")]
        [Name("Show Previously Picked Items / 拾取曾经捡过的物品")]
        public bool showPreviouslyPickedItems = true;

        [Name("Item Filter / 过滤物品类型")]
        public HideItemsFilter hideItemsFilter = HideItemsFilter.None;

        [Section("Containers / 容器")]
        [Name("Skip Searched Containers / 跳过已搜索容器")]
        public bool skipSearchedContainers = true;

        public float GetPickupRadius()
        {
            return pickupRadiusA + pickupRadiusB * 100f;
        }

        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            base.OnChange(field, oldValue, newValue);

            if (field.Name == nameof(searchContainers))
                SetFieldVisible(nameof(skipSearchedContainers), (bool)newValue);

            if (field.Name == nameof(pickupGear))
            {
                bool v = (bool)newValue;
                SetFieldVisible(nameof(showPreviouslyPickedItems), v);
                SetFieldVisible(nameof(hideItemsFilter), v);
            }
        }
    }

    // =========================================================
    // Settings 管理
    // =========================================================
    internal static class Settings
    {
        public static CheatHarvestSettings options;

        public static void OnLoad()
        {
            options = new CheatHarvestSettings();
            options.AddToModSettings("CheatHarvest");

            options.SetFieldVisible(nameof(options.skipSearchedContainers), options.searchContainers);
            options.SetFieldVisible(nameof(options.showPreviouslyPickedItems), options.pickupGear);
            options.SetFieldVisible(nameof(options.hideItemsFilter), options.pickupGear);
        }
    }
}