import pathlib,sys,tempfile,json,time
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[1]/'sources/worker'))
import astral_paths as paths
import astral_plugin_worker as worker
import astral_collection_status as status

def test_actions():
 with tempfile.TemporaryDirectory() as directory:
  root=pathlib.Path(directory);paths.set_data_dir(root)
  store=worker.collector.Store(root,7);worker.init(store)
  state={'owner_id':7,'owner_verified':True}
  request={'schema':1,'source':'astral-personal-mod','owner_id':7,'room_id':42,'start_time':1770000000,'replay_id':'1770000000000042','reason':'left_room'}
  worker.accept(store,request)
  with store.db:store.db.execute("UPDATE plugin_requests SET status='failed',attempts=5,error='HTTP 404'")
  def action(kind,owner=7):
   (root/'plugin-retry-request.json').write_text(json.dumps({'schema':1,'owner_id':owner,'replay_id':request['replay_id'],'requested_at':int(time.time()),'action':kind}))
   status.consume_retry(store,state)
  action('exclude',8)
  assert store.db.execute('SELECT status FROM plugin_requests').fetchone()[0]=='failed'
  action('retry')
  assert store.db.execute('SELECT status,attempts,next_try FROM plugin_requests').fetchone()==('pending',0,0)
  with store.db:store.db.execute("UPDATE plugin_requests SET status='failed'")
  action('exclude')
  for reason in ('finish','history'):
   worker.accept(store,{**request,'reason':reason})
  action('retry')
  assert store.db.execute('SELECT status FROM plugin_requests').fetchone()[0]=='excluded'
  assert not worker.process_one(store)
  status.publish(store,state)
  snapshot=json.loads((root/'plugin-collection-status.json').read_text(encoding='utf8'))
  assert snapshot['failed']==0 and snapshot['failures']==[]
  store.db.close()

if __name__=='__main__':
 test_actions()
 print('PASS: account guard, manual retry, exclusion persistence and status removal')
