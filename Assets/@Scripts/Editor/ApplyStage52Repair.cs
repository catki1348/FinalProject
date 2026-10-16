using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Codex (GPT-6), 2026-10-16. Apply only reviewed platform/path changes; preserve content, gates, drops and other areas.
public static class ApplyStage52Repair
{
    public static void Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/Stage5.unity" || scene.isDirty)throw new InvalidOperationException("Saved Stage5 edit mode required");
        var d=JsonUtility.FromJson<PrepareStage52.Design>(File.ReadAllText("@Edit/Stage52_UnityPreparation.json"));
        var root=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Single(t=>t.name=="Generated_5-2");
        var map=root.GetComponentsInChildren<Transform>(true).GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());
        if(root.GetComponentsInChildren<PlatformCore>(true).Length!=67)throw new InvalidOperationException("Unexpected platform baseline");
        foreach(var p in d.platforms)
        {
            var t=map[p.id];var sr=t.GetComponent<SpriteRenderer>();var box=t.GetComponent<BoxCollider2D>();
            if(!PrefabUtility.IsPartOfPrefabInstance(t) || t.lossyScale!=Vector3.one || Vector2.Distance(sr.size,box.size)>.001f)throw new InvalidOperationException("Unexpected platform geometry: "+p.id);
            if(p.points.Length==0)t.position=root.position+new Vector3(p.x,p.y-.25f,0);
            else
            {
                var moving=t.GetComponent<MovingPlatform>();var so=new SerializedObject(moving);var path=(PlatformPath)so.FindProperty("path").objectReferenceValue;
                if(path==null || path.PointCount!=p.points.Length)throw new InvalidOperationException("Unexpected path: "+p.id);
                for(int i=0;i<p.points.Length;i++)
                {
                    var q=p.points[i];var point=path.transform.GetChild(i);point.position=root.position+new Vector3(q.x,q.y-.25f,0);EditorUtility.SetDirty(point);
                }
                t.position=path.GetPoint(0);
                so.FindProperty("speed").floatValue=p.speed;so.FindProperty("startDelay").floatValue=p.delay;so.FindProperty("endpointWait").floatValue=p.wait;
                so.FindProperty("synchronizedCycleDuration").floatValue=p.syncPeriod;so.FindProperty("offscreenSpeedMultiplier").floatValue=1;
                so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.RecordPrefabInstancePropertyModifications(moving);EditorUtility.SetDirty(moving);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(t);EditorUtility.SetDirty(t);
        }
        Physics2D.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("5-2 deterministic repair saved: 4-second coupled chains, B4 separation, F1 headroom, bounded lift waits.");
    }
}
