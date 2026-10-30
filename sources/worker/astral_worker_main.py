"""Standalone plugin worker and offline diagnostics entry point."""
import argparse,json,pathlib,sys,os
VERSION='0.8.3'

def self_test(base=None):
 import tempfile,struct
 import astral_paths as paths
 context=tempfile.TemporaryDirectory(prefix='astral-selftest-',dir=base)
 with context as folder:
  root=pathlib.Path(folder);paths.set_data_dir(root)
  import astral_plugin_worker as worker,astral_worker_storage as storage,astral_personal_data as personal,astral_replay as replay,astral_overlay_data as overlay
  def var(n):
   out=b''
   while n>127:out+=bytes([(n&127)|128]);n>>=7
   return out+bytes([n])
  def value(k,n):return var(k*8)+var(n)
  def blob(k,b):return var(k*8+2)+var(len(b))+b
  def frame(op,b):return struct.pack('>HI',op,len(b))+b
  owner=900001;room=1234;started=1800000000;rid=str(started*1000000+room)
  hero=value(1,owner)+value(2,127)+blob(8,value(1,4)+value(2,10003))+blob(8,value(1,5)+value(2,21001))
  player=value(1,owner)+blob(2,b'Fixture')+blob(10,hero)
  room_data=value(1,room)+value(5,82008)+blob(9,player)+value(30,started)+value(36,4)
  hp=value(1,900002)+blob(3,value(1,900002)+value(3,10)+value(4,7)+value(8,2)+value(9,owner))
  data=frame(1003,blob(1,room_data))+frame(5028,value(1,owner)+blob(2,struct.pack('<2I',1,2)))+frame(5038,value(1,owner)+value(2,6))+frame(5212,value(1,owner)+value(2,50001))+frame(5036,value(1,owner)+value(2,4))+frame(5056,value(1,owner)+value(2,21001))+frame(1040,blob(4,hp))+frame(1016,value(1,room)+value(3,started+90)+blob(11,rid.encode())+blob(12,b'fixture')+value(13,4)+blob(7,value(1,owner)+blob(2,value(3,3)+value(14,2)))+blob(14,value(1,owner)+value(2,1)))
  store=storage.Store(root,owner)
  try:
   worker.init(store);worker.accept(store,{'schema':1,'source':'astral-personal-mod','owner_id':owner,'room_id':room,'start_time':started,'replay_id':rid,'reason':'history'})
   assert worker.process_one(store,lambda _:data)
   summary=replay.parse_replay(data,rid);totals=personal.game_totals(store.db,rid,owner)
   assert totals['movement_distance']==2 and totals['damage_dealt']==3
   snap=overlay.make_snapshot(store.db,owner,tab=3);assert snap['total_rows']==1 and snap['details'][0]['participants'][0]['own']
   from astral_combat_dice import aggregate
   assert aggregate(store.db,owner)['attack']['average']=='6.00'
   assert not any(name in sys.modules for name in ('astral_account_query','astral_npcap','astral_collector'))
  finally:store.close()
 return {'ok':True,'version':VERSION,'frozen':bool(getattr(sys,'frozen',False)),'checks':['sqlite','replay_analysis','card_chip_dice','worker_queue','overlay','no_capture_or_login_modules']}

def main():
 if '--version' in sys.argv:print(VERSION);return
 if '--self-test' in sys.argv:
  ap=argparse.ArgumentParser();ap.add_argument('--self-test',action='store_true');ap.add_argument('--data-dir',type=pathlib.Path);args=ap.parse_args()
  if args.data_dir:args.data_dir.mkdir(parents=True,exist_ok=True)
  print(json.dumps(self_test(args.data_dir),ensure_ascii=False));return
 if '--import-replay' in sys.argv:
  import astral_paths as paths
  ap=argparse.ArgumentParser();ap.add_argument('--import-replay',required=True,type=pathlib.Path);ap.add_argument('--data-dir',type=pathlib.Path,default=paths.data_dir());args=ap.parse_args();paths.set_data_dir(args.data_dir)
  import astral_worker_storage as storage,astral_personal_data as personal,astral_replay as replay
  owner=personal.settings()['player_id']
  if type(owner) is not int or owner<=0:raise ValueError('Launch the game and log in once before importing replays.')
  if args.import_replay.stat().st_size>replay.MAX_REPLAY_BYTES:raise ValueError('Replay file is too large.')
  data=args.import_replay.read_bytes();summary=replay.parse_replay(data)
  if owner not in [p['player_id'] for p in summary['participants']]:raise ValueError('The current account did not participate in this replay.')
  store=storage.Store(args.data_dir,owner)
  try:
   store.enqueue(summary['replay_id']);store.save(data,summary,True)
   with store.db:store.db.execute('UPDATE personal_cache SET managed=0 WHERE replay_id=?',(summary['replay_id'],))
  finally:store.close()
  print(json.dumps({'ok':True,'replay_id':summary['replay_id']}));return
 from astral_plugin_worker import main as worker_main
 worker_main()

if __name__=='__main__':main()
