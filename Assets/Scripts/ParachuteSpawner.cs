using UnityEngine;
using System.Collections;
using System.Collections.Generic;
public class ParachuteSpawner : MonoBehaviour
{
    [Header("生成设置")]
    public GameObject parachutePrefab;
    public float spawnInterval = 0.5f;
    [Header("爆炸特效设置")]
    public GameObject explosionPrefab;
    public Transform explosionPivot; //直升机上的爆炸挂点Pivot
    [Header("直升机引用")]
    public ChopperController helicopter;
    [Header("直升机Z轴移动")]
    public float helicopterMoveSpeedZ = 2f;
    private bool isHelicopterMoving;
    [Header("信号门控：控制是否响应停止降落伞信号")]
    public bool enableStopSignal = true;
    public bool shouldCameraSync { get; private set; }
    private List<ParachuteItem> spawnedParachutes = new List<ParachuteItem>();
    public IReadOnlyList<ParachuteItem> SpawnedParachutes => spawnedParachutes;

    private bool _hasRunBlackCleanup;

    void Update()
    {
        if (isHelicopterMoving)
        {
            transform.position += Vector3.forward * helicopterMoveSpeedZ * Time.deltaTime;
        }
    }
    public void SpawnTwoParachutes()
    {
        StartCoroutine(SpawnCoroutine());
    }
    IEnumerator SpawnCoroutine()
    {
        shouldCameraSync = false;
        isHelicopterMoving = false;
        Vector3 pos1 = transform.position + new Vector3(-1.8f, 0, 0f);
        GameObject obj1 = Instantiate(parachutePrefab, pos1, transform.rotation);
        ParachuteItem item1 = obj1.GetComponent<ParachuteItem>();
        if (item1 != null)
        {
            item1.useHorizontalMove = true;
            spawnedParachutes.Add(item1);
        }
        yield return new WaitForSeconds(spawnInterval);
        Vector3 pos2 = transform.position + new Vector3(1.8f, 0, 0f);
        GameObject obj2 = Instantiate(parachutePrefab, pos2, transform.rotation);
        ParachuteItem item2 = obj2.GetComponent<ParachuteItem>();
        if (item2 != null)
        {
            item2.useHorizontalMove = false;
            spawnedParachutes.Add(item2);
        }
    }
    public void SignalTriggerExplosionAndMove()
    {
        if (explosionPrefab != null && explosionPivot != null)
        {
            Instantiate(explosionPrefab, explosionPivot.position, explosionPivot.rotation);
        }
        isHelicopterMoving = true;
    }
    public void SignalEnableCameraSync()
    {
        shouldCameraSync = true;
        if (helicopter != null)
        {
            helicopter.StartEngineVolumeFadeToZero();
        }
    }
    public void SignalDisableCameraSync()
    {
        shouldCameraSync = false;
    }
    public void SignalStopAllParachutes()
    {
        if (!enableStopSignal) return;
        foreach (var parachute in spawnedParachutes)
        {
            if (parachute != null)
            {
                parachute.StopMove();
            }
        }
    }
    public void ClearParachuteList()
    {
        spawnedParachutes.Clear();
        shouldCameraSync = false;
        isHelicopterMoving = false;
        _hasRunBlackCleanup = false;
    }
    public void SignalHitSecondParachute()
    {
        var list = SpawnedParachutes;
        if (list.Count < 2) return;
        ParachuteItem secondPara = list[1];
        if (secondPara != null)
        {
            secondPara.OnBeHitByDebris();
        }
    }

    public void SignalBlackScreenCleanup()
    {
        if (_hasRunBlackCleanup) return;
        _hasRunBlackCleanup = true;

        foreach (var para in spawnedParachutes)
        {
            if (para != null)
            {
                Destroy(para.gameObject);
            }
        }
        ClearParachuteList();

        if (helicopter != null)
        {
            helicopter.gameObject.SetActive(false);
        }
        isHelicopterMoving = false;
    }
}