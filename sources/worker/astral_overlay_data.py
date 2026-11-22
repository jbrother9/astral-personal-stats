"""Small account-scoped presentation snapshot for the FairyGUI in-game window."""
import datetime,json,pathlib,sqlite3,time
import astral_personal_data as personal
import astral_card_names as card_names
import astral_game_catalog as game_catalog
ROOT=pathlib.Path(__file__).resolve().parent

def references(db=None):
 return game_catalog.hero_names(db),game_catalog.names('maps',db),game_catalog.names('modes',db)

def number(v):return '-' if v is None else f'{v:.2f}'.rstrip('0').rstrip('.')
def duration(v):return f'{int(v or 0)//60}분 {int(v or 0)%60}초'

def move_dice_text(detail):
 values=detail.get('dice_values');point=detail.get('move_point',0)
 if not values:return '이동 주사위 '+str(point)
 text='주사위 '+ ' + '.join(str(v) for v in values)
 adjustment=detail.get('adjustment')
 if adjustment:text+=(' + 보정 '+str(adjustment)) if adjustment>0 else ' − 보정 '+str(abs(adjustment))
 text+=' → 이동값 '+str(point)
 if detail.get('controlled'):text+=' · 지정'
 return text
def hp_change_text(before,after,detail):
 before=max(0,before);after=max(0,after);delta=after-before
 def maximum(key):
  value=detail.get(key)
  return str(value) if type(value) is int and value>=0 else '?'
 return 'HP '+str(before)+'/'+maximum('hp_max_before')+' → '+str(after)+'/'+maximum('hp_max_after')+' ('+('+' if delta>0 else '')+str(delta)+')'

def metric_rows(totals):
 turns=totals.get('turns',0);lines=[]
 for key,label in [('turns','플레이어 턴'),('deaths','사망'),('npc_last_hits','NPC 막타'),('effect_cards','효과 카드 사용'),('battle_cards','전투 카드 사용'),('skills','스킬 사용'),('chip_selection_events','칩 선택 횟수'),('dice_events','이동 주사위 굴림 횟수'),('movement_distance','총 이동 거리 (칸)'),('damage_dealt','총 피해량 (가한 피해)'),('damage_taken','총 받은 피해량'),('max_battle_damage','최대 전투 피해'),('transferred_gold','코인 송금'),('received_gold','지원받은 코인'),('gold_increase','코인 획득'),('gold_decrease','코인 소모'),('gold_changes','코인 변동'),('hp_increase','HP 증가량'),('hp_decrease','HP 감소량'),('ally_support','아군 지원')]:
  if key=='gold_changes':lines.append([label,'','']);continue
  value=totals.get(key);
  if value is None:lines.append([label,'-','-']);continue
  lines.append([label,str(value),'-' if key in ('turns','max_battle_damage') or not turns else number(value/turns)])
 lines.append(['상점 방문',number(totals.get('shop_visits')),'-'])
 return lines


METRIC_KEYS=('turns','deaths','npc_last_hits','effect_cards','battle_cards','skills','chip_selection_events','dice_events','movement_distance','damage_dealt','damage_taken','max_battle_damage','transferred_gold','received_gold','gold_increase','gold_decrease','gold_changes','hp_increase','hp_decrease','ally_support','shop_visits')

def best_metrics(totals,others,own):
 result=[]
 for key in METRIC_KEYS:
  value=totals.get(key)
  values=[v.get(key) for v in others]
  result.append(bool(key not in ('turns','gold_changes','shop_visits') and type(value) in (int,float) and value>0 and values and all(type(v) in (int,float) for v in values) and value==max(values)))
 return result

def metric_detail(db,owner,rid,pid,index,page=0,insight=None,records_cache=None):
 if type(page) is not int:return None
 if type(index) is not int or not 0<=index<len(METRIC_KEYS) or type(pid) is not int or not isinstance(rid,str):return None
 cache_key=(owner,rid,pid,index)
 value=records_cache.get(cache_key) if records_cache is not None else None
 if value is None:
  value=_metric_records(db,owner,rid,pid,index,insight=insight)
  if value is None:return None
  if records_cache is not None:records_cache[cache_key]=value
 page=max(0,min(page,value['pages']-1))
 result={**value,'page':page}
 for field in ('rows','shop_details','row_chip_selections'):
  if field in value:result[field]=value[field][page*8:page*8+8]
 return result

def metric_records_cache(store,owner,rid,pid):
 stamps=[]
 for name in ('plugin-game-catalog.json','plugin-card-names.json'):
  path=store.root/name
  try:
   stat=path.stat();stamps.append((stat.st_mtime_ns,stat.st_size))
  except OSError:stamps.append(None)
 signature=(owner,rid,pid,store.db.total_changes,store.db.execute('PRAGMA data_version').fetchone()[0],tuple(stamps))
 cached=getattr(store,'_metric_records_cache',None)
 if cached is None or cached[0]!=signature:
  cached=(signature,{})
  store._metric_records_cache=cached
 return cached[1]

def _metric_records(db,owner,rid,pid,index,insight=None):
 if type(index) is not int or not 0<=index<len(METRIC_KEYS) or type(pid) is not int or not isinstance(rid,str):return None
 if not db.execute('SELECT 1 FROM personal_matches WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone():return None
 if insight is None:insight=personal.match_insights(db,rid,owner)
 participant=next((p for p in (insight or {}).get('participants',[]) if p['player_id']==pid),None)
 title=metric_rows({})[index][0];key=METRIC_KEYS[index];rows=[]
 if key=='shop_visits':
  shops=(participant or {}).get('shops')
  card_name=card_names.resolver(db)
  names={p['player_id']:p.get('name') for p in personal.appearances(db,rid,owner)}
  details=[]
  for visit_number,visit in enumerate(shops or [],1):
   candidates=' / '.join(card_name(i) for i in visit['offers'] if i>0)
   bought=visit['bought']
   actions=['구매 기록 미확인' if bought is None else '구매: '+' / '.join(card_name(i) for i in bought) if bought else '구매 없이 종료']
   for transfer in visit['transfers']:
    target=names.get(transfer['recipient']) or '참가자 #'+str(transfer['recipient'])
    amount,received=transfer['amount'],transfer['received']
    actions.append(target+'에게 '+str(amount)+'코인 전달'+(' (수령 '+str(received)+')' if received!=amount else ''))
   turn=str(visit['round'])+'턴' if visit['round'] else '턴 미확인'
   rows.append([str(visit_number)+'번째 방문 · '+turn,'카드 구매' if bought else '', '골드 전달' if visit['transfers'] else ''])
   details.append(['후보: '+(candidates or '미확인'),*actions])
  total=len(rows);pages=max(1,(total+7)//8)
  note='종료 기록이 확인된 방문입니다.' if shops else '상점 방문 기록이 없습니다.' if shops==[] else '상점 방문 기록을 확인하려면 리플레이 재분석이 필요합니다.'
  return {'replay_id':rid,'player_id':pid,'metric':index,'title':title,'page':0,'pages':pages,'total':total,'rows':rows,'shop_details':details,'note':note}
 if key=='chip_selection_events' and (participant or {}).get('chips',{}).get('version',0)>=3:
  panel=personal.chips.panel([participant['chips']],1,rid,title,db=db,match=True);items=panel['items']
  total=len(items);pages=max(1,(total+7)//8);visible=items
  average=metric_rows(participant['totals'])[index][2]
  return {'replay_id':rid,'player_id':pid,'metric':index,'title':title,'turn_average':average if average not in ('','-',None) else None,'page':0,'pages':pages,'total':total,'rows':[[item['turn_label'],item['name']+(' · '+item['acquisition_label'] if item.get('acquisition_label') else ' · 후보 연결 미확인' if not item.get('acquisition_only') and not item['connection_confirmed'] else '')] for item in visible],'row_chip_selections':[0 if item.get('acquisition_only') else item['selection_id'] for item in visible],'note':'선택 기록의 행을 누르면 후보와 최종 선택을 확인할 수 있습니다. 전원 지급과 칩 보상은 획득 턴·출처만 표시합니다.'}
 kinds={'turns':'턴 시작','deaths':'사망','npc_last_hits':'NPC 마지막 공격','effect_cards':'효과 카드','battle_cards':'전투 카드','skills':'스킬','chip_selection_events':'칩 선택 응답','dice_events':'주사위','movement_distance':'이동','damage_dealt':'가한 피해','damage_taken':'HP 변화','max_battle_damage':'전투 피해','transferred_gold':'골드 전달','received_gold':'골드 변화','gold_increase':'골드 변화','gold_decrease':'골드 변화','gold_changes':'골드 변화','hp_increase':'HP 변화','hp_decrease':'HP 변화'}
 kinds['ally_support']='아군 지원'
 people={p['player_id']:p for p in personal.appearances(db,rid,owner)}
 heroes=game_catalog.hero_names(db);monsters=game_catalog.names('monsters',db);card_name=card_names.resolver(db);buff_names=game_catalog.names('buffs',db);skill_names=game_catalog.names('skills',db);chip_names=game_catalog.names('chips',db);land_names=game_catalog.names('lands',db);event_names=game_catalog.names('events',db);building_names=game_catalog.names('buildings',db)
 reasons={1:'스킬',2:'카드',3:'이벤트',5:'운명',6:'점술',7:'버프',8:'지역 버프',9:'칸 효과',10:'폭탄 사망',11:'턴 보상',12:'전투',13:'상점 열기',14:'상점 구매',15:'맵 이벤트',16:'턴 종료',17:'칩 선택',18:'미션',19:'현상금 처치',20:'현상금 방어',21:'보스 합체',22:'방 조건',23:'럭키스타'}
 def transfer_difference(value):
  paid=value.get('amount',0);received=value.get('received',paid);delta=received-paid
  if not delta:return ''
  return ' · '+('추가 획득 +' if delta>0 else '수령 차이 ')+str(delta)+'코인 (원인 미확인)'
 def reason(detail,event=None):
  if detail.get('revival'):return '부활'
  if detail.get('cure_healing'):return '버프 효과 · '+buff_names.get(detail['cure_healing']['definition_id'],'이름 미확인 #'+str(detail['cure_healing']['definition_id']))
  if detail.get('kill_reward'):
   reward=detail['kill_reward'];heroes=reward.get('hero_ids') or [reward['hero_id']]
   names=' / '.join(monsters.get(hero,'이름 미확인 #'+str(hero)) for hero in heroes)
   return '처치 보상 · '+names+(' (동일 금액 대상 묶음)' if len(heroes)>1 else '')
  if detail.get('transfer'):return '코인 송금' if detail['transfer']['direction']=='sent' else '코인 지원 받음'
  code=detail.get('cause_type');label=reasons.get(code,'원인 미확인')
  if label=='원인 미확인':label={1:'스킬',2:'카드',3:'운명',4:'점술',5:'이벤트',6:'칸 효과',7:'전투',8:'버프',9:'소환물',10:'칩 효과',11:'맵 이벤트'}.get(detail.get('damage_type'),'원인 미확인')
  if label=='원인 미확인' and detail.get('star_change') in ('reward','cost'):label='스타 보상' if detail['star_change']=='reward' else '스타 상승 비용'
  if code==24:
   name=building_names.get(detail.get('cause_id'))
   label='건물 효과'+(' · '+name if name else ' · 이름 미확인 #'+str(detail.get('cause_id',0)))
  if code==3 and event_names.get(detail.get('cause_id')):label+=' · '+event_names.get(detail['cause_id'])
  if code==12 and event:
   opponent=event.get('opponent')
   if opponent and opponent.get('entity'):label+=' · '+target_label(opponent)
   elif event.get('entity') and event.get('entity_id')!=pid:label+=' · '+entity_label(event)
  if code==1 and skill_names.get(detail.get('cause_id')):label+=' · '+skill_names.get(detail['cause_id'])
  if code==17 and chip_names.get(detail.get('cause_id')):label='칩 효과 · '+chip_names.get(detail['cause_id'])
  if code==9 and detail.get('land'):
   land=detail['land'];name=land_names.get(land.get('land_type')) if land.get('land_type') is not None else None
   label='발판 효과'+(' · '+name if name else ' · 종류 미확인')
  if code==7 and detail.get('buff'):
   buff=detail['buff'];name=buff_names.get(buff['definition_id'],'');source=buff['source_type'];identity=buff['source_id'];origin=''
   if source==1 and skill_names.get(identity):origin='스킬 · '+skill_names.get(identity)
   elif source==2:origin='카드 · '+card_name(identity)
   elif source==6:origin='칩 · '+chip_names.get(identity,'이름 미확인 #'+str(identity))
   label='버프 효과'+(' · '+name if name else '')+(' · '+origin if origin else '')
   if detail.get('modifier_expired'):label+=' · 중첩 소진'
  if code==14 and detail.get('purchase_card_ids'):
   from collections import Counter
   purchased=Counter(detail['purchase_card_ids']);label+=' · '+', '.join(card_name(id)+((' ×'+str(count)) if count>1 else '') for id,count in purchased.items())
  if code==2 and detail.get('cause_id'):label+=' · '+card_name(detail['cause_id'])
  return label
 def entity_label(event):
  entity=event.get('entity');identity=event.get('entity_id',0)
  if not entity:return '원인 미확인' if not identity else '상대 미확인'
  hid=entity['hero_id'];name=(monsters if entity['kind']=='npc' else heroes).get(hid,'이름 미확인 #'+str(hid))
  person=people.get(identity)
  return (person.get('name','')+' · ' if person and person.get('name') else '')+name
 def target_label(target):
  identity=target.get('id');entity=target.get('entity')
  if identity==pid:return '자신'
  if not entity:return '대상 미확인'
  hid=entity['hero_id'];name=(monsters if entity['kind']=='npc' else heroes).get(hid,'이름 미확인 #'+str(hid));person=people.get(identity)
  return (person.get('name','')+' · ' if person and person.get('name') else '')+name
 for event in (participant or {}).get('timeline',[]):
  if event['kind']!=kinds.get(key):continue
  d=event['detail'];text=event['kind']
  if d.get('not_applied'):continue
  if key=='received_gold' and (d.get('transfer') or {}).get('direction')!='received':continue
  if key=='max_battle_damage' and (not participant or d.get('amount')!=participant['totals'].get('max_battle_damage')):continue
  if key=='deaths':text=entity_label(event)+'에게 사망' if event.get('entity') else (reason(d,event) if d.get('cause_type') else entity_label(event))+' · 사망'
  elif key in ('damage_taken','gold_increase','gold_decrease','gold_changes','hp_increase','hp_decrease'):
   delta=d.get('after',0)-d.get('before',0);increase=key.endswith('increase')
   if key=='gold_changes':
    if not delta:continue
   elif (increase and delta<=0) or (not increase and delta>=0):continue
   text=hp_change_text(d.get('before',0),d.get('after',0),d) if key in ('damage_taken','hp_increase','hp_decrease') else str(d.get('before',0))+' → '+str(d.get('after',0))+' ('+('+' if delta>0 else '')+str(delta)+')'
   text+=' · '+reason(d,event)
   if d.get('transfer'):
    transfer=d['transfer'];text+=(' → ' if transfer['direction']=='sent' else ' ← ')+target_label(event['transfer_party'])
    if transfer.get('received',transfer['amount'])!=transfer['amount']:text+=' · 지불 '+str(transfer['amount'])+' / 수령 '+str(transfer['received'])+'코인'+transfer_difference(transfer)
   if key=='damage_taken' and event.get('entity') and d.get('cause_type')!=12:text+=' · '+entity_label(event)
  elif key in ('damage_dealt','max_battle_damage'):
   value=d.get('damage_value',d.get('amount',0));text=entity_label(event)+' · '+str(value)+' 피해'
   if 'hp_before' in d and 'hp_after' in d:
    text+=' · '+hp_change_text(d['hp_before'],d['hp_after'],d)
   text+=' · '+reason(d,event)
  elif key=='received_gold':
   transfer=d['transfer'];text=target_label(event['transfer_party'])+'에게서 '+str(transfer.get('received',transfer['amount']))+'코인 지원받음'+(' · 스킬 전달' if transfer.get('source')=='skill' else '')
  elif key=='transferred_gold':text=entity_label(event)+' · 지불 '+str(d.get('amount',0))+' / 수령 '+str(d.get('received',d.get('amount',0)))+'코인'+(' · 스킬 전달' if d.get('source')=='skill' else '')+transfer_difference(d)
  elif key=='movement_distance':text=str(d.get('distance',0))+'칸 이동'
  elif key=='dice_events':text=move_dice_text(d)
  elif key=='chip_selection_events':
   chip=d.get('relic_id',0);text=game_catalog.names('chips',db).get(chip,'이름 미확인 #'+str(chip))+(' · 재추첨' if d.get('is_reroll') else '')
  elif key=='skills':text='스킬 사용'
  elif key=='ally_support':
   text=('스킬 · '+skill_names[d['skill_id']] if skill_names.get(d.get('skill_id')) else '스킬 사용') if d.get('support_kind')=='skill' else card_name(d.get('definition_id',0))
   text+=' → '+', '.join(target_label(target) for target in event.get('targets',[]))
  elif key=='npc_last_hits':
   hid=d.get('target_hero_id',0);text=monsters.get(hid,'이름 미확인 #'+str(hid))+' 마지막 공격'
  elif key in ('effect_cards','battle_cards'):text=card_name(d['definition_id']) if d.get('definition_id') else '카드명 연결 미확인'
  if key in ('deaths','npc_last_hits') and 'hp_before' in d and 'hp_after' in d:
   text+=' · '+hp_change_text(d['hp_before'],d['hp_after'],d)
  if key in ('effect_cards','skills') and event.get('targets'):text+=' → '+', '.join(target_label(target) for target in event['targets'])
  if key=='battle_cards':
   if event.get('opponent',{}).get('id'):text+=' · 전투 상대: '+target_label(event['opponent'])
   if 'card_bonus' in d:text+=' · 카드 보너스 '+('+' if d['card_bonus']>=0 else '')+str(d['card_bonus'])
  rows.append([str(event['round'])+'턴' if event.get('round') else '턴 미확인',text])
 if key=='movement_distance':
  per_turn={}
  for event in (participant or {}).get('timeline',[]):
   if event['kind']=='이동':
    turn=event.get('round');per_turn[turn]=per_turn.get(turn,0)+event['detail'].get('distance',0)
  rows=[[str(turn)+'턴' if turn is not None else '턴 미확인 합계','총 '+str(distance)+'칸 이동'] for turn,distance in per_turn.items()]
 if key in ('gold_increase','gold_changes') and participant and 'starting_gold' in participant['totals']:
  totals=participant['totals'];initial=totals.get('starting_gold');observed=totals.get('gold_change_increase',0)
  if initial is not None:rows.insert(0,['시작','시작 코인 +'+str(initial)+'코인'])
  official=totals.get('gold_increase')
  if official is not None:
   difference=official-observed-(initial or 0)
   if difference:rows.append(['집계 차이','결과창 집계 차이 '+('+' if difference>0 else '')+str(difference)+'코인 (세부 원인 미확인)'])
 total=len(rows);pages=max(1,(total+7)//8)
 note=''
 if participant is None or 'timeline' not in participant:note='세부 기록 분석이 필요합니다. 리플레이가 남아 있으면 백그라운드에서 보완합니다.'
 elif key=='ally_support':note='아군에게 사용한 스킬과 효과 카드 기록입니다.'
 elif key=='transferred_gold':
  confirmed=sum(e['detail'].get('amount',0) for e in participant.get('timeline',[]) if e['kind']=='골드 전달');settled=participant['totals'].get('transferred_gold_settlement',participant['totals'].get('transferred_gold'))
  note='확인된 지불액 '+str(confirmed)+'코인 · 결과 전달 합계 '+(str(settled)+'코인' if settled is not None else '미확인')
  if settled is not None and confirmed!=settled:
   received=sum(e['detail'].get('received',e['detail'].get('amount',0)) for e in participant.get('timeline',[]) if e['kind']=='골드 전달')
   if received==settled:note+=' · 결과창은 수령액 기준, 모드는 지불액 기준입니다.'
   else:
    difference=settled-received
    note+=' · 수령액과 결과 차이 '+('+' if difference>0 else '')+str(difference)+'코인 (집계 경로 미확인)'
 elif key=='gold_changes':note='시작 코인과 획득·소모를 발생 순서로 표시합니다. 결과창 집계 차이는 실제 잔액 변화에 합산하지 않습니다.'
 elif key=='gold_increase':note='결과창의 얻은 코인입니다. 시작 코인을 포함하며, 확인되지 않은 차이는 별도로 표시합니다.'
 elif key=='max_battle_damage':note='결과창의 최대 전투 피해와 같은 금액의 전투 이벤트입니다. 같은 최댓값이 여러 번 나오면 모두 표시합니다.'
 elif key=='damage_dealt':note='피해값과 대상의 HP 변화를 함께 표시합니다. 남은 HP보다 큰 피해는 실제 HP 감소량과 다릅니다.'
 elif key=='damage_taken':note='HP 감소 내역입니다. 결과창의 받은 피해량과 같은 기준이라고 볼 수 없습니다.'
 elif not rows:note='관찰된 세부 기록이 없습니다. 최종 합계가 0이 아니라면 기록 누락 또는 집계 기준 차이일 수 있습니다.'
 average=metric_rows(participant['totals'])[index][2] if participant else None
 return {'replay_id':rid,'player_id':pid,'metric':index,'title':title,'turn_average':average if average not in ('','-',None) else None,'page':0,'pages':pages,'total':total,'rows':rows,'note':note}


def aggregate_metrics(r):
 rows=[]
 for key,label in [('damage_dealt','총 피해량 (가한 피해)'),('damage_taken','총 받은 피해량'),('deaths','사망'),('movement_distance','총 이동 거리 (칸)'),('transferred_gold','코인 송금'),('max_battle_damage','최대 전투 피해')]:
  measured=r[key+'_analyzed'];value=r[key]
  amount=number(value) if measured else '-'
  if measured and measured<r['matches']:amount+=' ('+str(measured)+'/'+str(r['matches'])+'경기)'
  rows.append([label,amount,number(r['average_'+key])])
 return rows

def card_ranking(*args,include_used=False,**kwargs):
 value=personal.cards.aggregate(*args,**kwargs)
 if not include_used:value.pop('used',None)
 value.pop('acquired',None)
 return value

def card_panel(value,key,title,db=None):
 resolve_name=card_names.resolver(db)
 panel={k:value.get(k,0) for k in ('unresolved_use','unresolved_purchase','assisted_purchase')};panel.update(key=key,title=title,analyzed=1,total=1)
 for kind in ('battle_used','effect_used','bought'):
  panel[kind]=[{'card_id':int(id),'name':resolve_name(int(id)),'count':count,'average':str(count)} for id,count in sorted(value.get(kind,{}).items(),key=lambda x:(-x[1],int(x[0])))]
 return panel

def game_detail(db,owner,rid,heroes=None,maps=None):
 if heroes is None:heroes=game_catalog.hero_names(db)
 if maps is None:maps=game_catalog.names('maps',db)
 match=db.execute('SELECT started_at,finished_at,map_id FROM personal_matches WHERE replay_id=? AND owner_id=?',(rid,owner)).fetchone()
 played_at=match[0] or match[1]
 played_date=datetime.datetime.fromtimestamp(played_at,datetime.timezone(datetime.timedelta(hours=9))).strftime('%Y-%m-%d %H:%M') if played_at else '-'
 map_name=maps.get(match[2],'이름 미확인 #'+str(match[2])) if match[2] is not None else '맵 미확인'
 insight=personal.match_insights(db,rid,owner) or {};by_player={p['player_id']:p for p in insight.get('participants',[])}
 participants=[]
 for person in personal.appearances(db,rid,owner):
  pid=person['player_id'];analysis=by_player.get(pid);totals=analysis['totals'] if analysis else personal.game_totals(db,rid,owner) if pid==owner else None
  card=card_panel(analysis['cards'],rid+':'+str(pid),person.get('name','')+' · 이 경기 카드 순위',db=db) if analysis else None
  if card is not None:card['chips']=personal.chips.panel([analysis['chips']] if analysis.get('chips') else [],1,rid+':'+str(pid),person.get('name','')+' · 이 경기에서 선택한 칩',db=db,match=True)
  if card is not None:card['dice']=personal.dice.panel([analysis['dice']] if analysis.get('dice') else [],1)
  peers=[by_player.get(v['player_id'],{}).get('totals',{}) for v in personal.appearances(db,rid,owner)]
  participants.append({**person,'hero_name':heroes.get(person['hero_id'],str(person['hero_id'])),'own':pid==owner,'available':totals is not None,'rows':metric_rows(totals or {}),'best':best_metrics(totals or {},peers,pid==owner),'final_stars':(totals or {}).get('growth_final'),'cards':card})
 totals=personal.game_totals(db,rid,owner) or {}
 card=card_ranking(db,owner,rid=rid);card.update(key=rid,title='이 경기 카드 순위');card['chips']=personal.chips.aggregate(db,owner,rid=rid);card['chips'].update(key=rid,title='이 경기에서 선택한 칩');card['dice']=personal.dice.aggregate(db,owner,rid=rid);return {'replay_id':rid,'played_date':played_date,'map_name':map_name,'title':'경기 '+rid,'rows':metric_rows(totals),'cards':card,'participants':participants}

def make_snapshot(db,owner,mode=None,map_id=None,tab=0,page=0,hero_id=None,difficulty=None,sort_column=0,sort_descending=True):
 if mode is not None and personal.mode_family(mode)=='pvp':difficulty=None
 heroes,maps,modes=references(db);stats=personal.statistics(db,owner,mode,map_id,hero_id,difficulty)
 difficulty_names=game_catalog.names('difficulties',db)
 def difficulty_label(value):return '미확인' if value is None or value==-2 else difficulty_names.get(value,'난이도 '+str(value))
 observed=db.execute('SELECT m.map_type,c.difficulty FROM personal_matches m LEFT JOIN personal_match_context c ON c.replay_id=m.replay_id AND c.owner_id=m.owner_id WHERE m.owner_id=?',(owner,)).fetchall()
 pve_difficulties={value for modecode,value in observed if personal.mode_family(modecode)=='pve'}
 difficulty_options=[{'id':-1,'label':'전체 난이도'}]+[{'id':value,'label':difficulty_label(value)} for value in sorted(v for v in pve_difficulties if v is not None)]+([{'id':-2,'label':'미확인'}] if None in pve_difficulties else [])
 options=db.execute('SELECT DISTINCT map_type,map_id,hero_id FROM personal_matches WHERE owner_id=? ORDER BY map_type,map_id',(owner,)).fetchall()
 mode_options=[{'id':-1,'label':'전체 모드'}]+[{'id':v,'label':modes.get(v,f'모드 {v}'),'is_pvp':personal.mode_family(v)=='pvp'} for v in sorted({r[0] for r in options if r[0] is not None})]
 map_options=[{'id':-1,'label':'전체 맵'}]+[{'id':v,'label':maps.get(v,f'맵 {v}')} for v in sorted({r[1] for r in options if r[1] is not None})]
 hero_options=[{'id':-1,'label':'전체 캐릭터'}]+[{'id':v,'label':heroes.get(v,f'캐릭터 {v}')} for v in sorted({r[2] for r in options if r[2] is not None})]
 aggregate=stats['overall'];count=aggregate['matches'];rows=[];columns=[];detail=[];row_cards=[];history_entries=None
 def aggregate_row(r,label,modecode):return [label,modes.get(modecode,f'모드 {modecode}'),str(r['matches']),str(r['wins']),str(r['losses']),personal.rate_label(r['win_rate']),duration(r['average_duration']),number(r['average_deaths']),str(r['movement_distance']) if r['movement_damage_analyzed']==r['matches'] else '분석 '+str(r['movement_damage_analyzed'])+'/'+str(r['matches']),str(r['damage_dealt']) if r['movement_damage_analyzed']==r['matches'] else '분석 '+str(r['movement_damage_analyzed'])+'/'+str(r['matches'])]
 profile=None
 if tab==0:
  def most_played(groups,names):
   counts={}
   for group in groups:
    entity=group['key'][0]
    if entity is not None:
     counts[entity]=counts.get(entity,0)+group['matches']
   return [{'id':entity,'name':names.get(entity,'이름 미확인 #'+str(entity)),'matches':matches}
           for entity,matches in sorted(counts.items(),key=lambda item:(-item[1],item[0]))[:3]]
  favorites=[{'hero_id':item['id'],'hero_name':item['name'],'matches':item['matches']}
             for item in most_played([group for group in stats['by_hero'] if (group['key'][0] or 0)>0],heroes)]
  nickname=''
  pick_counts=[0]*4
  encounter_counts={}
  skin_counts={}
  where='m.owner_id=?';params=[owner]
  for column,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id)):
   if value is not None:
    where+=f' AND m.{column}=?';params.append(value)
  extra,args=personal.difficulty_clause('m',difficulty)
  record_matches=db.execute('SELECT m.replay_id,m.map_type,m.rank,t.metrics FROM personal_matches m LEFT JOIN personal_game_totals t ON t.replay_id=m.replay_id AND t.owner_id=m.owner_id WHERE '+where+extra+' ORDER BY m.finished_at DESC,m.replay_id DESC',params+args).fetchall()
  rounds=personal.ending_rounds(db,owner)
  record_positions=[-1]*5
  record_values=[None]*5
  for position,(rid,modecode,rank,raw) in enumerate(record_matches):
   metrics=personal.normalized_totals(json.loads(raw)) if raw else {}
   values=[None,rounds.get(rid) if personal.mode_family(modecode)=='pve' and personal.match_outcome(modecode,rank)=='win' else None,
           metrics.get('max_battle_damage'),metrics.get('damage_dealt'),metrics.get('movement_distance')]
   for index in range(1,5):
    value=values[index];previous=record_values[index]
    if value is not None and (previous is None or (value<previous if index==1 else value>previous)):
     record_values[index]=value;record_positions[index]=position
  for raw, in db.execute('SELECT a.participants FROM personal_matches m JOIN personal_appearances a ON a.replay_id=m.replay_id AND a.owner_id=m.owner_id WHERE '+where+extra+' ORDER BY m.finished_at DESC,m.replay_id DESC',params+args):
   participants=json.loads(raw)
   positions=[i for i,p in enumerate(participants) if p.get('player_id')==owner]
   if len(positions)!=1:continue
   position=positions[0]
   own=participants[position]
   character=own.get('hero_id');skin=own.get('standing_painting_id')
   if type(character) is int and character>0 and type(skin) is int and skin>0:
    counts=skin_counts.setdefault(character,{})
    counts[skin]=counts.get(skin,0)+1
   if not nickname:nickname=participants[position].get('name','')
   if 1<len(participants)<=4:pick_counts[position]+=1
   seen_players=set()
   seen_characters=set()
   for person in participants:
    player=person.get('player_id');character=person.get('hero_id')
    if type(player) is not int or player<=0 or player==owner or player in seen_players:continue
    seen_players.add(player)
    if type(character) is int and character>0 and character not in seen_characters:
     seen_characters.add(character)
     encounter_counts[character]=encounter_counts.get(character,0)+1
  for favorite in favorites:
   counts=skin_counts.get(favorite['hero_id'],{})
   # Rows are newest first; equal counts retain the most recently used skin.
   favorite['standing_painting_id']=max(counts,key=counts.get) if counts else 0
  profile={'nickname':nickname,'matches':count,'favorites':favorites,
           'win_rates':['PvE '+personal.rate_label(stats['by_family']['pve']['win_rate']),
                        'PvP '+personal.rate_label(stats['by_family']['pvp']['win_rate'])],
           'average_stars':number(aggregate['average_growth_final']),
           'star_samples':aggregate['growth_final_analyzed'],
           'records':[[label,number(aggregate[key])+(unit if aggregate[key] is not None else '')]
                      for label,key,unit in [('평균 클리어','average_clear_round','턴'),('최단 클리어','fastest_clear_round','턴'),
                                             ('최고 전투 피해','peak_max_battle_damage',''),('최고 총 피해','peak_damage_dealt',''),
                                             ('최대 이동','peak_movement_distance','칸')]],
           'favorite_maps':most_played(stats['by_map'],maps),
           'encounters':[{'hero_id':character,'name':heroes.get(character,'이름 미확인 #'+str(character)),'count':amount}
                         for character,amount in sorted(encounter_counts.items(),key=lambda item:(-item[1],item[0]))[:3]],
           'pick_counts':pick_counts,'record_positions':record_positions}
 elif tab in (1,2,4):
  columns=['난이도' if tab==4 else '항목','모드','경기','승','패','승률','평균 시간','평균 사망','이동 합계','피해 합계']
  groups=stats['by_difficulty' if tab==4 else 'by_map' if tab==1 else 'by_hero']
  if tab==4:groups=sorted(groups,key=lambda r:(r['key'][-1],r['key'][0] is None,r['key'][0] or 0))
  for r in groups:
   key=r['key']
   label=('-' if personal.mode_family(key[-1])!='pve' else difficulty_label(key[0])) if tab==4 else (maps if tab==1 else heroes).get(key[0],str(key[0]))
   rows.append(aggregate_row(r,label,key[-1]))
   group_difficulty=((-2 if key[0] is None else key[0]) if personal.mode_family(key[-1])=='pve' else None) if tab==4 else difficulty
   card=card_ranking(db,owner,mode=key[-1],map_id=key[0] if tab==1 else map_id,hero_id=key[0] if tab==2 else hero_id,difficulty=group_difficulty)
   card['basic_rows']=aggregate_metrics(r)
   card.update(key=str(tab)+':'+str(tuple(key)),title=(label+' · '+modes.get(key[-1],str(key[-1])))+' 카드 순위');card['chips']=personal.chips.aggregate(db,owner,mode=key[-1],map_id=key[0] if tab==1 else map_id,hero_id=key[0] if tab==2 else hero_id,difficulty=group_difficulty);card['chips'].update(key=card['key'],title=card['title'].replace('카드 순위','선택한 칩'));card['dice']=personal.dice.aggregate(db,owner,mode=key[-1],map_id=key[0] if tab==1 else map_id,hero_id=key[0] if tab==2 else hero_id,difficulty=group_difficulty);row_cards.append(card)
 else:
  columns=['시각 (KST)','캐릭터','맵','모드','난이도','결과','경기 시간','종료 라운드','사망']
  final_rounds=personal.ending_rounds(db,owner)
  difficulties=game_catalog.names('difficulties',db)
  contexts=dict(db.execute('SELECT replay_id,difficulty FROM personal_match_context WHERE owner_id=?',(owner,)))
  where='owner_id=?';params=[owner]
  if mode is not None:where+=' AND map_type=?';params.append(mode)
  if map_id is not None:where+=' AND map_id=?';params.append(map_id)
  if hero_id is not None:where+=' AND hero_id=?';params.append(hero_id)
  extra,args=personal.difficulty_clause('personal_matches',difficulty);where+=extra;params+=args
  history=[]
  for rid,finished,hero,mapcode,modecode,rank,seconds,deaths in db.execute('SELECT replay_id,finished_at,hero_id,map_id,map_type,rank,duration,deaths FROM personal_matches WHERE '+where+' ORDER BY finished_at DESC,replay_id DESC',params):
   stamp=datetime.datetime.fromtimestamp(finished,datetime.timezone(datetime.timedelta(hours=9))).strftime('%m-%d %H:%M') if finished else '-'
   deaths=(personal.game_totals(db,rid,owner) or {}).get('deaths')
   row=[stamp,heroes.get(hero,str(hero)),maps.get(mapcode,str(mapcode)),modes.get(modecode,str(modecode)),('-' if personal.mode_family(modecode)=='pvp' else difficulties.get(contexts[rid],'난이도 '+str(contexts[rid])) if contexts.get(rid) is not None else '미확인'),personal.result_label(modecode,rank),duration(seconds),str(final_rounds[rid])+'R' if rid in final_rounds else '-',number(deaths)]
   difficulty_value=contexts.get(rid) if personal.mode_family(modecode)=='pve' else None
   outcome={'win':0,'loss':1}.get(personal.match_outcome(modecode,rank))
   keys=[finished or None,row[1] if hero is not None else None,row[2] if mapcode is not None else None,
         row[3] if modecode is not None else None,difficulty_value,outcome,seconds,final_rounds.get(rid),deaths]
   history.append((rid,row,keys))
   # Detail is read only for the visible page; no replay downloads on UI requests.
  known=[item for item in history if item[2][sort_column] is not None]
  missing=[item for item in history if item[2][sort_column] is None]
  history=sorted(known,key=lambda item:item[2][sort_column],reverse=sort_descending)+missing
  rows=[item[1] for item in history];detail=[item[0] for item in history]
  history_entries=[{'replay_id':rid,'row':row,'sort_values':keys} for rid,row,keys in history]
 killers=personal.killer_statistics(db,owner,mode,map_id,hero_id,difficulty)
 killers['filter_label']='모드: '+('전체 모드' if mode is None else modes.get(mode,'모드 '+str(mode)))+' · 맵: '+('전체 맵' if map_id is None else maps.get(map_id,'맵 '+str(map_id)))+' · 캐릭터: '+('전체 캐릭터' if hero_id is None else heroes.get(hero_id,'캐릭터 '+str(hero_id)))
 killers['filter_label']+=' · 난이도: '+('전체 난이도' if difficulty is None else difficulty_label(difficulty))
 npc_names=game_catalog.names('monsters',db)
 for item in killers['items']:
  name=(npc_names if item['kind']=='npc' else heroes).get(item['hero_id'],'이름 미확인 #'+str(item['hero_id']));item.update(hero_name=name,name=name,standing_painting_id=0)
 page_size=5 if tab!=3 and killers['items'] else 8
 total=len(rows);pages=max(1,(total+page_size-1)//page_size);page=max(0,min(page,pages-1));start=page*page_size;shown=rows[start:start+page_size]
 details=[]
 for rid in detail[start:start+page_size]:
  details.append(game_detail(db,owner,rid,heroes,maps))
 overview_cards=card_ranking(db,owner,mode,map_id,hero_id,difficulty=difficulty,include_used=tab==0);overview_cards['basic_rows']=aggregate_metrics(aggregate);overview_cards.update(key='overview',title='전체 카드 순위 · 현재 필터 기준');overview_cards['chips']=personal.chips.aggregate(db,owner,mode,map_id,hero_id,difficulty=difficulty);overview_cards['dice']=personal.dice.aggregate(db,owner,mode,map_id,hero_id,difficulty=difficulty)
 return {'history_entries':history_entries,'profile':profile,'difficulty':difficulty if difficulty is not None else -1,'difficulty_enabled':mode is None or personal.mode_family(mode)!='pvp','difficulties':difficulty_options,'page_size':page_size,'killers':killers,'card_overview':overview_cards,'row_cards':row_cards[start:start+page_size],'schema':1,'owner_id':owner,'generated_at':int(time.time()),'hero':hero_id if hero_id is not None else -1,'heroes':hero_options,'mode':mode if mode is not None else -1,'map':map_id if map_id is not None else -1,'tab':tab,'page':page,'pages':pages,'total_rows':total,'modes':mode_options,'maps':map_options,
         'summary':[str(count)+' 경기',duration(aggregate['average_duration'])+' 평균 시간']+(['승률 미분류 '+str(stats['by_family']['other']['matches'])+' 경기'] if stats['by_family']['other']['matches'] else []),'pve_summary':personal.family_summary('PvE',stats['by_family']['pve']),'pvp_summary':personal.family_summary('PvP',stats['by_family']['pvp']),
         'columns':columns,'rows':shown,'details':details,'note':'PvE: 1위 승리/2위 패배 · PvP: 1위 승리 · 미확인 결과/모드는 승률 제외'}

def publish(store,state,before_snapshot=None):
 if not state or type(state.get('owner_id')) is not int or state['owner_id']<=0:return False
 try:
  file=store.root/'plugin-ui-request.json'
  if file.stat().st_size>4096:return False
  request=json.loads(file.read_text(encoding='utf-8-sig'))
  if request.get('owner_id')!=state['owner_id'] or not 0<=time.time()-request.get('updated_at',0)<=60:return False
  if request.get('visible') is False:return True
  tab=request.get('tab',0);page=request.get('page',0);mode=request.get('mode',-1);map_id=request.get('map',-1);hero_id=request.get('hero',-1);difficulty=request.get('difficulty',-1)
  if any(type(v) is not int for v in (tab,page,mode,map_id,hero_id,difficulty)) or tab not in range(5) or not 0<=page<=100000 or difficulty < -2 or difficulty>=2**31:return False
  sort_column=request.get('sort_column',0);sort_descending=request.get('sort_descending',True)
  if type(sort_column) is not int or not 0<=sort_column<9 or type(sort_descending) is not bool:return False
  detail_only=request.get('detail_only',False)
  if type(detail_only) is not bool:return False
  detail_replay=request.get('detail_replay','');detail_player=request.get('detail_player',0)
  if before_snapshot:before_snapshot()
  if detail_only:
   if tab!=3 or type(detail_replay) is not str or not personal.replay.valid_id(detail_replay) or type(detail_player) is not int:return False
   if mode!=-1 and personal.mode_family(mode)=='pvp':difficulty=-1
   where='m.owner_id=? AND m.replay_id=?';params=[state['owner_id'],detail_replay]
   for column,value in (('map_type',mode),('map_id',map_id),('hero_id',hero_id)):
    if value!=-1:where+=f' AND m.{column}=?';params.append(value)
   extra,args=personal.difficulty_clause('m',None if difficulty==-1 else difficulty)
   if not store.db.execute('SELECT 1 FROM personal_matches m WHERE '+where+extra,params+args).fetchone():return False
   game=game_detail(store.db,state['owner_id'],detail_replay)
   if not any(person['player_id']==detail_player for person in game['participants']):return False
   result={'schema':1,'detail_only':True,'detail_game':game,'owner_id':state['owner_id'],
           'generated_at':int(time.time()),'tab':tab,'page':page,'mode':mode,'map':map_id,'hero':hero_id,'difficulty':difficulty}
  else:
   result=make_snapshot(store.db,state['owner_id'],None if mode==-1 else mode,None if map_id==-1 else map_id,tab,page,None if hero_id==-1 else hero_id,None if difficulty==-1 else difficulty,sort_column,sort_descending)
   game=next((game for game in result['details'] if game['replay_id']==detail_replay),None)
  result['request_id']=request.get('request_id',0)
  result['supports_metric_prefetch']=True
  records_cache=metric_records_cache(store,state['owner_id'],detail_replay,detail_player)
  insight=None
  if game and type(detail_player) is int and any(person['player_id']==detail_player for person in game['participants']):
   insight=personal.match_insights(store.db,detail_replay,state['owner_id']) or {}
   result['metric_first_pages']=[value for index in range(len(METRIC_KEYS))
                                 if (value:=metric_detail(store.db,state['owner_id'],detail_replay,detail_player,index,0,insight=insight,records_cache=records_cache)) is not None]
  if request.get('detail_metric',-1)>=0:
   current=metric_detail(store.db,state['owner_id'],detail_replay,detail_player,request.get('detail_metric'),request.get('detail_page',0),insight=insight,records_cache=records_cache)
   result['metric_detail']=current
   if current and current['page']+1<current['pages']:
    result['metric_next_page']=metric_detail(store.db,state['owner_id'],detail_replay,detail_player,current['metric'],current['page']+1,insight=insight,records_cache=records_cache)
  target=store.root/'plugin-ui-snapshot.json';temp=target.with_suffix('.part');temp.write_text(json.dumps(result,ensure_ascii=False),encoding='utf8');temp.replace(target);return True
 except (OSError,ValueError,TypeError,sqlite3.Error,OverflowError,IndexError,KeyError):return False
