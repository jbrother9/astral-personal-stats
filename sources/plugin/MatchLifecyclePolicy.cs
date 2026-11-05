namespace AstralPersonalMod {
 internal static class MatchLifecyclePolicy {
  internal static bool Playing(long state,long start,bool atHome,bool finishReady)=>state==25&&start>0&&!atHome&&!finishReady;
  internal static bool Finished(long state,long start,bool wasRunning,bool atHome,bool finishReady)=>start>0&&(state==30||finishReady||wasRunning&&atHome);
 }
}
