using System;
using System.Collections.Generic;
using UnityEngine;
using IObject=Il2CppSystem.Object;
namespace AstralPersonalMod {
 public sealed class MatchAppearance {public bool available{get;set;}public string[][] rows{get;set;}public CardPanel cards{get;set;}public long player_id{get;set;}public int hero_id{get;set;}public int standing_painting_id{get;set;}public string name{get;set;}public string hero_name{get;set;}public bool own{get;set;}}
 // Uses the game's TextureLoader/TextureManager, including its cache and reference lifetime.
 internal sealed class MatchPortraits {
  sealed class Pending {internal IObject Loader;internal string Fallback;internal float Deadline;}
  readonly List<Pending> pending=new List<Pending>();
  readonly Dictionary<string,string> addresses=new Dictionary<string,string>();
  long generation=-1;
  internal void Clear(){pending.Clear();}
  string Address(int hero,int skin,bool defaultOnly=false){
   if(generation!=RuntimeGameCatalog.Generation){addresses.Clear();generation=RuntimeGameCatalog.Generation;}
   string key=hero+":"+(defaultOnly?0:skin);if(addresses.TryGetValue(key,out var cached))return cached;
   hero=RuntimeGameCatalog.PortraitGroup(hero);
   var config=NativeUI.Static("StaticConfigure","Skin");var groups=NativeUI.Get(config,"StandingPaintings");
   string fallback="";
   int count=(int)NativeUI.Long(groups,"Count");
   for(int g=0;g<count;g++){
    var group=NativeUI.Call(groups,"get_Item",g);if(NativeUI.Long(group,"Id")!=hero)continue;
    var items=NativeUI.Get(group,"SkinStandingPaintingConfigureItems");int n=(int)NativeUI.Long(items,"Count");
    for(int i=0;i<n;i++){var item=NativeUI.Call(items,"get_Item",i);string address=NativeUI.Text(NativeUI.Get(item,"CharacterLevelUp"));if(string.IsNullOrEmpty(address))address=NativeUI.Text(NativeUI.Get(item,"Character"));if(NativeUI.Bool(item,"IsDefault"))fallback=address;if(!defaultOnly&&skin>0&&NativeUI.Long(item,"ItemID")==skin&&!string.IsNullOrEmpty(address)){addresses[key]=address;return address;}}
   }
   if(!string.IsNullOrEmpty(fallback))addresses[key]=fallback;return fallback;
  }
  internal void Add(IObject parent,MatchAppearance p,float x,float y,float w,float h){
   try{
    string fallback=Address(p.hero_id,0,true),address=Address(p.hero_id,p.standing_painting_id);
    if(string.IsNullOrEmpty(address))return;
    var loader=NativeUI.New("UI.TextureLoader");NativeUI.Call(loader,"SetXY",x,y);NativeUI.Call(loader,"SetSize",w,h);
    NativeUI.Set(loader,"touchable",false);NativeUI.Set(loader,"fill","Scale");NativeUI.Set(loader,"align","Center");NativeUI.Set(loader,"verticalAlign","Middle");NativeUI.Set(loader,"showErrorSign",false);
    NativeUI.Call(parent,"AddChild",loader);NativeUI.Set(loader,"url",address);
    if(address!=fallback&&!string.IsNullOrEmpty(fallback))pending.Add(new Pending{Loader=loader,Fallback=fallback,Deadline=Time.realtimeSinceStartup+12});
   }catch(Exception e){Plugin.Instance.Log.LogWarning("Match SD unavailable: "+e.GetType().Name);}
  }
  internal void Update(){
   for(int i=pending.Count-1;i>=0;i--){var p=pending[i];try{
    if(NativeUI.Bool(p.Loader,"isDisposed")){pending.RemoveAt(i);continue;}
    if(NativeUI.Get(NativeUI.Get(p.Loader,"image"),"texture")!=null){pending.RemoveAt(i);continue;}
    if(Time.realtimeSinceStartup>=p.Deadline){NativeUI.Set(p.Loader,"url",p.Fallback);pending.RemoveAt(i);}
   }catch{pending.RemoveAt(i);}}
  }
 }
}
