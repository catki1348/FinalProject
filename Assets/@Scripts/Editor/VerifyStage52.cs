using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Checks the saved main scene against the source placement data.
public static class VerifyStage52
{
    [Serializable] public class Report
    {
        public string scene;
        public int platforms,movers,monsters,pairedDrops,fallStrips,missingScripts;
        public Vector3 start,goal;
        public List<string> checks=new List<string>();
    }

    static void Require(bool condition,string message) { if(!condition)throw new InvalidOperationException(message); }

    [MenuItem("Tools/Verify/Stage 5-2 Saved Structure")]
    public static string Capture()
    {
        return CaptureTo("@Edit/Stage52_SavedStructure_20261015.json");
    }

    // Codex (GPT-6), 2026-10-16. Versioned reports keep earlier implementation evidence intact.
    public static string CaptureTo(string reportPath)
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Require(scene.path=="Assets/Scenes/Stage5.unity","Open saved Stage5");
        var d=JsonUtility.FromJson<PrepareStage52.Design>(File.ReadAllText("@Edit/Stage52_UnityPreparation.json"));
        var map=new Dictionary<string,Transform>();var report=new Report{scene=scene.path};
        foreach(var r in scene.GetRootGameObjects())foreach(var t in r.GetComponentsInChildren<Transform>(true)) {
            report.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if(!map.ContainsKey(t.name))map.Add(t.name,t);
        }
        Require(report.missingScripts==0,"Missing scripts in Stage5");
        var root=map["Generated_5-2"];
        var zone=map["Section_2"].GetComponent<CameraBoundsZone>().WorldBounds;
        Require(root.GetComponentsInChildren<PlatformCore>(true).Length==67,"Platform count");
        foreach(var p in d.platforms) {
            Require(map.ContainsKey(p.id),"Missing platform "+p.id);var t=map[p.id];
            var col=t.GetComponent<BoxCollider2D>();var sr=t.GetComponent<SpriteRenderer>();
            Require(PrefabUtility.IsPartOfPrefabInstance(t),"Not prefab: "+p.id);
            Require(t.lossyScale==Vector3.one,"Stretched platform: "+p.id);
            Require(sr.drawMode==SpriteDrawMode.Tiled&&sr.tileMode==SpriteTileMode.Continuous,"Tiling: "+p.id);
            Require(!col.autoTiling&&Vector2.Distance(col.size,new Vector2(p.width,.5f))<.001f&&col.size==sr.size,"Collider/art size: "+p.id);
            report.platforms++;
            if(p.points.Length>0) {
                report.movers++;var so=new SerializedObject(t.GetComponent<MovingPlatform>());
                var path=(PlatformPath)so.FindProperty("path").objectReferenceValue;
                Require(path!=null&&path.PointCount==p.points.Length&&!path.transform.IsChildOf(t),"Path reference: "+p.id);
                Require(Mathf.Abs(so.FindProperty("speed").floatValue-p.speed)<.001f,"Speed: "+p.id);
                Require(Mathf.Abs(so.FindProperty("startDelay").floatValue-p.delay)<.001f,"Delay: "+p.id);
                Require(Mathf.Abs(so.FindProperty("endpointWait").floatValue-p.wait)<.001f,"Wait: "+p.id);
                Require(so.FindProperty("loopMode").enumValueIndex==(p.loop?0:1),"Loop: "+p.id);
                Require(Mathf.Abs(so.FindProperty("synchronizedCycleDuration").floatValue-p.syncPeriod)<.001f,"Cycle lock: "+p.id);
                for(int i=0;i<p.points.Length;i++) {
                    var v=(Vector3)(d.origin.V+p.points[i].V)+Vector3.down*.25f;
                    Require(Vector3.Distance(path.GetPoint(i),v)<.001f,"Path point: "+p.id);
                    Require(v.x-p.width/2>=zone.min.x&&v.x+p.width/2<=zone.max.x&&v.y-.25f>=zone.min.y&&v.y+.25f<=zone.max.y,"Path outside camera zone: "+p.id);
                }
                Require(Vector3.Distance(t.position,path.GetPoint(0))<.001f,"Initial position: "+p.id);
            } else {
                Require(Vector3.Distance(t.position,(Vector3)(d.origin.V+new Vector2(p.x,p.y-.25f)))<.001f,"Static position: "+p.id);
                var b=col.bounds;Require(b.min.x>=zone.min.x&&b.max.x<=zone.max.x&&b.min.y>=zone.min.y&&b.max.y<=zone.max.y,"Outside zone: "+p.id);
            }
        }
        foreach(var m in d.monsters) {
            var t=map[m.id];report.monsters++;
            Require(PrefabUtility.IsPartOfPrefabInstance(t),"Monster prefab: "+m.id);
            Require(t.GetComponent<IMonsterPattern>().GetType().Name==m.pattern,"Pattern: "+m.id);
            Require(Vector3.Distance(t.position,(Vector3)(d.origin.V+new Vector2(m.x,m.y)))<.001f,"Monster position: "+m.id);
            var entries=new SerializedObject(t.GetComponent<MonsterItemDropper>()).FindProperty("entries");
            if(m.guaranteed) {
                report.pairedDrops++;Require(entries.arraySize==2,"Paired drop count");
                for(int i=0;i<2;i++){var e=entries.GetArrayElementAtIndex(i);Require(e.FindPropertyRelative("chance").floatValue==1&&e.FindPropertyRelative("condition").enumValueIndex==0&&e.FindPropertyRelative("prefab").objectReferenceValue!=null,"Guaranteed drop: "+m.id);}
            }
        }
        foreach(var strip in d.fallTriggers) {
            var t=map[strip.id];var box=t.GetComponent<BoxCollider2D>();report.fallStrips++;
            Require(PrefabUtility.IsPartOfPrefabInstance(t)&&t.GetComponent<OutOfBoundsTrigger>()&&box.isTrigger,"Fall prefab: "+strip.id);
            Require(Vector3.Distance(t.position,(Vector3)(d.origin.V+new Vector2((strip.x0+strip.x1)/2,strip.y)))<.001f,"Fall location: "+strip.id);
        }
        report.start=map["ZoneStartPoint_5-2"].position;report.goal=map["ZoneEndPoint_5-2"].position;
        Require(Vector3.Distance(report.start,new Vector3(532,.03f,0))<.001f,"Start marker");
        // 2026-10-15: explicit 5-1 deployment replaces the placeholder spawn with a supported start.
        if(map.ContainsKey("Generated_5-1"))
            Require(map["Section_1"].GetComponent<CameraBoundsZone>().WorldBounds.Contains(map["ZoneStartPoint_5-1"].position),"5-1 spawn outside its zone");
        else Require(map["ZoneStartPoint_5-1"].position==new Vector3(1,0,0),"5-1 placeholder spawn moved");
        var next=(CameraBoundsZone)new SerializedObject(map["ZoneEndPoint_5-2"].GetComponent<ZoneEndPoint>()).FindProperty("nextZone").objectReferenceValue;
        Require(next==map["Section_3"].GetComponent<CameraBoundsZone>(),"Exit next-zone reference");
        var zones=new List<CameraBoundsZone>();foreach(var t in map.Values)if(t.GetComponent<CameraBoundsZone>())zones.Add(t.GetComponent<CameraBoundsZone>());
        for(int i=0;i<zones.Count;i++)for(int j=i+1;j<zones.Count;j++) {
            var a=zones[i].WorldBounds;var b=zones[j].WorldBounds;
            Require(!(Mathf.Min(a.max.x,b.max.x)-Mathf.Max(a.min.x,b.min.x)>.001f&&Mathf.Min(a.max.y,b.max.y)-Mathf.Max(a.min.y,b.min.y)>.001f),"Overlapping zones");
        }
        Require(report.platforms==67&&report.movers==20&&report.monsters==31&&report.pairedDrops==22&&report.fallStrips==13,"Totals");
        report.checks.Add("Counts, prefab links, tiling, colliders, coordinates and path timing match design");
        report.checks.Add("Zones do not overlap; 5-1 start is valid; endpoint references 5-3");
        var json=JsonUtility.ToJson(report,true);File.WriteAllText(reportPath,json);return json;
    }
}
