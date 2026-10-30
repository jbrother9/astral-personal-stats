"""Per-game card counters. Card instance IDs stay replay-local; no event timeline is stored."""
import collections,json,pathlib,struct
import astral_replay as replay
import astral_card_names as card_names
VERSION=3
ROOT=pathlib.Path(__file__).resolve().parent

def fixed32_values(values):
 result=[]
 for value in values:
  if isinstance(value,int):result.append(value)
  elif isinstance(value,bytes) and len(value)%4==0:result.extend(struct.unpack('<'+'I'*(len(value)//4),value))
  else:raise ValueError('Unsupported packed card values')
 return result

def analyze(data,owner):
 known={};hand=None;used=collections.Counter();battle_used=collections.Counter();effect_used=collections.Counter();acquired=collections.Counter();bought=collections.Counter();pending=[]
 unresolved_use=unresolved_buy=assisted=selections=initial=0;pos=0
 def update_hand(raw_cards):
  nonlocal hand,initial
  current={}
  for raw in raw_cards:
   card=replay.protobuf(raw);uid,config=replay.first(card,1),replay.first(card,2)
   if uid>0 and config>0:current[uid]=config;known[uid]=config
  if hand is None:
   if current:initial=len(current);hand=current
  else:
   for uid,config in current.items():
    if uid not in hand:acquired[config]+=1
   hand=current
 while pos<len(data):
  if pos+6>len(data):raise ValueError('Incomplete card frame')
  op,size=struct.unpack_from('>HI',data,pos);pos+=6
  if size>len(data)-pos:raise ValueError('Incomplete card payload')
  body=data[pos:pos+size];pos+=size
  if op not in (1003,1113,1109,5036,5056,5074,5216,5030):continue
  msg=replay.protobuf(body)
  if op in (1003,1113):
   room=replay.protobuf(replay.first(msg,1 if op==1003 else 2,b''))
   for raw in room.get(9,[]):
    player=replay.protobuf(raw)
    if replay.first(player,1)==owner:update_hand(replay.protobuf(replay.first(player,10,b'')).get(8,[]))
   continue
  if replay.first(msg,2 if op==1109 else 1)!=owner:continue
  if op==1109:update_hand(msg.get(1,[]))
  elif op==5036:
   uid=replay.first(msg,2)
   if not uid or replay.first(msg,3):continue
   if uid in known:used[known[uid]]+=1;battle_used[known[uid]]+=1
   else:unresolved_use+=1
  elif op==5056:
   config=replay.first(msg,2)
   if config>0 and not replay.first(msg,8):used[config]+=1;effect_used[config]+=1
  elif op==5074:unresolved_use+=1 # Quick-card identity semantics have not been verified.
  elif op==5030:
   try:
    count=len(fixed32_values(msg.get(3,[])));unresolved_buy+=count;selections+=count
   except ValueError:unresolved_buy+=1
  elif op==5216:
   try:choices=fixed32_values(msg.get(2,[]))
   except ValueError:unresolved_buy+=1;continue
   selections+=len(choices);pending.extend(choices)
   if not replay.first(msg,6):continue
   try:
    offers=fixed32_values(msg.get(4,[]));sold=[]
    for value in msg.get(5,[]):sold.extend([value] if isinstance(value,int) else value)
    valid=bool(pending) and len(offers)==len(sold) and all(v in (0,1) for v in sold) and len(set(pending))==len(pending) and set(pending)=={i for i,v in enumerate(sold) if v} and all(0<=i<len(offers) and offers[i]>0 for i in pending)
    if valid:
     for i in pending:bought[offers[i]]+=1
     if replay.first(msg,3):assisted+=len(pending)
    else:unresolved_buy+=len(pending)
   except ValueError:unresolved_buy+=len(pending)
   pending=[]
 unresolved_buy+=len(pending)
 return {'version':VERSION,'used':dict(used),'battle_used':dict(battle_used),'effect_used':dict(effect_used),'acquired':dict(acquired),'bought':dict(bought),'initial_hand_excluded':initial,'unresolved_use':unresolved_use,'unresolved_purchase':unresolved_buy,'assisted_purchase':assisted,'purchase_selections':selections,'acquisition_basis':'new hand instance after first nonempty hand','purchase_basis':'confirmed matching PvE shop selections/offers/sold flags; includes assistance'}

def save(db,rid,owner,summary):
 db.execute('INSERT OR REPLACE INTO personal_card_stats VALUES (?,?,?,?)',(rid,owner,VERSION,json.dumps(summary,ensure_ascii=False)))
def has_index(db,rid,owner):
 row=db.execute('SELECT version FROM personal_card_stats WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone();return bool(row and row[0]>=VERSION)

def aggregate(db,owner,mode=None,map_id=None,hero_id=None,rid=None):
 where='m.owner_id=?';params=[owner]
 for col,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id),('replay_id',rid)):
  if value is not None:where+=' AND m.'+col+'=?';params.append(value)
 rows=db.execute('SELECT c.version,c.summary FROM personal_matches m LEFT JOIN personal_card_stats c ON c.replay_id=m.replay_id AND c.owner_id=m.owner_id WHERE '+where,params).fetchall()
 totals={kind:collections.Counter() for kind in ('used','battle_used','effect_used','acquired','bought')};analyzed=0;unresolved_use=unresolved_buy=assisted=0
 for version,raw in rows:
  if version is None or version<VERSION or not raw:continue
  value=json.loads(raw);analyzed+=1
  for kind in totals:
   totals[kind].update({int(k):int(n) for k,n in value.get(kind,{}).items()})
  unresolved_use+=value.get('unresolved_use',0);unresolved_buy+=value.get('unresolved_purchase',0);assisted+=value.get('assisted_purchase',0)
 resolve_name=card_names.resolver(db)
 result={'analyzed':analyzed,'total':len(rows),'unresolved_use':unresolved_use,'unresolved_purchase':unresolved_buy,'assisted_purchase':assisted}
 for kind,counter in totals.items():
  result[kind]=[{'card_id':id,'name':resolve_name(id),'count':count,'average':f'{count/analyzed:.2f}'.rstrip('0').rstrip('.') if analyzed else '-'} for id,count in sorted(counter.items(),key=lambda pair:(-pair[1],pair[0]))]
 return result
