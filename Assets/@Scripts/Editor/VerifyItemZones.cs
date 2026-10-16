using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Codex, model unknown, 2026-10-15. Controlled runtime probes only in a disposable scene.
public static class VerifyItemZones
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Base = "Assets/@ExternalAssets/@Prefab/";
    static readonly List<string> passed = new List<string>();
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("FAIL: " + description);
        passed.Add(description);
    }
    static object Get(object target, string field) { return target.GetType().GetField(field, Flags).GetValue(target); }
    static void Set(object target, string field, object value) { target.GetType().GetField(field, Flags).SetValue(target,value); }
    static void Call(object target, string method) { target.GetType().GetMethod(method, Flags).Invoke(target,null); }
    static T[] All<T>() where T : Component
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();
    }
    static GameObject Prefab(string name) { return AssetDatabase.LoadAssetAtPath<GameObject>(Base+name+".prefab"); }

    // Physics triggers perform the pickup, rather than directly calling its effect method.
    static void Pickup(GameObject prefab, PlayerHealth health)
    {
        var item=UnityEngine.Object.Instantiate(prefab,health.GetComponent<Collider2D>().bounds.center,Quaternion.identity);
        Physics2D.SyncTransforms(); Physics2D.Simulate(.02f);
        item.SetActive(false); UnityEngine.Object.Destroy(item);
    }

    public static string Run()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (!Application.isPlaying || scene.path != "Assets/Scenes/Test043.unity") throw new InvalidOperationException("Own disposable Test043 scene in Play mode required; other tests are protected");
        passed.Clear();
        float oldScale=Time.timeScale; var simulation=Physics2D.simulationMode;
        Time.timeScale=0; Physics2D.simulationMode=SimulationMode2D.Script;
        try {
            var stage=All<StageManager>().Single();
            var health=((Transform)Get(stage,"player")).GetComponent<PlayerHealth>();
            var movement=health.GetComponent<PlayerMovement>(); var rb=health.GetComponent<Rigidbody2D>();
            var combat=health.GetComponent<PlayerCombat>(); var stats=health.GetComponent<PlayerStats>();
            var fury=health.GetComponent<PlayerFuryBuff>(); var speed=health.GetComponent<PlayerSpeedBuff>(); var dash=health.GetComponent<PlayerDash>();
            movement.enabled=false; combat.enabled=false; dash.enabled=false; rb.gravityScale=0;
            rb.interpolation=RigidbodyInterpolation2D.None;
            foreach(var monster in All<MonsterStats>()) { var body=monster.GetComponent<Rigidbody2D>(); if(body!=null) body.simulated=false; }
            while(stage.CurrentArea<2) stage.AdvanceToNextArea();
            Check(stage.CurrentArea==2,"Stage4-2 entry");
            var runway=All<RunwayDamage>().Single(); int before=health.CurrentHealth;
            runway.AdvanceExposure(.3f,true); runway.AdvanceExposure(10,false); runway.AdvanceExposure(.2f,true);
            Check(health.CurrentHealth==before-1,"4-2 floor: 0.3 grounded + 10 air + 0.2 grounded = 1 damage");
            rb.position=new Vector2(120,6); Physics2D.SyncTransforms();
            for(int i=0;i<6;i++) Pickup(Prefab("P_Item_SpeedBoost"),health);
            Check(speed.Stacks==5 && Mathf.Approximately(movement.SpeedMultiplier,1.25f),"Physics pickups: speed caps at 5 / movement x1.25");
            Check(Mathf.Approximately(dash.DistanceMultiplier,1.25f)&&Mathf.Approximately(dash.SpeedMultiplier,1.25f)&&Mathf.Approximately(dash.CooldownMultiplier,.75f),"Speed changes dash distance/speed/cooldown together");
            // First step of the actual coroutine sets the boosted dash velocity and lock.
            var routine=(IEnumerator)typeof(PlayerDash).GetMethod("DashRoutine",Flags).Invoke(dash,null);
            routine.MoveNext();
            float expected=(float)Get(dash,"dashDistance")/(float)Get(dash,"dashDuration")*1.25f;
            Check(Mathf.Abs(Mathf.Abs(rb.linearVelocity.x)-expected)<.001f,"Actual dash coroutine uses boosted velocity");
            movement.MovementLocked=false; rb.linearVelocity=Vector2.zero;
            stage.AdvanceToNextArea();
            Check(stage.CurrentArea==3 && fury.AreaActive && !runway.Running,"4-2 -> 4-3 enables fury and stops floor damage");
            Check(speed.Stacks==5,"4-2 speed stacks survive transition to 4-3");
            rb.position=new Vector2(620,6); Physics2D.SyncTransforms();
            int baseline=stats.FinalAttack;
            Pickup(Prefab("Stage4_3_Fury/P_Item_Fury"),health);
            Check(fury.Stacks==1 && stats.FinalAttack==baseline+10,"Physics fury pickup: +10 additive attack");
            for(int i=0;i<104;i++) fury.AddStack();
            Check(fury.Stacks==105 && stats.FinalAttack==baseline+1050,"Fury reaches 105 stacks (+1050), no five-stack cap");
            Set(speed,"endTime",Time.time-1); Call(speed,"Update");
            Check(speed.Stacks==0 && movement.SpeedMultiplier==1 && dash.DistanceMultiplier==1 && dash.CooldownMultiplier==1 && fury.Stacks==105,"Speed expiry restores movement/dash, leaves fury unchanged");
            stats.SetDefenseAttackBonus(.2f);
            Check(stats.FinalAttack==(int)Math.Round(baseline*1.2d)+1050,"Defense multiplier does not multiply the additive fury bonus");
            stats.SetDefenseAttackBonus(0);
            var ui=All<ItemBuffHUD>().Single(); Call(ui,"Refresh");
            Check(((UnityEngine.UI.Text)Get(ui,"label")).text.Contains("1050"),"HUD displays actual fury bonus");
            // Test each gate once, then repeat at the end; no duplicate groups are created.
            var manager=All<RunwayEncounterManager>().Single(m=>(int)Get(m,"requiredArea")==3);
            int[] counts={5,10,15,20,25,30};
            var origin=(Transform)Get(manager,"startPoint");
            for(int i=0;i<6;i++) { health.transform.position=new Vector3(origin.position.x+i*80,6,0); rb.position=health.transform.position; Physics2D.SyncTransforms(); Call(manager,"Update"); }
            Check(manager.ActivatedGroups==6,"All six fury distance gates activate once");
            Call(manager,"Update"); Check(manager.ActivatedGroups==6,"Repeated gate evaluation never respawns a group");
            var groups=(Array)Get(manager,"groups");
            for(int i=0;i<6;i++) {
                var group=groups.GetValue(i); var root=(GameObject)group.GetType().GetField("root").GetValue(group);
                Check(root.GetComponentsInChildren<MonsterStats>(true).Length==counts[i],"Fury group "+(i+1)+" count "+counts[i]);
            }
            // Actual death event creates exactly one of each drop at the death position.
            var enemy=UnityEngine.Object.Instantiate(Prefab("Stage4_3_Fury/P_Stage43_FuryChaser"),new Vector3(625,6,0),Quaternion.identity);
            enemy.GetComponent<Rigidbody2D>().simulated=false;
            int f0=All<FuryItem>().Length, s0=All<SpeedBoostItem>().Length;
            enemy.GetComponent<MonsterStats>().TakeDamage(100000);
            var newFury=All<FuryItem>().Where(x=>x.gameObject.activeSelf && Vector2.Distance(x.transform.position,enemy.transform.position)<.01f).ToArray();
            Check(All<FuryItem>().Length==f0+1 && All<SpeedBoostItem>().Length==s0+1 && newFury.Length==1,"Fury monster death: one fury + one speed at exact death position");
            enemy.GetComponent<MonsterStats>().TakeDamage(100000);
            Check(All<FuryItem>().Length==f0+1,"Dead enemy cannot duplicate its drop");
            // Isolate actual sword overlap from crowd AI. A single swing must hit more than 8 bodies.
            foreach(var m in All<MonsterStats>()) foreach(var col in m.GetComponents<Collider2D>()) col.enabled=false;
            rb.position=new Vector2(640,6); Physics2D.SyncTransforms(); Call(combat,"UpdateAttackPointFacing");
            var point=(Transform)Get(combat,"attackPoint"); var crowd=new List<MonsterStats>();
            for(int i=0;i<12;i++) {
                var m=UnityEngine.Object.Instantiate(Prefab("Stage4_3_Fury/P_Stage43_FuryChaser"),point.position,Quaternion.identity);
                var body=m.GetComponent<Rigidbody2D>(); body.gravityScale=0;
                var col=m.GetComponent<Collider2D>(); m.transform.position+=point.position-col.bounds.center;
                crowd.Add(m.GetComponent<MonsterStats>());
            }
            Physics2D.SyncTransforms(); Call(combat,"PerformSwordAttack");
            Check(crowd.All(m=>m.IsDead),"One real sword overlap kills 12 unique bodies (old 8-hit bottleneck removed)");
            // Existing field cap remains independent of stack count, including simultaneous drops.
            var active=(IList)typeof(ItemLifetime).GetField("active",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            Check(active.Count<=20,"Field lifetime registry stays within 20 items after crowd deaths");
            stats.SetFuryAttackBonus(long.MaxValue);
            Check(stats.FinalAttack==int.MaxValue,"Huge bonuses saturate damage safely instead of overflowing");
            fury.AddStack();
            speed.AddStack(.05f,5,5);
            int speedBefore=speed.Stacks;
            stage.AdvanceToNextArea();
            Check(stage.CurrentArea==4 && !fury.AreaActive && fury.Stacks==0 && stats.FinalAttack==baseline,"4-3 -> 4-4 clears fury and restores base/equipment attack");
            Check(speed.Stacks==speedBefore && !fury.AddStack(),"Area exit preserves speed and rejects fury pickups");
            fury.SetAreaActive(true); fury.AddStack();
            // Invoke the health death event through its actual damage path; reset timers only for isolation.
            health.TakeEnvironmentalDamage(100000); health.TakeEnvironmentalDamage(100000);
            Check(!fury.AreaActive && fury.Stacks==0,"Actual player death clears fury");
            Time.timeScale=0;
            string result="PASS "+passed.Count+" checks\n"+string.Join("\n",passed);
            File.WriteAllText("@Edit/ItemZones_TestResults_20261015.txt",result);
            return result;
        } catch(Exception ex) {
            File.WriteAllText("@Edit/ItemZones_TestResults_20261015.txt","PASS before failure: "+passed.Count+"\n"+string.Join("\n",passed)+"\n"+ex);
            throw;
        } finally { Physics2D.simulationMode=simulation; Time.timeScale=oldScale; }
    }
}
