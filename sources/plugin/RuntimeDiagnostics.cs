using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using System.Collections.Generic;
namespace AstralPersonalMod {
 // Own-operation diagnostics only: no account IDs, names, replay data or exception messages.
 internal static class RuntimeDiagnostics {
  const long Limit=1048576;
  static string file;
  static readonly List<string> pending=new List<string>();
  static readonly string[] tasks={"watcher","catalog","profile_ui","pointer"};
  static readonly long[] total=new long[4],maximum=new long[4],slowAt=new long[4];
  static readonly Dictionary<string,long> errors=new Dictionary<string,long>();
  static long nextFlush,nextSummary,frames,frameTotal,frameMax;
  static bool open;static long searches,searchTicks,searchMax,searchNodes,searchNodesMax,searchBytes;
  internal static long Clock()=>Stopwatch.GetTimestamp();
  static string Number(double value)=>value.ToString("0.###",CultureInfo.InvariantCulture);
  internal static void Initialize(string directory,string version){
   file=Path.Combine(directory,"plugin-runtime.log");long now=Clock();nextSummary=now+30*Stopwatch.Frequency;
   Event("loaded","version="+version+" diagnostics_schema=1",true);
   AppDomain.CurrentDomain.ProcessExit+=(_,__)=>Event("process_exit","",true);
   AppDomain.CurrentDomain.UnhandledException+=(_,e)=>{Error("unhandled",e.ExceptionObject as Exception);Flush(true);};
  }
  internal static void Event(string kind,string detail="",bool immediate=false){
   if(file==null)return;if(pending.Count>=128)pending.RemoveAt(0);
   string text=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture)+" "+kind+" "+detail;
   text=text.Replace('\r',' ').Replace('\n',' ');pending.Add(text.Length>1500?text.Substring(0,1500):text);
   if(immediate)Flush(true);
  }
  internal static void Error(string phase,Exception error){
   long now=Clock();if(errors.TryGetValue(phase,out long last)&&now-last<5*Stopwatch.Frequency)return;errors[phase]=now;
   if(error==null){Event("error","phase="+phase+" type=unknown",true);return;}
   string detail="phase="+phase+" type="+error.GetType().Name+" hresult="+error.HResult.ToString("X8",CultureInfo.InvariantCulture);
   try{var trace=new StackTrace(error,false);var items=trace.GetFrames();if(items!=null)for(int i=0;i<items.Length&&i<6;i++){var method=items[i].GetMethod();if(method!=null)detail+=" at="+(method.DeclaringType?.FullName??"?")+"."+method.Name;}
    if(error.InnerException!=null)detail+=" inner="+error.InnerException.GetType().Name;
   }catch{}
   Event("error",detail,true);
  }
  internal static long Mark(int task,long started){
   long now=Clock(),elapsed=Math.Max(0,now-started);total[task]+=elapsed;maximum[task]=Math.Max(maximum[task],elapsed);
   if(elapsed*1000.0/Stopwatch.Frequency>=20&&now-slowAt[task]>=5*Stopwatch.Frequency){slowAt[task]=now;Event("slow_task","task="+tasks[task]+" ms="+Number(elapsed*1000.0/Stopwatch.Frequency));}
   return now;
  }
  internal static void Search(int nodes,long started,long allocated){
   long elapsed=Math.Max(0,Clock()-started);searches++;searchTicks+=elapsed;searchMax=Math.Max(searchMax,elapsed);searchNodes+=nodes;searchNodesMax=Math.Max(searchNodesMax,nodes);searchBytes+=Math.Max(0,allocated);
  }
  internal static void View(bool visible){if(open==visible)return;open=visible;Event(visible?"stats_open":"stats_close","",true);}
  internal static void CompleteFrame(long started){
   long now=Clock(),elapsed=Math.Max(0,now-started);frames++;frameTotal+=elapsed;frameMax=Math.Max(frameMax,elapsed);
   if(now>=nextSummary){
    string data="samples="+frames+" own_avg_ms="+Number(frameTotal*1000.0/Stopwatch.Frequency/Math.Max(1,frames))+" own_max_ms="+Number(frameMax*1000.0/Stopwatch.Frequency)+" stats_open="+open;
    for(int i=0;i<tasks.Length;i++){data+=" "+tasks[i]+"_avg_ms="+Number(total[i]*1000.0/Stopwatch.Frequency/Math.Max(1,frames))+" "+tasks[i]+"_max_ms="+Number(maximum[i]*1000.0/Stopwatch.Frequency);total[i]=maximum[i]=0;}
    try{data+=" clr_managed_mb="+Number(GC.GetTotalMemory(false)/1048576.0)+" gc0="+GC.CollectionCount(0)+" gc1="+GC.CollectionCount(1)+" gc2="+GC.CollectionCount(2);using(var process=Process.GetCurrentProcess()){data+=" game_private_mb="+Number(process.PrivateMemorySize64/1048576.0);}}catch{}
    data+=" profile_scans="+searches+" scan_avg_ms="+Number(searchTicks*1000.0/Stopwatch.Frequency/Math.Max(1,searches))+" scan_max_ms="+Number(searchMax*1000.0/Stopwatch.Frequency)+" scan_nodes_avg="+Number(searchNodes/(double)Math.Max(1,searches))+" scan_nodes_max="+searchNodesMax+" scan_clr_bytes="+searchBytes;
    searches=searchTicks=searchMax=searchNodes=searchNodesMax=searchBytes=0;
    Event("performance",data);frames=frameTotal=frameMax=0;nextSummary=now+30*Stopwatch.Frequency;
   }
   Flush(false);
  }
  internal static void Flush(bool force){
   long now=Clock();if(file==null||pending.Count==0||!force&&now<nextFlush)return;nextFlush=now+5*Stopwatch.Frequency;
   string text=string.Join(Environment.NewLine,pending)+Environment.NewLine;pending.Clear();
   try{
    if(File.Exists(file)&&new FileInfo(file).Length+Encoding.UTF8.GetByteCount(text)>Limit){
     if(File.Exists(file+".2"))File.Delete(file+".2");if(File.Exists(file+".1"))File.Move(file+".1",file+".2");File.Move(file,file+".1");
    }
    File.AppendAllText(file,text,new UTF8Encoding(false));
   }catch{} // Diagnostics must never interrupt the game.
  }
 }
}
