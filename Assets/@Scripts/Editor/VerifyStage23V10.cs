// AI work record: Codex; model unknown; 2026-10-16. Disposable-scene physics checks for Stage2-3.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
public static class VerifyStage23V10
{
    // Calls the existing platform controller with real manual Physics2D steps, never in the main scene.
    public static string Run(){
        if(!Application.isPlaying||!SceneManager.GetActiveScene().name.StartsWith("Test"))throw new Exception("Test scene in play mode required");
        var root=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).First(t=>t.name=="Generated_2-3_v10");
        var moving=root.GetComponentsInChildren<MovingPlatform>();var monsters=root.GetComponentsInChildren<MonsterController>();
        if(moving.Length!=7||monsters.Length!=30)throw new Exception("Count mismatch");
        // Freeze ordinary frame-driven scripts; explicitly invoke only the systems under test.
        foreach(var m in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))m.enabled=false;
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();player.GetComponent<Rigidbody2D>().position=new Vector2(-200,30);
        var type=typeof(MovingPlatform);var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var fixedStep=type.GetMethod("FixedUpdate",flags);var start=type.GetMethod("Start",flags);
        foreach(var m in moving){type.GetField("targetIndex",flags).SetValue(m,1);type.GetField("currentIndex",flags).SetValue(m,0);type.GetField("direction",flags).SetValue(m,1);type.GetField("finished",flags).SetValue(m,false);start.Invoke(m,null);}
        var six=moving.First(m=>m.name.EndsWith("M6"));var seven=moving.First(m=>m.name.EndsWith("M7"));
        var c6=six.GetComponent<BoxCollider2D>();var c7=seven.GetComponent<BoxCollider2D>();
        var a=new Vector2[6200];var b=new Vector2[6200];var seen=new bool[7];var oldMode=Physics2D.simulationMode;Physics2D.simulationMode=SimulationMode2D.Script;
        int collisions=0;float minSpace=999;
        try{
            for(int k=0;k<a.Length;k++){
                foreach(var m in moving)fixedStep.Invoke(m,null);
                Physics2D.Simulate(.02f);
                a[k]=new Vector2(c6.bounds.center.x,c6.bounds.max.y);b[k]=new Vector2(c7.bounds.center.x,c7.bounds.max.y);
                var aa=c6.bounds;var bb=c7.bounds;if(Mathf.Abs(aa.center.x-bb.center.x)<2.999f&&Mathf.Abs(aa.center.y-bb.center.y)<.499f)collisions++;
                for(int j=0;j<moving.Length;j++){var so=new SerializedObject(moving[j]);var path=(PlatformPath)so.FindProperty("path").objectReferenceValue;if(Vector2.Distance(moving[j].GetComponent<Rigidbody2D>().position,path.GetPoint(path.PointCount-1))<.15f)seen[j]=true;}
            }
        }finally{Physics2D.simulationMode=oldMode;}
        if(collisions>0||seen.Any(x=>!x))throw new Exception("Platform physics failure: collisions="+collisions+" ends="+string.Join(",",seen));
        // Evaluate complete monster and platform swept bounds using actual runtime collider dimensions.
        int pairs=0;
        foreach(var mon in monsters){var pat=mon.GetComponents<MonoBehaviour>().First(c=>c is IMonsterPattern);var mp=(PlatformPath)new SerializedObject(pat).FindProperty("path").objectReferenceValue;var mc=mon.GetComponent<BoxCollider2D>();var mb=new Bounds(mp.GetPoint(0),mc.bounds.size);for(int i=1;i<mp.PointCount;i++){var q=new Bounds(mp.GetPoint(i),mc.bounds.size);mb.Encapsulate(q);}
            foreach(var lift in moving){var path=(PlatformPath)new SerializedObject(lift).FindProperty("path").objectReferenceValue;var lb=new Bounds(path.GetPoint(0),lift.GetComponent<BoxCollider2D>().bounds.size);for(int i=1;i<path.PointCount;i++)lb.Encapsulate(new Bounds(path.GetPoint(i),lift.GetComponent<BoxCollider2D>().bounds.size));float dx=Mathf.Max(0,Mathf.Max(mb.min.x-lb.max.x,lb.min.x-mb.max.x));float dy=Mathf.Max(0,Mathf.Max(mb.min.y-lb.max.y,lb.min.y-mb.max.y));float clear=Mathf.Sqrt(dx*dx+dy*dy);minSpace=Mathf.Min(minSpace,clear);pairs++;if(clear<.499f)throw new Exception("Monster sweep collision "+mon.name+" "+lift.name);}}
        // Isolated real collision landing probes driven by the measured M6/M7 trajectories.
        var sandbox=SceneManager.CreateScene("Test024Jump",new CreateSceneParameters(LocalPhysicsMode.Physics2D));var ps=sandbox.GetPhysicsScene2D();int hits=0;float last=-1,maxWait=0;
        try{
            var cam=new GameObject("Camera");SceneManager.MoveGameObjectToScene(cam,sandbox);cam.AddComponent<Camera>().enabled=false;
            var light=new GameObject("Light");SceneManager.MoveGameObjectToScene(light,sandbox);light.AddComponent<Light>().type=LightType.Directional;
            var target=new GameObject("Target");SceneManager.MoveGameObjectToScene(target,sandbox);var tc=target.AddComponent<BoxCollider2D>();tc.size=new Vector2(3,.5f);var tr=target.AddComponent<Rigidbody2D>();tr.bodyType=RigidbodyType2D.Kinematic;
            var probe=new GameObject("Probe");SceneManager.MoveGameObjectToScene(probe,sandbox);var pc=probe.AddComponent<BoxCollider2D>();pc.size=new Vector2(.98f,1.2865f);var rb=probe.AddComponent<Rigidbody2D>();rb.gravityScale=3;rb.constraints=RigidbodyConstraints2D.FreezeRotation;rb.collisionDetectionMode=CollisionDetectionMode2D.Continuous;
            for(int k=100;k<6100;k+=5){rb.position=a[k]+new Vector2(1,.64325f+.02f);rb.linearVelocity=new Vector2(8,9*Mathf.Sqrt(.8f));tr.position=b[k]-new Vector2(0,.25f);rb.WakeUp();bool ok=false;
                for(int n=1;n<38;n++){tr.MovePosition(b[k+n]-new Vector2(0,.25f));rb.linearVelocity=new Vector2(8,rb.linearVelocity.y);ps.Simulate(.02f);if(rb.IsTouching(tc)&&pc.bounds.min.y>tc.bounds.max.y-.08f){ok=true;break;}}
                if(ok){hits++;if(last>=0)maxWait=Mathf.Max(maxWait,k*.02f-last);last=k*.02f;}
            }
        }finally{SceneManager.UnloadSceneAsync(sandbox);}
        if(hits==0||maxWait>4.2f)throw new Exception("Transfer probe failure hits="+hits+" wait="+maxWait);
        // The actual death event must disable collision and remove horizontal corpse drift.
        var bat=monsters.First(m=>m.GetComponent<FlyingPathPatrolPattern>());var body=bat.GetComponent<Rigidbody2D>();body.linearVelocity=new Vector2(1,1);bat.GetComponent<MonsterStats>().TakeDamage(int.MaxValue);
        if(body.linearVelocity.x!=0||body.gravityScale==0||bat.GetComponent<Collider2D>().enabled)throw new Exception("Corpse collision policy failed");
        string report="PASS moving endpoints 7/7; pair collision frames="+collisions+"/6200; monster/platform pairs="+pairs+"; minimum complete-sweep clearance="+minSpace+"; real Physics2D transfer landings="+hits+"/1200; max successful launch gap="+maxWait+"; corpse death gravity/no collider/no drift PASS";
        File.WriteAllText("@Edit/Stage23_RuntimeValidation.txt",report);Debug.Log(report);return report;
    }
}
