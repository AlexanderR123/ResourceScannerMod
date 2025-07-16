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

        #endregion

        #region Melon Methods

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("Initialized.");

            _PrefsCategory = MelonPreferences.CreateCategory("ResourceScanner");
            _PrefDetectionRadius = _PrefsCategory.CreateEntry("DetectionRadius", 200f, "Detection Radius");
            _PrefToggleKey = _PrefsCategory.CreateEntry("ToggleKey", Key.K, "Toggle Key");
            _PrefInstantSuck = _PrefsCategory.CreateEntry("InstantSuck", false, "Remove Resource Extraction Delay");

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

            if (_PlayerTransform == null)
                return;

            var lAllResources = GameObject.FindObjectsOfType<ResourceNodeUIInteractable>();

            int lTotalResources = (lAllResources != null ? lAllResources.Length : 0);

            if (lTotalResources == 0)
            {
                if (!_WaitingForResources)
                {
                    MelonLogger.Msg("No resourcenodes found yet – waiting...");
                    _WaitingForResources = true;
                }
                return;
            }
            else if (_WaitingForResources)
            {
                MelonLogger.Msg($"Resourcenodes found! ({lTotalResources} nodes)");
                _WaitingForResources = false;
            }

            var lVacuumableResources = GameObject.FindObjectsOfType<Vacuumable>();
            lTotalResources = (lVacuumableResources != null ? lVacuumableResources.Length : 0);

            if (lTotalResources == 0)
            {
                if (!_WaitingForResources)
                {
                    MelonLogger.Msg("No vacuumableresources found yet – waiting...");
                    _WaitingForResources = true;
                }
                return;
            }
            else if (_WaitingForResources)
            {
                MelonLogger.Msg($"Vacuumableresources found! ({lTotalResources} nodes)");
                _WaitingForResources = false;
            }


            _ShowResourcesInit = false;

            foreach (var lResource in lAllResources.Where(r => r.gameObject.activeInHierarchy).ToList())
            {
                if (lResource?.transform == null || lResource.resourceNode == null)
                    continue;

                float lDistance = Vector3.Distance(_PlayerTransform.position, lResource.transform.position);
                if (lDistance <= _PrefDetectionRadius.Value)
                {
                    float lScale = (lDistance / _PrefDetectionRadius.Value);
                    if (lResource.resourceNode._harvestAtTime == 0)
                    {
                        if (_PrefInstantSuck.Value)
                        {
                            lResource.resourceNode._timeToHarvest = 0.01f;
                            lResource.resourceNode._resourceSpawnDelaySeconds = 0.01f;
                        }

                        zDrawResourceIcon(lResource, lScale);
                    }
                }
            }

            foreach (var lVacuumable in lVacuumableResources)
            {
                if (lVacuumable?.transform == null || lVacuumable?._identifiable?.identType?.groupType == null)
                    continue;

                float lDistance = Vector3.Distance(_PlayerTransform.position, lVacuumable.transform.position);
                if (lDistance <= _PrefDetectionRadius.Value)
                {
                    float lScale = (lDistance / _PrefDetectionRadius.Value);
                    string lName = lVacuumable._identifiable.identType.groupType.name;
                    if (lName == "ResourceOreGroup" || lName == "ResourceWeatherGroup")
                    {
                        zDrawVacuumableIcon(lVacuumable, lScale);
                    }
                }
            }

        }

        private static void zDrawVacuumableIcon(Vacuumable pVacuumable, float pScale)
        {
            if (pVacuumable == null || pVacuumable._identifiable == null || pVacuumable._identifiable.identType == null || pVacuumable._identifiable.identType.icon == null)
                return;

            if (Camera.main == null)
                return;

            Vector3 lScreenPos = Camera.main.WorldToScreenPoint(pVacuumable.transform.position);
            if (lScreenPos.z <= 0)
                return;

            lScreenPos.y = Screen.height - lScreenPos.y;
            float lScaleValue = pScale * 25;
            Rect lRect = new Rect(lScreenPos.x - 25 + lScaleValue, lScreenPos.y - 25 + lScaleValue, 50 - lScaleValue, 50 - lScaleValue);

            GUI.color = Color.blue;
            GUI.Box(lRect, GUIContent.none);

            GUI.color = Color.white;
            GUI.DrawTexture(lRect, pVacuumable._identifiable.identType.icon.texture, ScaleMode.ScaleToFit, true);
        }


        private static void zDrawResourceIcon(ResourceNodeUIInteractable pResource, float pScale)
        {
            if (pResource == null || pResource.resourceNode == null || pResource.transform == null)
                return;

            var lResourcesList = pResource.resourceNode.GetResources();
            if (lResourcesList == null || lResourcesList.Count == 0)
                return;

            var lResourceData = lResourcesList[0];
            if (lResourceData == null || lResourceData.icon == null || lResourceData.icon.texture == null)
                return;

            if (Camera.main == null)
                return;

            Vector3 lScreenPos = Camera.main.WorldToScreenPoint(pResource.transform.position);
            if (lScreenPos.z <= 0)
                return;

            lScreenPos.y = Screen.height - lScreenPos.y;
            float lScaleValue = pScale * 25;
            Rect lRect = new Rect(lScreenPos.x - 25 + lScaleValue, lScreenPos.y - 25 + lScaleValue, 50 - lScaleValue, 50 - lScaleValue);

            GUI.color = Color.blue;
            GUI.Box(lRect, GUIContent.none);

            GUI.color = Color.white;
            GUI.DrawTexture(lRect, lResourceData.icon.texture, ScaleMode.ScaleToFit, true);
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
