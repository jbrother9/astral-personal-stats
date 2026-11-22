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
 public sealed class ChipOption {public int chip_id{get;set;}public string name{get;set;}public int rarity{get;set;}}
 public sealed class ChipItem {public ChipOption[][] detail_options{get;set;}public string acquisition_label{get;set;}public bool acquisition_only{get;set;}public int selection_id{get;set;}public string[][] detail_rows{get;set;}public string detail_note{get;set;}public bool connection_confirmed{get;set;}public string turn_label{get;set;}public int? selected_turn{get;set;}public int rarity{get;set;}public int chip_id{get;set;}public string name{get;set;}public int count{get;set;}public string average{get;set;}}
 public sealed class ChipPanel {public string key{get;set;}public string title{get;set;}public int analyzed{get;set;}public int total{get;set;}public int selections{get;set;}public int rerolls{get;set;}public ChipItem[] items{get;set;}}
 public sealed class DiceRole {public int count{get;set;}public string average{get;set;}public int[] faces{get;set;}}
 public sealed class DicePanel {public int analyzed{get;set;}public int total{get;set;}public int unknown{get;set;}public DiceRole attack{get;set;}public DiceRole defense{get;set;}public DiceRole dodge{get;set;}}
 public sealed class CardPanel {public string[][] basic_rows{get;set;}public CardTopItem[] battle_used{get;set;}public CardTopItem[] effect_used{get;set;}public DicePanel dice{get;set;}public ChipPanel chips{get;set;}public string key{get;set;}public string title{get;set;}public int analyzed{get;set;}public int total{get;set;}public int unresolved_use{get;set;}public int unresolved_purchase{get;set;}public int assisted_purchase{get;set;}public CardTopItem[] used{get;set;}public CardTopItem[] acquired{get;set;}public CardTopItem[] bought{get;set;}}
 public sealed class MetricDetail {public string[][] shop_details{get;set;}public int[] row_chip_selections{get;set;}public string replay_id{get;set;}public long player_id{get;set;}public int metric{get;set;}public string title{get;set;}public string turn_average{get;set;}public string note{get;set;}public int page{get;set;}public int pages{get;set;}public int total{get;set;}public string[][] rows{get;set;}}
 public sealed class ProfileCharacter {public int hero_id{get;set;}public int standing_painting_id{get;set;}public string hero_name{get;set;}public int matches{get;set;}}
 public sealed class ProfileActivity {public string name{get;set;}public int matches{get;set;}}
 public sealed class EncounterCharacter {public int hero_id{get;set;}public string name{get;set;}public int count{get;set;}}
 public sealed class OverallProfile {
  public string nickname{get;set;}
  public int matches{get;set;}
  public ProfileCharacter[] favorites{get;set;}
  public string[] win_rates{get;set;}
  public string average_stars{get;set;}
  public int star_samples{get;set;}
  public string[][] records{get;set;}
  public int[] record_positions{get;set;}
  public ProfileActivity[] favorite_maps{get;set;}
  public EncounterCharacter[] encounters{get;set;}
  public int[] pick_counts{get;set;}
 }
 public sealed class GameDetail {public string played_date{get;set;}public string map_name{get;set;}public string replay_id{get;set;}public MatchAppearance[] participants{get;set;}public CardPanel cards{get;set;}public string title{get;set;}public string[][] rows{get;set;}}
 public sealed class CollectionFailure {public string replay_id{get;set;}public string date{get;set;}public string reason{get;set;}public bool pending{get;set;}}
 public sealed class CollectionState {public CollectionFailure[] failures{get;set;}public int schema{get;set;}public long owner_id{get;set;}public long heartbeat{get;set;}public string kind{get;set;}public string label{get;set;}}
 public sealed class DataRevision {public int schema{get;set;}public long owner_id{get;set;}public long revision{get;set;}}
 public sealed class HistoryEntry {public string replay_id{get;set;}public string[] row{get;set;}public JsonElement[] sort_values{get;set;}}
 public sealed class StatsSnapshot {public bool supports_metric_prefetch{get;set;}public MetricDetail metric_next_page{get;set;}public bool detail_only{get;set;}public GameDetail detail_game{get;set;}public MetricDetail[] metric_first_pages{get;set;}public HistoryEntry[] history_entries{get;set;}public MetricDetail metric_detail{get;set;}public long request_id{get;set;}public int difficulty{get;set;}=-1;public bool difficulty_enabled{get;set;}=true;public Choice[] difficulties{get;set;}
  public OverallProfile profile{get;set;}public KillerPanel killers{get;set;}public CardPanel card_overview{get;set;}public CardPanel[] row_cards{get;set;}public int schema{get;set;}public long owner_id{get;set;}public long generated_at{get;set;}
  public int hero{get;set;}=-1;public Choice[] heroes{get;set;}public int page_size{get;set;}public int mode{get;set;}public int map{get;set;}public int tab{get;set;}public int page{get;set;}public int pages{get;set;}public int total_rows{get;set;}
  public Choice[] modes{get;set;}public Choice[] maps{get;set;}public string[] summary{get;set;}public string pve_summary{get;set;}public string pvp_summary{get;set;}public string[] columns{get;set;}public string[][] rows{get;set;}public GameDetail[] details{get;set;}public string note{get;set;}
  public StatsSnapshot WithDetail(StatsSnapshot response,int selected){
   var copy=(StatsSnapshot)MemberwiseClone();
   copy.request_id=response.request_id;copy.generated_at=response.generated_at;
   copy.metric_first_pages=response.metric_first_pages;copy.metric_detail=response.metric_detail;
   copy.metric_next_page=response.metric_next_page;copy.supports_metric_prefetch=response.supports_metric_prefetch;
   copy.details=details!=null?(GameDetail[])details.Clone():new GameDetail[rows.Length];
   if(details==null)for(int i=0;i<copy.details.Length;i++){
    int index=page*8+i;
    copy.details[i]=new GameDetail{replay_id=history_entries!=null&&index<history_entries.Length?history_entries[index].replay_id:""};
   }
   copy.details[selected]=response.detail_game;
   return copy;
  }
 }
 public sealed class ProfileStatsUI {
  readonly MatchPortraits portraits=new MatchPortraits();
  public static long AccountId;
  internal static bool AnyProfileVisible;
  IObject profile,main,entry,template,window,pane,body,font,modeButton,mapButton,heroButton,difficultyButton,refreshButton,footer,status;
  IObject[] contentTabs,matchContentTabs;IObject contentTabGroup,matchTabGroup,contextBack,matchBack;
  IObject[] tabs;IObject statisticsTab;string navigationFont,metricFont;int lastStatisticsTab=4;IObject[] originals;float[] oldX,oldY,oldW,oldH;
  sealed class ClickBinding { internal IObject Button;internal Action Action; }
  readonly Dictionary<IntPtr,ClickBinding> clicks=new Dictionary<IntPtr,ClickBinding>();
  sealed class ButtonPaint {internal IObject Background,Text,Border,Underline,Check,Arrow;internal IObject[] MenuBars;internal bool Selected,Primary,Navigation,Filter,Dropdown,Pagination,Segmented,MetricRow,Destructive,Enabled=true;}
  readonly Dictionary<IntPtr,ButtonPaint> paints=new Dictionary<IntPtr,ButtonPaint>();
  IntPtr hover;float nextHover;

  IntPtr pressed;
  IObject failurePane;CollectionFailure[] collectionFailures=new CollectionFailure[0];string failureStamp;int failurePage;
  IObject collectionLabel;float nextCollectionRead;long catalogGeneration;
  IObject pveSummary,pvpSummary,previousButton,nextButton,pageCounter;
  bool diceView;
  bool chipView;int chipPage,cardPage,chipSelectionId,chipDetailPage;
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


  int metricIndex=-1,metricPage;StatsSnapshot snapshot;bool open;float nextRead,nextError,nextDiagnostic;long owner;int tab=3,mode=-1,map=-1,hero=-1,difficulty=-1,page;int selected=-1;
  bool profileFound;long profilePlayerId;
  IObject lobby,cachedLobby;float lobbyWidth=-1,lobbyHeight=-1;
  IObject cachedProfile;bool entryLayoutApplied,entryStampValid,windowHidden=true;
  bool styleBold;int styleScreenWidth,styleScreenHeight,styleFontSize;long styleGeneration=-1;float styleWidth,styleHeight,styleScaleX,styleScaleY;string styleFont;
  readonly UiPlacementCache capturePlacement=new UiPlacementCache();
  IObject cameraButton,captureToast,captureToastText;byte[] captureImage;int captureFrame=-1;float captureDeadline,nextClipboardTry,toastUntil;
  string pendingReplayCopy;float replayCopyDeadline,nextReplayCopyTry;
  IObject replayDownloadAwaiter;string replayDownloading;long replayDownloadOwner;float nextReplayDownloadRead;
  float nextFit;int shieldOrder=int.MinValue;
  readonly UiPlacementCache placement=new UiPlacementCache();
  long snapshotFileTicks=-1,snapshotFileLength=-1;string snapshotPresentation;int snapshotSelection=int.MinValue;
  string shownCollectionLabel,shownCollectionKind;
  static bool SearchVisible(IObject node){return node!=null&&!NativeUI.Bool(node,"isDisposed")&&NativeUI.Bool(node,"onStage")&&NativeUI.Bool(node,"visible")&&NativeUI.Bool(node,"internalVisible")&&NativeUI.Bool(node,"internalVisible2");}
  static bool SearchMatch(IObject node)=>NativeUI.TypeName(node)=="UI.AccountInfoWindow";
  static int SearchChildren(IObject node){var component=NativeUI.Get(node,"asCom");return component==null?0:Math.Max(0,(int)NativeUI.Long(component,"numChildren"));}
  static IObject SearchChild(IObject node,int index)=>NativeUI.Call(node,"GetChildAt",index);
  static IObject SearchParent(IObject node)=>NativeUI.Get(node,"parent");


  string templateUrl;Color ink=new Color(.19f,.12f,.29f,1),paper=new Color(.965f,.945f,.985f,1),accent=new Color(.46f,.29f,.85f,1),line=new Color(.67f,.57f,.80f,1);
  readonly Color sectionBackground=Rgb(0xF7EFF2);
  Color pink=new Color(.94f,.20f,.61f,1),lime=new Color(.78f,1f,.17f,1),lavender=new Color(.87f,.82f,.95f,1),rose=new Color(.98f,.82f,.90f,1);
  const float W=StatsLayout.Width,H=StatsLayout.Height;
  public void Update(){float now=Time.realtimeSinceStartup;try{
   if(owner!=AccountId){CloseStats();DisposeCaptureChrome();Restore();DisposeEntry();if(window!=null){NativeUI.Call(window,"Dispose");window=null;}profile=main=entry=template=null;clicks.Clear();paints.Clear();hover=IntPtr.Zero;pressed=IntPtr.Zero;modeChoices=mapChoices=heroChoices=difficultyChoices=null;owner=AccountId;snapshot=null;cachedProfile=null;lobbyWidth=lobbyHeight=-1;entryStampValid=false;snapshotPresentation=null;placement.Invalidate();mode=map=hero=difficulty=-1;page=0;tab=3;}
   UpdateReplayDownload(now);UpdateEntry();if(now>=nextDiagnostic){Diagnostic(open?"open":entry!=null&&NativeUI.Bool(entry,"visible")?"button_attached":owner>0?"waiting_for_bottom_menu":"waiting_for_login");nextDiagnostic=now+10;}
   if(open){if(catalogGeneration!=RuntimeGameCatalog.Generation){catalogGeneration=RuntimeGameCatalog.Generation;metricCache.Clear();Request();Render();}portraits.Update();if(now>=nextCollectionRead){nextCollectionRead=now+1;ReadCollectionStatus();}if(!SearchVisible(lobby)){CloseStats();return;}if(now>=nextRevisionCheck){nextRevisionCheck=now+.5f;CheckRevision();}if(refreshDue>0&&now>=refreshDue){refreshDue=0;Request();}if(awaitingSnapshot&&now>=nextRead){nextRead=now+.25f;if(now<responseDeadline)ReadSnapshot();else awaitingSnapshot=false;}if(now>=nextFit){nextFit=now+.1f;Fit();}UpdateCapture(now);}
  }catch(Exception e){RuntimeDiagnostics.Error("profile.update",e);if(now>=nextError){Plugin.Instance.Log.LogWarning("Profile statistics UI retry: "+e.GetType().Name+" / "+e.Message);Diagnostic("ui_retry");nextError=now+15;}}}
  void Diagnostic(string state){try{File.WriteAllText(Path.Combine(Plugin.Instance.DataDir,"plugin-ui-state.json"),"{\"owner_id\":"+owner+",\"heartbeat\":"+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+",\"status\":\""+state+"\",\"profile_found\":"+(profileFound?"true":"false")+",\"profile_player_id\":"+profilePlayerId+",\"window_showing\":"+(window!=null&&NativeUI.Bool(window,"isShowing")?"true":"false")+",\"window_on_stage\":"+(window!=null&&NativeUI.Bool(window,"onStage")?"true":"false")+",\"window_order\":"+NativeUI.Long(window,"sortingOrder")+",\"profile_order\":"+NativeUI.Long(profile,"sortingOrder")+",\"shield_on_stage\":"+(shield!=null&&NativeUI.Bool(shield,"onStage")?"true":"false")+",\"profile_touchable\":"+(NativeUI.Bool(profile,"touchable")?"true":"false")+"}");}catch{}}
  IObject uiManager;bool profileObserved;
  bool IsHomeScreen(){var panel=NativeUI.Get(uiManager,"currentPanel");return panel!=null&&NativeUI.TypeName(panel)=="UI.HomePanel";}
  void UpdateEntry(){
   if(uiManager==null&&NativeUI.Type("UI.UIManager")!=null)uiManager=NativeUI.Static("UI.UIManager","inst");
   if(uiManager!=null){cachedLobby=NativeUI.Get(NativeUI.Get(uiManager,"bottomMenuPanel"),"ui");cachedProfile=NativeUI.Get(uiManager,"_cachedAccountInfoWindow");}
   AnyProfileVisible=cachedProfile!=null&&!NativeUI.Bool(cachedProfile,"isDisposed")&&NativeUI.Bool(cachedProfile,"isShowing");
   if(!AnyProfileVisible)profileObserved=false;
   else if(!profileObserved&&owner>0){var career=NativeUI.Get(cachedProfile,"playerCareer");if(NativeUI.Long(career,"playerId")==owner){Plugin.ObserveOwnHistory(career);profileObserved=true;}}
   var home=cachedLobby;if(home==null||NativeUI.Bool(home,"isDisposed")){if(home!=null)cachedLobby=null;if(entry!=null)DisposeEntry();CloseStats();return;}
   if(!IsHomeScreen()||!SearchVisible(home)||owner<=0){if(entry!=null&&!NativeUI.Bool(entry,"isDisposed")&&NativeUI.Bool(entry,"visible"))NativeUI.Set(entry,"visible",false);CloseStats();return;}
   if(!NativeUI.Same(lobby,home)||entry==null||NativeUI.Bool(entry,"isDisposed")){
    DisposeEntry();lobby=home;profile=null;lobbyWidth=lobbyHeight=-1;
    var functions=NativeUI.Get(home,"list_Function");if(functions!=null&&NativeUI.Long(functions,"numChildren")>0){var nativeText=NativeUI.Call(NativeUI.Call(functions,"GetChildAt",0),"GetTextField");font=NativeUI.Get(NativeUI.Get(nativeText,"textFormat"),"font");}
    entry=NewLobbyEntry(home);NativeUI.Set(entry,"name","astral_personal_stats_lobby");RuntimeDiagnostics.Event("lobby_button_created");
   }
   if(!NativeUI.Bool(entry,"visible"))NativeUI.Set(entry,"visible",true);LayoutLobbyEntry(home);
  }
  IObject NewLobbyEntry(IObject parent){
   var button=Component(parent,0,0,72,86);NativeUI.Set(button,"opaque",true);NativeUI.Set(button,"tooltips","내 전적");
   var text=Label(button,"전적",0,0,72,30,24);TextColor(text,Color.white);
   var format=NativeUI.Get(text,"textFormat");NativeUI.Set(format,"align",1);NativeUI.Set(text,"textFormat",format);
   var bars=new IObject[3];for(int i=0;i<3;i++){float height=18+i*12;bars[i]=Round(button,18+i*13,77-height,9,height,Color.white,3);}
   paints[button.Pointer]=new ButtonPaint{Text=text,MenuBars=bars};Bind(button,"OpenStats");return button;
  }
  void LayoutLobbyEntry(IObject bottom){
   var tokens=NativeUI.Get(bottom,"list_Token");if(tokens==null)return;
   float x=NativeUI.Float(tokens,"x"),y=NativeUI.Float(tokens,"y"),h=NativeUI.Float(tokens,"height");
   float scale=Math.Max(.5f,Math.Min(1.5f,h/64f));
   float px=x-84*scale,py=y+(h-86*scale)/2;
   if(px==lobbyWidth&&py==lobbyHeight)return;
   NativeUI.Call(entry,"SetScale",scale,scale);NativeUI.Call(entry,"SetXY",px,py);NativeUI.Set(entry,"sortingOrder",1);
   lobbyWidth=px;lobbyHeight=py;RuntimeDiagnostics.Event("lobby_button_positioned");
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
   var root=NativeUI.Static("FairyGUI.GRoot","inst");var hit=NativeUI.Get(root,"touchTarget");bool insideDropdown=Within(hit,dropdown);IntPtr target=IntPtr.Zero;if(open&&!Within(hit,window)&&!Within(hit,cameraButton)){if(down){pressed=IntPtr.Zero;CloseDropdown();}return;}
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
  void ApplyMetricFont(IObject text,bool bold=false){
   if(metricFont==null)return;
   var format=NativeUI.Get(text,"textFormat");
   NativeUI.Set(format,"font",metricFont);NativeUI.Set(format,"bold",bold);
   NativeUI.Set(text,"textFormat",format);
  }
  void Paint(IntPtr key,bool over){
   if(!paints.TryGetValue(key,out var p))return;
   if(p.MenuBars!=null){var color=over?lime:Color.white;TextColor(p.Text,color);foreach(var bar in p.MenuBars)NativeUI.Set(bar,"color",color);return;}
   if(p.Navigation){
    NativeUI.Set(p.Underline,"visible",p.Selected);
    TextColor(p.Text,p.Selected||over?accent:ink);
    return;
   }
   if(p.Filter){
    NativeUI.Set(p.Border,"color",p.Selected||over?accent:line);
    NativeUI.Set(p.Background,"color",Color.white);
    TextColor(p.Text,p.Selected?accent:ink);
    return;
   }
   if(p.Dropdown){
    NativeUI.Set(p.Background,"color",p.Selected?lavender:over?sectionBackground:Color.white);
    TextColor(p.Text,p.Selected?accent:ink);
    NativeUI.Set(p.Check,"visible",p.Selected);
    return;
   }
   if(p.Pagination){
    NativeUI.Set(p.Background,"visible",over&&p.Enabled);
    TextColor(p.Text,p.Enabled?(p.Destructive?Rgb(0xC74454):ink):line);
    return;
   }
   if(p.Segmented){
    NativeUI.Set(p.Background,"visible",p.Selected||over);
    NativeUI.Set(p.Background,"color",p.Selected?Color.white:paper);
    TextColor(p.Text,p.Selected?ink:Rgb(0x716A7A));
    return;
   }
   if(p.MetricRow){
    NativeUI.Set(p.Background,"color",over?sectionBackground:Color.white);
    TextColor(p.Arrow,over?ink:Rgb(0xB2AAB9));
    return;
   }
   NativeUI.Set(p.Background,"color",p.Selected?lime:p.Primary?(over?pink:accent):(over?rose:Color.white));
   TextColor(p.Text,p.Primary&&!p.Selected?Color.white:ink);
  }
  IObject NavigationTab(string title,string method,float x,float y,float width,float height,int size){
   var button=Component(pane,x,y,width,height);
   NativeUI.Set(button,"opaque",true);
   var text=Label(button,title,8,1,width-16,height-6,size);
   if(navigationFont!=null){
    var format=NativeUI.Get(text,"textFormat");
    NativeUI.Set(format,"font",navigationFont);
    NativeUI.Set(text,"textFormat",format);
   }
   NativeUI.Set(text,"autoSize",0);
   NativeUI.Set(text,"verticalAlign","Middle");
   var underline=Round(button,8,height-3,width-16,3,accent,1);
   paints[button.Pointer]=new ButtonPaint{Text=text,Underline=underline,Navigation=true};
   Paint(button.Pointer,false);Bind(button,method);
   return button;
  }
  IObject FilterField(string label,string title,string method,float x,float width){
   Label(pane,label,x,180,width,17,12);
   var button=Component(pane,x,199,width,29);
   NativeUI.Set(button,"opaque",true);
   var border=Round(button,0,0,width,29,line,4);
   var background=Round(button,1,1,width-2,27,Color.white,3);
   var text=Label(button,title,9,2,width-30,25,15);
   Label(button,"▾",width-21,2,16,25,15);
   paints[button.Pointer]=new ButtonPaint{Border=border,Background=background,Text=text,Filter=true};
   Bind(button,method);return button;
  }
  IObject SegmentedTab(IObject group,string title,string method,float x,float width){
   var button=Component(group,x,4,width,28);
   NativeUI.Set(button,"opaque",true);
   var background=Round(button,0,0,width,28,Color.white,6);
   var text=Label(button,title,7,1,width-14,26,16);
   var format=NativeUI.Get(text,"textFormat");NativeUI.Set(format,"align",1);NativeUI.Set(text,"textFormat",format);
   paints[button.Pointer]=new ButtonPaint{Background=background,Text=text,Segmented=true};
   Paint(button.Pointer,false);Bind(button,method);
   return button;
  }
  void LayoutNavigation(){
   bool summary=tab==0||recordMatch;
   bool statistics=tab!=3&&!summary;
   SetButtonSelected(tabs[3],tab==3&&!recordMatch);
   SetButtonSelected(tabs[0],summary);
   SetButtonSelected(statisticsTab,statistics);
   foreach(int i in new[]{4,1,2}){
    NativeUI.Set(tabs[i],"visible",statistics);
    SetButtonSelected(tabs[i],i==(recordMatch?0:tab));
   }
  }
  void SetButtonText(IObject b,string title){if(b!=null&&paints.TryGetValue(b.Pointer,out var p))NativeUI.Set(p.Text,"text",title);}
  void SetButtonSelected(IObject b,bool selected){if(b!=null&&paints.TryGetValue(b.Pointer,out var p)){if(p.Selected==selected)return;p.Selected=selected;Paint(b.Pointer,hover==b.Pointer);}}
  void PollHover(){if((!open&&entry==null)||Time.realtimeSinceStartup<nextHover)return;nextHover=Time.realtimeSinceStartup+.06f;var hit=NativeUI.Get(NativeUI.Static("FairyGUI.GRoot","inst"),"touchTarget");IntPtr target=IntPtr.Zero;for(int i=0;hit!=null&&i<24;i++,hit=NativeUI.Get(hit,"parent"))if(paints.ContainsKey(hit.Pointer)){target=hit.Pointer;break;}if(target==hover)return;var old=hover;hover=target;if(old!=IntPtr.Zero)Paint(old,false);if(target!=IntPtr.Zero)Paint(target,true);}
  IObject Component(IObject parent,float x,float y,float w,float h){var c=NativeUI.New("FairyGUI.GComponent");NativeUI.Call(c,"SetXY",x,y);NativeUI.Call(c,"SetSize",w,h);if(parent!=null)NativeUI.Call(parent,"AddChild",c);return c;}
  void Rect(IObject parent,float x,float y,float w,float h,Color fill){var graph=NativeUI.New("FairyGUI.GGraph");NativeUI.Call(graph,"SetXY",x,y);NativeUI.Call(graph,"SetSize",w,h);NativeUI.Call(graph,"DrawRect",w,h,1,line,fill);NativeUI.Set(graph,"touchable",false);NativeUI.Call(parent,"AddChild",graph);}
  IObject Label(IObject parent,string value,float x,float y,float w,float h,int size=19,bool bold=false){var t=NativeUI.New("FairyGUI.GTextField");var format=NativeUI.New("FairyGUI.TextFormat");if(font!=null)NativeUI.Set(format,"font",font);NativeUI.Set(format,"size",size);NativeUI.Set(format,"color",ink);NativeUI.Set(format,"bold",bold&&size>=24);NativeUI.Set(t,"textFormat",format);NativeUI.Set(t,"autoSize",3);NativeUI.Set(t,"singleLine",true);NativeUI.Set(t,"touchable",false);NativeUI.Call(t,"SetXY",x,y);NativeUI.Call(t,"SetSize",w,h);NativeUI.Set(t,"text",value??"");NativeUI.Call(parent,"AddChild",t);return t;}
  void Build(){DisposeCaptureChrome();
   navigationFont=null;
   try{
    foreach(string installed in UnityEngine.Font.GetOSInstalledFontNames()){
     if(string.Equals(installed,"Malgun Gothic",StringComparison.OrdinalIgnoreCase)){
      navigationFont=installed;break;
     }
    }
   }catch(Exception error){RuntimeDiagnostics.Error("stats.navigation_font",error);}
   metricFont=BundledUIFont.Load()??navigationFont;
   window=NativeUI.New("FairyGUI.Window");pane=Component(null,0,0,W,H);NativeUI.Set(pane,"opaque",true);
   Round(pane,0,0,W,H,paper,24);Round(pane,0,0,W,9,pink,4);Round(pane,18,18,214,82,lavender,18);
   Label(pane,"내 전적",34,20,190,38,31,true);var collectionButton=QuietAction(pane,"수집 상태 확인 중",()=>{if(collectionFailures.Length>0){failurePage=0;DrawFailures();}},32,56,200,22,12,"수집 실패 목록");collectionLabel=paints[collectionButton.Pointer].Text;NativeUI.Call(collectionLabel,"SetXY",3f,2f);NativeUI.Call(collectionLabel,"SetSize",195f,18f);var collectionFormat=NativeUI.Get(collectionLabel,"textFormat");NativeUI.Set(collectionFormat,"align",0);NativeUI.Set(collectionLabel,"textFormat",collectionFormat);shownCollectionLabel=shownCollectionKind=null;status=Label(pane,"플레이 기록 확인 중",35,79,195,18,12);
   Round(pane,255,18,345,82,rose,16);Round(pane,610,18,350,82,lavender,16);
   var a=Label(pane,"PvE · 협력전",271,27,310,23,14,true);TextColor(a,pink);pveSummary=Label(pane,"기록 확인 중",271,54,313,29,20,true);
   var b=Label(pane,"PvP · 경쟁전",626,27,310,23,14,true);TextColor(b,accent);pvpSummary=Label(pane,"기록 확인 중",626,54,318,29,20,true);
   NewButton(pane,"닫기","CloseStats",980,30,108,48);
   Round(pane,34,143,1054,1,line,0);
   tabs=new IObject[5];
   tabs[3]=NavigationTab("경기목록","TabHistory",34,108,170,35,22);
   tabs[0]=NavigationTab("요약","TabAll",224,108,122,35,22);
   statisticsTab=NavigationTab("통계","TabStatistics",366,108,122,35,22);
   string[] scopeNames={"전체 통계","맵별","캐릭터별"};
   string[] scopeEvents={"TabOverall","TabMaps","TabHeroes"};
   int[] scopeOrder={4,1,2};
   for(int i=0;i<3;i++)tabs[scopeOrder[i]]=NavigationTab(scopeNames[i],scopeEvents[i],34+i*154,148,144,28,17);
   modeButton=FilterField("모드","전체 모드","NextMode",34,225);
   mapButton=FilterField("맵","전체 맵","NextMap",269,300);
   heroButton=FilterField("캐릭터","전체 캐릭터","NextHero",579,250);
   difficultyButton=FilterField("난이도","전체 난이도","NextDifficulty",839,249);
   refreshButton=QuietAction(pane,"↻",RefreshStats,1052,107,36,36,24,"통계 새로고침");
   if(navigationFont!=null){
    var refreshFormat=NativeUI.Get(paints[refreshButton.Pointer].Text,"textFormat");
    NativeUI.Set(refreshFormat,"font",navigationFont);
    NativeUI.Set(paints[refreshButton.Pointer].Text,"textFormat",refreshFormat);
   }
   contentTabGroup=Component(pane,34,237,648,36);Round(contentTabGroup,0,0,648,36,sectionBackground,9);
   matchTabGroup=Component(pane,386,237,702,36);Round(matchTabGroup,0,0,702,36,sectionBackground,9);
   string[] contentNames={"기본 통계","카드","칩","주사위"};string[] contentEvents={"ShowBasic","ShowCards","ShowChips","ShowDice"};
   contentTabs=new IObject[4];matchContentTabs=new IObject[4];
   for(int i=0;i<4;i++){
    contentTabs[i]=SegmentedTab(contentTabGroup,contentNames[i],contentEvents[i],4+i*160,160);
    matchContentTabs[i]=SegmentedTab(matchTabGroup,i==0?"경기통계":contentNames[i],contentEvents[i],4+i*174,172);
   }
   matchBack=QuietAction(pane,"‹ 경기목록으로",BackToList,34,237,198,36);
   var backText=paints[matchBack.Pointer].Text;
   NativeUI.Call(backText,"SetXY",10f,5f);NativeUI.Call(backText,"SetSize",178f,26f);
   var backFormat=NativeUI.Get(backText,"textFormat");
   NativeUI.Set(backFormat,"size",16);NativeUI.Set(backFormat,"align",0);
   NativeUI.Set(backText,"textFormat",backFormat);
   contextBack=NewButton(pane,"전체 목록으로","BackToList",710,237,240,36);
   Round(pane,29,281,1064,400,lavender,16);body=Component(pane,34,287,1054,StatsLayout.TableHeight);Label(body,"통계를 불러오는 중입니다…",15,24,1000,35,22);
   footer=Label(pane,"",34,697,770,28,14);
   previousButton=PageArrow("‹","PreviousPage",842);
   pageCounter=Label(pane,"",884,698,148,28,16);
   var counterFormat=NativeUI.Get(pageCounter,"textFormat");NativeUI.Set(counterFormat,"align",1);NativeUI.Set(pageCounter,"textFormat",counterFormat);
   nextButton=PageArrow("›","NextPage",1038);
   NativeUI.Set(window,"contentPane",pane);NativeUI.Set(window,"modal",true);NativeUI.Set(window,"name","astral_personal_window");
  }
  public void OpenStats(){try{CloseFailures();collectionFailures=new CollectionFailure[0];failureStamp=null;metricCache.Clear();metricIndex=-1;metricPage=0;chipSelectionId=0;chipDetailPage=0;if(owner<=0||!IsHomeScreen())return;RuntimeDiagnostics.View(true);placement.Invalidate();nextFit=0;if(window==null||NativeUI.Bool(window,"isDisposed"))Build();EnsureFrontOrder();windowHidden=false;NativeUI.Call(window,"Show");NativeUI.Call(window,"BringToFront");open=true;catalogGeneration=RuntimeGameCatalog.Generation;nextCollectionRead=0;BlockProfile();Fit();selected=-1;cardKey=null;diceView=chipView=cardView=false;revisionTicks=-1;revisionLength=-1;revisionValue=0;CheckRevision(true);Request();nextRead=0;Render();}catch(Exception e){RuntimeDiagnostics.Error("profile.open",e);Plugin.Instance.Log.LogWarning("Could not open statistics: "+e.GetType().Name+" / "+e.Message);Diagnostic("open_failed");CloseStats();if(window!=null){try{NativeUI.Call(window,"Dispose");}catch{}window=null;}}}
  public void CloseStats(){
   pendingReplayCopy=null;
   captureImage=null;captureFrame=-1;toastUntil=0;if(cameraButton!=null&&!NativeUI.Bool(cameraButton,"isDisposed"))NativeUI.Set(cameraButton,"visible",false);if(captureToast!=null&&!NativeUI.Bool(captureToast,"isDisposed"))NativeUI.Set(captureToast,"visible",false);
   awaitingSnapshot=false;refreshDue=0;if(open)WriteClosedRequest();RuntimeDiagnostics.View(false);if(!open&&windowHidden&&shield==null&&blockedProfile==null&&dropdown==null)return;
   diceView=false;chipView=false;selectedPlayer=0;portraits.Clear();cardView=false;cardKey=null;
   try{CloseDropdown();}finally{ReleaseModal();open=false;selected=-1;if(window==null)windowHidden=true;else try{if(!NativeUI.Bool(window,"isDisposed"))NativeUI.Call(window,"Hide");windowHidden=true;}catch{}}
  }
  void EnsureFrontOrder(){if(window==null||lobby==null)return;long order=0;var ancestor=lobby;for(int depth=0;ancestor!=null&&depth<32;depth++,ancestor=NativeUI.Get(ancestor,"parent"))order=Math.Max(order,NativeUI.Long(ancestor,"sortingOrder"));int target=(int)Math.Min(int.MaxValue-3,Math.Max(0,order)+2);if(NativeUI.Long(window,"sortingOrder")!=target)NativeUI.Set(window,"sortingOrder",target);}
  void BlockProfile(){if(blockedProfile!=null||lobby==null)return;blockedProfile=lobby;savedProfileTouchable=NativeUI.Bool(lobby,"touchable");NativeUI.Set(lobby,"touchable",false);}
  void ReleaseModal(){if(shield!=null){try{NativeUI.Call(shield,"Dispose");}catch{}shield=shieldPaint=null;shieldWidth=shieldHeight=0;shieldOrder=int.MinValue;}if(blockedProfile!=null){try{if(!NativeUI.Bool(blockedProfile,"isDisposed"))NativeUI.Set(blockedProfile,"touchable",savedProfileTouchable);}catch{}blockedProfile=null;}}
  void Fit(){
   EnsureFrontOrder();var root=NativeUI.Static("FairyGUI.GRoot","inst");float rw=NativeUI.Float(root,"width"),rh=NativeUI.Float(root,"height");if(rw<=0||rh<=0)return;
   if(shield==null){shield=Component(root,0,0,rw,rh);NativeUI.Set(shield,"opaque",true);NativeUI.Set(shield,"name","astral_personal_input_shield");shieldPaint=NativeUI.New("FairyGUI.GGraph");NativeUI.Set(shieldPaint,"touchable",false);NativeUI.Call(shield,"AddChild",shieldPaint);}
   int order=(int)NativeUI.Long(window,"sortingOrder");int targetOrder=Math.Max(0,order-1);if(shieldOrder!=targetOrder){NativeUI.Set(shield,"sortingOrder",targetOrder);shieldOrder=targetOrder;}
   if(Math.Abs(rw-shieldWidth)>.1f||Math.Abs(rh-shieldHeight)>.1f){NativeUI.Call(shield,"SetSize",rw,rh);NativeUI.Call(shieldPaint,"SetSize",rw,rh);NativeUI.Call(shieldPaint,"DrawRect",rw,rh,0,new Color(0,0,0,0),new Color(.14f,.05f,.25f,.84f));shieldWidth=rw;shieldHeight=rh;}
   var bounds=StatsLayout.Fit(rw,rh);FitCaptureChrome(root,bounds,rw);if(placement.Changed(rw,rh,bounds)){try{NativeUI.Call(window,"SetScale",bounds.Scale,bounds.Scale);NativeUI.Call(window,"SetXY",bounds.X,bounds.Y);RuntimeDiagnostics.Event("window_refit");}catch{placement.Invalidate();throw;}}
  }
  void DisposeCaptureChrome(){
   pendingReplayCopy=null;
   captureImage=null;captureFrame=-1;toastUntil=0;capturePlacement.Invalidate();
   if(cameraButton!=null){var pointer=cameraButton.Pointer;clicks.Remove(pointer);paints.Remove(pointer);if(hover==pointer)hover=IntPtr.Zero;if(pressed==pointer)pressed=IntPtr.Zero;try{if(!NativeUI.Bool(cameraButton,"isDisposed"))NativeUI.Call(cameraButton,"Dispose");}catch{}}
   if(captureToast!=null)try{if(!NativeUI.Bool(captureToast,"isDisposed"))NativeUI.Call(captureToast,"Dispose");}catch{}
   cameraButton=captureToast=captureToastText=null;
  }
  void FitCaptureChrome(IObject root,ModalBounds bounds,float rootWidth){
   if(cameraButton==null||NativeUI.Bool(cameraButton,"isDisposed")){
    capturePlacement.Invalidate();cameraButton=NewButton(root,"","CaptureStats",0,0,40,40);NativeUI.Set(cameraButton,"name","astral_stats_capture");NativeUI.Set(cameraButton,"tooltips","전적창 캡처");
    Round(cameraButton,8,14,24,17,ink,4);Round(cameraButton,13,9,11,7,ink,2);Round(cameraButton,15,17,10,10,Color.white,5);
   }
   if(captureToast==null||NativeUI.Bool(captureToast,"isDisposed")){
    capturePlacement.Invalidate();captureToast=Component(root,0,0,360,40);NativeUI.Set(captureToast,"touchable",false);Round(captureToast,0,0,360,40,lavender,12);captureToastText=Label(captureToast,"",12,8,336,26,17);var format=NativeUI.Get(captureToastText,"textFormat");NativeUI.Set(format,"align",1);NativeUI.Set(captureToastText,"textFormat",format);
   }
   int order=(int)NativeUI.Long(window,"sortingOrder");if(NativeUI.Long(cameraButton,"sortingOrder")!=order+1)NativeUI.Set(cameraButton,"sortingOrder",order+1);if(NativeUI.Long(captureToast,"sortingOrder")!=order+2)NativeUI.Set(captureToast,"sortingOrder",order+2);
   if(capturePlacement.Changed(rootWidth,NativeUI.Float(root,"height"),bounds)){
   NativeUI.Call(cameraButton,"SetScale",bounds.Scale,bounds.Scale);NativeUI.Call(cameraButton,"SetXY",Math.Max(4,bounds.X-40*bounds.Scale),Math.Max(4,bounds.Y+8*bounds.Scale));
   NativeUI.Call(captureToast,"SetScale",bounds.Scale,bounds.Scale);NativeUI.Call(captureToast,"SetXY",Math.Max(4,Math.Min(rootWidth-360*bounds.Scale-4,bounds.X+(bounds.Width-360*bounds.Scale)/2)),Math.Max(4,bounds.Y-48*bounds.Scale));
   }
   bool cameraVisible=captureFrame<0,toastVisible=toastUntil>Time.realtimeSinceStartup&&captureFrame<0;if(NativeUI.Bool(cameraButton,"visible")!=cameraVisible)NativeUI.Set(cameraButton,"visible",cameraVisible);if(NativeUI.Bool(captureToast,"visible")!=toastVisible)NativeUI.Set(captureToast,"visible",toastVisible);
  }
  public void CaptureStats(){
   pendingReplayCopy=null;
   if(!open||captureFrame>=0)return;CloseDropdown();captureImage=null;toastUntil=0;captureFrame=Time.frameCount+2;captureDeadline=Time.realtimeSinceStartup+1.5f;nextClipboardTry=0;
   if(cameraButton!=null)NativeUI.Set(cameraButton,"visible",false);if(captureToast!=null)NativeUI.Set(captureToast,"visible",false);
  }
  void CaptureNotice(string message){captureImage=null;captureFrame=-1;NativeUI.Set(captureToastText,"text",message);toastUntil=Time.realtimeSinceStartup+1.8f;if(cameraButton!=null)NativeUI.Set(cameraButton,"visible",true);NativeUI.Set(captureToast,"visible",true);}
  void UpdateCapture(float now){
   if(toastUntil>0&&now>=toastUntil){toastUntil=0;if(captureToast!=null)NativeUI.Set(captureToast,"visible",false);}
   if(pendingReplayCopy!=null&&now>=nextReplayCopyTry){
    try{
     if(ScreenshotClipboard.TryCopyText(pendingReplayCopy)){pendingReplayCopy=null;CaptureNotice("리플레이 ID가 복사되었습니다");}
     else if(now>=replayCopyDeadline){pendingReplayCopy=null;CaptureNotice("클립보드를 사용할 수 없습니다");}
     else nextReplayCopyTry=now+.1f;
    }catch(Exception error){pendingReplayCopy=null;RuntimeDiagnostics.Error("stats.copy_replay_id",error);CaptureNotice("리플레이 ID를 복사하지 못했습니다");}
   }
   if(captureFrame<0||Time.frameCount<captureFrame||now<nextClipboardTry)return;
   try{
    if(captureImage==null){var root=NativeUI.Static("FairyGUI.GRoot","inst");captureImage=ScreenshotClipboard.Capture(NativeUI.Float(root,"width"),NativeUI.Float(root,"height"),NativeUI.Float(window,"x"),NativeUI.Float(window,"y"),W*NativeUI.Float(window,"scaleX"),H*NativeUI.Float(window,"scaleY"));}
    if(ScreenshotClipboard.TryCopy(captureImage)){RuntimeDiagnostics.Event("stats_copied_to_clipboard");CaptureNotice("클립보드에 캡처되었습니다");}
    else if(now>=captureDeadline)CaptureNotice("클립보드를 사용할 수 없습니다");else nextClipboardTry=now+.1f;
   }catch(Exception error){RuntimeDiagnostics.Error("stats.capture",error);CaptureNotice("전적창을 캡처하지 못했습니다");}
  }
  void DownloadReplay(string replayId){
   if(!open||owner<=0||replayDownloadAwaiter!=null||string.IsNullOrEmpty(replayId)||replayId.Length<10||replayId.Length>24)return;
   foreach(char digit in replayId)if(digit<'0'||digit>'9')return;
   try{
    var logic=NativeUI.Get(NativeUI.Static("GameLogic.GameLogicManager","inst"),"replay");
    if(logic==null)throw new InvalidOperationException("Replay logic unavailable");
    if(NativeValue.Boolean(NativeUI.Call(logic,"IsReplayCached",replayId))){CaptureNotice("이미 다운로드된 리플레이입니다");return;}
    var task=NativeUI.Call(logic,"TryLoadReplayFile",replayId,true);
    var awaiter=NativeUI.Call(task,"GetAwaiter");if(awaiter==null)throw new InvalidOperationException("Replay task unavailable");
    replayDownloadAwaiter=awaiter;replayDownloading=replayId;replayDownloadOwner=owner;nextReplayDownloadRead=0;
    RuntimeDiagnostics.Event("replay_download_requested");Render();
   }catch(Exception error){RuntimeDiagnostics.Error("stats.replay_download",error);CaptureNotice("리플레이 다운로드를 시작하지 못했습니다");}
  }
  void UpdateReplayDownload(float now){
   if(replayDownloadAwaiter==null||now<nextReplayDownloadRead)return;nextReplayDownloadRead=now+.2f;
   try{
    if(!NativeUI.Bool(replayDownloadAwaiter,"IsCompleted"))return;
    var awaiter=replayDownloadAwaiter;string replayId=replayDownloading;replayDownloadAwaiter=null;replayDownloading=null;
    var result=NativeUI.Call(awaiter,"GetResult");var status=NativeUI.Get(result,"Item2");if(status==null)throw new InvalidOperationException("Replay result unavailable");int failure=(int)NativeValue.Integer(status);
    if(failure==0){var logic=NativeUI.Get(NativeUI.Static("GameLogic.GameLogicManager","inst"),"replay");if(!NativeValue.Boolean(NativeUI.Call(logic,"IsReplayCached",replayId)))throw new InvalidOperationException("Replay file unavailable");}
    if(open&&owner==replayDownloadOwner){Render();CaptureNotice(failure==0?"리플레이 다운로드가 완료되었습니다":failure==3?"게임의 리플레이 저장 개수가 가득 찼습니다":"리플레이를 다운로드하지 못했습니다");}
    RuntimeDiagnostics.Event(failure==0?"replay_download_complete":"replay_download_failed");
   }catch(Exception error){replayDownloadAwaiter=null;replayDownloading=null;RuntimeDiagnostics.Error("stats.replay_download",error);if(open&&owner==replayDownloadOwner){Render();CaptureNotice("리플레이를 다운로드하지 못했습니다");}}
  }
  IObject loadingProgress;
  void ReadCollectionStatus(){
   if(collectionLabel==null)return;string label="수집 준비 중",kind="waiting";
   try{string path=Path.Combine(Plugin.Instance.DataDir,"plugin-collection-status.json");if(File.Exists(path)&&new FileInfo(path).Length<=2097152){var state=JsonSerializer.Deserialize<CollectionState>(File.ReadAllText(path));long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();if(state!=null&&state.schema==1&&state.owner_id==owner&&now-state.heartbeat>=-5&&now-state.heartbeat<=45&&!string.IsNullOrEmpty(state.label)&&state.label.Length<=80){label=state.label;kind=state.kind;collectionFailures=state.failures??new CollectionFailure[0];string stamp=JsonSerializer.Serialize(collectionFailures);if(stamp!=failureStamp){failureStamp=stamp;if(failurePane!=null)DrawFailures();}}else if(state!=null&&state.owner_id==owner)label="수집 상태 확인 중";}}
   catch{label="수집 상태 확인 중";}
   if(loadingProgress!=null)NativeUI.Set(loadingProgress,"text",kind=="ready"?"화면 통계 응답을 기다리는 중":label);
   string displayLabel=label+(collectionFailures.Length>0&&kind=="attention"?"  ›":"");
   if(shownCollectionLabel!=displayLabel){NativeUI.Set(collectionLabel,"text",displayLabel);shownCollectionLabel=displayLabel;}if(shownCollectionKind!=kind){shownCollectionKind=kind;TextColor(collectionLabel,kind=="ready"?Rgb(0x236B45):kind=="attention"||kind=="retry"?Rgb(0x927019):kind=="working"?accent:ink);}
  }
  void CloseFailures(){if(failurePane!=null){NativeUI.Call(failurePane,"Dispose");failurePane=null;PruneClicks();if(open)UpdatePagination();}}
  void DrawFailures(){
   CloseFailures();failurePane=Component(pane,34,237,1054,440);NativeUI.Set(failurePane,"opaque",true);Round(failurePane,0,0,1054,440,paper,12);UpdatePagination();
   QuietAction(failurePane,"‹ 돌아가기",CloseFailures,8,4,132,32,16);
   Label(failurePane,"수집 실패 목록",160,7,650,30,20,true);
   int pages=Math.Max(1,(collectionFailures.Length+4)/5);failurePage=Math.Max(0,Math.Min(failurePage,pages-1));
   QuietAction(failurePane,"‹",()=>{if(failurePage>0){failurePage--;DrawFailures();}},864,4,36,32,24);
   Label(failurePane,(failurePage+1)+" / "+pages,906,8,80,26,14);
   QuietAction(failurePane,"›",()=>{if(failurePage+1<pages){failurePage++;DrawFailures();}},998,4,36,32,24);
   if(collectionFailures.Length==0)Label(failurePane,"확인이 필요한 경기가 없습니다.",20,76,950,32,18);
   for(int n=0;n<5&&failurePage*5+n<collectionFailures.Length;n++){
    var item=collectionFailures[failurePage*5+n];float y=52+n*75;
    Round(failurePane,8,y,1038,68,Color.white,8);
    Label(failurePane,(item.date??"")+" · 리플레이 ID "+item.replay_id,22,y+6,830,26,15,true);
    Label(failurePane,item.pending?"다시 수집하는 중입니다…":item.reason,22,y+34,830,24,14);
    var retry=QuietAction(failurePane,item.pending?"처리 중":"다시 시도",()=>RetryFailure(item),868,y+17,112,34,16);
    NativeUI.Set(retry,"touchable",!item.pending);
    var exclude=QuietAction(failurePane,"×",()=>RetryFailure(item,true),990,y+17,40,34,24,"수집 대상에서 제외");
    paints[exclude.Pointer].Destructive=true;Paint(exclude.Pointer,false);NativeUI.Set(exclude,"touchable",!item.pending);
   }
  }
  void RetryFailure(CollectionFailure item,bool exclude=false){
   if(item.pending)return;
   try{string path=Path.Combine(Plugin.Instance.DataDir,"plugin-retry-request.json");if(File.Exists(path)){CaptureNotice("이전 재시도 요청을 처리하고 있습니다");return;}
    var value=new {schema=1,owner_id=owner,replay_id=item.replay_id,action=exclude?"exclude":"retry",requested_at=DateTimeOffset.UtcNow.ToUnixTimeSeconds()};string temp=path+".part";File.WriteAllText(temp,JsonSerializer.Serialize(value));File.Move(temp,path);Plugin.Instance.StartWorker();CaptureNotice(exclude?"수집 제외 요청을 보냈습니다":"다시 시도 요청을 보냈습니다");
   }catch(Exception error){RuntimeDiagnostics.Error("stats.retry",error);CaptureNotice("재시도를 요청하지 못했습니다");}
  }
  long requestId,revisionTicks=-1,revisionLength=-1,revisionValue;bool awaitingSnapshot;float responseDeadline,nextRevisionCheck,refreshDue;
  void CheckRevision(bool baseline=false){try{
   var path=Path.Combine(Plugin.Instance.DataDir,"plugin-data-revision.json");var info=new FileInfo(path);if(!info.Exists||info.Length>4096||info.LastWriteTimeUtc.Ticks==revisionTicks&&info.Length==revisionLength)return;
   var data=JsonSerializer.Deserialize<DataRevision>(File.ReadAllText(path));if(data==null||data.schema!=1||data.owner_id!=owner)return;
   revisionTicks=info.LastWriteTimeUtc.Ticks;revisionLength=info.Length;if(data.revision!=revisionValue){revisionValue=data.revision;metricCache.Clear();if(!baseline)refreshDue=Time.realtimeSinceStartup+.35f;}
  }catch(IOException){}catch(JsonException){}}
  void WriteClosedRequest(){try{WriteUiRequest(false);}catch(IOException){}}
  void WriteUiRequest(bool visible,bool detailOnly=false){
   string detailReplay=selected>=0&&snapshot?.details!=null&&selected<snapshot.details.Length?snapshot.details[selected].replay_id:"";long detailPlayer=selectedPlayer>0?selectedPlayer:owner;
   if(selected>=0&&string.IsNullOrEmpty(detailReplay)&&snapshot?.history_entries!=null){
    int index=page*8+selected;
    if(index<snapshot.history_entries.Length)detailReplay=snapshot.history_entries[index].replay_id;
   }
   string json=JsonSerializer.Serialize(new {detail_only=detailOnly,sort_column=sortColumn,sort_descending=sortDescending,detail_replay=detailReplay,detail_player=detailPlayer,detail_metric=metricIndex,detail_page=metricPage,owner_id=owner,updated_at=DateTimeOffset.UtcNow.ToUnixTimeSeconds(),request_id=requestId,visible,tab,mode,map,hero,difficulty,page});
   var target=Path.Combine(Plugin.Instance.DataDir,"plugin-ui-request.json");File.WriteAllText(target+".part",json);File.Move(target+".part",target,true);
  }
  void Request(bool detailOnly=false){requestId=Math.Max(requestId+1,DateTime.UtcNow.Ticks);WriteUiRequest(true,detailOnly);awaitingSnapshot=true;responseDeadline=Time.realtimeSinceStartup+30;nextRead=0;refreshDue=0;}
  void ReadSnapshot(){string path=Path.Combine(Plugin.Instance.DataDir,"plugin-ui-snapshot.json");if(!File.Exists(path))return;var info=new FileInfo(path);if(info.Length>4194304)return;if(snapshot!=null&&snapshotFileTicks==info.LastWriteTimeUtc.Ticks&&snapshotFileLength==info.Length)return;var next=JsonSerializer.Deserialize<StatsSnapshot>(File.ReadAllText(path));if(next==null||next.schema!=1||next.request_id!=requestId||next.owner_id!=owner||next.tab!=tab||next.mode!=mode||next.map!=map||next.hero!=hero||next.difficulty!=difficulty||next.page!=page||DateTimeOffset.UtcNow.ToUnixTimeSeconds()-next.generated_at>60)return;if(next.detail_only){next=MergeDetailResponse(next);if(next==null){awaitingSnapshot=false;return;}}awaitingSnapshot=false;CacheMetricDetails(next);if(metricIndex>=0&&next.metric_detail!=null&&next.metric_detail.metric==metricIndex&&next.metric_detail.player_id==(selectedPlayer>0?selectedPlayer:owner))metricPage=next.metric_detail.page;if(snapshot!=null&&snapshot.request_id==next.request_id){snapshotFileTicks=info.LastWriteTimeUtc.Ticks;snapshotFileLength=info.Length;return;}string nextPresentation=PresentationKey(next);string previousPresentation=snapshot!=null?(snapshotSelection==selected&&snapshotPresentation!=null?snapshotPresentation:PresentationKey(snapshot)):null;bool unchanged=snapshot!=null&&previousPresentation==nextPresentation;snapshot=next;snapshotPresentation=nextPresentation;snapshotSelection=selected;snapshotFileTicks=info.LastWriteTimeUtc.Ticks;snapshotFileLength=info.Length;modeChoices=next.modes;mapChoices=next.maps;heroChoices=next.heroes;difficultyChoices=next.difficulties;if(!unchanged)Render();if(selected>=0&&next.metric_first_pages==null)EnsureMetricPrefetch();else PrefetchNextMetricPage();}
  StatsSnapshot MergeDetailResponse(StatsSnapshot response){
   if(snapshot==null||selected<0||selected>=snapshot.rows.Length||response.detail_game==null)return null;
   string expected=snapshot.details!=null&&selected<snapshot.details.Length?snapshot.details[selected].replay_id:"";
   if(string.IsNullOrEmpty(expected)&&snapshot.history_entries!=null){
    int index=page*8+selected;
    if(index<snapshot.history_entries.Length)expected=snapshot.history_entries[index].replay_id;
   }
   if(response.detail_game.replay_id!=expected)return null;
   return snapshot.WithDetail(response,selected);
  }
  string PresentationKey(StatsSnapshot value){
   // Ignore heartbeat timestamps. Keep the existing loaders alive while visible content is unchanged.
   if(selected>=0&&value.details!=null&&selected<value.details.Length)return JsonSerializer.Serialize(new {value.mode,value.map,value.hero,value.difficulty,detail=value.details[selected],value.metric_detail,value.summary,value.pve_summary,value.pvp_summary});
   long generated=value.generated_at;try{value.generated_at=0;return JsonSerializer.Serialize(value);}finally{value.generated_at=generated;}
  }
  void ClearBody(){loadingProgress=null;portraits.Clear();NativeUI.Call(body,"RemoveChildren",0,-1,true);PruneClicks();}
  IObject PageArrow(string title,string method,float x){
   return QuietAction(pane,title,()=>{if(method=="PreviousPage")PreviousPage();else NextPage();},x,691,36,40,28,
    method=="PreviousPage"?"이전 페이지":"다음 페이지");
  }
  IObject QuietAction(IObject parent,string title,Action action,float x,float y,float width,float height,int size=15,string tooltip=null){
   var button=Component(parent,x,y,width,height);
   NativeUI.Set(button,"opaque",true);
   var background=Round(button,0,0,width,height,sectionBackground,5);
   var text=Label(button,title,3,1,width-6,height-2,size);
   var format=NativeUI.Get(text,"textFormat");NativeUI.Set(format,"align",1);NativeUI.Set(text,"textFormat",format);
   NativeUI.Set(button,"tooltips",tooltip??title);
   paints[button.Pointer]=new ButtonPaint{Background=background,Text=text,Pagination=true};
   Paint(button.Pointer,false);Bind(button,action);
   return button;
  }
  void DetailPager(int current,int pages,Action previous,Action next){
   var previousArrow=QuietAction(body,"‹",previous,862,37,30,32,25,"이전 페이지");
   var counter=Label(body,(current+1)+" / "+pages,898,41,112,26,15);
   var format=NativeUI.Get(counter,"textFormat");NativeUI.Set(format,"align",1);NativeUI.Set(counter,"textFormat",format);
   var nextArrow=QuietAction(body,"›",next,1018,37,30,32,25,"다음 페이지");
   SetPageArrowEnabled(previousArrow,current>0);
   SetPageArrowEnabled(nextArrow,current+1<pages);
  }
  void SetPageArrowEnabled(IObject button,bool enabled){
   NativeUI.Set(button,"touchable",enabled);
   paints[button.Pointer].Enabled=enabled;
   Paint(button.Pointer,hover==button.Pointer);
  }
  void UpdatePagination(){
   NativeUI.Set(footer,"visible",failurePane==null);
   bool visible=failurePane==null&&snapshot!=null&&snapshot.tab==tab&&snapshot.page==page&&
    snapshot.mode==mode&&snapshot.map==map&&snapshot.hero==hero&&snapshot.difficulty==difficulty&&
    chipSelectionId==0&&(cardView||chipView||(tab!=0&&!diceView&&selected<0&&cardKey==null));
   NativeUI.Set(previousButton,"visible",visible);
   NativeUI.Set(nextButton,"visible",visible);
   NativeUI.Set(pageCounter,"visible",visible);
   if(!visible)return;
   int current=page,pages=snapshot.pages;
   if(cardView){current=cardPage;pages=CardPages(CurrentCards());}
   else if(chipView){
    current=chipPage;int size=selected>=0?6:10;
    pages=Math.Max(1,((CurrentCards()?.chips?.items?.Length??0)+size-1)/size);
   }
   NativeUI.Set(pageCounter,"text",(current+1)+" / "+pages);
   SetPageArrowEnabled(previousButton,current>0);
   SetPageArrowEnabled(nextButton,current+1<pages);
   string note=NativeUI.Text(NativeUI.Get(footer,"text"));
   string prefix=(current+1)+" / "+pages+" 페이지";
   if(note.StartsWith(prefix,StringComparison.Ordinal))
    NativeUI.Set(footer,"text",note.Substring(prefix.Length).TrimStart(' ','·'));
  }
  void Render(){
   if(!open||body==null)return;
   RenderContent();UpdatePagination();
  }
  void RenderContent(){if(!open||body==null)return;LayoutContentTabs();bool pages=chipSelectionId==0&&(cardView||chipView||(tab!=0&&!diceView&&selected<0&&cardKey==null));NativeUI.Set(previousButton,"visible",pages);NativeUI.Set(nextButton,"visible",pages);UpdateFilterLabels();LayoutNavigation();ClearBody();if(snapshot==null||snapshot.tab!=tab||snapshot.mode!=mode||snapshot.map!=map||snapshot.hero!=hero||snapshot.difficulty!=difficulty||snapshot.page!=page){Label(body,"통계를 불러오는 중입니다…",15,24,1000,35,22);loadingProgress=Label(body,"",15,66,1000,28,17);ReadCollectionStatus();NativeUI.Set(footer,"text","");return;}
   UpdateFilterLabels();NativeUI.Set(status,"text",(snapshot.summary!=null&&snapshot.summary.Length>0?snapshot.summary[0]:"플레이 기록")+" · ID "+owner);NativeUI.Set(pveSummary,"text",(snapshot.pve_summary??"PvE 기록 없음").Replace("PvE ",""));NativeUI.Set(pvpSummary,"text",(snapshot.pvp_summary??"PvP 기록 없음").Replace("PvP ",""));
   if(selected>=0&&snapshot.details!=null&&selected<snapshot.details.Length&&snapshot.details[selected].rows!=null){DrawDetail(snapshot.details[selected]);return;}
   if(tab==3&&selected>=0){
    Label(body,"경기 상세를 불러오는 중입니다…",15,24,1000,35,22);
    loadingProgress=Label(body,"",15,66,1000,28,17);ReadCollectionStatus();return;
   }
   if(diceView){DrawDice(CurrentCards()?.dice,false);return;}
   if(chipView){DrawChips(CurrentCards()?.chips);return;}
   if(cardView){DrawCards(CurrentCards());return;}
   if(tab==0){DrawProfile(snapshot.profile);return;}
   if(cardKey!=null){DrawGroupBasic(CurrentCards());return;}
   float[] widths=tab==3?new float[]{132,120,170,132,96,100,112,100,68}:new float[]{170,120,50,45,45,70,125,75,115,115};
   Round(body,0,0,1054,36,accent,10);
   float x=12;
   for(int c=0;c<snapshot.columns.Length&&c<widths.Length;c++){
    string title=snapshot.columns[c]+(tab==3&&sortActive&&c==sortColumn?(sortDescending?" ▼":" ▲"):"");
    var heading=Label(body,title,x,7,widths[c]-8,28,tab==3?16:14);
    TextColor(heading,Color.white);
    if(tab==3){
     var target=Component(body,x-6,0,widths[c],36);
     NativeUI.Set(target,"opaque",true);
     int column=c;Bind(target,()=>SortHistory(column));
    }
    x+=widths[c];
   }
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
   Label(body,who,360,0,680,32,21,true);
   var replayLabel=Label(body,"리플레이 ID  "+d.replay_id,8,9,200,24,12);NativeUI.Set(replayLabel,"tooltips",d.replay_id);
   string matchInfo=(d.played_date??"-")+" · "+(d.map_name??"맵 미확인");
   var matchInfoLabel=Label(body,matchInfo,8,34,336,20,12);
   NativeUI.Set(matchInfoLabel,"tooltips",matchInfo+" (한국 시간)");
   QuietAction(body,"복사",()=>{if(!open||captureFrame>=0||string.IsNullOrEmpty(d.replay_id))return;pendingReplayCopy=d.replay_id;replayCopyDeadline=Time.realtimeSinceStartup+1.5f;nextReplayCopyTry=0;},212,8,54,26,12);
   var download=QuietAction(body,replayDownloading==d.replay_id?"받는 중":"다운로드",()=>DownloadReplay(d.replay_id),272,8,72,26,12);SetPageArrowEnabled(download,replayDownloadAwaiter==null);
   if(d.participants!=null&&d.participants.Length>0){
    for(int i=0;i<d.participants.Length&&i<4;i++){var p=d.participants[i];float x=(i%2)*172,y=58+(i/2)*162;
     var tile=Component(body,x,y,164,156);NativeUI.Set(tile,"opaque",true);bool active=current!=null&&current.player_id==p.player_id;
     Round(tile,0,0,164,156,active?accent:lavender,9);Round(tile,3,3,158,150,active?lavender:Color.white,8);portraits.Add(tile,p,10,4,144,104);
     if(p.final_stars.HasValue&&p.final_stars.Value>0){int stars=Math.Min(p.final_stars.Value,5);var starLabel=Label(tile,new string('★',stars),158-stars*13,5,stars*13,19,13);TextColor(starLabel,new Color(.82f,.57f,.12f,1));NativeUI.Set(starLabel,"tooltips","최종 스타 "+p.final_stars.Value);}
     Label(tile,(p.own?"본인 · ":"")+p.hero_name,8,109,150,22,14);Label(tile,p.name,8,131,150,20,12);
     long player=p.player_id;Bind(tile,()=>SwitchMatchParticipant(player));
    }
   }else Label(body,"외형 정보 없음 · 캐시 자동 보완 대기",10,70,330,100,16);
   if(current!=null&&!current.available){Label(body,"이 참가자의 경기 분석을 준비하고 있습니다.",360,100,680,40,18);return;}
   if(chipSelectionId!=0){DrawChipSelectionDetail(current?.cards?.chips??(current==null?d.cards?.chips:null));return;}
   if(diceView){DrawDice(current?.cards?.dice??(current==null?d.cards?.dice:null),true);return;}
   if(cardView){DrawParticipantCards(current?.cards??(current==null?d.cards:null));return;}
   if(chipView){DrawParticipantChips(current?.cards?.chips??(current==null?d.cards?.chips:null));return;}
   if(metricIndex>=0){DrawMetricDetail(d,current);return;}
   var rows=current?.rows??d.rows;
   for(int column=0;column<3;column++)Round(body,352+column*237,44,228,330,Color.white,8);
   for(int i=0;rows!=null&&i<rows.Length&&i<21;i++){
    float x=352+(i/7)*237,y=44+(i%7)*48;var row=Component(body,x,y,228,42);NativeUI.Set(row,"opaque",true);
    var background=Round(row,0,0,228,42,Color.white,4);
    if(i%7<6)Round(body,x+10,y+45,208,1,Rgb(0xE4E0E7),0);
    string label=rows[i][0].Replace("총 피해량 (가한 피해)","가한 피해").Replace("총 받은 피해량","받은 피해").Replace("총 이동 거리 (칸)","총 이동 거리").Replace("이동 주사위 굴림 횟수","이동 주사위").Replace("칩 선택 횟수","칩 선택");
    bool best=current!=null&&current.best!=null&&i<current.best.Length&&current.best[i];
    float textY=best?1:7;
    var metricLabel=Label(row,label,10,textY,145,28,16);
    var metricValue=Label(row,rows[i][1],157,textY,50,28,20,true);
    NativeUI.Set(metricLabel,"tooltips",rows[i][0]);
    NativeUI.Set(metricLabel,"verticalAlign","Middle");
    NativeUI.Set(metricValue,"verticalAlign","Middle");
    ApplyMetricFont(metricLabel);
    TextColor(metricLabel,Rgb(0x514B59));
    NativeUI.Set(metricLabel,"stroke",.12f);
    NativeUI.Set(metricLabel,"strokeColor",Rgb(0x514B59));
    if(best){var badge=Label(row,"BEST!",11,24,95,16,12,true);TextColor(badge,new Color(.78f,.39f,.08f,1));}
    if(i>0){
     var arrow=Label(row,"›",210,8,16,25,19);
     paints[row.Pointer]=new ButtonPaint{Background=background,Text=metricLabel,Arrow=arrow,MetricRow=true};
     Paint(row.Pointer,false);
     int metric=i;Bind(row,()=>{metricIndex=metric;metricPage=0;shopDetailRow=-1;ShowMetricPage();});
    }
   }
   NativeUI.Set(footer,"text","");
  }
  readonly Dictionary<string,MetricDetail> metricCache=new Dictionary<string,MetricDetail>();
  string metricCacheReplay;
  static string MetricCacheKey(string replay,long player,int metric,int page)=>replay+":"+player+":"+metric+":"+page;
  void CacheMetricDetails(StatsSnapshot value){
   string replay=value.metric_detail?.replay_id;
   if(replay==null&&value.metric_first_pages!=null&&value.metric_first_pages.Length>0)replay=value.metric_first_pages[0].replay_id;
   if(replay!=null&&replay!=metricCacheReplay){metricCache.Clear();metricCacheReplay=replay;}
   if(value.metric_first_pages!=null)foreach(var detail in value.metric_first_pages)
    metricCache[MetricCacheKey(detail.replay_id,detail.player_id,detail.metric,detail.page)]=detail;
   var current=value.metric_detail;
   if(current!=null)metricCache[MetricCacheKey(current.replay_id,current.player_id,current.metric,current.page)]=current;
   var next=value.metric_next_page;
   if(next!=null)metricCache[MetricCacheKey(next.replay_id,next.player_id,next.metric,next.page)]=next;
  }
  MetricDetail CachedMetricDetail(GameDetail game,long player,int metric,int page){
   return metricCache.TryGetValue(MetricCacheKey(game.replay_id,player,metric,page),out var detail)?detail:null;
  }
  void ShowMetricPage(){
   var game=snapshot?.details!=null&&selected>=0&&selected<snapshot.details.Length?snapshot.details[selected]:null;
   long player=selectedPlayer>0?selectedPlayer:owner;
   if(game==null||CachedMetricDetail(game,player,metricIndex,metricPage)==null)Request(true);
   else awaitingSnapshot=false;
   Render();
   PrefetchNextMetricPage();
  }
  void PrefetchNextMetricPage(){
   if(awaitingSnapshot||metricIndex<0||snapshot?.supports_metric_prefetch!=true)return;
   var game=snapshot.details!=null&&selected>=0&&selected<snapshot.details.Length?snapshot.details[selected]:null;
   if(game==null)return;
   long player=selectedPlayer>0?selectedPlayer:owner;
   var current=CachedMetricDetail(game,player,metricIndex,metricPage);
   if(current==null||metricPage+1>=current.pages)return;
   if(CachedMetricDetail(game,player,metricIndex,metricPage+1)==null)Request(true);
  }
  void EnsureMetricPrefetch(){
   var game=snapshot?.details!=null&&selected>=0&&selected<snapshot.details.Length?snapshot.details[selected]:null;
   if(game==null)return;
   var participant=SelectedParticipant(game);
   var rows=participant?.rows??game.rows;
   long player=participant?.player_id??owner;
   if(rows==null)return;
   for(int metric=0;metric<rows.Length;metric++){
    if(CachedMetricDetail(game,player,metric,0)==null){Request(true);return;}
   }
  }
  int shopDetailRow=-1;
  void SwitchMatchParticipant(long player){
   if((selectedPlayer>0?selectedPlayer:owner)==player)return;
   selectedPlayer=player;
   metricPage=chipPage=cardPage=chipDetailPage=0;
   chipSelectionId=0;
   shopDetailRow=-1;
   if(metricIndex>=0)ShowMetricPage();
   else{EnsureMetricPrefetch();Render();}
  }
  void DrawMetricDetail(GameDetail game,MatchAppearance participant){
   QuietAction(body,"‹ 합계로",()=>{metricIndex=-1;metricPage=0;chipSelectionId=0;chipDetailPage=0;Render();},352,37,92,32);
   long player=participant?.player_id??owner;
   var detail=CachedMetricDetail(game,player,metricIndex,metricPage)??snapshot.metric_detail;
   if(detail==null||detail.replay_id!=game.replay_id||detail.player_id!=player||detail.metric!=metricIndex||detail.page!=metricPage){Label(body,"상세 기록을 불러오는 중입니다…",465,42,570,28,17);loadingProgress=Label(body,"",465,79,570,28,16);ReadCollectionStatus();return;}
   if(detail.shop_details!=null&&shopDetailRow>=0&&shopDetailRow<detail.shop_details.Length){
    DrawShopVisit(detail,shopDetailRow);return;
   }
   Label(body,detail.title+" · "+detail.total+"건",458,40,395,28,18);
   DetailPager(metricPage,detail.pages,
    ()=>{metricPage--;ShowMetricPage();},
    ()=>{metricPage++;ShowMetricPage();});
   const float stride=31,height=29;
   for(int i=0;detail.rows!=null&&i<detail.rows.Length;i++){
    float y=82+i*stride;var row=Component(body,352,y,702,height);NativeUI.Set(row,"opaque",true);Round(row,0,0,702,height,i%2==0?Color.white:sectionBackground,5);
    if(detail.shop_details!=null&&i<detail.shop_details.Length){
     var visit=detail.rows[i];Label(row,visit[0],10,3,320,24,15);
     if(!string.IsNullOrEmpty(visit[1]))DrawShopBadge(row,visit[1],355,0xE8EFFF,0x1758A6);
     if(!string.IsNullOrEmpty(visit[2]))DrawShopBadge(row,visit[2],469,0xFFF1D6,0x805410);
     Label(row,"›",678,2,18,25,19);int index=i;Bind(row,()=>{shopDetailRow=index;Render();});
     continue;
    }
    Label(row,detail.rows[i][0],10,3,95,24,14);var eventText=Label(row,detail.rows[i][1],113,1,555,27,13);NativeUI.Set(eventText,"singleLine",false);NativeUI.Set(eventText,"tooltips",detail.rows[i][1]);
    if(detail.row_chip_selections!=null&&i<detail.row_chip_selections.Length&&detail.row_chip_selections[i]>0){int selection=detail.row_chip_selections[i];Label(row,"›",678,2,18,25,19);Bind(row,()=>OpenChipSelection(selection));}
   }
   if(detail.title=="아군 지원"){
    var note=Label(body,detail.note??"",360,337,690,42,13);
    NativeUI.Set(note,"singleLine",false);
   }
   NativeUI.Set(footer,"text",string.IsNullOrEmpty(detail.turn_average)?"":"선택 유저 턴당 평균 "+detail.turn_average);
  }
  void DrawShopBadge(IObject row,string title,float x,int background,int foreground){
   Round(row,x,3,105,23,Rgb(background),6);
   TextColor(Label(row,title,x+9,3,92,23,14,true),Rgb(foreground));
  }
  void DrawShopVisit(MetricDetail detail,int index){
   QuietAction(body,"‹ 방문 목록",()=>{shopDetailRow=-1;Render();},453,37,114,32);
   Label(body,detail.rows[index][0],579,40,460,28,19,true);
   Round(body,352,82,702,295,Color.white,8);
   string content=string.Join("\n\n",detail.shop_details[index]);
   var text=Label(body,content,368,95,670,266,17);
   NativeUI.Set(text,"singleLine",false);
   NativeUI.Set(text,"tooltips",content);
   NativeUI.Set(footer,"text","");
  }
  void DrawProfile(OverallProfile profile){
   NativeUI.Set(footer,"text","");
   if(profile==null){Label(body,"프로필 통계를 불러올 수 없습니다.",16,30,1000,35,20);return;}
   var favorites=profile.favorites??Array.Empty<ProfileCharacter>();
   Round(body,0,0,1054,384,Color.white,16);
   Round(body,8,8,270,368,sectionBackground,12);
   Round(body,25,23,236,220,lavender,10);
   if(favorites.Length>0)portraits.Add(body,new MatchAppearance{hero_id=favorites[0].hero_id,standing_painting_id=favorites[0].standing_painting_id},30,29,226,208);
   else Label(body,"캐릭터 기록 없음",44,112,196,28,17);
   var name=Label(body,string.IsNullOrWhiteSpace(profile.nickname)?"내 프로필":profile.nickname,26,251,234,42,29,true);
   NativeUI.Set(name,"tooltips",profile.nickname??"");
   if(favorites.Length>0){
    Label(body,"가장 많이 플레이한 캐릭터",26,303,234,25,14);
    var favorite=Label(body,favorites[0].hero_name,26,329,234,27,18,true);
    NativeUI.Set(favorite,"tooltips",favorites[0].hero_name);
   }
   string[] labels={"총 경기 수","승률"};
   string[] values={profile.matches.ToString(),profile.win_rates!=null&&profile.win_rates.Length>0?profile.win_rates[0]:"-"};
   for(int i=0;i<labels.Length;i++){
    float x=i==0?300:448,width=i==0?130:180;
    Label(body,labels[i],x,22,width,25,16);
    var value=Label(body,values[i],x,51,width,37,i==1?23:32,true);
    TextColor(value,accent);
    if(i==1&&profile.win_rates!=null&&profile.win_rates.Length>1)Label(body,profile.win_rates[1],x,89,width,25,19,true);
   }
   Label(body,"평균 클리어",636,22,150,25,16);
   string clear=profile.records!=null&&profile.records.Length>0?profile.records[0][1]:"-";
   var clearValue=Label(body,clear,636,51,150,37,29,true);
   TextColor(clearValue,accent);
   NativeUI.Set(clearValue,"tooltips","PvE 승리 경기의 최종 턴 기준");
   DrawProfilePicks(profile.pick_counts);
   Round(body,300,129,730,1,line,0);
   DrawProfileRanking("자주 플레이한 맵",300,profile.favorite_maps,profile.matches);
   var characters=Array.ConvertAll(favorites,item=>new ProfileActivity{name=item.hero_name,matches=item.matches});
   DrawProfileRanking("자주 쓰는 캐릭터",448,characters,profile.matches);
   var encounters=Array.ConvertAll(profile.encounters??Array.Empty<EncounterCharacter>(),item=>new ProfileActivity{name=item.name,matches=item.count});
   DrawProfileRanking("자주 만난 캐릭터",596,encounters,profile.matches);
   DrawProfileChoices(CurrentCards());
   if(profile.records!=null){
    Round(body,294,310,744,62,sectionBackground,8);
    for(int i=1;i<profile.records.Length&&i<5;i++){
     float x=304+(i-1)*183;
     bool linked=profile.record_positions!=null&&i<profile.record_positions.Length&&profile.record_positions[i]>=0;
     Label(body,profile.records[i][0]+(linked?"  ›":""),x,315,176,21,14);
     var record=Label(body,profile.records[i][1],x,337,176,28,21,true);
     TextColor(record,accent);
     if(linked){
      int position=profile.record_positions[i];
      var target=Component(body,x,313,176,56);
      NativeUI.Set(target,"opaque",true);
      NativeUI.Set(target,"tooltips","기록 경기 보기");
      Bind(target,()=>OpenRecordMatch(position));
      NativeUI.Set(record,"tooltips","클릭하여 기록 경기 보기");
     }
     if(i<2)NativeUI.Set(record,"tooltips","PvE 승리 경기의 최종 턴 기준");
    }
   }
  }
  IObject SecondaryLabel(IObject parent,string text,float x,float y,float width,float height){
   var label=Label(parent,text,x,y,width,height,13);
   ApplyMetricFont(label,true);
   TextColor(label,Rgb(0x514B59));
   return label;
  }
  void DrawProfileRanking(string title,float x,ProfileActivity[] items,int total){
   const float width=138;
   Label(body,title,x,146,width,28,15,true);
   if(items==null||items.Length==0){
    Label(body,"현재 필터에 기록이 없습니다.",x,192,width,28,15);
    return;
   }
   for(int i=0;i<items.Length&&i<3;i++){
    var item=items[i];
    float y=176+i*43;
    var label=Label(body,item.name,x,y,width,24,15,true);
    NativeUI.Set(label,"tooltips",item.name);
    float share=total>0?(float)item.matches/total:0;
    SecondaryLabel(body,item.matches+"경기 · "+share.ToString("P0"),x,y+23,width,20);
    Round(body,x,y+40,width,3,sectionBackground,2);
    if(share>0)Round(body,x,y+40,width*share,3,accent,2);
   }
  }
  void DrawProfileChoices(CardPanel data){
   const float width=138;
   Label(body,"자주 쓰는 카드",744,146,width,28,15,true);
   Label(body,"자주 고르는 칩",892,146,width,28,15,true);
   for(int column=0;column<2;column++){
    float x=744+column*148;
    int length=column==0?(data?.effect_used?.Length??0):(data?.chips?.items?.Length??0);
    if(length==0){Label(body,"기록 없음",x,192,width,28,15);continue;}
    for(int i=0;i<length&&i<3;i++){
     float y=176+i*43;
     string name=column==0?data.effect_used[i].name:data.chips.items[i].name;
     int count=column==0?data.effect_used[i].count:data.chips.items[i].count;
     int total=column==0?data.analyzed:data.chips.analyzed;
     var label=Label(body,name,x,y,width,24,15,true);
     NativeUI.Set(label,"tooltips",name);
     if(column==1)TextColor(label,ChipRarityColor(data.chips.items[i].rarity));
     SecondaryLabel(body,column==0?count+"회 · 경기당 "+(total>0?((float)count/total).ToString("0.##"):"-")+"회":count+"회",x,y+23,width,20);
    }
   }
  }
  void DrawProfilePicks(int[] counts){
   const float x=806;
   Label(body,"즐겨하는 픽",x,22,224,25,16,true);
   int total=0,highest=0;
   if(counts!=null)foreach(int count in counts){total+=count;highest=Math.Max(highest,count);}
   if(total==0){Label(body,"순번 기록 없음",x,51,224,26,15);return;}
   for(int i=0;i<counts.Length&&i<4;i++){
    int count=counts[i];
    float left=x+(i%2)*112,top=47+(i/2)*40;
    var label=Label(body,(i+1)+"픽 · "+count+"경기",left,top,108,20,14,count==highest);
    var percentage=SecondaryLabel(body,((float)count/total).ToString("P0"),left,top+21,108,17);
    if(count==highest)TextColor(percentage,accent);
    if(count==highest)TextColor(label,accent);
   }
  }
  void DrawGroupBasic(CardPanel data){
   if(data==null){Label(body,"통계를 불러오는 중입니다…",16,30,1000,35,20);return;}
   DrawAggregateRows((data.title??"통계").Replace(" 카드 순위",""),data.basic_rows);
  }
  void DrawAggregateRows(string title,string[][] rows){
   Label(body,title,16,6,1000,30,21,true);
   Label(body,"지표",24,53,440,28,16);Label(body,"합계 / 최고 기록",480,53,270,28,16);Label(body,"경기당 평균",800,53,230,28,16);
   for(int i=0;rows!=null&&i<rows.Length&&i<8;i++){float y=94+i*43;Round(body,0,y,1054,38,i%2==0?Color.white:sectionBackground,8);Label(body,rows[i][0],24,y+6,440,27,17);Label(body,rows[i][1],480,y+6,300,27,17);Label(body,rows[i][2],800,y+6,230,27,17);}
   NativeUI.Set(footer,"text","");
  }
  void DrawParticipantCards(CardPanel data){
   if(data==null){Label(body,"선택한 참가자의 카드 기록을 준비하고 있습니다.",360,80,680,35,18);return;}
   int pages=CardPages(data);cardPage=Math.Min(cardPage,pages-1);
   string[] titles={"배틀 카드 사용","그 외 카드 사용","구매"};CardTopItem[][] lists={data.battle_used,data.effect_used,data.bought};
   for(int col=0;col<3;col++){float x=352+238*col;Round(body,x,44,226,342,Color.white,10);Label(body,titles[col],x+10,55,206,28,14);
    Round(body,x+10,86,206,1,line,0);
    var list=lists[col];int start=cardPage*CardsPerPage;
    if(list==null||start>=list.Length){Label(body,"이 페이지에 기록 없음",x+10,115,206,26,13);continue;}
    for(int i=0;i<CardsPerPage&&start+i<list.Length;i++){
     var item=list[start+i];float y=90+i*48;
     bool image=StatItemImages.Card(body,item.card_id,x+10,y+5,28,38);
     var title=Label(body,item.name,x+(image?48:13),y+11,image?113:148,27,15);
     ApplyMetricFont(title,true);NativeUI.Set(title,"tooltips",item.name);
     var count=Label(body,item.count+"회",x+165,y+10,48,28,18,true);
     var format=NativeUI.Get(count,"textFormat");NativeUI.Set(format,"align",2);NativeUI.Set(count,"textFormat",format);
     if(i+1<CardsPerPage&&start+i+1<list.Length)Round(body,x+10,y+47,206,1,line,0);
    }
   }
   NativeUI.Set(footer,"text",(cardPage+1)+" / "+pages+" 페이지 · 선택한 참가자의 해당 경기 카드");
  }
  void DrawParticipantChips(ChipPanel data){
   if(data==null){Label(body,"선택한 참가자의 칩 기록을 준비하고 있습니다.",360,80,680,35,18);return;}
   int n=data.items?.Length??0,pages=Math.Max(1,(n+5)/6);chipPage=Math.Min(chipPage,pages-1);
   Label(body,"칩 획득 "+data.selections+"건",360,39,680,24,14);
   if(n==0)Label(body,"확인된 칩 획득 기록이 없습니다.",360,110,680,35,19);
   for(int i=0;i<6&&chipPage*6+i<n;i++){int rank=chipPage*6+i;var item=data.items[rank];float y=70+i*48;
    var row=Component(body,352,y,702,43);NativeUI.Set(row,"opaque",true);Round(row,0,0,702,43,line,8);Round(row,1,1,700,41,Color.white,7);Round(row,10,7,32,29,sectionBackground,6);
    bool reward=!string.IsNullOrEmpty(item.acquisition_label);Label(row,(rank+1).ToString(),16,12,28,23,14);
    bool image=StatItemImages.Chip(row,item.chip_id,54,6,30,30);
    float nameLeft=image?94:54,nameWidth=image?440:480;
    var title=Label(row,item.name,nameLeft,reward?2:9,nameWidth,reward?23:27,16);
    TextColor(title,ChipRarityColor(item.rarity));NativeUI.Set(title,"tooltips",item.name);
    var turn=Label(row,string.IsNullOrEmpty(item.turn_label)?"턴 미확인":item.turn_label,548,11,116,25,16);
    var turnFormat=NativeUI.Get(turn,"textFormat");NativeUI.Set(turnFormat,"align",2);NativeUI.Set(turn,"textFormat",turnFormat);
    if(!item.acquisition_only)TextColor(Label(row,"›",678,9,16,25,19),Rgb(0xB2AAB9));
    if(reward)Label(row,item.acquisition_label,nameLeft,25,nameWidth,17,12);
    if(!item.acquisition_only){int selection=ChipSelectionKey(item);Bind(row,()=>OpenChipSelection(selection));}
   }
   NativeUI.Set(footer,"text",(chipPage+1)+" / "+pages+" 페이지 · 선택한 참가자의 해당 경기 칩");
  }
  static int ChipSelectionKey(ChipItem item)=>item.selection_id>0?item.selection_id:-item.chip_id;
  void OpenChipSelection(int selection){chipSelectionId=selection;chipDetailPage=0;Render();}
  void DrawChipSelectionDetail(ChipPanel data){
   QuietAction(body,chipView?"‹ 칩 목록":"‹ 선택 목록",()=>{chipSelectionId=0;chipDetailPage=0;Render();},352,37,92,32);
   ChipItem item=null;if(data?.items!=null)foreach(var candidate in data.items)if(ChipSelectionKey(candidate)==chipSelectionId){item=candidate;break;}
   if(item==null){Label(body,"칩 선택 기록을 불러오는 중입니다…",460,42,580,28,17);return;}
   if(item.acquisition_only){chipSelectionId=0;chipDetailPage=0;Render();return;}
   var heading=Label(body,(item.turn_label??"턴 미확인")+" · "+item.name,458,40,390,28,17);NativeUI.Set(heading,"tooltips",item.name);TextColor(heading,ChipRarityColor(item.rarity));
   var rows=item.detail_rows??new[]{new[]{"최초 후보","확인 불가 · 후보 기록 재분석이 필요합니다."},new[]{"최종 선택",item.name}};
   int pages=Math.Max(1,(rows.Length+3)/4);chipDetailPage=Math.Min(chipDetailPage,pages-1);
   DetailPager(chipDetailPage,pages,
    ()=>{chipDetailPage--;Render();},
    ()=>{chipDetailPage++;Render();});
   for(int i=0;i<4&&chipDetailPage*4+i<rows.Length;i++){
    var row=rows[chipDetailPage*4+i];float y=82+i*58;Round(body,352,y,702,54,i%2==0?Color.white:sectionBackground,7);Label(body,row[0],362,y+15,104,26,16);
    int rowIndex=chipDetailPage*4+i;var options=item.detail_options!=null&&rowIndex<item.detail_options.Length?item.detail_options[rowIndex]:null;
    if(options!=null&&options.Length>0){
     float columnWidth=565f/options.Length;
     for(int j=0;j<options.Length;j++){var option=options[j];var text=Label(body,option.name,475+j*columnWidth,y+3,columnWidth-10,48,16);NativeUI.Set(text,"singleLine",false);NativeUI.Set(text,"tooltips",option.name);TextColor(text,ChipRarityColor(option.rarity));if(j+1<options.Length)Label(body,"/",475+(j+1)*columnWidth-9,y+16,9,22,13);}
    }else{var text=Label(body,row[1],475,y+3,565,48,16);NativeUI.Set(text,"singleLine",false);NativeUI.Set(text,"tooltips",row[1]);if(row[0]=="최종 선택")TextColor(text,ChipRarityColor(item.rarity));}
   }
   NativeUI.Set(footer,"text","");
  }
  int ContentIndex=>diceView?3:chipView?2:cardView?1:0;
  void SetContent(int index){metricIndex=-1;metricPage=0;chipSelectionId=0;chipDetailPage=0;CloseDropdown();if(tab==3&&selected<0&&index!=0)return;cardView=index==1;chipView=index==2;diceView=index==3;chipPage=cardPage=0;Render();}
  public void ShowDice(){SetContent(3);}
  public void ShowBasic(){SetContent(0);}
  void LayoutContentTabs(){
   bool match=tab==3&&selected>=0,history=tab==3&&!match;
   NativeUI.Set(contentTabGroup,"visible",!match&&!history&&(tab!=0||ContentIndex!=0));
   NativeUI.Set(matchTabGroup,"visible",match);
   for(int i=0;i<4;i++){var b=contentTabs[i];NativeUI.Set(b,"visible",!match&&!history&&(tab!=0||ContentIndex!=0));SetButtonText(b,i==0?(tab==0?"프로필":"기본 통계"):i==1?"카드":i==2?"칩":"주사위");SetButtonSelected(b,i==ContentIndex);}
   for(int i=0;i<4;i++){NativeUI.Set(matchContentTabs[i],"visible",match);SetButtonSelected(matchContentTabs[i],i==ContentIndex);}
   SetButtonText(matchBack,recordMatch?"‹ 요약으로":"‹ 경기목록으로");
   NativeUI.Set(matchBack,"visible",match);bool grouped=cardKey!=null;NativeUI.Set(contextBack,"visible",!match&&grouped);
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
   Round(body,x,66,342,320,kind==2?sectionBackground:lavender,12);Label(body,title,x+14,72,314,22,17);
   int start=cardPage*CardsPerPage;
   if(list==null||start>=list.Length){string empty=data.analyzed==0?"분석된 원본이 없습니다":start>0?"이 페이지에 기록 없음":"확인된 기록이 없습니다";Label(body,empty,x+14,133,314,28,16);return;}
   for(int i=0;i<CardsPerPage&&start+i<list.Length;i++){
    var item=list[start+i];float y=98+i*58;
    Round(body,x+8,y,326,48,Color.white,8);
    bool image=StatItemImages.Card(body,item.card_id,x+14,y+4,28,40);
    float left=image?52:17,width=image?272:308;
    var name=Label(body,item.name,x+left,y+4,width,22,16);
    ApplyMetricFont(name,true);NativeUI.Set(name,"tooltips",item.name);
    Label(body,item.count+"회 · 경기당 "+item.average+"회",x+left,y+28,width,18,13);
   }
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
    Round(body,x,y,520,49,rank<3?RankOutline(rank):line,9);Round(body,x+2,y+2,516,45,Color.white,7);Round(body,x+10,y+9,32,30,rank<3?RankBadge(rank):paper,6);Label(body,(rank+1).ToString(),x+16,y+14,28,23,15);bool image=StatItemImages.Chip(body,item.chip_id,x+54,y+7,34,34);
    float nameLeft=image?98:54,nameWidth=image?410:454;
    var chipName=Label(body,item.name,x+nameLeft,y+3,nameWidth,24,16);
    NativeUI.Set(chipName,"tooltips",item.name);TextColor(chipName,ChipRarityColor(item.rarity));
    Label(body,item.count+"회 선택",x+nameLeft,y+27,nameWidth,20,13);
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
  void UpdateFilterLabels(){
   if(modeButton!=null){
    SetButtonText(modeButton,ChoiceLabel(modeChoices,mode,"전체 모드"));
    SetButtonSelected(modeButton,mode!=-1);
   }
   if(mapButton!=null){
    SetButtonText(mapButton,ChoiceLabel(mapChoices,map,"전체 맵"));
    SetButtonSelected(mapButton,map!=-1);
   }
   if(heroButton!=null){
    SetButtonText(heroButton,ChoiceLabel(heroChoices,hero,"전체 캐릭터"));
    SetButtonSelected(heroButton,hero!=-1);
   }
   if(difficultyButton!=null){
    bool enabled=DifficultyEnabled;
    SetButtonSelected(difficultyButton,enabled&&difficulty!=-1);
    SetButtonText(difficultyButton,enabled?ChoiceLabel(difficultyChoices,difficulty,"전체 난이도"):"난이도 해당 없음");
    NativeUI.Set(difficultyButton,"grayed",!enabled);
    NativeUI.Set(difficultyButton,"touchable",enabled);
   }
  }
  bool Within(IObject child,IObject ancestor){if(child==null||ancestor==null)return false;for(int i=0;child!=null&&i<24;i++,child=NativeUI.Get(child,"parent"))if(NativeUI.Same(child,ancestor))return true;return false;}
  void CloseDropdown(){if(dropdown!=null){try{NativeUI.Call(dropdown,"Dispose");}catch{}dropdown=null;PruneClicks();}dropdownKind=0;dropdownModel=null;}
  void ToggleDropdown(int kind){if(kind==4&&!DifficultyEnabled)return;if(dropdown!=null&&dropdownKind==kind){CloseDropdown();return;}CloseDropdown();var choices=kind==1?modeChoices:kind==2?mapChoices:kind==3?heroChoices:difficultyChoices;if(choices==null||choices.Length==0)return;dropdownKind=kind;dropdownModel=new DropdownModel(choices,kind==1?mode:kind==2?map:kind==3?hero:difficulty);DrawDropdown();}
  IObject ActionButton(IObject parent,string text,Action action,float x,float y,float w,float h){var b=NewButton(parent,text,null,x,y,w,h);Bind(b,action);return b;}
  IObject DropdownItem(string title,Action action,float x,float y,float width,float height){
   var item=Component(dropdown,x,y,width,height);
   NativeUI.Set(item,"opaque",true);
   var background=Round(item,0,0,width,height,Color.white,2);
   var text=Label(item,title,10,3,width>80?width-39:width-20,height-6,15);
   NativeUI.Set(text,"tooltips",title);
   var check=Label(item,"✓",width-26,3,21,height-6,15);
   TextColor(check,accent);
   paints[item.Pointer]=new ButtonPaint{Background=background,Text=text,Check=check,Dropdown=true};
   Paint(item.Pointer,false);Bind(item,action);
   return item;
  }
  void DrawDropdown(){
   if(dropdownModel==null)return;
   if(dropdown!=null){NativeUI.Call(dropdown,"Dispose");dropdown=null;PruneClicks();}
   var anchor=dropdownKind==1?modeButton:dropdownKind==2?mapButton:dropdownKind==3?heroButton:difficultyButton;float w=NativeUI.Float(anchor,"width");int count=dropdownModel.VisibleCount;float h=12+count*40+(dropdownModel.Count>DropdownModel.Limit?42:0);
   dropdown=Component(pane,NativeUI.Float(anchor,"x"),NativeUI.Float(anchor,"y")+NativeUI.Float(anchor,"height")+4,w,h);NativeUI.Set(dropdown,"name","astral_filter_dropdown");NativeUI.Set(dropdown,"sortingOrder",100);NativeUI.Set(dropdown,"opaque",true);Round(dropdown,0,0,w,h,line,4);Round(dropdown,1,1,w-2,h-2,Color.white,3);
   for(int row=0;row<count;row++){
    int index=dropdownModel.Offset+row;var option=dropdownModel.At(index);bool chosen=option.id==dropdownModel.SelectedId;
    var b=DropdownItem(option.label,()=>SelectDropdown(index),5,6+row*40,w-10,37);SetButtonSelected(b,chosen);
   }
   if(dropdownModel.Count>DropdownModel.Limit){
    float y=8+count*40;
    Round(dropdown,6,y-3,w-12,1,line,0);
    DropdownItem("▲",()=>ScrollDropdown(-1),5,y,52,32);
    DropdownItem("▼",()=>ScrollDropdown(1),w-57,y,52,32);
    Label(dropdown,(dropdownModel.Offset+1)+"–"+(dropdownModel.Offset+count)+" / "+dropdownModel.Count,68,y+5,w-136,26,14);
   }
  }
  void SelectDropdown(int index){var option=dropdownModel?.At(index);if(option==null)return;if(dropdownKind==1){mode=option.id;if(option.is_pvp)difficulty=-1;}else if(dropdownKind==2)map=option.id;else if(dropdownKind==3)hero=option.id;else if(dropdownKind==4)difficulty=option.id;page=0;Change(true);}
  void ScrollDropdown(int delta){if(dropdownModel!=null&&dropdownModel.Scroll(delta))DrawDropdown();}
  void PollDropdownScroll(){if(!open||dropdown==null)return;float wheel=Input.mouseScrollDelta.y;if(Math.Abs(wheel)<.001f)return;var root=NativeUI.Static("FairyGUI.GRoot","inst");if(Within(NativeUI.Get(root,"touchTarget"),dropdown))ScrollDropdown(wheel>0?-1:1);}
  void Change(bool preserveView=false){metricIndex=-1;metricPage=0;chipSelectionId=0;chipDetailPage=0;if(!preserveView||tab==3){diceView=false;chipView=false;cardView=false;}chipPage=cardPage=0;selectedPlayer=0;CloseDropdown();selected=-1;cardKey=null;snapshot=null;Request();Render();nextRead=0;}
  void SetTab(int v){
   CloseFailures();
   if(v!=3&&v!=0)lastStatisticsTab=v;
   if(recordMatch){sortColumn=savedSortColumn;sortDescending=savedSortDescending;sortActive=savedSortActive;}
   recordMatch=false;cardView=false;tab=v;page=0;Change();
  }
  public void TabStatistics(){SetTab(lastStatisticsTab);}
  public void TabAll(){SetTab(0);}public void TabOverall(){SetTab(4);}public void TabMaps(){SetTab(1);}public void TabHeroes(){SetTab(2);}public void TabHistory(){SetTab(3);}
  public void NextDifficulty(){ToggleDropdown(4);}public void NextMode(){ToggleDropdown(1);}public void NextMap(){ToggleDropdown(2);}public void NextHero(){ToggleDropdown(3);}
  public void PreviousPage(){if(cardView){PreviousCards();return;}if(chipView){PreviousChips();return;}if(page>0){page--;if(!ShowCachedHistoryPage())Change();}}
  public void NextPage(){if(cardView){NextCards();return;}if(chipView){NextChips();return;}if(snapshot!=null&&page+1<snapshot.pages){page++;if(!ShowCachedHistoryPage())Change();}}
  int sortColumn,savedSortColumn;bool sortDescending=true,savedSortDescending,sortActive,savedSortActive;
  void SortHistory(int column){
   bool firstDirection=column==0;
   if(!sortActive||column!=sortColumn){
    sortActive=true;sortColumn=column;sortDescending=firstDirection;
   }else if(sortDescending==firstDirection){
    sortDescending=!firstDirection;
   }else{
    sortActive=false;sortColumn=0;sortDescending=true;
   }
   page=0;
   if(snapshot?.history_entries==null){Change();return;}
   Array.Sort(snapshot.history_entries,CompareHistoryEntries);
   ShowCachedHistoryPage();
  }
  static int CompareHistoryValue(JsonElement first,JsonElement second){
   bool firstMissing=first.ValueKind==JsonValueKind.Null||first.ValueKind==JsonValueKind.Undefined;
   bool secondMissing=second.ValueKind==JsonValueKind.Null||second.ValueKind==JsonValueKind.Undefined;
   if(firstMissing||secondMissing)return firstMissing==secondMissing?0:firstMissing?1:-1;
   if(first.ValueKind==JsonValueKind.Number)return first.GetDouble().CompareTo(second.GetDouble());
   return string.Compare(first.GetString(),second.GetString(),StringComparison.Ordinal);
  }
  int CompareHistoryEntries(HistoryEntry first,HistoryEntry second){
   var a=first.sort_values[sortColumn];var b=second.sort_values[sortColumn];
   int comparison=CompareHistoryValue(a,b);
   bool missing=a.ValueKind==JsonValueKind.Null||b.ValueKind==JsonValueKind.Null;
   if(comparison!=0)return sortDescending&&!missing?-comparison:comparison;
   comparison=CompareHistoryValue(first.sort_values[0],second.sort_values[0]);
   bool dateMissing=first.sort_values[0].ValueKind==JsonValueKind.Null||second.sort_values[0].ValueKind==JsonValueKind.Null;
   if(comparison!=0)return dateMissing?comparison:-comparison;
   return string.Compare(second.replay_id,first.replay_id,StringComparison.Ordinal);
  }
  bool ShowCachedHistoryPage(){
   if(tab!=3||selected>=0||snapshot?.history_entries==null)return false;
   var entries=snapshot.history_entries;
   int start=page*8,count=Math.Min(8,Math.Max(0,entries.Length-start));
   snapshot.rows=new string[count][];
   for(int i=0;i<count;i++)snapshot.rows[i]=entries[start+i].row;
   snapshot.page=page;snapshot.details=null;snapshotPresentation=null;
   awaitingSnapshot=false;
   Render();return true;
  }
  bool recordMatch;
  void OpenRecordMatch(int position){
   savedSortColumn=sortColumn;savedSortDescending=sortDescending;savedSortActive=sortActive;
   sortColumn=0;sortDescending=true;sortActive=false;
   recordMatch=true;tab=3;page=position/8;selected=position%8;selectedPlayer=0;
   metricIndex=-1;metricPage=0;chipSelectionId=0;chipDetailPage=0;shopDetailRow=-1;
   cardPage=chipPage=0;cardView=chipView=diceView=false;cardKey=null;
   snapshot=null;Request();Render();
  }
  public void RefreshStats(){metricCache.Clear();RuntimeGameCatalog.Invalidate();CloseDropdown();Request();nextRead=0;}public void BackToList(){metricIndex=-1;metricPage=0;chipSelectionId=0;chipDetailPage=0;cardPage=0;diceView=false;chipView=false;selectedPlayer=0;cardView=false;cardKey=null;selected=-1;if(recordMatch){recordMatch=false;sortColumn=savedSortColumn;sortDescending=savedSortDescending;sortActive=savedSortActive;tab=0;page=0;snapshot=null;Request();}Render();}
  void Select(int r){metricIndex=-1;metricPage=0;chipSelectionId=0;chipDetailPage=0;cardPage=0;diceView=false;chipView=false;if(snapshot==null)return;if(tab==3){if(r<0||r>=snapshot.rows.Length)return;selected=r;selectedPlayer=0;cardView=false;if(snapshot.details==null||r>=snapshot.details.Length||snapshot.details[r].rows==null)Request(true);else EnsureMetricPrefetch();}else{if(snapshot.row_cards==null||r>=snapshot.row_cards.Length)return;cardKey=snapshot.row_cards[r].key;selected=-1;cardView=false;}Render();}
  public void Row0(){Select(0);}public void Row1(){Select(1);}public void Row2(){Select(2);}public void Row3(){Select(3);}public void Row4(){Select(4);}public void Row5(){Select(5);}public void Row6(){Select(6);}public void Row7(){Select(7);}
 }
}

