using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Codex (GPT-6), 2026-10-15. Physics and live-clock checks are restricted to an owned disposable scene.
public static class VerifyStage22Revision
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> log=new List<string>();
    static string TestPath=>SessionState.GetString("Stage22RevisionTest","");
    static T[] All<T>() where T:Component=>DeployStage22Revision.All<T>();
    static object Get(object o,string n)=>o.GetType().GetField(n,Flags).GetValue(o);
    static void Set(object o,string n,object v)=>o.GetType().GetField(n,Flags).SetValue(o,v);
    static void Call(object o,string n)=>o.GetType().GetMethod(n,Flags).Invoke(o,null);
    static void Check(bool ok,string msg){if(!ok)throw new InvalidOperationException("FAIL "+msg);log.Add(msg);}
    static Transform Section()=>All<Transform>().Single(t=>t.name=="Section_2");
    static Dictionary<string,GameObject> Platforms()=>Section().GetComponentsInChildren<PlatformCore>(true).ToDictionary(c=>c.name,c=>c.gameObject);
    static void Guard(){if(!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=TestPath)throw new InvalidOperationException("Owned TestNNN Play only");}

    // Reopened scene checks prove that prefab originals and serialized references survive saving.
    public static string Structure() {
        log.Clear();var d=DeployStage22Revision.Read();var objects=Platforms();Check(objects.Count==18,"18 platforms: normal10 / moving5 / disappearing3");
        Check(objects.Values.Count(g=>g.GetComponent<MovingPlatform>()!=null)==5 && objects.Values.Count(g=>g.GetComponent<DisappearingFoothold>()!=null)==3,"5 moving / 3 disappearing components");
        foreach(var p in d.platforms) {
            var go=objects[p.id];var sr=go.GetComponent<SpriteRenderer>();var col=go.GetComponent<BoxCollider2D>();var b=col.bounds;
            Check(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go)==p.prefab,p.id+" dedicated prefab reference");
            Check(go.transform.lossyScale==Vector3.one && sr.drawMode==SpriteDrawMode.Tiled && sr.tileMode==SpriteTileMode.Continuous && !col.autoTiling && Vector2.Distance(sr.size,col.size)<.001f,p.id+" unit scale / Tiled Continuous / matched collider");
            Check(Mathf.Abs(b.center.x-p.x)<.002f && Mathf.Abs(b.max.y-p.top)<.002f && Mathf.Abs(b.size.x-p.width)<.002f,p.id+" planned collision geometry");
            if(p.kind=="moving") {
                var m=go.GetComponent<MovingPlatform>();var so=new SerializedObject(m);var path=(PlatformPath)so.FindProperty("path").objectReferenceValue;
                Check(PrefabUtility.GetCorrespondingObjectFromSource(m)!=null && path!=null && !path.transform.IsChildOf(go.transform),p.id+" behavior in prefab / external connected path");
                Check(so.FindProperty("speed").floatValue==2 && so.FindProperty("startDelay").floatValue==0 && so.FindProperty("endpointWait").floatValue==0 && so.FindProperty("loopMode").enumValueIndex==p.loop,p.id+" preserved movement settings");
                for(int i=0;i<p.points.Length;i++)Check(Vector2.Distance(path.GetPoint(i),new Vector2(p.points[i].x,p.points[i].y-p.height/2))<.002f,p.id+" waypoint "+i);
            }
            if(p.kind=="disappearing") {
                var m=go.GetComponent<DisappearingFoothold>();var so=new SerializedObject(m);
                Check(PrefabUtility.GetCorrespondingObjectFromSource(m)!=null && Mathf.Abs(so.FindProperty("vanishDelay").floatValue-.4f)<.001f && so.FindProperty("respawnDelay").floatValue==2,p.id+" prefab-owned 0.4 / 2.0 second vanish");
            }
        }
        var stage=All<StageManager>().Single();var zones=(CameraBoundsZone[])Get(stage,"zoneCameraBounds");var zone=zones[1].WorldBounds;
        Check(Mathf.Abs(zone.min.x-50)<.002f && Mathf.Abs(zone.max.x-172)<.002f,"2-2 camera 50..172");
        foreach(var p in objects.Values)Check(zone.Contains(new Vector3(p.transform.position.x,p.GetComponent<Collider2D>().bounds.max.y,zone.center.z)),p.name+" contained in zone");
        for(int i=0;i<4;i++)for(int j=i+1;j<5;j++){var a=zones[i].WorldBounds;var b=zones[j].WorldBounds;Check(!(Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x)>.002f && Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y)>.002f),"zones "+(i+1)+" / "+(j+1)+" do not overlap");}
        Check(Mathf.Abs(zones[4].WorldBounds.center.x-(zones.Take(4).Min(z=>z.WorldBounds.min.x)+zones.Take(4).Max(z=>z.WorldBounds.max.x))/2)<.002f,"boss arena centered under upper row");
        Check(All<Transform>().Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))==0,"no missing scripts");
        File.WriteAllLines("@Edit/Stage22_Revision_Structure_20261015.txt",log);return "PASS "+log.Count+" saved structure checks";
    }
    public static string Prepare() {
        int n=0;string path;do{path="Assets/Scenes/Test"+(n++).ToString("D3")+".unity";}while(File.Exists(path));
        if(!AssetDatabase.CopyAsset("Assets/Scenes/Stage2.unity",path))throw new IOException("Test copy failed");
        SessionState.SetString("Stage22RevisionTest",path);EditorSceneManager.OpenScene(path);return path;
    }
    static Rigidbody2D Body() {var s=All<StageManager>().Single();return ((Transform)Get(s,"player")).GetComponent<Rigidbody2D>();}
    static void Place(Rigidbody2D rb,float x,float feet) {
        rb.transform.SetParent(null,true);rb.transform.position=rb.position;Physics2D.SyncTransforms();
        var c=rb.GetComponent<Collider2D>();float offset=rb.transform.position.y-c.bounds.min.y;
        rb.position=new Vector2(x,feet+offset);rb.transform.position=rb.position;rb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    static void Isolate(Rigidbody2D body) {
        foreach(var b in body.GetComponents<MonoBehaviour>())b.enabled=false;
        foreach(var monster in All<MonsterStats>())monster.gameObject.SetActive(false);
        foreach(var boss in All<Boss>())boss.gameObject.SetActive(false);
        foreach(var go in Platforms().Values)if(go.TryGetComponent<PlatformBehaviour>(out var behavior)){behavior.StopAllCoroutines();if(behavior is DisappearingFoothold)Set(behavior,"vanishRoutine",null);behavior.enabled=false;go.GetComponent<PlatformCore>().SetPlatformActive(true);}
        foreach(var t in All<SectionEdgeTrigger>())t.enabled=false;
        // Falling during a respawn observation must not pause the test clock via the game-over overlay.
        foreach(var t in All<OutOfBoundsTrigger>())t.gameObject.SetActive(false);
    }
    // Advance actual MovingPlatform code and Physics2D together, including its kinematic rider transport.
    public static string PhysicsChecks() {
        Guard();log.Clear();var previous=Physics2D.simulationMode;var body=Body();Isolate(body);Time.timeScale=0;
        try {
            Physics2D.simulationMode=SimulationMode2D.Script;var objects=Platforms();var movers=objects.Values.Select(g=>g.GetComponent<MovingPlatform>()).Where(m=>m!=null).ToArray();
            var data=DeployStage22Revision.Read();var byID=data.platforms.ToDictionary(p=>p.id);
            Action reset=()=>{body.transform.SetParent(null,true);foreach(var m in movers){var p=byID[m.name];var r=m.GetComponent<Rigidbody2D>();m.transform.position=new Vector3(p.x,p.top-p.height/2,0);r.position=m.transform.position;r.linearVelocity=Vector2.zero;((IDictionary)Get(m,"riders")).Clear();Set(m,"targetIndex",1);Set(m,"direction",1);Set(m,"finished",false);Call(m,"Start");}Physics2D.SyncTransforms();};
            Action tick=()=>{foreach(var m in movers)Call(m,"FixedUpdate");Physics2D.Simulate(.02f);};
            var order="N1 N2 N3 MP1 N4 MP2 N5 MP3 N6 MP4 N7 MP5 N8 D1 D2 D3 N9 N10".Split(' ');
            var collider=body.GetComponent<Collider2D>();
            // Search departure phases in the actual engine. Each candidate starts from a fresh mover state.
            for(int i=0;i<order.Length-1;i++) {
                bool landed=false;float usedPhase=0,usedSpeed=0;
                for(int phase=0;phase<70 && !landed;phase++)foreach(float vx in new[]{6.4f,5.6f,4.8f,4f,3.2f,2.4f,.8f}) {
                    reset();Place(body,-200,30);for(int k=0;k<phase*5;k++)tick();
                    var src=objects[order[i]].GetComponent<Collider2D>();var dst=objects[order[i+1]].GetComponent<Collider2D>();
                    Place(body,src.bounds.max.x-.03f,src.bounds.max.y+.015f);body.linearVelocity=new Vector2(vx,9);
                    for(int k=0;k<75;k++) {
                        tick();var foot=collider.bounds;var target=dst.bounds;
                        if(body.linearVelocity.y<=.1f && Mathf.Abs(foot.min.y-target.max.y)<.07f && foot.max.x>target.min.x+.02f && foot.min.x<target.max.x-.02f && body.IsTouching(dst)){landed=true;usedPhase=phase*.1f;usedSpeed=vx;break;}
                        if(foot.min.y<target.min.y-2)break;
                    }
                    if(landed)break;
                }
                Check(landed,order[i]+"->"+order[i+1]+" actual engine landing, vx="+usedSpeed+" phase="+usedPhase);
            }
            // One complete cycle for each rail plus actual MP2 rider elevation.
            foreach(var m in movers) {
                reset();Place(body,m.name=="MP2"?96:-200,m.name=="MP2"?.54f:30);float min=float.MaxValue,max=float.MinValue;var p=byID[m.name];
                var reached=new bool[p.points.Length];
                for(int k=0;k<750;k++) {
                    tick();var pos=m.GetComponent<Rigidbody2D>().position;
                    for(int j=0;j<p.points.Length;j++)if(Vector2.Distance(pos,new Vector2(p.points[j].x,p.points[j].y-p.height/2))<.07f)reached[j]=true;
                    if(m.name=="MP2"){min=Mathf.Min(min,collider.bounds.min.y);max=Mathf.Max(max,collider.bounds.min.y);}
                }
                Check(reached.All(v=>v),m.name+" actual motion reaches every waypoint in 15 seconds");
                if(m.name=="MP2")Check(max-min>3.05f && max<4.1f,"MP2 transports player through 3.2-unit lift height; measured="+(max-min));
            }
            // Test all six skips at maximum buff, sampling launch positions and dash timing.
            foreach(var pair in new[]{"N8 D2","N8 D3","N8 N9","D1 D3","D1 N9","D2 N9"}) {
                var keys=pair.Split(' ');bool skipped=false;reset();var from=objects[keys[0]].GetComponent<Collider2D>();var to=objects[keys[1]].GetComponent<Collider2D>();
                for(int launch=0;launch<5 && !skipped;launch++)for(int dash=0;dash<31 && !skipped;dash++) {
                    Place(body,from.bounds.max.x-.02f-launch*.25f,from.bounds.max.y+.015f);body.linearVelocity=new Vector2(10,9);
                    for(int step=0;step<60;step++) {
                        if(step>=dash && step<dash+10)body.linearVelocity=new Vector2(5.625f,0);
                        else body.linearVelocity=new Vector2(10,body.linearVelocity.y);
                        Physics2D.Simulate(.02f);
                        if(body.IsTouching(to) && Mathf.Abs(collider.bounds.min.y-to.bounds.max.y)<.08f && body.linearVelocity.y<=.1f){skipped=true;break;}
                        if(collider.bounds.min.y<to.bounds.min.y-2)break;
                    }
                }
                Check(!skipped,keys[0]+"->"+keys[1]+" blocked with maximum buff / sampled air dash");
            }
            File.WriteAllLines("@Edit/Stage22_Revision_Physics_20261015.txt",log);return "PASS "+log.Count+" engine physics checks";
        }catch(Exception e){File.WriteAllLines("@Edit/Stage22_Revision_Physics_20261015.txt",log.Concat(new[]{e.ToString()}));return e.ToString();}
        finally{Physics2D.simulationMode=previous;Time.timeScale=0;}
    }
    static double wallStart;static bool hidden,restored;static DisappearingFoothold disappearing;static float contactAt;
    // Live frame callbacks verify real coroutine clocks instead of manually advancing an iterator.
    public static string StartLive() {
        Guard();EditorApplication.update-=LiveTick;log.Clear();var body=Body();Isolate(body);
        disappearing=Platforms()["D1"].GetComponent<DisappearingFoothold>();disappearing.enabled=true;
        hidden=restored=false;contactAt=-1;Place(body,disappearing.transform.position.x,disappearing.GetComponent<Collider2D>().bounds.max.y+.08f);
        wallStart=EditorApplication.timeSinceStartup;Time.timeScale=1;EditorApplication.update+=LiveTick;return "Live D1 collision -> vanish -> respawn running";
    }
    static void LiveTick() {
        try {
            var body=Body();var col=disappearing.GetComponent<Collider2D>();
            if(contactAt<0 && body.IsTouching(col)){contactAt=Time.time;log.Add("Actual top collision at "+contactAt);}
            if(!col.enabled && !hidden){hidden=true;float elapsed=Time.time-contactAt;Check(contactAt>=0 && elapsed>=.37f && elapsed<.65f,"Vanish after real top contact: "+elapsed+" seconds");}
            if(hidden && col.enabled && !restored){restored=true;float elapsed=Time.time-contactAt;Check(elapsed>=2.37f && elapsed<2.75f,"Respawn after real top contact: "+elapsed+" seconds");FinishLive("PASS real collision and coroutine timings");}
            if(EditorApplication.timeSinceStartup-wallStart>10)throw new TimeoutException("Live vanish timed out");
        }catch(Exception e){FinishLive(e.ToString());}
    }
    static void FinishLive(string result){EditorApplication.update-=LiveTick;Time.timeScale=0;log.Add(result);File.WriteAllLines("@Edit/Stage22_Revision_Live_20261015.txt",log);}
    static int chainIndex;static bool inFlight;static float chainSpeed;static Dictionary<string,GameObject> chainObjects;
    static readonly string[] Chain={"N8","D1","D2","D3","N9"};
    // Live continuous run uses normal physics and the actual disappearing coroutines, with no teleport between jumps.
    public static string StartChain(float speed) {
        Guard();EditorApplication.update-=ChainTick;log.Clear();var body=Body();Isolate(body);chainObjects=Platforms();
        foreach(var go in chainObjects.Values)if(go.TryGetComponent<DisappearingFoothold>(out var v))v.enabled=true;
        chainSpeed=speed;chainIndex=0;inFlight=false;var src=chainObjects[Chain[0]].GetComponent<Collider2D>();Place(body,src.bounds.max.x-.1f,src.bounds.max.y+.02f);
        wallStart=EditorApplication.timeSinceStartup;Time.timeScale=1;EditorApplication.update+=ChainTick;return "Live continuous N8-D1-D2-D3-N9 at speed "+speed;
    }
    static void ChainTick() {
        try {
            var body=Body();var foot=body.GetComponent<Collider2D>().bounds;
            body.linearVelocity=new Vector2(chainSpeed,body.linearVelocity.y);
            if(inFlight) {
                var target=chainObjects[Chain[chainIndex+1]].GetComponent<Collider2D>();
                if(body.IsTouching(target) && Mathf.Abs(foot.min.y-target.bounds.max.y)<.08f && body.linearVelocity.y<=.1f) {
                    chainIndex++;inFlight=false;log.Add("Actual landing "+Chain[chainIndex]+" at "+Time.time);
                    if(chainIndex==4){FinishChain("PASS live continuous chain speed "+chainSpeed);return;}
                }
            }else {
                var src=chainObjects[Chain[chainIndex]].GetComponent<Collider2D>();
                if(body.position.x>=src.bounds.max.x-.08f){body.linearVelocity=new Vector2(chainSpeed,9);inFlight=true;log.Add("Jump from "+Chain[chainIndex]+" at "+Time.time);}
            }
            if(foot.min.y<0 || EditorApplication.timeSinceStartup-wallStart>12)throw new InvalidOperationException("Continuous chain fell or timed out at "+Chain[chainIndex]);
        }catch(Exception e){FinishChain(e.ToString());}
    }
    static void FinishChain(string result){EditorApplication.update-=ChainTick;Time.timeScale=0;Body().linearVelocity=Vector2.zero;log.Add(result);File.WriteAllLines("@Edit/Stage22_Revision_Chain_"+chainSpeed.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"_20261015.txt",log);}
    public static string ExitCheck() {
        Guard();var body=Body();Isolate(body);var stage=All<StageManager>().Single();
        // Earlier landing probes can consume one-shot triggers. Establish an isolated area-2 fixture first.
        Set(stage,"<CurrentArea>k__BackingField",2);
        var end=All<ZoneEndPoint>().Single(e=>e.name=="ZoneEndPoint_2-2");var previous=Physics2D.simulationMode;
        try{Physics2D.simulationMode=SimulationMode2D.Script;Place(body,160,3.64f);Physics2D.Simulate(.02f);Set(end,"triggered",false);Place(body,end.transform.position.x,3.64f);Physics2D.Simulate(.02f);Check(stage.CurrentArea==3,"Actual N10 goal trigger advances 2-2 -> 2-3");var start=((ZoneStartPoint[])Get(stage,"zoneStartPoints"))[2];Check(Vector2.Distance(body.position,start.transform.position)<.1f,"Exit teleports to shifted 2-3 start");File.WriteAllLines("@Edit/Stage22_Revision_Exit_20261015.txt",log);return "PASS actual exit trigger";}finally{Physics2D.simulationMode=previous;Time.timeScale=0;}
    }
    public static string Cleanup() {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first");EditorApplication.update-=LiveTick;EditorApplication.update-=ChainTick;
        string path=TestPath;EditorSceneManager.OpenScene("Assets/Scenes/Stage2.unity");
        if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/Scenes/Test"))throw new InvalidOperationException("Owned test not found");
        AssetDatabase.DeleteAsset(path);SessionState.EraseString("Stage22RevisionTest");return "Owned test deleted / Stage2 restored";
    }
    // Temporary verification renders are deleted after inspection; no camera is saved into the game scene.
    public static string CaptureViews() {
        if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=TestPath)throw new InvalidOperationException("Owned Test edit mode required");
        var go=new GameObject("Temporary_22_ReviewCamera");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.orthographic=true;camera.orthographicSize=12;
        var target=new Vector3(116.875f,1.7f,0);string folder="@Edit/Stage22_ReviewShots";Directory.CreateDirectory(folder);
        var views=new[]{"front","back","left","right","top"};var rotations=new[]{Quaternion.identity,Quaternion.Euler(0,180,0),Quaternion.Euler(0,75,0),Quaternion.Euler(0,-75,0),Quaternion.Euler(75,0,0)};
        var rt=new RenderTexture(1920,448,24);var tex=new Texture2D(1920,448,TextureFormat.RGB24,false);var previous=RenderTexture.active;
        try{for(int i=0;i<views.Length;i++){camera.transform.rotation=rotations[i];camera.transform.position=target-camera.transform.forward*100;camera.nearClipPlane=.1f;camera.farClipPlane=300;camera.targetTexture=rt;camera.aspect=1920f/448;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,448),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+views[i]+".png",tex.EncodeToPNG());}}
        finally{RenderTexture.active=previous;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
        return "5 temporary verification views captured";
    }
}
