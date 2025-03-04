using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Avatars.Components;
#endif

namespace Fluff_Toolbox.Extensions.UnityExtensions {
    public static class FAnimator {

        public static bool[] getWriteDefaultFromLayer(this AnimatorController animator, AnimatorControllerLayer layer) {
            bool[] writedefaults = new bool[3] { false, false, false };

            Action<ChildAnimatorState> stateWork = delegate (ChildAnimatorState sm) {
                writedefaults[0] = sm.state.writeDefaultValues || writedefaults[0];
                writedefaults[1] = !sm.state.writeDefaultValues || writedefaults[1];
                writedefaults[2] = true;
            };

            Action<ChildAnimatorStateMachine[], Action<ChildAnimatorState>> goLayerDeep = null;
            goLayerDeep = delegate (ChildAnimatorStateMachine[] stateMachines, Action<ChildAnimatorState> a) {
                foreach (ChildAnimatorStateMachine casm in stateMachines) {
                    foreach (ChildAnimatorState state in casm.stateMachine.states)
                        a(state);

                    goLayerDeep(casm.stateMachine.stateMachines, a);
                }
            };

            if (layer.stateMachine != null) {
                goLayerDeep(layer.stateMachine.stateMachines, stateWork);

                foreach (ChildAnimatorState sm in layer.stateMachine.states)
                    stateWork(sm);
            }

            return writedefaults;
        }

        public static bool[] getWriteDefault(this AnimatorController animator) {
            //first is writedefaults ON
            //second is writedefaults OFF
            //both True = MIX
            bool[] writedefaults = new bool[2] { false, false} ;

            foreach (AnimatorControllerLayer cl in animator.layers) {
                bool[] t = animator.getWriteDefaultFromLayer(cl);
                if (t[2]) {
                    writedefaults[0] = t[0] || writedefaults[0];
                    writedefaults[1] = t[1] || writedefaults[1];
                }
            }

            return writedefaults;
        }

        public static AnimatorControllerLayer getLayerByName(this AnimatorController animator, string name) {
            foreach (AnimatorControllerLayer cl in animator.layers) 
                if (cl.name.Equals(name)) return cl;
            return null;
        }

        public static AnimatorStateTransition setTransition(this AnimatorStateTransition transition, bool hasExitTime, float exitTime, bool hasFixedDuration, float duration) {
            transition.hasExitTime = hasExitTime;
            transition.exitTime = exitTime;
            transition.hasFixedDuration = hasFixedDuration;
            transition.duration = duration;
            return transition;
        }


        public static AnimatorStateTransition cloneTransition(this AnimatorStateTransition transition) {
            AnimatorStateTransition copy = new AnimatorStateTransition();
            copy.conditions = new AnimatorCondition[0];
            foreach (AnimatorCondition condition in transition.conditions)
                copy.AddCondition(condition.mode, condition.threshold, condition.parameter);

            copy.canTransitionToSelf = transition.canTransitionToSelf;
            copy.hasFixedDuration = transition.hasFixedDuration;
            copy.duration = transition.duration;
            copy.exitTime = transition.exitTime;
            copy.offset = transition.offset;
            copy.solo = transition.solo;
            //copy.destinationState = transition.destinationState;
            //copy.destinationStateMachine = transition.destinationStateMachine;
            copy.hasExitTime = transition.hasExitTime;
            copy.orderedInterruption = transition.orderedInterruption;
            copy.interruptionSource = transition.interruptionSource;
            copy.isExit = transition.isExit;
            copy.mute = transition.mute;

            return copy;
        }

        public static AnimatorControllerLayer cloneLayer(this AnimatorControllerLayer layer, AnimatorControllerLayer CopyFrom ) {
            layer.defaultWeight = CopyFrom.defaultWeight;
            layer.syncedLayerIndex = CopyFrom.syncedLayerIndex;
            layer.syncedLayerAffectsTiming = CopyFrom.syncedLayerAffectsTiming;
            layer.avatarMask = CopyFrom.avatarMask;
            layer.blendingMode = CopyFrom.blendingMode;
            layer.iKPass = CopyFrom.iKPass;
            layer.name = CopyFrom.name;

            layer.stateMachine = CopyFrom.stateMachine.cloneStateMachine();
            return layer;
        }

        public static AnimatorStateMachine cloneStateMachine(this AnimatorStateMachine CopyFrom) {
            AnimatorStateMachine statemachine = new AnimatorStateMachine();
            statemachine.name = CopyFrom.name;

            statemachine.exitPosition = CopyFrom.exitPosition;
            statemachine.entryPosition = CopyFrom.entryPosition;
            statemachine.anyStatePosition = CopyFrom.anyStatePosition;

            for (int i = 0; i < CopyFrom.stateMachines.Length; i++) {
                statemachine.AddStateMachine(CopyFrom.stateMachines[i].stateMachine.cloneStateMachine(), CopyFrom.stateMachines[i].position);
            }

            for (int i = 0; i < CopyFrom.states.Length; i++) {
                statemachine.AddState(CopyFrom.states[i].state.cloneState(), CopyFrom.states[i].position);
            }

            //figure out all transitions by searching the index of the original state or statemachine
            for (int i = 0; i < CopyFrom.anyStateTransitions.Length; i++) {
                statemachine.AddAnyStateTransition(CopyFrom.anyStateTransitions[i].destinationState).cloneFrom(CopyFrom.anyStateTransitions[i]);
            }

            //loop once over ALL States/StateMachines -> figure out which statemachine contains which states and make a easy find solution to find the new matching statemachine.
            //soo object should contain:
            //old statemachine + states
            //new statemachine + states

            for (int j = 0; j < statemachine.states.Length; j++) {
                for (int i = 0; i < CopyFrom.states[j].state.transitions.Length; i++) {
                    int index = CopyFrom.states.getIndex(CopyFrom.states[j].state.transitions[i].destinationState);
                    if (index != -1) {
                        var trans = statemachine.states[j].state.AddTransition(CopyFrom.states[j].state.transitions[i].destinationState);
                        trans.cloneFrom(CopyFrom.states[j].state.transitions[i]);
                        trans.destinationState = statemachine.states[index].state;
                    }

                    var T = CopyFrom.states[j].state.transitions[i];
                    Debug.Log("State: " + (T.destinationState != null ? T.destinationState.name : "None") + ", StateMachine: " + (T.destinationStateMachine != null ? T.destinationStateMachine.name : "None") + ", Exit: " + T.isExit); 

                    if (T.destinationState != null) 
                        foreach(ChildAnimatorStateMachine x in CopyFrom.stateMachines) {
                            foreach (var st in x.stateMachine.states) {
                                if (st.state == T.destinationState) {
                                    Debug.Log("Extra: " + x.stateMachine.name);
                                }
                            }
                        }
                }
            }


            return statemachine;
        }

        private static int getIndex(this ChildAnimatorState[] states, AnimatorState state) {
            for (int i = 0; i < states.Length; i++) {
                //Debug.Log(states[i].state.name);
                //Debug.Log(state);
                if (states[i].state == state) return i;
            }

            return -1;
        }

        public static AnimatorState cloneState(this AnimatorState state) {
            AnimatorState newState = new AnimatorState();
            newState.iKOnFeet = state.iKOnFeet;
            newState.mirror = state.mirror;
            newState.mirrorParameter = state.mirrorParameter;
            newState.mirrorParameterActive = state.mirrorParameterActive;

            newState.timeParameterActive = state.timeParameterActive;
            newState.timeParameter = state.timeParameter;

            newState.speed = state.speed;
            newState.speedParameterActive = state.speedParameterActive;
            newState.speedParameter = state.speedParameter;

            newState.cycleOffset = state.cycleOffset;
            newState.cycleOffsetParameter = state.cycleOffsetParameter;
            newState.cycleOffsetParameterActive = state.cycleOffsetParameterActive;

            newState.name = state.name;
            newState.motion = state.motion;
            newState.tag = state.tag;

            newState.writeDefaultValues = state.writeDefaultValues;

            //do all transitions.

            return newState;
        }

        public static AnimatorStateTransition cloneFrom(this AnimatorStateTransition transition, AnimatorStateTransition CopyFrom) {
            transition.conditions = new AnimatorCondition[0];
            foreach (AnimatorCondition condition in CopyFrom.conditions)
                transition.AddCondition(condition.mode, condition.threshold, condition.parameter);

            transition.canTransitionToSelf = CopyFrom.canTransitionToSelf;
            transition.hasFixedDuration = CopyFrom.hasFixedDuration;
            transition.duration = CopyFrom.duration;
            transition.exitTime = CopyFrom.exitTime;
            transition.offset = CopyFrom.offset;
            transition.solo = CopyFrom.solo;
            transition.destinationState = CopyFrom.destinationState;
            transition.destinationStateMachine = CopyFrom.destinationStateMachine;
            transition.hasExitTime = CopyFrom.hasExitTime;
            transition.orderedInterruption = CopyFrom.orderedInterruption;
            transition.interruptionSource = CopyFrom.interruptionSource;
            transition.isExit = CopyFrom.isExit;
            transition.mute = CopyFrom.mute;

            return transition;
        }

        public static void RenameParameters(this AnimatorController animator, List<string[]> parameters) {
            AnimatorControllerParameter[] newParameters = animator.parameters;

            foreach (AnimatorControllerParameter para in newParameters)
                for (int i = 0; i < parameters.Count; i++) 
                    if (para.name.Equals(parameters[i][0])) {
                        para.name = parameters[i][1];
                        break;
                    }

            animator.parameters = newParameters;

            foreach (AnimatorControllerLayer cl in animator.layers) {
                if (cl.stateMachine == null) continue;
                foreach (AnimatorStateTransition st in cl.stateMachine.anyStateTransitions) {
                    AnimatorCondition[] listToUpdate2 = st.conditions;
                    for (int i = 0; i < listToUpdate2.Length; i++)
                        for (int j = 0; j < parameters.Count; j++)
                            if (listToUpdate2[i].parameter.Equals(parameters[j][0])) {
                                listToUpdate2[i].parameter = parameters[j][1];
                                break;
                            }
                        
                    st.conditions = listToUpdate2;
                }

                Action<ChildAnimatorState> stateWork = delegate (ChildAnimatorState sm) {
                    for (int i = 0; i < parameters.Count; i++) 
                        if (sm.state.mirrorParameter.Equals(parameters[i][0])) {
                            sm.state.mirrorParameter = parameters[i][1];
                            break;
                        }

                    for (int i = 0; i < parameters.Count; i++) 
                        if (sm.state.cycleOffsetParameter.Equals(parameters[i][0])) { 
                            sm.state.cycleOffsetParameter = parameters[i][1];
                            break;
                        }

                    for (int i = 0; i < parameters.Count; i++)
                        if (sm.state.speedParameter.Equals(parameters[i][0])) {
                            sm.state.speedParameter = parameters[i][1];
                            break;
                        }

                    for (int i = 0; i < parameters.Count; i++)
                        if (sm.state.timeParameter.Equals(parameters[i][0])) {
                            sm.state.timeParameter = parameters[i][1];
                            break;
                        }

#if VRC_SDK_VRCSDK3
                    for (int i = sm.state.behaviours.Length - 1; i >= 0; i--) {
                        if (sm.state.behaviours[i] is VRCAvatarParameterDriver driver) {
                            List<VRCAvatarParameterDriver.Parameter> driverparameters = driver.parameters;
                            for (int ij = 0; ij < driverparameters.Count; ij++) {
                                for (int ik = 0; ik < parameters.Count; ik++) {
                                    if (driverparameters[ij].name.Equals(parameters[ik][0]))
                                        driverparameters[ij].name = parameters[ik][1];
                                }
                            }
                            driver.parameters = driverparameters;
                        }
                    }
#endif


                    foreach (AnimatorStateTransition st in sm.state.transitions) {
                        AnimatorCondition[] listToUpdate = st.conditions;
                        for (int i = 0; i < listToUpdate.Length; i++)
                            for (int j = 0; j < parameters.Count; j++)
                                if (listToUpdate[i].parameter.Equals(parameters[j][0]))
                                    listToUpdate[i].parameter = parameters[j][1];

                        st.conditions = listToUpdate;
                    }


                    if (sm.state.motion is BlendTree) {
                        Action<Motion> blendTreeAction = null;
                        blendTreeAction = delegate (Motion m) {
                            var blendTree = (BlendTree)m;
                            for (int i = 0; i < parameters.Count; i++)
                                if (blendTree.blendParameter.Equals(parameters[i][0])) {
                                    blendTree.blendParameter = parameters[i][1];
                                }
                            for (int i = 0; i < parameters.Count; i++)
                                if (blendTree.blendParameterY.Equals(parameters[i][0])) {
                                    blendTree.blendParameterY = parameters[i][1];
                                }
                               

                            ChildMotion[] motions = blendTree.children;
                            for (int i = 0; i < motions.Length; i++) {
                                for (int j = 0; j < parameters.Count; j++)
                                    if (motions[i].directBlendParameter.Equals(parameters[j][0])) {
                                        motions[i].directBlendParameter = parameters[j][1];
                                    }

                                if (motions[i].motion is BlendTree) blendTreeAction(motions[i].motion);
                            }

                            blendTree.children = motions;
                        };
                        blendTreeAction(sm.state.motion);
                    }
                };


                

                Action<ChildAnimatorStateMachine[], Action<ChildAnimatorState>> goLayerDeep = null;
                goLayerDeep = delegate (ChildAnimatorStateMachine[] stateMachines, Action<ChildAnimatorState> a) {
                    foreach (ChildAnimatorStateMachine casm in stateMachines) {
                        foreach (ChildAnimatorState state in casm.stateMachine.states)
                            a(state);
                    }
                };

                goLayerDeep(cl.stateMachine.stateMachines, stateWork);

                foreach (ChildAnimatorState sm in cl.stateMachine.states)
                    stateWork(sm);
            }
        }

        public static bool ExistParameter(this AnimatorController animator, string name) {
            return animator.parameters.Any(p => p.name.Equals(name));
        }

        public static void removeLayerByName(this AnimatorController animator, string name) {
            for (int a = animator.layers.Length - 1; a >= 0; a--)
                if (animator.layers[a].name.Equals(name))
                    animator.RemoveLayer(a);
        }

        public static void removeLayerByRegex(this AnimatorController animator, string regex) {
            Regex regexExpression = new Regex(regex);
            for (int a = animator.layers.Length - 1; a >= 0; a--)
                if (regexExpression.IsMatch(animator.layers[a].name))
                    animator.RemoveLayer(a);
        }

        public static void removeParameterByName(this AnimatorController animator, string name) {
            for (int a = animator.parameters.Length - 1; a >= 0; a--)
                if (animator.parameters[a].name.Equals(name))
                    animator.RemoveParameter(a);
        }

        public static void removeParameterByRegex(this AnimatorController animator, string regex) {
            Regex regexExpression = new Regex(regex);
            for (int a = animator.parameters.Length - 1; a >= 0; a--)
                if (regexExpression.IsMatch(animator.parameters[a].name))
                    animator.RemoveParameter(a);
        }

    }
}