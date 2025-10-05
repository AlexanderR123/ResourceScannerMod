using Il2Cpp;
using Il2CppMonomiPark.SlimeRancher;
using Il2CppMonomiPark.SlimeRancher.UI;
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

        private static MelonPreferences_Category _PrefsCategory;
        private static MelonPreferences_Entry<float> _PrefDetectionRadius;
        private static MelonPreferences_Entry<Key> _PrefToggleKey;
        private static MelonPreferences_Entry<bool> _PrefInstantSuck;
        private static MelonPreferences_Entry<bool> _PrefShowTreasurePod;
        private static MelonPreferences_Entry<bool> _PrefEnableColors;
        private static MelonPreferences_Entry<bool> _PrefShowGordo;
        private static MelonPreferences_Entry<bool> _PrefShowDirectedAnimalSpawner;
        private static MelonPreferences_Entry<bool> _PrefShowVacuumableFood;


        // Caching-Felder
        private static List<CachedResourceData> _CachedResourceData = new List<CachedResourceData>();
        private static List<CachedVacuumableData> _CachedVacuumableData = new List<CachedVacuumableData>();
        private static List<CachedTreasurePodData> _CachedTreasurePodData = new List<CachedTreasurePodData>();
        private static List<CachedGordoData> _CachedGordoData = new List<CachedGordoData>();
        private static List<CachedDirectedAnimalSpawnerData> _CachedDirectedAnimalSpawnerData = new List<CachedDirectedAnimalSpawnerData>();
        private static List<CachedVacuumableData> _CachedVacuumableFoodData = new List<CachedVacuumableData>();

        private static float _LastDataUpdateTime = 0f;
        private static float _DataUpdateInterval = 2.5f; // Daten alle 0.1s aktualisieren

        public abstract class CachedWorldIconData
        {
            public Vector3 WorldPosition;
            public string Letter;
            public Texture2D Icon;
            public float Distance;
        }

        public class CachedResourceData : CachedWorldIconData
        {
            public ResourceNodeUIInteractable Resource;
            public bool IsHarvestable;
        }

        public class CachedTreasurePodData : CachedWorldIconData
        {
            public TreasurePodUIInteractable TreasurePod;
            public bool IsCollectable;
        }

        public class CachedVacuumableData : CachedWorldIconData
        {
            public Vacuumable Vacuumable;
        }

        public class CachedGordoData : CachedWorldIconData
        {
            public GordoIdentifiable Gordo;
            public bool IsActive;
        }

        public class CachedDirectedAnimalSpawnerData : CachedWorldIconData
        {
            public DirectedAnimalSpawner Spawner;
            public bool IsAvailable;
        }

        private static int _MaxDrawnResources = 100; // Maximal gleichzeitig gezeichnete Ressourcen
        #endregion

        #region Melon Methods

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");

            _PrefsCategory = MelonPreferences.CreateCategory("ResourceScanner");
            _PrefDetectionRadius = _PrefsCategory.CreateEntry("DetectionRadius", 200f, "Detection Radius");
            _PrefToggleKey = _PrefsCategory.CreateEntry("ToggleKey", Key.K, "Toggle Key");
            _PrefInstantSuck = _PrefsCategory.CreateEntry("InstantSuck", false, "Remove Resource Extraction Delay");
            _PrefShowTreasurePod = _PrefsCategory.CreateEntry("ShowTreasurePod", false, "Shows Treasure Pod");
            _PrefEnableColors = _PrefsCategory.CreateEntry("EnableTintColors", false, "Enable colored tint on icons");
            _PrefShowGordo = _PrefsCategory.CreateEntry("ShowGordo", false, "Shows Gordos");
            _PrefShowDirectedAnimalSpawner = _PrefsCategory.CreateEntry("ShowDirectedAnimalSpawner", false, "Shows Directed Animal Spawners");
            _PrefShowVacuumableFood = _PrefsCategory.CreateEntry("ShowVacuumableFood", false, "Show Vacuumable Food");

            // Neue Performance-Einstellungen
            var _PrefMaxDrawnResources = _PrefsCategory.CreateEntry("MaxDrawnResources", 100, "Maximum drawn resources");

            // Werte anwenden
            _MaxDrawnResources = _PrefMaxDrawnResources.Value;

            MelonPreferences.Load();
        }

        public override void OnLateUpdate()
        {
            Key lToggleKey = _PrefToggleKey.Value;

            if (Keyboard.current[lToggleKey].wasPressedThisFrame)
            {
                ToggleShowResources();
            }
            if (Keyboard.current[Key.L].wasPressedThisFrame)
            {
                _PrefShowTreasurePod.Value = !_PrefShowTreasurePod.Value;
                if (_PrefShowTreasurePod.Value)
                    MelonLogger.Msg("Show Treasurepods Enabled");
                else 
                    MelonLogger.Msg("Show Treasurepods Disabled");
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
                        Icon = lIcon,
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
                        Icon = icon,
                        Letter = "C",
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

            // Nach Distanz sortieren für bessere Performance beim Zeichnen
            _CachedResourceData.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            _CachedVacuumableData.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }


        private static void DrawCachedResourceData()
        {
            if (Camera.main == null) return;

            var camera = Camera.main;
            int drawnCount = 0;
            int maxDrawn = _MaxDrawnResources;

            // -------------------
            // Resources
            // -------------------
            DrawDataList(
                _CachedResourceData,
                camera,
                ref drawnCount,
                maxDrawn,
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
                maxDrawn,
                215f
            );

            // -------------------
            // TreasurePods
            // -------------------
            if (_PrefShowTreasurePod.Value)
            {
                DrawDataList(
                    _CachedTreasurePodData,
                    camera,
                    ref drawnCount,
                    maxDrawn,
                    215f
                );
            }

            if (_PrefShowGordo.Value)
            {
                DrawDataList(
                    _CachedGordoData,
                    camera,
                    ref drawnCount,
                    maxDrawn,
                    215f
                );
            }

            if (_PrefShowDirectedAnimalSpawner.Value)
            {
                DrawDataList(
                    _CachedDirectedAnimalSpawnerData,
                    camera,
                    ref drawnCount,
                    maxDrawn,
                    215f
                );
            }

            if (_PrefShowVacuumableFood.Value)
            {
                DrawDataList(
                    _CachedVacuumableFoodData,
                    camera,
                    ref drawnCount,
                    maxDrawn,
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
        ) where T : CachedWorldIconData
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
