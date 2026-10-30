"""Small own-account collection status, separate from statistics snapshots."""
import json,time

def publish(store,state,phase=None,replay_id=None,job_owner=None):
 if not state or state.get('owner_verified') is not True:return False
 owner=state.get('owner_id')
 if type(owner) is not int or owner<=0 or (job_owner is not None and owner!=job_owner):return False
 counts=dict(store.db.execute('SELECT status,COUNT(*) FROM plugin_requests WHERE owner_id=? GROUP BY status',(owner,)))
 pending=counts.get('pending',0);failed=counts.get('failed',0)
 retry=store.db.execute("SELECT COUNT(*) FROM plugin_requests WHERE owner_id=? AND status='pending' AND attempts>0 AND next_try>?",(owner,time.time())).fetchone()[0]
 indexed=store.db.execute('SELECT COUNT(*) FROM personal_matches WHERE owner_id=?',(owner,)).fetchone()[0]
 labels={'downloading':'리플레이 받는 중','analyzing':'경기 분석 중','backfill':'기존 경기 분석 중'}
 if phase in labels:label=labels[phase];kind='working'
 elif (store.root/'plugin-disabled.flag').exists():label='새 경기 수집 중지';kind='paused'
 elif pending:label=('재시도 대기' if retry==pending else '수집 대기')+' · '+str(pending)+'건';kind='retry' if retry==pending else 'working'
 elif failed:label='확인 필요 · '+str(failed)+'건';kind='attention'
 elif state.get('history_status')=='requesting':label='최근 경기 확인 중';kind='working'
 elif state.get('history_status')=='waiting_for_lobby':label='최근 경기 확인 대기';kind='waiting'
 elif state.get('room_state')==25:label='플레이 중 · 종료 후 수집';kind='waiting'
 elif indexed:label='수집 완료';kind='ready'
 else:label='새 경기 수집 대기';kind='waiting'
 value={'schema':1,'owner_id':owner,'heartbeat':int(time.time()),'kind':kind,'label':label,'pending':pending,'failed':failed,'indexed':indexed}
 if replay_id is not None:value['replay_id']=replay_id
 target=store.root/'plugin-collection-status.json';tmp=target.with_suffix('.part');tmp.write_text(json.dumps(value,ensure_ascii=False),encoding='utf8');tmp.replace(target);return True
