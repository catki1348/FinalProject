using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Isolated structural and controlled physics probes.
public static class VerifyStage44
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> passed=new List<string>();
    static string TestPath => SessionState.GetString("Stage44TestPath","");
    static T[] All<T>() where T:Component => UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    static object Get(object o,string f) => o.GetType().GetField(f,Flags).GetValue(o);
    static void Set(object o,string f,object v) => o.GetType().GetField(f,Flags).SetValue(o,v);
    static void Call(object o,string m) => o.GetType().GetMethod(m,Flags).Invoke(o,null);
    static void Check(bool ok,string message) {if(!ok)throw new InvalidOperationException("FAIL: "+message);passed.Add(message);}

    // Never overwrite Test000 or another session's scene. Keep one owned disposable copy.
    public static string Prepare()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Edit mode required");
        int i=0;string path;
        do {path="Assets/Scenes/Test"+i.ToString("D3")+".unity";i++;} while(File.Exists(path));
        if(!AssetDatabase.CopyAsset("Assets/Scenes/Stage4.unity",path))throw new IOException("Test copy failed");
        SessionState.SetString("Stage44TestPath",path);
        EditorSceneManager.OpenScene(path);
        return path;
    }

    // Verify saved prefab connections and geometry after reopening, before play-mode mutation.
    public static string Structure()
    {
        passed.Clear();
        var manager=All<SuperRunManager>().Single();
        var p=(SequentialFallingPlatform[])Get(manager,"platforms");
        var start=(Transform)Get(manager,"startPoint");
        Check(p.Length==150,"150 serialized falling supports");
        for(int i=0;i<p.Length;i++) {
            var box=p[i].GetComponent<BoxCollider2D>();var sr=p[i].GetComponent<SpriteRenderer>();
            Check(PrefabUtility.IsPartOfPrefabInstance(p[i]) && p[i].transform.lossyScale==Vector3.one && sr.drawMode==SpriteDrawMode.Tiled && sr.tileMode==SpriteTileMode.Continuous && !box.autoTiling && box.size==sr.size,"Support "+(i+1)+" prefab / scale / tiled / collider");
            if(i>0)Check(Mathf.Abs(p[i-1].GetComponent<SpriteRenderer>().bounds.max.x-sr.bounds.min.x)<.002f,"Contiguous seam "+i);
        }
        var end=All<ZoneEndPoint>().Single(e=>e.transform.parent==start.parent);
        Check(Mathf.Abs(end.transform.position.x-start.position.x-1200)<.001f,"Route exactly 1200 units");
        Check(All<MonsterStats>().Count(m=>m.transform.IsChildOf(manager.transform))==30,"30 proposed ordinary monsters");
        var z=start.parent.GetComponent<CameraBoundsZone>().WorldBounds;
        Check(p[0].GetComponent<SpriteRenderer>().bounds.min.x>=z.min.x && p[149].GetComponent<SpriteRenderer>().bounds.max.x<=z.max.x,"All supports inside camera area");
        var zones=All<CameraBoundsZone>();
        foreach(var other in zones) if(other.transform!=start.parent) {
            var b=other.WorldBounds;
            Check(!(z.min.x<b.max.x && z.max.x>b.min.x && z.min.y<b.max.y && z.max.y>b.min.y),"4-4 separated from "+other.name);
        }
        File.WriteAllLines("@Edit/Stage44_Structure_20261015.txt",passed);
        return "PASS "+passed.Count+" saved-structure checks";
    }

    // Runtime checks use actual scene references and physics; no main scene is modified.
    public static string Run()
    {
        if(!Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=TestPath)throw new InvalidOperationException("Owned Test scene Play mode required");
        passed.Clear();
        float oldScale=Time.timeScale;var oldSimulation=Physics2D.simulationMode;
        try {
            Time.timeScale=1;Physics2D.simulationMode=SimulationMode2D.Script;
            var manager=All<SuperRunManager>().Single();var stage=(StageManager)Get(manager,"stage");
            // MCP can invoke while the editor's Play transition has not advanced a frame.
            // Establish a deterministic fresh-attempt probe instead of using wall-clock editor delays.
            foreach(var support in (SequentialFallingPlatform[])Get(manager,"platforms"))support.ResetSupport();
            Set(manager,"<ElapsedSeconds>k__BackingField",0f);Set(manager,"<FallenCount>k__BackingField",0);
            Set(manager,"<Running>k__BackingField",false);Set(manager,"attempted",false);Set(manager,"dead",false);
            foreach(var e in (SuperRunManager.Encounter[])Get(manager,"encounters")) {e.activated=false;e.root.SetActive(false);}
            Set(stage,"<CurrentArea>k__BackingField",1);
            var player=(PlayerHealth)Get(manager,"player");var rb=player.GetComponent<Rigidbody2D>();
            var movement=player.GetComponent<PlayerMovement>();var dash=player.GetComponent<PlayerDash>();var speed=player.GetComponent<PlayerSpeedBuff>();
            movement.enabled=false;player.GetComponent<PlayerCombat>().enabled=false;dash.enabled=false;
            rb.interpolation=RigidbodyInterpolation2D.None;
            foreach(var monster in All<MonsterStats>()) {var b=monster.GetComponent<Rigidbody2D>();if(b!=null)b.simulated=false;}
            while(stage.CurrentArea<4)stage.AdvanceToNextArea();
            if(Get(manager,"playerBody")==null)Call(manager,"Start");
            Physics2D.SyncTransforms();Call(manager,"Update");
            Check(manager.Running && speed.Stacks==5 && speed.HasZoneMaximum,"Entry grants maximum speed and starts sequence after teleport");
            Check(Mathf.Approximately(movement.SpeedMultiplier,1.25f) && Mathf.Approximately(dash.DistanceMultiplier,1.25f) && Mathf.Approximately(dash.SpeedMultiplier,1.25f) && Mathf.Approximately(dash.CooldownMultiplier,.75f),"All four maximum acceleration effects");
            for(int i=0;i<6;i++)speed.AddStack(.05f,5,5);
            Set(speed,"endTime",Time.time-1);Call(speed,"Update");
            Check(speed.Stacks==5 && float.IsPositiveInfinity(speed.RemainingTime),"Normal pickup expiry cannot remove zone maximum");
            var supports=(SequentialFallingPlatform[])Get(manager,"platforms");
            manager.AdvanceSequence(.99f,0);Check(manager.FallenCount==0,"No immediate fall at t=0");
            manager.AdvanceSequence(1,0);Check(manager.FallenCount==1 && !supports[0].GetComponent<Collider2D>().enabled && supports[1].GetComponent<Collider2D>().enabled,"t=1 only first support loses collision");
            manager.AdvanceSequence(3.4f,0);Check(manager.FallenCount==3 && !supports[2].GetComponent<Collider2D>().enabled && supports[3].GetComponent<Collider2D>().enabled,"Slow-frame catch-up preserves absolute one-second order");
            Physics2D.SyncTransforms();
            float startX=((Transform)Get(manager,"startPoint")).position.x;
            Check(Physics2D.Raycast(new Vector2(startX+2,4),Vector2.down,8,LayerMask.GetMask("Ground")).collider==null && Physics2D.Raycast(new Vector2(startX+28,4),Vector2.down,8,LayerMask.GetMask("Ground")).collider!=null,"Merged physics surface removes fallen inputs without ghost support");
            Time.timeScale=0;float paused=manager.ElapsedSeconds;Call(manager,"Update");
            Check(manager.ElapsedSeconds==paused,"Pause does not advance the fall clock");Time.timeScale=1;
            manager.AdvanceSequence(150,1200);Physics2D.SyncTransforms();
            Check(manager.FallenCount==150 && supports.All(s=>s.HasFallen && !s.GetComponent<Collider2D>().enabled) && Physics2D.Raycast(new Vector2(startX+1200,4),Vector2.down,8,LayerMask.GetMask("Ground")).collider==null,"t=150 removes all 150 supports including the goal support");
            var ordinary=All<MonsterStats>().First(m=>m.transform.IsChildOf(manager.transform) && m.gameObject.activeInHierarchy);
            int damage=player.GetComponent<PlayerStats>().FinalAttack;
            ordinary.TakeDamage(damage);ordinary.TakeDamage(damage);
            Check(ordinary.IsDead,"4-4 ordinary enemy dies in two baseline attacks");
            // Reset only the controlled probe state to run the full no-enemy geometry traversal.
            foreach(var support in supports)support.ResetSupport();
            Set(manager,"<ElapsedSeconds>k__BackingField",0f);
            rb.position=((Transform)Get(manager,"startPoint")).position;rb.linearVelocity=Vector2.zero;
            Physics2D.SyncTransforms();Set(movement,"moveInput",1f);
            float origin=rb.position.x;int steps=0;bool reached=false;
            for(int i=1;i<=6100;i++) {
                float elapsed=(i-1)*.02f;
                manager.AdvanceSequence(elapsed,rb.position.x-origin);
                Call(movement,"FixedUpdate");Physics2D.Simulate(.02f);steps=i;
                if(stage.CurrentArea==5){reached=true;break;}
                if(rb.position.y < -5)throw new InvalidOperationException("Fell during geometry route at step "+i+" x="+rb.position.x);
            }
            Check(reached,"Actual movement + 2D physics traverses all seams and triggers 4-5 goal; last position="+rb.position);
            Check(Mathf.Abs(steps*.02f-120)<1,"Full route travel approximately 120s: "+(steps*.02f).ToString("0.00"));
            Check(!manager.Running && !speed.HasZoneMaximum && speed.Stacks==0 && movement.SpeedMultiplier==1,"4-5 transition removes zone maximum without leakage");
            // Check ordinary pickup coexistence independently from the zone source.
            speed.AddStack(.05f,5,5);speed.SetZoneMaximum(manager,true);speed.SetZoneMaximum(manager,false);
            Check(speed.Stacks==1 && Mathf.Approximately(movement.SpeedMultiplier,1.05f),"Exit preserves unexpired ordinary pickup source");
            speed.SetZoneMaximum(manager,true);GameOverManager.Instance.TriggerGameOver();
            Check(!speed.HasZoneMaximum && !manager.Running,"Fall/game-over event revokes zone source before pause");
            // A sequence does not restart itself from repeated entry notifications in one attempt.
            Check((bool)Get(manager,"attempted"),"Attempt remains one-shot until scene restart");
            File.WriteAllLines("@Edit/Stage44_Runtime_20261015.txt",passed);
            return "PASS "+passed.Count+" runtime probes; full route tested with enemy physics disabled";
        }
        catch(Exception e) {File.WriteAllLines("@Edit/Stage44_Runtime_20261015.txt",passed.Concat(new[]{e.ToString()}));return e.ToString();}
        finally {Time.timeScale=0;Physics2D.simulationMode=oldSimulation;}
    }

    // Reloading the owned scene is the game's fresh-attempt boundary; verify serialized restoration.
    public static string Restore()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first");
        EditorSceneManager.OpenScene(TestPath);
        var manager=All<SuperRunManager>().Single();var p=(SequentialFallingPlatform[])Get(manager,"platforms");
        if(p.Any(s=>!s.GetComponent<Collider2D>().enabled || s.HasFallen))throw new InvalidOperationException("Restart support restore failed");
        return "PASS: scene restart restores all 150 supports and fresh manager state";
    }

    // Delete only the test scene created by Prepare, then return the editor to the real Stage4.
    public static string Cleanup()
    {
        if(Application.isPlaying)throw new InvalidOperationException("Stop Play first");
        string path=TestPath;EditorSceneManager.OpenScene("Assets/Scenes/Stage4.unity");
        if(string.IsNullOrEmpty(path)||!path.StartsWith("Assets/Scenes/Test"))throw new InvalidOperationException("No owned test");
        AssetDatabase.DeleteAsset(path);SessionState.EraseString("Stage44TestPath");
        return "Owned test scene removed; Stage4 restored";
    }
}
