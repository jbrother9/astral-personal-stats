using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using IObject=Il2CppSystem.Object;
namespace AstralPersonalMod {
 internal sealed class RuntimeLandLayouts {
  public sealed class Node {public int node_id{get;set;}public int land_type{get;set;}}
  public sealed class Status {public int index{get;set;}public int land_type{get;set;}public bool active{get;set;}}
  public sealed class Group {public int group_id{get;set;}public List<int> nodes{get;set;}public List<Status> statuses{get;set;}}
  public sealed class Layout {public int map_id{get;set;}public int map_data_id{get;set;}public List<Node> base_nodes{get;set;}public List<Group> groups{get;set;}}
  public sealed class Cache {public int schema{get;set;}public string source{get;set;}public bool complete{get;set;}public List<Layout> layouts{get;set;}}
  float next;string roomStamp,completed;int attempts;
  internal void Invalidate(){next=0;roomStamp=completed=null;attempts=0;}
  void Waiting(string stage,string detail=""){if(attempts==1||attempts==8)RuntimeDiagnostics.Event("land_layout_wait","stage="+stage+" attempt="+attempts+(detail.Length>0?" "+detail:""));}
  internal void Update(){
   float now=Time.realtimeSinceStartup;if(now<next||ProfileStatsUI.AccountId<=0)return;next=now+3;
   string stage="room";try{
    var manager=NativeUI.Static("GameLogic.GameLogicManager","inst");var roomLogic=NativeUI.Get(manager,"room");var localRoom=NativeUI.Get(NativeUI.Get(roomLogic,"roomController"),"localRoom");
    if(localRoom==null||NativeUI.Long(localRoom,"State")!=25)return;
    // localRoom is GameLogic.RoomInfo: it has no Lands or MapDataId.
    // Its info field is the original party.model.Room used by the replay.
    var room=NativeUI.Get(localRoom,"info");if(room==null)return;
    int map=(int)NativeUI.Long(room,"MapId"),data=(int)NativeUI.Long(room,"MapDataId");
    string stamp=NativeUI.Long(room,"Id")+"/"+NativeUI.Long(room,"StartTime")+"/"+map+"/"+data;
    if(stamp==completed)return;if(stamp!=roomStamp){roomStamp=stamp;attempts=0;}if(++attempts>8)return;
    if(map<=0||data<0||NativeUI.Long(room,"Id")<=0||NativeUI.Long(localRoom,"Id")!=NativeUI.Long(room,"Id")||NativeUI.Long(localRoom,"MapId")!=map||NativeUI.Long(localRoom,"StartTime")!=NativeUI.Long(room,"StartTime")){Waiting("room_identity");return;}
    stage="land_manager";
    // LandManager is a SimpleSingletonProvider, not a UnityEngine.Object.
    // This is the same inst reference the game's board/UI code uses.
    var landManager=NativeUI.Static("Core.Unit.LandManager","inst");
    if(landManager==null){Waiting(stage);return;}
    stage="land_groups";var groupType=NativeUI.Type("Core.Unit.LandGroupManager");if(groupType==null){Waiting(stage);return;}
    // LandGroupManager is a scene MonoBehaviour; discover it only during the
    // new room's bounded capture, never as an idle UI scan.
    var groupManager=NativeUI.CallStatic("UnityEngine.Object","FindObjectOfType",groupType);
    if(groupManager==null){Waiting(stage);return;}
    stage="nodes";
    var sceneNodes=NativeUI.Get(landManager,"NodeDict");var roomNodes=NativeUI.Get(room,"Lands");int count=(int)NativeUI.Long(roomNodes,"Count");
    long sceneCount=NativeUI.Long(sceneNodes,"Count");
    if(count<=0||count>2000||sceneNodes==null||sceneCount!=count){Waiting("node_count","room_nodes="+count+" scene_nodes="+sceneCount+" room_nodes_missing="+(roomNodes==null)+" scene_nodes_missing="+(sceneNodes==null));return;}
    var nodes=new List<Node>();var seen=new HashSet<int>();var enumerator=NativeUI.Call(roomNodes,"GetEnumerator");
    while(NativeValue.Boolean(NativeUI.Call(enumerator,"MoveNext"))){
     var pair=NativeUI.Get(enumerator,"Current");int id=(int)NativeUI.Long(pair,"Key");var value=NativeUI.Get(pair,"Value");int kind=(int)NativeUI.Long(value,"LandType");
     if(!seen.Add(id)||!NativeValue.Boolean(NativeUI.Call(sceneNodes,"ContainsKey",id))){Waiting("node_identity");return;}
     var node=NativeUI.Call(sceneNodes,"get_Item",id);if(NativeUI.Long(node,"Id")!=id||NativeUI.Long(NativeUI.Get(node,"nodeInfo"),"landType")!=kind){Waiting("node_type");return;}
     nodes.Add(new Node{node_id=id,land_type=kind});
    }
    if(nodes.Count!=count){Waiting("node_enumeration");return;}nodes.Sort((a,b)=>a.node_id.CompareTo(b.node_id));
    stage="groups";var groupList=NativeUI.Get(groupManager,"LandGroups");int groupCount=(int)NativeUI.Long(groupList,"Count");if(groupList==null||groupCount<0||groupCount>256)return;
    var groups=new List<Group>();var groupIds=new HashSet<int>();
    for(int i=0;i<groupCount;i++){
     var group=NativeUI.Call(groupList,"get_Item",i);int id=(int)NativeUI.Long(group,"LandGroupId");if(id!=i+1||!groupIds.Add(id))return;
     var members=NativeUI.Get(group,"Lands");var statuses=NativeUI.Get(group,"LandStatus");int memberCount=(int)NativeUI.Long(members,"Count"),statusCount=(int)NativeUI.Long(statuses,"Count");
     if(members==null||statuses==null||memberCount<0||memberCount>2000||statusCount<1||statusCount>256)return;
     var entry=new Group{group_id=id,nodes=new List<int>(),statuses=new List<Status>()};var memberIds=new HashSet<int>();
     for(int j=0;j<memberCount;j++){int node=(int)NativeUI.Long(NativeUI.Call(members,"get_Item",j),"Id");if(!seen.Contains(node)||!memberIds.Add(node))return;entry.nodes.Add(node);}
     for(int j=0;j<statusCount;j++){var status=NativeUI.Call(statuses,"get_Item",j);entry.statuses.Add(new Status{index=j,land_type=(int)NativeUI.Long(status,"LandType"),active=NativeUI.Bool(status,"Active")});}
     entry.nodes.Sort();groups.Add(entry);
    }
    stage="write";var layout=new Layout{map_id=map,map_data_id=data,base_nodes=nodes,groups=groups};string path=Path.Combine(Plugin.Instance.DataDir,"plugin-land-layouts.json");
    var cache=new Cache{schema=1,source="live_scene_land_groups",complete=true,layouts=new List<Layout>()};
    if(File.Exists(path)&&new FileInfo(path).Length<=4194304){try{var previous=JsonSerializer.Deserialize<Cache>(File.ReadAllText(path));if(previous?.schema==1&&previous.source==cache.source&&previous.complete&&previous.layouts!=null&&previous.layouts.Count<=128)cache.layouts=previous.layouts;}catch(JsonException){}}
    cache.layouts.RemoveAll(x=>x.map_id==map&&x.map_data_id==data);if(cache.layouts.Count>=128)return;cache.layouts.Add(layout);
    File.WriteAllText(path+".part",JsonSerializer.Serialize(cache));File.Move(path+".part",path,true);completed=stamp;RuntimeDiagnostics.Event("land_layout_cached","map="+map+" map_data="+data+" nodes="+count+" groups="+groups.Count);
   }catch(Exception error){RuntimeDiagnostics.Event("land_layout_failed","stage="+stage);RuntimeDiagnostics.Error("catalog.land_layout",error);}
  }
 }
}
