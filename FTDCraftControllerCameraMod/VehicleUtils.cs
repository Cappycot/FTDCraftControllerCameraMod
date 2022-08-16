using BrilliantSkies.Ai;
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
