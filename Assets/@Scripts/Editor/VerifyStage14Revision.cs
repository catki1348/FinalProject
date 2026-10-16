using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Only the owned disposable scene may run destructive probes.
public static class VerifyStage14Revision
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> results=new List<string>();
    static string TestPath=>SessionState.GetString("Stage14RevisionTest","");
    static T[] All<T>() where T:Component=>UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    static object Get(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Set(object o,string name,object v)=>o.GetType().GetField(name,Flags).SetValue(o,v);
    static void Call(object o,string name)=>o.GetType().GetMethod(name,Flags).Invoke(o,null);
    static void Check(bool ok,string detail){if(!ok)throw new InvalidOperationException("FAIL: "+detail);results.Add(detail);}
    static Transform Root()=>All<AreaContentStartManager>().Single().transform;
    static GameObject[] Platforms()=>Root().GetComponentsInChildren<PlatformCore>(true).OrderBy(p=>p.name).Select(p=>p.gameObject).ToArray();

    // Reopen-based checks inspect serialized references and reusable prefab data.
    public static string Structure()
    {
        results.Clear();var data=DeployStage14Revision.Read();var platforms=Platforms();
        Check(platforms.Length==29,"29 platform instances");
        for(int i=0;i<29;i++) {
            var go=platforms[i];var p=data.platforms[i];var sr=go.GetComponent<SpriteRenderer>();var box=go.GetComponent<BoxCollider2D>();
            Check(go.name.Contains(p.id+"_"+p.type) && PrefabUtility.IsPartOfPrefabInstance(go),p.id+" correct type / prefab");
            Check(go.transform.lossyScale==Vector3.one && sr.drawMode==SpriteDrawMode.Tiled && sr.tileMode==SpriteTileMode.Continuous && !box.autoTiling && Vector2.Distance(box.size,sr.size)<.001f,p.id+" tiled / scale / dimensions");
            Check(Mathf.Abs(sr.sprite.bounds.size.y-1.03f)<.001f,p.id+" one full-height forest tile");
            Check(sr.sprite.border.x==96 && sr.sprite.border.z==96,p.id+" complete left/right end caps");
            if(p.type=="Moving") {
                var so=new SerializedObject(go.GetComponent<MovingPlatform>());var path=(PlatformPath)so.FindProperty("path").objectReferenceValue;
                Check(path!=null && !path.transform.IsChildOf(go.transform) && path.PointCount==2 && Mathf.Abs(Vector3.Distance(path.GetPoint(0),path.GetPoint(1))-3)<.001f,p.id+" stationary explicit path / 3-unit travel");
                Check(so.FindProperty("speed").floatValue==p.moveSpeed && so.FindProperty("startDelay").floatValue==p.startDelay,p.id+" distinct speed / delay");
            }
        }
        Check(Root().GetComponentsInChildren<MonsterStats>(true).Length==2,"2 ordinary ground patrols");
        var stage=All<StageManager>().Single();var zones=(CameraBoundsZone[])Get(stage,"zoneCameraBounds");var z=zones[3].WorldBounds;
        for(int i=0;i<5;i++)if(i!=3){var b=zones[i].WorldBounds;Check(!(Mathf.Min(z.max.x,b.max.x)-Mathf.Max(z.min.x,b.min.x)>.002f && Mathf.Min(z.max.y,b.max.y)-Mathf.Max(z.min.y,b.min.y)>.002f),"4 separated from zone "+(i+1));}
        float upperLeft=zones.Take(4).Min(v=>v.WorldBounds.min.x),upperRight=zones.Take(4).Max(v=>v.WorldBounds.max.x),upperBottom=zones.Take(4).Min(v=>v.WorldBounds.min.y);
        Check(Mathf.Abs(zones[4].WorldBounds.center.x-(upperLeft+upperRight)/2)<.002f && zones[4].WorldBounds.max.y<upperBottom,"Boss arena centered below entire upper row");
        File.WriteAllLines("@Edit/Stage14_Revision_Structure_20261015.txt",results);return "PASS "+results.Count+" structure checks";
    }
    // Allocate a free TestNNN path; other sessions' Test000 etc. remain untouched.
    public static string Prepare()
    {
        int i=0;string path;do{path="Assets/Scenes/Test"+i.ToString("D3")+".unity";i++;}while(File.Exists(path));
        if(!AssetDatabase.CopyAsset("Assets/Scenes/Stage1.unity",path))throw new IOException("Test copy failed");
        SessionState.SetString("Stage14RevisionTest",path);EditorSceneManager.OpenScene(path);return path;
    }

    // Place both Transform and Rigidbody to avoid the teleport frame's stale Transform pose.
    static void Place(Rigidbody2D body,float x,float feet,float offset,Transform parent)
    {
        body.transform.SetParent(parent,true);body.transform.position=new Vector3(x,feet+offset,0);body.position=body.transform.position;body.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
    }
    // Per-link physics probes isolate geometric reach from temporal gimmick windows and monsters.
    public static string Run()
    {
        if(!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=TestPath)throw new InvalidOperationException("Owned TestNNN in Play required");
        results.Clear();var oldMode=Physics2D.simulationMode;
        try {
            Time.timeScale=1;Physics2D.simulationMode=SimulationMode2D.Script;
            var stage=All<StageManager>().Single();var player=(Transform)Get(stage,"player");var body=player.GetComponent<Rigidbody2D>();var collider=player.GetComponent<Collider2D>();
            var gate=All<AreaContentStartManager>().Single();var content=(GameObject)Get(gate,"content");
            if(Get(gate,"stage")!=null && !gate.HasStarted && content.activeSelf)Call(gate,"Awake");
            Check(!gate.HasStarted && !content.activeSelf,"Area gimmicks dormant before 1-4 entry");
            while(stage.CurrentArea<4)stage.AdvanceToNextArea();
            if(!gate.HasStarted)Call(gate,"Start");
            Check(gate.HasStarted && content.activeInHierarchy,"1-4 entry activates fresh gimmick clocks");
            var data=DeployStage14Revision.Read();var platforms=Platforms();float origin=platforms[0].transform.position.x;
            foreach(var b in player.GetComponents<MonoBehaviour>())if(!(b is PlayerHealth) && !(b is PlayerStats) && !(b is PlayerEquipment))b.enabled=false;
            foreach(var monster in All<MonsterStats>()){var rb=monster.GetComponent<Rigidbody2D>();if(rb!=null)rb.simulated=false;}
            foreach(var boss in All<Boss>()){var rb=boss.GetComponent<Rigidbody2D>();if(rb!=null)rb.simulated=false;boss.GetComponent<BossController>().enabled=false;}
            foreach(var b in Root().GetComponentsInChildren<PlatformBehaviour>(true)){b.StopAllCoroutines();b.enabled=false;b.GetComponent<PlatformCore>().SetPlatformActive(true);}
            body.interpolation=RigidbodyInterpolation2D.None;body.gravityScale=3;
            // Align the just-teleported pose before measuring pivot-to-feet offset.
            player.position=body.position;Physics2D.SyncTransforms();
            float offset=player.position.y-collider.bounds.min.y;var parent=player.parent;
            Action resetMovers=()=>{
                for(int k=0;k<29;k++)if(data.platforms[k].type=="Moving"){
                    var p=data.platforms[k];var go=platforms[k];var rb=go.GetComponent<Rigidbody2D>();
                    ((IDictionary)Get(go.GetComponent<MovingPlatform>(),"riders")).Clear();
                    go.transform.position=new Vector3(origin+p.x,p.lowerY-p.thickness/2,0);rb.position=go.transform.position;rb.linearVelocity=Vector2.zero;
                }Physics2D.SyncTransforms();
            };
            foreach(float jump in new[]{9f,7.2f})for(int i=0;i<28;i++) {
                player.SetParent(parent,true);resetMovers();
                var from=data.platforms[i];var to=data.platforms[i+1];
                if(from.type=="Moving"){var go=platforms[i];go.transform.position+=Vector3.up*3;go.GetComponent<Rigidbody2D>().position=go.transform.position;Physics2D.SyncTransforms();}
                var src=platforms[i].GetComponent<Collider2D>().bounds;var dst=platforms[i+1].GetComponent<Collider2D>().bounds;
                float half=collider.bounds.extents.x;float sx=src.max.x-half-.1f,tx=dst.min.x+half+.1f;
                float dy=dst.max.y-src.max.y;float flight=(jump+Mathf.Sqrt(jump*jump-2*29.43f*dy))/29.43f;
                float vx=(tx-sx)/flight;Check(vx<=8.001f,from.id+"->"+to.id+" velocity within base 8 at jump "+jump);
                Place(body,sx,src.max.y+.02f,offset,parent);body.linearVelocity=new Vector2(vx,jump);bool landed=false;
                for(int step=0;step<70;step++) {
                    Physics2D.Simulate(.02f);
                    var bounds=collider.bounds;
                    if(body.linearVelocity.y<=.05f && Mathf.Abs(bounds.min.y-dst.max.y)<.06f && bounds.center.x>=dst.min.x+half-.03f && bounds.center.x<=dst.max.x-half+.03f){landed=true;break;}
                    if(bounds.min.y<dst.max.y-.2f && body.linearVelocity.y<0)break;
                }
                Check(landed,from.id+"->"+to.id+" physical landing with jump "+jump);
            }
            // Evaluate the real lift rider transport over multiple complete trips.
            for(int i=0;i<29;i++)if(data.platforms[i].type=="Moving") {
                player.SetParent(parent,true);resetMovers();var p=data.platforms[i];var mover=platforms[i].GetComponent<MovingPlatform>();
                Set(mover,"targetIndex",1);Set(mover,"direction",1);Set(mover,"finished",false);Call(mover,"Start");
                Place(body,origin+p.x,p.lowerY+.02f,offset,parent);float worst=0,maxTop=p.lowerY;
                for(int step=0;step<230;step++) {Call(mover,"FixedUpdate");Physics2D.Simulate(.02f);float top=mover.GetComponent<Collider2D>().bounds.max.y;maxTop=Mathf.Max(maxTop,top);if(step>10)worst=Mathf.Max(worst,Mathf.Abs(collider.bounds.min.y-top));}
                Check(maxTop>=p.upperY-.05f && worst<.16f,p.id+" actual rider transport reaches upper endpoint; worst foot error="+worst);
            }
            player.SetParent(parent,true);resetMovers();
            // Advance real iterator state machines without claiming wall-clock coroutine playback.
            foreach(var blink in Root().GetComponentsInChildren<BlinkingFoothold>(true)) {
                blink.StopAllCoroutines();blink.GetComponent<PlatformCore>().SetPlatformActive(true);
                var loop=(IEnumerator)blink.GetType().GetMethod("BlinkLoop",Flags).Invoke(blink,null);
                loop.MoveNext();if((float)Get(blink,"startDelay")>0)loop.MoveNext();
                Check(blink.GetComponent<Collider2D>().enabled,"Blink visible "+blink.name);loop.MoveNext();Check(!blink.GetComponent<Collider2D>().enabled,"Blink hidden "+blink.name);loop.MoveNext();Check(blink.GetComponent<Collider2D>().enabled,"Blink restored "+blink.name);
            }
            foreach(var vanish in Root().GetComponentsInChildren<DisappearingFoothold>(true)) {
                vanish.StopAllCoroutines();vanish.GetComponent<PlatformCore>().SetPlatformActive(true);
                var routine=(IEnumerator)vanish.GetType().GetMethod("VanishAfterDelay",Flags).Invoke(vanish,null);routine.MoveNext();routine.MoveNext();
                Check(!vanish.GetComponent<Collider2D>().enabled,"Vanish removes support "+vanish.name);routine.MoveNext();Check(vanish.GetComponent<Collider2D>().enabled,"Vanish respawns support "+vanish.name);
            }
            foreach(var damage in Root().GetComponentsInChildren<DamageFoothold>(true)) {
                Set(damage,"cycleTimer",2.5f);Call(damage,"UpdateCycle");Check(damage.IsDamaging,"Damage active phase "+damage.name);
                Set(damage,"cycleTimer",0f);Call(damage,"UpdateCycle");Check(!damage.IsDamaging,"Damage safe phase "+damage.name);
            }
            var end=All<ZoneEndPoint>().Single(e=>e.name=="ZoneEndPoint_1-4");
            Place(body,end.transform.position.x,16.82f,offset,parent);Physics2D.Simulate(.02f);
            Check(stage.CurrentArea==5,"Actual goal trigger advances to 1-5");
            var starts=(ZoneStartPoint[])Get(stage,"zoneStartPoints");Check(Vector2.Distance(body.position,starts[4].transform.position)<.15f,"Goal teleports to relocated boss start");
            File.WriteAllLines("@Edit/Stage14_Revision_Runtime_20261015.txt",results);return "PASS "+results.Count+" controlled runtime checks";
        }
        catch(Exception e){File.WriteAllLines("@Edit/Stage14_Revision_Runtime_20261015.txt",results.Concat(new[]{e.ToString()}));return e.ToString();}
        finally{Time.timeScale=0;Physics2D.simulationMode=oldMode;}
    }
    // Restore serialized fresh positions after stopping Play; remove only this run's owned scene.
    public static string Cleanup()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first");
        string path=TestPath;EditorSceneManager.OpenScene("Assets/Scenes/Stage1.unity");
        if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/Scenes/Test"))throw new InvalidOperationException("No owned test");
        AssetDatabase.DeleteAsset(path);SessionState.EraseString("Stage14RevisionTest");return "Owned test removed; Stage1 restored";
    }
}
