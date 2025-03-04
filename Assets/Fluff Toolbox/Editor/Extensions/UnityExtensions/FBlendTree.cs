using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Fluff_Toolbox.Extensions.UnityExtensions {
    public static class FBlendTree {

        public static void AddChild(this BlendTree blendtree, Motion motion, Vector2 position, float threshold) {
            ChildMotion[] array = blendtree.children;
            ChildMotion item = default(ChildMotion);
            item.timeScale = 1f;
            item.motion = motion;
            item.position = position;
            item.threshold = threshold;
            item.directBlendParameter = "Blend";
            ArrayUtility.Add(ref array, item);
            blendtree.children = array;
        }

    }
}
