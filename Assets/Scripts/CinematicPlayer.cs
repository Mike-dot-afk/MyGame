using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

public static class CinematicPlayer
{
    /// <summary>
    /// CG播放完成回调
    /// </summary>
    public delegate void CinematicFinishCallback();

    /// <summary>
    /// 启动一段CG，自动执行整套标准化流程
    /// excludeLayers：CG相机需要屏蔽的层级，默认屏蔽Enemy、Player
    /// onCgStart: CG流程初始化完成、Timeline播放之前，放你的专属业务（瞬移怪物，开关导航）
    /// onCgFinish: CG全部复原完成，EndCG之前，放你的收尾业务
    /// </summary>
    public static void Play(
        PlayableDirector director,
        Camera cgCamera,
        CinematicFinishCallback onCgStart,
        CinematicFinishCallback onCgFinish,
        params string[] excludeLayers)
    {
        if (director == null)
        {
            Debug.LogError("PlayableDirector不能为空");
            return;
        }
        if (excludeLayers == null || excludeLayers.Length == 0)
        {
            excludeLayers = new string[] { "Enemy", "Player" };
        }

        // 1. 开启CG全局冻结
        StartGameFlow.BeginCG();

        // 2. 标记Timeline内参演敌人AI，忽略战斗冻结
        List<EnemyAIBase> aiActors = TimelineCinematicHelper.MarkCinematicActors(director);

        // 3. 获取Timeline全部绑定物体，切换Performance层级
        List<GameObject> boundObjects = TimelineCinematicHelper.GetAllBoundGameObjects(director);
        StartGameFlow.PushActorsToPerformanceLayer(boundObjects);

        // 4. 设置CG相机遮罩
        StartGameFlow.ApplyCGCameraCullingMask(cgCamera, excludeLayers);

        // 【业务前置回调】用户自定义逻辑：怪物传送、激活、导航开关，在Timeline播放前执行
        onCgStart?.Invoke();

        // 5. 订阅结束事件，开始播放Timeline
        director.stopped += (d) =>
        {
            OnCinematicStopped(d, aiActors, boundObjects, cgCamera, onCgFinish);
        };
        director.Play();
    }

    private static void OnCinematicStopped(
        PlayableDirector director,
        List<EnemyAIBase> aiActors,
        List<GameObject> boundObjects,
        Camera cgCamera,
        CinematicFinishCallback onCgFinish)
    {
        director.stopped -= (d) => OnCinematicStopped(d, aiActors, boundObjects, cgCamera, onCgFinish);

        // 1.恢复AI参演标记
        TimelineCinematicHelper.RestoreCinematicActors(aiActors);

        // 2.恢复相机遮罩
        StartGameFlow.RestoreCGCameraCullingMask(cgCamera);

        // 3.恢复物体原始层级
        StartGameFlow.RestoreActorsOriginalLayer();

        //【业务收尾回调】用户自定义收尾逻辑：关门、重置Rig、恢复导航
        onCgFinish?.Invoke();

        // 4.解除全局战斗冻结
        StartGameFlow.EndCG();
    }
}
