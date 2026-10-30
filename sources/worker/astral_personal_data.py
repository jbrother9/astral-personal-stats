"""Personal-only replay analysis and SQLite index. Does not store other players' profiles."""
import json,pathlib,struct,time
import astral_replay as replay
import astral_card_stats as cards
import astral_chip_stats as chips
import astral_combat_dice as dice
import astral_paths as paths
VERSION=4
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

def analyze(data,summary,owner):
 person=next((p for p in summary['participants'] if p['player_id']==owner),None)
 if person is None:raise ValueError('Own player not in replay')
 events=[];stats=dict(turns=0,deaths=0,npc_last_hits=0,effect_cards=0,battle_cards=0,skills=0)
 pos=frame=roundno=0;actor=0;level=None
 def add(kind,detail):
  events.append((summary['replay_id'],owner,frame,len(events),roundno,int(actor==owner),kind,json.dumps(detail,ensure_ascii=False)))
 while pos<len(data):
  op,n=struct.unpack_from('>HI',data,pos);pos+=6;body=data[pos:pos+n];pos+=n;frame+=1
  if op not in (1003,1113,1015,5056,5036,1115,1040,5022,5028,5212):continue
  f=replay.protobuf(body);pid=replay.first(f,1)
  if op==1015:
   roundno=replay.first(f,1);actor=replay.first(f,5)
   if actor==owner:stats['turns']+=1;add('턴 시작',{'round':roundno})
  elif op in (1003,1113):
   room=replay.protobuf(replay.first(f,1 if op==1003 else 2,b''))
   for raw in room.get(9,[]):
    p=replay.protobuf(raw)
    if replay.first(p,1)!=owner:continue
    hero=replay.protobuf(replay.first(p,10,b''));current=replay.first(hero,33)
    if current!=level:
     add('성장 상태',{'previous':level,'current':current,'snapshot_round':replay.first(room,14),'note':'게임 내부 성장 값; 스냅샷에서 확인한 시점'});level=current
  elif op==5056 and pid==owner:
   if replay.first(f,8):stats['skills']+=1;add('스킬',{'skill_id':replay.first(f,9)})
   if replay.first(f,2) and not replay.first(f,8):stats['effect_cards']+=1;add('효과 카드',{'card_id':replay.first(f,2)})
  elif op==5036 and pid==owner and replay.first(f,2) and not replay.first(f,3):
   stats['battle_cards']+=1;add('전투 카드',{'card_id':replay.first(f,2)})
  elif op==1115:
   if pid==owner:stats['deaths']+=1;add('사망',{})
   elif replay.first(f,3)==owner and pid not in [p['player_id'] for p in summary['participants']]:stats['npc_last_hits']+=1;add('NPC 마지막 공격',{'target_hero_id':replay.first(f,2)})
  elif op==5022 and replay.first(f,3)==owner:add('주사위',{'move_point':replay.first(f,2)})
  elif op==5028 and pid==owner:add('이동',{'distance':len(cards.fixed32_values(f.get(2,[])))})
  elif op==5212 and pid==owner and replay.first(f,2):add('칩 선택 응답',{'relic_id':replay.first(f,2),'is_reroll':bool(replay.first(f,3))})
  elif op==1040:
   for raw in f.get(4,[]):
    effect=replay.protobuf(raw)
    target=replay.first(effect,1)
    if 3 in effect and isinstance(effect[3][0],bytes):
     hp=replay.protobuf(effect[3][0]);source=replay.first(hp,9);damage_type=replay.first(hp,8)
     if source==owner and target and target!=owner and 1<=damage_type<=11:
      before=signed32(replay.first(hp,3));after=signed32(replay.first(hp,4));amount=max(0,max(0,before)-max(0,after))
      if amount:add('가한 피해',{'amount':amount,'target':target,'damage_type':damage_type})
    if target!=owner:continue
    for field,kind in ((2,'골드 변화'),(3,'HP 변화')):
     if field not in effect or not isinstance(effect[field][0],bytes):continue
     v=replay.protobuf(effect[field][0]);add(kind,{'before':signed32(replay.first(v,3)),'after':signed32(replay.first(v,4)),'requested_change':signed32(replay.first(v,2)),'note':'피해/치유 기여도는 아직 집계하지 않음'})
 record=(summary['replay_id'],owner,summary['finish_time'],summary.get('start_time'),person['hero_id'],summary.get('map_id'),summary.get('map_type'),person.get('rank'),max(0,summary['finish_time']-(summary.get('start_time') or summary['finish_time'])),*[stats[k] for k in ('turns','deaths','npc_last_hits','effect_cards','battle_cards','skills')],VERSION)
 return record,events

def index(db,data,summary,owner,managed=False):
 record,events=analyze(data,summary,owner)
 card_summary=cards.analyze(data,owner)
 with db:
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
 return metrics

def game_totals(db,rid,owner):
 row=db.execute('SELECT metrics FROM personal_game_totals WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 if row:return json.loads(row[0])
 record=db.execute('SELECT * FROM personal_matches WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 if record is None:return None
 events=db.execute('SELECT * FROM personal_events WHERE replay_id=? AND owner_id=? ORDER BY frame,ordinal',(rid,owner)).fetchall()
 return summarize_events(record,events)

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

def statistics(db,owner,mode=None,map_id=None,hero_id=None):
 """Personal-only aggregation. Separate PvE/PvP win rates; unknown results are excluded."""
 where='owner_id=?';params=[owner]
 if mode is not None:where+=' AND map_type=?';params.append(mode)
 if map_id is not None:where+=' AND map_id=?';params.append(map_id)
 if hero_id is not None:where+=' AND hero_id=?';params.append(hero_id)
 rows=db.execute('SELECT replay_id,hero_id,map_id,map_type,rank,duration,turns,deaths,npc_last_hits,effect_cards,battle_cards,skills FROM personal_matches WHERE '+where,params).fetchall()
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
 metrics_by_id={rid:json.loads(raw) for rid,raw in db.execute('SELECT replay_id,metrics FROM personal_game_totals WHERE owner_id=?',(owner,))}
 def movement_damage(items):
  measured=[metrics_by_id[r[0]] for r in items if r[0] in metrics_by_id and 'movement_distance' in metrics_by_id[r[0]] and 'damage_dealt' in metrics_by_id[r[0]]]
  return {'movement_damage_analyzed':len(measured),'movement_distance':sum(r['movement_distance'] for r in measured),'damage_dealt':sum(r['damage_dealt'] for r in measured)}
 def aggregate(items):
  count=len(items);ranked=[r[4] for r in items if r[4] is not None];rounds=[growth[r[0]] for r in items if r[0] in growth]
  outcomes=[match_outcome(r[3],r[4]) for r in items];wins=outcomes.count('win');losses=outcomes.count('loss');decided=wins+losses;families={mode_family(r[3]) for r in items}
  win_rate=wins/decided if decided and len(families)==1 and 'other' not in families else None
  return {'wins':wins,'losses':losses,'decided':decided,'unresolved':count-decided,'win_rate':win_rate,'matches':count,'duration':sum(r[5] or 0 for r in items),'first_places':sum(r[4]==1 for r in items),'average_rank':sum(ranked)/len(ranked) if ranked else None,'average_duration':sum(r[5] or 0 for r in items)/count if count else 0,'deaths':sum(r[7] or 0 for r in items),'average_deaths':sum(r[7] or 0 for r in items)/count if count else 0,'average_npc_last_hits':sum(r[8] or 0 for r in items)/count if count else 0,'average_effect_cards':sum(r[9] or 0 for r in items)/count if count else 0,'average_battle_cards':sum(r[10] or 0 for r in items)/count if count else 0,'average_skills':sum(r[11] or 0 for r in items)/count if count else 0,**movement_damage(items),'growth3_average_round':sum(rounds)/len(rounds) if rounds else None,'growth3_reached_matches':len(rounds)}
 def group(indices):
  groups={}
  for row in rows:groups.setdefault(tuple(row[i] for i in indices),[]).append(row)
  return [{'key':key,**aggregate(items)} for key,items in sorted(groups.items(),key=lambda pair:(-len(pair[1]),str(pair[0])))]
 return {'overall':aggregate(rows),'by_family':{kind:aggregate([r for r in rows if mode_family(r[3])==kind]) for kind in ('pve','pvp','other')},'by_mode':group((3,)),'by_map':group((2,3)),'by_hero':group((1,3))}


def save_appearances(db,summary,owner):
 if owner not in [p['player_id'] for p in summary['participants']]:raise ValueError('Own player not in replay')
 participants=[{'player_id':p['player_id'],'name':p.get('name',''),'hero_id':p.get('equipped_hero_id') or p['hero_id'],'standing_painting_id':p.get('standing_painting_id',0)} for p in summary['participants']]
 db.execute('INSERT OR REPLACE INTO personal_appearances VALUES (?,?,?)',(summary['replay_id'],owner,json.dumps(participants,ensure_ascii=False)))

def appearances(db,rid,owner):
 row=db.execute('SELECT participants FROM personal_appearances WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 return json.loads(row[0]) if row else []


INSIGHTS_VERSION=6

def has_insights(db,rid,owner):
 row=db.execute('SELECT version FROM personal_match_insights WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 return bool(row and row[0]>=INSIGHTS_VERSION)

def match_insights(db,rid,owner):
 row=db.execute('SELECT summary FROM personal_match_insights WHERE replay_id=? AND owner_id=? AND version>=?',(rid,owner,INSIGHTS_VERSION)).fetchone()
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
 participants=[]
 for p in summary['participants']:
  record,events=analyze(data,summary,p['player_id'])
  participants.append({'player_id':p['player_id'],'totals':summarize_events(record,events),'cards':cards.analyze(data,p['player_id']),'chips':chips.analyze(data,p['player_id']),'dice':dice.analyze(data,p['player_id'])})
 value={'participants':participants,'killers':[{'kind':kind,'hero_id':hero,'count':count} for (kind,hero),count in sorted(counter.items())],'unknown_deaths':unknown,'deaths':len(deaths)}
 db.execute('INSERT OR REPLACE INTO personal_match_insights VALUES (?,?,?,?)',(summary['replay_id'],owner,INSIGHTS_VERSION,json.dumps(value,ensure_ascii=False)))

def killer_statistics(db,owner,mode=None,map_id=None,hero_id=None):
 import collections
 where='m.owner_id=?';params=[owner]
 for column,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id)):
  if value is not None:where+=' AND m.'+column+'=?';params.append(value)
 rows=db.execute('SELECT i.version,i.summary FROM personal_matches m LEFT JOIN personal_match_insights i ON m.replay_id=i.replay_id AND m.owner_id=i.owner_id WHERE '+where,params).fetchall()
 counts=collections.Counter();analyzed=unknown=deaths=0
 for version,raw in rows:
  if version is None or version<INSIGHTS_VERSION:continue
  value=json.loads(raw);analyzed+=1;unknown+=value['unknown_deaths'];deaths+=value['deaths']
  for p in value['killers']:counts[(p['kind'],p['hero_id'])]+=p['count']
 return {'analyzed':analyzed,'total':len(rows),'unknown':unknown,'deaths':deaths,'items':[{'kind':kind,'hero_id':hero,'count':count} for (kind,hero),count in sorted(counts.items(),key=lambda p:(-p[1],p[0]))[:3]]}
