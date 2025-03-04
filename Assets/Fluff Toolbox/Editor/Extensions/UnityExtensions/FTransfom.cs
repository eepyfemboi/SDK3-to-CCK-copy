using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Fluff_Toolbox.Extensions.UnityExtensions {
    public static class FTransfom {

        public static Transform Find(this Transform transfom, string name, bool inactive) {
            foreach (Transform t in transfom) {
                if ((!inactive && t.name == name && t.gameObject.activeInHierarchy) || (inactive && t.name == name)) {
                    return t;
                }
            }
            return null;
        }

        public static Transform FindChildInHierarchy(this Transform transfom, string name) {
            Func<Transform, Transform> goOverAll = null;

            goOverAll = delegate (Transform t) {
                if (t.name == name) return t;

                foreach (Transform t2 in t) {
                    Transform t3 = goOverAll(t2);
                    if (t3 != null) return t3;
                }

                return null;
            };

            return goOverAll(transfom);
        }

    }
}
