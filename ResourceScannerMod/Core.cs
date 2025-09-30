using Il2Cpp;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppMonomiPark.SlimeRancher;
using Il2CppMonomiPark.SlimeRancher.Labyrinth;
using Il2CppMonomiPark.SlimeRancher.UI;
using Il2CppSystem.Collections;
using Il2CppSystem.Data;
using Il2CppSystem.Xml;
using Il2CppSystem.Xml.Serialization;
using MelonLoader;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[assembly: MelonInfo(typeof(ResourceScannerMod.Core), "ResourceScannerMod", "1.0.0", "AlexanderR123", null)]
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


        // Caching-Felder
        private static List<CachedResourceData> _CachedResourceData = new List<CachedResourceData>();
        private static List<CachedVacuumableData> _CachedVacuumableData = new List<CachedVacuumableData>();
        private static List<CachedTreasurePodData> _CachedTreasurePodData = new List<CachedTreasurePodData>();
        private static float _LastDataUpdateTime = 0f;
        private static float _DataUpdateInterval = 2.5f; // Daten alle 0.1s aktualisieren

        private struct CachedResourceData
        {
            public ResourceNodeUIInteractable Resource;
            public Vector3 WorldPosition;
            public Texture2D Icon;
            public float Distance;
            public bool IsHarvestable;
        }

        private struct CachedTreasurePodData
        {
            public TreasurePodUIInteractable TreasurePod;
            public Vector3 WorldPosition;
            public Texture2D Icon;
            public float Distance;
            public bool IsCollectable;
        }

        private struct CachedVacuumableData
        {
            public Vacuumable Vacuumable;
            public Vector3 WorldPosition;
            public Texture2D Icon;
            public float Distance;
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

                // Nach Distanz sortieren für bessere Performance beim Zeichnen
                _CachedTreasurePodData.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            }

            // Nach Distanz sortieren für bessere Performance beim Zeichnen
            _CachedResourceData.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            _CachedVacuumableData.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        }


        private static void DrawCachedResourceData()
        {
            if (Camera.main == null) return;

            var camera = Camera.main;
            var detectionRadius = _PrefDetectionRadius.Value;
            int drawnCount = 0;
            int maxDrawn = _MaxDrawnResources;

            // Resources zeichnen
            foreach (var data in _CachedResourceData)
            {
                if (drawnCount >= maxDrawn) break;

                // Frustum Culling - nur sichtbare Objekte
                Vector3 screenPos = camera.WorldToScreenPoint(data.WorldPosition);
                if (screenPos.z <= 0 ||
                    screenPos.x < -50 || screenPos.x > Screen.width + 50 ||
                    screenPos.y < -50 || screenPos.y > Screen.height + 50) continue;

                if (data.IsHarvestable)
                {
                    // Instant Suck nur bei den tatsächlich sichtbaren Objekten anwenden
                    if (_PrefInstantSuck.Value && data.Resource?.resourceNode != null)
                    {
                        data.Resource.resourceNode._timeToHarvest = 0.01f;
                        data.Resource.resourceNode._resourceSpawnDelaySeconds = 0.01f;
                    }

                    float scale = Remap(data.Distance, 0f, 300f, 0f, 1f);
                    scale = Mathf.Clamp01(scale);
                    DrawIcon(data.WorldPosition, data.Icon, scale, Color.green);
                    drawnCount++;
                }
            }

            // Vacuumables zeichnen
            foreach (var data in _CachedVacuumableData)
            {
                if (drawnCount >= maxDrawn) break;

                Vector3 screenPos = camera.WorldToScreenPoint(data.WorldPosition);
                if (screenPos.z <= 0 ||
                    screenPos.x < -50 || screenPos.x > Screen.width + 50 ||
                    screenPos.y < -50 || screenPos.y > Screen.height + 50) continue;

                float scale = Remap(data.Distance, 0f, 215f, 0f, 1f);
                scale = Mathf.Clamp01(scale);
                DrawIcon(data.WorldPosition, data.Icon, scale, Color.red);
                drawnCount++;
            }

            if (_PrefShowTreasurePod.Value)
            {
                // TreasurePod zeichnen
                foreach (var data in _CachedTreasurePodData)
                {
                    if (drawnCount >= maxDrawn) break;

                    Vector3 screenPos = camera.WorldToScreenPoint(data.WorldPosition);
                    if (screenPos.z <= 0 ||
                        screenPos.x < -50 || screenPos.x > Screen.width + 50 ||
                        screenPos.y < -50 || screenPos.y > Screen.height + 50) continue;

                    float scale = Remap(data.Distance, 0f, 300f, 0f, 1f);
                    scale = Mathf.Clamp01(scale);
                    DrawIcon(data.WorldPosition, data.Icon, scale, Color.blue);
                    drawnCount++;
                }
            }
        
        
        }


        public static float Remap(float value, float from1, float to1, float from2, float to2)
        {
            return from2 + (value - from1) * (to2 - from2) / (to1 - from1);
        }

        private static void DrawIcon(Vector3 worldPos, Texture2D icon, float scale, Color uniqueColor)
        {
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);
            screenPos.y = Screen.height - screenPos.y;

            // LOD-System für Icon-Größe
            float baseSize = 50f;
            float scaleValue = scale * 25f;
            float iconSize = Mathf.Max(baseSize - scaleValue, 15f);

            Rect rect = new Rect(
                screenPos.x - iconSize * 0.5f,
                screenPos.y - iconSize * 0.5f,
                iconSize,
                iconSize
            );

            // Transparenz
            float alpha = Mathf.Lerp(1f, 0.4f, scale);

            GUI.color = new Color(0f, 0f, 1f, alpha * 0.8f);
            GUI.Box(rect, GUIContent.none);

            // --- Icon: Weit weg = stark UniqueColor, nah = fast weiß, aber mit Rest-Farbe ---
            Color baseIconColor = new Color(1f, 1f, 1f, alpha);

            // Faktor für die Mischung
            float t = 1f - scale;
            // dafür sorgen, dass immer mind. 20% UniqueColor erhalten bleibt
            float minUnique = 0.2f;
            float uniqueWeight = Mathf.Lerp(1f, minUnique, t);

            Color finalIconColor = (uniqueColor * uniqueWeight) + (baseIconColor * (1f - uniqueWeight));
            finalIconColor.a = alpha; // wichtig: Alpha nicht überschreiben

            GUI.color = finalIconColor;
            GUI.DrawTexture(rect, icon, ScaleMode.ScaleToFit, true);

            GUI.color = Color.white; // Reset
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
