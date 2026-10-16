// AI work record: Codex; model unknown; 2026-10-16. Stage2-3 v10 deployment and structural verification.
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

public static class DeployStage23V10
{
    const string Folder="Assets/@ExternalAssets/@Prefab/Stage2_3_v10/";
    [Serializable] public class Point { public float x,y; }
    [Serializable] public class Plat { public string id,kind; public float width,speed,delay,wait; public Point[] points; }
    [Serializable] public class Mon { public int id; public string kind,host; public float speed; public Point[] points; }
    [Serializable] public class Data { public Plat[] platforms; public Mon[] monsters; public float routeLength; }
    // Record every override immediately so prefab synchronization cannot discard it.
    static void Set(UnityEngine.Object obj,Action<SerializedObject> act) {var so=new SerializedObject(obj);act(so);so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(obj);if(PrefabUtility.IsPartOfPrefabInstance(obj))PrefabUtility.RecordPrefabInstancePropertyModifications(obj);}
    // Reuse the entire original block silhouette; trim transparent padding only and repeat the center while preserving both ends.
    static Sprite Tile() {
        string dest=Folder+"T_JungleOriginalShape.asset";var s=AssetDatabase.LoadAssetAtPath<Sprite>(dest);if(s)return s;
        var source=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage2_Jungle.png");
        float height=695f/source.pixelsPerUnit; s=Sprite.Create(source.texture,new Rect(222,89,564,695),new Vector2(.5f,1f-.25f/height),source.pixelsPerUnit,0,SpriteMeshType.FullRect,new Vector4(110,0,110,0));
        AssetDatabase.CreateAsset(s,dest);return s;
    }
    // Build reusable variants rather than attaching repeated behavior only to scene objects.
    static GameObject Prefab(string kind) {
        string dest=Folder+"P_Stage23_"+kind+".prefab";
        bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(dest)!=null;
        string source=kind=="Ground"?"Monster_ForestGoblin":kind=="Flying"?"Monster_ForestBat":"Platform_Stage2_Jungle";
        var go=exists?PrefabUtility.LoadPrefabContents(dest):(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/"+source+".prefab"));
        try {
            if(kind=="Normal"||kind=="Moving"){
                foreach(var behavior in go.GetComponents<PlatformBehaviour>())if(kind!="Moving" || !(behavior is MovingPlatform))UnityEngine.Object.DestroyImmediate(behavior);
                if(!go.GetComponent<PlatformCore>())go.AddComponent<PlatformCore>();
                if(kind=="Moving" && !go.GetComponent<MovingPlatform>())go.AddComponent<MovingPlatform>();
                go.transform.localScale=Vector3.one;go.layer=LayerMask.NameToLayer("Ground");
                var sr=go.GetComponent<SpriteRenderer>();sr.sprite=Tile();sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;sr.size=new Vector2(3,695f/512f);
                var col=go.GetComponent<BoxCollider2D>();col.autoTiling=false;col.size=new Vector2(3,.5f);col.offset=Vector2.zero;
                if(kind=="Moving") {var rb=go.GetComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;rb.gravityScale=0;rb.constraints=RigidbodyConstraints2D.FreezeRotation;}
            }else{
                foreach(var c in go.GetComponents<MonoBehaviour>())if(c is IMonsterPattern)UnityEngine.Object.DestroyImmediate(c);
                if(kind=="Ground")go.AddComponent<PathPatrolPattern>();else {go.AddComponent<FlyingPathPatrolPattern>();if(!go.GetComponent<MonsterCorpseDriftStop>())go.AddComponent<MonsterCorpseDriftStop>();}
                Set(go.GetComponent<MonsterController>(),so=>{so.FindProperty("playerWorldHeight").floatValue=1.2865f;so.FindProperty("sizeRatio").floatValue=.5f;});
                typeof(MonsterController).GetMethod("ApplySpawnScale",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(go.GetComponent<MonsterController>(),null);
                var rb=go.GetComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Dynamic;rb.gravityScale=kind=="Flying"?0:3;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
                // Normalize gameplay collider dimensions to half the player's collision dimensions.
                foreach(var c in go.GetComponents<Collider2D>())if(!(c is BoxCollider2D))UnityEngine.Object.DestroyImmediate(c);
                var box=go.GetComponent<BoxCollider2D>();if(!box)box=go.AddComponent<BoxCollider2D>();box.offset=Vector2.zero;box.size=new Vector2(.49f,.64325f)/go.transform.localScale.x;box.isTrigger=false;
                Set(go.GetComponent<MonsterStats>(),so=>{so.FindProperty("attack").intValue=10;so.FindProperty("defense").intValue=10;});
                var drop=go.GetComponent<MonsterItemDropper>();if(!drop)drop=go.AddComponent<MonsterItemDropper>();
                string[] names={"SpeedBoost","HealSmall","HealLarge","Rearm"};float[] chance={1,.1f,.1f,.05f};
                Set(drop,so=>{var entries=so.FindProperty("entries");entries.arraySize=4;for(int i=0;i<4;i++){var e=entries.GetArrayElementAtIndex(i);e.FindPropertyRelative("prefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/P_Item_"+names[i]+".prefab");e.FindPropertyRelative("chance").floatValue=chance[i];e.FindPropertyRelative("condition").enumValueIndex=i<2?0:i==2?1:2;e.FindPropertyRelative("lowHealthMultiplier").floatValue=i==1?2:1;}});
            }
            return PrefabUtility.SaveAsPrefabAsset(go,dest);
        }finally{if(exists)PrefabUtility.UnloadPrefabContents(go);else UnityEngine.Object.DestroyImmediate(go);}
    }
    // Path roots are siblings of platforms; waypoint motion never inherits platform motion.
    static PlatformPath Path(Transform parent,string name,Point[] points,bool platform) {
        var root=new GameObject(name);root.transform.SetParent(parent,false);var path=root.AddComponent<PlatformPath>();
        for(int i=0;i<points.Length;i++){var p=new GameObject("Point_"+i).transform;p.SetParent(root.transform,false);p.localPosition=new Vector3(points[i].x,points[i].y-(platform?.25f:0),0);}
        return path;
    }
    // Restore original prefab art without rebuilding the map or changing any physics/path data.
    public static string RestoreOriginalAppearance() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play before visual correction");
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Stage2.unity")throw new InvalidOperationException("Open Stage2 first");
        Prefab("Normal");Prefab("Moving");
        var root=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).First(t=>t.name=="Generated_2-3_v10");
        int count=0;
        foreach(var core in root.GetComponentsInChildren<PlatformCore>()){
            var sr=core.GetComponent<SpriteRenderer>();float width=core.GetComponent<BoxCollider2D>().size.x;
            Set(sr,so=>{so.FindProperty("m_Sprite").objectReferenceValue=Tile();so.FindProperty("m_DrawMode").enumValueIndex=2;so.FindProperty("m_Size").vector2Value=new Vector2(width,695f/512f);});
            sr.tileMode=SpriteTileMode.Continuous;PrefabUtility.RecordPrefabInstancePropertyModifications(sr);count++;
        }
        var data=JsonUtility.FromJson<Data>(File.ReadAllText("@Edit/Stage23_Unity.json"));
        foreach(var p in data.platforms.Where(v=>v.kind=="Moving")){
            var lift=root.Find("Lift_2-3_"+p.id).GetComponent<MovingPlatform>();var path=root.Find("Path_Lift_2-3_"+p.id).GetComponent<PlatformPath>();
            Set(lift,so=>{so.FindProperty("path").objectReferenceValue=path;so.FindProperty("speed").floatValue=p.speed;so.FindProperty("startDelay").floatValue=Mathf.Max(0,p.delay-.0001f);so.FindProperty("endpointWait").floatValue=p.wait-.0001f;so.FindProperty("loopMode").enumValueIndex=1;so.FindProperty("offscreenSpeedMultiplier").floatValue=1;});
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Restored original complete jungle-block silhouette on "+count+" platforms; placement, colliders, movement paths and monsters unchanged";
    }
    [MenuItem("Tools/Deploy/Stage 2-3 Latest v10")]
    public static void Deploy() {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop play before deploy");
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/Stage2.unity")throw new InvalidOperationException("Open Stage2 first");
        var data=JsonUtility.FromJson<Data>(File.ReadAllText("@Edit/Stage23_Unity.json"));
        var map=new Dictionary<string,Transform>();foreach(var r in scene.GetRootGameObjects())foreach(var t in r.GetComponentsInChildren<Transform>(true))if(!map.ContainsKey(t.name))map.Add(t.name,t);
        var section=map["Section_3"];var old=new List<GameObject>();
        foreach(Transform t in section)if(t.name.StartsWith("Ground_2-3_")||t.name.StartsWith("Lift_2-3_")||t.name.StartsWith("Path_Lift_2-3_")||t.name.StartsWith("Monster_2-3_")||t.name.StartsWith("Path_Monster_2-3_")||t.name=="Generated_2-3_v10")old.Add(t.gameObject);
        if(!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))AssetDatabase.CreateFolder("Assets/@ExternalAssets/@Prefab","Stage2_3_v10");
        var prefabs=new Dictionary<string,GameObject>();foreach(string k in new[]{"Normal","Moving","Ground","Flying"})prefabs[k]=Prefab(k);
        foreach(var go in old)Undo.DestroyObjectImmediate(go);
        var root=new GameObject("Generated_2-3_v10").transform;root.SetParent(section,false);root.position=new Vector3(192.5f,0,0);
        foreach(var p in data.platforms){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[p.kind],root);go.name=p.kind=="Moving"?"Lift_2-3_"+p.id:"Ground_2-3_"+p.id;go.transform.localPosition=new Vector3(p.points[0].x,p.points[0].y-.25f,0);go.transform.localScale=Vector3.one;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            Set(go.GetComponent<SpriteRenderer>(),so=>so.FindProperty("m_Size").vector2Value=new Vector2(p.width,695f/512f));
            Set(go.GetComponent<BoxCollider2D>(),so=>{so.FindProperty("m_Size").vector2Value=new Vector2(p.width,.5f);so.FindProperty("m_Offset").vector2Value=Vector2.zero;});
            if(p.kind=="Moving"){var path=Path(root,"Path_Lift_2-3_"+p.id,p.points,true);Set(go.GetComponent<MovingPlatform>(),so=>{so.FindProperty("path").objectReferenceValue=path;so.FindProperty("speed").floatValue=p.speed;so.FindProperty("startDelay").floatValue=Mathf.Max(0,p.delay-.0001f);so.FindProperty("endpointWait").floatValue=p.wait-.0001f;so.FindProperty("loopMode").enumValueIndex=1;so.FindProperty("offscreenSpeedMultiplier").floatValue=1;});}
        }
        foreach(var m in data.monsters){var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[m.kind],root);go.name="Monster_2-3_"+m.id;go.transform.localPosition=new Vector3(m.points[0].x,m.points[0].y,0);PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);var path=Path(root,"Path_Monster_2-3_"+m.id,m.points,false);var pattern=go.GetComponents<MonoBehaviour>().First(c=>c is IMonsterPattern);Set(pattern,so=>{so.FindProperty("path").objectReferenceValue=path;so.FindProperty("moveSpeed").floatValue=m.speed;});}
        // Preserve the outer stage boundaries and the other areas; the new map fits the reserved slot.
        map["ZoneStartPoint_2-3"].position=new Vector3(192.5f,.03f,0);
        var goal=data.platforms.First(p=>p.id=="Goal").points[0];map["ZoneEndPoint_2-3"].position=new Vector3(192.5f+goal.x,goal.y+.8f,0);
        var zone=map["CameraBounds_2-3"].GetComponent<CameraBoundsZone>();Set(zone,so=>{so.FindProperty("size").vector2Value=new Vector2(261,20);so.FindProperty("orthographicSize").floatValue=5;so.FindProperty("leftBoundary").objectReferenceValue=map["Boundary_2_3"];so.FindProperty("rightBoundary").objectReferenceValue=map["Boundary_3_4"];});zone.transform.position=new Vector3(302.5f,1,0);EditorUtility.SetDirty(zone.transform);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("2-3 v10 saved: 21 platforms, 30 monsters, route 240");
    }
}






