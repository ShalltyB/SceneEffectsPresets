using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DefaultParamEditor.Koikatu;
using KKAPI.Studio.SaveLoad;
using KKAPI.Utilities;
using ParadoxNotion.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ToolBox.Extensions;
using UnityEngine;
using static DefaultParamEditor.Koikatu.DefaultParamEditor;
using static DefaultParamEditor.Koikatu.ParamData;

namespace SceneEffectsPresets
{
    [BepInPlugin(GUID, PluginName, Version)]
    [BepInDependency(KKAPI.KoikatuAPI.GUID, KKAPI.KoikatuAPI.VersionConst)]
    [BepInDependency(DefaultParamEditor.Koikatu.DefaultParamEditor.GUID, DefaultParamEditor.Koikatu.DefaultParamEditor.Version)]
    [BepInProcess(KK_Plugins.Constants.StudioProcessName)]
    public class SceneEffectsPresets : BaseUnityPlugin
    {
        #region CONFIG VARIABLES

        public const string GUID = "com.shallty.SceneEffectsPresets";
        public const string PluginName = "Scene Effects Presets";
#if KK
        public const string PluginNameInternal = "KK_SceneEffectsPresets";
#elif KKS
        public const string PluginNameInternal = "KKS_SceneEffectsPresets";
#endif
        public const string Version = "2.0";
        private const int _uniqueId = ('S' << 24) | ('E' << 16) | ('P' << 8) | 'R';
        internal static new ManualLogSource Logger;

        #endregion CONFIG VARIABLES

        #region UI VARIABLES

        private static GUISkin NewGUISkin;
        private static GUISkin DefaultGUISkin;

        private static ConfigEntry<KeyboardShortcut> keyShortcut;
        private static Rect windowRect = new Rect(130, 230f, 330f, 630f);
        private static bool toggleUI = false;
        private static Color defColor = GUI.color;
        private static Vector2 presetsFilesScroll;
        private static Vector2 mixPresetsScroll;
        public static string saveName = "LastestScene";
        public static string folder_path = "";
        private static List<string> fileList = new List<string>();

        private static OrderBy fileOrder = OrderBy.Date;
        private static Sort fileNameSort = Sort.Descending;
        private static Sort fileDateSort = Sort.Descending;

        private static bool isSelecting = false;
        private static string presetsSearch = "";

        private static SceneData currentSceneData;

        private static float currentSceneData_mixStrength = 100f;
        private static float currentSceneData_allSliders = 50f;

        private static float currentSceneData_aceNo = 100f;
        private static float currentSceneData_ace2No = 100f;
        private static float currentSceneData_aceBlend = 100f;
        private static float currentSceneData_enableAOE = 100f;
        private static float currentSceneData_aoeColor = 100f;
        private static float currentSceneData_aoeRadius = 100f;
        private static float currentSceneData_enableBloom = 100f;
        private static float currentSceneData_bloomIntensity = 100f;
        private static float currentSceneData_bloomThreshold = 100f;
        private static float currentSceneData_bloomBlur = 100f;
        private static float currentSceneData_enableDepth = 100f;
        private static float currentSceneData_depthFocalSize = 100f;
        private static float currentSceneData_depthAperture = 100f;
        private static float currentSceneData_enableVignette = 100f;
        private static float currentSceneData_enableFog = 100f;
        private static float currentSceneData_fogColor = 100f;
        private static float currentSceneData_fogHeight = 100f;
        private static float currentSceneData_fogStartDistance = 100f;
        private static float currentSceneData_enableSunShafts = 100f;
        private static float currentSceneData_sunThresholdColor = 100f;
        private static float currentSceneData_sunColor = 100f;
        private static float currentSceneData_enableShadow = 100f;
        private static float currentSceneData_lineColorG = 100f;
        private static float currentSceneData_ambientShadow = 100f;
        private static float currentSceneData_lineWidthG = 100f;
        private static float currentSceneData_rampG = 100f;
        private static float currentSceneData_ambientShadowG = 100f;

        private static KeyValuePair<string, SceneData> presetA;
        private static KeyValuePair<string, SceneData> presetB;

        private static bool autoUpdate = true;

        private static bool showMixPresets = false;

        #endregion UI VARIABLES

        public enum OrderBy
        {
            Name,
            Date
        }
        public enum Sort
        {
            Ascending,
            Descending
        }


        internal void Awake()
        {
            #region BEPINEX CONFIG

            Logger = base.Logger;

            StudioSaveLoadApi.SceneLoad += new EventHandler<SceneLoadEventArgs>(OnSceneLoad);

            folder_path = Path.Combine(Path.GetFullPath(UserData.Path), "SceneEffectsPresets");
            if (!Directory.Exists(folder_path))
                Directory.CreateDirectory(folder_path);

            /// MIGRATE FILES TO THE NEW PATH
            ///
            var old_path = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "SceneEffects_Presets");
            if (Directory.Exists(old_path) && Directory.Exists(folder_path))
            {
                try
                {
                    DirectoryInfo oldDirectory = new DirectoryInfo(old_path);
                    FileInfo[] jsonFiles = oldDirectory.GetFiles("*.json");

                    Logger.LogInfo("Moving " + jsonFiles.Length + " files to new path: " + folder_path);

                    foreach (FileInfo file in jsonFiles)
                    {
                        if (file == null) continue;

                        string fileName = file.Name;
                        string filePath = Path.Combine(folder_path, fileName);
                        int count = 1;
                        while (File.Exists(filePath))
                        {
                            fileName = Path.GetFileNameWithoutExtension(file.Name) + "_" + count + Path.GetExtension(file.Name);
                            filePath = Path.Combine(folder_path, fileName);
                            count++;
                        }
                        file.MoveTo(filePath);
                    }

                    if (oldDirectory.GetFiles().Length == 0 && oldDirectory.GetDirectories().Length == 0)
                    {
                        oldDirectory.Delete();
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError("There was an error migrating the files: " + ex.Message);
                }
            }

            KeyboardShortcut _keyShortcut = new KeyboardShortcut(KeyCode.F, KeyCode.LeftAlt);
            keyShortcut = Config.Bind("GENERAL", "Open UI shortcut", _keyShortcut, "Press this button to launch the UI.");

            #endregion BEPINEX CONFIG

            //LoadResources();
        }

        internal void Update()
        {
            if (keyShortcut.Value.IsDown())
            {
                ReloadFilesList();
                toggleUI = !toggleUI;
            }
        }

        private void OnGUI()
        {
            var skin = GUI.skin;
            GUI.skin = IMGUIUtils.SolidBackgroundGuiSkin;

            if (toggleUI)
            {
                windowRect = GUILayout.Window(_uniqueId, windowRect, WindowFunction, PluginName + "  " + Version);
            }

            GUI.skin = skin;
        }
        /*
        private static void LoadResources()
        {
            // GUISKIN

            AssetBundle guiSkinAB = null;
            try
            {
                var res = ResourceUtils.GetEmbeddedResource("guiskin.unity3d") ?? throw new ArgumentNullException("GetEmbeddedResource");
                guiSkinAB = AssetBundle.LoadFromMemory(res) ?? throw new ArgumentNullException("LoadFromMemory");
                NewGUISkin = ScriptableObject.CreateInstance<GUISkin>();

                NewGUISkin.box.normal.background = guiSkinAB.LoadAsset<Texture2D>("box.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.scrollView.normal.background = NewGUISkin.box.normal.background;

                NewGUISkin.window.normal.background = guiSkinAB.LoadAsset<Texture2D>("window.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.window.onNormal.background = guiSkinAB.LoadAsset<Texture2D>("window.png") ?? throw new ArgumentNullException("LoadAsset");

                NewGUISkin.button.normal.background = guiSkinAB.LoadAsset<Texture2D>("button.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.button.hover.background = guiSkinAB.LoadAsset<Texture2D>("buttonhover.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.button.active.background = guiSkinAB.LoadAsset<Texture2D>("buttonactive.png") ?? throw new ArgumentNullException("LoadAsset");

                NewGUISkin.toggle.normal.background = guiSkinAB.LoadAsset<Texture2D>("toggle.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.toggle.hover.background = NewGUISkin.toggle.normal.background;
                NewGUISkin.toggle.onNormal.background = guiSkinAB.LoadAsset<Texture2D>("ontoggle.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.toggle.onHover.background = guiSkinAB.LoadAsset<Texture2D>("ontogglehover.png") ?? throw new ArgumentNullException("LoadAsset");

                // HORIZONTAL
                NewGUISkin.horizontalScrollbarThumb.normal.background = guiSkinAB.LoadAsset<Texture2D>("scrollbarthumb.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.horizontalScrollbar.normal.background = guiSkinAB.LoadAsset<Texture2D>("slider.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.horizontalSliderThumb.normal.background = guiSkinAB.LoadAsset<Texture2D>("sliderthumb.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.horizontalSliderThumb.hover.background = guiSkinAB.LoadAsset<Texture2D>("sliderthumbhover.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.horizontalSlider.normal.background = NewGUISkin.horizontalScrollbar.normal.background;

                // VERTICAL
                NewGUISkin.verticalScrollbarThumb.normal.background = guiSkinAB.LoadAsset<Texture2D>("scrollbarthumbvert.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.verticalScrollbar.normal.background = guiSkinAB.LoadAsset<Texture2D>("slidervert.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.verticalSliderThumb.normal.background = NewGUISkin.horizontalSliderThumb.normal.background;
                NewGUISkin.verticalSliderThumb.hover.background = NewGUISkin.horizontalSliderThumb.hover.background;
                NewGUISkin.verticalSlider.normal.background = NewGUISkin.horizontalScrollbar.normal.background;

                NewGUISkin.textField.normal.background = guiSkinAB.LoadAsset<Texture2D>("textfield.png") ?? throw new ArgumentNullException("LoadAsset");
                NewGUISkin.textField.hover.background = guiSkinAB.LoadAsset<Texture2D>("textfieldhover.png") ?? throw new ArgumentNullException("LoadAsset");

                NewGUISkin.textArea.normal.background = NewGUISkin.textField.normal.background;
                NewGUISkin.textArea.hover.background = NewGUISkin.textField.hover.background;

                var jsonBytes = ResourceUtils.GetEmbeddedResource("guiskindata.json") ?? throw new ArgumentNullException("GetEmbeddedResource");
                string jsonData = Encoding.UTF8.GetString(jsonBytes);
                NewGUISkin = ToolBox.GUISkinSerializer.LoadGUISkin(jsonData, NewGUISkin);

                guiSkinAB.Unload(false);
            }
            catch (Exception)
            {
                if (guiSkinAB != null) guiSkinAB.Unload(true);
                throw;
            }
        }
        */
        private void WindowFunction(int WindowID)
        {
            GUI.color = defColor;

            if (GUI.Button(new Rect(windowRect.width - 18, 0, 18, 18), "X")) toggleUI = false;
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUI.skin.box);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Save File");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            // FILE NAME FIELD
            GUILayout.Label("Name: ", GUILayout.ExpandWidth(false));
            saveName = GUILayout.TextField(saveName, GUILayout.ExpandWidth(true));

            // CLEAR FILE NAME FIELD
            if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                saveName = "";

            // SAVE FILE
            GUI.enabled = !saveName.IsNullOrEmpty();
            if (GUILayout.Button(isSelecting ? "Overwrite" : "Save", GUILayout.ExpandWidth(false)))
            {
                SaveSceneParam(Path.Combine(folder_path, saveName + ".json"));
            }
            GUI.enabled = true;

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Presets List:");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUI.enabled = true;

            GUI.color = fileOrder == OrderBy.Name ? defColor : Color.grey;
            if (GUILayout.Button(fileNameSort == Sort.Ascending ? "Name ↓" : "Name ↑ "))
            {
                if (fileOrder == OrderBy.Name)
                    fileNameSort = fileNameSort == Sort.Ascending ? Sort.Descending : Sort.Ascending;
                else
                    fileOrder = OrderBy.Name;

                ReloadFilesList();
            }
            GUI.color = fileOrder == OrderBy.Date ? defColor : Color.grey;
            if (GUILayout.Button(fileDateSort == Sort.Ascending ? "Date ↓" : "Date ↑ "))
            {
               if (fileOrder == OrderBy.Date)
                    fileDateSort = fileDateSort == Sort.Ascending ? Sort.Descending : Sort.Ascending;
                else
                    fileOrder = OrderBy.Date;

                ReloadFilesList();
            }
            GUI.color = defColor;

            GUILayout.EndHorizontal();



            GUILayout.BeginHorizontal();
            {
                GUI.changed = false;
                GUI.SetNextControlName("sbox");
                var showTipString = presetsSearch.Length == 0 && GUI.GetNameOfFocusedControl() != "sbox";
                var newVal = GUILayout.TextField(showTipString ? " Search..." : presetsSearch, GUILayout.ExpandWidth(true));
                if (GUI.changed)
                    presetsSearch = newVal;

                if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                    presetsSearch = "";
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginVertical(GUI.skin.box);

            presetsFilesScroll = GUILayout.BeginScrollView(presetsFilesScroll, false, true, GUILayout.ExpandHeight(true));

            var fileListCopy = new List<string>(fileList);
            foreach (string file in fileListCopy)
            {
                if (!File.Exists(file)) continue;

                string _fileName = Path.GetFileNameWithoutExtension(file);
                if (_fileName.IndexOf(presetsSearch, StringComparison.CurrentCultureIgnoreCase) != -1)
                {
                    GUILayout.BeginHorizontal();
                    GUI.color = defColor;

                    if (showMixPresets)
                    {
                        // PRESET MIX A
                        GUI.color = Color.green;
                        if (GUILayout.Button("A", GUILayout.ExpandWidth(false)))
                        {
                            if (File.Exists(file))
                            {
                                presetA = new KeyValuePair<string, SceneData>(_fileName, LoadSceneData(file));
                                if (autoUpdate && presetA.Value != null && presetB.Value != null)
                                    UpdateCurrentPreset();
                            }
                        }

                        // PRESET MIX B
                        GUI.color = Color.yellow;
                        if (GUILayout.Button("B", GUILayout.ExpandWidth(false)))
                        {
                            if (File.Exists(file))
                            {
                                presetB = new KeyValuePair<string, SceneData>(_fileName, LoadSceneData(file));
                                if (autoUpdate && presetA.Value != null && presetB.Value != null)
                                    UpdateCurrentPreset();
                            }
                        }
                        GUI.color = defColor;
                    }

                    isSelecting = false;

                    // LOAD FILE
                    if (saveName == _fileName)
                    {
                        GUI.color = Color.cyan;
                        isSelecting = true;
                    }
                    if (GUILayout.Button(_fileName))
                    {
                        LoadSceneParam(file);
                        saveName = _fileName;
                    }

                    // DELETE FILE
                    GUI.color = Color.red;
                    if (GUILayout.Button("X", GUILayout.ExpandWidth(false)))
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            ReloadFilesList();
                        }
                    }

                    GUI.color = defColor;
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.EndScrollView();

            GUILayout.EndVertical();

            GUILayout.Space(15f);

            if (GUILayout.Button("Open presets folder"))
            {
                if (!Directory.Exists(folder_path))
                    Directory.CreateDirectory(folder_path);

                if (!saveName.IsNullOrEmpty())
                {
                    string filePath = Path.Combine(folder_path, saveName + ".json");
                    if (File.Exists(filePath))
                    {
                        KK_Plugins.CC.OpenFileInExplorer(filePath);
                        return;
                    }
                }

                KK_Plugins.CC.OpenFileInExplorer(folder_path);
            }

            if (GUILayout.Button(showMixPresets ? "Hide Mix Presets" : "Show Mix Presets"))
            {
                showMixPresets = !showMixPresets;
                windowRect.width = showMixPresets ? windowRect.width * 2f : windowRect.width * 0.5f;
            }

            GUILayout.EndVertical();

            if (showMixPresets)
            {
                GUILayout.BeginVertical(GUI.skin.box);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Mix Presets: ");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginVertical(GUI.skin.box);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUI.color = Color.green;
                //GUILayout.Label("Preset A: " + presetA.Key ?? "None");
                GUI.enabled = presetA.Value != null;
                if (GUILayout.Button("Preset A: " + presetA.Key ?? "None"))
                {
                    if (presetA.Value != null)
                    {
                        SceneParam._sceneData = presetA.Value;
                        SceneParam._sceneData.saved = true;

                        saveName = presetA.Key;

                        if (Studio.Studio.Instance)
                        {
                            SceneParam.SetSceneInfoValues(Studio.Studio.Instance.sceneInfo);
                            Studio.Studio.Instance.systemButtonCtrl.UpdateInfo();
                        }
                    }
                }
                GUI.color = defColor;
                GUILayout.FlexibleSpace();
                GUILayout.Label("◄►");
                GUILayout.FlexibleSpace();
                GUI.color = Color.yellow;
                //GUILayout.Label("Preset B: " + presetB.Key ?? "None");
                GUI.enabled = presetB.Value != null;
                if (GUILayout.Button("Preset B: " + presetB.Key ?? "None"))
                {
                    if (presetB.Value != null)
                    {
                        SceneParam._sceneData = presetB.Value;
                        SceneParam._sceneData.saved = true;
                        saveName = presetB.Key;
                        if (Studio.Studio.Instance)
                        {
                            SceneParam.SetSceneInfoValues(Studio.Studio.Instance.sceneInfo);
                            Studio.Studio.Instance.systemButtonCtrl.UpdateInfo();
                        }
                    }
                }
                GUI.color = defColor;
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                // MIX PRESETS A - B

                GUILayout.Space(20f);

                GUI.enabled = !autoUpdate && presetA.Value != null && presetB.Value != null;
                if (GUILayout.Button("Mix Presets"))
                {
                    UpdateCurrentPreset();
                }

                GUI.enabled = true;
                IMGUIExtensions.BoolValue("Automatically Update", autoUpdate, (val) =>
                {
                    autoUpdate = val;
                    if (autoUpdate && presetA.Value != null && presetB.Value != null)
                        UpdateCurrentPreset();
                });

                GUILayout.Space(20f);

                GUI.enabled = presetA.Value != null && presetB.Value != null;

                IMGUIExtensions.FloatValue("Mix Strength", currentSceneData_mixStrength, 0f, 100f, "", (val) =>
                {
                    currentSceneData_mixStrength = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("All Sliders", currentSceneData_allSliders, 0f, 100f, "", (val) =>
                {
                    currentSceneData_allSliders = val;
                    SetMixValues(currentSceneData_allSliders);
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                GUILayout.EndVertical();

                GUILayout.Space(10f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Scene Parameters: ");
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.Space(5f);

                GUILayout.BeginVertical(GUI.skin.box);

                GUI.enabled = true;
                mixPresetsScroll = GUILayout.BeginScrollView(mixPresetsScroll, false, true, GUILayout.ExpandHeight(true));

                GUI.enabled = presetA.Value != null && presetB.Value != null;
                IMGUIExtensions.FloatValue("LUT 1", currentSceneData_aceNo, 0f, 100f, "", (val) =>
                {
                    currentSceneData_aceNo = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("LUT 2", currentSceneData_ace2No, 0f, 100f, "", (val) =>
                {
                    currentSceneData_ace2No = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("LUT Blend", currentSceneData_aceBlend, 0f, 100f, "", (val) =>
                {
                    currentSceneData_aceBlend = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Enable AOE", currentSceneData_enableAOE, 0f, 100f, "", (val) =>
                {
                    currentSceneData_enableAOE = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("AOE Color", currentSceneData_aoeColor, 0f, 100f, "", (val) =>
                {
                    currentSceneData_aoeColor = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("AOE Radius", currentSceneData_aoeRadius, 0f, 100f, "", (val) =>
                {
                    currentSceneData_aoeRadius = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Enable Bloom", currentSceneData_enableBloom, 0f, 100f, "", (val) =>
                {
                    currentSceneData_enableBloom = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Bloom Intensity", currentSceneData_bloomIntensity, 0f, 100f, "", (val) =>
                {
                    currentSceneData_bloomIntensity = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Bloom Threshold", currentSceneData_bloomThreshold, 0f, 100f, "", (val) =>
                {
                    currentSceneData_bloomThreshold = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Bloom Blur", currentSceneData_bloomBlur, 0f, 100f, "", (val) =>
                {
                    currentSceneData_bloomBlur = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Enable Depth", currentSceneData_enableDepth, 0f, 100f, "", (val) =>
                {
                    currentSceneData_enableDepth = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Depth Focal Size", currentSceneData_depthFocalSize, 0f, 100f, "", (val) =>
                {
                    currentSceneData_depthFocalSize = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Depth Aperture", currentSceneData_depthAperture, 0f, 100f, "", (val) =>
                {
                    currentSceneData_depthAperture = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Enable Vignette", currentSceneData_enableVignette, 0f, 100f, "", (val) =>
                {
                    currentSceneData_enableVignette = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Enable Fog", currentSceneData_enableFog, 0f, 100f, "", (val) =>
                {
                    currentSceneData_enableFog = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Fog Color", currentSceneData_fogColor, 0f, 100f, "", (val) =>
                {
                    currentSceneData_fogColor = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Fog Height", currentSceneData_fogHeight, 0f, 100f, "", (val) =>
                {
                    currentSceneData_fogHeight = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Fog Start Distance", currentSceneData_fogStartDistance, 0f, 100f, "", (val) =>
                {
                    currentSceneData_fogStartDistance = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Enable Sun Shafts", currentSceneData_enableSunShafts, 0f, 100f, "", (val) =>
                {
                    currentSceneData_enableSunShafts = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Sun Threshold Color", currentSceneData_sunThresholdColor, 0f, 100f, "", (val) =>
                {
                    currentSceneData_sunThresholdColor = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Sun Color", currentSceneData_sunColor, 0f, 100f, "", (val) =>
                {
                    currentSceneData_sunColor = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Enable Shadow", currentSceneData_enableShadow, 0f, 100f, "", (val) =>
                {
                    currentSceneData_enableShadow = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Line Color", currentSceneData_lineColorG, 0f, 100f, "", (val) =>
                {
                    currentSceneData_lineColorG = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Ambient Shadow", currentSceneData_ambientShadow, 0f, 100f, "", (val) =>
                {
                    currentSceneData_ambientShadow = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Line Width", currentSceneData_lineWidthG, 0f, 100f, "", (val) =>
                {
                    currentSceneData_lineWidthG = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Ramp", currentSceneData_rampG, 0f, 100f, "", (val) =>
                {
                    currentSceneData_rampG = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                IMGUIExtensions.FloatValue("Ambient Shadow", currentSceneData_ambientShadowG, 0f, 100f, "", (val) =>
                {
                    currentSceneData_ambientShadowG = val;
                    if (autoUpdate) UpdateCurrentPreset();
                }, true);

                GUILayout.EndScrollView();

                GUILayout.EndVertical();

                GUI.enabled = true;
                GUI.color = defColor;

                GUILayout.FlexibleSpace();

                GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();

            windowRect = IMGUIUtils.DragResizeEatWindow(_uniqueId, windowRect);
        }

        public void SetMixValues(float value)
        {
            currentSceneData_aceNo = value;
            currentSceneData_ace2No = value;
            currentSceneData_aceBlend = value;
            currentSceneData_enableAOE = value;
            currentSceneData_aoeColor = value;
            currentSceneData_aoeRadius = value;
            currentSceneData_enableBloom = value;
            currentSceneData_bloomIntensity = value;
            currentSceneData_bloomThreshold = value;
            currentSceneData_bloomBlur = value;
            currentSceneData_enableDepth = value;
            currentSceneData_depthFocalSize = value;
            currentSceneData_depthAperture = value;
            currentSceneData_enableVignette = value;
            currentSceneData_enableFog = value;
            currentSceneData_fogColor = value;
            currentSceneData_fogHeight = value;
            currentSceneData_fogStartDistance = value;
            currentSceneData_enableSunShafts = value;
            currentSceneData_sunThresholdColor = value;
            currentSceneData_sunColor = value;
            currentSceneData_enableShadow = value;
            currentSceneData_lineColorG = value;
            currentSceneData_ambientShadow = value;
            currentSceneData_lineWidthG = value;
            currentSceneData_rampG = value;
            currentSceneData_ambientShadowG = value;
        }

        public void UpdateCurrentPreset()
        {
            float mixStrength = currentSceneData_mixStrength / 100;

            SceneData presetAData = presetA.Value;
            SceneData presetBData = presetB.Value;
            currentSceneData = new SceneData
            {
                // Int properties are determined by the lerp factor (under 50 use presetAData, otherwise presetBData)
                aceNo = currentSceneData_aceNo * mixStrength > 50f ? presetBData.aceNo : presetAData.aceNo,
                ace2No = currentSceneData_ace2No * mixStrength > 50f ? presetBData.ace2No : presetAData.ace2No,
                rampG = currentSceneData_rampG * mixStrength > 50f ? presetBData.rampG : presetAData.rampG,

                // String properties are determined by the lerp factor (under 50 use presetAData, otherwise presetBData)
                aceNo_GUID = currentSceneData_aceNo * mixStrength > 50f ? presetBData.aceNo_GUID : presetAData.aceNo_GUID,
                ace2No_GUID = currentSceneData_ace2No * mixStrength > 50f ? presetBData.ace2No_GUID : presetAData.ace2No_GUID,
                rampG_GUID = currentSceneData_rampG * mixStrength > 50f ? presetBData.rampG_GUID : presetAData.rampG_GUID,

                // Float properties are linearly interpolated and scaled by mixStrength
                aceBlend = Mathf.Lerp(presetAData.aceBlend, presetBData.aceBlend, currentSceneData_aceBlend / 100) * mixStrength,
                aoeRadius = Mathf.Lerp(presetAData.aoeRadius, presetBData.aoeRadius, currentSceneData_aoeRadius / 100) * mixStrength,
                bloomIntensity = Mathf.Lerp(presetAData.bloomIntensity, presetBData.bloomIntensity, currentSceneData_bloomIntensity / 100) * mixStrength,
                bloomThreshold = Mathf.Lerp(presetAData.bloomThreshold, presetBData.bloomThreshold, currentSceneData_bloomThreshold / 100) * mixStrength,
                bloomBlur = Mathf.Lerp(presetAData.bloomBlur, presetBData.bloomBlur, currentSceneData_bloomBlur / 100) * mixStrength,
                depthFocalSize = Mathf.Lerp(presetAData.depthFocalSize, presetBData.depthFocalSize, currentSceneData_depthFocalSize / 100) * mixStrength,
                depthAperture = Mathf.Lerp(presetAData.depthAperture, presetBData.depthAperture, currentSceneData_depthAperture / 100) * mixStrength,
                fogHeight = Mathf.Lerp(presetAData.fogHeight, presetBData.fogHeight, currentSceneData_fogHeight / 100) * mixStrength,
                fogStartDistance = Mathf.Lerp(presetAData.fogStartDistance, presetBData.fogStartDistance, currentSceneData_fogStartDistance / 100) * mixStrength,
                lineColorG = Mathf.Lerp(presetAData.lineColorG, presetBData.lineColorG, currentSceneData_lineColorG / 100) * mixStrength,
                lineWidthG = Mathf.Lerp(presetAData.lineWidthG, presetBData.lineWidthG, currentSceneData_lineWidthG / 100) * mixStrength,
                ambientShadowG = Mathf.Lerp(presetAData.ambientShadowG, presetBData.ambientShadowG, currentSceneData_ambientShadowG / 100) * mixStrength,

                // Bool properties are determined by the lerp factor (under 50 false, otherwise true)
                enableAOE = currentSceneData_enableAOE * mixStrength > 50f ? presetBData.enableAOE : presetAData.enableAOE,
                enableBloom = currentSceneData_enableBloom * mixStrength > 50f ? presetBData.enableBloom : presetAData.enableBloom,
                enableDepth = currentSceneData_enableDepth * mixStrength > 50f ? presetBData.enableDepth : presetAData.enableDepth,
                enableVignette = currentSceneData_enableVignette * mixStrength > 50f ? presetBData.enableVignette : presetAData.enableVignette,
                enableFog = currentSceneData_enableFog * mixStrength > 50f ? presetBData.enableFog : presetAData.enableFog,
                enableSunShafts = currentSceneData_enableSunShafts * mixStrength > 50f ? presetBData.enableSunShafts : presetAData.enableSunShafts,
                enableShadow = currentSceneData_enableShadow * mixStrength > 50f ? presetBData.enableShadow : presetAData.enableShadow,

                // Color properties are linearly interpolated and scaled by mixStrength
                aoeColor = Color.Lerp(presetAData.aoeColor, presetBData.aoeColor, currentSceneData_aoeColor / 100) * mixStrength,
                fogColor = Color.Lerp(presetAData.fogColor, presetBData.fogColor, currentSceneData_fogColor / 100) * mixStrength,
                sunThresholdColor = Color.Lerp(presetAData.sunThresholdColor, presetBData.sunThresholdColor, currentSceneData_sunThresholdColor / 100) * mixStrength,
                sunColor = Color.Lerp(presetAData.sunColor, presetBData.sunColor, currentSceneData_sunColor / 100) * mixStrength,
                ambientShadow = Color.Lerp(presetAData.ambientShadow, presetBData.ambientShadow, currentSceneData_ambientShadow / 100) * mixStrength
            };

            SceneParam._sceneData = currentSceneData;
            SceneParam._sceneData.saved = true;

            if (Studio.Studio.Instance)
            {
                SceneParam.SetSceneInfoValues(Studio.Studio.Instance.sceneInfo);
                Studio.Studio.Instance.systemButtonCtrl.UpdateInfo();
            }
        }

        public static void ReloadFilesList()
        {
            if (!Directory.Exists(folder_path))
                Directory.CreateDirectory(folder_path);

            string[] files = Directory.GetFiles(folder_path, "*.json");
            fileList.Clear();
            fileList = files.ToList();

            if (fileOrder == OrderBy.Name)
            {
                if (fileNameSort == Sort.Ascending)
                    fileList.Sort((path1, path2) => string.Compare(Path.GetFileNameWithoutExtension(path1), Path.GetFileNameWithoutExtension(path2)));
                else if (fileNameSort == Sort.Descending)
                    fileList.Sort((path1, path2) => string.Compare(Path.GetFileNameWithoutExtension(path2), Path.GetFileNameWithoutExtension(path1)));
            }
            else if (fileOrder == OrderBy.Date)
            {
                if (fileDateSort == Sort.Ascending)
                    fileList.Sort((x, y) => File.GetLastWriteTime(x).CompareTo(File.GetLastWriteTime(y)));
                else if (fileDateSort == Sort.Descending)
                    fileList.Sort((x, y) => File.GetLastWriteTime(y).CompareTo(File.GetLastWriteTime(x)));
            }

            var _file = Path.Combine(folder_path, "LastestScene.json");
            if (fileList.Contains(_file))
            {
                fileList.Remove(_file);
                fileList.Insert(0, _file);
            }
        }

        public static void LoadSceneParam(string filePath)
        {
            try
            {
                data = JSONSerializer.Deserialize<ParamData>(File.ReadAllText(filePath));

                SceneParam._sceneData = data.sceneParamData;
                SceneParam._sceneData.saved = true;

                if (Studio.Studio.Instance)
                {
                    Logger.LogInfo("Loading scene effects preset.");
                    SceneParam.SetSceneInfoValues(Studio.Studio.Instance.sceneInfo);
                    Studio.Studio.Instance.systemButtonCtrl.UpdateInfo();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to load scene effects from with error: " + ex);
            }
        }

        public static SceneData LoadSceneData(string filePath)
        {
            return JSONSerializer.Deserialize<ParamData>(File.ReadAllText(filePath)).sceneParamData;
        }

        public static void SaveSceneParam(string filePath)
        {
            if (!Directory.Exists(folder_path))
                Directory.CreateDirectory(folder_path);

            SceneParam.Save();
            var json = JSONSerializer.Serialize(data.GetType(), data, true);
            File.WriteAllText(filePath, json);
            ReloadFilesList();
        }

        private void OnSceneLoad(object sender, SceneLoadEventArgs e)
        {
            if (e.Operation == SceneOperationKind.Clear) return;

            saveName = "LastestScene";
            SaveSceneParam(Path.Combine(folder_path, saveName + ".json"));
        }
    }
}