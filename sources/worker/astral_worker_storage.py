"""Replay storage and CDN download for the plugin worker. No capture or login API."""
import datetime,json,pathlib,sqlite3,time,urllib.request
import astral_replay as replay
import astral_personal_data as personal
ROOT=pathlib.Path(__file__).resolve().parent
CDN='https://sereplayjp.feimogames.com/prod/'
def say(message):print('['+datetime.datetime.now().strftime('%H:%M:%S')+'] '+message,flush=True)
class Store:
 def __init__(self,root,owner_id=None):
  self.owner_id=owner_id or personal.settings()['player_id']
  self.root=pathlib.Path(root);self.root.mkdir(parents=True,exist_ok=True)
  for name in ('replays','summaries'):(self.root/name).mkdir(exist_ok=True)
  self.db=sqlite3.connect(self.root/'collector.sqlite3');self.db.execute('PRAGMA journal_mode=WAL')
  self.db.executescript('''
  CREATE TABLE IF NOT EXISTS jobs(replay_id TEXT PRIMARY KEY,status TEXT NOT NULL,attempts INTEGER NOT NULL DEFAULT 0,next_try REAL NOT NULL DEFAULT 0,error TEXT,detected_at REAL NOT NULL);
  CREATE TABLE IF NOT EXISTS matches(replay_id TEXT PRIMARY KEY,finished_at INTEGER,map_type INTEGER,summary TEXT NOT NULL,imported_at REAL NOT NULL);
  ''');self.db.commit();personal.init(self.db)
 def enqueue(self,rid):
  if not replay.valid_id(rid):raise ValueError('Invalid replay ID')
  cur=self.db.execute("INSERT OR IGNORE INTO jobs(replay_id,status,detected_at) VALUES (?,'pending',?)",(rid,time.time()));self.db.commit();return cur.rowcount>0
 def due(self):return self.db.execute("SELECT replay_id,attempts FROM jobs WHERE status='pending' AND next_try<=? ORDER BY detected_at LIMIT 1",(time.time(),)).fetchone()
 def failed(self,rid,attempt,error):
  delay=(5,15,60,180,600)[min(attempt-1,4)];status='failed' if attempt>=5 else 'pending'
  self.db.execute('UPDATE jobs SET attempts=?,status=?,next_try=?,error=? WHERE replay_id=?',(attempt,status,time.time()+delay,error,rid));self.db.commit()
  say('Replay '+rid+': '+error+(' (retry in '+str(delay)+'s)' if status=='pending' else ' (saved as failed; use --retry-failed)'))
 def save(self,data,summary,keep):
  rid=summary['replay_id'];text=json.dumps(summary,ensure_ascii=False,indent=2)
  target=self.root/'summaries'/(rid+'.json');tmp=target.with_suffix('.json.part');tmp.write_text(text,encoding='utf8');tmp.replace(target)
  if keep:
   target=self.root/'replays'/rid;tmp=target.with_suffix('.part');tmp.write_bytes(data);tmp.replace(target)
  personal.index(self.db,data,summary,self.owner_id,managed=True)
  with self.db:
   self.db.execute('INSERT OR IGNORE INTO matches VALUES (?,?,?,?,?)',(rid,summary['finish_time'],summary['map_type'],text,time.time()))
   self.db.execute("UPDATE jobs SET status='complete',error=NULL WHERE replay_id=?",(rid,))
 def status(self):return {'matches':self.db.execute('SELECT COUNT(*) FROM matches').fetchone()[0],'jobs':dict(self.db.execute('SELECT status,COUNT(*) FROM jobs GROUP BY status')),'data_directory':str(self.root.resolve())}
 def close(self):self.db.close()
def download(rid):
 if not replay.valid_id(rid):raise ValueError('Invalid replay ID')
 request=urllib.request.Request(CDN+rid,headers={'User-Agent':'UnityPlayer/2022.3.62f3 (UnityWebRequest/1.0)','X-Unity-Version':'2022.3.62f3'})
 with urllib.request.urlopen(request,timeout=20) as response:data=response.read(replay.MAX_REPLAY_BYTES+1)
 if len(data)>replay.MAX_REPLAY_BYTES:raise ValueError('Replay exceeds 64 MiB limit')
 return data
class WatchLock:
 def __init__(self,path):self.path=path;self.file=None
 def __enter__(self):
  import msvcrt
  self.file=self.path.open('a+b');self.file.seek(0);self.file.write(b'0');self.file.flush();self.file.seek(0)
  try:msvcrt.locking(self.file.fileno(),msvcrt.LK_NBLCK,1)
  except OSError:self.file.close();raise RuntimeError('Another collector is already running') from None
  return self
 def __exit__(self,*args):self.file.close()
