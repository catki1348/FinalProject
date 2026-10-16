using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Codex (GPT-6), 2026-10-15. Reviewed 1-4 revision deployment, preserving other areas.
public static class DeployStage14Revision
{
    const string Base="Assets/@ExternalAssets/@Prefab/";
    public const string Folder=Base+"Stage1_4_Revision/";
    [Serializable] public class PlatformData {public string id,type;public float x,lowerY,upperY,width,thickness,moveDistance,moveSpeed,startDelay,endpointWait;}
    [Serializable] public class BoundsData {public float minX,maxX,minY,maxY;}
    [Serializable] public class Data {public string schema;public PlatformData[] platforms;public BoundsData bounds;}
    public static Data Read() => JsonUtility.FromJson<Data>(File.ReadAllText("@Edit/MapDesign_Stage1-4_Revision.json"));

    // Scene-only references are overrides; reusable size/behavior is authored in each variant original.
    public static void Set(UnityEngine.Object target,Action<SerializedObject> configure)
    {
        var so=new SerializedObject(target);configure(so);so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(target);
        if(PrefabUtility.IsPartOfPrefabInstance(target))PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }
    static GameObject Variant(string name,string source,Action<GameObject> configure)
    {
        string path=Folder+name+".prefab";bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null;
        var go=exists?PrefabUtility.LoadPrefabContents(path):(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try{configure(go);return PrefabUtility.SaveAsPrefabAsset(go,path);}
        finally{if(exists)PrefabUtility.UnloadPrefabContents(go);else UnityEngine.Object.DestroyImmediate(go);}
    }
    // Keep the existing forest pixels, but reserve both end caps so a partial repeat cannot cut an end.
    static Sprite Tile()
    {
        const string path=Folder+"T_Stage14_ForestEndCaps.asset";
        var capped=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(capped!=null)return capped;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Base+"P_Stage1_GrassStone_Normal.prefab");
        var source=prefab.GetComponent<SpriteRenderer>().sprite;
        capped=Sprite.Create(source.texture,source.rect,source.pivot/source.rect.size,source.pixelsPerUnit,0,SpriteMeshType.FullRect,new Vector4(96,0,96,0));
        capped.name="T_Stage14_ForestEndCaps";AssetDatabase.CreateAsset(capped,path);return capped;
    }

    // Correct only appearance on the 29 reusable originals; retain all scene geometry and paths.
    public static string RestorePrefabAppearance()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/Stage1.unity")throw new InvalidOperationException("Stage1 edit mode required");
        int count=0;
        foreach(var p in Read().platforms) {
            string path=Folder+"P_Stage14_"+p.id+"_"+p.type+".prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try {
                var renderer=root.GetComponent<SpriteRenderer>();
                // Original prefab pixels with metadata-only end caps; keep this on the variant original.
                renderer.sprite=Tile();
                PrefabUtility.SaveAsPrefabAsset(root,path);count++;
            }
            finally {PrefabUtility.UnloadPrefabContents(root);}
        }
        foreach(var r in scene.GetRootGameObjects())foreach(var core in r.GetComponentsInChildren<PlatformCore>(true)) {
            if(!core.name.StartsWith("Platform_1-4_"))continue;
            var renderer=core.GetComponent<SpriteRenderer>();var so=new SerializedObject(renderer);
            var sprite=so.FindProperty("m_Sprite");
            if(sprite.prefabOverride)PrefabUtility.RevertPropertyOverride(sprite,InteractionMode.AutomatedAction);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Restored existing forest prefab appearance on "+count+" platform variants";
    }
    // Save a complete reusable platform, with matching renderer and collision dimensions.
    static GameObject PlatformPrefab(PlatformData p,Sprite tile)
    {
        return Variant("P_Stage14_"+p.id+"_"+p.type,Base+"P_Stage1_GrassStone_"+p.type+".prefab",go=>{
            go.layer=LayerMask.NameToLayer("Ground");
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=tile;sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;sr.size=new Vector2(p.width,p.thickness);
            var box=go.GetComponent<BoxCollider2D>();box.autoTiling=false;box.size=sr.size;box.offset=Vector2.zero;go.transform.localScale=Vector3.one;
            if(p.type=="Moving")Set(go.GetComponent<MovingPlatform>(),so=>{
                so.FindProperty("path").objectReferenceValue=null;so.FindProperty("speed").floatValue=p.moveSpeed;
                so.FindProperty("startDelay").floatValue=p.startDelay;so.FindProperty("endpointWait").floatValue=p.endpointWait;
                so.FindProperty("loopMode").enumValueIndex=1;
                var points=so.FindProperty("localWaypoints");points.arraySize=2;points.GetArrayElementAtIndex(0).vector2Value=Vector2.zero;points.GetArrayElementAtIndex(1).vector2Value=Vector2.up*p.moveDistance;
                so.FindProperty("offscreenSpeedMultiplier").floatValue=1;
            });
            if(p.type=="Disappear")Set(go.GetComponent<DisappearingFoothold>(),so=>{so.FindProperty("vanishDelay").floatValue=.9f;so.FindProperty("respawnDelay").floatValue=2;});
            if(p.type=="Blink")Set(go.GetComponent<BlinkingFoothold>(),so=>{
                so.FindProperty("visibleDuration").floatValue=2.4f;so.FindProperty("invisibleDuration").floatValue=1.2f;so.FindProperty("startDelay").floatValue=((int.Parse(p.id.Substring(1))-11)/2)*.45f;
            });
            if(p.type=="Damage")Set(go.GetComponent<DamageFoothold>(),so=>{
                so.FindProperty("safeDuration").floatValue=2.4f;so.FindProperty("activeDuration").floatValue=1.6f;
                so.FindProperty("cycleOffset").floatValue=((int.Parse(p.id.Substring(1))-21)/2)*.5f;
                so.FindProperty("damageDelay").floatValue=.5f;so.FindProperty("damageAmount").intValue=8;so.FindProperty("damageInterval").floatValue=.5f;
            });
        });
    }
    // Stationary path objects are siblings of movers, never children carried along with them.
    static PlatformPath Path(Transform parent,string name,Vector3 a,Vector3 b)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);var path=go.AddComponent<PlatformPath>();
        var first=new GameObject("Point_0").transform;first.SetParent(go.transform,false);first.position=a;
        var last=new GameObject("Point_1").transform;last.SetParent(go.transform,false);last.position=b;return path;
    }

    [MenuItem("Tools/Deploy/Stage 1-4 Revision")]
    public static void Deploy()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying || scene.path!="Assets/Scenes/Stage1.unity")throw new InvalidOperationException("Stage1 edit mode required");
        var data=Read();if(data.schema!="stage1_4_revision_20261015" || data.platforms.Length!=29)throw new InvalidOperationException("Unknown revision");
        var map=new Dictionary<string,Transform>();foreach(var r in scene.GetRootGameObjects())foreach(var t in r.GetComponentsInChildren<Transform>(true))if(!map.ContainsKey(t.name))map.Add(t.name,t);
        var section=map["Section_4"];var start=map["ZoneStartPoint_1-4"];var end=map["ZoneEndPoint_1-4"];
        var zone=section.GetComponentsInChildren<CameraBoundsZone>(true).Single();var background=section.GetComponentsInChildren<ZoneBackground>(true).Single();
        var stage=map["StageManager"].GetComponent<StageManager>();var player=(Transform)new SerializedObject(stage).FindProperty("player").objectReferenceValue;
        var left=map["Boundary_3_4"];var right=map["Boundary_4_5"];float origin=left.position.x-data.bounds.minX;
        var keep=new HashSet<GameObject>{start.gameObject,end.gameObject,zone.gameObject,background.gameObject};
        var remove=new List<GameObject>();foreach(Transform t in section)if(!keep.Contains(t.gameObject))remove.Add(t.gameObject);foreach(var go in remove)UnityEngine.Object.DestroyImmediate(go);
        section.position=new Vector3(origin,0,0);section.localScale=Vector3.one;
        float rightX=origin+data.bounds.maxX;float midX=(left.position.x+rightX)*.5f;
        left.position=new Vector3(left.position.x,12,0);right.position=new Vector3(rightX,12,0);
        foreach(var edge in new[]{left,right}){edge.localScale=Vector3.one;Set(edge.GetComponent<BoxCollider2D>(),so=>so.FindProperty("m_Size").vector2Value=new Vector2(.3f,30));}
        zone.transform.position=new Vector3(midX,12,0);
        Set(zone,so=>{so.FindProperty("size").vector2Value=new Vector2(rightX-left.position.x,30);so.FindProperty("orthographicSize").floatValue=5;so.FindProperty("leftBoundary").objectReferenceValue=left;so.FindProperty("rightBoundary").objectReferenceValue=right;});
        // Only the dedicated ZoneBackground renderer covers the enlarged bounds.
        if(zone.TryGetComponent<SpriteRenderer>(out var oldBackdrop))oldBackdrop.enabled=false;
        Set(background,so=>so.FindProperty("zone").objectReferenceValue=zone);
        var playerBox=player.GetComponent<Collider2D>();start.position=new Vector3(origin,player.position.y-playerBox.bounds.min.y+.02f,0);
        var last=data.platforms.Last();end.position=new Vector3(origin+last.x+last.width/2-.8f,last.lowerY+1,0);
        Set(end.GetComponent<BoxCollider2D>(),so=>{so.FindProperty("m_Size").vector2Value=new Vector2(.8f,3);so.FindProperty("m_IsTrigger").boolValue=true;});
        PrefabUtility.RecordPrefabInstancePropertyModifications(start);PrefabUtility.RecordPrefabInstancePropertyModifications(end);
        if(!AssetDatabase.IsValidFolder(Folder.TrimEnd('/')))AssetDatabase.CreateFolder(Base.TrimEnd('/'),"Stage1_4_Revision");
        var tile=Tile();var generated=new GameObject("Generated_1-4_Revision").transform;generated.SetParent(section,false);generated.position=new Vector3(origin,0,0);
        var content=new GameObject("Content_1-4").transform;content.SetParent(generated,false);
        foreach(var p in data.platforms) {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(PlatformPrefab(p,tile),content);
            go.name="Platform_1-4_"+p.id+"_"+p.type;go.transform.localPosition=new Vector3(p.x,p.lowerY-p.thickness/2,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
            if(p.type=="Moving") {
                var path=Path(content,"Path_1-4_"+p.id,go.transform.position,go.transform.position+Vector3.up*p.moveDistance);
                Set(go.GetComponent<MovingPlatform>(),so=>so.FindProperty("path").objectReferenceValue=path);
            }
        }
        for(int i=0;i<2;i++) {
            string platform=i==0?"P06":"P18";var p=data.platforms.Single(x=>x.id==platform);
            var prefab=Variant("P_Stage14_Patrol_"+(i+1),Base+"P_Stage1_GroundPatrol.prefab",go=>{
                Set(go.GetComponent<PathPatrolPattern>(),so=>{so.FindProperty("path").objectReferenceValue=null;so.FindProperty("moveSpeed").floatValue=i==0?1.2f:1.6f;});
            });
            var monster=(GameObject)PrefabUtility.InstantiatePrefab(prefab,content);monster.name="Monster_1-4_"+(i+1);
            monster.GetComponent<MonsterScaleSetter>().ApplyScale(player);
            monster.transform.position=new Vector3(origin+p.x-.8f,p.upperY,0);Physics2D.SyncTransforms();
            var box=monster.GetComponent<Collider2D>();monster.transform.position+=Vector3.up*(p.upperY+.02f-box.bounds.min.y);
            var path=Path(content,"PatrolPath_1-4_"+(i+1),new Vector3(origin+p.x-.8f,p.upperY,0),new Vector3(origin+p.x+.8f,p.upperY,0));
            Set(monster.GetComponent<PathPatrolPattern>(),so=>so.FindProperty("path").objectReferenceValue=path);
            PrefabUtility.RecordPrefabInstancePropertyModifications(monster.transform);
        }
        var fall=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base+"P_OutOfBounds_Zone.prefab"),content);
        fall.name="OutOfBoundsTrigger_1-4";fall.transform.position=new Vector3(midX,-6.5f,0);
        Set(fall.GetComponent<BoxCollider2D>(),so=>so.FindProperty("m_Size").vector2Value=new Vector2(rightX-left.position.x,3));PrefabUtility.RecordPrefabInstancePropertyModifications(fall.transform);
        Set(generated.gameObject.AddComponent<AreaContentStartManager>(),so=>{so.FindProperty("stage").objectReferenceValue=stage;so.FindProperty("content").objectReferenceValue=content.gameObject;so.FindProperty("area").intValue=4;});
        // Keep the boss below every upper-row area, including the deep 1-2 descent.
        var bossSection=map["Section_5"];var bossZone=bossSection.GetComponentsInChildren<CameraBoundsZone>(true).Single();
        var upperZones=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CameraBoundsZone>(true)).Where(z=>!z.transform.IsChildOf(bossSection)).ToArray();
        float minX=upperZones.Min(z=>z.WorldBounds.min.x),bottom=upperZones.Min(z=>z.WorldBounds.min.y);
        Vector3 destination=new Vector3((minX+rightX)/2,bottom-bossZone.WorldBounds.extents.y-5,0);
        bossSection.position+=destination-bossZone.WorldBounds.center;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("Stage1-4 revision: 29 platforms / 3 distinct lifts / 2 patrols / gate on area entry; origin="+origin);
    }
}
