
// 생성된 프레임을 발 기준점으로 임포트하고 기존 플레이어의 테스트 전용 프리팹을 만든다.
string art="Assets/Sprites/Warrior/HeroKnightTest";
foreach(string path in AssetDatabase.FindAssets("t:Texture2D",new[]{art}).Select(AssetDatabase.GUIDToAssetPath)){
 var ti=(TextureImporter)AssetImporter.GetAtPath(path);
 ti.textureType=TextureImporterType.Sprite; ti.spriteImportMode=SpriteImportMode.Single;
 ti.alphaIsTransparency=true; ti.filterMode=FilterMode.Point; ti.mipmapEnabled=false;
 ti.textureCompression=TextureImporterCompression.Uncompressed; ti.spritePixelsPerUnit=560f/9.4f;
 var settings=new TextureImporterSettings(); ti.ReadTextureSettings(settings);
 settings.spriteAlignment=(int)SpriteAlignment.Custom; settings.spritePivot=new Vector2(0.5f,44f/1024f);
 settings.spriteMeshType=SpriteMeshType.FullRect; ti.SetTextureSettings(settings); ti.SaveAndReimport();
}
Func<string,Sprite> sprite = name=>AssetDatabase.LoadAssetAtPath<Sprite>(art+"/"+name+".png");
var run=Enumerable.Range(1,8).Select(i=>sprite("run_"+i)).ToArray();
var attacks=Enumerable.Range(1,6).Select(i=>sprite("attack_"+i)).ToArray();
var dash=sprite("dash_1");
if(run.Any(s=>s==null)||attacks.Any(s=>s==null)||dash==null) throw new Exception("Missing test sprite.");
string folder="Assets/Animations/Player/HeroKnightTest";
if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Animations/Player","HeroKnightTest");
// 두 방향의 클립은 같은 프레임을 쓰고 flipX 곡선으로 방향을 명시한다.
var clips=new AnimationClip[2];
for(int d=0;d<2;d++){
 string name=d==0?"Player_HeroKnight_Run_Right":"Player_HeroKnight_Run_Left";
 string path=folder+"/"+name+".anim";
 var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
 if(clip==null){clip=new AnimationClip(); AssetDatabase.CreateAsset(clip,path);}
 clip.ClearCurves(); clip.name=name; clip.frameRate=12;
 var keys=new ObjectReferenceKeyframe[9];
 for(int i=0;i<9;i++)keys[i]=new ObjectReferenceKeyframe{time=i/12f,value=run[i%8]};
 AnimationUtility.SetObjectReferenceCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"},keys);
 AnimationUtility.SetEditorCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_FlipX"},AnimationCurve.Constant(0,8f/12f,d));
 var settings=AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime=true;
 AnimationUtility.SetAnimationClipSettings(clip,settings); EditorUtility.SetDirty(clip); clips[d]=clip;
}
var baseController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/Player/Player_WalkAnimator.controller");
string overridePath=folder+"/Player_HeroKnight_Test.overrideController";
var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
if(controller==null){controller=new AnimatorOverrideController(baseController);AssetDatabase.CreateAsset(controller,overridePath);}
controller.runtimeAnimatorController=baseController;
foreach(var clip in baseController.animationClips)controller[clip]=clip.name=="Player_Walk_Left"?clips[0]:clips[1];
EditorUtility.SetDirty(controller);
string prefabPath="Assets/@ExternalAssets/@Prefab/P_Player_HeroKnightTest.prefab";
var root=PrefabUtility.LoadPrefabContents("Assets/@ExternalAssets/@Prefab/Player_Warrior.prefab");
try{
 root.name="P_Player_HeroKnightTest";
 var animator=root.GetComponent<PlayerSpriteAnimator>(); var so=new SerializedObject(animator);
 var visual=(SpriteRenderer)so.FindProperty("spriteRenderer").objectReferenceValue;
 var a=visual.GetComponent<Animator>(); if(a==null)a=visual.gameObject.AddComponent<Animator>();
 a.runtimeAnimatorController=controller; a.enabled=false; a.applyRootMotion=false;
 foreach(string field in new[]{"walkVisualOffsetY","dashVisualOffsetY","attackVisualOffsetY"})so.FindProperty(field).floatValue=0f;
 // Shift는 대쉬 입력에만 사용하고, 이후에도 새 이동 애니메이션으로 복귀한다.
 so.FindProperty("runKey").intValue=(int)KeyCode.None;
 so.FindProperty("dashRight").objectReferenceValue=dash;so.FindProperty("dashLeft").objectReferenceValue=dash;
 var frames=so.FindProperty("attackFrames");frames.arraySize=6;
 for(int i=0;i<6;i++)frames.GetArrayElementAtIndex(i).objectReferenceValue=attacks[i];
 so.ApplyModifiedPropertiesWithoutUndo();
 var helper=root.AddComponent<PlayerHeroKnightMotionTest>(); var hs=new SerializedObject(helper);
 hs.FindProperty("visual").objectReferenceValue=visual;
 var poses=hs.FindProperty("rightFacingPoses");poses.arraySize=7;
 for(int i=0;i<6;i++)poses.GetArrayElementAtIndex(i).objectReferenceValue=attacks[i];
 poses.GetArrayElementAtIndex(6).objectReferenceValue=dash;hs.ApplyModifiedPropertiesWithoutUndo();
 PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
}finally{PrefabUtility.UnloadPrefabContents(root);}
AssetDatabase.SaveAssets();
return new {prefab=prefabPath,frames=run.Length+attacks.Length+1,controller=overridePath};
