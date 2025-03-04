#if CVR_CCK_EXISTS
using ABI.CCK.Components;
using ABI.CCK.Scripts;
#endif
using Fluff_Toolbox.Extensions.UnityExtensions;
using Fluff_Toolbox.Extensions.VRCExtensions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace Fluff_Toolbox.Views.ChilloutVR {
    public class SDK3ToCCKConverter : EditorWindow {

#if VRC_SDK_VRCSDK3 
        class parameterFixer {
            private VRCExpressionParameters.Parameter Parameter = null;
            private List<menuValue> allValues = new List<menuValue>();

            public parameterFixer(VRCExpressionParameters.Parameter parameter) {
                Parameter = parameter;
            }

            public VRCExpressionParameters.Parameter getVRCParameter() {
                return Parameter;
            }

            public void addParameterValue(string name, float value) {
                allValues.Add(new menuValue(name, value));
            }

            public List<menuValue> getSortedValues() {
                allValues = allValues.OrderBy(a => a.getValue()).ToList();
                if (allValues.Where(p => p.getValue() == Parameter.defaultValue).Any()) {
                    menuValue x = allValues.Where(p => p.getValue() == Parameter.defaultValue).First();
                    string name = x.getName();
                    allValues.Remove(x);
                    allValues.Insert(0, new menuValue(name, Parameter.defaultValue));
                }else {
                    allValues.Insert(0, new menuValue("None", Parameter.defaultValue));
                }

                return allValues;
            }

            public class menuValue {
                private string name;
                private float value;

                public menuValue(string name, float value) {
                    this.name = name;
                    this.value = value;
                }

                public string getName() {
                    return name;
                }

                public float getValue() {
                    return value;
                }
            }
        }

        class parameterDriverFixer {

            private List<Parameter> Parameters = new List<Parameter>();
            private Motion Animation = null;

            public parameterDriverFixer(List<VRCAvatarParameterDriver.Parameter> parameters, Motion animation) {
                foreach (VRCAvatarParameterDriver.Parameter parameter in parameters) {
                    if (parameter.type == VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set) {
                        Parameters.Add(new Parameter(parameter.name, parameter.value));
                    }
                }
                Animation = animation;
            }      

            public List<Parameter> getParameters() {
                return Parameters;
            }

            public Motion getAnimation() {
                return Animation;
            }


            public class Parameter {
                public string Name;
                public float Value;

                public Parameter(string name, float value) {
                    Name = name;
                    Value = value;
                }
            }
        }

        class AnimatorLayerControl {

            public AnimatorController Main;
            public List<LayerInfo> Layers = new List<LayerInfo>();
            public bool foldout = false;

            public AnimatorLayerControl(AnimatorController controller, bool defaultLayer) {
                Main = controller;
                if (controller != null)
                    foreach (AnimatorControllerLayer l in controller.layers) Layers.Add(new LayerInfo(l.name, defaultLayer));

            }

            public class LayerInfo {
                public string Name;
                public bool copyOver = false;

                public LayerInfo(string name, bool copy) {
                    Name = name;
                    copyOver = copy;
                }
            }
        }
#endif


#if VRC_SDK_VRCSDK3 && CVR_CCK_EXISTS
        GameObject Avatar;
        GameObject PrevAvatar;

        bool replaceOnOriginal = false;
        Vector2 uiscroll = new Vector2(0, 0);

        bool convertLipsyncVisemes = true;
        bool convertEyeNVoicePoint = true;
        bool ConvertToggles = true;
        bool FixFacialGestures = true;
        bool ImplementCCKHandGestures = true;
        bool ImplementCCKLocomotion = true;


        //bool copyLayerFromAnimatorsDropdown = false;
        AnimatorLayerControl[] animators = new AnimatorLayerControl[4] { new AnimatorLayerControl(null, false), new AnimatorLayerControl(null, false), new AnimatorLayerControl(null, false), new AnimatorLayerControl(null, false) };

        bool parametersDropDown = false;
        Vector2 parameterscroll = new Vector2(0, 0);
        List<string[]> OriginalParametersThatGotSpecialCharacters = new List<string[]>();
        List<string[]> ParametersThatGotSpecialCharacters = new List<string[]>();
#endif

        [MenuItem("Fluffs Toolbox/ChilloutVR/Convert SDK3 To CCK", false, 0)]
        private static void ShowWindow() {
            GetWindow<SDK3ToCCKConverter>("Converter");
        }




        void OnGUI() {
            this.minSize = new Vector2(430, 300);

            

#if !VRC_SDK_VRCSDK3 || !CVR_CCK_EXISTS
            makeWarning("Your missing the CCK or SDK3 in your project!\nIf you got them both imported make sure to get rid of ANY errors in your console!", MessageType.Error, delegate () { });
#else
            //run all things in here IF CCK & SDK3 are in project
            bool convertAllowed = true;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Avatar Prefab: ", EditorStyles.boldLabel, new GUILayoutOption[] { GUILayout.Width(90) });
            PrevAvatar = Avatar;
            Avatar = (GameObject)EditorGUILayout.ObjectField("", Avatar, typeof(GameObject), true);
            if (GUILayout.Button("Select From Scene", new GUILayoutOption[] { GUILayout.Width(120) }))
                selectAvatarFromScene();
            EditorGUILayout.EndHorizontal();

            if (Avatar == null) return;

            if (PrevAvatar != Avatar) {
                //reset / reload anything
                reset(Avatar);
            }

            bool nameValid = Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") == "";

            uiscroll = EditorGUILayout.BeginScrollView(uiscroll, new GUILayoutOption[] { GUILayout.ExpandHeight(false)});

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Options", EditorStyles.boldLabel, new GUILayoutOption[] { GUILayout.Width(120) });
            convertLipsyncVisemes = EditorGUILayout.Toggle("Lipsync & Visemes", convertLipsyncVisemes);
            convertEyeNVoicePoint = EditorGUILayout.Toggle("Eye & Voice Convertion", convertEyeNVoicePoint);
            ConvertToggles = EditorGUILayout.Toggle("Convert Toggles", ConvertToggles);
            FixFacialGestures = EditorGUILayout.Toggle("Fix Facial Gestures", FixFacialGestures);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Implements (extra)", EditorStyles.boldLabel, new GUILayoutOption[] { GUILayout.Width(120) });
            EditorGUILayout.LabelField("If not selected, you won't have these features in CVR.");
            ImplementCCKHandGestures = EditorGUILayout.Toggle("CVR HandGestures", ImplementCCKHandGestures);
            ImplementCCKLocomotion = EditorGUILayout.Toggle("CVR Locomotion", ImplementCCKLocomotion);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Advanced Settings", EditorStyles.boldLabel, new GUILayoutOption[] { GUILayout.Width(120) });
            replaceOnOriginal = EditorGUILayout.Toggle("Replace on Original", replaceOnOriginal);

            parametersDropDown = EditorGUILayout.BeginFoldoutHeaderGroup(parametersDropDown, "Parameters");
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (parametersDropDown) {
                var style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
                EditorGUILayout.LabelField("Parameter names need to contain atleast 3 characters from: a-z / A-Z / 0-9!");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Original Name", new GUILayoutOption[] { GUILayout.MinWidth(150), GUILayout.ExpandWidth(true) });
                EditorGUILayout.LabelField("Gets Replaced To", new GUILayoutOption[] { GUILayout.MinWidth(150), GUILayout.ExpandWidth(true) });
                EditorGUILayout.LabelField("Allowed", style, new GUILayoutOption[] { GUILayout.MinWidth(55), GUILayout.MaxWidth(55) });
                EditorGUILayout.EndHorizontal();

                parameterscroll = EditorGUILayout.BeginScrollView(parameterscroll, new GUILayoutOption[] { GUILayout.MinHeight(150), GUILayout.MaxHeight(400) });
                foreach (string[] para in ParametersThatGotSpecialCharacters) {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.BeginDisabledGroup(true);
                    EditorGUILayout.TextField(para[0], new GUILayoutOption[] { GUILayout.ExpandWidth(true) });
                    EditorGUI.EndDisabledGroup();
                    para[1] = EditorGUILayout.TextField(para[1], new GUILayoutOption[] { GUILayout.ExpandWidth(true) });

                    bool allowed = Regex.Replace(para[1].Trim(), "[^a-zA-Z0-9]", "") == "" || Regex.Replace(para[1].Trim(), "[^a-zA-Z0-9]", "").Length < 3;
                    style.normal.textColor = allowed ? Color.red : Color.green;

                    EditorGUILayout.LabelField(allowed ? "✖" : "✔", style, new GUILayoutOption[] { GUILayout.MinWidth(55), GUILayout.MaxWidth(55) });
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();

                EditorGUI.BeginDisabledGroup(ParametersThatGotSpecialCharacters.Where(p => Regex.Replace(p[1].Trim(), "[^a-zA-Z0-9]", "") == "" || Regex.Replace(p[1].Trim(), "[^a-zA-Z0-9]", "").Length < 3).Any() || nameValid);
                if (GUILayout.Button("Rename Parameters"))
                    fixVRCParameters(Avatar, ParametersThatGotSpecialCharacters);

                EditorGUI.EndDisabledGroup();
                EditorGUILayout.Space();
            }

            /*
            copyLayerFromAnimatorsDropdown = EditorGUILayout.BeginFoldoutHeaderGroup(copyLayerFromAnimatorsDropdown, "Merge Animator Layers");
            EditorGUILayout.EndFoldoutHeaderGroup();

            if (copyLayerFromAnimatorsDropdown) {
                //VRCAvatarDescriptor VRCDescriptor = Avatar.GetComponent<VRCAvatarDescriptor>();

                //if (GUILayout.Button("Copy Test"))
                //    copyOverLayerFromAnimatorToAnimator((AnimatorController) VRCDescriptor.baseAnimationLayers[4].animatorController, (AnimatorController) VRCDescriptor.baseAnimationLayers[3].animatorController, );


                EditorGUILayout.BeginVertical(new GUIStyle() { padding = new RectOffset(20, 0, 0, 0) });
                EditorGUILayout.LabelField("Select other layers that will be merge with the FX!");
                EditorGUILayout.LabelField("Keep in mind Locomotion Layers will probably not work!");
                for (int i = 0; i < animators.Length; i++) {
                    EditorGUI.BeginDisabledGroup(animators[i].Main == null);
                    animators[i].foldout = EditorGUILayout.BeginFoldoutHeaderGroup((animators[i].Main == null ? false : animators[i].foldout), (animators[i].Main == null ? "No " : "") + (i == 0 ? "Base" : i == 1 ? "Additive" : i == 2 ? "Gesture" : i == 3 ? "Action" : "FX") + (animators[i].Main == null ? " Animator Found" : ""));
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    EditorGUI.EndDisabledGroup();

                    if (animators[i].foldout) {
                        for (int j = 0; j < animators[i].Layers.Count(); j++) {
                            EditorGUILayout.BeginHorizontal(new GUIStyle() { padding = new RectOffset(20, 0, 0, 0) });
                            animators[i].Layers[j].copyOver = EditorGUILayout.Toggle(animators[i].Layers[j].copyOver, new GUILayoutOption[] { GUILayout.Width(20) });
                            EditorGUILayout.LabelField(animators[i].Layers[j].Name);
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.Space();
                    }
                }
                EditorGUILayout.EndVertical();


            }
            */





            //EditorGUILayout.Space();
            if (nameValid) {
                makeWarning("Please change your avatar name in the hierarchy to not contain any of the follow characters /?<>\\:*|\"", MessageType.Error, delegate { });
                convertAllowed = false;
            }

            if (OriginalParametersThatGotSpecialCharacters.Where(p => Regex.Replace(p[1].Trim(), "[^a-zA-Z0-9]", "") == "" || Regex.Replace(p[1].Trim(), "[^a-zA-Z0-9]", "").Length < 3).Any()) {
                makeWarning("You got parameters that don't fit the requirements for CVR Conversion!\nPlease change them in the Advanced Settings > Parameters accordingly! ", MessageType.Error, delegate { });
                convertAllowed = false;
            }

            if (EditorUserBuildSettings.activeBuildTarget.ToString() != "StandaloneWindows64") {
                makeWarning("Your unity project buildsettings is on " + EditorUserBuildSettings.activeBuildTarget.ToString() + ", please change your buildsettings to PC, Mac & Linux Standalone!", MessageType.Error, delegate { });
                convertAllowed = false;
            }

            EditorGUILayout.EndScrollView();

            EditorGUI.BeginDisabledGroup(!convertAllowed);
            if (GUILayout.Button("Convert"))
                convert(Avatar, replaceOnOriginal);

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            makeWarning("It's recommended to convert first Physicsbones to Dynamic bones!\nThis isn't a converter for Physicsbones!\nDownload my Physicsbones converter on my gumroad page.https://fluffs.gumroad.com/", MessageType.Info, delegate { });
            makeWarning("Converter will convert the following:\nToggle, Buttons, Radial Puppets & Joystick2D.\n\nImplements:\nCVR Locomotion, Emotes & Hand gestures.\n\nConverting to CVR will mean parameter names & order might change. This converter makes a duplicate of your original Animator and edits that one!", MessageType.Info, delegate { });
            makeWarning("Converting big animators might cause unity to freeze!", MessageType.Warning, delegate { });
            makeWarning("Parameter Drivers, Physicsbones, senders & receivers aren't convert able or not included yet!", MessageType.Error, delegate { });

#endif
        }

#if VRC_SDK_VRCSDK3 && CVR_CCK_EXISTS
        public void reset(GameObject avi) {
            ParametersThatGotSpecialCharacters.Clear();
            OriginalParametersThatGotSpecialCharacters.Clear();

            VRCAvatarDescriptor VRCDescriptor = avi.GetComponent<VRCAvatarDescriptor>();
            if (VRCDescriptor == null) return;

            if (VRCDescriptor.expressionParameters == null) return;

            foreach (VRCExpressionParameters.Parameter para in VRCDescriptor.expressionParameters.parameters) {
                ParametersThatGotSpecialCharacters.Add(new string[2] { para.name, para.name });
                OriginalParametersThatGotSpecialCharacters.Add(new string[2] { para.name, para.name });
            }

            animators[0] = new AnimatorLayerControl(VRCDescriptor.baseAnimationLayers[0].animatorController == null ? null : (AnimatorController) VRCDescriptor.baseAnimationLayers[0].animatorController, false);
            animators[1] = new AnimatorLayerControl(VRCDescriptor.baseAnimationLayers[1].animatorController == null ? null : (AnimatorController) VRCDescriptor.baseAnimationLayers[1].animatorController, false);
            animators[2] = new AnimatorLayerControl(VRCDescriptor.baseAnimationLayers[2].animatorController == null ? null : (AnimatorController) VRCDescriptor.baseAnimationLayers[2].animatorController, true);
            animators[3] = new AnimatorLayerControl(VRCDescriptor.baseAnimationLayers[3].animatorController == null ? null : (AnimatorController) VRCDescriptor.baseAnimationLayers[3].animatorController, true);
            //animators[4] = new AnimatorLayerControl(VRCDescriptor.baseAnimationLayers[4].animatorController == null ? null : (AnimatorController)VRCDescriptor.baseAnimationLayers[4].animatorController, true);


        }


        public void copyOverLayerFromAnimatorToAnimator(AnimatorController copyTo, AnimatorController copyFrom, bool[] copyFromLayer) {
            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(copyFrom), "Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/Temp_Copy.controller");
            AnimatorController AnimatorFX = (AnimatorController)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/Temp_Copy.controller", typeof(AnimatorController));

            for (int i = 0; i < AnimatorFX.parameters.Length; i++) {
                if (!copyTo.ExistParameter(AnimatorFX.parameters[i].name))
                    copyTo.AddParameter(AnimatorFX.parameters[i].name, AnimatorFX.parameters[i].type);
            }

            for (int i = 0; i < AnimatorFX.layers.Length; i++) {
                if (copyFromLayer[i]) {
                    copyTo.AddLayer(AnimatorFX.layers[i]);

                    var layers = copyTo.layers;
                    if (i == 0) layers[copyTo.layers.Length - 1].defaultWeight = 1f;
                    copyTo.layers = layers;
                }
            }

            EditorUtility.SetDirty(copyTo);
            DestroyImmediate(AnimatorFX, true);

            /*for(int i = 0; i < copyFrom.layers.Length; i++)  {
                AnimatorControllerLayer layer = copyFrom.layers[i];
                copyTo.AddLayer(layer.name);
                var layers = copyTo.layers;
                layers[copyTo.layers.Length - 1].cloneLayer(layer);
                if (i == 0) layers[copyTo.layers.Length - 1].defaultWeight = 1f;
                copyTo.layers = layers;

            }*/
        }


        public void fixVRCParameters(GameObject avi, List<string[]> parameters) {
            GameObject duplicate = avi;
            if (!replaceOnOriginal) {
                duplicate = Instantiate(avi);
                duplicate.name = avi.name + " VRC Renamed Parameters";
            }

            parameters.RemoveAll(p => p[0] == p[1]);

            VRCAvatarDescriptor VRCDescriptor = duplicate.GetComponent<VRCAvatarDescriptor>();
            if (VRCDescriptor == null) return;

            if (!AssetDatabase.IsValidFolder($"Assets/Avatar"))
                AssetDatabase.CreateFolder("Assets", "Avatar");

            if (!AssetDatabase.IsValidFolder($"Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "")))
                AssetDatabase.CreateFolder("Assets/Avatar", Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", ""));

            if (!AssetDatabase.IsValidFolder($"Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation"))
                AssetDatabase.CreateFolder("Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", ""), "Animation");

            if (!AssetDatabase.IsValidFolder($"Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Menu"))
                AssetDatabase.CreateFolder("Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", ""), "Menu");



            //fixes
            if (VRCDescriptor.expressionParameters != null) {
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(VRCDescriptor.expressionParameters), "Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Menu/VRC_Parameters_ParameterFix.asset");
                VRCExpressionParameters ParameterDuplicate = (VRCExpressionParameters)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Menu/VRC_Parameters_ParameterFix.asset", typeof(VRCExpressionParameters));
                VRCDescriptor.expressionParameters = ParameterDuplicate;

                VRCExpressionParameters.Parameter[] exprparas = VRCDescriptor.expressionParameters.parameters;

                foreach (VRCExpressionParameters.Parameter para in exprparas)
                    foreach (string[] gothrough in parameters)
                        if (gothrough[0] == para.name) {
                            para.name = gothrough[1];
                            continue;
                        }

                VRCDescriptor.expressionParameters.parameters = exprparas;

                EditorUtility.SetDirty(ParameterDuplicate);
            }

            
            //go over every menu/submenu and make a clone of it, fix it only in those clones.
            if (VRCDescriptor.expressionsMenu != null) {

                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(VRCDescriptor.expressionsMenu), "Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Menu/" + VRCDescriptor.expressionsMenu.name + "_ParameterFix.asset");
                VRCExpressionsMenu vrcmenu = (VRCExpressionsMenu)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Menu/" + VRCDescriptor.expressionsMenu.name + "_ParameterFix.asset", typeof(VRCExpressionsMenu));
                VRCDescriptor.expressionsMenu = vrcmenu;

                Action<VRCExpressionsMenu> goThroughAllMenus = null;
                goThroughAllMenus = delegate (VRCExpressionsMenu menu) {
                    foreach (VRCExpressionsMenu.Control controller in menu.controls) {
                        if (controller.parameter.name != "")
                            foreach (string[] gothrough in parameters)
                                if (gothrough[0] == controller.parameter.name) {
                                    controller.parameter.name = gothrough[1];
                                    continue;
                                }

                        foreach (VRCExpressionsMenu.Control.Parameter p in controller.subParameters) {
                            if (p.name == "") continue;
                            foreach (string[] gothrough in parameters)
                                if (gothrough[0] == p.name) {
                                    p.name = gothrough[1];
                                    continue;
                                }
                        }

                        if (controller.type == VRCExpressionsMenu.Control.ControlType.SubMenu && controller.subMenu != null) {
                            AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(controller.subMenu), "Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Menu/" + controller.subMenu.name + "_ParameterFix.asset");
                            VRCExpressionsMenu duplicateSubmenu = (VRCExpressionsMenu)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Menu/" + controller.subMenu.name + "_ParameterFix.asset", typeof(VRCExpressionsMenu));
                            controller.subMenu = duplicateSubmenu;
                            goThroughAllMenus(controller.subMenu);
                            EditorUtility.SetDirty(duplicateSubmenu);
                        }

                        EditorUtility.SetDirty(menu);
                    }
                };

                goThroughAllMenus(vrcmenu);


            }


            //get FX and make duplicate of that ->
            if (VRCDescriptor.baseAnimationLayers.Length == 5 && VRCDescriptor.baseAnimationLayers[4].animatorController != null) {
                //clone FX layer.
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(VRCDescriptor.baseAnimationLayers[4].animatorController), "Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/VRC_FX_ParameterFix.controller");
                AnimatorController AnimatorFX = (AnimatorController)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/VRC_FX_ParameterFix.controller", typeof(AnimatorController));

                AnimatorFX.RenameParameters(parameters);

                VRCDescriptor.baseAnimationLayers[4].animatorController = AnimatorFX;

                EditorUtility.SetDirty(AnimatorFX);
            }

            EditorUtility.SetDirty(VRCDescriptor);

            PrevAvatar = avi;
            Avatar = duplicate;
            reset(duplicate);
        }



        public void convert(GameObject gameObject, bool replaceOnOriginal) {
            GameObject duplicate = gameObject;
            if (!replaceOnOriginal) {
                duplicate = Instantiate(gameObject);
                duplicate.name = gameObject.name + " CCK";
            }

            

            VRCAvatarDescriptor VRCDescriptor = duplicate.GetComponent<VRCAvatarDescriptor>();
            CVRAvatar CCKDescriptor = duplicate.GetOrAddComponent<CVRAvatar>();

            duplicate.GetComponent<Animator>().runtimeAnimatorController = null; //reset animator to nothing.

            if (VRCDescriptor == null) return;

            if (convertLipsyncVisemes) {
                if (VRCDescriptor.customEyeLookSettings.eyelidType == VRCAvatarDescriptor.EyelidType.Blendshapes && VRCDescriptor.customEyeLookSettings.eyelidsBlendshapes[0] != -1 & VRCDescriptor.customEyeLookSettings.eyelidsSkinnedMesh != null) {
                    CCKDescriptor.useBlinkBlendshapes = true;
                    CCKDescriptor.blinkBlendshape.SetValue(VRCDescriptor.customEyeLookSettings.eyelidsSkinnedMesh.sharedMesh.GetBlendShapeName(VRCDescriptor.customEyeLookSettings.eyelidsBlendshapes[0]), 0);
                    CCKDescriptor.bodyMesh = VRCDescriptor.customEyeLookSettings.eyelidsSkinnedMesh;


                }

                if (VRCDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.VisemeBlendShape && VRCDescriptor.VisemeSkinnedMesh != null) {
                    CCKDescriptor.useVisemeLipsync = true;
                    CCKDescriptor.visemeMode = CVRAvatar.CVRAvatarVisemeMode.Visemes;
                    CCKDescriptor.bodyMesh = VRCDescriptor.VisemeSkinnedMesh;
                    CCKDescriptor.visemeBlendshapes = VRCDescriptor.VisemeBlendShapes;
                } else if (VRCDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBone) {
                    CCKDescriptor.useVisemeLipsync = true;
                    CCKDescriptor.visemeMode = CVRAvatar.CVRAvatarVisemeMode.JawBone;
                } else if (VRCDescriptor.lipSync == VRC.SDKBase.VRC_AvatarDescriptor.LipSyncStyle.JawFlapBlendShape) {
                    CCKDescriptor.useVisemeLipsync = true;
                    CCKDescriptor.visemeMode = CVRAvatar.CVRAvatarVisemeMode.SingleBlendshape;
                    CCKDescriptor.bodyMesh = VRCDescriptor.VisemeSkinnedMesh;
                    CCKDescriptor.visemeBlendshapes.SetValue(VRCDescriptor.MouthOpenBlendShapeName, 0);
                }



            }

            if (convertEyeNVoicePoint) {
                CCKDescriptor.useEyeMovement = VRCDescriptor.enableEyeLook;

                CCKDescriptor.viewPosition = VRCDescriptor.ViewPosition;
                CCKDescriptor.voicePosition = new Vector3(VRCDescriptor.ViewPosition.x, (VRCDescriptor.ViewPosition.y * 0.963f), VRCDescriptor.ViewPosition.z);
            }


            if (VRCDescriptor.baseAnimationLayers.Length == 5 && VRCDescriptor.baseAnimationLayers[4].animatorController != null) {

                if (!AssetDatabase.IsValidFolder($"Assets/Avatar"))
                    AssetDatabase.CreateFolder("Assets", "Avatar");

                if (!AssetDatabase.IsValidFolder($"Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "")))
                    AssetDatabase.CreateFolder("Assets/Avatar",Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", ""));

                if (!AssetDatabase.IsValidFolder($"Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation"))
                    AssetDatabase.CreateFolder("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", ""), "Animation");

                if (!AssetDatabase.IsValidFolder($"Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK"))
                    AssetDatabase.CreateFolder("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation", "CCK");

                //clone FX layer.
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(VRCDescriptor.baseAnimationLayers[4].animatorController), "Assets/Avatar/" + Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK_FX.controller");
                AnimatorController AnimatorFX = (AnimatorController)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK_FX.controller", typeof(AnimatorController));

                //add custom layers to FX layer
                /*
                foreach (AnimatorLayerControl animatorsOfAvatar in animators) {
                    if (animatorsOfAvatar != null && animatorsOfAvatar.Main != null) {
                        //if null animator didn't got set correctly!
                        for (int i = 0; i < animatorsOfAvatar.Main.parameters.Length; i++) {
                            if (!AnimatorFX.ExistParameter(animatorsOfAvatar.Main.parameters[i].name))
                                AnimatorFX.AddParameter(animatorsOfAvatar.Main.parameters[i].name, animatorsOfAvatar.Main.parameters[i].type);
                        }

                        for (int i = 0; i < animatorsOfAvatar.Main.layers.Length; i++) {
                            if (animatorsOfAvatar.Layers[i].copyOver) {
                                AnimatorControllerLayer layer = animatorsOfAvatar.Main.layers[i];
                                AnimatorFX.AddLayer(layer.name);
                                var layers2 = AnimatorFX.layers;
                                layers2[AnimatorFX.layers.Length - 1].cloneLayer(layer);
                                if (i == 0) layers2[AnimatorFX.layers.Length - 1].defaultWeight = 1f;
                                AnimatorFX.layers = layers2;
                            }
                        }
                    }
                }*/


                Action<ChildAnimatorStateMachine[], Action<ChildAnimatorState>> goLayerDeep = null;
                goLayerDeep = delegate (ChildAnimatorStateMachine[] stateMachines, Action<ChildAnimatorState> a) {
                    foreach (ChildAnimatorStateMachine casm in stateMachines) {
                        foreach (ChildAnimatorState state in casm.stateMachine.states)
                            a(state);
                    }
                };

                if (FixFacialGestures) {
                    //rename original vrc Left&Right Hand
                    if (AnimatorFX.ExistParameter("GestureLeftWeight") || AnimatorFX.ExistParameter("GestureRightWeight")) {
                        CVRParameterStream paraStream = duplicate.AddComponent<CVRParameterStream>();

                        CVRParameterStreamEntry entryL = new CVRParameterStreamEntry();
                        entryL.type = CVRParameterStreamEntry.Type.TriggerLeftValue;
                        entryL.parameterName = "GestureLeftWeight";
                        entryL.applicationType = CVRParameterStreamEntry.ApplicationType.Override;
                        paraStream.entries.Add(entryL);

                        CVRParameterStreamEntry entryR = new CVRParameterStreamEntry();
                        entryR.type = CVRParameterStreamEntry.Type.TriggerRightValue;
                        entryR.parameterName = "GestureRightWeight";
                        entryR.applicationType = CVRParameterStreamEntry.ApplicationType.Override;
                        paraStream.entries.Add(entryR);

                        EditorUtility.SetDirty(paraStream);
                    }


                    AnimatorFX.RenameParameters(new List<string[]>() { new string[2] { "GestureLeft", "VRC_GestureLeft" }, new string[2] { "GestureRight", "VRC_GestureRight" } });

                    AnimatorFX.AddParameter("GestureLeft", AnimatorControllerParameterType.Float);
                    AnimatorFX.AddParameter("GestureRight", AnimatorControllerParameterType.Float);

                    foreach (AnimatorControllerLayer cl in AnimatorFX.layers) {
                        if (cl.stateMachine == null) continue;
                        foreach (AnimatorStateTransition st in cl.stateMachine.anyStateTransitions) {

                            Action<AnimatorStateTransition> fixTransitions = null;
                            fixTransitions = delegate (AnimatorStateTransition ts) {

                                foreach (AnimatorCondition ac in ts.conditions) {
                                    if (ac.parameter.Equals("VRC_GestureLeft") || ac.parameter.Equals("VRC_GestureRight")) {
                                        string para = (ac.parameter.Equals("VRC_GestureLeft") ? "GestureLeft" : "GestureRight");
                                        switch (ac.threshold) {
                                            case 0:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, -0.1f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 0.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    //this for not equals
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    ts.AddCondition(AnimatorConditionMode.Less, -0.1f, para);


                                                    //clone the transition when finding the first parameter (ac.parameter)
                                                    //'split' the transitions in 2 and add to both the parameter but the other way one less then other greater
                                                    //afterwards it will 'add' the transitions at the end so other parameter will also be found and it will split up more
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 0.1f, para);
                                                    fixTransitions(newTransition);
                                                } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    ts.AddCondition(AnimatorConditionMode.Greater, -1.1f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, -0.9f, para);

                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }
                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 0.9f, para);
                                                    fixTransitions(newTransition);

                                                }
                                                break;
                                            case 1:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 0.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if (ac.parameter == ac2.parameter) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }
                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 1.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 0.9f, para);

                                                } else if (ac.mode == AnimatorConditionMode.Less) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, -0.1f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 0.1f, para);

                                                } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }
                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 1.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 0.9f, para);
                                                }
                                                break;
                                            case 2:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Less, -0.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Greater, -1.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }
                                                    newTransition.AddCondition(AnimatorConditionMode.Less, -1.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Greater, -0.9f, para);
                                                } else if (ac.mode == AnimatorConditionMode.Less) {
                                                    //-0.1 && < 1.1 
                                                    ts.AddCondition(AnimatorConditionMode.Greater, -0.1f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);

                                                } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.9f, para);
                                                }
                                                break;
                                            case 3:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 4.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 4.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 3.9f, para);
                                                } else if (ac.mode == AnimatorConditionMode.Less) {
                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }
                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                    newTransition.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 4.9f, para);
                                                }
                                                break;
                                            case 4:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 4.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 5.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if (ac.parameter == ac2.parameter) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 5.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 4.9f, para);

                                                } else if (ac.mode == AnimatorConditionMode.Less) {
                                                    //< 1.1 || > 3.9 && < 4.1
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState);
                                                    newTransition.cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if (ac.parameter == ac2.parameter) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 4.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                    //> 1.9 && < 3.1 || > 5.9
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts); ;
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 5.9f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                }
                                                break;
                                            case 5:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 5.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 6.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }
                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 6.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 5.9f, para);

                                                } else if (ac.mode == AnimatorConditionMode.Less) {
                                                    //< 1.1 || > 3.9 && < 5.1
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Less, 5.1f, para);
                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                    //> 1.9 && < 3.1

                                                    ts.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                }
                                                break;
                                            case 6:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 2.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }
                                                    

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 3.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 2.9f, para);
                                                } else if (ac.mode == AnimatorConditionMode.Less) {
                                                    //< 1.1 || > 3.9

                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);
                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);

                                                } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                    //1.9 && < 2.1
                                                    ts.AddCondition(AnimatorConditionMode.Less, 2.1f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                }
                                                break;
                                            case 7:
                                                if (ac.mode == AnimatorConditionMode.Equals) {
                                                    ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                    ts.AddCondition(AnimatorConditionMode.Less, 2.1f, para);
                                                } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);

                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 2.1f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.9f, para);
                                                } else if (ac.mode == AnimatorConditionMode.Less) {
                                                    //< 1.1 || > 2.9
                                                    AnimatorStateTransition newTransition = cl.stateMachine.AddAnyStateTransition(ts.destinationState).cloneFrom(ts);

                                                    foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                        if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                            newTransition.RemoveCondition(ac2); break;
                                                        }

                                                    newTransition.AddCondition(AnimatorConditionMode.Greater, 2.9f, para);
                                                    fixTransitions(newTransition);

                                                    ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);

                                                }
                                                break;
                                        }
                                        ts.RemoveCondition(ac);


                                    }
                                }
                            };

                            fixTransitions(st);
                        }


                        //LOGICAL VALUES FOR LESS OR GREATER IN GESTURE VALUES

                        //less then 0   =>         nothing
                        //bigger then 0 =>         > -1.1 && < -0.9 || > 0.9

                        //less then 1   =>         > -0.1 && < 0.1
                        //bigger then 1 =>         < 0.9 || > 1.1

                        //less then 2   =>         > -0.1 && < 1.1 
                        //bigger then 2 =>         > 1.9

                        //less then 3   =>         < 1.1
                        //bigger then 3 =>         > 1.9 && < 3.1 || > 4.9

                        //less then 4   =>         < 1.1 || > 3.9 && < 4.1
                        //bigger then 4 =>         > 1.9 && < 3.1 || > 5.9

                        //less then 5   =>         < 1.1 || > 3.9 && < 5.1
                        //bigger then 5 =>         > 1.9 && < 3.1

                        //less then 6   =>         < 1.1 || > 3.9
                        //bigger then 6 =>         > 1.9 && < 2.1

                        //less then 7   =>         < 1.1 || > 2.9
                        //bigger then 7 =>         nothing


                        Action<ChildAnimatorState> stateWork = delegate (ChildAnimatorState sm) {
                            foreach (AnimatorStateTransition st in sm.state.transitions) {
                                Action<AnimatorStateTransition> fixTransitions = null;
                                fixTransitions = delegate (AnimatorStateTransition ts) {

                                    foreach (AnimatorCondition ac in ts.conditions) {
                                        if (ac.parameter.Equals("VRC_GestureLeft") || ac.parameter.Equals("VRC_GestureRight")) {
                                            string para = (ac.parameter.Equals("VRC_GestureLeft") ? "GestureLeft" : "GestureRight");
                                            switch (ac.threshold) {
                                                case 0:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, -0.1f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 0.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        //this for not equals
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        ts.AddCondition(AnimatorConditionMode.Less, -0.1f, para);


                                                        //clone the transition when finding the first parameter (ac.parameter)
                                                        //'split' the transitions in 2 and add to both the parameter but the other way one less then other greater
                                                        //afterwards it will 'add' the transitions at the end so other parameter will also be found and it will split up more
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 0.1f, para);
                                                        fixTransitions(newTransition);
                                                    } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        ts.AddCondition(AnimatorConditionMode.Greater, -1.1f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, -0.9f, para);

                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }
                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 0.9f, para);
                                                        fixTransitions(newTransition);

                                                    }
                                                    break;
                                                case 1:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 0.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if (ac.parameter == ac2.parameter) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }
                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 1.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 0.9f, para);

                                                    } else if (ac.mode == AnimatorConditionMode.Less) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, -0.1f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 0.1f, para);

                                                    } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }
                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 1.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 0.9f, para);
                                                    }
                                                    break;
                                                case 2:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Less, -0.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Greater, -1.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }
                                                        newTransition.AddCondition(AnimatorConditionMode.Less, -1.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Greater, -0.9f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.Less) {
                                                        //-0.1 && < 1.1 
                                                        ts.AddCondition(AnimatorConditionMode.Greater, -0.1f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);

                                                    } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.9f, para);
                                                    }
                                                    break;
                                                case 3:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 4.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 4.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 3.9f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.Less) {
                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }
                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                        newTransition.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 4.9f, para);
                                                    }
                                                    break;
                                                case 4:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 4.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 5.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if (ac.parameter == ac2.parameter) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 5.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 4.9f, para);

                                                    } else if (ac.mode == AnimatorConditionMode.Less) {
                                                        //< 1.1 || > 3.9 && < 4.1
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState);
                                                        newTransition.cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if (ac.parameter == ac2.parameter) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 4.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                        //> 1.9 && < 3.1 || > 5.9
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts); ;
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 5.9f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                    }
                                                    break;
                                                case 5:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 5.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 6.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }
                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 6.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 5.9f, para);

                                                    } else if (ac.mode == AnimatorConditionMode.Less) {
                                                        //< 1.1 || > 3.9 && < 5.1
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Less, 5.1f, para);
                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                        //> 1.9 && < 3.1

                                                        ts.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                    }
                                                    break;
                                                case 6:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 2.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 3.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 3.1f, para);
                                                        fixTransitions(newTransition);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 2.9f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.Less) {
                                                        //< 1.1 || > 3.9

                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);
                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 3.9f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);

                                                    } else if (ac.mode == AnimatorConditionMode.Greater) {
                                                        //1.9 && < 2.1
                                                        ts.AddCondition(AnimatorConditionMode.Less, 2.1f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                    }
                                                    break;
                                                case 7:
                                                    if (ac.mode == AnimatorConditionMode.Equals) {
                                                        ts.AddCondition(AnimatorConditionMode.Greater, 1.9f, para);
                                                        ts.AddCondition(AnimatorConditionMode.Less, 2.1f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.NotEqual) {
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);

                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 2.1f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.9f, para);
                                                    } else if (ac.mode == AnimatorConditionMode.Less) {
                                                        //< 1.1 || > 2.9
                                                        AnimatorStateTransition newTransition = sm.state.AddTransition(ts.destinationState).cloneFrom(ts);

                                                        foreach (AnimatorCondition ac2 in newTransition.conditions)
                                                            if ((para == "GestureLeft" && ac2.parameter == "VRC_GestureLeft") || (para == "GestureRight" && ac2.parameter == "VRC_GestureRight")) {
                                                                newTransition.RemoveCondition(ac2); break;
                                                            }

                                                        newTransition.AddCondition(AnimatorConditionMode.Greater, 2.9f, para);
                                                        fixTransitions(newTransition);

                                                        ts.AddCondition(AnimatorConditionMode.Less, 1.1f, para);

                                                    }
                                                    break;
                                            }
                                            ts.RemoveCondition(ac);


                                        }
                                    }
                                };

                                fixTransitions(st);
                            }
                        };

                        goLayerDeep(cl.stateMachine.stateMachines, stateWork);

                        foreach (ChildAnimatorState sm in cl.stateMachine.states)
                            stateWork(sm);
                    }

                    //delete VRC animator parameters
                    for (int a = AnimatorFX.parameters.Length - 1; a >= 0; a--)
                        if (AnimatorFX.parameters[a].name == "VRC_GestureLeft" || AnimatorFX.parameters[a].name == "VRC_GestureRight")
                            AnimatorFX.RemoveParameter(a);

                }

                if (ConvertToggles) {

                    List<string> joystickFix = new List<string>();
                    List<parameterFixer> parametersThatsNeedsToBeReNumbered = new List<parameterFixer>();

                    CCKDescriptor.avatarUsesAdvancedSettings = true;
                    CCKDescriptor.avatarSettings = new CVRAdvancedAvatarSettings();
                    CCKDescriptor.avatarSettings.initialized = true;

                    if (VRCDescriptor.expressionsMenu != null) {
                        VRCExpressionsMenu vrcmenu = VRCDescriptor.expressionsMenu;
                        VRCExpressionParameters vrcparameters = VRCDescriptor.expressionParameters;

                        CCKDescriptor.avatarSettings.settings = new List<CVRAdvancedSettingsEntry>();

                        List<string[]> parametersToBeRanamedInBatch = new List<string[]>();

                        Action<VRCExpressionsMenu> goThroughAllMenus = null;
                        goThroughAllMenus = delegate (VRCExpressionsMenu menu) {
                            foreach (VRCExpressionsMenu.Control controller in menu.controls) {


                                if (controller.type != VRCExpressionsMenu.Control.ControlType.SubMenu) {


                                    if (controller.type == VRCExpressionsMenu.Control.ControlType.Toggle || controller.type == VRCExpressionsMenu.Control.ControlType.Button) {
                                        if (controller.parameter == null || controller.parameter.name == "") continue;
                                        VRCExpressionParameters.Parameter currentParameter = vrcparameters.parameters.getParameter(controller.parameter.name);
                                        if (currentParameter == null) continue;


                                        if (currentParameter.valueType == VRCExpressionParameters.ValueType.Bool) {

                                            CVRAdvancedSettingsEntry entry = new CVRAdvancedSettingsEntry();

                                            entry.name = currentParameter.name;
                                            entry.type = CVRAdvancedSettingsEntry.SettingsType.GameObjectToggle;

                                            CVRAdvancesAvatarSettingGameObjectToggle settings = new CVRAdvancesAvatarSettingGameObjectToggle();
                                            settings.usedType = CVRAdvancesAvatarSettingBase.ParameterType.GenerateBool;
                                            settings.defaultValue = currentParameter.defaultValue == 1f;
                                            entry.setting = settings;

                                            CCKDescriptor.avatarSettings.settings.Add(entry);

                                            parametersToBeRanamedInBatch.Add(new string[2] { currentParameter.name, Regex.Replace(currentParameter.name, "[^a-zA-Z0-9#]", "") });
                                        } else {



                                            //float or int?
                                            //restrure the official value to new value
                                            bool isFound = parametersThatsNeedsToBeReNumbered.Where(p => p.getVRCParameter() == currentParameter).Any();
                                            if (!isFound) {
                                                parameterFixer parameterFound = new parameterFixer(currentParameter);
                                                parameterFound.addParameterValue(controller.name, controller.value);
                                                parametersThatsNeedsToBeReNumbered.Add(parameterFound);


                                                parametersToBeRanamedInBatch.Add(new string[2] { currentParameter.name, Regex.Replace(currentParameter.name, "[^a-zA-Z0-9#]", "") });
                                            } else {
                                                parameterFixer parameterFound = parametersThatsNeedsToBeReNumbered.Where(p => p.getVRCParameter() == currentParameter).First();
                                                parameterFound.addParameterValue(controller.name, controller.value);
                                            }
                                        }

                                    } else if (controller.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet) {
                                        if (controller.GetSubParameter(0) == null || controller.GetSubParameter(0).name == "") continue;

                                        parametersToBeRanamedInBatch.Add(new string[2] { controller.GetSubParameter(0).name, Regex.Replace(controller.GetSubParameter(0).name, "[^a-zA-Z0-9#]", "") });

                                        //slider

                                        CVRAdvancedSettingsEntry entry = new CVRAdvancedSettingsEntry();
                                        entry.name = controller.GetSubParameter(0).name;
                                        entry.type = CVRAdvancedSettingsEntry.SettingsType.Slider;


                                        CVRAdvancesAvatarSettingSlider settings = new CVRAdvancesAvatarSettingSlider();
                                        settings.usedType = CVRAdvancesAvatarSettingBase.ParameterType.GenerateFloat;

                                        entry.setting = settings;


                                        CCKDescriptor.avatarSettings.settings.Add(entry);

                                    } else if (controller.type == VRCExpressionsMenu.Control.ControlType.TwoAxisPuppet) {
                                        if (controller.parameter.name != "") {
                                            VRCExpressionParameters.Parameter currentParameter = vrcparameters.parameters.getParameter(controller.parameter.name);
                                            if (currentParameter.valueType == VRCExpressionParameters.ValueType.Bool) {

                                                CVRAdvancedSettingsEntry entry2 = new CVRAdvancedSettingsEntry();

                                                entry2.name = currentParameter.name;
                                                entry2.type = CVRAdvancedSettingsEntry.SettingsType.GameObjectToggle;

                                                CVRAdvancesAvatarSettingGameObjectToggle settings2 = new CVRAdvancesAvatarSettingGameObjectToggle();
                                                settings2.usedType = CVRAdvancesAvatarSettingBase.ParameterType.GenerateBool;
                                                settings2.defaultValue = currentParameter.defaultValue == 1f;
                                                entry2.setting = settings2;

                                                CCKDescriptor.avatarSettings.settings.Add(entry2);
                                                parametersToBeRanamedInBatch.Add(new string[2] { currentParameter.name, Regex.Replace(currentParameter.name, "[^a-zA-Z0-9#]", "") });
                                            }
                                        }

                                        parametersToBeRanamedInBatch.Add(new string[2] { controller.GetSubParameter(0).name, Regex.Replace(controller.GetSubParameter(0).name, "[^a-zA-Z0-9#]", "") + "-x" });
                                        parametersToBeRanamedInBatch.Add(new string[2] { controller.GetSubParameter(1).name, Regex.Replace(controller.GetSubParameter(0).name, "[^a-zA-Z0-9#]", "") + "-y" });

                                        joystickFix.Add(Regex.Replace(controller.GetSubParameter(0).name, "[^a-zA-Z0-9#]", "") + "-x");
                                        joystickFix.Add(Regex.Replace(controller.GetSubParameter(0).name, "[^a-zA-Z0-9#]", "") + "-y");

                                        //joystick2d
                                        CVRAdvancedSettingsEntry entry = new CVRAdvancedSettingsEntry();
                                        entry.name = controller.GetSubParameter(0).name;
                                        entry.type = CVRAdvancedSettingsEntry.SettingsType.Joystick2D;



                                        CVRAdvancesAvatarSettingJoystick2D settings = new CVRAdvancesAvatarSettingJoystick2D();
                                        settings.defaultValue = new Vector2(0.5f, 0.5f);
                                        settings.rangeMin = new Vector2(0, 0);
                                        settings.rangeMax = new Vector2(1, 1);

                                        entry.setting = settings;


                                        CCKDescriptor.avatarSettings.settings.Add(entry);
                                    }
                                }
                                if (controller.type == VRCExpressionsMenu.Control.ControlType.SubMenu && controller.subMenu != null) {
                                    if (controller.parameter.name != "") {
                                        VRCExpressionParameters.Parameter currentParameter = vrcparameters.parameters.getParameter(controller.parameter.name);
                                        if (currentParameter.valueType == VRCExpressionParameters.ValueType.Bool) {

                                            CVRAdvancedSettingsEntry entry = new CVRAdvancedSettingsEntry();

                                            entry.name = currentParameter.name;
                                            entry.type = CVRAdvancedSettingsEntry.SettingsType.GameObjectToggle;

                                            CVRAdvancesAvatarSettingGameObjectToggle settings = new CVRAdvancesAvatarSettingGameObjectToggle();
                                            settings.usedType = CVRAdvancesAvatarSettingBase.ParameterType.GenerateBool;
                                            settings.defaultValue = currentParameter.defaultValue == 1f;
                                            entry.setting = settings;

                                            CCKDescriptor.avatarSettings.settings.Add(entry);
                                            parametersToBeRanamedInBatch.Add(new string[2] { currentParameter.name, Regex.Replace(currentParameter.name, "[^a-zA-Z0-9#]", "") });

                                            
                                        }
                                    }
                                    goThroughAllMenus(controller.subMenu);
                                }
                            }
                        };

                        goThroughAllMenus(vrcmenu);

                        //rename ALL (optimised) -> remove those that don't require change!
                        parametersToBeRanamedInBatch.RemoveAll(c => c[0] == c[1]);    
                        AnimatorFX.RenameParameters(parametersToBeRanamedInBatch);
                    }

                    //float MaxDistanceBetweenValues = 0.5f;

                    //List<parameterDriverFixer> drivers = new List<parameterDriverFixer>();


                    foreach (AnimatorControllerLayer cl in AnimatorFX.layers) {
                        if (cl.stateMachine == null) continue;
                        foreach (AnimatorStateTransition st in cl.stateMachine.anyStateTransitions) {
                            AnimatorCondition[] ReplaceConditions = st.conditions;
                            for (int a = 0; a < st.conditions.Length; a++) {
                                AnimatorCondition ac = st.conditions[a];
                                bool isFound = parametersThatsNeedsToBeReNumbered.Where(pa => Regex.Replace(pa.getVRCParameter().name, "[^a-zA-Z0-9#]", "") == ac.parameter).Any();
                                if (isFound) {
                                    parameterFixer paraFix = parametersThatsNeedsToBeReNumbered.Where(pa => Regex.Replace(pa.getVRCParameter().name, "[^a-zA-Z0-9#]", "") == ac.parameter).First();
                                    ReplaceConditions[a].threshold = paraFix.getSortedValues().FindIndex(0, p => p.getValue() == ac.threshold);
                                }

                                if (joystickFix.Contains(ac.parameter)) {
                                    ReplaceConditions[a].threshold = (ac.threshold + 1) / 2;
                                }

                            }
                            st.conditions = ReplaceConditions;
                        }

                        Action<ChildAnimatorState> stateWork = delegate (ChildAnimatorState sm) {
                            for (int i = sm.state.behaviours.Length - 1; i >= 0; i--) {
                                if (sm.state.behaviours[i] is VRCAvatarParameterDriver driver) {
                                    if (driver.parameters.Where(p => p.type == VRC.SDKBase.VRC_AvatarParameterDriver.ChangeType.Set).Any()) {
                                        //blendtree parameter drivers are almost impossible todo
                                        if (sm.state.motion is AnimationClip) {
                                            List<VRCAvatarParameterDriver.Parameter> parameters = driver.parameters;
                                            for (int ij = 0; ij < parameters.Count; ij++) {
                                                bool isFound = parametersThatsNeedsToBeReNumbered.Where(pa => pa.getVRCParameter().name == parameters[ij].name).Any();
                                                if (isFound) {
                                                    parameterFixer paraFix = parametersThatsNeedsToBeReNumbered.Where(pa => pa.getVRCParameter().name == parameters[ij].name).First();
                                                    bool foundOldValue = paraFix.getSortedValues().Where(p => p.getValue() == parameters[ij].value).Any();
                                                    if (foundOldValue) {
                                                        parameters[ij].value = paraFix.getSortedValues().FindIndex(0, p => p.getValue() == parameters[ij].value);
                                                    }
                                                }
                                            }
                                            //drivers.Add(new parameterDriverFixer(parameters.ToList(), sm.state.motion));
                                        }

                                        DestroyImmediate(sm.state.behaviours[i], true);
                                    }
                                }
                            }

                            if (sm.state.motion is BlendTree) {
                                Action<Motion> blendTreeAction = null;
                                blendTreeAction = delegate (Motion m) {
                                    var blendTree = (BlendTree)m;

                                    bool b = (joystickFix.Contains(blendTree.blendParameter) ? blendTree.children.Where(c => c.position[0] < 0).Any() : false);
                                    bool b2 = (joystickFix.Contains(blendTree.blendParameterY) ? blendTree.children.Where(c => c.position[1] < 0).Any() : false);
                                    ChildMotion[] motions = blendTree.children;
                                    for (int ik = 0; ik < motions.Length; ik++) {
                                        if (b)
                                            motions[ik].position = new Vector2(joystickFix.Contains(blendTree.blendParameter) ? (motions[ik].position.x + 1) /2 : motions[ik].position.x, motions[ik].position.y);

                                        if (b2)
                                            motions[ik].position = new Vector2(motions[ik].position.x, joystickFix.Contains(blendTree.blendParameterY) ? (motions[ik].position.y + 1) /2 : motions[ik].position.y);


                                        if (motions[ik].motion is BlendTree) blendTreeAction(motions[ik].motion);
                                    }

                                    blendTree.children = motions;
                                };

                                blendTreeAction(sm.state.motion);
                            }

                            foreach (AnimatorStateTransition st in sm.state.transitions) {
                                AnimatorCondition[] ReplaceConditions = st.conditions;
                                for (int a = 0; a < st.conditions.Length; a++) {
                                    AnimatorCondition ac = st.conditions[a];
                                    bool isFound = parametersThatsNeedsToBeReNumbered.Where(pa => Regex.Replace(pa.getVRCParameter().name, "[^a-zA-Z0-9#]", "") == ac.parameter).Any();
                                    if (isFound) {
                                        parameterFixer paraFix = parametersThatsNeedsToBeReNumbered.Where(pa => Regex.Replace(pa.getVRCParameter().name, "[^a-zA-Z0-9#]", "") == ac.parameter).First();

                                        //check if value is even there?

                                        bool foundOldValue = paraFix.getSortedValues().Where(p => p.getValue() == ac.threshold || p.getValue() == Math.Round(ac.threshold)).Any();
                                        if (foundOldValue) {
                                            float valueFound = paraFix.getSortedValues().Where(p => p.getValue() == ac.threshold || p.getValue() == Math.Round(ac.threshold)).First().getValue();
                                            if (valueFound != ac.threshold) {
                                                //meaning its a float and its closets to
                                                float respectableValue = ac.threshold - valueFound;

                                                ReplaceConditions[a].threshold = paraFix.getSortedValues().FindIndex(0, p => p.getValue() == ac.threshold) + respectableValue;
                                            } else {
                                                //int just easily replaceable
                                                ReplaceConditions[a].threshold = paraFix.getSortedValues().FindIndex(0, p => p.getValue() == ac.threshold);
                                            }
                                        } else {
                                            //if not found...
                                            //see if its 'equels' or 'notequels'
                                            //if less then or greater then -> find closet value that matches (originally) and put new value -1 for less, and new value +1 for greater then
                                            if (ac.mode == AnimatorConditionMode.Less || ac.mode == AnimatorConditionMode.Greater) {
                                                //match index by value looking
                                                //less => look all values that lower as and then the first that HIGHER then the less value get Index put it in!
                                                //greater => look all values that are higer as and find the first lowest then the value and get Index
                                                float indexToBeUsed = paraFix.getSortedValues().FindIndex(0, p => p.getValue() > ac.threshold);

                                                ReplaceConditions[a].threshold = (ac.mode == AnimatorConditionMode.Less ? indexToBeUsed == -1 ? paraFix.getSortedValues().Count : indexToBeUsed : indexToBeUsed == -1 ? 0 : indexToBeUsed - 1);

                                            }

                                        }
                                    }

                                    if (joystickFix.Contains(ac.parameter)) {
                                        ReplaceConditions[a].threshold = (ac.threshold + 1) / 2;
                                    }
                                }
                                st.conditions = ReplaceConditions;
                            }
                        };

                        goLayerDeep(cl.stateMachine.stateMachines, stateWork);

                        foreach (ChildAnimatorState sm in cl.stateMachine.states)
                            stateWork(sm);
                    }


                    foreach (parameterFixer para in parametersThatsNeedsToBeReNumbered) {
                        //driver values don't need a toggle -> this was by mistake but a good mistake.
                        //when drivers are convertable it wont make this 
                        CVRAdvancedSettingsEntry entry = new CVRAdvancedSettingsEntry();

                        entry.name = para.getVRCParameter().name;
                        entry.type = CVRAdvancedSettingsEntry.SettingsType.GameObjectDropdown;

                        CVRAdvancesAvatarSettingGameObjectDropdown settings = new CVRAdvancesAvatarSettingGameObjectDropdown();
                        settings.usedType = (para.getVRCParameter().valueType == VRCExpressionParameters.ValueType.Int ? CVRAdvancesAvatarSettingBase.ParameterType.GenerateInt : CVRAdvancesAvatarSettingBase.ParameterType.GenerateFloat);
                        settings.options = new List<CVRAdvancedSettingsDropDownEntry>();

                        foreach (parameterFixer.menuValue x in para.getSortedValues()) {
                            settings.options.Add(new CVRAdvancedSettingsDropDownEntry() {
                                name = x.getName(),
                            });
                        }

                        entry.setting = settings;


                        CCKDescriptor.avatarSettings.settings.Add(entry);
                    }

                    

                }

                CCKDescriptor.avatarSettings.settings = CCKDescriptor.avatarSettings.settings.Distinct().ToList();

                List<string> names = new List<string>();

                //remove double names.
                for (int i = CCKDescriptor.avatarSettings.settings.Count - 1; i >= 0; i--) {
                    if (names.Contains(CCKDescriptor.avatarSettings.settings[i].name)) {
                        CCKDescriptor.avatarSettings.settings.RemoveAt(i); 
                        continue;
                    }

                    names.Add(CCKDescriptor.avatarSettings.settings[i].name);
                }


                //VRC Parameter drivers fixer

                //find all parameter drivers add it to a list
                //animate the animator driver parameter X (bool to enable)

                //have a seperate layer to handle all parameter drivers 
                //when animator driver parameter X is enable play animation to turn on a gameobject (that has another parameter driver on it that set the real value) then play second animation to turn the value animator driver parameter X Off and turn gameobject off.

                //MAKE SURE TO CHECK IF VALUE ORDER ISN'T CHANGED

                AnimatorControllerLayer[] layers = AnimatorFX.layers;
                bool writeDefault = AnimatorFX.getWriteDefault()[0];


        #region Avatar Driver Converter

                /*
                if (drivers.Count > 0) {

                    AnimationCurve animationCurveEnabled = AnimationCurve.Linear(0, 1, (1f / 60f), 1);
                    AnimationCurve animationCurveDisabled = AnimationCurve.Linear(0, 0, (1f / 60f), 0);

                    GameObject parameterDriverList = new GameObject("ParameterDriver").SetParentTransform(duplicate.transform);
                    CVRAnimatorDriver DriverDriver = duplicate.AddComponent<CVRAnimatorDriver>();

                    //create animation layer
                    AnimatorFX.AddLayer("Animator Driver Logic");
                    layers = AnimatorFX.layers;

                    layers[AnimatorFX.layers.Length - 1].defaultWeight = 1;
                    AnimatorFX.layers = layers;

                    AnimatorStateMachine Logic = AnimatorFX.layers[AnimatorFX.layers.Length - 1].stateMachine;

                    Logic.exitPosition = new Vector3(0, -20, 0);
                    Logic.anyStatePosition = new Vector3(0, 90, 0);
                    Logic.entryPosition = new Vector3(0, 35, 0);


                    AnimatorState noDrivers = Logic.AddState("No Drivers");
                    noDrivers.writeDefaultValues = writeDefault;

                    AnimatorStateTransition transition = Logic.AddAnyStateTransition(noDrivers);
                    transition.canTransitionToSelf = false;


                    AnimationClip NoneAnimation = new AnimationClip();

                    for (int i = 0; i < drivers.Count; i++) {
                        AnimatorFX.AddParameter("Driver" + i, AnimatorControllerParameterType.Bool);
                        DriverDriver.animators.Add(duplicate.GetComponent<Animator>());
                        DriverDriver.animatorParameters.Add("Driver" + i);

                        transition.AddCondition(AnimatorConditionMode.IfNot, 0f, "Driver" + i);






                        AnimatorState DriverState = Logic.AddState("Driver" + i + "Enabled");
                        //DriverState.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/.anim", typeof(AnimationClip));
                        DriverState.writeDefaultValues = writeDefault;

                        AnimatorStateTransition DriverStatetransition = Logic.AddAnyStateTransition(DriverState);
                        DriverStatetransition.AddCondition(AnimatorConditionMode.If, 0f, "Driver" + i);
                        DriverStatetransition.canTransitionToSelf = false;



                        AnimatorState DriverStateDisabled = Logic.AddState("Driver" + i + "Disabled");
                        //DriverState.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/.anim", typeof(AnimationClip));
                        DriverStateDisabled.writeDefaultValues = writeDefault;

                        AnimatorStateTransition DriverStatetransitionDisabled = DriverState.AddTransition(DriverStateDisabled).setTransition(true, 0f, false, 0f);
                        DriverStatetransitionDisabled.canTransitionToSelf = false;


                        switch (i) {
                            case 0:
                                DriverDriver.animatorParameter01 = 0f;
                                break;
                            case 1:
                                DriverDriver.animatorParameter02 = 0f;
                                break;
                            case 2:
                                DriverDriver.animatorParameter03 = 0f;
                                break;
                            case 3:
                                DriverDriver.animatorParameter04 = 0f;
                                break;
                            case 4:
                                DriverDriver.animatorParameter05 = 0f;
                                break;
                            case 5:
                                DriverDriver.animatorParameter06 = 0f;
                                break;
                            case 6:
                                DriverDriver.animatorParameter07 = 0f;
                                break;
                            case 7:
                                DriverDriver.animatorParameter08 = 0f;
                                break;
                            case 8:
                                DriverDriver.animatorParameter09 = 0f;
                                break;
                            case 9:
                                DriverDriver.animatorParameter10 = 0f;
                                break;
                            case 10:
                                DriverDriver.animatorParameter11 = 0f;
                                break;
                            case 11:
                                DriverDriver.animatorParameter12 = 0f;
                                break;
                            case 12:
                                DriverDriver.animatorParameter13 = 0f;
                                break;
                            case 13:
                                DriverDriver.animatorParameter14 = 0f;
                                break;
                            case 14:
                                DriverDriver.animatorParameter15 = 0f;
                                break;
                            case 15:
                                DriverDriver.animatorParameter16 = 0f;
                                break;
                        }


                        GameObject DriverX = new GameObject("Driver_" + i).SetParentTransform(parameterDriverList.transform);
                        NoneAnimation.SetCurve(DriverX.transform.GetHierarchyPath(duplicate.transform), typeof(GameObject), "m_IsActive", animationCurveDisabled);

                        AnimationClip DriverXAnimationE = new AnimationClip();
                        DriverXAnimationE.SetCurve(DriverX.transform.GetHierarchyPath(duplicate.transform), typeof(GameObject), "m_IsActive", animationCurveEnabled);
                        DriverXAnimationE.SetCurve("", typeof(CVRAnimatorDriver), "animatorParameter" + ((i + 1) < 10 ? "0" + (i + 1) : "" + (i + 1)), animationCurveEnabled);
                        AssetDatabase.CreateAsset(DriverXAnimationE, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/Driver" + i + "_enabled.anim");

                        AnimationClip DriverXAnimationD = new AnimationClip();
                        DriverXAnimationD.SetCurve(DriverX.transform.GetHierarchyPath(duplicate.transform), typeof(GameObject), "m_IsActive", animationCurveDisabled);
                        DriverXAnimationD.SetCurve("", typeof(CVRAnimatorDriver), "animatorParameter" + ((i + 1) < 10 ? "0" + (i + 1) : "" + (i + 1)), animationCurveDisabled);
                        AssetDatabase.CreateAsset(DriverXAnimationD, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/Driver" + i + "_disabled.anim");

                        DriverState.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/Driver" + i + "_enabled.anim", typeof(AnimationClip));
                        DriverStateDisabled.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/Driver" + i + "_disabled.anim", typeof(AnimationClip));


                        DriverX.SetActive(false);
                        CVRAnimatorDriver driver = DriverX.AddComponent<CVRAnimatorDriver>();
                        for (int ij = 0; ij < drivers[i].getParameters().Count; ij++) {
                            driver.animators.Add(duplicate.GetComponent<Animator>());
                            driver.animatorParameters.Add(Regex.Replace(drivers[i].getParameters()[ij].Name, "[^a-zA-Z0-9#]", ""));
                            switch (ij) {
                                case 0:
                                    driver.animatorParameter01 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 1:
                                    driver.animatorParameter02 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 2:
                                    driver.animatorParameter03 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 3:
                                    driver.animatorParameter04 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 4:
                                    driver.animatorParameter05 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 5:
                                    driver.animatorParameter06 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 6:
                                    driver.animatorParameter07 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 7:
                                    driver.animatorParameter08 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 8:
                                    driver.animatorParameter09 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 9:
                                    driver.animatorParameter10 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 10:
                                    driver.animatorParameter11 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 11:
                                    driver.animatorParameter12 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 12:
                                    driver.animatorParameter13 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 13:
                                    driver.animatorParameter14 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 14:
                                    driver.animatorParameter15 = drivers[i].getParameters()[ij].Value;
                                    break;
                                case 15:
                                    driver.animatorParameter16 = drivers[i].getParameters()[ij].Value;
                                    break;
                            }

                        }

                        //fix animation where parameter drivers use to be on~ to toggle bool
                        AnimationClip anim = (AnimationClip)drivers[i].getAnimation();
                        anim.SetCurve("", typeof(CVRAnimatorDriver), "animatorParameter" + ((i + 1) < 10 ? "0" + (i + 1) : "" + (i + 1)), animationCurveEnabled);

                        EditorUtility.SetDirty(anim);
                    }


                    AssetDatabase.CreateAsset(NoneAnimation, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/None.anim");

                    noDrivers.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/None.anim", typeof(AnimationClip));


                    var states = Logic.states;
                    //place in correct place
                    states[0].position = new Vector3(200, 80, 0); //world

                    for (int a = 1; a < states.Length; a++) {
                        states[a].position = new Vector3((a % 2 == 1 ? 200 : 500), 120 + (((a - 1) + a % 2) * 40), 0);
                    }


                    Logic.states = states;



                }
                */
        #endregion

        #region Locomotion

                if (ImplementCCKLocomotion) {
                    //Add CVR Locomotion
                    AnimatorFX.AddLayer("Locomotion/Emotes");
                    layers = AnimatorFX.layers;

                    AnimatorFX.AddParameter("MovementX", AnimatorControllerParameterType.Float);
                    AnimatorFX.AddParameter("MovementY", AnimatorControllerParameterType.Float);
                    AnimatorFX.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
                    AnimatorFX.AddParameter("CancelEmote", AnimatorControllerParameterType.Trigger);
                    AnimatorFX.AddParameter("Emote", AnimatorControllerParameterType.Float);
                    AnimatorFX.AddParameter("Sitting", AnimatorControllerParameterType.Bool);
                    AnimatorFX.AddParameter("Crouching", AnimatorControllerParameterType.Bool);
                    AnimatorFX.AddParameter("Prone", AnimatorControllerParameterType.Bool);
                    AnimatorFX.AddParameter("Flying", AnimatorControllerParameterType.Bool);

                    layers[AnimatorFX.layers.Length - 1].defaultWeight = 1;
                    layers[AnimatorFX.layers.Length - 1].iKPass = true;
                    AnimatorFX.layers = layers;


                    AnimatorStateMachine Locomotion = AnimatorFX.layers[AnimatorFX.layers.Length - 1].stateMachine;

                    Locomotion.exitPosition = new Vector3(340, 400, 0);
                    Locomotion.anyStatePosition = new Vector3(-200, -100, 0);
                    Locomotion.entryPosition = new Vector3(-200, 0, 0);



                    //TO FIND ALL ANIMATIONS FILES USE THIS:
                    //string[] guids = AssetDatabase.FindAssets(string.Format("t:{0}", typeof(Texture2D).ToString().Replace("UnityEngine.", "")), new[] { AssetDatabase.GetAssetPath(folder) });
                    //WILL FIND IN ANY TYPE OF FOLDER. (if there is more then 1, check location if its in the ABI folder)

                    AnimatorState StL = Locomotion.AddState("Standard Locomotion");


                    BlendTree blendtreeDefault = new BlendTree();
                    blendtreeDefault.blendType = BlendTreeType.FreeformDirectional2D;
                    blendtreeDefault.blendParameter = "MovementX";
                    blendtreeDefault.blendParameterY = "MovementY";

                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingForward.anim", typeof(AnimationClip)), new Vector2(0, 0.4f), 1f);
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingBackwards.anim", typeof(AnimationClip)), new Vector2(0, -0.4f), 1f);
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingStrafeRight.anim", typeof(AnimationClip)), new Vector2(-0.4f, 0f)); //mirror
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingStrafeRight.anim", typeof(AnimationClip)), new Vector2(0.4f, 0f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocIdle.anim", typeof(AnimationClip)), new Vector2(0, 0f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningForward.anim", typeof(AnimationClip)), new Vector2(0, 1f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningBackward.anim", typeof(AnimationClip)), new Vector2(0, -1f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningStrafeRight.anim", typeof(AnimationClip)), new Vector2(-1, 0f)); //mirror
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningStrafeRight.anim", typeof(AnimationClip)), new Vector2(1, 0f));

                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingStrafeRightForwards.anim", typeof(AnimationClip)), new Vector2(0.25f, 0.25f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningStrafeRightForwards.anim", typeof(AnimationClip)), new Vector2(0.7f, 0.7f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingStrafeRightBackwards.anim", typeof(AnimationClip)), new Vector2(0.25f, -0.25f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningStrafeRightBackwards.anim", typeof(AnimationClip)), new Vector2(0.7f, -0.7f));

                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingStrafeRightForwards.anim", typeof(AnimationClip)), new Vector2(-0.25f, 0.25f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningStrafeRightForwards.anim", typeof(AnimationClip)), new Vector2(-0.7f, 0.7f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocWalkingStrafeRightBackwards.anim", typeof(AnimationClip)), new Vector2(-0.25f, -0.25f));
                    blendtreeDefault.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocRunningStrafeRightBackwards.anim", typeof(AnimationClip)), new Vector2(-0.7f, -0.7f));


                    ChildMotion[] childernOfBlendtreeDefault = blendtreeDefault.children;
                    childernOfBlendtreeDefault[2].mirror = true;
                    childernOfBlendtreeDefault[2].timeScale = 0.9f;
                    childernOfBlendtreeDefault[3].timeScale = 0.9f;
                    childernOfBlendtreeDefault[7].mirror = true;
                    childernOfBlendtreeDefault[7].timeScale = 1.2f;
                    childernOfBlendtreeDefault[8].timeScale = 1.2f;
                    childernOfBlendtreeDefault[13].mirror = true;
                    childernOfBlendtreeDefault[14].mirror = true;
                    childernOfBlendtreeDefault[15].mirror = true;
                    childernOfBlendtreeDefault[16].mirror = true;
                    blendtreeDefault.children = childernOfBlendtreeDefault;

                    AssetDatabase.CreateAsset(blendtreeDefault, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/DefaultLocomotionBlendtree.asset");

                    StL.motion = (BlendTree)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/DefaultLocomotionBlendtree.asset", typeof(BlendTree));
                    StL.writeDefaultValues = writeDefault;

                    AnimatorState JS = Locomotion.AddState("JumpStart");
                    JS.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocJumpStart.anim", typeof(AnimationClip));
                    JS.writeDefaultValues = writeDefault;

                    AnimatorState JA = Locomotion.AddState("JumpAir");
                    JA.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocJumpAir.anim", typeof(AnimationClip));
                    JA.writeDefaultValues = writeDefault;

                    AnimatorState JL = Locomotion.AddState("JumpLand");
                    JL.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocJumpLand.anim", typeof(AnimationClip));
                    JL.writeDefaultValues = writeDefault;

                    AnimatorState LF = Locomotion.AddState("LocFlying");
                    LF.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocFlying.anim", typeof(AnimationClip));
                    LF.writeDefaultValues = writeDefault;

                    AnimatorState ST = Locomotion.AddState("Sitting");
                    ST.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocSitting.anim", typeof(AnimationClip));
                    ST.writeDefaultValues = writeDefault;

                    AnimatorState PL = Locomotion.AddState("Prone Locomotion");

                    BlendTree blendtreeProne = new BlendTree();
                    blendtreeProne.blendType = BlendTreeType.FreeformDirectional2D;
                    blendtreeProne.blendParameter = "MovementX";
                    blendtreeProne.blendParameterY = "MovementY";

                    blendtreeProne.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocProneIdle.anim", typeof(AnimationClip)), new Vector2(0, 0f), 1f);
                    blendtreeProne.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocProneForward.anim", typeof(AnimationClip)), new Vector2(0, 0.5f), 1f);
                    blendtreeProne.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocProneRight.anim", typeof(AnimationClip)), new Vector2(-0.5f, 0.0f), 1f);
                    blendtreeProne.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocProneBackward.anim", typeof(AnimationClip)), new Vector2(0, -0.5f), 1f);
                    blendtreeProne.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocProneRight.anim", typeof(AnimationClip)), new Vector2(0.5f, 0f), 1f);


                    ChildMotion[] childernOfBlendtreeProne = blendtreeProne.children;
                    childernOfBlendtreeProne[2].mirror = true;
                    //childernOfBlendtreeProne[3].timeScale = -1;
                    blendtreeProne.children = childernOfBlendtreeProne;

                    AssetDatabase.CreateAsset(blendtreeProne, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/ProneLocomotionBlendtree.asset");
                    PL.motion = (BlendTree)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/ProneLocomotionBlendtree.asset", typeof(BlendTree));

                    PL.writeDefaultValues = writeDefault;

                    AnimatorState CL = Locomotion.AddState("Crouching Locomotion");

                    BlendTree blendtreeCrouch = new BlendTree();
                    blendtreeCrouch.blendType = BlendTreeType.FreeformDirectional2D;
                    blendtreeCrouch.blendParameter = "MovementX";
                    blendtreeCrouch.blendParameterY = "MovementY";

                    blendtreeCrouch.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocCrouchIdle.anim", typeof(AnimationClip)), new Vector2(0, 0f), 1f);
                    blendtreeCrouch.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocCrouchRight.anim", typeof(AnimationClip)), new Vector2(-0.5f, 0.0f), 1f);
                    blendtreeCrouch.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocCrouchRight.anim", typeof(AnimationClip)), new Vector2(0.5f, 0f), 1f);
                    blendtreeCrouch.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocCrouchForward.anim", typeof(AnimationClip)), new Vector2(0, 0.5f), 1f);
                    blendtreeCrouch.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/LocCrouchForward.anim", typeof(AnimationClip)), new Vector2(0, -0.5f), 1f);
                    

                    ChildMotion[] childernOfBlendtreeCrouch = blendtreeCrouch.children;
                    childernOfBlendtreeCrouch[1].mirror = true;
                    childernOfBlendtreeCrouch[4].timeScale = -1;
                    blendtreeCrouch.children = childernOfBlendtreeCrouch;

                    AssetDatabase.CreateAsset(blendtreeCrouch, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/CrouchLocomotionBlendtree.asset");
                    CL.motion = (BlendTree)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/CrouchLocomotionBlendtree.asset", typeof(BlendTree));
                    CL.writeDefaultValues = writeDefault;

                    AnimatorStateMachine animatorStateMachine = Locomotion.AddStateMachine("Emotes");

                    AnimatorState E1 = animatorStateMachine.AddState("Emote 1");
                    E1.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote1.anim", typeof(AnimationClip));
                    E1.writeDefaultValues = writeDefault;


                    AnimatorState E2 = animatorStateMachine.AddState("Emote 2");
                    E2.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote2.anim", typeof(AnimationClip));
                    E2.writeDefaultValues = writeDefault;

                    AnimatorState E3 = animatorStateMachine.AddState("Emote 3");
                    E3.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote3.anim", typeof(AnimationClip));
                    E3.writeDefaultValues = writeDefault;

                    AnimatorState E4 = animatorStateMachine.AddState("Emote 4");
                    E4.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote4.anim", typeof(AnimationClip));
                    E4.writeDefaultValues = writeDefault;

                    AnimatorState E5 = animatorStateMachine.AddState("Emote 5");
                    E5.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote5.anim", typeof(AnimationClip));
                    E5.writeDefaultValues = writeDefault;

                    AnimatorState E6 = animatorStateMachine.AddState("Emote 6");
                    E6.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote6.anim", typeof(AnimationClip));
                    E6.writeDefaultValues = writeDefault;

                    AnimatorState E7 = animatorStateMachine.AddState("Emote 7");
                    E7.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote7.anim", typeof(AnimationClip));
                    E7.writeDefaultValues = writeDefault;

                    AnimatorState E8 = animatorStateMachine.AddState("Emote 8");
                    E8.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/Emote8.anim", typeof(AnimationClip));
                    E8.writeDefaultValues = writeDefault;


                    var E1Exit = E1.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E1ExitCancel = E1.AddExitTransition().setTransition(false, 0f, true, 0); ;
                    E1ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    var TransitionE2 = animatorStateMachine.AddEntryTransition(E2);
                    TransitionE2.AddCondition(AnimatorConditionMode.Greater, 1f, "Emote");

                    var E2Exit = E2.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E2ExitCancel = E2.AddExitTransition().setTransition(false, 0f, true, 0); ;
                    E2ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    var TransitionE3 = animatorStateMachine.AddEntryTransition(E3);
                    TransitionE3.AddCondition(AnimatorConditionMode.Greater, 2f, "Emote");

                    var E3Exit = E3.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E3ExitCancel = E3.AddExitTransition().setTransition(false, 0f, true, 0); ;
                    E3ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    var TransitionE4 = animatorStateMachine.AddEntryTransition(E4);
                    TransitionE4.AddCondition(AnimatorConditionMode.Greater, 3f, "Emote");

                    var E4Exit = E4.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E4ExitCancel = E4.AddExitTransition().setTransition(false, 0f, true, 0); ;
                    E4ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    var TransitionE5 = animatorStateMachine.AddEntryTransition(E5);
                    TransitionE5.AddCondition(AnimatorConditionMode.Greater, 4f, "Emote");

                    var E5Exit = E5.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E5ExitCancel = E5.AddExitTransition().setTransition(false, 0f, true, 0); ;
                    E5ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    var TransitionE6 = animatorStateMachine.AddEntryTransition(E6);
                    TransitionE6.AddCondition(AnimatorConditionMode.Greater, 5f, "Emote");

                    var E6Exit = E6.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E6ExitCancel = E6.AddExitTransition().setTransition(false, 0f, true, 0); ;
                    E6ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    var TransitionE7 = animatorStateMachine.AddEntryTransition(E7);
                    TransitionE7.AddCondition(AnimatorConditionMode.Greater, 6f, "Emote");

                    var E7Exit = E7.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E7ExitCancel = E7.AddExitTransition().setTransition(false, 0f, true, 0); ;
                    E7ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    var TransitionE8 = animatorStateMachine.AddEntryTransition(E8);
                    TransitionE8.AddCondition(AnimatorConditionMode.Greater, 7f, "Emote");

                    var E8Exit = E8.AddExitTransition().setTransition(true, 1f, true, 0);
                    var E8ExitCancel = E8.AddExitTransition().setTransition(false, 0f, true, 0);
                    E8ExitCancel.AddCondition(AnimatorConditionMode.If, 1f, "CancelEmote");

                    List<AnimatorTransition> newTransitionList = new List<AnimatorTransition>();
                    for (int j = animatorStateMachine.entryTransitions.Length - 1; j >= 0; j--) {
                        newTransitionList.Add(animatorStateMachine.entryTransitions[j]);
                    }

                    animatorStateMachine.entryTransitions = newTransitionList.ToArray();


                    animatorStateMachine.exitPosition = new Vector3(440, 0, 0);
                    animatorStateMachine.anyStatePosition = new Vector3(-200, -50, 0);
                    animatorStateMachine.entryPosition = new Vector3(-200, 0, 0);

                    Locomotion.AddStateMachineTransition(animatorStateMachine, StL);

                    animatorStateMachine.parentStateMachinePosition = new Vector3(400, -80, 0);



                    var SSstates = animatorStateMachine.states;
                    for (int i = 0; i < SSstates.Length; i++) {
                        SSstates[i].position = new Vector3(100, -280 + (i * 80), 0);
                    }
                    animatorStateMachine.states = SSstates;


                    var Sstates = Locomotion.stateMachines;
                    Sstates[0].position = new Vector3(-200, 280, 0); // Emotes
                    Locomotion.stateMachines = Sstates;



                    var Lstates = Locomotion.states;
                    //place in correct place
                    Lstates[0].position = new Vector3(0, 0, 0); //Standard Locomotion
                    Lstates[1].position = new Vector3(0, -100, 0); //Jump Start
                    Lstates[2].position = new Vector3(300, -100, 0); //Jump Air
                    Lstates[3].position = new Vector3(300, 0, 0); //Jump Land
                    Lstates[4].position = new Vector3(0, -200, 0); //LocFlying
                    Lstates[5].position = new Vector3(-200, 100, 0); //Sitting
                    Lstates[6].position = new Vector3(0, 200, 0); //Prone Locomotion
                    Lstates[7].position = new Vector3(300, 280, 0); //Crouching Locomotion

                    Locomotion.states = Lstates;


                    //transitions
                    var Transition = Locomotion.AddAnyStateTransition(LF).setTransition(false, 0, true, 0);
                    Transition.AddCondition(AnimatorConditionMode.If, 0f, "Flying");
                    Transition.canTransitionToSelf = false;

                    var FlyingToJumpAir = LF.AddTransition(JA).setTransition(false, 0, true, 0.1f);
                    FlyingToJumpAir.AddCondition(AnimatorConditionMode.IfNot, 0f, "Flying");

                    var JumpAirToJumpLand = JA.AddTransition(JL).setTransition(false, 0, true, 0);
                    JumpAirToJumpLand.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");

                    var JumpLandToStandard = JL.AddTransition(StL).setTransition(true, 0.5588235f, true, 0.25f);

                    var StandardToJumpStart = StL.AddTransition(JS).setTransition(false, 0.7815534f, true, 0.25f);
                    StandardToJumpStart.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");

                    var JumpStartToJumpAir = JS.AddTransition(JA).setTransition(true, 0.7864793f, true, 0.07436973f);
                    JumpStartToJumpAir.offset = 0.009453841f;

                    var StandardToSitting = StL.AddTransition(ST).setTransition(false, 0f, false, 0f);
                    StandardToSitting.AddCondition(AnimatorConditionMode.If, 0f, "Sitting");

                    var SittingToStandard = ST.AddTransition(StL).setTransition(false, 0f, false, 0f);
                    SittingToStandard.AddCondition(AnimatorConditionMode.IfNot, 0f, "Sitting");

                    var StandardToProne = StL.AddTransition(PL).setTransition(false, 0f, false, 0f);
                    StandardToProne.AddCondition(AnimatorConditionMode.If, 0f, "Prone");

                    var ProneToEmotes = PL.AddTransition(animatorStateMachine).setTransition(false, 0f, false, 0f); ;
                    ProneToEmotes.AddCondition(AnimatorConditionMode.Greater, 0f, "Emote");

                    var ProneToStandard = PL.AddTransition(StL).setTransition(false, 0f, false, 0f);
                    ProneToStandard.AddCondition(AnimatorConditionMode.IfNot, 0f, "Prone");

                    var StandardToCrouching = StL.AddTransition(CL).setTransition(false, 0f, false, 0f);
                    StandardToCrouching.AddCondition(AnimatorConditionMode.If, 0f, "Crouching");

                    var CrouchingToEmotes = CL.AddTransition(animatorStateMachine).setTransition(false, 0f, false, 0f); ;
                    CrouchingToEmotes.AddCondition(AnimatorConditionMode.Greater, 0f, "Emote");

                    var CrouchingToStandard = CL.AddTransition(StL).setTransition(false, 0f, false, 0f);
                    CrouchingToStandard.AddCondition(AnimatorConditionMode.IfNot, 0f, "Crouching");

                    var StandardToEmotes = StL.AddTransition(animatorStateMachine).setTransition(false, 0f, false, 0f); ;
                    StandardToEmotes.AddCondition(AnimatorConditionMode.Greater, 0f, "Emote");
                }
        #endregion

        #region handLayers Left
                if (ImplementCCKHandGestures) {
                    AnimatorFX.AddLayer("LeftHand");
                    layers = AnimatorFX.layers;

                    layers[AnimatorFX.layers.Length - 1].defaultWeight = 1;
                    layers[AnimatorFX.layers.Length - 1].avatarMask = (AvatarMask)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/GesturesLeft.mask", typeof(AvatarMask));
                    AnimatorFX.layers = layers;


                    AnimatorStateMachine HandLayerL = AnimatorFX.layers[AnimatorFX.layers.Length - 1].stateMachine;

                    HandLayerL.exitPosition = new Vector3(0, -20, 0);
                    HandLayerL.anyStatePosition = new Vector3(0, 90, 0);
                    HandLayerL.entryPosition = new Vector3(0, 35, 0);

                    AnimatorState OpenL = HandLayerL.AddState("Open");
                    OpenL.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftOpen.anim", typeof(AnimationClip));
                    OpenL.writeDefaultValues = writeDefault;

                    AnimatorState RelaxedFistL = HandLayerL.AddState("Left Relaxed/Fist");

                    BlendTree blendtreeRelaxL = new BlendTree();
                    blendtreeRelaxL.blendType = BlendTreeType.Simple1D;
                    blendtreeRelaxL.blendParameter = "GestureLeft";
                    blendtreeRelaxL.useAutomaticThresholds = false;

                    blendtreeRelaxL.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftRelaxed.anim", typeof(AnimationClip)), 0f);
                    blendtreeRelaxL.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftFist.anim", typeof(AnimationClip)), 1f);
                    blendtreeRelaxL.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftRelaxed.anim", typeof(AnimationClip)), 2f);

                    AssetDatabase.CreateAsset(blendtreeRelaxL, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/HandLRelaxBlendtree.asset");
                    RelaxedFistL.motion = (BlendTree)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/HandLRelaxBlendtree.asset", typeof(BlendTree));
                    RelaxedFistL.writeDefaultValues = writeDefault;

                    AnimatorState ThumbsUpL = HandLayerL.AddState("Left Thumbsup");
                    ThumbsUpL.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftThumbsup.anim", typeof(AnimationClip));
                    ThumbsUpL.writeDefaultValues = writeDefault;

                    AnimatorState GunL = HandLayerL.AddState("Left Gun");
                    GunL.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftGun.anim", typeof(AnimationClip));
                    GunL.writeDefaultValues = writeDefault;

                    AnimatorState PointL = HandLayerL.AddState("Left Point");
                    PointL.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftPoint.anim", typeof(AnimationClip));
                    PointL.writeDefaultValues = writeDefault;

                    AnimatorState PeaceL = HandLayerL.AddState("Left Peace");
                    PeaceL.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftPeace.anim", typeof(AnimationClip));
                    PeaceL.writeDefaultValues = writeDefault;

                    AnimatorState RocknRollL = HandLayerL.AddState("Left Rock&roll");
                    RocknRollL.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandLeftRocknroll.anim", typeof(AnimationClip));
                    RocknRollL.writeDefaultValues = writeDefault;

                    var HandLstates = HandLayerL.states;
                    for (int i = 0; i < HandLstates.Length; i++) {
                        HandLstates[i].position = new Vector3(300, 30 + (i * 80), 0);
                    }
                    HandLayerL.states = HandLstates;

                    var AnyToLeftHOpen = HandLayerL.AddAnyStateTransition(OpenL).setTransition(true, 0, false, 0);
                    AnyToLeftHOpen.AddCondition(AnimatorConditionMode.Less, -0.9f, "GestureLeft");
                    AnyToLeftHOpen.canTransitionToSelf = false;


                    var AnyToLeftHRelax = HandLayerL.AddAnyStateTransition(RelaxedFistL).setTransition(true, 0, false, 6);
                    AnyToLeftHRelax.AddCondition(AnimatorConditionMode.Greater, -0.9f, "GestureLeft");
                    AnyToLeftHRelax.AddCondition(AnimatorConditionMode.Less, 1.1f, "GestureLeft");
                    AnyToLeftHRelax.canTransitionToSelf = false;

                    var AnyToLeftHThumbup = HandLayerL.AddAnyStateTransition(ThumbsUpL).setTransition(true, 0, false, 6);
                    AnyToLeftHThumbup.AddCondition(AnimatorConditionMode.Greater, 1.9f, "GestureLeft");
                    AnyToLeftHThumbup.AddCondition(AnimatorConditionMode.Less, 2.1f, "GestureLeft");
                    AnyToLeftHThumbup.canTransitionToSelf = false;

                    var AnyToLeftHGun = HandLayerL.AddAnyStateTransition(GunL).setTransition(true, 0, false, 6);
                    AnyToLeftHGun.AddCondition(AnimatorConditionMode.Greater, 2.9f, "GestureLeft");
                    AnyToLeftHGun.AddCondition(AnimatorConditionMode.Less, 3.1f, "GestureLeft");
                    AnyToLeftHGun.canTransitionToSelf = false;

                    var AnyToLeftHPoint = HandLayerL.AddAnyStateTransition(PointL).setTransition(true, 0, false, 6);
                    AnyToLeftHPoint.AddCondition(AnimatorConditionMode.Greater, 3.9f, "GestureLeft");
                    AnyToLeftHPoint.AddCondition(AnimatorConditionMode.Less, 4.1f, "GestureLeft");
                    AnyToLeftHPoint.canTransitionToSelf = false;

                    var AnyToLeftHPeace = HandLayerL.AddAnyStateTransition(PeaceL).setTransition(true, 0, false, 6);
                    AnyToLeftHPeace.AddCondition(AnimatorConditionMode.Greater, 4.9f, "GestureLeft");
                    AnyToLeftHPeace.AddCondition(AnimatorConditionMode.Less, 5.1f, "GestureLeft");
                    AnyToLeftHPeace.canTransitionToSelf = false;

                    var AnyToLeftHRocknRoll = HandLayerL.AddAnyStateTransition(RocknRollL).setTransition(true, 0, false, 6);
                    AnyToLeftHRocknRoll.AddCondition(AnimatorConditionMode.Greater, 5.9f, "GestureLeft");
                    AnyToLeftHRocknRoll.canTransitionToSelf = false;

        #endregion

        #region handLayers Right

                    AnimatorFX.AddLayer("RightHand");
                    layers = AnimatorFX.layers;

                    layers[AnimatorFX.layers.Length - 1].defaultWeight = 1;
                    layers[AnimatorFX.layers.Length - 1].avatarMask = (AvatarMask)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/GesturesRight.mask", typeof(AvatarMask));
                    AnimatorFX.layers = layers;


                    AnimatorStateMachine HandLayerR = AnimatorFX.layers[AnimatorFX.layers.Length - 1].stateMachine;

                    HandLayerR.exitPosition = new Vector3(0, -20, 0);
                    HandLayerR.anyStatePosition = new Vector3(0, 90, 0);
                    HandLayerR.entryPosition = new Vector3(0, 35, 0);

                    AnimatorState OpenR = HandLayerR.AddState("Open");
                    OpenR.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightOpen.anim", typeof(AnimationClip));
                    OpenR.writeDefaultValues = writeDefault;

                    AnimatorState RelaxedFistR = HandLayerR.AddState("Right Relaxed/Fist");

                    BlendTree blendtreeRelaxR = new BlendTree();
                    blendtreeRelaxR.blendType = BlendTreeType.Simple1D;
                    blendtreeRelaxR.blendParameter = "GestureRight";
                    blendtreeRelaxR.useAutomaticThresholds = false;

                    blendtreeRelaxR.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightRelaxed.anim", typeof(AnimationClip)), 0f);
                    blendtreeRelaxR.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightFist.anim", typeof(AnimationClip)), 1f);
                    blendtreeRelaxR.AddChild((AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightRelaxed.anim", typeof(AnimationClip)), 2f);

                    AssetDatabase.CreateAsset(blendtreeRelaxR, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/HandRRelaxBlendtree.asset");
                    RelaxedFistR.motion = (BlendTree)AssetDatabase.LoadAssetAtPath("Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK/HandRRelaxBlendtree.asset", typeof(BlendTree));
                    RelaxedFistR.writeDefaultValues = writeDefault;

                    AnimatorState ThumbsUpR = HandLayerR.AddState("Right Thumbsup");
                    ThumbsUpR.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightThumbsup.anim", typeof(AnimationClip));
                    ThumbsUpR.writeDefaultValues = writeDefault;

                    AnimatorState GunR = HandLayerR.AddState("Right Gun");
                    GunR.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightGun.anim", typeof(AnimationClip));
                    GunR.writeDefaultValues = writeDefault;

                    AnimatorState PointR = HandLayerR.AddState("Right Point");
                    PointR.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightPoint.anim", typeof(AnimationClip));
                    PointR.writeDefaultValues = writeDefault;

                    AnimatorState PeaceR = HandLayerR.AddState("Right Peace");
                    PeaceR.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightPeace.anim", typeof(AnimationClip));
                    PeaceR.writeDefaultValues = writeDefault;

                    AnimatorState RocknRollR = HandLayerR.AddState("Right Rock&roll");
                    RocknRollR.motion = (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/ABI.CCK/Animations/HandRightRocknroll.anim", typeof(AnimationClip));
                    RocknRollR.writeDefaultValues = writeDefault;

                    var HandRstates = HandLayerR.states;
                    for (int i = 0; i < HandRstates.Length; i++) {
                        HandRstates[i].position = new Vector3(300, 30 + (i * 80), 0);
                    }
                    HandLayerR.states = HandRstates;

                    var AnyToRightHOpen = HandLayerR.AddAnyStateTransition(OpenR).setTransition(true, 0, false, 0);
                    AnyToRightHOpen.AddCondition(AnimatorConditionMode.Less, -0.9f, "GestureRight");
                    AnyToRightHOpen.canTransitionToSelf = false;

                    var AnyToRightHRelax = HandLayerR.AddAnyStateTransition(RelaxedFistR).setTransition(true, 0, false, 6);
                    AnyToRightHRelax.AddCondition(AnimatorConditionMode.Greater, -0.9f, "GestureRight");
                    AnyToRightHRelax.AddCondition(AnimatorConditionMode.Less, 1.1f, "GestureRight");
                    AnyToRightHRelax.canTransitionToSelf = false;

                    var AnyToRightHThumbup = HandLayerR.AddAnyStateTransition(ThumbsUpR).setTransition(true, 0, false, 6);
                    AnyToRightHThumbup.AddCondition(AnimatorConditionMode.Greater, 1.9f, "GestureRight");
                    AnyToRightHThumbup.AddCondition(AnimatorConditionMode.Less, 2.1f, "GestureRight");
                    AnyToRightHThumbup.canTransitionToSelf = false;

                    var AnyToRightHGun = HandLayerR.AddAnyStateTransition(GunR).setTransition(true, 0, false, 6);
                    AnyToRightHGun.AddCondition(AnimatorConditionMode.Greater, 2.9f, "GestureRight");
                    AnyToRightHGun.AddCondition(AnimatorConditionMode.Less, 3.1f, "GestureRight");
                    AnyToRightHGun.canTransitionToSelf = false;

                    var AnyToRightHPoint = HandLayerR.AddAnyStateTransition(PointR).setTransition(true, 0, false, 6);
                    AnyToRightHPoint.AddCondition(AnimatorConditionMode.Greater, 3.9f, "GestureRight");
                    AnyToRightHPoint.AddCondition(AnimatorConditionMode.Less, 4.1f, "GestureRight");
                    AnyToRightHPoint.canTransitionToSelf = false;

                    var AnyToRightHPeace = HandLayerR.AddAnyStateTransition(PeaceR).setTransition(true, 0, false, 6);
                    AnyToRightHPeace.AddCondition(AnimatorConditionMode.Greater, 4.9f, "GestureRight");
                    AnyToRightHPeace.AddCondition(AnimatorConditionMode.Less, 5.1f, "GestureRight");
                    AnyToRightHPeace.canTransitionToSelf = false;

                    var AnyToRightHRocknRoll = HandLayerR.AddAnyStateTransition(RocknRollR).setTransition(true, 0, false, 6);
                    AnyToRightHRocknRoll.AddCondition(AnimatorConditionMode.Greater, 5.9f, "GestureRight");
                    AnyToRightHRocknRoll.canTransitionToSelf = false;
                }

        #endregion


                EditorUtility.SetDirty(AnimatorFX);

                CCKDescriptor.avatarSettings.baseController = AnimatorFX;

                AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController(AnimatorFX);
                AssetDatabase.CreateAsset(animatorOverrideController, "Assets/Avatar/" +Regex.Replace(Avatar.name.Trim(), "[/?<>\\:*|\"]", "") + "/Animation/CCK_FX_overrides.overrideController");
                CCKDescriptor.overrides = animatorOverrideController;

                EditorUtility.SetDirty(CCKDescriptor);


                //delete old stuff
                DestroyImmediate(VRCDescriptor);
                DestroyImmediate(duplicate.GetComponent(typeof(VRC.Core.PipelineManager)));


            }

        }
#endif

        public void makeWarning(string message, MessageType type, Action a) {
            GUILayout.BeginHorizontal(new GUILayoutOption[] {
                GUILayout.ExpandHeight(true),
                GUILayout.Height(-1)
        });

            EditorGUILayout.HelpBox(message, type);
            a();

            GUILayout.EndHorizontal();

        }
        #if VRC_SDK_VRCSDK3 && CVR_CCK_EXISTS
        public void selectAvatarFromScene() {
            if (Selection.activeGameObject != null) this.Avatar = Selection.activeGameObject;
        }
#endif
    }
}
