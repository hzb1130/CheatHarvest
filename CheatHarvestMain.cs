#nullable disable
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using Il2Cpp;
using System.Collections.Generic;

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

        private System.Collections.IEnumerator DetectHarvestableSetup()
        {
            yield return new WaitForSeconds(5f);

            Harvestable[] all = UnityEngine.Object.FindObjectsOfType<Harvestable>();
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
            float radius = Settings.options.pickupRadius;

            if (Settings.options.pickupGear)
                PickupGearItems(playerPos, radius, playerManager);

            if (Settings.options.harvestPlants)
                HarvestPlants(playerPos, radius);

            if (Settings.options.searchContainers)
                AutoLootContainers(playerPos, radius);
        }

        // =========================
        // 地面物品
        // =========================
        private void PickupGearItems(Vector3 playerPos, float radius, PlayerManager playerManager)
        {
            HashSet<int> processed = new HashSet<int>();

            Collider[] colliders = Physics.OverlapSphere(playerPos, radius, gearLayerMask, QueryTriggerInteraction.Collide);

            if (colliders != null)
            {
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

                    processed.Add(id);
                    playerManager.ProcessPickupItemInteraction(gear, false, false, false);
                }
            }
        }

        // =========================
        // 植物采集
        // =========================
        private void HarvestPlants(Vector3 playerPos, float radius)
        {
            List<Harvestable> harvestableList = new List<Harvestable>();

            if (usePhysicsForHarvest)
                HarvestPlantsWithPhysics(playerPos, radius, harvestableList);
            else
                HarvestPlantsWithFindAll(playerPos, radius, harvestableList);

            if (harvestableList.Count == 0)
                return;

            PatchHarvestableInspect.AddHarvestablesToSkip(harvestableList);

            foreach (Harvestable harvestable in harvestableList)
                harvestable.Harvest();
        }

        private void HarvestPlantsWithPhysics(Vector3 playerPos, float radius, List<Harvestable> list)
        {
            Collider[] colliders = Physics.OverlapSphere(playerPos, radius, harvestableLayerMask, QueryTriggerInteraction.Collide);
            if (colliders == null)
                return;

            HashSet<int> processed = new HashSet<int>();

            foreach (Collider col in colliders)
            {
                if (col == null)
                    continue;

                Harvestable harvestable = col.GetComponentInParent<Harvestable>();
                if (harvestable == null)
                    continue;

                int id = harvestable.gameObject.GetInstanceID();
                if (processed.Contains(id))
                    continue;

                if (!IsHarvestableValid(harvestable))
                    continue;

                processed.Add(id);
                list.Add(harvestable);
            }
        }

        private void HarvestPlantsWithFindAll(Vector3 playerPos, float radius, List<Harvestable> list)
        {
            Harvestable[] all = UnityEngine.Object.FindObjectsOfType<Harvestable>();
            float sqrRadius = radius * radius;

            foreach (Harvestable harvestable in all)
            {
                if (harvestable == null || harvestable.gameObject == null)
                    continue;

                float sqrDistance = (harvestable.transform.position - playerPos).sqrMagnitude;
                if (sqrDistance > sqrRadius)
                    continue;

                if (!IsHarvestableValid(harvestable))
                    continue;

                list.Add(harvestable);
            }
        }

        private bool IsHarvestableValid(Harvestable harvestable)
        {
            if (harvestable.m_Harvested)
                return false;

            if (!harvestable.gameObject.activeInHierarchy)
                return false;

            if (!harvestable.RegisterAsPlantsHaversted)
                return false;

            return true;
        }

        // =========================
        // 容器
        // =========================
        private void AutoLootContainers(Vector3 playerPos, float radius)
        {
            Container[] allContainers = UnityEngine.Object.FindObjectsOfType<Container>();

            foreach (Container container in allContainers)
            {
                if (container == null)
                    continue;

                if (Vector3.Distance(playerPos, container.transform.position) > radius)
                    continue;

                if (container.CanNeverBeOpened() ||
                    container.IsLocked() ||
                    container.IsSafeLocked())
                    continue;

                if (Settings.options.skipSearchedContainers && container.IsInspected())
                    continue;

                AutoLootContainer(container);
            }
        }

        private void AutoLootContainer(Container container)
        {
            if (container.m_NotPopulated)
                container.InstantiateContents();

            Il2CppSystem.Collections.Generic.List<GearItem> items =
                new Il2CppSystem.Collections.Generic.List<GearItem>();

            container.GetItems(items);

            Inventory inventory = GameManager.GetInventoryComponent() as Inventory;
            if (inventory == null)
                return;

            if (items.Count > 0)
            {
                Il2CppSystem.Collections.Generic.List<GearItem> copy =
                    new Il2CppSystem.Collections.Generic.List<GearItem>();

                foreach (GearItem g in items)
                    copy.Add(g);

                foreach (GearItem item in copy)
                {
                    if (item == null)
                        continue;

                    inventory.AddGear(item, false);
                    container.RemoveGear(item);
                }
            }

            container.MarkAsInspected();
            container.UpdateContainer();
        }
    }

    [HarmonyPatch(typeof(Harvestable), nameof(Harvestable.EnterInspectMode))]
    internal class PatchHarvestableInspect
    {
        private static HashSet<int> skipIDs = new HashSet<int>();

        public static void AddHarvestablesToSkip(List<Harvestable> harvestables)
        {
            skipIDs.Clear();
            foreach (var h in harvestables)
            {
                if (h != null && h.gameObject != null)
                    skipIDs.Add(h.gameObject.GetInstanceID());
            }
        }

        static bool Prefix(Harvestable __instance, GearItem gearPrefab)
        {
            if (__instance == null || __instance.gameObject == null)
                return true;

            int id = __instance.gameObject.GetInstanceID();
            if (!skipIDs.Contains(id))
                return true;

            skipIDs.Remove(id);

            var playerManager = GameManager.GetPlayerManagerComponent();
            if (playerManager == null)
                return false;

            for (int i = 0; i < __instance.m_NumPrimary; i++)
            {
                GearItem item = UnityEngine.Object.Instantiate(__instance.m_GearPrefab);
                playerManager.InstantiateItemInPlayerInventory(item);
            }

            if (__instance.m_SecondGearPrefab != null)
            {
                for (int i = 0; i < __instance.m_NumSecondary; i++)
                {
                    GearItem item = UnityEngine.Object.Instantiate(__instance.m_SecondGearPrefab);
                    playerManager.InstantiateItemInPlayerInventory(item);
                }
            }

            return false;
        }
    }
}
