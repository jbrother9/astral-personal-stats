"""Personal-only replay analysis and SQLite index. Does not store other players' profiles."""
import json,pathlib,struct,time
import astral_replay as replay
import astral_card_stats as cards
import astral_chip_stats as chips
import astral_combat_dice as dice
import astral_paths as paths
VERSION=7
ROOT=pathlib.Path(__file__).resolve().parent
DEFAULTS={'player_id':0,'cache_days':7,'cache_enabled':True}
def settings():
 try:return {**DEFAULTS,**json.loads(paths.settings_file().read_text(encoding='utf8'))}
 except (OSError,ValueError):return dict(DEFAULTS)
def save_settings(value):
 p=paths.settings_file();p.parent.mkdir(parents=True,exist_ok=True);tmp=p.with_suffix('.json.part');tmp.write_text(json.dumps(value,indent=2),encoding='utf8');tmp.replace(p)
def init(db):
 db.executescript('''
 CREATE TABLE IF NOT EXISTS personal_matches(
 replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,finished_at INTEGER,started_at INTEGER,
 hero_id INTEGER,map_id INTEGER,map_type INTEGER,rank INTEGER,duration INTEGER,
 turns INTEGER,deaths INTEGER,npc_last_hits INTEGER,effect_cards INTEGER,battle_cards INTEGER,skills INTEGER,
 analysis_version INTEGER NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_match_context(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,difficulty INTEGER,map_difficulty_id INTEGER,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_events(
 replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,frame INTEGER NOT NULL,ordinal INTEGER NOT NULL,
 round INTEGER,own_turn INTEGER,kind TEXT NOT NULL,detail TEXT NOT NULL,
 PRIMARY KEY(replay_id,owner_id,frame,ordinal));
 CREATE TABLE IF NOT EXISTS personal_game_totals(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,metrics TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_cache(replay_id TEXT PRIMARY KEY,managed INTEGER NOT NULL DEFAULT 0,pinned INTEGER NOT NULL DEFAULT 0,acquired_at REAL NOT NULL);
 CREATE TABLE IF NOT EXISTS personal_match_insights(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,version INTEGER NOT NULL,summary TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_appearances(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,participants TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE TABLE IF NOT EXISTS personal_card_stats(replay_id TEXT NOT NULL,owner_id INTEGER NOT NULL,version INTEGER NOT NULL,summary TEXT NOT NULL,PRIMARY KEY(replay_id,owner_id));
 CREATE INDEX IF NOT EXISTS personal_matches_owner_mode ON personal_matches(owner_id,map_type);
 CREATE INDEX IF NOT EXISTS personal_events_owner_kind ON personal_events(owner_id,kind);
 ''');db.commit()
def signed32(x):return x-2**32 if x>=2**31 else x

def fixed64_target_ids(values):
 # UseEffectCardS2C uses FieldCodec.ForSFixed64, including its packed representation.
 result=[]
 for value in values:
  if type(value) is int:
   result.append(value-2**64 if value>=2**63 else value)
  elif isinstance(value,bytes):
   if len(value)%8:raise ValueError('Invalid packed sfixed64 target IDs')
   result.extend(struct.unpack('<'+'q'*(len(value)//8),value))
  else:raise ValueError('Unsupported target ID representation')
 return list(dict.fromkeys(value for value in result if value>0))


def gold_transfers(data,players,include_skills=True):
 # Explicit declaration + attributable debit/credit. Amounts need not be equal.
 from collections import deque
 window=deque(maxlen=3);result=[];pos=frame=roundno=0
 def change(packet,pid,cause_type,skill=0):
  _,op,raw,_=packet
  if op!=1040:return None
  f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''))
  if replay.first(cause,1)!=cause_type or skill and replay.first(cause,3)!=skill:return None
  values=[]
  for raw_effect in f.get(4,[]):
   effect=replay.protobuf(raw_effect)
   if replay.first(effect,1)!=pid or 2 not in effect:continue
   value=replay.protobuf(replay.first(effect,2,b''))
   if replay.first(value,1)!=pid:return None
   values.append(signed32(replay.first(value,4))-signed32(replay.first(value,3)))
  return values[0] if len(values)==1 else None
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if op==1015:roundno=replay.first(replay.protobuf(raw),1)
  elif op in (1003,1113):
   room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''));roundno=replay.first(room,14,roundno)
  window.append((frame,op,raw,roundno))
  if len(window)!=3:continue
  declaration=window[0];op=declaration[1]
  if op!=5216 and (not include_skills or op!=5056):continue
  f=replay.protobuf(declaration[2]);sender=replay.first(f,1);skill=0
  if op==5216:
   recipient=replay.first(f,3);cause_type=14;source='shop'
   if f.get(2):continue
  else:
   selected=fixed64_target_ids(f.get(3,[]));skill=replay.first(f,9)
   if not replay.first(f,8) or not skill or len(selected)!=1:continue
   recipient=selected[0];cause_type=1;source='skill'
  if sender==recipient or sender not in players or recipient not in players:continue
  debit=change(window[1],sender,cause_type,skill);credit=change(window[1],recipient,cause_type,skill);credit_packet=window[1]
  if credit is None:credit=change(window[2],recipient,cause_type,skill);credit_packet=window[2]
  if debit is None or credit is None or debit>=0 or credit<=0:continue
  result.append({'sender':sender,'recipient':recipient,'amount':-debit,'received':credit,'source':source,'source_skill_id':skill,'round':declaration[3],'debit_frame':window[1][0],'credit_frame':credit_packet[0]})
 return result


def shop_transfers(data,players):
 return gold_transfers(data,players,include_skills=False)


def kill_gold_rewards(data,players):
 # Require a unique nearby NPC death, the same killer/recipient, and the NPC's known wallet.
 wallets={};pending=None;rewards={};pos=frame=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if pending and frame-pending['frame']>15:pending=None
  if op in (1003,1113):
   pending=None;room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''))
   for b in room.get(10,[]):
    person=replay.protobuf(b);hero=replay.protobuf(replay.first(person,10,b''));wallets[replay.first(person,1)]=signed32(replay.first(hero,7))
  elif op==1115:
   f=replay.protobuf(raw);victim,killer=replay.first(f,1),replay.first(f,3);pending=None
   if victim not in players and killer in players and wallets.get(victim,0)>0:
    pending={'frame':frame,'killer':killer,'victim_id':victim,'hero_id':replay.first(f,2),'amount':wallets[victim]}
  elif op in (1015,5056,5036,5216):pending=None
  elif op==1040:
   f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''));changes=[];has_level=False
   for b in f.get(4,[]):
    effect=replay.protobuf(b);has_level|=11 in effect
    if 2 not in effect:continue
    gold=replay.protobuf(replay.first(effect,2,b''));pid=replay.first(effect,1);after=signed32(replay.first(gold,4));before=signed32(replay.first(gold,3))
    if pid not in players:wallets[pid]=after
    if after!=before:changes.append((pid,after-before))
   if pending and (changes or has_level):
    if not has_level and replay.first(cause,1)==0 and changes==[(pending['killer'],pending['amount'])]:rewards[frame]=dict(pending)
    pending=None
 return rewards


def purchase_gold_cards(data,players):
 # Use confirmed closing offers/sold slots, not newly acquired hand cards or inferred prices.
 sessions={};batches=[];awaiting=None;pos=frame=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if awaiting is not None:
   batch=awaiting;awaiting=None
   if op==1040:
    f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''));cost=[]
    if replay.first(cause,1)==14:
     for b in f.get(4,[]):
      e=replay.protobuf(b)
      if replay.first(e,1)!=batch['player_id'] or 2 not in e:continue
      gold=replay.protobuf(replay.first(e,2,b''))
      if replay.first(gold,1)==batch['player_id']:cost.append(signed32(replay.first(gold,4))-signed32(replay.first(gold,3)))
     if len(cost)==1 and cost[0]<0:batch['debit_frame']=frame
  if op in (1015,1003,1113):sessions.clear()
  if op!=5216:continue
  f=replay.protobuf(raw);pid=replay.first(f,1)
  if pid not in players:continue
  try:choices=cards.fixed32_values(f.get(2,[]))
  except ValueError:sessions.pop(pid,None);continue
  session=sessions.setdefault(pid,[])
  if choices:
   batch={'player_id':pid,'indices':choices};session.append(batch);batches.append(batch);awaiting=batch
  if not replay.first(f,6):continue
  try:
   offers=cards.fixed32_values(f.get(4,[]));sold=[]
   for value in f.get(5,[]):sold.extend([value] if isinstance(value,int) else value)
   chosen=[idx for batch in session for idx in batch['indices']]
   valid=bool(chosen) and len(offers)==len(sold) and all(v in (0,1) for v in sold) and len(chosen)==len(set(chosen)) and set(chosen)=={i for i,v in enumerate(sold) if v} and all(0<=i<len(offers) and offers[i]>0 for i in chosen)
   if valid:
    for batch in session:batch['card_ids']=[offers[i] for i in batch['indices']]
  except ValueError:pass
  sessions.pop(pid,None)
 return {b['debit_frame']:{'player_id':b['player_id'],'card_ids':b['card_ids']} for b in batches if 'debit_frame' in b and 'card_ids' in b}


def buff_effect_sources(data):
 # Resolve instance IDs in the holder's container at the effect frame, never from future records.
 active={};result={};pos=frame=0
 def read_buff(raw,previous=None):
  b=replay.protobuf(raw);map_id=replay.first(b,1)
  if isinstance(replay.first(b,2),bytes):b=replay.protobuf(replay.first(b,2,b''))
  uid=replay.first(b,1) or map_id;definition=replay.first(b,2)
  if type(uid) is not int or type(definition) is not int or uid<=0:return None
  old=previous.get(uid,{}) if previous else {};source=replay.protobuf(replay.first(b,50,b'')) if 50 in b else None
  return {'instance_id':uid,'definition_id':definition or old.get('definition_id',0),'source_type':replay.first(source,1) if source is not None else old.get('source_type',0),'source_id':replay.first(source,2) if source is not None else old.get('source_id',0)}
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n;frame+=1
  if op in (1003,1113):
   active.clear();room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''))
   for b in room.get(9,[])+room.get(10,[]):
    person=replay.protobuf(b);holder=replay.first(person,1);hero=replay.protobuf(replay.first(person,10,b''));container={}
    for raw_buff in hero.get(4,[]):
     buff=read_buff(raw_buff)
     if buff and buff['definition_id']>0:container[buff['instance_id']]=buff
    active[holder]=container
  if op!=1040:continue
  f=replay.protobuf(raw);cause=replay.protobuf(replay.first(f,2,b''));holder=replay.first(f,1);uid=replay.first(cause,3)
  before=active.get(holder,{}).get(uid) if replay.first(cause,1)==7 else None
  for b in f.get(4,[]):
   effect=replay.protobuf(b)
   if 6 not in effect:continue
   change=replay.protobuf(replay.first(effect,6,b''));owner=replay.first(change,1) or replay.first(effect,1);operation=replay.first(change,3);container=active.setdefault(owner,{})
   if operation==0:
    replacement={}
    for raw_buff in change.get(4,[]):
     buff=read_buff(raw_buff)
     if buff and buff['definition_id']>0:replacement[buff['instance_id']]=buff
    active[owner]=replacement
   elif operation in (1,2,3) and 2 in change:
    buff=read_buff(replay.first(change,2,b''),container)
    if not buff:continue
    if operation==2:container.pop(buff['instance_id'],None)
    elif buff['definition_id']>0:container[buff['instance_id']]=buff
  if replay.first(cause,1)==7:
   buff=before or active.get(holder,{}).get(uid)
   if buff:result[frame]=dict(buff)
 return result


def analyze(data,summary,owner,transfers=None,kill_rewards=None,purchases=None,buff_refs=None):
 person=next((p for p in summary['participants'] if p['player_id']==owner),None)
 if person is None:raise ValueError('Own player not in replay')
 if buff_refs is None:buff_refs=buff_effect_sources(data)
 if purchases is None:purchases=purchase_gold_cards(data,{p['player_id'] for p in summary['participants']})
 if kill_rewards is None:kill_rewards=kill_gold_rewards(data,{p['player_id'] for p in summary['participants']})
 if transfers is None:transfers=gold_transfers(data,{p['player_id'] for p in summary['participants']})
 transfer_frames={t['debit_frame']:{'direction':'sent','counterpart':t['recipient'],'amount':t['amount'],'received':t.get('received',t['amount']),'source':t.get('source','shop')} for t in transfers if t['sender']==owner}
 transfer_frames.update({t['credit_frame']:{'direction':'received','counterpart':t['sender'],'amount':t['amount'],'received':t.get('received',t['amount']),'source':t.get('source','shop')} for t in transfers if t['recipient']==owner})
 events=[];stats=dict(turns=0,deaths=0,npc_last_hits=0,effect_cards=0,battle_cards=0,skills=0)
 pos=frame=roundno=0;actor=0;level=None;star_level=None;known_cards={};battle_id=0;opponent=0;battle_bonuses={};lethal_hits={}
 def remember_cards(raw_cards):
  for raw in raw_cards:
   card=replay.protobuf(raw);uid,definition=replay.first(card,1),replay.first(card,2)
   if uid>0 and definition>0:known_cards[uid]=definition
 def add(kind,detail):
  events.append((summary['replay_id'],owner,frame,len(events),roundno,int(actor==owner),kind,json.dumps(detail,ensure_ascii=False)))
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;body=data[pos:pos+n];pos+=n;frame+=1
  if op not in (1003,1113,1015,5056,5036,1115,1040,5022,5028,5212,1109,1007):continue
  f=replay.protobuf(body);pid=replay.first(f,1)
  if op in (1015,1003,1113,5056,5036):lethal_hits.clear()
  if op==1007:
   battle=replay.protobuf(replay.first(f,1,b''));battle_id=replay.first(battle,1);opponent=0
   attacker=replay.protobuf(replay.first(battle,2,b''));defender=replay.protobuf(replay.first(battle,3,b''))
   ap,dp=replay.first(attacker,1),replay.first(defender,1)
   role=attacker if ap==owner else defender if dp==owner else None
   if role is not None and ap!=dp:
    opponent=dp if ap==owner else ap
    bonuses=[]
    for raw_bonus in role.get(23,[]):
     bonus=replay.protobuf(raw_bonus);bonuses.append((replay.first(bonus,1),signed32(replay.first(bonus,2))))
    if battle_id and bonuses:battle_bonuses[battle_id]=bonuses
   if replay.first(battle,5):opponent=0
  elif op==1015:
   roundno=replay.first(f,1);actor=replay.first(f,5)
   if actor==owner:stats['turns']+=1;add('턴 시작',{'round':roundno})
  elif op in (1003,1113):
   room=replay.protobuf(replay.first(f,1 if op==1003 else 2,b''))
   if replay.first(room,14)>0:roundno=replay.first(room,14)
   for raw in room.get(9,[]):
    p=replay.protobuf(raw)
    if replay.first(p,1)!=owner:continue
    hero=replay.protobuf(replay.first(p,10,b''));remember_cards(hero.get(8,[]));current=replay.first(hero,33);star_level=current
    if current!=level:
     add('성장 상태',{'previous':level,'current':current,'snapshot_round':replay.first(room,14),'note':'게임 내부 성장 값; 스냅샷에서 확인한 시점'});level=current
  elif op==1109 and replay.first(f,2)==owner:remember_cards(f.get(1,[]))
  elif op==5056 and pid==owner:
   if replay.first(f,8):stats['skills']+=1;add('스킬',{'skill_id':replay.first(f,9),'targets':fixed64_target_ids(f.get(3,[]))})
   if replay.first(f,2) and not replay.first(f,8):stats['effect_cards']+=1;add('효과 카드',{'card_id':replay.first(f,2),'definition_id':replay.first(f,2),'targets':fixed64_target_ids(f.get(3,[]))})
  elif op==5036 and pid==owner and replay.first(f,2) and not replay.first(f,3):
   stats['battle_cards']+=1;add('전투 카드',{'card_id':replay.first(f,2),'definition_id':known_cards.get(replay.first(f,2)),'battle_id':battle_id,'opponent_id':opponent})
  elif op==1115:
   hit=lethal_hits.pop(pid,None);killer=replay.first(f,3);health={}
   if hit and frame-hit['frame']<=5 and hit['source'] in (0,killer):health={key:hit[key] for key in ('hp_before','hp_after','damage_value')}
   if pid==owner:stats['deaths']+=1;add('사망',{'killer_id':killer,**health})
   elif killer==owner and pid not in [p['player_id'] for p in summary['participants']]:stats['npc_last_hits']+=1;add('NPC 마지막 공격',{'target_hero_id':replay.first(f,2),**health})
  elif op==5022 and replay.first(f,3)==owner:add('주사위',{'move_point':replay.first(f,2)})
  elif op==5028 and pid==owner:add('이동',{'distance':len(cards.fixed32_values(f.get(2,[])))})
  elif op==5212 and pid==owner and replay.first(f,2):add('칩 선택 응답',{'relic_id':replay.first(f,2),'is_reroll':bool(replay.first(f,3))})
  elif op==1040:
   cause=replay.protobuf(replay.first(f,2,b''));origin={'cause_type':replay.first(cause,1),'cause_id':replay.first(cause,3),'buff':buff_refs.get(frame)}
   star_up=False
   for item in f.get(4,[]):
    change=replay.protobuf(item)
    if replay.first(change,1)!=owner or 11 not in change:continue
    lv=replay.protobuf(replay.first(change,11,b''));new_level=replay.first(lv,2)
    if replay.first(lv,1)==owner and new_level>0 and (star_level is None or new_level>star_level):star_up=True
    star_level=new_level
   for raw in f.get(4,[]):
    effect=replay.protobuf(raw)
    target=replay.first(effect,1)
    if 3 in effect and isinstance(effect[3][0],bytes):
     hp=replay.protobuf(effect[3][0]);source=replay.first(hp,9);damage_type=replay.first(hp,8)
     hp_before=signed32(replay.first(hp,3));hp_after=signed32(replay.first(hp,4));hit_change=signed32(replay.first(hp,2))
     lethal_hits.pop(target,None)
     if hp_before>0 and hp_after<=0:lethal_hits[target]={'frame':frame,'source':source,'hp_before':hp_before,'hp_after':hp_after,'damage_value':-hit_change if hit_change<0 else max(0,hp_before-hp_after)}
     if source==owner and target and target!=owner and 1<=damage_type<=11:
      before=signed32(replay.first(hp,3));after=signed32(replay.first(hp,4));amount=max(0,max(0,before)-max(0,after))
      requested=signed32(replay.first(hp,2));health={'hp_before':before,'hp_after':after}
      if amount or requested<0:add('가한 피해',{'amount':amount,'damage_value':-requested if requested<0 else amount,'target':target,'damage_type':damage_type,**health,**origin})
      if damage_type==7 and requested<0:add('전투 피해',{'amount':-requested,'target':target,'damage_type':7,**health,**origin})
    if target!=owner:continue
    for field,kind in ((2,'골드 변화'),(3,'HP 변화')):
     if field not in effect or not isinstance(effect[field][0],bytes):continue
     v=replay.protobuf(effect[field][0]);delta=signed32(replay.first(v,4))-signed32(replay.first(v,3));star_change=('reward' if delta>0 else 'cost' if field==2 and delta<0 else None) if star_up else None;add(kind,{'purchase_card_ids':purchases.get(frame,{}).get('card_ids') if field==2 and purchases.get(frame,{}).get('player_id')==owner else None,'kill_reward':kill_rewards.get(frame) if field==2 and kill_rewards.get(frame,{}).get('killer')==owner else None,'transfer':transfer_frames.get(frame) if field==2 else None,'star_change':star_change,'before':signed32(replay.first(v,3)),'after':signed32(replay.first(v,4)),'requested_change':signed32(replay.first(v,2)),'source':replay.first(v,9) if field==3 else 0,'damage_type':replay.first(v,8) if field==3 else None,**origin})
   transfer=transfer_frames.get(frame)
   if transfer and transfer['direction']=='sent':add('골드 전달',{'amount':transfer['amount'],'received':transfer.get('received',transfer['amount']),'source':transfer.get('source','shop'),'target':transfer['counterpart']})
 # A repeated definition in one battle is ambiguous; do not assign a bonus by guessing order.
 battle_events={}
 for i,event in enumerate(events):
  if event[6]!='전투 카드':continue
  detail=json.loads(event[7]);key=(detail.get('battle_id'),detail.get('definition_id'));battle_events.setdefault(key,[]).append(i)
 for (battle,definition),indices in battle_events.items():
  values=[v for card,v in battle_bonuses.get(battle,[]) if card==definition]
  if not battle or not definition or len(indices)!=1 or len(values)!=1:continue
  i=indices[0];event=list(events[i]);detail=json.loads(event[7]);detail['card_bonus']=values[0];event[7]=json.dumps(detail,ensure_ascii=False);events[i]=tuple(event)
 settlement=summary.get('settlement',{}).get(str(owner))
 if settlement is not None:
  stats['deaths']=settlement['deaths'];add('최종 결과',settlement)
 record=(summary['replay_id'],owner,summary['finish_time'],summary.get('start_time'),person['hero_id'],summary.get('map_id'),summary.get('map_type'),person.get('rank'),max(0,summary['finish_time']-(summary.get('start_time') or summary['finish_time'])),*[stats[k] for k in ('turns','deaths','npc_last_hits','effect_cards','battle_cards','skills')],VERSION)
 return record,events

def index(db,data,summary,owner,managed=False):
 record,events=analyze(data,summary,owner)
 card_summary=cards.analyze(data,owner)
 with db:
  difficulty=summary.get('difficulty');map_difficulty=summary.get('map_difficulty_id')
  if type(difficulty) is not int or not 0<=difficulty<2**31:difficulty=None
  if type(map_difficulty) is not int or not 0<=map_difficulty<2**31:map_difficulty=None
  db.execute('INSERT OR REPLACE INTO personal_match_context VALUES (?,?,?,?)',(summary['replay_id'],owner,difficulty,map_difficulty))
  cards.save(db,summary['replay_id'],owner,card_summary)
  save_appearances(db,summary,owner)
  save_insights(db,data,summary,owner)
  db.execute('INSERT OR REPLACE INTO personal_matches VALUES ('+','.join('?'*16)+')',record)
  metrics=summarize_events(record,events)
  db.execute('INSERT OR REPLACE INTO personal_game_totals VALUES (?,?,?)',(summary['replay_id'],owner,json.dumps(metrics,ensure_ascii=False)))
  db.execute('INSERT OR IGNORE INTO personal_cache VALUES (?,?,0,?)',(summary['replay_id'],int(managed),time.time()))
 return len(events)

def summarize_events(record,events):
 metrics={key:record[index] for key,index in (('turns',9),('deaths',10),('npc_last_hits',11),('effect_cards',12),('battle_cards',13),('skills',14))}
 metrics.update(movement_distance=0,damage_dealt=0,chip_selection_events=0,dice_events=0,move_events=0,hp_increase=0,hp_decrease=0,gold_increase=0,gold_decrease=0,growth3_round=None,growth_final=None)
 for event in events:
  roundno,kind,detail=event[4],event[6],json.loads(event[7])
  key={'칩 선택 응답':'chip_selection_events','주사위':'dice_events','이동':'move_events'}.get(kind)
  if key:metrics[key]+=1
  if kind=='이동':metrics['movement_distance']+=detail.get('distance',0)
  if kind=='가한 피해':metrics['damage_dealt']+=detail.get('amount',0)
  if kind in ('HP 변화','골드 변화'):
   delta=detail['after']-detail['before'];prefix='hp' if kind=='HP 변화' else 'gold'
   metrics[prefix+('_increase' if delta>=0 else '_decrease')]+=abs(delta)
  if kind=='성장 상태':
   metrics['growth_final']=detail.get('current')
   reached=detail.get('snapshot_round',roundno)
   if detail.get('current')==3 and isinstance(reached,int) and reached>0:metrics['growth3_round']=min(metrics['growth3_round'] or reached,reached)
 if record[15]<4:
  metrics.pop('movement_distance',None);metrics.pop('damage_dealt',None)
 if record[15]>=5:
  settlement=next((json.loads(event[7]) for event in reversed(events) if event[6]=='최종 결과'),None)
  for key in ('damage_dealt','damage_taken','deaths','movement_distance','transferred_gold','max_battle_damage'):metrics[key]=settlement.get(key) if settlement is not None else None
  metrics['settlement_available']=settlement is not None
 return metrics

def normalized_totals(value):
 value=dict(value)
 if not value.get('settlement_available'):
  for key in ('damage_dealt','damage_taken','deaths','movement_distance','transferred_gold','max_battle_damage'):value[key]=None
 return value

def game_totals(db,rid,owner):
 row=db.execute('SELECT metrics FROM personal_game_totals WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 if row:return normalized_totals(json.loads(row[0]))
 record=db.execute('SELECT * FROM personal_matches WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 if record is None:return None
 events=db.execute('SELECT * FROM personal_events WHERE replay_id=? AND owner_id=? ORDER BY frame,ordinal',(rid,owner)).fetchall()
 return normalized_totals(summarize_events(record,events))

def cleanup(db,root,days):
 if not 1<=days<=3650:raise ValueError('Invalid cache days')
 folder=(pathlib.Path(root)/'replays').resolve();cutoff=time.time()-days*86400;removed=0
 for (rid,) in db.execute('SELECT replay_id FROM personal_cache WHERE managed=1 AND pinned=0 AND acquired_at<?',(cutoff,)).fetchall():
  if not replay.valid_id(rid):continue
  p=folder/rid
  if p.is_file() and p.resolve().parent==folder:p.unlink();removed+=1
 return removed

# MapModeType names from the installed runtime. Asymmetric/LuckyStar rules remain unverified.
PVE_MODES=frozenset((4,6,9,10,12))
PVP_MODES=frozenset((1,2,3,5,8))

def difficulty_clause(alias,difficulty):
 """None means all; -2 means unknown. Difficulty filters only include PvE matches."""
 if difficulty is None:return '',[]
 if type(difficulty) is not int or (difficulty!=-2 and not 0<=difficulty<2**31):raise ValueError('Invalid difficulty filter')
 field='(SELECT context.difficulty FROM personal_match_context context WHERE context.replay_id='+alias+'.replay_id AND context.owner_id='+alias+'.owner_id)'
 clause=' AND '+alias+'.map_type IN ('+','.join(str(v) for v in sorted(PVE_MODES))+') AND '+field
 return (clause+' IS NULL',[]) if difficulty==-2 else (clause+'=?',[difficulty])

def mode_family(mode):
 return 'pve' if mode in PVE_MODES else 'pvp' if mode in PVP_MODES else 'other'

def match_outcome(mode,rank):
 if type(rank) is not int or rank<=0:return None
 family=mode_family(mode)
 if family=='pve':return 'win' if rank==1 else 'loss' if rank==2 else None
 if family=='pvp':return 'win' if rank==1 else 'loss'
 return None

def result_label(mode,rank):
 outcome=match_outcome(mode,rank)
 if outcome is None:return '미확인'
 label='승리' if outcome=='win' else '패배'
 return label if mode_family(mode)=='pve' else label+' · '+str(rank)+'위'

def rate_label(rate):return '-' if rate is None else f'{rate*100:.1f}%'

def family_summary(label,row):
 if not row['matches']:return label+' 기록 없음'
 if not row['decided']:return label+' '+str(row['matches'])+'경기 · 결과 미확인'
 value=label+' '+str(row['wins'])+'승 '+str(row['losses'])+'패 · '+rate_label(row['win_rate'])
 if row['unresolved']:value+=' (미확인 '+str(row['unresolved'])+')'
 return value

def statistics(db,owner,mode=None,map_id=None,hero_id=None,difficulty=None):
 """Personal-only aggregation. Separate PvE/PvP win rates; unknown results are excluded."""
 where='owner_id=?';params=[owner]
 if mode is not None:where+=' AND map_type=?';params.append(mode)
 if map_id is not None:where+=' AND map_id=?';params.append(map_id)
 if hero_id is not None:where+=' AND hero_id=?';params.append(hero_id)
 extra,args=difficulty_clause('personal_matches',difficulty);where+=extra;params+=args
 rows=db.execute('SELECT replay_id,hero_id,map_id,map_type,rank,duration,turns,deaths,npc_last_hits,effect_cards,battle_cards,skills,(SELECT difficulty FROM personal_match_context context WHERE context.replay_id=personal_matches.replay_id AND context.owner_id=personal_matches.owner_id) FROM personal_matches WHERE '+where,params).fetchall()
 rows=[tuple(row[:12])+(row[12] if mode_family(row[3])=='pve' else None,) for row in rows]
 growth={}
 selected={r[0] for r in rows}
 for rid,roundno,raw in db.execute("SELECT replay_id,round,detail FROM personal_events WHERE owner_id=? AND kind='성장 상태'",(owner,)):
  if rid not in selected:continue
  detail=json.loads(raw)
  if detail.get('current')==3:
   reached=detail.get('snapshot_round',roundno)
   if isinstance(reached,int) and reached>0:growth[rid]=min(growth.get(rid,reached),reached)
 for rid,raw in db.execute('SELECT replay_id,metrics FROM personal_game_totals WHERE owner_id=?',(owner,)):
  if rid not in selected:continue
  reached=json.loads(raw).get('growth3_round');growth.pop(rid,None)
  if reached is not None:growth[rid]=reached
 metrics_by_id={rid:normalized_totals(json.loads(raw)) for rid,raw in db.execute('SELECT replay_id,metrics FROM personal_game_totals WHERE owner_id=?',(owner,))}
 def movement_damage(items):
  values=[metrics_by_id.get(r[0],{}) for r in items];result={}
  for key in ('movement_distance','damage_dealt','damage_taken','deaths','transferred_gold','max_battle_damage'):
   found=[r[key] for r in values if r.get(key) is not None]
   result[key+'_analyzed']=len(found)
   result[key]=max(found) if key=='max_battle_damage' and found else sum(found)
   result['average_'+key]=sum(found)/len(found) if found else None
  result['movement_damage_analyzed']=sum(r.get('movement_distance') is not None and r.get('damage_dealt') is not None for r in values)
  return result
 def aggregate(items):
  count=len(items);ranked=[r[4] for r in items if r[4] is not None];rounds=[growth[r[0]] for r in items if r[0] in growth]
  outcomes=[match_outcome(r[3],r[4]) for r in items];wins=outcomes.count('win');losses=outcomes.count('loss');decided=wins+losses;families={mode_family(r[3]) for r in items}
  win_rate=wins/decided if decided and len(families)==1 and 'other' not in families else None
  return {'wins':wins,'losses':losses,'decided':decided,'unresolved':count-decided,'win_rate':win_rate,'matches':count,'duration':sum(r[5] or 0 for r in items),'first_places':sum(r[4]==1 for r in items),'average_rank':sum(ranked)/len(ranked) if ranked else None,'average_duration':sum(r[5] or 0 for r in items)/count if count else 0,'deaths':sum(r[7] or 0 for r in items),'average_deaths':sum(r[7] or 0 for r in items)/count if count else 0,'average_npc_last_hits':sum(r[8] or 0 for r in items)/count if count else 0,'average_effect_cards':sum(r[9] or 0 for r in items)/count if count else 0,'average_battle_cards':sum(r[10] or 0 for r in items)/count if count else 0,'average_skills':sum(r[11] or 0 for r in items)/count if count else 0,**movement_damage(items),'growth3_average_round':sum(rounds)/len(rounds) if rounds else None,'growth3_reached_matches':len(rounds)}
 def group(indices):
  groups={}
  for row in rows:groups.setdefault(tuple(row[i] for i in indices),[]).append(row)
  return [{'key':key,**aggregate(items)} for key,items in sorted(groups.items(),key=lambda pair:(-len(pair[1]),str(pair[0])))]
 return {'overall':aggregate(rows),'by_family':{kind:aggregate([r for r in rows if mode_family(r[3])==kind]) for kind in ('pve','pvp','other')},'by_difficulty':group((12,3)),'by_mode':group((3,)),'by_map':group((2,3)),'by_hero':group((1,3))}


def save_appearances(db,summary,owner):
 if owner not in [p['player_id'] for p in summary['participants']]:raise ValueError('Own player not in replay')
 participants=[{'player_id':p['player_id'],'name':p.get('name',''),'hero_id':p.get('equipped_hero_id') or p['hero_id'],'standing_painting_id':p.get('standing_painting_id',0)} for p in summary['participants']]
 db.execute('INSERT OR REPLACE INTO personal_appearances VALUES (?,?,?)',(summary['replay_id'],owner,json.dumps(participants,ensure_ascii=False)))

def appearances(db,rid,owner):
 row=db.execute('SELECT participants FROM personal_appearances WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 return json.loads(row[0]) if row else []


INSIGHTS_VERSION=19

def has_insights(db,rid,owner):
 row=db.execute('SELECT version FROM personal_match_insights WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 return bool(row and row[0]>=INSIGHTS_VERSION)

def match_insights(db,rid,owner):
 row=db.execute('SELECT summary FROM personal_match_insights WHERE replay_id=? AND owner_id=? AND version>=?',(rid,owner,7)).fetchone()
 return json.loads(row[0]) if row else None

def save_insights(db,data,summary,owner):
 if owner not in [p['player_id'] for p in summary['participants']]:raise ValueError('Own player not in replay')
 import collections
 entities={p['player_id']:{'kind':'player','hero_id':p['hero_id']} for p in summary['participants']};ambiguous=set();deaths=[];pos=0
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;raw=data[pos:pos+n];pos+=n
  if op in (1003,1113):
   room=replay.protobuf(replay.first(replay.protobuf(raw),1 if op==1003 else 2,b''))
   for monster in room.get(10,[]):
    p=replay.protobuf(monster);pid=replay.first(p,1);hero=replay.protobuf(replay.first(p,10,b''));hid=replay.first(hero,2)
    if not pid or not hid:continue
    value={'kind':'npc','hero_id':hid}
    if pid in entities and entities[pid]!=value:ambiguous.add(pid)
    entities[pid]=value
  elif op==1115:
   f=replay.protobuf(raw)
   if replay.first(f,1)==owner:deaths.append(replay.first(f,3))
 counter=collections.Counter();unknown=0
 for killer in deaths:
  entity=entities.get(killer)
  if not killer or killer in ambiguous or entity is None or killer==owner:unknown+=1;continue
  counter[(entity['kind'],entity['hero_id'])]+=1
 participants=[];transfers=gold_transfers(data,{p['player_id'] for p in summary['participants']});kill_rewards=kill_gold_rewards(data,{p['player_id'] for p in summary['participants']});purchases=purchase_gold_cards(data,{p['player_id'] for p in summary['participants']});buff_refs=buff_effect_sources(data)
 for p in summary['participants']:
  record,events=analyze(data,summary,p['player_id'],transfers,kill_rewards,purchases,buff_refs)
  timeline=[]
  for event in events:
   if event[6] in ('성장 상태','최종 결과'):continue
   detail=json.loads(event[7]);source=detail.get('killer_id',detail.get('source',detail.get('target',0)))
   entity=entities.get(source) if source not in ambiguous else None
   timeline.append({'round':event[4] or None,'kind':event[6],'detail':detail,'entity':entity,'entity_id':source,'transfer_party':{'id':(detail.get('transfer') or {}).get('counterpart',0),'entity':entities.get((detail.get('transfer') or {}).get('counterpart'))},'targets':[{'id':target,'entity':entities.get(target) if target not in ambiguous else None} for target in detail.get('targets',[])],'opponent':{'id':detail.get('opponent_id',0),'entity':entities.get(detail.get('opponent_id')) if detail.get('opponent_id') not in ambiguous else None}})
  participants.append({'player_id':p['player_id'],'timeline':timeline,'totals':summarize_events(record,events),'cards':cards.analyze(data,p['player_id']),'chips':chips.analyze(data,p['player_id']),'dice':dice.analyze(data,p['player_id'])})
 value={'participants':participants,'killers':[{'kind':kind,'hero_id':hero,'count':count} for (kind,hero),count in sorted(counter.items())],'unknown_deaths':unknown,'deaths':len(deaths)}
 db.execute('INSERT OR REPLACE INTO personal_match_insights VALUES (?,?,?,?)',(summary['replay_id'],owner,INSIGHTS_VERSION,json.dumps(value,ensure_ascii=False)))

def killer_statistics(db,owner,mode=None,map_id=None,hero_id=None,difficulty=None):
 import collections
 where='m.owner_id=?';params=[owner]
 for column,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id)):
  if value is not None:where+=' AND m.'+column+'=?';params.append(value)
 extra,args=difficulty_clause('m',difficulty);where+=extra;params+=args
 rows=db.execute('SELECT i.version,i.summary FROM personal_matches m LEFT JOIN personal_match_insights i ON m.replay_id=i.replay_id AND m.owner_id=i.owner_id WHERE '+where,params).fetchall()
 counts=collections.Counter();analyzed=unknown=deaths=0
 for version,raw in rows:
  if version is None or version<7:continue
  value=json.loads(raw);analyzed+=1;unknown+=value['unknown_deaths'];deaths+=value['deaths']
  for p in value['killers']:counts[(p['kind'],p['hero_id'])]+=p['count']
 return {'analyzed':analyzed,'total':len(rows),'unknown':unknown,'deaths':deaths,'items':[{'kind':kind,'hero_id':hero,'count':count} for (kind,hero),count in sorted(counts.items(),key=lambda p:(-p[1],p[0]))[:3]]}
