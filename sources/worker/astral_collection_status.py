"""Small own-account collection status, separate from statistics snapshots."""
import json,time,datetime

def failure_reason(error):
 if error=='HTTP 404':return '서버에서 리플레이를 찾지 못했습니다.'
 if error and error.startswith('HTTP '):return '리플레이 서버 요청에 실패했습니다. ('+error+')'
 if error in ('TimeoutError','URLError','ConnectionError'):return '서버에 연결하지 못했습니다.'
 return '리플레이 다운로드 또는 분석에 실패했습니다.'

def consume_retry(store,state):
 path=store.root/'plugin-retry-request.json'
 if not path.exists():return
 try:
  if path.stat().st_size>4096:return
  obj=json.loads(path.read_text(encoding='utf-8-sig'))
  owner=state.get('owner_id') if state and state.get('owner_verified') is True else None
  if not owner or obj.get('schema')!=1 or type(obj.get('owner_id')) is not int or obj['owner_id']!=owner:return
  if type(obj.get('requested_at')) is not int or not -5<=time.time()-obj['requested_at']<=120:return
  rid=obj.get('replay_id')
  if not isinstance(rid,str) or not rid.isdigit() or len(rid)>20:return
  action=obj.get('action','retry')
  if action not in ('retry','exclude'):return
  with store.db:
   if action=='exclude':
    store.db.execute("UPDATE plugin_requests SET status='excluded',next_try=0 WHERE owner_id=? AND replay_id=? AND status='failed'",(owner,rid))
   else:
    store.db.execute("UPDATE plugin_requests SET status='pending',attempts=0,next_try=0 WHERE owner_id=? AND replay_id=? AND status='failed'",(owner,rid))
 except (OSError,ValueError,TypeError):pass
 finally:
  path.unlink(missing_ok=True)

def publish(store,state,phase=None,replay_id=None,job_owner=None,progress=None):
 if not state or state.get('owner_verified') is not True:return False
 owner=state.get('owner_id')
 if type(owner) is not int or owner<=0 or (job_owner is not None and owner!=job_owner):return False
 counts=dict(store.db.execute('SELECT status,COUNT(*) FROM plugin_requests WHERE owner_id=? GROUP BY status',(owner,)))
 pending=counts.get('pending',0);failed=counts.get('failed',0)
 retry=store.db.execute("SELECT COUNT(*) FROM plugin_requests WHERE owner_id=? AND status='pending' AND attempts>0 AND next_try>?",(owner,time.time())).fetchone()[0]
 indexed=store.db.execute('SELECT COUNT(*) FROM personal_matches WHERE owner_id=?',(owner,)).fetchone()[0]
 labels={'downloading':'리플레이 받는 중','analyzing':'경기 분석 중','backfill':'기존 경기 분석 중','snapshot':'화면 통계 집계 중'}
 if phase in labels:label=labels[phase];kind='working'
 elif (store.root/'plugin-disabled.flag').exists():label='새 경기 수집 중지';kind='paused'
 elif pending:label=('재시도 대기' if retry==pending else '수집 대기')+' · '+str(pending)+'건';kind='retry' if retry==pending else 'working'
 elif failed:label='확인 필요 · '+str(failed)+'건';kind='attention'
 elif state.get('history_status')=='requesting':label='최근 경기 확인 중';kind='working'
 elif state.get('history_status')=='waiting_for_lobby':label='최근 경기 확인 대기';kind='waiting'
 elif state.get('is_playing',state.get('room_state')==25):label='플레이 중 · 종료 후 수집';kind='waiting'
 elif indexed:label='수집 완료';kind='ready'
 else:label='새 경기 수집 대기';kind='waiting'
 value={'schema':1,'owner_id':owner,'heartbeat':int(time.time()),'kind':kind,'label':label,'pending':pending,'failed':failed,'indexed':indexed}
 value['failures']=[{'replay_id':rid,'date':datetime.datetime.fromtimestamp(start).strftime('%Y-%m-%d %H:%M'), 'reason':failure_reason(error),'pending':status=='pending'} for rid,start,error,status in store.db.execute("SELECT replay_id,start_time,error,status FROM plugin_requests WHERE owner_id=? AND (status='failed' OR (status='pending' AND error IS NOT NULL)) ORDER BY start_time DESC",(owner,))]
 if progress is not None:
  done,total=progress
  value['label']+=f' · {done}/{total}경기 처리'
 if replay_id is not None:value['replay_id']=replay_id
 target=store.root/'plugin-collection-status.json';tmp=target.with_suffix('.part');tmp.write_text(json.dumps(value,ensure_ascii=False),encoding='utf8');tmp.replace(target);return True
