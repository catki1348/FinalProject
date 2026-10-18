#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

// Rebuildable Stage4 asset assembly; explicit references restrict deployment to the boss zone.
public static class BlindCaveHorrorSetup
{
    const string Root = "Assets/@ExternalAssets/Bosses/Stage4_BlindCaveHorror";
    const string Prefabs = "Assets/@ExternalAssets/@Prefab";
    static readonly string[] Names = { "Idle","Walk","Groggy","ClawSlash","LowSweep","SonicRoar","CaveCollapse","Death","Jump","Charge" };
    static readonly float[] Durations = {1,.5f,.6f,.7f,.8f,1.9f,1.4f,1.1f,.8f,1.5f};
    // Configure private serialized references in asset originals.
    static void Ref(Object o,string field,Object value) { var so=new SerializedObject(o);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo(); }
    static Sprite Import(string path,Vector2 pivot)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var ti=(TextureImporter)AssetImporter.GetAtPath(path);ti.textureType=TextureImporterType.Sprite;ti.spriteImportMode=SpriteImportMode.Single;ti.spritePixelsPerUnit=100;
        var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=pivot;ti.SetTextureSettings(settings);
        ti.filterMode=FilterMode.Point;ti.textureCompression=TextureImporterCompression.Uncompressed;ti.mipmapEnabled=false;ti.alphaIsTransparency=true;ti.maxTextureSize=4096;ti.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    // Native geometric indicators are effects, not replacement character artwork.
    static Sprite Shape(string name,int kind)
    {
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color32[4096];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float dx=x-32,dy=y-32,r=dx*dx+dy*dy;
            bool inside=kind==0 || (kind==1 ? r<900 && r>650 : Mathf.Abs(dx)+Mathf.Abs(dy)*.65f<30);
            pixels[y*64+x]=inside?new Color32(180,170,190,255):new Color32(0,0,0,0);
        }
        texture.SetPixels32(pixels);texture.Apply();string path=Root+"/Effects/T_"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
        var sprite=Import(path,new Vector2(.5f,.5f));return sprite;
    }
    // Twelve generated poses are used unchanged. Basic states use explicitly temporary pose reuse.
    static AnimatorController Animations()
    {
        string folder=Root+"/Animations";Directory.CreateDirectory(folder);
        var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(folder+"/Boss_Stage4_BlindCaveHorrorAnimator.controller");
        if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(folder+"/Boss_Stage4_BlindCaveHorrorAnimator.controller");
        var machine=controller.layers[0].stateMachine;
        for(int i=0;i<Names.Length;i++)
        {
            string source=i<3||i==7?"LowSweep":Names[i]; // Death overrides this with its own four poses below
            var first=Import(Root+"/Frames/"+source+"_01.png",new Vector2(.5f,8f/1198));
            var second=i<3||i==7?first:Import(Root+"/Frames/"+source+"_02.png",new Vector2(.5f,8f/1198));
            string path=folder+"/Boss_"+Names[i]+".anim";var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!clip){clip=new AnimationClip();AssetDatabase.CreateAsset(clip,path);}clip.frameRate=60;
            float switchTime=i==3?.35f:i==4?.4f:i==5?.5f:i==6?.8f:i==8?.3f:i==9?.6f:Durations[i]*.5f;
            AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),new[]{new ObjectReferenceKeyframe{time=0,value=first},new ObjectReferenceKeyframe{time=switchTime,value=second},new ObjectReferenceKeyframe{time=Durations[i],value=second}});
            if(i<3)
            {
                float mid=i==2?-.18f:i==1?.06f:.025f;
                clip.SetCurve("",typeof(Transform),"m_LocalPosition.y",new AnimationCurve(new Keyframe(0,0),new Keyframe(Durations[i]*.5f,mid),new Keyframe(Durations[i],0)));
            }
            else clip.SetCurve("",typeof(Transform),"m_LocalPosition.y",AnimationCurve.Constant(0,Durations[i],0));
            if(i==7)ApplyDeathPoses(clip);
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=i<3;settings.stopTime=Durations[i];AnimationUtility.SetAnimationClipSettings(clip,settings);
            var state=machine.states.Select(x=>x.state).FirstOrDefault(x=>x.name=="Boss_"+Names[i]);if(!state)state=machine.AddState("Boss_"+Names[i]);state.motion=clip;state.writeDefaultValues=false;if(i==0)machine.defaultState=state;EditorUtility.SetDirty(clip);
        }
        return controller;
    }
    // Death uses its own four generated poses (stiffen, arms go limp, body sinks, final rest); the poses carry the
    // lowering themselves, so the clip only switches sprites at the times in the design (0, 0.25, 0.5, 0.8s).
    static void ApplyDeathPoses(AnimationClip clip)
    {
        var poses=new Sprite[4];
        for(int i=0;i<4;i++)poses[i]=Import(Root+"/Frames/Death_0"+(i+1)+".png",new Vector2(.5f,8f/1198));
        float[] times={0,.25f,.5f,.8f};
        var keys=new ObjectReferenceKeyframe[5];
        for(int i=0;i<4;i++)keys[i]=new ObjectReferenceKeyframe{time=times[i],value=poses[i]};
        keys[4]=new ObjectReferenceKeyframe{time=Durations[7],value=poses[3]};
        AnimationUtility.SetObjectReferenceCurve(clip,EditorCurveBinding.PPtrCurve("",typeof(SpriteRenderer),"m_Sprite"),keys);
    }
    // Build all repeated effects in their prefab original.
    static BossCaveEffect Effect()
    {
        var go=new GameObject("P_BlindCaveHorrorEffect");var effect=go.AddComponent<BossCaveEffect>();
        Sprite[] sprites={Shape("CaveWarning",0),Shape("SonicRing",1),Shape("CaveRock",2)};
        var renderers=new SpriteRenderer[5];
        for(int i=0;i<5;i++){var child=new GameObject(i==0?"Warning":i==1?"Rock":"Wave"+(i-1));child.transform.SetParent(go.transform,false);var sr=child.AddComponent<SpriteRenderer>();sr.sprite=sprites[i==0?0:i==1?2:1];sr.sortingOrder=8;sr.transform.localScale=Vector3.one*(100f/64);renderers[i]=sr;}
        Ref(effect,"warning",renderers[0]);Ref(effect,"rock",renderers[1]);var so=new SerializedObject(effect);var arr=so.FindProperty("waves");arr.arraySize=3;for(int i=0;i<3;i++)arr.GetArrayElementAtIndex(i).objectReferenceValue=renderers[i+2];so.ApplyModifiedPropertiesWithoutUndo();
        var saved=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"/P_BlindCaveHorrorEffect.prefab");Object.DestroyImmediate(go);return saved.GetComponent<BossCaveEffect>();
    }
    // Replace only the supplied Stage4 placeholder; retain the existing floor/start and stage routing.
    public static string Build(CameraBoundsZone zone,GameObject placeholder)
    {
        if(EditorApplication.isPlaying||!zone||!placeholder||placeholder.transform.parent!=zone.transform)throw new System.InvalidOperationException("Expected explicit Stage4 boss-zone references in edit mode.");
        Directory.CreateDirectory(Root+"/Effects");Directory.CreateDirectory(Prefabs);AssetDatabase.Refresh();
        var controller=Animations();var effect=Effect();
        var go=new GameObject("P_Boss_Stage4_BlindCaveHorror");go.layer=9;
        var rb=go.AddComponent<Rigidbody2D>();rb.gravityScale=3;rb.mass=50;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.interpolation=RigidbodyInterpolation2D.Interpolate;
        var col=go.AddComponent<BoxCollider2D>();col.size=new Vector2(2,3);col.offset=new Vector2(0,1.5f);
        var boss=go.AddComponent<Boss>();var hp=new SerializedObject(boss);hp.FindProperty("maxHealth").intValue=220;hp.ApplyModifiedPropertiesWithoutUndo();
        var sound=go.AddComponent<BossSound>();var audio=new SerializedObject(sound);var entries=audio.FindProperty("sfxEntries");int[] sounds={8,9,10,11,20,21,22,23,24,25,6,7,19,26,27,28,29,30};entries.arraySize=sounds.Length;for(int i=0;i<sounds.Length;i++){var entry=entries.GetArrayElementAtIndex(i);entry.FindPropertyRelative("type").intValue=sounds[i];entry.FindPropertyRelative("clips").arraySize=0;}audio.ApplyModifiedPropertiesWithoutUndo();
        var visual=new GameObject("Visual");visual.layer=9;visual.transform.SetParent(go.transform,false);visual.transform.localScale=Vector3.one*.35f;
        var sr=visual.AddComponent<SpriteRenderer>();sr.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Frames/LowSweep_01.png");sr.sortingOrder=5;var animator=visual.AddComponent<Animator>();animator.runtimeAnimatorController=controller;
        var ai=go.AddComponent<BlindCaveHorrorController>();Ref(ai,"animator",animator);Ref(ai,"visual",visual.transform);Ref(ai,"effectPrefab",effect);var config=new SerializedObject(ai);config.FindProperty("groundLayer").intValue=LayerMask.GetMask("Ground");config.ApplyModifiedPropertiesWithoutUndo();
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,Prefabs+"/P_Boss_Stage4_BlindCaveHorror.prefab");Object.DestroyImmediate(go);
        var position=placeholder.transform.position;
        // Replace the single-tile placeholder with a prefab arena that supports every attack range.
        var oldFloor=zone.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r=>r.GetComponent<BoxCollider2D>() && r.gameObject.layer==LayerMask.NameToLayer("Ground"));
        if(oldFloor)
        {
            var arena=new GameObject("P_Stage4_CaveArena");
            var floor=new GameObject("Floor");floor.layer=LayerMask.NameToLayer("Ground");floor.transform.SetParent(arena.transform,false);floor.transform.localPosition=new Vector3(0,-.5f,0);
            var renderer=floor.AddComponent<SpriteRenderer>();renderer.sprite=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/@ExternalAssets/Tiles/Platform_Stage4_Dungeon_Tiled.png");if(!renderer.sprite)renderer.sprite=oldFloor.sprite;renderer.drawMode=SpriteDrawMode.Tiled;renderer.size=new Vector2(20,1);renderer.sortingOrder=1;
            var collider=floor.AddComponent<BoxCollider2D>();collider.size=new Vector2(20,1);
            for(int side=-1;side<=1;side+=2){var wall=new GameObject("Wall");wall.layer=floor.layer;wall.transform.SetParent(arena.transform,false);wall.transform.localPosition=new Vector3(side*10.25f,3,0);var wallCollider=wall.AddComponent<BoxCollider2D>();wallCollider.size=new Vector2(.5f,7);}
            var arenaPrefab=PrefabUtility.SaveAsPrefabAsset(arena,Prefabs+"/P_Stage4_CaveArena.prefab");Object.DestroyImmediate(arena);
            var oldArena=PrefabUtility.GetNearestPrefabInstanceRoot(oldFloor.gameObject);Object.DestroyImmediate(oldArena && oldArena.transform.parent==zone.transform?oldArena:oldFloor.gameObject);var arenaInstance=(GameObject)PrefabUtility.InstantiatePrefab(arenaPrefab,zone.transform);arenaInstance.transform.position=new Vector3(zone.transform.position.x,position.y,0);
        }
        Object.DestroyImmediate(placeholder);var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,zone.transform);instance.transform.position=position;instance.name="Boss_4-5";
        if(!zone.GetComponentInChildren<BossZoneEndPoint>(true))
        {
            var exit=new GameObject("P_Stage4_BossExit");var trigger=exit.AddComponent<BoxCollider2D>();trigger.size=new Vector2(1,3);trigger.isTrigger=true;trigger.enabled=false;exit.AddComponent<BossZoneEndPoint>();
            var exitPrefab=PrefabUtility.SaveAsPrefabAsset(exit,Prefabs+"/P_Stage4_BossExit.prefab");Object.DestroyImmediate(exit);
            var placed=(GameObject)PrefabUtility.InstantiatePrefab(exitPrefab,zone.transform);placed.transform.position=new Vector3(zone.WorldBounds.max.x-1,position.y+1,0);
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(zone.gameObject.scene);EditorSceneManager.SaveScene(zone.gameObject.scene);return "Stage4 boss prefab and 10 animation states installed.";
    }
}
#endif
