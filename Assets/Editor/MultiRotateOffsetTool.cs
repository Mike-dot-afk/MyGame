using UnityEditor;
using UnityEngine;

public static class MultiRotateOffsetTool
{
    [MenuItem("Tools/Transform/选中物体 Y轴旋转+180(世界)", false, 100)]
    static void AddRotateY180World()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;
            Undo.RecordObject(go.transform, "Y轴+180度");
            go.transform.Rotate(0, 180f, 0, Space.World);
        }
    }

    [MenuItem("Tools/Transform/选中物体 Y轴旋转+180(本地)", false, 101)]
    static void AddRotateY180Local()
    {
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;
            Undo.RecordObject(go.transform, "Y轴本地+180度");
            go.transform.Rotate(0, 180f, 0, Space.Self);
        }
    }
}
