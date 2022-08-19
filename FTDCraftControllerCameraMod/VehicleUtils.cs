using BrilliantSkies.Ai;
using BrilliantSkies.Ai.Modules;
using BrilliantSkies.Ai.Modules.Behaviour;
using BrilliantSkies.Ai.Modules.Behaviour.Examples;
using BrilliantSkies.Ai.Modules.Manoeuvre;
using UnityEngine;

namespace FTDCraftControllerCameraMod
{
    public class VehicleUtils
    {
        public static AiMaster GetMovementAiFromMainConstruct(MainConstruct mainConstruct, out IManoeuvre movement)
        {
            BlockStore<AIMainframe> ais = mainConstruct.iBlockTypeStorage.MainframeStore;
            AiMaster aiMaster = null;
            movement = null;
            for (int i = 0; i < ais.Count; i++)
            {
                AiMaster ai = ais.Blocks[i].Node.Master;
                // If movement is null, aiMaster is also null.
                if ((movement == null || ai.Priority > aiMaster.Priority)
                    && ai.Pack.GetSelectedManoeuvre(out IManoeuvre tempMovement))
                {
                    aiMaster = ai;
                    movement = tempMovement;
                }
            }
            return aiMaster;
        }

        public static bool GetMaxPitchFromAiMaster(AiMaster aiMaster, ref float pitch)
        {
            bool changed = false;
            for (int i = 0; i < aiMaster.Pack.Packages.Count; i++)
            {
                AiBaseAbstract aiBaseAbstract = aiMaster.Pack.Packages[i];
                if (aiBaseAbstract.RoutineType == AiRoutineType.Behaviour)
                {
                    switch (aiBaseAbstract)
                    {
                        /*case BehaviourCircleAtDistance bcad:
                            if (bcad.RollWithinAzi.Us > 0f && bcad.RollToTarget.Us > roll)
                            {
                                changed = true;
                                roll = bcad.RollToTarget.Us;
                            }
                            break;*/
                        case BehaviourPointAndMaintainDistance bpamd:
                            if (bpamd.PitchWithinAzi.Us > 0f && bpamd.PitchToTarget.Us > pitch)
                            {
                                changed = true;
                                pitch = bpamd.PitchToTarget.Us;
                            }
                            break;
                        case BehaviourPointAndMaintainDistanceLegacy bpamdl:
                            changed |= bpamdl.LookDirection.Us == LookOptions.AtThem;
                            pitch = bpamdl.LookDirection.Us == LookOptions.AtThem ? 90f : pitch;
                            break;
                        default:
                            break;
                    }
                }
            }
            return changed;
        }

        public static Vector3 NormalizeAngles(Vector3 vector3)
        {
            vector3.x = NormalizeAngle(vector3.x);
            vector3.y = NormalizeAngle(vector3.y);
            vector3.z = NormalizeAngle(vector3.z);
            return vector3;
        }

        public static float NormalizeAngle(float angle, float min = -180f, float max = 180f)
        {
            angle %= 360f;
            if (angle < min)
                angle += 360f;
            else if (angle > max)
                angle -= 360f;
            return angle;
        }
    }
}
