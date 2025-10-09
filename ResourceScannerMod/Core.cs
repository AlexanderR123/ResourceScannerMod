using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppMonomiPark.SlimeRancher;
using Il2CppMonomiPark.SlimeRancher.UI;
using Il2CppMonomiPark.SlimeRancher.World;
using Il2CppSony.NP;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

[assembly: MelonInfo(typeof(ResourceScannerMod.Core), "ResourceScannerMod", "auto", "AlexanderR123", null)]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace ResourceScannerMod
{
    public class Core : MelonMod
    {
        #region Private Fields

        private static bool _ShowResources;
        private static bool _ShowResourcesInit = false;
        private static bool _WaitingForResources = false;
        private static Transform _PlayerTransform;

        private static float _LastDataUpdateTime = 0f;
        private static float _DataUpdateInterval = 2.5f; // Daten alle 0.1s aktualisieren

        #region Settings

        private static MelonPreferences_Category _PrefsCategory;

        #region Default

        private static MelonPreferences_Entry<float> _PrefDetectionRadius;
        private static MelonPreferences_Entry<Key> _PrefToggleKey;
        private static MelonPreferences_Entry<int> _MaxDrawnResources;

        #endregion Default

        #region Extra

        private static MelonPreferences_Entry<bool> _PrefInstantSuck;
        private static MelonPreferences_Entry<bool> _PrefEnableColors;

        #endregion Extra

        #region Show Options

        private static MelonPreferences_Entry<bool> _PrefShowResources;
        private static MelonPreferences_Entry<Key> _PrefShowResourcesKey;
        private static MelonPreferences_Entry<bool> _PrefShowTreasurePod;
        private static MelonPreferences_Entry<Key> _PrefShowTreasurePodKey;
        private static MelonPreferences_Entry<bool> _PrefShowGordo;
        private static MelonPreferences_Entry<Key> _PrefShowGordoKey;
        private static MelonPreferences_Entry<bool> _PrefShowDirectedAnimalSpawner;
        private static MelonPreferences_Entry<Key> _PrefShowDirectedAnimalSpawnerKey;
        private static MelonPreferences_Entry<bool> _PrefShowVacuumableFood;
        private static MelonPreferences_Entry<Key> _PrefShowVacuumableFoodKey;
        private static MelonPreferences_Entry<bool> _PrefShowCrates;
        private static MelonPreferences_Entry<Key> _PrefShowCratesKey;

        private static MelonPreferences_Entry<bool> _PrefSpoilerSafeMode;
        private static MelonPreferences_Entry<Key> _PrefSpoilerSafeModeKey;

        private static MelonPreferences_Entry<bool> _PrefShowGadgetTracker;
        private static MelonPreferences_Entry<Key> _PrefShowGadgetTrackerKey;
        private static MelonPreferences_Entry<bool> _PrefShowUnstablePrismaPlorts;
        private static MelonPreferences_Entry<Key> _PrefShowUnstablePrismaPlortsKey;
        private static MelonPreferences_Entry<bool> _PrefShowPlortStatues;
        private static MelonPreferences_Entry<Key> _PrefShowPlortStatuesKey;
        #endregion Show Options

        #endregion Settings

        #region Data Caching

        // Caching-Felder
        private static List<CachedResourceData> _CachedResourceData = new List<CachedResourceData>();
        private static List<CachedVacuumableData> _CachedVacuumableData = new List<CachedVacuumableData>();
        private static List<CachedTreasurePodData> _CachedTreasurePodData = new List<CachedTreasurePodData>();
        private static List<CachedGordoData> _CachedGordoData = new List<CachedGordoData>();
        private static List<CachedDirectedAnimalSpawnerData> _CachedDirectedAnimalSpawnerData = new List<CachedDirectedAnimalSpawnerData>();
        private static List<CachedVacuumableData> _CachedVacuumableFoodData = new List<CachedVacuumableData>();
        private static List<CachedVacuumableData> _CachedCachedCrateData = new List<CachedVacuumableData>();
        private static List<CachedWorldData> _CachedGadgetData = new List<CachedWorldData>();
        private static List<CachedVacuumableData> _CachedCachedUnstablePrismaPlortsData = new List<CachedVacuumableData>();
        private static List<CachedWorldData> _CachedCachedPlortStatuesData = new List<CachedWorldData>();

        #endregion Data Caching


        public class CachedWorldData
        {
            public Vector3 WorldPosition;
            public string Letter;
            public Texture2D Icon;
            public float Distance;
        }

        public class CachedResourceData : CachedWorldData
        {
            public ResourceNodeUIInteractable Resource;
            public bool IsHarvestable;
        }

        public class CachedTreasurePodData : CachedWorldData
        {
            public TreasurePodUIInteractable TreasurePod;
            public bool IsCollectable;
        }

        public class CachedVacuumableData : CachedWorldData
        {
            public Vacuumable Vacuumable;
        }

        public class CachedGordoData : CachedWorldData
        {
            public GordoIdentifiable Gordo;
            public bool IsActive;
        }

        public class CachedDirectedAnimalSpawnerData : CachedWorldData
        {
            public DirectedAnimalSpawner Spawner;
            public bool IsAvailable;
        }

        #endregion

        #region Melon Methods

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");

            _PrefsCategory = MelonPreferences.CreateCategory("ResourceScanner");

            // Default
            _PrefDetectionRadius = _PrefsCategory.CreateEntry("DetectionRadius", 200f, "Detection Radius");
            _PrefToggleKey = _PrefsCategory.CreateEntry("ToggleKey", Key.K, "Toggle Key");
            _MaxDrawnResources = _PrefsCategory.CreateEntry("MaxDrawnResources", 100, "Maximum drawn resources");

            // Extra
            _PrefInstantSuck = _PrefsCategory.CreateEntry("InstantSuck", false, "Remove Resource Extraction Delay");
            _PrefEnableColors = _PrefsCategory.CreateEntry("EnableTintColors", false, "Enable colored tint on icons");

            // Show Options
            _PrefShowResources = _PrefsCategory.CreateEntry("ShowResources", true, "Shows Resources");
            _PrefShowResourcesKey = _PrefsCategory.CreateEntry("ShowResourcesKey", Key.Y, "Key for ShowResources");
            _PrefShowTreasurePod = _PrefsCategory.CreateEntry("ShowTreasurePod", false, "Shows Treasure Pod");
            _PrefShowTreasurePodKey = _PrefsCategory.CreateEntry("ShowTreasurePodKey", Key.L, "Key for ShowTreasurePod");
            _PrefShowGordo = _PrefsCategory.CreateEntry("ShowGordo", false, "Shows Gordos");
            _PrefShowGordoKey = _PrefsCategory.CreateEntry("ShowGordoKey", Key.H, "Key for ShowGordo");
            _PrefShowDirectedAnimalSpawner = _PrefsCategory.CreateEntry("ShowDirectedAnimalSpawner", false, "Shows Directed Animal Spawners");
            _PrefShowDirectedAnimalSpawnerKey = _PrefsCategory.CreateEntry("ShowDirectedAnimalSpawnerKey", Key.U, "Key for ShowDirectedAnimalSpawner");
            _PrefShowVacuumableFood = _PrefsCategory.CreateEntry("ShowVacuumableFood", false, "Show Vacuumable Food");
            _PrefShowVacuumableFoodKey = _PrefsCategory.CreateEntry("ShowVacuumableFoodKey", Key.V, "Key for ShowVacuumableFood");
            _PrefShowCrates = _PrefsCategory.CreateEntry("ShowCrates", false, "Shows Crates");
            _PrefShowCratesKey = _PrefsCategory.CreateEntry("ShowCratesKey", Key.C, "Key for ShowCrates");

            _PrefSpoilerSafeMode = _PrefsCategory.CreateEntry("SpoilerSafeMode", true, "Hide contents for Treasure Pods and Shadow Deposits (icons only)");
            _PrefSpoilerSafeModeKey = _PrefsCategory.CreateEntry("SpoilerSafeModeKey", Key.P, "Key to toggle Spoiler Safe Mode");


            _PrefShowGadgetTracker = _PrefsCategory.CreateEntry("ShowGadgetTracker", true, "Show gadget tracker (teleporters, refinery/market links, other gadgets)");
            _PrefShowGadgetTrackerKey = _PrefsCategory.CreateEntry("ShowGadgetTrackerKey", Key.Y, "Key for gadget tracker toggle");

            _PrefShowUnstablePrismaPlorts = _PrefsCategory.CreateEntry("ShowUnstablePrismaPlorts", true, "Track unstable/prisma plorts on ground");
            _PrefShowUnstablePrismaPlortsKey = _PrefsCategory.CreateEntry("ShowUnstablePrismaPlortsKey", Key.P, "Key for unstable/prisma plorts tracker toggle");

            _PrefShowPlortStatues = _PrefsCategory.CreateEntry("ShowPlortStatues", false, "Track plort statues (if detectable)");
            _PrefShowPlortStatuesKey = _PrefsCategory.CreateEntry("ShowPlortStatuesKey", Key.H, "Key for plort statues tracker toggle");

            MelonPreferences.Load();
        }

        public override void OnLateUpdate()
        {
            if (Keyboard.current[_PrefToggleKey.Value].wasPressedThisFrame)
            {
                ToggleShowResources();
            }
            else if (Keyboard.current[_PrefShowResourcesKey.Value].wasPressedThisFrame)
            {
                _PrefShowResources.Value = !_PrefShowResources.Value;
                MelonLogger.Msg(_PrefShowResources.Value ? "Show Resources Enabled" : "Show Resources Disabled");
            }
            else if (Keyboard.current[_PrefShowTreasurePodKey.Value].wasPressedThisFrame)
            {
                _PrefShowTreasurePod.Value = !_PrefShowTreasurePod.Value;
                MelonLogger.Msg(_PrefShowTreasurePod.Value ? "Show Treasurepods Enabled" : "Show Treasurepods Disabled");
            }
            else if (Keyboard.current[_PrefShowGordoKey.Value].wasPressedThisFrame)
            {
                _PrefShowGordo.Value = !_PrefShowGordo.Value;
                MelonLogger.Msg(_PrefShowGordo.Value ? "Show Gordos Enabled" : "Show Gordos Disabled");
            }
            else if (Keyboard.current[_PrefShowDirectedAnimalSpawnerKey.Value].wasPressedThisFrame)
            {
                _PrefShowDirectedAnimalSpawner.Value = !_PrefShowDirectedAnimalSpawner.Value;
                MelonLogger.Msg(_PrefShowDirectedAnimalSpawner.Value ? "Show Directed Animal Spawners Enabled" : "Show Directed Animal Spawners Disabled");
            }
            else if (Keyboard.current[_PrefShowVacuumableFoodKey.Value].wasPressedThisFrame)
            {
                _PrefShowVacuumableFood.Value = !_PrefShowVacuumableFood.Value;
                MelonLogger.Msg(_PrefShowVacuumableFood.Value ? "Show Vacuumable Food Enabled" : "Show Vacuumable Food Disabled");
            }
            else if (Keyboard.current[_PrefShowCratesKey.Value].wasPressedThisFrame)
            {
                _PrefShowCrates.Value = !_PrefShowCrates.Value;
                MelonLogger.Msg(_PrefShowCrates.Value ? "Show Crates Enabled" : "Show Crates Disabled");
            }
            else if (Keyboard.current[_PrefSpoilerSafeModeKey.Value].wasPressedThisFrame)
            {
                _PrefSpoilerSafeMode.Value = !_PrefSpoilerSafeMode.Value;
                MelonLogger.Msg(_PrefSpoilerSafeMode.Value ? "Spoiler Safe Mode Enabled (icons only)" : "Spoiler Safe Mode Disabled (show contents)");
            }
            else if (Keyboard.current[_PrefShowGadgetTrackerKey.Value].wasPressedThisFrame)
            {
                _PrefShowGadgetTracker.Value = !_PrefShowGadgetTracker.Value;
                MelonLogger.Msg(_PrefShowGadgetTracker.Value ? "Gadget Tracker Enabled" : "Gadget Tracker Disabled");
            }
            else if (Keyboard.current[_PrefShowUnstablePrismaPlortsKey.Value].wasPressedThisFrame)
            {
                _PrefShowUnstablePrismaPlorts.Value = !_PrefShowUnstablePrismaPlorts.Value;
                MelonLogger.Msg(_PrefShowUnstablePrismaPlorts.Value ? "Unstable/Prisma Plorts Tracker Enabled" : "Unstable/Prisma Plorts Tracker Disabled");
            }
            else if (Keyboard.current[_PrefShowPlortStatuesKey.Value].wasPressedThisFrame)
            {
                _PrefShowPlortStatues.Value = !_PrefShowPlortStatues.Value;
                MelonLogger.Msg(_PrefShowPlortStatues.Value ? "Plort Statues Tracker Enabled" : "Plort Statues Tracker Disabled");
            }

        }

        public override void OnDeinitializeMelon()
        {
            if (_ShowResources)
            {
                ToggleShowResources();
            }
        }

        #endregion

        #region Overlay Control

        public static void ToggleShowResources()
        {
            _ShowResources = !_ShowResources;

            if (_ShowResources)
            {
                MelonLogger.Msg("Show Resources Enabled");
                MelonEvents.OnGUI.Subscribe(DrawResourcesOverlay, 100);
                MelonEvents.OnGUI.Subscribe(DrawShowResourcesText, 100);
                _ShowResourcesInit = true;
            }
            else
            {
                MelonLogger.Msg("Show Resources Disabled");
                MelonEvents.OnGUI.Unsubscribe(DrawShowResourcesText);
                MelonEvents.OnGUI.Unsubscribe(DrawResourcesOverlay);
            }
        }

        public static void DrawShowResourcesText()
        {
            GUI.Label(new Rect(20, 20, 1000, 200), "<b><color=cyan><size=10>ShowResources</size></color></b>");
        }

        #endregion

        #region Resource Overlay

        public static void DrawResourcesOverlay()
        {
            if (Event.current.type != EventType.Repaint && !_ShowResourcesInit)
                return;

            zEnsurePlayerTransform();
            if (_PlayerTransform == null) return;

            // Nur die Datenberechnung weniger oft machen, GUI immer zeichnen
            float currentTime = Time.time;
            if (currentTime - _LastDataUpdateTime > _DataUpdateInterval)
            {
                UpdateCachedData();
                _LastDataUpdateTime = currentTime;
            }

            _ShowResourcesInit = false;

            // GUI wird bei jedem Frame gezeichnet - kein Flackern
            DrawCachedResourceData();
        }

        private static void UpdateCachedData()
        {
            if (_PlayerTransform == null) return;

            var playerPos = _PlayerTransform.position;
            var detectionRadius = _PrefDetectionRadius.Value;

            if (_PrefShowResources.Value)
            {

                // Resource Nodes verarbeiten
                var allResources = GameObject.FindObjectsOfType<ResourceNodeUIInteractable>();
                _CachedResourceData.Clear();

                foreach (var resource in allResources)
                {
                    if (resource?.gameObject?.activeInHierarchy != true ||
                        resource.resourceNode == null ||
                        resource.transform == null) continue;

                    var worldPos = resource.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    // Icon nur einmal laden und cachen
                    Texture2D icon = null;
                    var resourcesList = resource.resourceNode.GetResources();
                    if (resourcesList?.Count > 0 && resourcesList[0]?.icon?.texture != null)
                    {
                        icon = resourcesList[0].icon.texture;
                    }

                    if (icon == null) continue;

                    _CachedResourceData.Add(new CachedResourceData
                    {
                        Resource = resource,
                        WorldPosition = worldPos,
                        Icon = icon,
                        Distance = distance,
                        IsHarvestable = resource.resourceNode._harvestAtTime == 0
                    });
                }
                _CachedResourceData.Sort((a, b) => a.Distance.CompareTo(b.Distance));

                // Vacuumables verarbeiten
                var allVacuumables = GameObject.FindObjectsOfType<Vacuumable>();
                _CachedVacuumableData.Clear();

                foreach (var vacuumable in allVacuumables)
                {
                    if (vacuumable?.transform == null ||
                        vacuumable._identifiable?.identType?.groupType == null ||
                        vacuumable._identifiable.identType.icon?.texture == null) continue;

                    var groupName = vacuumable._identifiable.identType.groupType.name;
                    if (groupName != "ResourceOreGroup" && groupName != "ResourceWeatherGroup") continue;

                    var worldPos = vacuumable.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedVacuumableData.Add(new CachedVacuumableData
                    {
                        Vacuumable = vacuumable,
                        WorldPosition = worldPos,
                        Icon = vacuumable._identifiable.identType.icon.texture,
                        Distance = distance
                    });
                }
                // Nach Distanz sortieren für bessere Performance beim Zeichnen
                _CachedVacuumableData.Sort((a, b) => a.Distance.CompareTo(b.Distance));

            }



            if (_PrefShowTreasurePod.Value)
            {
                // TreasurePod verarbeiten
                var allTreasurePod = GameObject.FindObjectsOfType<TreasurePodUIInteractable>();
                _CachedTreasurePodData.Clear();

                foreach (var treasurePod in allTreasurePod)
                {
                    var lIcon = treasurePod.treasurePod?.Blueprint?.icon.texture ?? treasurePod.treasurePod?.UpgradeComponent?.Icon?.texture ?? treasurePod.treasurePod?.SpawnObjs[0]?.icon?.texture;

                    if (treasurePod?.transform == null || lIcon == null) continue;

                    if (!treasurePod.treasurePod.IsLocked) continue;

                    var worldPos = treasurePod.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedTreasurePodData.Add(new CachedTreasurePodData()
                    {
                        TreasurePod = treasurePod,
                        WorldPosition = worldPos,
                        Icon = _PrefSpoilerSafeMode.Value ? null : lIcon,
                        Letter = "T",
                        Distance = distance
                    });
                }

                var plortDepositors = GameObject.FindObjectsOfType<Il2Cpp.PlortDepositor>();

                foreach (var plortCollector in plortDepositors)
                {
                    if (plortCollector?.transform == null) continue;

                    if (!plortCollector.IsLocked()) continue;

                    var icon = plortCollector.IdentifiableTypeToCatch.icon?.texture;

                    var worldPos = plortCollector.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedTreasurePodData.Add(new CachedTreasurePodData()
                    {
                        WorldPosition = worldPos,
                        Icon = _PrefSpoilerSafeMode.Value ? null : icon,
                        Letter = "T",
                        Distance = distance
                    });
                }

                // Nach Distanz sortieren für bessere Performance beim Zeichnen
                _CachedTreasurePodData.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }

            if (_PrefShowGordo.Value)
            {
                var allGordos = GameObject.FindObjectsOfType<GordoIdentifiable>();
                _CachedGordoData.Clear();

                foreach (var gordo in allGordos)
                {
                    if (gordo?.transform == null) continue;

                    var lIcon = gordo.identType?.Icon?.texture;
                    if (lIcon == null) continue;

                    // Beispiel: nur aktive/lebende Gordos
                    if (!gordo.isActiveAndEnabled) continue;

                    var worldPos = gordo.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedGordoData.Add(
                        new CachedGordoData()
                        {
                            Gordo = gordo,
                            WorldPosition = worldPos,
                            Icon = lIcon,
                            Distance = distance,
                            IsActive = gordo.isActiveAndEnabled
                        }
                    );
                }

                // Nach Distanz sortieren für bessere Performance beim Zeichnen
                _CachedGordoData.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }

            if (_PrefShowDirectedAnimalSpawner.Value)
            {
                var allSpawners = GameObject.FindObjectsOfType<DirectedAnimalSpawner>();

                _CachedDirectedAnimalSpawnerData.Clear();

                foreach (var spawner in allSpawners)
                {
                    if (spawner?.transform == null)
                        continue;

                    var worldPos = spawner.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius)
                        continue;

                    _CachedDirectedAnimalSpawnerData.Add(new CachedDirectedAnimalSpawnerData()
                    {
                        Spawner = spawner,
                        WorldPosition = worldPos,
                        Letter = "N", // N für Nest
                        Distance = distance,
                        IsAvailable = spawner.enabled // oder eine andere Logik wenn nötig
                    });
                }

                _CachedDirectedAnimalSpawnerData.Sort(
                    (a, b) => a.Distance.CompareTo(b.Distance)
                );
            }


            if (_PrefShowVacuumableFood.Value)
            {
                var allVacuumables1 = GameObject.FindObjectsOfType<Vacuumable>();

                _CachedVacuumableFoodData.Clear();

                foreach (var vacuumable in allVacuumables1)
                {
                    if (vacuumable?.transform == null ||
                        vacuumable._identifiable?.identType?.groupType == null ||
                        vacuumable._identifiable.identType.icon?.texture == null) continue;

                    var groupName = vacuumable._identifiable.identType.groupType.name;

                    if (groupName == "ResourceOreGroup" && groupName == "ResourceWeatherGroup") continue;

                    var worldPos = vacuumable.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedVacuumableFoodData.Add(new CachedVacuumableData()
                    {
                        Vacuumable = vacuumable,
                        WorldPosition = worldPos,
                        Icon = vacuumable._identifiable.identType.icon.texture,
                        Distance = distance
                    });
                }

                _CachedVacuumableFoodData.Sort(
                    (a, b) => a.Distance.CompareTo(b.Distance)
                );
            }

            if (_PrefShowCrates.Value)
            {
                var allVacuumables = GameObject.FindObjectsOfType<Vacuumable>();

                _CachedCachedCrateData.Clear();

                foreach (var vacuumable in allVacuumables)
                {
                    if (vacuumable?.transform == null) continue;

                    var groupName = vacuumable.name;

                    if (groupName != "containerFields01(Clone)") continue;

                    var worldPos = vacuumable.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedCachedCrateData.Add(new CachedVacuumableData()
                    {
                        Vacuumable = vacuumable,
                        WorldPosition = worldPos,
                        Letter = "C",
                        Distance = distance
                    });
                }

                _CachedCachedCrateData.Sort(
                    (a, b) => a.Distance.CompareTo(b.Distance)
                );
            }

            if (_PrefShowGadgetTracker.Value)
            {
                var allGadget = GameObject.FindObjectsOfType<Gadget>();

                _CachedGadgetData.Clear();

                foreach (var gadget in allGadget)
                {
                    if (gadget?.transform == null) continue;

                    string letter = "G";

                    if (gadget.IdentTypeAsDefinition?.name == "RefineryLink")
                    {
                        letter = "RL";
                    }
                    else if (gadget.IdentTypeAsDefinition?.name == "MarketLink")
                    {
                        letter = "ML";
                    }
                    else if (gadget.IdentTypeAsDefinition.Type == GadgetDefinition.Types.TELEPORTER)
                    {
                        letter = "TP";
                    }
                    else
                        continue;

                    var worldPos = gadget.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedGadgetData.Add(new CachedWorldData()
                    {
                        WorldPosition = worldPos,
                        Icon = gadget.identType?.icon?.texture,
                        Letter = letter,
                        Distance = distance
                    });
                }

                _CachedGadgetData.Sort(
                    (a, b) => a.Distance.CompareTo(b.Distance)
                );
            }


            if (_PrefShowUnstablePrismaPlorts.Value)
            {
                var allVacuumables = GameObject.FindObjectsOfType<Vacuumable>();

                _CachedCachedUnstablePrismaPlortsData.Clear();

                foreach (var vacuumable in allVacuumables)
                {
                    if (vacuumable?.transform == null) continue;

                    var Type = vacuumable._identifiable?.identType?.ReferenceId;

                    if (Type != "IdentifiableType.UnstablePlort" && Type != "IdentifiableType.StablePlort") continue;

                    var worldPos = vacuumable.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if (distance > detectionRadius) continue;

                    _CachedCachedUnstablePrismaPlortsData.Add(new CachedVacuumableData()
                    {
                        Vacuumable = vacuumable,
                        WorldPosition = worldPos,
                        Icon = vacuumable._identifiable?.identType?.icon?.texture,
                        Letter = "UP",
                        Distance = distance
                    });
                }

                _CachedCachedUnstablePrismaPlortsData.Sort(
                    (a, b) => a.Distance.CompareTo(b.Distance)
                );
            }


            if (_PrefShowPlortStatues.Value)
            {
                var allPuzzleSlot = GameObject.FindObjectsOfType<PuzzleSlot>();

                _CachedCachedPlortStatuesData.Clear();

                foreach (var PuzzleSlot in allPuzzleSlot)
                {
                    if (PuzzleSlot?.transform == null) continue;

                    var worldPos = PuzzleSlot.transform.position;
                    var distance = Vector3.Distance(playerPos, worldPos);

                    if ((PuzzleSlot?._activateOnFill[0]?.active ?? false) || (PuzzleSlot._activateOnFill[0]?.activeSelf ?? false))
                        continue;

                    if (distance > detectionRadius) continue;

                    _CachedCachedPlortStatuesData.Add(new CachedWorldData()
                    {
                        WorldPosition = worldPos,
                        Icon = PuzzleSlot.IdentifiableTypeToCatch?.icon?.texture,
                        Letter = "DOOR",
                        Distance = distance
                    });
                }

                _CachedCachedPlortStatuesData.Sort(
                    (a, b) => a.Distance.CompareTo(b.Distance)
                );
            }

        }


        private static void DrawCachedResourceData()
        {
            if (Camera.main == null) return;

            var camera = Camera.main;
            int drawnCount = 0;

            if (_PrefShowResources.Value)
            {
                // -------------------
                // Resources
                // -------------------
                DrawDataList(
                _CachedResourceData,
                camera,
                ref drawnCount,
                _MaxDrawnResources.Value,
                215f,
                data =>
                {
                    if (data.IsHarvestable && _PrefInstantSuck.Value && data.Resource?.resourceNode != null)
                    {
                        data.Resource.resourceNode._timeToHarvest = 0.01f;
                        data.Resource.resourceNode._resourceSpawnDelaySeconds = 0.01f;
                    }
                }
                );


                // -------------------
                // Vacuumables
                // -------------------


                DrawDataList(
                _CachedVacuumableData,
                camera,
                ref drawnCount,
                _MaxDrawnResources.Value,
                215f
                );
            }


            // -------------------
            // TreasurePods
            // -------------------
            if (_PrefShowTreasurePod.Value)
            {
                DrawDataList(
                    _CachedTreasurePodData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f
                );
            }

            if (_PrefShowGordo.Value)
            {
                DrawDataList(
                    _CachedGordoData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f
                );
            }

            if (_PrefShowDirectedAnimalSpawner.Value)
            {
                DrawDataList(
                    _CachedDirectedAnimalSpawnerData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f
                );
            }

            if (_PrefShowVacuumableFood.Value)
            {
                DrawDataList(
                    _CachedVacuumableFoodData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f);
            }

            if (_PrefShowCrates.Value)
            {
                DrawDataList(
                    _CachedCachedCrateData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f);
            }

            if (_PrefShowGadgetTracker.Value)
            {
                DrawDataList(
                    _CachedGadgetData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f);
            }

            if (_PrefShowUnstablePrismaPlorts.Value)
            {
                DrawDataList(
                    _CachedCachedUnstablePrismaPlortsData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f);
            }

            if (_PrefShowPlortStatues.Value)
            {
                DrawDataList(
                    _CachedCachedPlortStatuesData,
                    camera,
                    ref drawnCount,
                    _MaxDrawnResources.Value,
                    215f);
            }

        }

        /// <summary>
        /// Generische Draw-Methode für alle Caches
        /// </summary>
        private static void DrawDataList<T>(
            IEnumerable<T> dataList,
            Camera camera,
            ref int drawnCount,
            int maxDrawn,
            float maxDistance,
            Action<T>? specialAction = null
        ) where T : CachedWorldData
        {
            foreach (var data in dataList)
            {
                if (drawnCount >= maxDrawn) break;

                // --- Frustum Culling ---
                Vector3 screenPos = camera.WorldToScreenPoint(data.WorldPosition);
                if (screenPos.z <= 0 ||
                    screenPos.x < -50 || screenPos.x > Screen.width + 50 ||
                    screenPos.y < -50 || screenPos.y > Screen.height + 50)
                    continue;

                // Speziallogik optional ausführen (z. B. InstantSuck nur bei Resources)
                specialAction?.Invoke(data);

                // --- Scale ---
                float smoothScale = Mathf.Clamp01(Remap(data.Distance, 0f, 215f, 0f, 1f));

                if (data.Icon != null)
                    DrawIcon(data.WorldPosition, data.Icon, smoothScale);

                else
                    DrawLetterMarker(data.WorldPosition, data.Letter, smoothScale);

                drawnCount++;
            }
        }


        public static float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return from2 + (value - from1) * (to2 - from2) / (to1 - from1);
        }


        private static void DrawIcon(
    Vector3 worldPos,
    Texture2D icon,
    float smoothScale
)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            screenPos.y = Screen.height - screenPos.y;

            // ---------- Größe ----------
            float baseSize = 50f;
            float scaleValue = smoothScale * 25f;
            float iconSize = Mathf.Max(baseSize - scaleValue, 15f);

            Rect rect = new Rect(
                screenPos.x - iconSize * 0.5f,
                screenPos.y - iconSize * 0.5f,
                iconSize,
                iconSize
            );

            // ---------- Transparenz ----------
            float alpha = Mathf.Lerp(1f, 0.4f, smoothScale);

            // ---------- Box (immer blau) ----------
            GUI.color = new Color(0f, 0f, 1f, alpha * 0.8f);
            GUI.Box(rect, GUIContent.none);

            // ---------- Ampel-Farblogik ----------
            Color iconColor;

            if (_PrefEnableColors.Value)
            {
                // Ampel-System basierend auf smoothScale
                // 0–0.25: neutral | 0.25–0.5: grün | 0.5–0.75: gelb | 0.75–1.0: rot
                float tintAlpha = 0.5f * alpha;

                if (smoothScale < 0.25f)
                    iconColor = new Color(1f, 1f, 1f, tintAlpha); // neutral
                else if (smoothScale < 0.5f)
                    iconColor = new Color(0f, 1f, 0f, tintAlpha); // grün
                else if (smoothScale < 0.75f)
                    iconColor = new Color(1f, 1f, 0f, tintAlpha); // gelb
                else
                    iconColor = new Color(1f, 0f, 0f, tintAlpha); // rot
            }
            else
            {
                // Wenn ausgeschaltet -> immer weiß
                iconColor = new Color(1f, 1f, 1f, alpha);
            }

            GUI.color = iconColor;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);

            GUI.color = Color.white; // reset
        }

        private static void DrawLetterMarker(
     Vector3 worldPos,
     string letter,
     float smoothScale
 )
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            screenPos.y = Screen.height - screenPos.y;

            float baseSize = 50f;
            float scaleValue = smoothScale * 25f;
            float iconSize = Mathf.Max(baseSize - scaleValue, 15f);

            Rect rect = new Rect(
                screenPos.x - iconSize * 0.5f,
                screenPos.y - iconSize * 0.5f,
                iconSize,
                iconSize
            );

            float alpha = Mathf.Lerp(1f, 0.4f, smoothScale);

            // ---------- Ampel-Farblogik ----------
            float tintAlpha = 0.5f * alpha;
            Color tintedColor;

            if (smoothScale < 0.25f)
                tintedColor = new Color(1f, 1f, 1f, tintAlpha);
            else if (smoothScale < 0.5f)
                tintedColor = new Color(0f, 1f, 0f, tintAlpha);
            else if (smoothScale < 0.75f)
                tintedColor = new Color(1f, 1f, 0f, tintAlpha);
            else
                tintedColor = new Color(1f, 0f, 0f, tintAlpha);

            GUI.color = tintedColor;
            GUI.Box(rect, GUIContent.none);

            GUIStyle style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = (int)(iconSize * 0.6f),
            };

            GUI.Label(rect, letter.ToString(), style);
            GUI.color = Color.white;
        }

        private static void zEnsurePlayerTransform()
        {
            if (_PlayerTransform != null) return;

            GameObject lPlayerObj = GameObject.FindWithTag("Player");
            if (lPlayerObj != null)
                _PlayerTransform = lPlayerObj.transform;
        }

        #endregion
    }
}
