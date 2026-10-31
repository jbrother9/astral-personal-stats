using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using UnityEngine;
using IObject=Il2CppSystem.Object;
using IType=Il2CppSystem.Type;
using IFlags=Il2CppSystem.Reflection.BindingFlags;
using IArray=Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Il2CppSystem.Object>;
namespace AstralPersonalMod {
 // Native reflection is necessary: FairyGUI and the profile live in HybridCLR's runtime assembly.
 internal static class NativeUI {
  internal const IFlags Flags=IFlags.Public|IFlags.NonPublic|IFlags.Instance|IFlags.Static|IFlags.FlattenHierarchy;
  static Dictionary<string,IType> types=new Dictionary<string,IType>();
  static Dictionary<string,Il2CppSystem.Reflection.MethodInfo> methods=new Dictionary<string,Il2CppSystem.Reflection.MethodInfo>();
  internal static IType Type(string name){if(types.TryGetValue(name,out var found))return found;foreach(var assembly in Il2CppSystem.AppDomain.CurrentDomain.GetAssemblies()){var t=assembly.GetType(name,false,false);if(t!=null){types[name]=t;return t;}}return null;}
  internal static IObject Box(object v){if(v==null)return null;if(v is IObject io)return io;if(v is string s)return (Il2CppSystem.String)s;if(v is long l)return System.Runtime.CompilerServices.Unsafe.As<long,Il2CppSystem.Int64>(ref l).BoxIl2CppObject();if(v is int i)return System.Runtime.CompilerServices.Unsafe.As<int,Il2CppSystem.Int32>(ref i).BoxIl2CppObject();if(v is float f)return System.Runtime.CompilerServices.Unsafe.As<float,Il2CppSystem.Single>(ref f).BoxIl2CppObject();if(v is bool b)return System.Runtime.CompilerServices.Unsafe.As<bool,Il2CppSystem.Boolean>(ref b).BoxIl2CppObject();if(v is Color c)return c.BoxIl2CppObject();throw new ArgumentException("Unsupported UI value");}
  internal static IObject Get(IObject obj,string name){if(obj==null)return null;return Get(obj.GetIl2CppType(),obj,name);}
  internal static IObject Static(string type,string name){return Get(Type(type),null,name);}
  sealed class Accessor {internal IType Type;internal Il2CppSystem.Reflection.FieldInfo Field;internal Il2CppSystem.Reflection.PropertyInfo Property;internal IType ValueType;}
  static readonly Dictionary<(IntPtr,string),Accessor> readers=new Dictionary<(IntPtr,string),Accessor>(),writers=new Dictionary<(IntPtr,string),Accessor>();
  static readonly Dictionary<IntPtr,(IType Type,string Name)> typeNames=new Dictionary<IntPtr,(IType,string)>();
  internal static string TypeName(IObject value)=>value==null?"":TypeName(value.GetIl2CppType());
  static string TypeName(IType type){if(typeNames.TryGetValue(type.Pointer,out var cached))return cached.Name;string name=type.FullName;typeNames[type.Pointer]=(type,name);return name;}
  static Accessor Resolve(IType type,string name,bool write){
   if(type==null)return null;var cache=write?writers:readers;var key=(type.Pointer,name);if(cache.TryGetValue(key,out var found))return found;
   IType requested=type;
   for(int i=0;type!=null&&i<16;i++,type=type.BaseType){
    if(write){var property=type.GetProperty(name,Flags);if(property!=null){found=new Accessor{Type=requested,Property=property,ValueType=property.PropertyType};break;}var field=type.GetField(name,Flags);if(field!=null){found=new Accessor{Type=requested,Field=field,ValueType=field.FieldType};break;}}
    else{var field=type.GetField(name,Flags)??type.GetField("<"+name+">k__BackingField",Flags);if(field!=null){found=new Accessor{Type=requested,Field=field};break;}var property=type.GetProperty(name,Flags);if(property!=null){found=new Accessor{Type=requested,Property=property};break;}}
   }
   // Cache metadata only. Never cache failed late-loaded lookups or live field values.
   if(found!=null)cache[key]=found;return found;
  }
  static IObject Get(IType type,IObject obj,string name){var accessor=Resolve(type,name,false);return accessor==null?null:accessor.Field!=null?accessor.Field.GetValue(obj):accessor.Property.GetValue(obj,null);}
  internal static void Set(IObject obj,string name,object value){if(obj==null)throw new ArgumentNullException(name);var accessor=Resolve(obj.GetIl2CppType(),name,true);if(accessor==null)throw new MissingMemberException(name);var boxed=Convert(value,accessor.ValueType);if(accessor.Field!=null)accessor.Field.SetValue(obj,boxed);else accessor.Property.SetValue(obj,boxed,null);}
  static IObject Convert(object value,IType target){if(target.IsEnum)return Il2CppSystem.Enum.Parse(target,value.ToString());return Box(value);}
  internal static IObject Call(IObject obj,string name,params object[] args){return Invoke(obj.GetIl2CppType(),obj,name,args);}
  internal static IObject CallStatic(string type,string name,params object[] args){return Invoke(Type(type),null,name,args);}
  static IObject Invoke(IType type,IObject obj,string name,object[] args){if(type==null)throw new TypeLoadException(name);string key=TypeName(type)+"/"+name;var values=new IArray(args.Length);for(int i=0;i<args.Length;i++){values[i]=Box(args[i]);key+="/"+(values[i]==null?"null":TypeName(values[i]));}
   if(!methods.TryGetValue(key,out var method)){
    foreach(var m in type.GetMethods(Flags)){if(m.Name!=name||m.IsStatic!=(obj==null))continue;var ps=m.GetParameters();if(ps.Length!=args.Length)continue;bool match=true;for(int i=0;i<ps.Length;i++){if(values[i]!=null&&!ps[i].ParameterType.IsInstanceOfType(values[i])){match=false;break;}}if(match){method=m;break;}}
    if(method==null)throw new MissingMethodException(type.FullName+"."+name+"/"+args.Length);methods[key]=method;
   }
   return method.Invoke(obj,values);
  }
  internal static IObject New(string type){var t=Type(type);if(t==null)throw new TypeLoadException(type);return Il2CppSystem.Activator.CreateInstance(t);}
  internal static string Text(IObject v){return v==null?"":v.ToString();}
  internal static float Float(IObject obj,string name){return NativeValue.Float(Get(obj,name));}
  internal static long Long(IObject obj,string name){return NativeValue.Integer(Get(obj,name));}
  internal static bool Bool(IObject obj,string name){return NativeValue.Boolean(Get(obj,name));}
  internal static bool Same(IObject a,IObject b){return a!=null&&b!=null&&a.Pointer==b.Pointer;}
 }
 public sealed class KillerItem {public string kind{get;set;}public int hero_id{get;set;}public string hero_name{get;set;}public int count{get;set;}}
 public sealed class KillerPanel {public string filter_label{get;set;}public int analyzed{get;set;}public int total{get;set;}public int unknown{get;set;}public int deaths{get;set;}public KillerItem[] items{get;set;}}
 public sealed class CardTopItem {public int card_id{get;set;}public string name{get;set;}public int count{get;set;}public string average{get;set;}}
 public sealed class ChipItem {public string turn_label{get;set;}public int? selected_turn{get;set;}public int rarity{get;set;}public int chip_id{get;set;}public string name{get;set;}public int count{get;set;}public string average{get;set;}}
 public sealed class ChipPanel {public string key{get;set;}public string title{get;set;}public int analyzed{get;set;}public int total{get;set;}public int selections{get;set;}public int rerolls{get;set;}public ChipItem[] items{get;set;}}
 public sealed class DiceRole {public int count{get;set;}public string average{get;set;}public int[] faces{get;set;}}
 public sealed class DicePanel {public int analyzed{get;set;}public int total{get;set;}public int unknown{get;set;}public DiceRole attack{get;set;}public DiceRole defense{get;set;}public DiceRole dodge{get;set;}}
 public sealed class CardPanel {public string[][] basic_rows{get;set;}public CardTopItem[] battle_used{get;set;}public CardTopItem[] effect_used{get;set;}public DicePanel dice{get;set;}public ChipPanel chips{get;set;}public string key{get;set;}public string title{get;set;}public int analyzed{get;set;}public int total{get;set;}public int unresolved_use{get;set;}public int unresolved_purchase{get;set;}public int assisted_purchase{get;set;}public CardTopItem[] used{get;set;}public CardTopItem[] acquired{get;set;}public CardTopItem[] bought{get;set;}}
 public sealed class GameDetail {public MatchAppearance[] participants{get;set;}public CardPanel cards{get;set;}public string title{get;set;}public string[][] rows{get;set;}}
 public sealed class CollectionState {public int schema{get;set;}public long owner_id{get;set;}public long heartbeat{get;set;}public string kind{get;set;}public string label{get;set;}}
 public sealed class StatsSnapshot {public int difficulty{get;set;}=-1;public bool difficulty_enabled{get;set;}=true;public Choice[] difficulties{get;set;}
  public KillerPanel killers{get;set;}public CardPanel card_overview{get;set;}public CardPanel[] row_cards{get;set;}public int schema{get;set;}public long owner_id{get;set;}public long generated_at{get;set;}
  public int hero{get;set;}=-1;public Choice[] heroes{get;set;}public int page_size{get;set;}public int mode{get;set;}public int map{get;set;}public int tab{get;set;}public int page{get;set;}public int pages{get;set;}public int total_rows{get;set;}
  public Choice[] modes{get;set;}public Choice[] maps{get;set;}public string[] summary{get;set;}public string pve_summary{get;set;}public string pvp_summary{get;set;}public string[] columns{get;set;}public string[][] rows{get;set;}public GameDetail[] details{get;set;}public string note{get;set;}
 }
 public sealed class ProfileStatsUI {
  readonly MatchPortraits portraits=new MatchPortraits();
  public static long AccountId;
  internal static bool AnyProfileVisible;
  IObject profile,main,entry,template,window,pane,body,font,modeButton,mapButton,heroButton,difficultyButton,refreshButton,matchRefreshButton,footer,status;
  IObject[] contentTabs,matchContentTabs;IObject contextBack,matchBack,contextLabel;
  IObject[] tabs;IObject[] originals;float[] oldX,oldY,oldW,oldH;
  sealed class ClickBinding { internal IObject Button;internal Action Action; }
  readonly Dictionary<IntPtr,ClickBinding> clicks=new Dictionary<IntPtr,ClickBinding>();
  sealed class ButtonPaint {internal IObject Background,Text;internal bool Selected,Primary;}
  readonly Dictionary<IntPtr,ButtonPaint> paints=new Dictionary<IntPtr,ButtonPaint>();
  IntPtr hover;float nextHover;

  IntPtr pressed;
  IObject collectionLabel;float nextCollectionRead;long catalogGeneration;
  IObject pveSummary,pvpSummary,previousButton,nextButton;
  bool diceView;
  bool chipView;int chipPage,cardPage;
  const int CardsPerPage=5;
  long selectedPlayer;
  bool cardView;string cardKey;
  IObject shield,shieldPaint,blockedProfile;
  bool savedProfileTouchable;
  float shieldWidth,shieldHeight;
  IObject dropdown;
  DropdownModel dropdownModel;
  int dropdownKind;
  Choice[] modeChoices,mapChoices,heroChoices,difficultyChoices;


  StatsSnapshot snapshot;bool open;float nextScan,nextRead,nextRequest,nextError,nextDiagnostic;long owner;int tab=3,mode=-1,map=-1,hero=-1,difficulty=-1,page;int selected=-1;
  bool profileFound;long profilePlayerId;
  IObject lobby,cachedLobby;float lobbyWidth=-1,lobbyHeight=-1;
  readonly ProfileTreeSearch<IObject> lobbySearch=new ProfileTreeSearch<IObject>(SearchVisible,node=>NativeUI.TypeName(node)=="UI.UIHomePanel",SearchChildren,SearchChild,SearchParent,NativeUI.Same);
  IObject cachedProfile;bool entryLayoutApplied,entryStampValid,windowHidden=true;
  bool styleBold;int styleScreenWidth,styleScreenHeight,styleFontSize;long styleGeneration=-1;float styleWidth,styleHeight,styleScaleX,styleScaleY;string styleFont;
  float nextFit;int shieldOrder=int.MinValue;
  readonly UiPlacementCache placement=new UiPlacementCache();
  long snapshotFileTicks=-1,snapshotFileLength=-1;string snapshotPresentation;int snapshotSelection=int.MinValue;
  string shownCollectionLabel,shownCollectionKind;
  static bool SearchVisible(IObject node){return node!=null&&!NativeUI.Bool(node,"isDisposed")&&NativeUI.Bool(node,"onStage")&&NativeUI.Bool(node,"visible")&&NativeUI.Bool(node,"internalVisible")&&NativeUI.Bool(node,"internalVisible2");}
  static bool SearchMatch(IObject node)=>NativeUI.TypeName(node)=="UI.AccountInfoWindow";
  static int SearchChildren(IObject node){var component=NativeUI.Get(node,"asCom");return component==null?0:Math.Max(0,(int)NativeUI.Long(component,"numChildren"));}
  static IObject SearchChild(IObject node,int index)=>NativeUI.Call(node,"GetChildAt",index);
  static IObject SearchParent(IObject node)=>NativeUI.Get(node,"parent");
  readonly ProfileTreeSearch<IObject> profileSearch=new ProfileTreeSearch<IObject>(SearchVisible,SearchMatch,SearchChildren,SearchChild,SearchParent,NativeUI.Same);

  string templateUrl;Color ink=new Color(.19f,.12f,.29f,1),paper=new Color(.965f,.945f,.985f,1),accent=new Color(.46f,.29f,.85f,1),line=new Color(.67f,.57f,.80f,1);
  Color pink=new Color(.94f,.20f,.61f,1),lime=new Color(.78f,1f,.17f,1),lavender=new Color(.87f,.82f,.95f,1),rose=new Color(.98f,.82f,.90f,1);
  const float W=StatsLayout.Width,H=StatsLayout.Height;
  public void Update(){float now=Time.realtimeSinceStartup;try{
   if(owner!=AccountId){CloseStats();Restore();DisposeEntry();if(window!=null){NativeUI.Call(window,"Dispose");window=null;}profile=main=entry=template=null;clicks.Clear();paints.Clear();hover=IntPtr.Zero;pressed=IntPtr.Zero;modeChoices=mapChoices=heroChoices=difficultyChoices=null;owner=AccountId;snapshot=null;cachedProfile=null;profileSearch.Clear();lobby=cachedLobby=null;lobbySearch.Clear();lobbyWidth=lobbyHeight=-1;entryStampValid=false;snapshotPresentation=null;placement.Invalidate();mode=map=hero=difficulty=-1;page=0;tab=3;}
   if(now>=nextScan){nextScan=now+1;Scan();}if(now>=nextDiagnostic){Diagnostic(open?"open":entry!=null&&NativeUI.Bool(entry,"visible")?"button_attached":owner>0?"waiting_for_own_profile":"waiting_for_login");nextDiagnostic=now+10;}
   if(open){if(catalogGeneration!=RuntimeGameCatalog.Generation){catalogGeneration=RuntimeGameCatalog.Generation;Render();}portraits.Update();if(now>=nextCollectionRead){nextCollectionRead=now+1;ReadCollectionStatus();}if(!SearchVisible(lobby)){CloseStats();return;}if(now>=nextRequest){Request();nextRequest=now+15;}if(now>=nextRead){nextRead=now+1;ReadSnapshot();}if(now>=nextFit){nextFit=now+.1f;Fit();}}
  }catch(Exception e){RuntimeDiagnostics.Error("profile.update",e);if(now>=nextError){Plugin.Instance.Log.LogWarning("Profile statistics UI retry: "+e.GetType().Name+" / "+e.Message);Diagnostic("ui_retry");nextError=now+15;}}}
  void Diagnostic(string state){try{File.WriteAllText(Path.Combine(Plugin.Instance.DataDir,"plugin-ui-state.json"),"{\"owner_id\":"+owner+",\"heartbeat\":"+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+",\"status\":\""+state+"\",\"profile_found\":"+(profileFound?"true":"false")+",\"profile_player_id\":"+profilePlayerId+",\"window_showing\":"+(window!=null&&NativeUI.Bool(window,"isShowing")?"true":"false")+",\"window_on_stage\":"+(window!=null&&NativeUI.Bool(window,"onStage")?"true":"false")+",\"window_order\":"+NativeUI.Long(window,"sortingOrder")+",\"profile_order\":"+NativeUI.Long(profile,"sortingOrder")+",\"shield_on_stage\":"+(shield!=null&&NativeUI.Bool(shield,"onStage")?"true":"false")+",\"profile_touchable\":"+(NativeUI.Bool(profile,"touchable")?"true":"false")+"}");}catch{}}
  void Scan(){if(owner<=0||NativeUI.Type("FairyGUI.GRoot")==null)return;var root=NativeUI.Static("FairyGUI.GRoot","inst");long scanStarted=RuntimeDiagnostics.Clock(),scanAllocated=GC.GetAllocatedBytesForCurrentThread();
   var found=profileSearch.Find(root,cachedProfile,out int visited);cachedProfile=found;
   if(profileSearch.Pending)nextScan=Math.Min(nextScan,Time.realtimeSinceStartup+.05f);
   RuntimeDiagnostics.Search(visited,scanStarted,GC.GetAllocatedBytesForCurrentThread()-scanAllocated,profileSearch.UsedCache);
   profileFound=found!=null;AnyProfileVisible=found!=null;profilePlayerId=NativeUI.Long(NativeUI.Get(found,"playerCareer"),"playerId");
   if(found!=null&&profilePlayerId==owner)Plugin.ObserveOwnHistory(NativeUI.Get(found,"playerCareer"));
   var home=lobbySearch.Find(root,cachedLobby,out int homeVisited);cachedLobby=home;
   if(lobbySearch.Pending)nextScan=Math.Min(nextScan,Time.realtimeSinceStartup+.05f);
   if(home==null){if(entry!=null&&!NativeUI.Bool(entry,"isDisposed"))NativeUI.Set(entry,"visible",false);CloseStats();return;}
   if(!NativeUI.Same(lobby,home)||entry==null||NativeUI.Bool(entry,"isDisposed")){
    DisposeEntry();lobby=home;profile=null;lobbyWidth=lobbyHeight=-1;
    var nativeButton=NativeUI.Get(home,"btn_Dictionary");var nativeText=NativeUI.Call(nativeButton,"GetTextField");font=NativeUI.Get(NativeUI.Get(nativeText,"textFormat"),"font");
    entry=NewButton(root,"전적","OpenStats",0,0,80,38);NativeUI.Set(entry,"name","astral_personal_stats_lobby");
    RuntimeDiagnostics.Event("lobby_button_created");
   }
   NativeUI.Set(entry,"visible",true);LayoutLobbyEntry(root);
  }
  void LayoutLobbyEntry(IObject root){
   float rw=NativeUI.Float(root,"width"),rh=NativeUI.Float(root,"height");if(rw<=0||rh<=0||rw==lobbyWidth&&rh==lobbyHeight)return;
   float scale=Math.Min(rw/2048f,rh/1152f);float w=80*scale,h=38*scale;
   NativeUI.Call(entry,"SetScale",scale,scale);NativeUI.Call(entry,"SetXY",rw*.3125f-w/2,rh-72*scale);
   lobbyWidth=rw;lobbyHeight=rh;
  }

  void DisposeEntry(){
   if(entry!=null){var pointer=entry.Pointer;clicks.Remove(pointer);paints.Remove(pointer);if(pressed==pointer)pressed=IntPtr.Zero;if(hover==pointer)hover=IntPtr.Zero;try{if(!NativeUI.Bool(entry,"isDisposed"))NativeUI.Call(entry,"Dispose");}catch{}}
   entry=null;entryStampValid=false;
  }
  void Restore(){if(originals==null||oldX==null||!entryLayoutApplied)return;entryLayoutApplied=false;entryStampValid=false;for(int i=0;i<3;i++)try{if(!NativeUI.Bool(originals[i],"isDisposed")){NativeUI.Call(originals[i],"SetXY",oldX[i],oldY[i]);NativeUI.Call(originals[i],"SetSize",oldW[i],oldH[i]);}}catch{} }
  void RefreshEntryStyle(){
   var format=NativeUI.Get(NativeUI.Call(template,"GetTextField"),"textFormat");string currentFont=NativeUI.Text(NativeUI.Get(format,"font"));int size=(int)NativeUI.Long(format,"size");bool bold=NativeUI.Bool(format,"bold");
   float width=NativeUI.Float(template,"width"),height=NativeUI.Float(template,"height"),sx=NativeUI.Float(template,"scaleX"),sy=NativeUI.Float(template,"scaleY");
   if(entryStampValid&&entryLayoutApplied&&styleScreenWidth==Screen.width&&styleScreenHeight==Screen.height&&styleGeneration==RuntimeGameCatalog.Generation&&styleFont==currentFont&&styleFontSize==size&&styleBold==bold&&styleWidth==width&&styleHeight==height&&styleScaleX==sx&&styleScaleY==sy)return;
   entryStampValid=false;entryLayoutApplied=true;LayoutEntry();MatchEntryStyle();
   styleScreenWidth=Screen.width;styleScreenHeight=Screen.height;styleGeneration=RuntimeGameCatalog.Generation;styleFont=currentFont;styleFontSize=size;styleBold=bold;
   styleWidth=NativeUI.Float(template,"width");styleHeight=NativeUI.Float(template,"height");styleScaleX=NativeUI.Float(template,"scaleX");styleScaleY=NativeUI.Float(template,"scaleY");entryStampValid=true;RuntimeDiagnostics.Event("profile_style_refresh");
  }
  static void MatchProfilePart(IObject source,IObject target){
   NativeUI.Call(target,"SetPivot",NativeUI.Float(source,"pivotX"),NativeUI.Float(source,"pivotY"),NativeUI.Bool(source,"pivotAsAnchor"));
   NativeUI.Call(target,"SetSize",NativeUI.Float(source,"width"),NativeUI.Float(source,"height"));
   NativeUI.Call(target,"SetScale",NativeUI.Float(source,"scaleX"),NativeUI.Float(source,"scaleY"));
   NativeUI.Set(target,"rotation",NativeUI.Float(source,"rotation"));
  }
  void MatchEntryStyle(){
   // Package defaults omit per-instance child overrides from the profile component.
   // Copy geometry after layout; SetSize can otherwise move pivot-anchored children.
   MatchProfilePart(template,entry);
   int count=(int)NativeUI.Long(entry,"numChildren");
   for(int i=0;i<count;i++){
    var target=NativeUI.Call(entry,"GetChildAt",i);string name=NativeUI.Text(NativeUI.Get(target,"name"));
    if(string.IsNullOrEmpty(name))continue;var source=NativeUI.Call(template,"GetChild",name);if(source==null)continue;
    MatchProfilePart(source,target);NativeUI.Set(target,"xMin",NativeUI.Float(source,"xMin"));NativeUI.Set(target,"yMin",NativeUI.Float(source,"yMin"));
   }
   var sourceText=NativeUI.Call(template,"GetTextField");var targetText=NativeUI.Call(entry,"GetTextField");
   if(sourceText!=null&&targetText!=null){var format=NativeUI.New("FairyGUI.TextFormat");NativeUI.Call(format,"CopyFrom",NativeUI.Get(sourceText,"textFormat"));NativeUI.Set(targetText,"textFormat",format);}
   bool horizontal=Math.Abs(oldY[0]-oldY[2])<Math.Abs(oldX[0]-oldX[2]);
   // Use displayed bounds, not pivot coordinates, to line up the fourth button.
   if(horizontal){float step=NativeUI.Float(originals[2],"xMin")-NativeUI.Float(originals[1],"xMin");if(step<=0)step=NativeUI.Float(template,"width")*NativeUI.Float(template,"scaleX")+8;NativeUI.Set(entry,"xMin",NativeUI.Float(template,"xMin")+step);NativeUI.Set(entry,"yMin",NativeUI.Float(template,"yMin"));}
   else{float step=NativeUI.Float(originals[2],"yMin")-NativeUI.Float(originals[1],"yMin");if(step<=0)step=NativeUI.Float(template,"height")*NativeUI.Float(template,"scaleY")+8;NativeUI.Set(entry,"xMin",NativeUI.Float(template,"xMin"));NativeUI.Set(entry,"yMin",NativeUI.Float(template,"yMin")+step);}
  }
  void LayoutEntry(){bool horizontal=Math.Abs(oldY[0]-oldY[2])<Math.Abs(oldX[0]-oldX[2]);float min=float.MaxValue,max=float.MinValue;for(int i=0;i<3;i++){min=Math.Min(min,horizontal?oldX[i]:oldY[i]);max=Math.Max(max,horizontal?oldX[i]+oldW[i]:oldY[i]+oldH[i]);}
   float gap=8,slot=(max-min-3*gap)/4;if(slot<24)throw new InvalidOperationException("Unexpected profile button layout");var order=new List<int>{0,1,2};order.Sort((a,b)=>(horizontal?oldX[a]:oldY[a]).CompareTo(horizontal?oldX[b]:oldY[b]));
   for(int k=0;k<4;k++){IObject b=k==3?entry:originals[order[k]];float x=horizontal?min+k*(slot+gap):oldX[order[0]],y=horizontal?oldY[order[0]]:min+k*(slot+gap);NativeUI.Call(b,"SetXY",x,y);NativeUI.Call(b,"SetSize",horizontal?slot:oldW[order[0]],horizontal?oldH[order[0]]:slot);}
  }
  void Bind(IObject button,string method){
   var info=GetType().GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public);
   if(info==null)throw new MissingMethodException(method);
   // This is a CoreCLR-only Action. Never convert it into an IL2CPP delegate.
   var action=(Action)System.Delegate.CreateDelegate(typeof(Action),this,info);
   Bind(button,action);
  }
  void Bind(IObject button,Action action){clicks[button.Pointer]=new ClickBinding{Button=button,Action=action};}
  internal void PollPointer(){
   PollDropdownScroll();PollHover();
   bool down=Input.GetMouseButtonDown(0),up=Input.GetMouseButtonUp(0);if(!down&&!up)return;
   if(owner<=0||clicks.Count==0)return;
   var root=NativeUI.Static("FairyGUI.GRoot","inst");var hit=NativeUI.Get(root,"touchTarget");bool insideDropdown=Within(hit,dropdown);IntPtr target=IntPtr.Zero;if(open&&!Within(hit,window)){if(down){pressed=IntPtr.Zero;CloseDropdown();}return;}
   for(int i=0;hit!=null&&i<24;i++,hit=NativeUI.Get(hit,"parent")){
    if(clicks.TryGetValue(hit.Pointer,out var binding)&&!NativeUI.Bool(binding.Button,"isDisposed")&&NativeUI.Bool(binding.Button,"onStage")&&NativeUI.Bool(binding.Button,"visible")){target=hit.Pointer;break;}
   }
   if(down){if(dropdown!=null&&!insideDropdown&&target!=(modeButton?.Pointer??IntPtr.Zero)&&target!=(mapButton?.Pointer??IntPtr.Zero)&&target!=(heroButton?.Pointer??IntPtr.Zero)&&target!=(difficultyButton?.Pointer??IntPtr.Zero))CloseDropdown();pressed=target;}
   if(up){var previous=pressed;pressed=IntPtr.Zero;if(previous!=IntPtr.Zero&&target==previous&&clicks.TryGetValue(target,out var binding))binding.Action();}
  }
  void PruneClicks(){var dead=new List<IntPtr>();foreach(var pair in clicks)if(NativeUI.Bool(pair.Value.Button,"isDisposed"))dead.Add(pair.Key);foreach(var key in dead){clicks.Remove(key);paints.Remove(key);if(hover==key)hover=IntPtr.Zero;}}
  IObject NewProfileEntry(IObject parent,string title,string method,float x,float y,float w,float h){var b=NativeUI.CallStatic("FairyGUI.UIPackage","CreateObjectFromURL",templateUrl);NativeUI.Set(b,"title",title);NativeUI.Set(b,"selectedTitle",title);NativeUI.Set(b,"relatedController",null);NativeUI.Set(b,"mode",0);NativeUI.Set(b,"selected",false);NativeUI.Call(b,"SetXY",x,y);NativeUI.Call(b,"SetSize",w,h);if(method!=null)Bind(b,method);NativeUI.Call(parent,"AddChild",b);return b;}
  IObject NewButton(IObject parent,string title,string method,float x,float y,float w,float h){
   if(NativeUI.Same(parent,pane)&&!StatsLayout.Contains(x,y,w,h,W,H))throw new InvalidOperationException("Control outside statistics panel");
   var b=Component(parent,x,y,w,h);NativeUI.Set(b,"opaque",true);bool primary=method=="RefreshStats";
   Round(b,0,0,w,h,line,11);var bg=Round(b,1,1,w-2,h-2,Color.white,10);var text=Label(b,title,14,(h-26)/2,w-28,26,18,true);
   var format=NativeUI.Get(text,"textFormat");NativeUI.Set(format,"align",1);NativeUI.Set(text,"textFormat",format);
   paints[b.Pointer]=new ButtonPaint{Background=bg,Text=text,Primary=primary};Paint(b.Pointer,false);if(method!=null)Bind(b,method);return b;
  }
  IObject Round(IObject parent,float x,float y,float w,float h,Color fill,float radius){var graph=NativeUI.New("FairyGUI.GGraph");NativeUI.Call(graph,"SetXY",x,y);NativeUI.Call(graph,"SetSize",w,h);var radii=new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<float>(new float[]{radius,radius,radius,radius});NativeUI.Call(graph,"DrawRoundRect",w,h,fill,new IObject(radii.Pointer));NativeUI.Set(graph,"touchable",false);NativeUI.Call(parent,"AddChild",graph);return graph;}
  void TextColor(IObject text,Color color){var format=NativeUI.Get(text,"textFormat");NativeUI.Set(format,"color",color);NativeUI.Set(text,"textFormat",format);}
  void Paint(IntPtr key,bool over){if(!paints.TryGetValue(key,out var p))return;NativeUI.Set(p.Background,"color",p.Selected?lime:p.Primary?(over?pink:accent):(over?rose:Color.white));TextColor(p.Text,p.Primary&&!p.Selected?Color.white:ink);}
  void SetButtonText(IObject b,string title){if(b!=null&&paints.TryGetValue(b.Pointer,out var p))NativeUI.Set(p.Text,"text",title);}
  void SetButtonSelected(IObject b,bool selected){if(b!=null&&paints.TryGetValue(b.Pointer,out var p)){if(p.Selected==selected)return;p.Selected=selected;Paint(b.Pointer,hover==b.Pointer);}}
  void PollHover(){if(!open||Time.realtimeSinceStartup<nextHover)return;nextHover=Time.realtimeSinceStartup+.06f;var hit=NativeUI.Get(NativeUI.Static("FairyGUI.GRoot","inst"),"touchTarget");IntPtr target=IntPtr.Zero;for(int i=0;hit!=null&&i<24;i++,hit=NativeUI.Get(hit,"parent"))if(paints.ContainsKey(hit.Pointer)){target=hit.Pointer;break;}if(target==hover)return;var old=hover;hover=target;if(old!=IntPtr.Zero)Paint(old,false);if(target!=IntPtr.Zero)Paint(target,true);}
  IObject Component(IObject parent,float x,float y,float w,float h){var c=NativeUI.New("FairyGUI.GComponent");NativeUI.Call(c,"SetXY",x,y);NativeUI.Call(c,"SetSize",w,h);if(parent!=null)NativeUI.Call(parent,"AddChild",c);return c;}
  void Rect(IObject parent,float x,float y,float w,float h,Color fill){var graph=NativeUI.New("FairyGUI.GGraph");NativeUI.Call(graph,"SetXY",x,y);NativeUI.Call(graph,"SetSize",w,h);NativeUI.Call(graph,"DrawRect",w,h,1,line,fill);NativeUI.Set(graph,"touchable",false);NativeUI.Call(parent,"AddChild",graph);}
  IObject Label(IObject parent,string value,float x,float y,float w,float h,int size=19,bool bold=false){var t=NativeUI.New("FairyGUI.GTextField");var format=NativeUI.New("FairyGUI.TextFormat");if(font!=null)NativeUI.Set(format,"font",font);NativeUI.Set(format,"size",size);NativeUI.Set(format,"color",ink);NativeUI.Set(format,"bold",bold&&size>=24);NativeUI.Set(t,"textFormat",format);NativeUI.Set(t,"autoSize",3);NativeUI.Set(t,"singleLine",true);NativeUI.Set(t,"touchable",false);NativeUI.Call(t,"SetXY",x,y);NativeUI.Call(t,"SetSize",w,h);NativeUI.Set(t,"text",value??"");NativeUI.Call(parent,"AddChild",t);return t;}
  void Build(){
   window=NativeUI.New("FairyGUI.Window");pane=Component(null,0,0,W,H);NativeUI.Set(pane,"opaque",true);
   Round(pane,0,0,W,H,paper,24);Round(pane,0,0,W,9,pink,4);Round(pane,18,18,214,82,lavender,18);
   Label(pane,"내 전적",34,20,190,38,31,true);collectionLabel=Label(pane,"수집 상태 확인 중",35,58,195,18,12);shownCollectionLabel=shownCollectionKind=null;status=Label(pane,"플레이 기록 확인 중",35,79,195,18,12);
   Round(pane,255,18,345,82,rose,16);Round(pane,610,18,350,82,lavender,16);
   var a=Label(pane,"PvE · 협력전",271,27,310,23,14,true);TextColor(a,pink);pveSummary=Label(pane,"기록 확인 중",271,54,313,29,20,true);
   var b=Label(pane,"PvP · 경쟁전",626,27,310,23,14,true);TextColor(b,accent);pvpSummary=Label(pane,"기록 확인 중",626,54,318,29,20,true);
   NewButton(pane,"닫기","CloseStats",980,30,108,48);
   Round(pane,30,111,1062,54,lavender,16);string[] names={"전체","맵별","캐릭터별","경기 목록"};string[] events={"TabAll","TabMaps","TabHeroes","TabHistory"};tabs=new IObject[4];int[] scopeOrder={3,1,2,0};for(int slot=0;slot<4;slot++){int i=scopeOrder[slot];tabs[i]=NewButton(pane,names[i],events[i],34+slot*266,115,256,46);}
   modeButton=NewButton(pane,"전체 모드 ▾","NextMode",34,181,225,42);mapButton=NewButton(pane,"전체 맵 ▾","NextMap",269,181,300,42);heroButton=NewButton(pane,"전체 캐릭터 ▾","NextHero",579,181,250,42);difficultyButton=NewButton(pane,"전체 난이도 ▾","NextDifficulty",839,181,249,42);refreshButton=NewButton(pane,"새로고침","RefreshStats",966,237,122,36);matchRefreshButton=NewButton(pane,"새로고침","RefreshStats",244,237,120,36);
   string[] contentNames={"기본 통계","카드","칩","주사위"};string[] contentEvents={"ShowBasic","ShowCards","ShowChips","ShowDice"};contentTabs=new IObject[4];for(int i=0;i<4;i++)contentTabs[i]=NewButton(pane,contentNames[i],contentEvents[i],34+i*164,237,156,36);
   matchContentTabs=new IObject[4];for(int i=0;i<4;i++){int index=i;matchContentTabs[i]=NewButton(pane,i==0?"경기 통계":contentNames[index],contentEvents[index],386+i*177,237,169,36);}matchBack=NewButton(pane,"경기 목록으로","BackToList",34,237,198,36);
   contextLabel=Label(pane,"",710,244,240,24,13);contextBack=NewButton(pane,"전체 목록으로","BackToList",710,237,240,36);
   Round(pane,29,281,1064,400,lavender,16);body=Component(pane,34,287,1054,StatsLayout.TableHeight);Label(body,"통계를 불러오는 중입니다…",15,24,1000,35,22);
   footer=Label(pane,"",34,697,770,28,14);previousButton=NewButton(pane,"이전","PreviousPage",824,691,110,40);nextButton=NewButton(pane,"다음","NextPage",946,691,110,40);
   NativeUI.Set(window,"contentPane",pane);NativeUI.Set(window,"modal",true);NativeUI.Set(window,"name","astral_personal_window");
  }
  public void OpenStats(){try{if(owner<=0)return;RuntimeDiagnostics.View(true);placement.Invalidate();nextFit=0;if(window==null||NativeUI.Bool(window,"isDisposed"))Build();EnsureFrontOrder();windowHidden=false;NativeUI.Call(window,"Show");NativeUI.Call(window,"BringToFront");open=true;catalogGeneration=RuntimeGameCatalog.Generation;nextCollectionRead=0;BlockProfile();Fit();selected=-1;cardKey=null;diceView=chipView=cardView=false;Request();nextRequest=Time.realtimeSinceStartup+15;nextRead=0;Render();}catch(Exception e){RuntimeDiagnostics.Error("profile.open",e);Plugin.Instance.Log.LogWarning("Could not open statistics: "+e.GetType().Name+" / "+e.Message);Diagnostic("open_failed");CloseStats();if(window!=null){try{NativeUI.Call(window,"Dispose");}catch{}window=null;}}}
  public void CloseStats(){
   RuntimeDiagnostics.View(false);if(!open&&windowHidden&&shield==null&&blockedProfile==null&&dropdown==null)return;
   diceView=false;chipView=false;selectedPlayer=0;portraits.Clear();cardView=false;cardKey=null;
   try{CloseDropdown();}finally{ReleaseModal();open=false;selected=-1;if(window==null)windowHidden=true;else try{if(!NativeUI.Bool(window,"isDisposed"))NativeUI.Call(window,"Hide");windowHidden=true;}catch{}}
  }
  void EnsureFrontOrder(){if(window==null||lobby==null)return;long order=NativeUI.Long(lobby,"sortingOrder");int target=(int)Math.Min(int.MaxValue-1,Math.Max(0,order)+2);if(NativeUI.Long(window,"sortingOrder")!=target)NativeUI.Set(window,"sortingOrder",target);}
  void BlockProfile(){if(blockedProfile!=null||lobby==null)return;blockedProfile=lobby;savedProfileTouchable=NativeUI.Bool(lobby,"touchable");NativeUI.Set(lobby,"touchable",false);}
  void ReleaseModal(){if(shield!=null){try{NativeUI.Call(shield,"Dispose");}catch{}shield=shieldPaint=null;shieldWidth=shieldHeight=0;shieldOrder=int.MinValue;}if(blockedProfile!=null){try{if(!NativeUI.Bool(blockedProfile,"isDisposed"))NativeUI.Set(blockedProfile,"touchable",savedProfileTouchable);}catch{}blockedProfile=null;}}
  void Fit(){
   EnsureFrontOrder();var root=NativeUI.Static("FairyGUI.GRoot","inst");float rw=NativeUI.Float(root,"width"),rh=NativeUI.Float(root,"height");if(rw<=0||rh<=0)return;
   if(shield==null){shield=Component(root,0,0,rw,rh);NativeUI.Set(shield,"opaque",true);NativeUI.Set(shield,"name","astral_personal_input_shield");shieldPaint=NativeUI.New("FairyGUI.GGraph");NativeUI.Set(shieldPaint,"touchable",false);NativeUI.Call(shield,"AddChild",shieldPaint);}
   int order=(int)NativeUI.Long(window,"sortingOrder");int targetOrder=Math.Max(0,order-1);if(shieldOrder!=targetOrder){NativeUI.Set(shield,"sortingOrder",targetOrder);shieldOrder=targetOrder;}
   if(Math.Abs(rw-shieldWidth)>.1f||Math.Abs(rh-shieldHeight)>.1f){NativeUI.Call(shield,"SetSize",rw,rh);NativeUI.Call(shieldPaint,"SetSize",rw,rh);NativeUI.Call(shieldPaint,"DrawRect",rw,rh,0,new Color(0,0,0,0),new Color(.14f,.05f,.25f,.84f));shieldWidth=rw;shieldHeight=rh;}
   var bounds=StatsLayout.Fit(rw,rh);if(placement.Changed(rw,rh,bounds)){try{NativeUI.Call(window,"SetScale",bounds.Scale,bounds.Scale);NativeUI.Call(window,"SetXY",bounds.X,bounds.Y);RuntimeDiagnostics.Event("window_refit");}catch{placement.Invalidate();throw;}}
  }
  void ReadCollectionStatus(){
   if(collectionLabel==null)return;string label="수집 준비 중",kind="waiting";
   try{string path=Path.Combine(Plugin.Instance.DataDir,"plugin-collection-status.json");if(File.Exists(path)&&new FileInfo(path).Length<=8192){var state=JsonSerializer.Deserialize<CollectionState>(File.ReadAllText(path));long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();if(state!=null&&state.schema==1&&state.owner_id==owner&&now-state.heartbeat>=-5&&now-state.heartbeat<=45&&!string.IsNullOrEmpty(state.label)&&state.label.Length<=80){label=state.label;kind=state.kind;}else if(state!=null&&state.owner_id==owner)label="수집 상태 확인 중";}}
   catch{label="수집 상태 확인 중";}
   if(shownCollectionLabel!=label){NativeUI.Set(collectionLabel,"text",label);shownCollectionLabel=label;}if(shownCollectionKind!=kind){shownCollectionKind=kind;TextColor(collectionLabel,kind=="ready"?Rgb(0x236B45):kind=="attention"||kind=="retry"?Rgb(0x927019):kind=="working"?accent:ink);}
  }
  void Request(){string json="{\"owner_id\":"+owner+",\"updated_at\":"+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+",\"tab\":"+tab+",\"mode\":"+mode+",\"map\":"+map+",\"hero\":"+hero+",\"difficulty\":"+difficulty+",\"page\":"+page+"}";var target=Path.Combine(Plugin.Instance.DataDir,"plugin-ui-request.json");File.WriteAllText(target+".part",json);File.Move(target+".part",target,true);}
  void ReadSnapshot(){string path=Path.Combine(Plugin.Instance.DataDir,"plugin-ui-snapshot.json");if(!File.Exists(path))return;var info=new FileInfo(path);if(info.Length>4194304)return;if(snapshot!=null&&snapshotFileTicks==info.LastWriteTimeUtc.Ticks&&snapshotFileLength==info.Length)return;var next=JsonSerializer.Deserialize<StatsSnapshot>(File.ReadAllText(path));if(next==null||next.schema!=1||next.owner_id!=owner||next.tab!=tab||next.mode!=mode||next.map!=map||next.hero!=hero||next.difficulty!=difficulty||next.page!=page||DateTimeOffset.UtcNow.ToUnixTimeSeconds()-next.generated_at>60)return;if(snapshot!=null&&snapshot.generated_at==next.generated_at){snapshotFileTicks=info.LastWriteTimeUtc.Ticks;snapshotFileLength=info.Length;return;}string nextPresentation=PresentationKey(next);string previousPresentation=snapshot!=null?(snapshotSelection==selected&&snapshotPresentation!=null?snapshotPresentation:PresentationKey(snapshot)):null;bool unchanged=snapshot!=null&&previousPresentation==nextPresentation;snapshot=next;snapshotPresentation=nextPresentation;snapshotSelection=selected;snapshotFileTicks=info.LastWriteTimeUtc.Ticks;snapshotFileLength=info.Length;modeChoices=next.modes;mapChoices=next.maps;heroChoices=next.heroes;difficultyChoices=next.difficulties;if(!unchanged)Render();}
  string PresentationKey(StatsSnapshot value){
   // Ignore heartbeat timestamps. Keep the existing loaders alive while visible content is unchanged.
   if(selected>=0&&value.details!=null&&selected<value.details.Length)return JsonSerializer.Serialize(new {value.mode,value.map,value.hero,value.difficulty,detail=value.details[selected],value.summary,value.pve_summary,value.pvp_summary});
   long generated=value.generated_at;try{value.generated_at=0;return JsonSerializer.Serialize(value);}finally{value.generated_at=generated;}
  }
  void ClearBody(){portraits.Clear();NativeUI.Call(body,"RemoveChildren",0,-1,true);PruneClicks();}
  void Render(){if(!open||body==null)return;LayoutContentTabs();bool pages=cardView||chipView||(!cardView&&!diceView&&selected<0&&cardKey==null);NativeUI.Set(previousButton,"visible",pages);NativeUI.Set(nextButton,"visible",pages);UpdateFilterLabels();for(int i=0;i<4;i++)SetButtonSelected(tabs[i],i==tab);ClearBody();if(snapshot==null||snapshot.tab!=tab||snapshot.mode!=mode||snapshot.map!=map||snapshot.hero!=hero||snapshot.difficulty!=difficulty||snapshot.page!=page){Label(body,"통계를 불러오는 중입니다…",15,24,1000,35,22);NativeUI.Set(footer,"text","분석기가 준비되면 자동으로 표시됩니다.");return;}
   UpdateFilterLabels();NativeUI.Set(status,"text",(snapshot.summary!=null&&snapshot.summary.Length>0?snapshot.summary[0]:"플레이 기록")+" · ID "+owner);NativeUI.Set(pveSummary,"text",(snapshot.pve_summary??"PvE 기록 없음").Replace("PvE ",""));NativeUI.Set(pvpSummary,"text",(snapshot.pvp_summary??"PvP 기록 없음").Replace("PvP ",""));
   if(selected>=0&&snapshot.details!=null&&selected<snapshot.details.Length){DrawDetail(snapshot.details[selected]);return;}
   if(diceView){DrawDice(CurrentCards()?.dice,false);return;}
   if(chipView){DrawChips(CurrentCards()?.chips);return;}
   if(cardView){DrawCards(CurrentCards());return;}
   if(cardKey!=null){DrawGroupBasic(CurrentCards());return;}
   float[] widths=tab==3?new float[]{143,129,196,150,104,112,126,72}:new float[]{170,120,50,45,45,70,125,75,115,115};
   Round(body,0,0,1054,36,accent,10);float x=12;for(int c=0;c<snapshot.columns.Length&&c<widths.Length;c++){var heading=Label(body,snapshot.columns[c],x,7,widths[c]-8,28,tab==3?17:14);TextColor(heading,Color.white);x+=widths[c];}
   if(snapshot.rows.Length==0){Label(body,"아직 수집된 경기가 없습니다.",16,82,1000,36,23,true);Label(body,"설치 후 플레이한 경기가 분석되면 이곳에 표시됩니다.",16,130,1000,30,18);}
   for(int r=0;r<snapshot.rows.Length&&r<(snapshot.page_size>0?snapshot.page_size:8);r++){
    var row=Component(body,0,StatsLayout.RowTop(r),1054,StatsLayout.RowHeight);NativeUI.Set(row,"name","astral_stats_row_"+r);NativeUI.Set(row,"opaque",true);
    Round(row,0,0,1054,StatsLayout.RowHeight,r%2==0?Color.white:new Color(.94f,.90f,.98f,1),8);
    int selectedRow=r;Bind(row,()=>Select(selectedRow));
    x=12;for(int c=0;c<snapshot.rows[r].Length&&c<widths.Length;c++){var cell=Label(row,snapshot.rows[r][c],x,8,widths[c]-8,28,tab==3?17:14,c==0);if(tab==3&&c==5){string result=snapshot.rows[r][c];if(result.StartsWith("승리"))TextColor(cell,new Color(.12f,.47f,.28f,1));else if(result.StartsWith("패배"))TextColor(cell,pink);}x+=widths[c];}
   }
   if(tab!=3)DrawKillerSummary(snapshot.killers);
   NativeUI.Set(footer,"text",(snapshot.page+1)+" / "+snapshot.pages+" 페이지 · "+snapshot.total_rows+" 항목"+(tab==3?" · 경기를 누르면 상세 합계":" · 항목을 누르면 해당 그룹 상세 통계"));
  }
  MatchAppearance SelectedParticipant(GameDetail d){if(d.participants==null)return null;foreach(var p in d.participants)if(p.player_id==selectedPlayer&&selectedPlayer>0)return p;foreach(var p in d.participants)if(p.own)return p;return null;}
  void DrawDetail(GameDetail d){
   var current=SelectedParticipant(d);string who=current==null?"본인":(current.own?"본인 · ":"")+current.name+" · "+current.hero_name;
   Label(body,who,360,0,680,32,21,true);Label(body,"참가자 선택",10,8,330,25,16);
   if(d.participants!=null&&d.participants.Length>0){
    for(int i=0;i<d.participants.Length&&i<4;i++){var p=d.participants[i];float x=(i%2)*172,y=44+(i/2)*171;
     var tile=Component(body,x,y,164,164);NativeUI.Set(tile,"opaque",true);bool active=current!=null&&current.player_id==p.player_id;
     Round(tile,0,0,164,164,active?accent:lavender,9);Round(tile,3,3,158,158,active?lavender:Color.white,8);portraits.Add(tile,p,10,4,144,112);
     Label(tile,(p.own?"본인 · ":"")+p.hero_name,8,117,150,22,14);Label(tile,p.name,8,139,150,20,12);
     long player=p.player_id;Bind(tile,()=>{selectedPlayer=player;chipPage=cardPage=0;Render();});
    }
   }else Label(body,"외형 정보 없음 · 캐시 자동 보완 대기",10,70,330,100,16);
   if(current!=null&&!current.available){Label(body,"이 참가자의 경기 분석을 준비하고 있습니다.",360,100,680,40,18);return;}
   if(diceView){DrawDice(current?.cards?.dice??(current==null?d.cards?.dice:null),true);return;}
   if(cardView){DrawParticipantCards(current?.cards??(current==null?d.cards:null));return;}
   if(chipView){DrawParticipantChips(current?.cards?.chips??(current==null?d.cards?.chips:null));return;}
   var rows=current?.rows??d.rows;
   Label(body,"지표",360,33,305,24,16);Label(body,"경기 합계",690,33,125,24,16);Label(body,"선택 유저 턴당 평균",835,33,210,24,16);
   for(int i=0;rows!=null&&i<rows.Length&&i<17;i++){float y=59+i*19;Round(body,352,y,702,19,i%2==0?Color.white:rose,5);Label(body,rows[i][0],360,y+1,320,19,14);Label(body,rows[i][1],690,y+1,125,19,14);Label(body,rows[i][2],835,y+1,210,19,14);}
   NativeUI.Set(footer,"text","");
  }
  void DrawGroupBasic(CardPanel data){
   if(data==null){Label(body,"통계를 불러오는 중입니다…",16,30,1000,35,20);return;}
   Label(body,(data.title??"통계").Replace(" 카드 순위",""),16,6,1000,30,21,true);
   Label(body,"지표",24,53,440,28,16);Label(body,"합계 / 최고 기록",480,53,270,28,16);Label(body,"경기당 평균",800,53,230,28,16);
   var rows=data.basic_rows;
   for(int i=0;rows!=null&&i<rows.Length&&i<8;i++){float y=94+i*43;Round(body,0,y,1054,38,i%2==0?Color.white:rose,8);Label(body,rows[i][0],24,y+6,440,27,17);Label(body,rows[i][1],480,y+6,300,27,17);Label(body,rows[i][2],800,y+6,230,27,17);}
   NativeUI.Set(footer,"text","");
  }
  void DrawParticipantCards(CardPanel data){
   if(data==null){Label(body,"선택한 참가자의 카드 기록을 준비하고 있습니다.",360,80,680,35,18);return;}
   int pages=CardPages(data);cardPage=Math.Min(cardPage,pages-1);
   string[] titles={"배틀 카드 사용","그 외 카드 사용","구매"};CardTopItem[][] lists={data.battle_used,data.effect_used,data.bought};
   for(int col=0;col<3;col++){float x=352+238*col;Round(body,x,44,226,342,col==2?rose:lavender,10);Label(body,titles[col],x+10,55,206,28,14);
    var list=lists[col];int start=cardPage*CardsPerPage;
    if(list==null||start>=list.Length){Label(body,"이 페이지에 기록 없음",x+10,115,206,26,13);continue;}
    for(int i=0;i<CardsPerPage&&start+i<list.Length;i++){int rank=start+i;var item=list[rank];float y=90+i*60;Round(body,x+6,y,214,50,Color.white,8);var title=Label(body,(rank+1)+". "+item.name,x+13,y+4,200,22,15);NativeUI.Set(title,"tooltips",item.name);Label(body,item.count+"회",x+13,y+29,200,18,13);}
   }
   NativeUI.Set(footer,"text",(cardPage+1)+" / "+pages+" 페이지 · 선택한 참가자의 해당 경기 카드");
  }
  void DrawParticipantChips(ChipPanel data){
   if(data==null){Label(body,"선택한 참가자의 칩 기록을 준비하고 있습니다.",360,80,680,35,18);return;}
   int n=data.items?.Length??0,pages=Math.Max(1,(n+5)/6);chipPage=Math.Min(chipPage,pages-1);
   Label(body,"선택 확정 "+data.selections+"회",360,39,680,24,14);
   if(n==0)Label(body,"확인된 칩 선택 기록이 없습니다.",360,110,680,35,19);
   for(int i=0;i<6&&chipPage*6+i<n;i++){int rank=chipPage*6+i;var item=data.items[rank];float y=70+i*48;
    Round(body,352,y,702,43,rank<3?RankOutline(rank):line,8);Round(body,354,y+2,698,39,Color.white,6);Round(body,362,y+7,32,29,rank<3?RankBadge(rank):paper,6);
    Label(body,(rank+1).ToString(),368,y+12,28,23,14);var title=Label(body,item.name,406,y+9,480,27,16);TextColor(title,ChipRarityColor(item.rarity));NativeUI.Set(title,"tooltips",item.name);Label(body,(string.IsNullOrEmpty(item.turn_label)?"턴 미확인":item.turn_label),900,y+11,146,25,16);
   }
   NativeUI.Set(footer,"text",(chipPage+1)+" / "+pages+" 페이지 · 선택한 참가자의 해당 경기 칩");
  }
  int ContentIndex=>diceView?3:chipView?2:cardView?1:0;
  void SetContent(int index){CloseDropdown();if(tab==3&&selected<0&&index!=0)return;cardView=index==1;chipView=index==2;diceView=index==3;chipPage=cardPage=0;Render();}
  public void ShowDice(){SetContent(3);}
  public void ShowBasic(){SetContent(0);}
  void LayoutContentTabs(){
   bool match=tab==3&&selected>=0,history=tab==3&&!match;
   for(int i=0;i<4;i++){var b=contentTabs[i];NativeUI.Set(b,"visible",!match&&!history);SetButtonText(b,i==0&&history?"경기 목록":i==0?"기본 통계":i==1?"카드":i==2?"칩":"주사위");SetButtonSelected(b,i==ContentIndex);}
   for(int i=0;i<4;i++){NativeUI.Set(matchContentTabs[i],"visible",match);SetButtonSelected(matchContentTabs[i],i==ContentIndex);}
   NativeUI.Set(matchBack,"visible",match);bool grouped=cardKey!=null;NativeUI.Set(contextBack,"visible",!match&&grouped);
   NativeUI.Set(refreshButton,"visible",!match);NativeUI.Set(matchRefreshButton,"visible",match);NativeUI.Set(contextLabel,"visible",!match&&!grouped);NativeUI.Set(contextLabel,"text",history?"경기를 선택해 상세 보기":grouped?"선택한 항목 기준":"현재 모드·맵·캐릭터 기준");
  }
  void DrawKillerSummary(KillerPanel data){
   if(data?.items==null||data.items.Length==0)return;
   Round(body,0,267,1054,1,line,0);Label(body,"나를 많이 쓰러뜨린 적",12,274,400,25,17);
   var filter=Label(body,(data.filter_label??"")+" · 분석 "+data.analyzed+" / "+data.total+"경기",440,276,602,23,12);NativeUI.Set(filter,"tooltips",data.filter_label??"");
   for(int i=0;i<data.items.Length&&i<3;i++){var item=data.items[i];float x=i*356;
    Round(body,x,306,342,78,RankOutline(i),10);Round(body,x+2,308,338,74,Color.white,8);
    portraits.Add(body,new MatchAppearance{hero_id=item.hero_id},x+7,310,72,70);
    var name=Label(body,(i+1)+". "+item.hero_name,x+90,315,240,25,15);NativeUI.Set(name,"tooltips",item.hero_name);
    Label(body,item.count+"회",x+90,347,240,25,17);
   }
  }
  public void ShowCards(){SetContent(1);}
  public void BackFromCards(){SetContent(0);}
  CardPanel CurrentCards(){if(selected>=0&&tab==3&&snapshot.details!=null&&selected<snapshot.details.Length)return SelectedParticipant(snapshot.details[selected]) is MatchAppearance participant?participant.cards:snapshot.details[selected].cards;if(cardKey!=null&&snapshot.row_cards!=null)foreach(var group in snapshot.row_cards)if(group.key==cardKey)return group;return snapshot.card_overview;}
  int CardPages(CardPanel data){int n=Math.Max(data?.battle_used?.Length??0,Math.Max(data?.effect_used?.Length??0,data?.bought?.Length??0));return Math.Max(1,(n+CardsPerPage-1)/CardsPerPage);}
  public void PreviousCards(){if(cardPage>0){cardPage--;Render();}}
  public void NextCards(){if(cardPage+1<CardPages(CurrentCards())){cardPage++;Render();}}
  void DrawCards(CardPanel data){
   if(data==null){Label(body,"카드 통계를 준비하고 있습니다.",14,30,1000,35,23);return;}
   int pages=CardPages(data);cardPage=Math.Min(cardPage,pages-1);
   Label(body,data.title??"카드 순위",12,0,1020,32,21);
   Label(body,"카드 분석 "+data.analyzed+" / "+data.total+"경기",12,34,1025,25,14);
   CardColumn("배틀 카드 사용",data.battle_used,0,0,data);CardColumn("그 외 카드 사용",data.effect_used,356,1,data);CardColumn("구매",data.bought,712,2,data);
   NativeUI.Set(footer,"text",(cardPage+1)+" / "+pages+" 페이지"+(data.unresolved_use+data.unresolved_purchase>0?" · 분석 미완료: 사용 "+data.unresolved_use+"회 / 구매 "+data.unresolved_purchase+"회":""));
  }
  void CardColumn(string title,CardTopItem[] list,float x,int kind,CardPanel data){
   Round(body,x,66,342,320,kind==2?rose:lavender,12);Label(body,title,x+14,72,314,22,17);
   int start=cardPage*CardsPerPage;
   if(list==null||start>=list.Length){string empty=data.analyzed==0?"분석된 원본이 없습니다":start>0?"이 페이지에 기록 없음":"확인된 기록이 없습니다";Label(body,empty,x+14,133,314,28,16);return;}
   for(int i=0;i<CardsPerPage&&start+i<list.Length;i++){int rank=start+i;var item=list[rank];float y=98+i*58;Round(body,x+8,y,326,48,Color.white,8);var name=Label(body,(rank+1)+". "+item.name,x+17,y+4,308,22,16);NativeUI.Set(name,"tooltips",item.name);Label(body,item.count+"회 · 경기당 "+item.average+"회",x+17,y+28,308,18,13);}
  }
  static Color Rgb(int rgb){return new Color(((rgb>>16)&255)/255f,((rgb>>8)&255)/255f,(rgb&255)/255f,1);}
  static Color RankOutline(int rank){return Rgb(rank==0?0xBA862B:rank==1?0x8B739E:0xAC7957);}
  static Color RankBadge(int rank){return Rgb(rank==0?0xF6D478:rank==1?0xE5DAED:0xEDC7AA);}
  Color ChipRarityColor(int rarity){switch(rarity){case 1:return Rgb(0x1758A6);case 2:return Rgb(0x7040B8);case 3:return Rgb(0x927019);default:return ink;}}
  public void ShowChips(){SetContent(2);}
  public void BackFromChips(){SetContent(0);}
  public void PreviousChips(){if(chipPage>0){chipPage--;Render();}}
  public void NextChips(){var data=CurrentCards()?.chips;if(data?.items!=null&&(chipPage+1)*(selected>=0?6:10)<data.items.Length){chipPage++;Render();}}
  void DrawChips(ChipPanel data){

   if(data==null){Label(body,"칩 선택 기록을 준비하고 있습니다.",12,70,1000,35,21);return;}
   Label(body,data.title??"선택한 칩",12,0,650,32,21);
   Label(body,"분석 "+data.analyzed+" / "+data.total+"경기 · 선택 확정 "+data.selections+"회",12,38,1020,25,15);
   int n=data.items?.Length??0,pages=Math.Max(1,(n+9)/10);chipPage=Math.Min(chipPage,pages-1);
   if(n==0)Label(body,data.analyzed==0?"분석된 리플레이가 없습니다.":"확인된 칩 선택 기록이 없습니다.",16,130,1000,36,22);
   for(int i=0;i<10&&chipPage*10+i<n;i++){int rank=chipPage*10+i;var item=data.items[rank];float x=(i%2)*534,y=77+(i/2)*55;
    Round(body,x,y,520,49,rank<3?RankOutline(rank):line,9);Round(body,x+2,y+2,516,45,Color.white,7);Round(body,x+10,y+9,32,30,rank<3?RankBadge(rank):paper,6);Label(body,(rank+1).ToString(),x+16,y+14,28,23,15);var chipName=Label(body,item.name,x+54,y+3,454,24,16);TextColor(chipName,ChipRarityColor(item.rarity));Label(body,item.count+"회 선택 · 분석 경기당 "+item.average+"회",x+54,y+27,454,20,13);
   }

   NativeUI.Set(footer,"text",(chipPage+1)+" / "+pages+" 페이지");
  }
  void DrawDice(DicePanel data,bool match){
   float left=match?352:0,width=match?226:342,stride=match?238:356;
   if(data==null){Label(body,"전투 주사위 기록을 분석하고 있습니다.",left+12,80,match?680:1020,35,19);return;}
   if(!match)Label(body,(CurrentCards()?.title??"전투 주사위").Replace("카드 순위","전투 주사위"),12,0,1020,32,21);
   Label(body,"분석 "+data.analyzed+" / "+data.total+"경기 · 주사위 결과 눈 (최종 전투 수치와 별도)",left+10,match?39:36,match?680:1020,25,14);
   DiceRole[] roles={data.attack,data.defense,data.dodge};string[] titles={"공격 주사위","방어 주사위","회피 주사위"};Color[] colors={pink,accent,Rgb(0x1758A6)};
   for(int col=0;col<3;col++){float x=left+col*stride;Round(body,x,70,width,300,line,11);Round(body,x+2,72,width-4,296,Color.white,9);Label(body,titles[col],x+12,80,width-24,28,17);
    var role=roles[col];int count=role?.count??0;Label(body,count+"회 · 평균 "+(role?.average??"-")+"눈",x+12,115,width-24,26,15);
    for(int i=0;i<6;i++){int n=role?.faces!=null&&i<role.faces.Length?role.faces[i]:0;float y=158+i*31;Label(body,(i+1)+"눈",x+12,y,width-24,22,14);float barWidth=width-146;Round(body,x+50,y+5,barWidth,12,paper,5);if(n>0&&count>0)Round(body,x+50,y+5,Math.Max(2,barWidth*n/count),12,colors[col],5);Label(body,n+"회"+(count>0?" · "+(100f*n/count).ToString("0")+"%":""),x+width-87,y,80,23,12);}
   }
   NativeUI.Set(footer,"text","");
  }
  string ChoiceLabel(Choice[] choices,int id,string fallback){if(choices!=null)foreach(var c in choices)if(c.id==id)return c.label;return fallback;}
  bool DifficultyEnabled{get{if(modeChoices!=null)foreach(var choice in modeChoices)if(choice.id==mode)return !choice.is_pvp;return true;}}
  void UpdateFilterLabels(){if(modeButton!=null)SetButtonText(modeButton,ChoiceLabel(modeChoices,mode,"전체 모드")+" ▾");if(mapButton!=null)SetButtonText(mapButton,ChoiceLabel(mapChoices,map,"전체 맵")+" ▾");if(heroButton!=null)SetButtonText(heroButton,ChoiceLabel(heroChoices,hero,"전체 캐릭터")+" ▾");if(difficultyButton!=null){bool enabled=DifficultyEnabled;SetButtonText(difficultyButton,enabled?ChoiceLabel(difficultyChoices,difficulty,"전체 난이도")+" ▾":"난이도 해당 없음");NativeUI.Set(difficultyButton,"grayed",!enabled);NativeUI.Set(difficultyButton,"touchable",enabled);}}
  bool Within(IObject child,IObject ancestor){if(child==null||ancestor==null)return false;for(int i=0;child!=null&&i<24;i++,child=NativeUI.Get(child,"parent"))if(NativeUI.Same(child,ancestor))return true;return false;}
  void CloseDropdown(){if(dropdown!=null){try{NativeUI.Call(dropdown,"Dispose");}catch{}dropdown=null;PruneClicks();}dropdownKind=0;dropdownModel=null;}
  void ToggleDropdown(int kind){if(kind==4&&!DifficultyEnabled)return;if(dropdown!=null&&dropdownKind==kind){CloseDropdown();return;}CloseDropdown();var choices=kind==1?modeChoices:kind==2?mapChoices:kind==3?heroChoices:difficultyChoices;if(choices==null||choices.Length==0)return;dropdownKind=kind;dropdownModel=new DropdownModel(choices,kind==1?mode:kind==2?map:kind==3?hero:difficulty);DrawDropdown();}
  IObject ActionButton(IObject parent,string text,Action action,float x,float y,float w,float h){var b=NewButton(parent,text,null,x,y,w,h);Bind(b,action);return b;}
  void DrawDropdown(){
   if(dropdownModel==null)return;
   if(dropdown!=null){NativeUI.Call(dropdown,"Dispose");dropdown=null;PruneClicks();}
   var anchor=dropdownKind==1?modeButton:dropdownKind==2?mapButton:dropdownKind==3?heroButton:difficultyButton;float w=NativeUI.Float(anchor,"width");int count=dropdownModel.VisibleCount;float h=12+count*40+(dropdownModel.Count>DropdownModel.Limit?42:0);
   dropdown=Component(pane,NativeUI.Float(anchor,"x"),226,w,h);NativeUI.Set(dropdown,"name","astral_filter_dropdown");NativeUI.Set(dropdown,"sortingOrder",100);NativeUI.Set(dropdown,"opaque",true);Round(dropdown,0,0,w,h,lavender,13);
   for(int row=0;row<count;row++){
    int index=dropdownModel.Offset+row;var option=dropdownModel.At(index);bool chosen=option.id==dropdownModel.SelectedId;
    var b=ActionButton(dropdown,option.label,()=>SelectDropdown(index),5,6+row*40,w-10,37);SetButtonSelected(b,chosen);
   }
   if(dropdownModel.Count>DropdownModel.Limit){float y=8+count*40;ActionButton(dropdown,"▲",()=>ScrollDropdown(-1),5,y,52,32);ActionButton(dropdown,"▼",()=>ScrollDropdown(1),w-57,y,52,32);Label(dropdown,(dropdownModel.Offset+1)+"–"+(dropdownModel.Offset+count)+" / "+dropdownModel.Count,68,y+5,w-136,26,16);}
  }
  void SelectDropdown(int index){var option=dropdownModel?.At(index);if(option==null)return;if(dropdownKind==1){mode=option.id;if(option.is_pvp)difficulty=-1;}else if(dropdownKind==2)map=option.id;else if(dropdownKind==3)hero=option.id;else if(dropdownKind==4)difficulty=option.id;page=0;Change(true);}
  void ScrollDropdown(int delta){if(dropdownModel!=null&&dropdownModel.Scroll(delta))DrawDropdown();}
  void PollDropdownScroll(){if(!open||dropdown==null)return;float wheel=Input.mouseScrollDelta.y;if(Math.Abs(wheel)<.001f)return;var root=NativeUI.Static("FairyGUI.GRoot","inst");if(Within(NativeUI.Get(root,"touchTarget"),dropdown))ScrollDropdown(wheel>0?-1:1);}
  void Change(bool preserveView=false){if(!preserveView||tab==3){diceView=false;chipView=false;cardView=false;}chipPage=cardPage=0;selectedPlayer=0;CloseDropdown();selected=-1;cardKey=null;snapshot=null;Request();Render();nextRead=0;}
  void SetTab(int v){cardView=false;tab=v;page=0;Change();}
  public void TabAll(){SetTab(0);}public void TabMaps(){SetTab(1);}public void TabHeroes(){SetTab(2);}public void TabHistory(){SetTab(3);}
  public void NextDifficulty(){ToggleDropdown(4);}public void NextMode(){ToggleDropdown(1);}public void NextMap(){ToggleDropdown(2);}public void NextHero(){ToggleDropdown(3);}
  public void PreviousPage(){if(cardView){PreviousCards();return;}if(chipView){PreviousChips();return;}if(page>0){page--;Change();}}public void NextPage(){if(cardView){NextCards();return;}if(chipView){NextChips();return;}if(snapshot!=null&&page+1<snapshot.pages){page++;Change();}}
  public void RefreshStats(){RuntimeGameCatalog.Invalidate();CloseDropdown();Request();nextRead=0;}public void BackToList(){cardPage=0;diceView=false;chipView=false;selectedPlayer=0;cardView=false;cardKey=null;selected=-1;Render();}
  void Select(int r){cardPage=0;diceView=false;chipView=false;if(snapshot==null)return;if(tab==3){if(snapshot.details==null||r>=snapshot.details.Length)return;selected=r;selectedPlayer=0;cardView=false;}else{if(snapshot.row_cards==null||r>=snapshot.row_cards.Length)return;cardKey=snapshot.row_cards[r].key;selected=-1;cardView=false;}Render();}
  public void Row0(){Select(0);}public void Row1(){Select(1);}public void Row2(){Select(2);}public void Row3(){Select(3);}public void Row4(){Select(4);}public void Row5(){Select(5);}public void Row6(){Select(6);}public void Row7(){Select(7);}
 }
}

