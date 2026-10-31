using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using IObject=Il2CppSystem.Object;
namespace AstralPersonalMod {
 internal sealed class RuntimeGameCatalog {
  internal static long Generation{get;private set;}
  static int revision;int seenRevision;
  internal static void Invalidate(){revision++;}
  static Dictionary<int,int> portraits=new Dictionary<int,int>();
  internal static int PortraitGroup(int hero){return portraits.TryGetValue(hero,out int group)&&group>0?group:hero;}
  sealed class Entry {public int id{get;set;}public int name_key{get;set;}public string name{get;set;}public int rarity{get;set;}public int portrait_group{get;set;}}
  sealed class Spec {internal string Category,Config,Localization,Id,Name,List;internal bool Rarity,Portrait;internal Spec(string category,string config,string local,string id="Id",string name="NameID",bool rarity=false,bool portrait=false,string list="Infos"){List=list;Category=category;Config=config;Localization=local;Id=id;Name=name;Rarity=rarity;Portrait=portrait;}}
  static readonly Spec[] specs={new Spec("cards","Card","STRCard"),new Spec("characters","Character","STRCharacter",portrait:true),new Spec("monsters","Monster","STRMonster",portrait:true),new Spec("maps","Map","STRMap",name:"MapName"),new Spec("chips","Relic","STRRelic",rarity:true),new Spec("modes","GameMode","STRGameMode",id:"MapModeType"),new Spec("difficulties","ChoosingTimeLimit","STRChoosingTimeLimit",id:"GameDifficultyType",name:"DescriptionID",list:"Difficultys")};
  sealed class Source {internal Spec Spec;internal IObject Infos,Locals;internal int Count;}
  List<Source> sources;Dictionary<string,List<Entry>> output;HashSet<int> ids;int sourceIndex,rowIndex,language;float next;string activeStamp,lastStamp,lastContent;
  string Capture(out List<Source> captured,out int lang){
   lang=(int)NativeValue.Integer(NativeUI.Static("Core.GameSettings","languageType"));captured=new List<Source>();string stamp=lang+"/"+Application.version+"/"+revision;
   foreach(var spec in specs){var root=NativeUI.Static("StaticConfigure",spec.Config);var infos=NativeUI.Get(root,spec.List);var localRoot=NativeUI.Static("StaticConfigure",spec.Localization);var locals=NativeUI.Get(localRoot,"LocalDict");int n=(int)NativeUI.Long(infos,"Count"),localCount=(int)NativeUI.Long(locals,"Count");if(infos==null||locals==null||n<=0||n>20000||localCount<=0)throw new InvalidDataException("Configuration not ready: "+spec.Config);captured.Add(new Source{Spec=spec,Infos=infos,Locals=locals,Count=n});stamp+="/"+root.Pointer+"/"+infos.Pointer+":"+n+"/"+localRoot.Pointer+"/"+locals.Pointer+":"+localCount;}
   var skin=NativeUI.Static("StaticConfigure","Skin");stamp+="/skin:"+(skin==null?IntPtr.Zero:skin.Pointer);return stamp;
  }
  internal void Update(){
   float now=Time.realtimeSinceStartup;if(seenRevision!=revision){seenRevision=revision;Reset(0);}if(now<next||ProfileStatsUI.AccountId<=0)return;
   try{
    if(sources==null){string stamp=Capture(out var captured,out int lang);if(stamp==lastStamp){next=now+5;return;}sources=captured;language=lang;activeStamp=stamp;sourceIndex=rowIndex=0;ids=new HashSet<int>();output=new Dictionary<string,List<Entry>>();foreach(var spec in specs)output[spec.Category]=new List<Entry>();}
    for(int batch=0;batch<12&&sourceIndex<sources.Count;batch++){
     var source=sources[sourceIndex];var spec=source.Spec;var info=NativeUI.Call(source.Infos,"get_Item",rowIndex);int id=(int)NativeUI.Long(info,spec.Id),key=(int)NativeUI.Long(info,spec.Name);
     if(id>=0){if(!ids.Add(id))throw new InvalidDataException("Duplicate definition: "+spec.Category);string name="";if(key>0&&NativeValue.Boolean(NativeUI.Call(source.Locals,"ContainsKey",key)))name=NativeUI.Text(NativeUI.Call(NativeUI.Call(source.Locals,"get_Item",key),"GetLocal"));output[spec.Category].Add(new Entry{id=id,name_key=key,name=name,rarity=spec.Rarity?(int)NativeUI.Long(info,"RelicQualityType"):0,portrait_group=spec.Portrait?(int)NativeUI.Long(info,"StandingPainting"):0});}
     rowIndex++;if(rowIndex>=source.Count){sourceIndex++;rowIndex=0;ids.Clear();}
    }
    if(sourceIndex<sources.Count)return;
    if(Capture(out var current,out int currentLanguage)!=activeStamp||currentLanguage!=language){Reset(now+1);return;}
    foreach(var list in output.Values)list.Sort((a,b)=>a.id.CompareTo(b.id));
    string json=JsonSerializer.Serialize(new{schema=1,source="live_game_configuration",complete=true,game_version=Application.version,language=language,catalogs=output});
    if(json!=lastContent){string path=Path.Combine(Plugin.Instance.DataDir,"plugin-game-catalog.json");File.WriteAllText(path+".part",json);File.Move(path+".part",path,true);lastContent=json;Plugin.Instance.Log.LogInfo("Runtime game catalogue updated.");}
    var groups=new Dictionary<int,int>();foreach(string category in new[]{"monsters","characters"})foreach(var entry in output[category])if(entry.portrait_group>0)groups[entry.id]=entry.portrait_group;portraits=groups;Generation++;lastStamp=activeStamp;Reset(now+5);
   }catch(Exception e){RuntimeDiagnostics.Error("catalog.export",e);Plugin.Instance.Log.LogWarning("Runtime game catalogue retry: "+e.GetType().Name);Reset(now+10);}
  }
  void Reset(float time){sources=null;output=null;ids=null;sourceIndex=rowIndex=0;next=time;}
 }
}
