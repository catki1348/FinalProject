using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Codex (GPT-6), 2026-10-15. Apply the reviewed 2-2 plan without rebuilding other areas.
public static class DeployStage22Revision
{
    public const string Folder="Assets/@ExternalAssets/@Prefab/Stage2_2/";
    [Serializable] public class Point { public float x,y; }
    [Serializable] public class Platform {
        public string id,kind,prefab;public float x,top,width,height,speed,delay,wait,vanish,respawn;
        public int loop;public Point[] points;
    }
    [Serializable] public class Plan { public Platform[] platforms;public Point start,goal,bossCenter; }
    public static Plan Read()=>JsonUtility.FromJson<Plan>(File.ReadAllText("@Edit/MapDesign_Stage2-2_Revision.json"));
    public static T[] All<T>() where T:Component=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    public static void Set(UnityEngine.Object target,Action<SerializedObject> edit) {
        var so=new SerializedObject(target);edit(so);so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
        if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    static void Record(Transform t){EditorUtility.SetDirty(t);if(PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
    // Crop only the interior of the existing jungle texture. The common texture/importer remains unchanged.
    static Sprite Tile(float height) {
        string path=Folder+"T_Stage22_"+(height==1?"Disappear":"Platform")+".asset";
        var tile=AssetDatabase.LoadAssetAtPath<Sprite>(path);if(tile!=null)return tile;
        var source=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage2_Jungle.png");
        tile=Sprite.Create(source.texture,new Rect(280,264,400,400),new Vector2(.5f,.5f),400/height,0,SpriteMeshType.FullRect,Vector4.zero);
        tile.name=Path.GetFileNameWithoutExtension(path);AssetDatabase.CreateAsset(tile,path);return tile;
    }
    // Reusable behavior belongs to the prefab original; scene instances only hold geometry and external paths.
    static GameObject Prefab(string kind) {
        string title=kind=="normal"?"Normal":kind=="moving"?"Moving":"Disappearing",path=Folder+"P_Stage22_"+title+".prefab";
        bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null;
        var go=exists?PrefabUtility.LoadPrefabContents(path):(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/Platform_Stage2_Jungle.prefab"));
        try {
            go.name="P_Stage22_"+title;go.transform.localScale=Vector3.one;float h=kind=="disappearing"?1:1.05f;
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=Tile(h);sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;sr.size=new Vector2(kind=="disappearing"?1.6f:1.1f,h);
            var col=go.GetComponent<BoxCollider2D>();col.autoTiling=false;col.size=sr.size;col.offset=Vector2.zero;
            if(go.GetComponent<PlatformCore>()==null)go.AddComponent<PlatformCore>();
            if(kind=="moving") {
                var m=go.GetComponent<MovingPlatform>();if(m==null)m=go.AddComponent<MovingPlatform>();
                go.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
                Set(m,so=>{so.FindProperty("path").objectReferenceValue=null;so.FindProperty("speed").floatValue=2;so.FindProperty("startDelay").floatValue=0;so.FindProperty("endpointWait").floatValue=0;so.FindProperty("offscreenSpeedMultiplier").floatValue=1;});
            }
            if(kind=="disappearing") {
                var m=go.GetComponent<DisappearingFoothold>();if(m==null)m=go.AddComponent<DisappearingFoothold>();
                Set(m,so=>{so.FindProperty("vanishDelay").floatValue=.4f;so.FindProperty("respawnDelay").floatValue=2;});
            }
            return PrefabUtility.SaveAsPrefabAsset(go,path);
        }finally{if(exists)PrefabUtility.UnloadPrefabContents(go);else UnityEngine.Object.DestroyImmediate(go);}
    }
    // Absolute boundary destinations make repeated deployment idempotent; each descendant shifts only once.
    static void ShiftDownstream(Dictionary<string,Transform> map) {
        float delta=172-map["Boundary_2_3"].position.x;
        var targets=new[]{map["Section_3"],map["Section_4"],map["Boundary_2_3"],map["Boundary_3_4"],map["Boundary_4_5"]};
        foreach(var t in targets)if(!targets.Any(other=>other!=t && t.IsChildOf(other))){t.position+=Vector3.right*delta;Record(t);}
    }
    [MenuItem("Tools/Deploy/Stage 2-2 Revision")]
    public static void Deploy() {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/Stage2.unity")throw new InvalidOperationException("Stage2 edit mode required");
        var d=Read();if(d.platforms.Length!=18)throw new InvalidOperationException("Expected reviewed 18-platform plan");
        var map=All<Transform>().GroupBy(t=>t.name).ToDictionary(g=>g.Key,g=>g.First());var section=map["Section_2"];
        if(section.lossyScale!=Vector3.one)throw new InvalidOperationException("Section parent must have unit scale");
        ShiftDownstream(map);
        var boss=map["Section_5"];var bz=boss.GetComponentsInChildren<CameraBoundsZone>(true).Single();boss.position+=new Vector3(d.bossCenter.x,d.bossCenter.y,0)-bz.WorldBounds.center;Record(boss);
        if(!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))AssetDatabase.CreateFolder("Assets/@ExternalAssets/@Prefab","Stage2_2");
        var prefabs=new Dictionary<string,GameObject>();foreach(var k in new[]{"normal","moving","disappearing"})prefabs[k]=Prefab(k);
        // Keep original marker, background and path objects so outside scene references stay valid.
        foreach(var p in d.platforms) {
            var old=section.GetComponentsInChildren<Transform>(true).Single(t=>t.name==p.id);UnityEngine.Object.DestroyImmediate(old.gameObject);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabs[p.kind],section);go.name=p.id;
            go.transform.position=new Vector3(p.x,p.top-p.height/2,0);go.transform.localScale=Vector3.one;Record(go.transform);
            Set(go.GetComponent<SpriteRenderer>(),so=>so.FindProperty("m_Size").vector2Value=new Vector2(p.width,p.height));
            Set(go.GetComponent<BoxCollider2D>(),so=>{so.FindProperty("m_Size").vector2Value=new Vector2(p.width,p.height);so.FindProperty("m_Offset").vector2Value=Vector2.zero;});
            if(p.kind=="moving") {
                var path=section.GetComponentsInChildren<PlatformPath>(true).Single(q=>q.name==p.id+"_Path");
                if(path.PointCount!=p.points.Length)throw new InvalidOperationException("Path point count differs: "+p.id);
                for(int i=0;i<p.points.Length;i++){path.transform.GetChild(i).position=new Vector3(p.points[i].x,p.points[i].y-p.height/2,0);Record(path.transform.GetChild(i));}
                Set(path,so=>so.FindProperty("closedLoop").boolValue=p.loop==0);
                Set(go.GetComponent<MovingPlatform>(),so=>{so.FindProperty("path").objectReferenceValue=path;so.FindProperty("loopMode").enumValueIndex=p.loop;});
            }
        }
        map["ZoneStartPoint_2-2"].position=new Vector3(d.start.x,d.start.y,0);Record(map["ZoneStartPoint_2-2"]);
        map["ZoneEndPoint_2-2"].position=new Vector3(d.goal.x,d.goal.y,0);Record(map["ZoneEndPoint_2-2"]);
        var stage=All<StageManager>().Single();var stageData=new SerializedObject(stage);
        var nextZone=(CameraBoundsZone)stageData.FindProperty("zoneCameraBounds").GetArrayElementAtIndex(2).objectReferenceValue;
        Set(map["ZoneEndPoint_2-2"].GetComponent<ZoneEndPoint>(),so=>{so.FindProperty("nextZone").objectReferenceValue=nextZone;so.FindProperty("cameraFollow").objectReferenceValue=stageData.FindProperty("cameraFollow").objectReferenceValue;});
        Set(map["ZoneEndPoint_2-2"].GetComponent<BoxCollider2D>(),so=>so.FindProperty("m_IsTrigger").boolValue=true);
        var zone=section.GetComponent<CameraBoundsZone>();
        if(zone==null)zone=section.GetComponentsInChildren<CameraBoundsZone>(true).Single();
        Set(zone,so=>{so.FindProperty("size").vector2Value=new Vector2(122,16);so.FindProperty("leftBoundary").objectReferenceValue=map["Boundary_1_2"];so.FindProperty("rightBoundary").objectReferenceValue=map["Boundary_2_3"];});
        // Boundaries supply X bounds; centering the section also centers its background without moving platforms.
        var background=map["ZoneBackground_2-2"];
        background.position=new Vector3(111,0,background.position.z);Record(background);
        Physics2D.SyncTransforms();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("2-2 revision saved: 18 tiled prefabs, 5 connected movers, 3 staircase disappearing platforms.");
    }
}
