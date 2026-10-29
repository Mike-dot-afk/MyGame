using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public static class TimelineCinematicHelper
{
    /// <summary>
    /// 标记本次CG参演的EnemyAIBase，返回本次被修改过的组件列表，结束CG用来复原
    /// </summary>
    public static List<EnemyAIBase> MarkCinematicActors(PlayableDirector director)
    {
        List<EnemyAIBase> modifiedList = new List<EnemyAIBase>();
        if (director == null || director.playableAsset == null)
            return modifiedList;

        TimelineAsset timeline = director.playableAsset as TimelineAsset;
        if (timeline == null) return modifiedList;

        //遍历全部输出轨道，拿到轨道绑定对象
        foreach (var binding in timeline.outputs)
        {
            TrackAsset track = binding.sourceObject as TrackAsset;
            if (track == null) continue;

            Object boundObj = director.GetGenericBinding(track);
            if (boundObj == null) continue;

            GameObject go = null;
            if (boundObj is GameObject g)
                go = g;
            else if (boundObj is Component comp)
                go = comp.gameObject;

            if (go == null) continue;

            EnemyAIBase ai = go.GetComponent<EnemyAIBase>();
            if (ai != null && !ai.ignoreCinematicAnimatorDisable)
            {
                ai.ignoreCinematicAnimatorDisable = true;
                modifiedList.Add(ai);
            }
        }
        return modifiedList;
    }

    /// <summary>
    /// CG结束复原：把本次打上标记的全部改回false
    /// </summary>
    public static void RestoreCinematicActors(List<EnemyAIBase> list)
    {
        if (list == null) return;
        foreach (var ai in list)
        {
            if (ai != null)
            {
                ai.ignoreCinematicAnimatorDisable = false;
            }
        }
        list.Clear();
    }

    /// <summary>
    /// 获取Timeline所有轨道绑定的GameObject列表（包括玩家、敌人、道具等所有绑定对象）。
    /// 用于CG层级隔离：将这些参演角色临时移到Performance层。
    /// </summary>
    public static List<GameObject> GetAllBoundGameObjects(PlayableDirector director)
    {
        List<GameObject> result = new List<GameObject>();
        if (director == null || director.playableAsset == null)
            return result;

        TimelineAsset timeline = director.playableAsset as TimelineAsset;
        if (timeline == null) return result;

        foreach (var binding in timeline.outputs)
        {
            TrackAsset track = binding.sourceObject as TrackAsset;
            if (track == null) continue;

            Object boundObj = director.GetGenericBinding(track);
            if (boundObj == null) continue;

            GameObject go = null;
            if (boundObj is GameObject g)
                go = g;
            else if (boundObj is Component comp)
                go = comp.gameObject;

            if (go != null && !result.Contains(go))
            {
                result.Add(go);
            }
        }
        return result;
    }
}
