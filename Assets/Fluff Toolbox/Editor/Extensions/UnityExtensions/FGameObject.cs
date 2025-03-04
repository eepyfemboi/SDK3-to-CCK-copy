using System.Collections;
using UnityEngine;

namespace Fluff_Toolbox.Extensions.UnityExtensions {
    public static class FGameObject {

        public static GameObject SetParentTransform(this GameObject obj, Transform parent) {
            if (parent != null) obj.transform.parent = parent;
            return obj;
        }

    }
}