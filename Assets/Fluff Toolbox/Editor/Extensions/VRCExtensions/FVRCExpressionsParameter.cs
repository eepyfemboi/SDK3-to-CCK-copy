using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace Fluff_Toolbox.Extensions.VRCExtensions {
    public static class FVRCExpressionsParameter {
        #if VRC_SDK_VRCSDK3
        public static VRCExpressionParameters.Parameter getParameter(this VRCExpressionParameters.Parameter[] parameters, string parameterName) {
            foreach (VRCExpressionParameters.Parameter parameter in parameters) 
                if (parameter.name.Equals(parameterName)) return parameter;
            return null;
        }

        public static void addOrRemoveParameter(this VRCExpressionParameters.Parameter[] parameters, string parameterName, bool remove) {

        }

        public static void removeParameter(this VRCExpressionParameters parameters, string parameterName) {
            List<VRCExpressionParameters.Parameter> newList = new List<VRCExpressionParameters.Parameter>();
            foreach (VRCExpressionParameters.Parameter parameter in parameters.parameters)
                if (!parameter.name.Equals(parameterName)) newList.Add(parameter);

            parameters.parameters = newList.ToArray();
        }

        public static void addParameter(this VRCExpressionParameters parameters, VRCExpressionParameters.Parameter parameter) {
            VRCExpressionParameters.Parameter[] newList = new VRCExpressionParameters.Parameter[parameters.parameters.Length + 1];
            Array.Copy(parameters.parameters, 0, newList, 0, parameters.parameters.Length);
            newList.SetValue(parameter, parameters.parameters.Length);

            parameters.parameters = newList.ToArray();
        }

        public static void removeParameterByRegex(this VRCExpressionParameters parameters, string regex) {
            Regex regexExpression = new Regex(regex);
            List<VRCExpressionParameters.Parameter> newList = new List<VRCExpressionParameters.Parameter>();
            foreach (VRCExpressionParameters.Parameter parameter in parameters.parameters)
                if (!regexExpression.IsMatch(parameter.name)) newList.Add(parameter);

            parameters.parameters = newList.ToArray();
        }
        #endif

    }
}
