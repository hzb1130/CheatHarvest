#nullable disable
using UnityEngine;
using ModSettings;
using System.Reflection;

namespace CheatHarvest
{
    internal class CheatHarvestSettings : JsonModSettings
    {
        // =========================
        // Keybind Settings
        // =========================
        [Section("Keybind Settings")]

        [Name("Pickup Key")]
        public KeyCode pickUpKey = KeyCode.LeftAlt;


        // =========================
        // General Settings
        // =========================
        [Section("General Settings")]

        [Name("Pickup Radius")]
        [Slider(0.5f, 25f)]
        public float pickupRadius = 10.0f;


        // =========================
        // Pickup Categories
        // =========================
        [Section("Pickup Categories")]

        [Name("Pickup Ground Items")]
        public bool pickupGear = false;

        [Name("Harvest Plants")]
        public bool harvestPlants = false;

        [Name("Search Containers")]
        public bool searchContainers = false;


        // =========================
        // Container Options (Conditional)
        // =========================
        [Section("Container Options")]

        [Name("Skip Already Searched Containers")]
        public bool skipSearchedContainers = true;


        // =========================
        // Visibility Control Logic
        // =========================
        protected override void OnChange(FieldInfo field, object oldValue, object newValue)
        {
            base.OnChange(field, oldValue, newValue);

            if (field.Name == nameof(searchContainers))
            {
                bool visible = (bool)newValue;
                SetFieldVisible(nameof(skipSearchedContainers), visible);
            }
        }

        protected override void OnConfirm()
        {
            base.OnConfirm();
        }
    }


    internal static class Settings
    {
        public static CheatHarvestSettings options;

        public static void OnLoad()
        {
            options = new CheatHarvestSettings();
            options.AddToModSettings("CheatHarvest");

            UpdateContainerVisibility(options.searchContainers);
        }

        internal static void UpdateContainerVisibility(bool visible)
        {
            options.SetFieldVisible(nameof(options.skipSearchedContainers), visible);
        }
    }
}


// #nullable disable
// //中文版本
// using UnityEngine;
// using ModSettings;
// using System.Reflection;

// namespace CheatHarvest
// {
//     internal class CheatHarvestSettings : JsonModSettings
//     {
//         // =========================
//         // 按键
//         // =========================
//         [Section("按键设置")]

//         [Name("拾取按键")]
//         public KeyCode pickUpKey = KeyCode.LeftAlt;


//         // =========================
//         // 基础
//         // =========================
//         [Section("基础设置")]

//         [Name("拾取半径")]
//         [Slider(0.5f, 25f)]
//         public float pickupRadius = 10.0f;


//         // =========================
//         // 类型选择
//         // =========================
//         [Section("拾取类型")]

//         [Name("拾取地面物品")]
//         public bool pickupGear = false;

//         [Name("采摘植物")]
//         public bool harvestPlants = false;

//         [Name("搜索容器")]
//         public bool searchContainers = false;


//         // =========================
//         // 容器选项（折叠）
//         // =========================
//         [Section("容器选项")]

//         [Name("跳过已搜索容器")]
//         public bool skipSearchedContainers = true;


//         // =========================
//         // 折叠控制逻辑
//         // =========================
//         protected override void OnChange(FieldInfo field, object oldValue, object newValue)
//         {
//             base.OnChange(field, oldValue, newValue);

//             if (field.Name == nameof(searchContainers))
//             {
//                 bool visible = (bool)newValue;
//                 SetFieldVisible(nameof(skipSearchedContainers), visible);
//             }
//         }

//         protected override void OnConfirm()
//         {
//             base.OnConfirm();
//         }
//     }


//     internal static class Settings
//     {
//         public static CheatHarvestSettings options;

//         public static void OnLoad()
//         {
//             options = new CheatHarvestSettings();
//             options.AddToModSettings("CheatHarvest");

//             UpdateContainerVisibility(options.searchContainers);
//         }

//         internal static void UpdateContainerVisibility(bool visible)
//         {
//             options.SetFieldVisible(nameof(options.skipSearchedContainers), visible);
//         }
//     }
// }
