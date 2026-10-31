using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using HarmonyLib;
using System.Reflection;
using IObject = Il2CppSystem.Object;
using IType = Il2CppSystem.Type;
using IFlags = Il2CppSystem.Reflection.BindingFlags;

namespace AstralPersonalMod {
 [BepInPlugin("local.astral.personalcollector", "Astral Personal Collector", "0.8.7")]
 public sealed class Plugin : BasePlugin {
  internal static Plugin Instance;
  internal string Backend, Python, WorkerExecutable, DataDir;
  private Harmony framePatch;
  private Process workerProcess;
  private static Watcher watcher;
  private static ProfileStatsUI profileUI;
  private static RuntimeGameCatalog gameCatalog;
  private static int lastFrame=-1;
  private static float nextFrameError;
  private static bool pumpLogged;
  public override void Load() {
   Instance=this;
   string pluginFolder=Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
   Backend=Config.Bind<string>("Paths","BackendDirectory",Path.Combine(pluginFolder,"worker"),"Worker folder; legacy source backend folder is also supported").Value;
   WorkerExecutable=Config.Bind<string>("Paths","WorkerExecutable",Path.Combine(pluginFolder,"worker","AstralStatsWorker.exe"),"Standalone background worker").Value;
   Python=Config.Bind<string>("Paths","PythonExecutable","","Optional legacy Python runtime; not needed for distribution").Value;
   DataDir=Config.Bind<string>("Paths","DataDirectory",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AstralPersonalStats"),"Personal database, configuration and replay cache").Value;
   Directory.CreateDirectory(DataDir);Directory.CreateDirectory(Path.Combine(DataDir,"plugin-inbox"));
   RuntimeDiagnostics.Initialize(DataDir,"0.8.7");
   // Do not use ClassInjector/AddComponent: the installed bridge's metadata hook crashes this HybridCLR game.
   watcher=new Watcher();profileUI=new ProfileStatsUI();gameCatalog=new RuntimeGameCatalog();
   var update=typeof(UnityEngine.EventSystems.EventSystem).GetMethod("Update",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
   if(update==null)throw new MissingMethodException("EventSystem.Update");
   framePatch=new Harmony("local.astral.personalcollector.frame");
   framePatch.Patch(update,postfix:new HarmonyMethod(typeof(Plugin).GetMethod(nameof(OnUnityFrame),BindingFlags.Static|BindingFlags.NonPublic)));
   Log.LogInfo("Personal collector 0.8.7 loaded without class injection. Waiting for existing Unity event loop.");
  }
  private static void OnUnityFrame() {
   if(Instance==null||watcher==null||profileUI==null)return;
   int frame=Time.frameCount;if(frame==lastFrame)return;lastFrame=frame;
   // Let the startup scene and hot-update assemblies finish their initial loading first.
   float now=Time.realtimeSinceStartup;if(now<10f)return;
   long diagnosticStart=RuntimeDiagnostics.Clock(),taskStart=diagnosticStart;string diagnosticPhase="watcher";
   try {
    if(!pumpLogged){pumpLogged=true;Instance.Log.LogInfo("Existing Unity event loop ready; no injected types or native delegate conversion.");}
    watcher.Update();taskStart=RuntimeDiagnostics.Mark(0,taskStart);diagnosticPhase="catalog";gameCatalog.Update();taskStart=RuntimeDiagnostics.Mark(1,taskStart);diagnosticPhase="profile_ui";profileUI.Update();taskStart=RuntimeDiagnostics.Mark(2,taskStart);diagnosticPhase="pointer";profileUI.PollPointer();RuntimeDiagnostics.Mark(3,taskStart);
   }catch(Exception error){RuntimeDiagnostics.Error(diagnosticPhase,error);if(now>=nextFrameError){nextFrameError=now+15f;Instance.Log.LogWarning("Frame task retry: "+error.GetType().Name);}}finally{RuntimeDiagnostics.CompleteFrame(diagnosticStart);}
  }
  internal static void ObserveOwnHistory(IObject career){watcher?.ObserveOwnHistory(career);}
  internal void StartWorker() {
   if(workerProcess!=null){try{if(!workerProcess.HasExited)return;}catch{}workerProcess.Dispose();workerProcess=null;}
   ProcessStartInfo info;
   string arguments=" --data-dir "+Quote(DataDir)+" --parent-pid "+Process.GetCurrentProcess().Id;
   if(File.Exists(WorkerExecutable)){info=new ProcessStartInfo(WorkerExecutable,arguments);info.WorkingDirectory=Path.GetDirectoryName(WorkerExecutable);}
   else {
    string script=Path.Combine(Backend,"astral_plugin_worker.py");
    if(!File.Exists(script)||!File.Exists(Python)){Log.LogWarning("Background worker missing; reinstall Astral Personal Stats.");return;}
    info=new ProcessStartInfo(Python,Quote(script)+arguments);info.WorkingDirectory=Backend;
   }
   info.UseShellExecute=false;info.CreateNoWindow=true;
   workerProcess=Process.Start(info);RuntimeDiagnostics.Event("worker_started");Log.LogInfo("Background worker start requested.");
  }
  internal static string Quote(string value){return "\""+value.Replace("\"","")+"\"";}
 }
 public sealed class Watcher {
  private float nextTick,nextWorker,nextHeartbeat;
  private IType managerType;
  private long owner,lastOwner,roomId,startTime,candidateId,careerAccountId,canonicalId,observedState,observedSelf;
  private float nextHistory;
  private bool roomPresent;
  private IObject historyRequest;
  private long historyOwner;
  private float historyNext,historyDeadline;
  private int historyAttempts,historySeen;
  private string historyStatus="waiting_for_login";
  private int absent;
  private string stage="initializing";
  private bool running;
  private HashSet<string> emitted=new HashSet<string>();
  private static readonly IFlags Flags=IFlags.Public|IFlags.NonPublic|IFlags.Instance|IFlags.Static|IFlags.FlattenHierarchy;
  private IType FindType(string name) {
   foreach(var assembly in Il2CppSystem.AppDomain.CurrentDomain.GetAssemblies()) {
    try {IType type=assembly.GetType(name,false,false);if(type!=null)return type;}catch{}
   }
   return null;
  }
  private static IObject Read(IObject target,string name) {
   if(target==null)return null;
   IType type=target.GetIl2CppType();
   for(int depth=0;type!=null&&depth<12;depth++,type=type.BaseType) {
    var field=type.GetField(name,Flags)??type.GetField("<"+name+">k__BackingField",Flags);
    if(field!=null)return field.GetValue(target);
    var property=type.GetProperty(name,Flags);
    if(property!=null)return property.GetValue(target,null);
   }
   return null;
  }
  private static IObject Call(IObject target,string method) {
   if(target==null)return null;var member=target.GetIl2CppType().GetMethod(method,Flags);
   return member==null?null:member.Invoke(target,null);
  }
  private static IObject Singleton(IType type) {
   for(int depth=0;type!=null&&depth<12;depth++,type=type.BaseType) {
    var property=type.GetProperty("inst",Flags);
    if(property!=null)return property.GetValue(null,null);
    var field=type.GetField("inst",Flags);
    if(field!=null)return field.GetValue(null);
    var method=type.GetMethod("get_inst",Flags);
    if(method!=null)return method.Invoke(null,null);
   }
   return null;
  }
  private static long Number(IObject value){return NativeValue.Integer(value);}
  private static bool Truth(IObject value){return NativeValue.Boolean(value);}
  private void Status(string status) {
   Plugin p=Plugin.Instance;
   string json="{\"version\":\"0.8.7\",\"game_pid\":"+Process.GetCurrentProcess().Id+",\"owner_id\":"+owner+",\"owner_verified\":"+(owner>0?"true":"false")+",\"canonical_id\":"+canonicalId+",\"candidate_id\":"+candidateId+",\"career_account_id\":"+careerAccountId+",\"history_status\":\""+historyStatus+"\",\"history_seen\":"+historySeen+",\"room_present\":"+(roomPresent?"true":"false")+",\"room_state\":"+observedState+",\"self_id\":"+observedSelf+",\"start_time\":"+startTime+",\"room_id\":"+roomId+",\"heartbeat\":"+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+",\"status\":\""+status+"\"}";
   string temp=Path.Combine(p.DataDir,"plugin-state.json.part");File.WriteAllText(temp,json);
   string target=Path.Combine(p.DataDir,"plugin-state.json");
   if(File.Exists(target))File.Delete(target);File.Move(temp,target);
  }
  private void Emit(string reason) {QueueReplay(roomId,startTime,reason);historyNext=Math.Min(historyNext,Time.realtimeSinceStartup+10);}
  private void QueueReplay(long room,long start,string reason) {
   if(owner<=0||room<=0||room>=1000000||start<1000000000||start>4102444800)return;
   string rid=(start*1000000+room).ToString();string key=owner+"_"+rid+"_"+reason;
   if(emitted.Contains(key))return;
   string json="{\"schema\":1,\"source\":\"astral-personal-mod\",\"game_pid\":"+Process.GetCurrentProcess().Id+",\"owner_id\":"+owner+",\"owner_verified\":"+(owner>0?"true":"false")+",\"room_id\":"+room+",\"start_time\":"+start+",\"replay_id\":\""+rid+"\",\"reason\":\""+reason+"\"}";
   string folder=Path.Combine(Plugin.Instance.DataDir,"plugin-inbox");string temp=Path.Combine(folder,key+".part");
   File.WriteAllText(temp,json);File.Move(temp,Path.Combine(folder,key+".json"));emitted.Add(key);
   Plugin.Instance.Log.LogInfo("Replay queued: "+rid+" ("+reason+")");
  }
  private void QueueReplayId(string rid){if(rid.Length<10||rid.Length>18||!long.TryParse(rid,out long id)||id<=0)return;historySeen++;QueueReplay(id%1000000,id/1000000,"history");}
  private void ReadRecordList(IObject records,string idField){if(records==null)return;int count=(int)NativeUI.Long(records,"Count");for(int i=0;i<Math.Min(count,10);i++){var record=NativeUI.Call(records,"get_Item",i);QueueReplayId(NativeUI.Text(Read(record,idField)));}}
  private void QueueCareer(IObject career){if(career==null||Number(Read(career,"playerId"))!=owner)return;ReadRecordList(Read(career,"Records"),"ReplayId");ReadRecordList(Read(career,"_replayRecord"),"ReplayId");}
  internal void ObserveOwnHistory(IObject career){float now=Time.realtimeSinceStartup;if(now<nextHistory||owner<=0||career==null||Number(Read(career,"playerId"))!=owner||File.Exists(Path.Combine(Plugin.Instance.DataDir,"plugin-disabled.flag")))return;nextHistory=now+5;try{QueueCareer(career);}catch(Exception error){Plugin.Instance.Log.LogWarning("Own history scan retry: "+error.GetType().Name);}}
  private void HistoryRetry(float now,string status){historyRequest=null;historyStatus=status;historyAttempts++;historyNext=now+(historyAttempts>=3?600:30);if(historyAttempts>=3)historyAttempts=0;Plugin.Instance.Log.LogWarning("Automatic own-history sync: "+status);}
  private void PollOwnHistory(IObject account,float now){
   if(owner<=0)return;
   if(historyOwner!=owner){historyOwner=owner;historyRequest=null;historyAttempts=historySeen=0;historyNext=now+5;historyStatus="waiting_for_lobby";}
   if(historyRequest!=null){
    if(!Truth(Read(historyRequest,"isCompleted"))){if(now>=historyDeadline)HistoryRetry(now,"timeout");return;}
    if(Number(Read(historyRequest,"errId"))!=0||Truth(Read(historyRequest,"isTimeOut"))){HistoryRetry(now,"server_error");return;}
    var response=Read(historyRequest,"resultParam0");var show=Read(response,"showData_");
    if(show!=null&&Number(Read(show,"playerId_"))!=owner){HistoryRetry(now,"profile_owner_mismatch");return;}
    if(show!=null){historySeen=0;ReadRecordList(Read(show,"record_"),"replayId_");ReadRecordList(Read(show,"replayRecord_"),"replayId_");}
    else {var career=Read(account,"playerCareer");if(career==null||Number(Read(career,"playerId"))!=owner){if(now>=historyDeadline)HistoryRetry(now,"own_profile_not_ready");return;}historySeen=0;QueueCareer(career);}
    historyRequest=null;historyAttempts=0;historyStatus="scanned";historyNext=now+600;Plugin.Instance.Log.LogInfo("Automatic own-history sync completed; replay ID candidates: "+historySeen);return;
   }
   if(now<historyNext||running||ProfileStatsUI.AnyProfileVisible)return;
   try {
    var net=Singleton(FindType("Core.Net.NetManager"));if(net==null||!Truth(Read(net,"IsConnected")))return;
    var rpc=Read(Read(net,"RPC"),"GetShowPlayerC2S");if(rpc==null){HistoryRetry(now,"rpc_unavailable");return;}
    var request=NativeUI.New("party.protocol.GetShowPlayerC2S");NativeUI.Set(request,"playerId_",owner);
    // Use the existing authenticated game connection. Do not call ShowAccountInfo or attach UI-opening callbacks.
    historyRequest=NativeUI.Call(rpc,"GetShowPlayerC2SCall",request);historyDeadline=now+15;historyNext=now+30;historyStatus="requesting";
    if(historyRequest==null)HistoryRetry(now,"request_not_started");
   }catch(Exception error){HistoryRetry(now,"request_"+error.GetType().Name);}
  }
  public void Update() {
   float now=Time.realtimeSinceStartup;if(now<nextTick)return;nextTick=now+1f;
   try {
    bool paused=File.Exists(Path.Combine(Plugin.Instance.DataDir,"plugin-disabled.flag"));
    stage="find_game_types";if(managerType==null)managerType=FindType("GameLogic.GameLogicManager");
    if(managerType==null){if(now>=nextHeartbeat){Status("waiting_for_game_types");nextHeartbeat=now+10;}return;}
    stage="manager_singleton";IObject manager=Singleton(managerType);if(manager==null)return;
    stage="account_id";IObject account=Read(manager,"account");IObject player=Read(account,"player");long candidate=Number(Read(player,"id_"));long careerId=Number(Read(Read(account,"playerCareer"),"playerId"));candidateId=candidate;careerAccountId=careerId;canonicalId=Number(Call(account,"GetPlayerID"));owner=canonicalId>0?canonicalId:0;
    ProfileStatsUI.AccountId=owner;
    if(owner!=lastOwner){lastOwner=owner;running=false;roomId=startTime=0;emitted.Clear();historyOwner=0;historyRequest=null;Plugin.Instance.Log.LogInfo("Current game account: "+owner);}
    if(now>=nextWorker){Plugin.Instance.StartWorker();nextWorker=now+120f;}
    if(now>=nextHeartbeat){Status(paused?"paused":owner>0?"watching":"waiting_for_login");nextHeartbeat=now+10;}
    if(paused){running=false;return;}
    if(owner<=0)return;
    stage="replay_guard";IObject session=Read(Read(manager,"replay"),"Session");
    if(Truth(Read(session,"IsReplay"))){running=false;return;}
    stage="own_history_sync";PollOwnHistory(account,now);
    stage="room_info";IObject room=Read(Read(manager,"room"),"curRoomInfo");roomPresent=room!=null;observedState=0;observedSelf=0;
    if(room==null){if(running&&++absent>=6){Emit("left_room");running=false;}return;}
    absent=0;
    stage="own_room_guard";IObject self=Call(room,"GetSelfInfo");observedSelf=Number(Read(Read(self,"serverPlayer"),"id_"));if(observedSelf!=owner)return;
    stage="room_state";IObject serverRoom=Read(room,"info");long stateCode=Number(Read(room,"State"));observedState=stateCode;
    long currentRoom=Number(Read(serverRoom,"id_"));long start=Number(Read(serverRoom,"startTime_"));
    if(stateCode==25&&start>0){roomId=currentRoom;startTime=start;running=true;}
    if(stateCode==30&&start>0){roomId=currentRoom;startTime=start;Emit("finish");running=false;}
   }catch(Exception error){
    RuntimeDiagnostics.Error("watcher."+stage,error);
    if(now>=nextHeartbeat){Plugin.Instance.Log.LogWarning("Collector read retry at "+stage+": "+error.GetType().Name);Status("reflection_retry");nextHeartbeat=now+15;}
   }
  }
 }
}

