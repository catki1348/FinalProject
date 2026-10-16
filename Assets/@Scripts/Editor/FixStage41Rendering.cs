using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Repair visual alpha holes in 4-1 without changing gameplay geometry.
public static class FixStage41Rendering
{
    const string Base="Assets/@ExternalAssets/@Prefab/Platform_Stage4_Dungeon.prefab";
    const string Folder="Assets/@ExternalAssets/@Prefab/Stage4_1/";
    public const string PrefabPath=Folder+"P_Stage41_VisibleDungeon.prefab";
    const string SpritePath=Folder+"T_Stage41_SolidTile.asset";

    // Reuse pixels from the original dungeon prefab. The sampled interior has no missing alpha.
    public static Sprite Tile()
    {
        var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);if(sprite!=null)return sprite;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Base).GetComponent<SpriteRenderer>().sprite;
        var raw=new Texture2D(2,2);raw.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(source)));
        try {
            for(int y=330;y<700;y++)for(int x=320;x<690;x++)if(raw.GetPixel(x,y).a<.95f)throw new InvalidOperationException("Source interior changed: translucent tile");
        }finally{UnityEngine.Object.DestroyImmediate(raw);}
        sprite=Sprite.Create(source.texture,new Rect(320,330,370,370),new Vector2(.5f,.5f),740,0,SpriteMeshType.FullRect,new Vector4(32,0,32,0));
        sprite.name="T_Stage41_SolidTile";AssetDatabase.CreateAsset(sprite,SpritePath);return sprite;
    }
    // Configure the reusable original before replacing scene instances; shared 4-2/3/4 assets stay intact.
    public static GameObject Prefab()
    {
        bool exists=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)!=null;
        var go=exists?PrefabUtility.LoadPrefabContents(PrefabPath):(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Base));
        try {
            var sr=go.GetComponent<SpriteRenderer>();sr.sprite=Tile();sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;
            sr.size=new Vector2(3,.5f);go.transform.localScale=Vector3.one;
            var box=go.GetComponent<BoxCollider2D>();box.autoTiling=false;box.size=sr.size;box.offset=Vector2.zero;
            return PrefabUtility.SaveAsPrefabAsset(go,PrefabPath);
        }finally{if(exists)PrefabUtility.UnloadPrefabContents(go);else UnityEngine.Object.DestroyImmediate(go);}
    }
    // Include paths and added trap children in the preservation check, not just platform positions.
    static string Snapshot(GameObject go)
    {
        var box=go.GetComponent<BoxCollider2D>();
        // SpriteTilingProperty is renderer metadata mirrored on BoxCollider2D; exclude it.
        return go.name+"|"+go.transform.position.ToString("F5")+"|"+go.transform.lossyScale.ToString("F5")+"|"+box.size.ToString("F5")+"|"+box.offset.ToString("F5")+"|"+box.enabled+"|"+box.isTrigger+"|"+box.sharedMaterial+"|"+
            string.Join("|",go.GetComponentsInChildren<MonoBehaviour>(true).Select(b=>EditorJsonUtility.ToJson(b)).ToArray());
    }
    public static GameObject[] Platforms()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SpriteRenderer>(true)).Where(r=>r.name.StartsWith("Platform_4-1_")).OrderBy(r=>r.name).Select(r=>r.gameObject).ToArray();
    }
    // Keep every compatible component/override while changing only the rendering source prefab.
    public static string Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.isPlaying||scene.path!="Assets/Scenes/Stage4.unity")throw new InvalidOperationException("Stage4 edit mode required");
        var platforms=Platforms();if(platforms.Length!=22)throw new InvalidOperationException("Expected all 22 platforms before repair");
        var before=platforms.Select(Snapshot).ToArray();var prefab=Prefab();
        var settings=new PrefabReplacingSettings{objectMatchMode=ObjectMatchMode.ByHierarchy,prefabOverridesOptions=PrefabOverridesOptions.KeepAllPossibleOverrides,changeRootNameToAssetName=false};
        for(int i=0;i<platforms.Length;i++) {
            var go=platforms[i];var oldSize=go.GetComponent<SpriteRenderer>().size;
            PrefabUtility.ReplacePrefabAssetOfPrefabInstance(go,prefab,settings,InteractionMode.AutomatedAction);
            var sr=go.GetComponent<SpriteRenderer>();
            var sprite=new SerializedObject(sr).FindProperty("m_Sprite");
            if(sprite.prefabOverride)PrefabUtility.RevertPropertyOverride(sprite,InteractionMode.AutomatedAction);
            sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;sr.size=oldSize;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
            if(Snapshot(go)!=before[i])throw new InvalidOperationException("Gameplay state changed: "+go.name+". Scene has not been saved.");
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "22 platforms repaired; positions, colliders, behaviors, path/trap references unchanged";
    }

    // Render actual sprite material into a transparent target to detect any internal missing pixels.
    public static string VerifyPixels()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
        var main=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var platforms=Platforms();
        int i=0;string path;do{path="Assets/Scenes/Test"+i.ToString("D3")+".unity";i++;}while(File.Exists(path));
        var test=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);EditorSceneManager.SaveScene(test,path);
        var results=new List<string>();
        try {
            var camera=new GameObject("RenderProbeCamera").AddComponent<Camera>();camera.orthographic=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.clear;camera.cullingMask=1<<31;camera.transform.position=new Vector3(0,0,-10);
            new GameObject("Main Light").AddComponent<Light>().type=LightType.Directional;
            var probe=new GameObject("SpriteProbe");probe.layer=31;var sr=probe.AddComponent<SpriteRenderer>();
            foreach(var go in platforms) {
                var source=go.GetComponent<SpriteRenderer>();
                if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)!=PrefabPath || source.sprite!=Tile())throw new InvalidOperationException("Wrong prefab/sprite: "+go.name);
                sr.sprite=source.sprite;sr.sharedMaterial=source.sharedMaterial;sr.drawMode=SpriteDrawMode.Tiled;sr.tileMode=SpriteTileMode.Continuous;sr.size=source.size;
                int width=Mathf.CeilToInt(128*sr.size.x/sr.size.y);camera.orthographicSize=sr.size.y/2;camera.aspect=(float)width/128;
                var rt=new RenderTexture(width,128,24,RenderTextureFormat.ARGB32);var old=RenderTexture.active;var image=new Texture2D(width,128,TextureFormat.RGBA32,false);
                try {
                    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,128),0,0);image.Apply();
                    var pixels=image.GetPixels32();int missing=0;
                    for(int y=2;y<126;y++)for(int x=2;x<width-2;x++)if(pixels[y*width+x].a<230)missing++;
                    if(missing>0)throw new InvalidOperationException(go.name+" missing rendered pixels="+missing);
                    if(Vector2.Distance(go.GetComponent<BoxCollider2D>().size,source.size)>.001f)throw new InvalidOperationException("Collider size mismatch");
                    results.Add("PASS "+go.name+" / full alpha coverage / dimensions / prefab");
                }finally{camera.targetTexture=null;RenderTexture.active=old;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
            }
            File.WriteAllLines("@Edit/Stage41_RenderRepair_20261015.txt",results);return "PASS: "+results.Count+" platform render probes; no interior missing pixels";
        }finally{EditorSceneManager.CloseScene(test,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(main);AssetDatabase.DeleteAsset(path);}
    }
}
