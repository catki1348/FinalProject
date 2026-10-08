// Unity MCP execute_code로 실행할 수 있는 테스트 프리팹/클립 연결 검사.
var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/@ExternalAssets/@Prefab/P_Player_HeroKnightTest.prefab");
if(prefab==null||prefab.GetComponent<PlayerHeroKnightMotionTest>()==null)throw new Exception("Missing test prefab or direction helper");
var so=new SerializedObject(prefab.GetComponent<PlayerSpriteAnimator>());
if(so.FindProperty("attackFrames").arraySize!=8||so.FindProperty("runKey").intValue!=(int)KeyCode.None)throw new Exception("Attack/run configuration mismatch");
for(int d=0;d<2;d++){
 var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Animations/Player/HeroKnightTest/Player_HeroKnight_Run_"+(d==0?"Right":"Left")+".anim");
 var binding=new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_Sprite"};
 var keys=AnimationUtility.GetObjectReferenceCurve(clip,binding);
 if(keys==null||keys.Length!=9||keys.Take(8).Select(k=>k.value).Distinct().Count()!=8)throw new Exception("Run frame count mismatch");
 var flip=AnimationUtility.GetEditorCurve(clip,new EditorCurveBinding{path="",type=typeof(SpriteRenderer),propertyName="m_FlipX"});
 if(flip==null||flip.Evaluate(0)!=d)throw new Exception("Direction curve mismatch");
}
return "PASS: test prefab, attack 8 frames, run 8 frames in both directions, Shift dash configuration.";
