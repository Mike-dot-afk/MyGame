using UnityEngine;

public class Vcam2SyncMove : MonoBehaviour
{
    [Header("引用直升机控制器")]
    public ChopperController chopperController;

    private void Update()
    {
        if (chopperController == null) return;
        // 只有直升机飞过400米，进入跟随状态之后镜头二才开始移动
        if (chopperController.IsFollowCamState)
        {
            float speed = chopperController.followCamSpeed;
            Vector3 dir = chopperController.FlyForwardDir;
            transform.position += dir * speed * Time.deltaTime;
        }
    }
}