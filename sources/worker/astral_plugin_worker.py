"""Background worker for the in-game plugin. No capture, Steam login or admin required."""
import argparse,contextlib,datetime,json,os,pathlib,sqlite3,struct,sys,time,urllib.error
import astral_worker_storage as collector
import astral_paths as paths
import astral_personal_data as personal
import astral_replay as replay
import astral_overlay_data as overlay
import astral_card_stats as cards
import astral_collection_status as collection_status
from astral_worker_lifetime import ParentLifetime

SCHEMA=1

def request_value(obj):
 if not isinstance(obj,dict) or obj.get('schema')!=1 or obj.get('source')!='astral-personal-mod':raise ValueError('Unsupported request schema')
 for key in ('owner_id','room_id','start_time'):
  if type(obj.get(key)) is not int:raise ValueError('Invalid request number')
 owner,room,start=obj['owner_id'],obj['room_id'],obj['start_time']
 if not 0<owner<2**63 or not 0<room<1000000 or not 1000000000<=start<=4102444800:raise ValueError('Request number outside limits')
 if obj.get('reason') not in ('finish','left_room','history'):raise ValueError('Invalid request reason')
 if obj.get('replay_id')!=str(start*1000000+room):raise ValueError('Replay candidate mismatch')
 return obj

def init(store):
 store.db.execute('''CREATE TABLE IF NOT EXISTS plugin_requests(
 replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,room_id INTEGER NOT NULL,start_time INTEGER NOT NULL,
 reason TEXT NOT NULL,status TEXT NOT NULL,attempts INTEGER NOT NULL DEFAULT 0,next_try REAL NOT NULL DEFAULT 0,error TEXT,
 PRIMARY KEY(replay_id,owner_id))''');store.db.commit()

def accept(store,obj):
 obj=request_value(obj);rid,owner=obj['replay_id'],obj['owner_id']
 old=store.db.execute('SELECT status,reason FROM plugin_requests WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 with store.db:
  if old is None:
   store.db.execute("INSERT INTO plugin_requests(replay_id,owner_id,room_id,start_time,reason,status) VALUES (?,?,?,?,?,'pending')",(rid,owner,obj['room_id'],obj['start_time'],obj['reason']))
  elif old[0]=='failed' and old[1]=='left_room' and obj['reason'] in ('finish','history'):
   store.db.execute("UPDATE plugin_requests SET status='pending',attempts=0,next_try=0,error=NULL,reason=? WHERE replay_id=? AND owner_id=?",(obj['reason'],rid,owner))


def consume_inbox(store):
 folder=store.root/'plugin-inbox';folder.mkdir(exist_ok=True)
 for file in sorted(folder.glob('*.json'))[:100]:
  try:
   if file.stat().st_size>16384:raise ValueError('Request too large')
   obj=request_value(json.loads(file.read_text(encoding='utf-8-sig')));accept(store,obj)
   # No secrets are logged; the durable request is already in SQLite.
   file.unlink()
  except (ValueError,OSError,TypeError):
   collector.say('Invalid plugin request retained with .invalid suffix: '+file.name)
   try:file.rename(file.with_suffix('.invalid'))
   except OSError:pass


def process_one(store,downloader=None,status=None):
 row=store.db.execute("SELECT replay_id,owner_id,room_id,start_time,attempts FROM plugin_requests WHERE status='pending' AND next_try<=? ORDER BY start_time LIMIT 1",(time.time(),)).fetchone()
 if row is None:return False
 rid,owner,room,start,attempts=row
 try:
  exists=store.db.execute('SELECT analysis_version FROM personal_matches WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
  if exists and exists[0]>=personal.VERSION and cards.has_index(store.db,rid,owner) and personal.appearances(store.db,rid,owner) and personal.has_insights(store.db,rid,owner):
   with store.db:store.db.execute("UPDATE plugin_requests SET status='complete',error=NULL WHERE replay_id=? AND owner_id=?",(rid,owner))
   return True
  if status:status('analyzing',rid,owner)
  cache=store.root/'replays'/rid;data=None;summary=None
  if cache.exists() and cache.stat().st_size<=replay.MAX_REPLAY_BYTES:
   try:data=cache.read_bytes();summary=replay.parse_replay(data,rid)
   except (ValueError,IndexError):data=summary=None
  if data is None:
   if status:status('downloading',rid,owner)
   data=(downloader or collector.download)(rid)
   if status:status('analyzing',rid,owner)
   summary=replay.parse_replay(data,rid)
  if summary['room_id']!=room or summary['start_time']!=start:raise ValueError('Room/time mismatch')
  if owner not in [p['player_id'] for p in summary['participants']]:raise ValueError('Current account is not a replay participant')
  store.owner_id=owner;store.enqueue(rid);store.save(data,summary,True)
  with store.db:store.db.execute("UPDATE plugin_requests SET status='complete',error=NULL WHERE replay_id=? AND owner_id=?",(rid,owner))
  collector.say('SAVED '+rid+' / account '+str(owner)+' / '+str(summary['frame_count'])+' messages')
 except (OSError,ValueError,IndexError,TypeError,struct.error) as error:
  attempts+=1;delay=(5,15,60,180,600)[min(attempts-1,4)];status='failed' if attempts>=5 else 'pending'
  safe='HTTP '+str(error.code) if isinstance(error,urllib.error.HTTPError) else type(error).__name__
  with store.db:store.db.execute('UPDATE plugin_requests SET status=?,attempts=?,next_try=?,error=? WHERE replay_id=? AND owner_id=?',(status,attempts,time.time()+delay,safe,rid,owner))
  collector.say('Replay '+rid+' / '+safe+' / '+status)
 return True


def backfill_card_cache(store,state,status=None):
 if not state or state.get('owner_verified') is not True:return False
 owner=state.get('owner_id');rows=store.db.execute('SELECT m.replay_id FROM personal_matches m LEFT JOIN personal_card_stats c ON c.replay_id=m.replay_id AND c.owner_id=m.owner_id WHERE m.owner_id=? AND (c.version IS NULL OR c.version<? OR m.analysis_version<? OR NOT EXISTS (SELECT 1 FROM personal_appearances a WHERE a.replay_id=m.replay_id AND a.owner_id=m.owner_id) OR NOT EXISTS (SELECT 1 FROM personal_match_insights i WHERE i.replay_id=m.replay_id AND i.owner_id=m.owner_id AND i.version>=?)) ORDER BY m.finished_at DESC',(owner,cards.VERSION,personal.VERSION,personal.INSIGHTS_VERSION)).fetchall()
 for (rid,) in rows:
  file=store.root/'replays'/rid
  if not replay.valid_id(rid) or not file.is_file():continue
  if status:status('backfill',rid,owner)
  data=file.read_bytes();summary=replay.parse_replay(data,rid)
  if owner not in [p['player_id'] for p in summary['participants']]:continue
  personal.index(store.db,data,summary,owner)
  return True
 return False

def heartbeat(store):
 try:
  state=json.loads((store.root/'plugin-state.json').read_text(encoding='utf-8-sig'))
  if type(state.get('heartbeat')) not in (int,float) or abs(time.time()-state['heartbeat'])>120:return None
  owner=state.get('owner_id')
  if state.get('owner_verified') is not True:return None
  if type(owner) is int and 0<owner<2**63:
   prefs=personal.settings()
   if prefs['player_id']!=owner:personal.save_settings({**prefs,'player_id':owner})
  return state
 except (OSError,ValueError):return None

def run(data_dir,once=False,idle_seconds=180,stop_event=None,parent_pid=None):
 store=collector.Store(data_dir);init(store)
 try:
  with collector.WatchLock(store.root/'plugin-worker.lock'):
   collector.say('Plugin worker started. No packet capture.');last_seen=time.monotonic();last_cleanup=0;last_ui=0
   while True:
    if stop_event is not None and stop_event.is_set():collector.say('Game process ended; pending requests retained for next launch.');break
    state=heartbeat(store)
    if state:last_seen=time.monotonic()
    def report(phase=None,rid=None,job_owner=None):
     try:return collection_status.publish(store,state,phase,rid,job_owner)
     except (OSError,sqlite3.Error):return False
    consume_inbox(store);report();active=process_one(store,status=report)
    if not active:
     try:active=backfill_card_cache(store,state,status=report)
     except (OSError,ValueError,IndexError,struct.error):collector.say('Card cache backfill skipped an unreadable replay.')
    report()
    if active:last_seen=time.monotonic()
    if time.monotonic()-last_ui>3:
     overlay.publish(store,state);last_ui=time.monotonic()
    if time.monotonic()-last_cleanup>60:
     prefs=personal.settings()
     if prefs['cache_enabled']:personal.cleanup(store.db,store.root,int(prefs['cache_days']))
     status={'heartbeat':int(time.time()),'game_pid':parent_pid,'owner_id':state.get('owner_id',0) if state else 0,'requests':dict(store.db.execute('SELECT status,COUNT(*) FROM plugin_requests GROUP BY status'))}
     target=store.root/'plugin-worker-status.json';tmp=target.with_suffix('.part');tmp.write_text(json.dumps(status,indent=2),encoding='utf8');tmp.replace(target);last_cleanup=time.monotonic()
    if once:break
    pending=store.db.execute("SELECT COUNT(*) FROM plugin_requests WHERE status='pending'").fetchone()[0]
    if not pending and time.monotonic()-last_seen>idle_seconds:collector.say('Game heartbeat ended; worker exiting.');break
    if stop_event is None:time.sleep(1)
    else:stop_event.wait(1)
 except RuntimeError as error:
  if 'already running' not in str(error):raise
 finally:store.close()

def main():
 ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--data-dir',type=pathlib.Path,default=paths.data_dir());ap.add_argument('--once',action='store_true');ap.add_argument('--idle-seconds',type=int,default=180);ap.add_argument('--parent-pid',type=int,default=os.getppid());args=ap.parse_args();paths.set_data_dir(args.data_dir);args.data_dir.mkdir(parents=True,exist_ok=True)
 with (args.data_dir/'plugin-worker.log').open('a',encoding='utf8',buffering=1) as log,contextlib.redirect_stdout(log),contextlib.redirect_stderr(log):
  lifetime=None
  try:
   lifetime=ParentLifetime(args.parent_pid).start()
   run(args.data_dir,args.once,args.idle_seconds,lifetime.stopping,args.parent_pid)
  except Exception as error:collector.say('WORKER STOPPED: '+type(error).__name__);raise
  finally:
   if lifetime is not None:lifetime.close()
if __name__=='__main__':main()
