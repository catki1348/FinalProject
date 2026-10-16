using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Codex (GPT-6), 2026-10-16. Bounded engine probes only inside this run's owned TestNNN copy.
[InitializeOnLoad]
public static class TestStage52Repair
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    [Serializable] public class Witness{public float t0,offset,vx,jump,flight;}
    [Serializable] public class Pair{public string pair;public Witness witness;public Witness[] witnesses;}
    [Serializable] public class Model{public Pair[] pairs,staticJumpPairs;}
    static readonly List<string> log=new List<string>();
    static string TestPath=>SessionState.GetString("Stage52RepairTest","");
    static TestStage52Repair(){if(SessionState.GetBool("Stage52RepairAuto",false))EditorApplication.playModeStateChanged+=AutoState;}
    // Schedule the complete owned-scene test immediately after Play starts, rather than leaving gaps between MCP calls.
    public static string Schedule(){var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(Application.isPlaying || active.isDirty)throw new InvalidOperationException("Editor busy or unsaved");
        if(!string.IsNullOrEmpty(TestPath) && File.Exists(TestPath))EditorSceneManager.OpenScene(TestPath);else Prepare();
        cachedRoot=null;SessionState.SetBool("Stage52RepairAuto",true);EditorApplication.playModeStateChanged-=AutoState;EditorApplication.playModeStateChanged+=AutoState;EditorApplication.isPlaying=true;return "Owned test scheduled";}
    static void AutoState(PlayModeStateChange state){if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("Stage52RepairAuto",false))EditorApplication.delayCall+=AutoRun;}
    static void AutoRun(){try{Guard();string motion=Motion400();string landings=Landings();string exit=Exit();File.WriteAllText("@Edit/Stage52_RepairedAuto_20261016.txt",motion+"\n"+landings+"\n"+exit);}catch(Exception e){File.WriteAllText("@Edit/Stage52_RepairedAuto_20261016.txt",e.ToString());}
        finally{SessionState.SetBool("Stage52RepairAuto",false);EditorApplication.playModeStateChanged-=AutoState;EditorApplication.isPlaying=false;}}
    static T[] All<T>() where T:Component=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
    static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static void Call(object o,string n)=>o.GetType().GetMethod(n,Flags).Invoke(o,null);
    static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException(msg);log.Add(msg);}
    static Transform cachedRoot;
    static Transform Root(){if(cachedRoot==null)cachedRoot=All<Transform>().Single(t=>t.name=="Generated_5-2");return cachedRoot;}
    static Rigidbody2D Player()=>((Transform)Get(All<StageManager>().Single(),"player")).GetComponent<Rigidbody2D>();
    static void Guard(){if(!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=TestPath)throw new InvalidOperationException("Owned TestNNN Play required");}
    static void Place(Rigidbody2D body,float x,float feet){body.transform.SetParent(null,true);body.transform.position=body.position;Physics2D.SyncTransforms();float offset=body.transform.position.y-body.GetComponent<Collider2D>().bounds.min.y;body.position=new Vector2(x,feet+offset);body.transform.position=body.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    public static string Prepare(){int n=0;string path;do{path="Assets/Scenes/Test"+(n++).ToString("D3")+".unity";}while(File.Exists(path));if(!AssetDatabase.CopyAsset("Assets/Scenes/Stage5.unity",path))throw new IOException("Cannot create test copy");SessionState.SetString("Stage52RepairTest",path);EditorSceneManager.OpenScene(path);return path;}

    static Dictionary<string,GameObject> Platforms()=>Root().GetComponentsInChildren<PlatformCore>(true).ToDictionary(c=>c.name,c=>c.gameObject);
    static MovingPlatform[] Isolate(){var body=Player();foreach(var m in body.GetComponents<MonoBehaviour>())m.enabled=false;foreach(var m in All<MonsterStats>())m.gameObject.SetActive(false);foreach(var b in All<Boss>())b.gameObject.SetActive(false);foreach(var end in All<ZoneEndPoint>())end.GetComponent<Collider2D>().enabled=false;foreach(var edge in All<SectionEdgeTrigger>())edge.GetComponent<Collider2D>().enabled=false;foreach(var p in Platforms().Values){if(p.TryGetComponent<PlatformBehaviour>(out var b)){b.StopAllCoroutines();b.enabled=false;}p.GetComponent<PlatformCore>().SetPlatformActive(true);}Time.timeScale=0;return Platforms().Values.Select(g=>g.GetComponent<MovingPlatform>()).Where(m=>m!=null).ToArray();}
    static void Reset(MovingPlatform[] movers,Dictionary<string,PrepareStage52.Platform> data){var body=Player();body.transform.SetParent(null,true);foreach(var m in movers){var p=data[m.name];m.transform.position=Root().position+new Vector3(p.points[0].x,p.points[0].y-.25f,0);var rb=m.GetComponent<Rigidbody2D>();rb.position=m.transform.position;rb.linearVelocity=Vector2.zero;((IDictionary)Get(m,"riders")).Clear();Set(m,"targetIndex",1);Set(m,"direction",1);Set(m,"finished",false);Call(m,"Start");}Physics2D.SyncTransforms();}
    static void Tick(MovingPlatform[] movers){foreach(var m in movers)Call(m,"FixedUpdate");Physics2D.Simulate(.02f);}

    // 20,000 real fixed physics steps check all reachable rail poses, including the former late B4/B5 collision.
    public static string Motion400(){Guard();log.Clear();var previous=Physics2D.simulationMode;var body=Player();var movers=Isolate();body.simulated=false;
        try{Physics2D.simulationMode=SimulationMode2D.Script;var d=JsonUtility.FromJson<PrepareStage52.Design>(File.ReadAllText("@Edit/Stage52_UnityPreparation.json"));var data=d.platforms.ToDictionary(p=>p.id);Reset(movers,data);
            var platforms=Platforms().Values.ToArray();var cols=platforms.Select(g=>g.GetComponent<Collider2D>()).ToArray();var pairs=new List<int[]>();
            for(int i=0;i<platforms.Length;i++)for(int j=i+1;j<platforms.Length;j++){var a=data[platforms[i].name];var b=data[platforms[j].name];if(a.points.Length==0 && b.points.Length==0)continue;
                var aa=a.points.Length>0?a.points:new[]{new PrepareStage52.Point{x=a.x,y=a.y}};var bb=b.points.Length>0?b.points:new[]{new PrepareStage52.Point{x=b.x,y=b.y}};
                if(aa.Min(p=>p.x)-a.width/2-.3f>bb.Max(p=>p.x)+b.width/2 || bb.Min(p=>p.x)-b.width/2-.3f>aa.Max(p=>p.x)+a.width/2)continue;
                if(aa.Min(p=>p.y)-1.9f>bb.Max(p=>p.y) || bb.Min(p=>p.y)-1.9f>aa.Max(p=>p.y))continue;pairs.Add(new[]{i,j});}
            var reached=movers.ToDictionary(m=>m.name,m=>new bool[data[m.name].points.Length]);var bounds=new Bounds[cols.Length];
            for(int step=0;step<20000;step++){Tick(movers);for(int i=0;i<cols.Length;i++)bounds[i]=cols[i].bounds;
                foreach(var pair in pairs){var a=bounds[pair[0]];var b=bounds[pair[1]];if(Mathf.Abs(a.center.x-b.center.x)<a.extents.x+b.extents.x+.3f && Mathf.Abs(a.max.y-b.max.y)<1.887f)throw new InvalidOperationException("Motion/headspace overlap at "+(step*.02f)+": "+platforms[pair[0]].name+" / "+platforms[pair[1]].name);}
                foreach(var m in movers){var p=data[m.name];for(int k=0;k<p.points.Length;k++)if(Vector2.Distance(m.GetComponent<Rigidbody2D>().position,(Vector2)Root().position+new Vector2(p.points[k].x,p.points[k].y-.25f))<.18f)reached[m.name][k]=true;}}
            Check(reached.All(p=>p.Value.All(v=>v)),"All 20 movers reach every waypoint during 400 simulated seconds");Check(true,"20,000 actual Physics2D steps: no platform/headspace overlap in "+pairs.Count+" possible interfering pairs");
            File.WriteAllLines("@Edit/Stage52_RepairedMotion_20261016.txt",log);return "PASS actual 400-second motion and clearance";
        }catch(Exception e){File.WriteAllLines("@Edit/Stage52_RepairedMotion_20261016.txt",log.Concat(new[]{e.ToString()}));return e.ToString();}finally{body.simulated=true;Physics2D.simulationMode=previous;Time.timeScale=0;}}

    // Test the model's departures against real rigidbodies, with small finite phase offsets for contact quantization.
    public static string Landings(){Guard();log.Clear();var previous=Physics2D.simulationMode;var body=Player();var movers=Isolate();bool killed=false;Action death=()=>killed=true;GameOverManager.Instance.OnGameOver+=death;
        try{Physics2D.simulationMode=SimulationMode2D.Script;var data=JsonUtility.FromJson<PrepareStage52.Design>(File.ReadAllText("@Edit/Stage52_UnityPreparation.json")).platforms.ToDictionary(p=>p.id);var model=JsonUtility.FromJson<Model>(File.ReadAllText("@Edit/Stage52_Deterministic_Model_20261016.json"));var objects=Platforms();var playerCol=body.GetComponent<Collider2D>();body.gravityScale=3;body.simulated=true;var failures=new List<string>();
            foreach(var pair in model.pairs.Concat(model.staticJumpPairs)){var names=pair.pair.Split(new[]{" -> "},StringSplitOptions.None);bool landed=false;float used=0;
                foreach(var w in pair.witnesses!=null && pair.witnesses.Length>0?pair.witnesses:new[]{pair.witness}){if(landed)break;
                foreach(float shift in new[]{0f,.2f,-.2f}){Reset(movers,data);Place(body,-200,30);killed=false;float t0=Mathf.Max(0,w.t0+shift);for(int i=0;i<Mathf.RoundToInt(t0/.02f);i++)Tick(movers);
                    var src=objects[names[0]].GetComponent<Collider2D>();var dst=objects[names[1]].GetComponent<Collider2D>();Place(body,src.bounds.center.x+w.offset,src.bounds.max.y+.02f);body.linearVelocity=new Vector2(w.vx,w.jump);
                    for(int i=0;i<145;i++){Tick(movers);var foot=playerCol.bounds;if(body.IsTouching(dst) && Mathf.Abs(foot.min.y-dst.bounds.max.y)<.08f && foot.max.x>dst.bounds.min.x+.03f && foot.min.x<dst.bounds.max.x-.03f && !killed){landed=true;used=t0;break;}if(killed || foot.min.y<dst.bounds.min.y-3)break;}
                    if(landed)break;}}
                if(landed)log.Add(pair.pair+" real landing at 80% jump height / phase="+used);else{failures.Add(pair.pair);log.Add("FAIL "+pair.pair);}}
            File.WriteAllLines("@Edit/Stage52_RepairedLandings_20261016.txt",log);return failures.Count==0?"PASS "+log.Count+" actual landing links":"FAIL "+string.Join(", ",failures);
        }finally{GameOverManager.Instance.OnGameOver-=death;Physics2D.simulationMode=previous;Time.timeScale=0;}}

    public static string Exit(){Guard();var body=Player();var movers=Isolate();var stage=All<StageManager>().Single();Set(stage,"<CurrentArea>k__BackingField",2);var end=All<ZoneEndPoint>().Single(e=>e.name=="ZoneEndPoint_5-2");end.GetComponent<Collider2D>().enabled=true;Set(end,"triggered",false);var previous=Physics2D.simulationMode;
        try{Physics2D.simulationMode=SimulationMode2D.Script;Place(body,end.transform.position.x+3.5f,end.transform.position.y-1+.02f);for(int i=0;i<40 && stage.CurrentArea==2;i++){body.linearVelocity=new Vector2(-8,body.linearVelocity.y);Tick(movers);}Check(stage.CurrentArea==3,"Actual leftward exit trigger advances 5-2 -> 5-3");var start=((ZoneStartPoint[])Get(stage,"zoneStartPoints"))[2];Check(Vector2.Distance(body.position,start.transform.position)<.15f,"Exit arrives at 5-3 spawn");File.WriteAllText("@Edit/Stage52_RepairedExit_20261016.txt","PASS actual leftward exit and 5-3 spawn");return "PASS actual exit";}finally{Physics2D.simulationMode=previous;Time.timeScale=0;}}

    // Five temporary views are inspection artifacts; remove after viewing, before deleting the owned scene.
    public static string Views(){if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=TestPath)throw new InvalidOperationException("Owned Test edit mode required");
        var go=new GameObject("ReviewCamera52");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=88;camera.aspect=1440f/720;
        var target=new Vector3(709,-71,0);var names=new[]{"front","back","left","right","top"};var rotations=new[]{Quaternion.identity,Quaternion.Euler(0,180,0),Quaternion.Euler(0,75,0),Quaternion.Euler(0,-75,0),Quaternion.Euler(75,0,0)};Directory.CreateDirectory("@Edit/Stage52_RepairShots");
        var rt=new RenderTexture(1440,720,24);var tex=new Texture2D(1440,720,TextureFormat.RGB24,false);var old=RenderTexture.active;
        try{for(int i=0;i<5;i++){camera.transform.rotation=rotations[i];camera.transform.position=target-camera.transform.forward*500;camera.farClipPlane=1000;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,720),0,0);tex.Apply();File.WriteAllBytes("@Edit/Stage52_RepairShots/"+names[i]+".png",tex.EncodeToPNG());}}finally{RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}return "5 views captured";}
    public static string Cleanup(){if(Application.isPlaying)throw new InvalidOperationException("Stop Play first");string path=TestPath;if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/Scenes/Test"))throw new InvalidOperationException("Owned test missing");EditorSceneManager.OpenScene("Assets/Scenes/Stage5.unity");AssetDatabase.DeleteAsset(path);SessionState.EraseString("Stage52RepairTest");return "Owned test deleted; Stage5 restored";}
}
