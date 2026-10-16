using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Codex (GPT-6), 2026-10-15. Read-only scene audit; exports collider-based geometry.
public static class AuditStage22
{
    [Serializable] public class Point { public float x,y; public Point(float x,float y){this.x=x;this.y=y;} }
    [Serializable] public class Platform
    {
        public string id,kind,prefab,drawMode;
        public float x,top,width,height,scaleX,scaleY,speed,delay,wait,vanish,respawn;
        public int loop;
        public bool behaviorAddedInScene,pathConnected,pathOutsidePlatform;
        public Point[] points;
    }
    [Serializable] public class Zone {public string name;public float x0,x1,y0,y1;}
    [Serializable] public class Snapshot
    {
        public string scene,scope="read_only_audit";
        public float playerWidth,playerHeight,moveSpeed,jumpForce,gravity;
        public Point start,end;
        public int missingScripts;
        public List<Platform> platforms=new List<Platform>();
        public List<Zone> zones=new List<Zone>();
    }

    public static string Capture()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/Stage2.unity")throw new InvalidOperationException("Open Stage2 for read-only audit");
        var snapshot=new Snapshot{scene=scene.path};Transform section=null;PlayerMovement player=null;
        foreach(var root in scene.GetRootGameObjects())foreach(var t in root.GetComponentsInChildren<Transform>(true)) {
            snapshot.missingScripts+=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
            if(t.name=="Section_2")section=t;
            if(t.TryGetComponent<PlayerMovement>(out var p))player=p;
            if(t.TryGetComponent<CameraBoundsZone>(out var z)){var b=z.WorldBounds;snapshot.zones.Add(new Zone{name=t.name,x0=b.min.x,x1=b.max.x,y0=b.min.y,y1=b.max.y});}
        }
        var pb=player.GetComponent<Collider2D>().bounds;
        snapshot.playerWidth=pb.size.x;snapshot.playerHeight=pb.size.y;
        var ps=new SerializedObject(player);snapshot.moveSpeed=ps.FindProperty("moveSpeed").floatValue;snapshot.jumpForce=ps.FindProperty("jumpForce").floatValue;
        snapshot.gravity=-Physics2D.gravity.y*player.GetComponent<Rigidbody2D>().gravityScale;
        foreach(var t in section.GetComponentsInChildren<Transform>(true)) {
            if(t.GetComponent<ZoneStartPoint>())snapshot.start=new Point(t.position.x,t.position.y);
            if(t.GetComponent<ZoneEndPoint>())snapshot.end=new Point(t.position.x,t.position.y);
            if(!Regex.IsMatch(t.name,@"^(N\d+|MP\d+|D\d+)$"))continue;
            var col=t.GetComponent<BoxCollider2D>();var sr=t.GetComponent<SpriteRenderer>();var bounds=col.bounds;
            var p=new Platform{id=t.name,kind="normal",prefab=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject),drawMode=sr.drawMode.ToString(),x=bounds.center.x,top=bounds.max.y,width=bounds.size.x,height=bounds.size.y,scaleX=t.lossyScale.x,scaleY=t.lossyScale.y,points=new Point[0]};
            if(t.TryGetComponent<MovingPlatform>(out var m)) {
                p.kind="moving";var so=new SerializedObject(m);var path=(PlatformPath)so.FindProperty("path").objectReferenceValue;
                p.pathConnected=path!=null;p.pathOutsidePlatform=path!=null&&!path.transform.IsChildOf(t);
                p.speed=so.FindProperty("speed").floatValue;p.delay=so.FindProperty("startDelay").floatValue;p.wait=so.FindProperty("endpointWait").floatValue;p.loop=so.FindProperty("loopMode").enumValueIndex;
                p.behaviorAddedInScene=PrefabUtility.GetCorrespondingObjectFromSource(m)==null;
                if(path!=null){p.points=new Point[path.PointCount];for(int i=0;i<path.PointCount;i++){var v=path.GetPoint(i);p.points[i]=new Point(v.x,v.y+bounds.extents.y);}}
            }
            if(t.TryGetComponent<DisappearingFoothold>(out var disappearing)) {
                p.kind="disappearing";var so=new SerializedObject(disappearing);p.vanish=so.FindProperty("vanishDelay").floatValue;p.respawn=so.FindProperty("respawnDelay").floatValue;
                p.behaviorAddedInScene=PrefabUtility.GetCorrespondingObjectFromSource(disappearing)==null;
            }
            snapshot.platforms.Add(p);
        }
        string json=JsonUtility.ToJson(snapshot,true);File.WriteAllText("@Edit/Stage22_Audit_20261015.json",json);return json;
    }
}
