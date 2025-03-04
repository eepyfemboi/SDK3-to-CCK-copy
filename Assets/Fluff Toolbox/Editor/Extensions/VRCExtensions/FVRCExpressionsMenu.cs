using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace Fluff_Toolbox.Extensions.VRCExtensions {
    public static class FVRCExpressionsMenu {

        #if VRC_SDK_VRCSDK3
        public static VRCExpressionsMenu getVRCExpressionsMenuByName(this VRCExpressionsMenu menu, string name, bool submenu) {
            Func<VRCExpressionsMenu, VRCExpressionsMenu> action = null;
            action = delegate (VRCExpressionsMenu m2) {
                foreach(VRCExpressionsMenu.Control c in  m2.controls) 
                    if (c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null) {
                        if (c.subMenu.name == name) return (submenu ? c.subMenu : m2);
                        return action(c.subMenu);
                    }
                return null;
            };

            return action(menu);
        }

        public static VRCExpressionsMenu getVRCExpressionsControllerByName(this VRCExpressionsMenu menu, string name) {
            Func<VRCExpressionsMenu, VRCExpressionsMenu> action = null;
            action = delegate (VRCExpressionsMenu m2) {
                foreach (VRCExpressionsMenu.Control c in m2.controls) {
                    if (c.name == name) return m2;
                    if (c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null) 
                        action(c.subMenu);
                    
                }
                return null;
            };

            return action(menu);
        }

        public static void deleteMenuControllerByName(this VRCExpressionsMenu menu, string name) {
            Action<VRCExpressionsMenu> action = null;
            action = delegate (VRCExpressionsMenu m2) {
                foreach (VRCExpressionsMenu.Control c in m2.controls) {
                    if (c.name == name) { m2.controls.Remove(c); EditorUtility.SetDirty(m2); return; }
                    if (c.type == VRCExpressionsMenu.Control.ControlType.SubMenu && c.subMenu != null) 
                        action(c.subMenu);
                    
                }
            };
            action(menu);
        }
        #endif

    }
}
