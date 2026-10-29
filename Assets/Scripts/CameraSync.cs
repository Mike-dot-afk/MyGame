using UnityEngine;

public class CameraSync : MonoBehaviour
{
    [Header("引用：拖入场景上带ParachuteSpawner的直升机物体")]
    public ParachuteSpawner parachuteSpawner;

    [Header("镜头同步参数，自行调参")]
    public float speedToCameraOffsetScale = 1f;

    private void Update()
    {
        if (parachuteSpawner == null) return;

        if (!parachuteSpawner.shouldCameraSync)
        {
            return;
        }

        var list = parachuteSpawner.SpawnedParachutes;
        if (list.Count < 2) return;

        ParachuteItem secondPara = list[1];
        if (secondPara == null) return;

        float paraSpeed = secondPara.CurrentFallSpeed;

        float offsetY = -paraSpeed * speedToCameraOffsetScale * Time.deltaTime;
        transform.position += new Vector3(0, offsetY, 0);
    }
}