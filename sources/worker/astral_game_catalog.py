"""Validated generated game catalogue. No manually maintained ID/name references."""
import json,pathlib,time,sqlite3
import astral_paths as paths
ROOT=pathlib.Path(__file__).resolve().parent
CATEGORIES=('cards','characters','monsters','maps','chips','modes')
_cache={};_reported=set()

def data_root(db=None):
 if db is not None:
  try:
   for _,name,file in db.execute('PRAGMA database_list'):
    if name=='main' and file:return pathlib.Path(file).resolve().parent
  except sqlite3.Error:pass
 return paths.data_dir()

def load(db=None):
 path=data_root(db)/'plugin-game-catalog.json'
 try:
  stat=path.stat()
  if stat.st_size>4*1024*1024:return None
  signature=(stat.st_mtime_ns,stat.st_size)
  if _cache.get(path,(None,None))[0]==signature:return _cache[path][1]
  value=json.loads(path.read_text(encoding='utf-8-sig'))
  if value.get('schema')!=1 or value.get('source')!='live_game_configuration' or value.get('complete') is not True:return None
  catalogs=value.get('catalogs');result={}
  if not isinstance(catalogs,dict):return None
  for category in CATEGORIES:
   rows=catalogs.get(category)
   if not isinstance(rows,list) or len(rows)>20000:return None
   entries={}
   for row in rows:
    id=row.get('id');key=row.get('name_key');name=row.get('name');rarity=row.get('rarity',0);portrait=row.get('portrait_group',0)
    if type(id) is not int or id<0 or id in entries or type(key) is not int or not isinstance(name,str) or len(name)>2048 or type(rarity) is not int or type(portrait) is not int:return None
    entries[id]={'id':id,'name_key':key,'name':name.strip(),'rarity':rarity,'portrait_group':portrait}
   result[category]=entries
  _cache[path]=(signature,result);return result
 except (OSError,ValueError,TypeError,AttributeError):return None

def legacy_cards(db=None):
 # Compatibility with the previous generated runtime cache, never a manual reference table.
 try:
  path=data_root(db)/'plugin-card-names.json'
  if path.stat().st_size>4*1024*1024:return {}
  value=json.loads(path.read_text(encoding='utf-8-sig'))
  if value.get('schema')!=1 or value.get('source')!='live_game_configuration' or value.get('complete') is not True:return {}
  result={}
  for row in value.get('cards',[]):
   id=row.get('card_id');key=row.get('name_key');name=row.get('name')
   if type(id) is not int or id<=0 or id in result or type(key) is not int or not isinstance(name,str) or len(name)>2048:return {}
   result[id]={'id':id,'name_key':key,'name':name.strip(),'rarity':0,'portrait_group':0}
  return result
 except (OSError,ValueError,TypeError,AttributeError):return {}

def metadata(category,db=None):
 value=load(db)
 return value.get(category,{}) if value is not None else legacy_cards(db) if category=='cards' else {}

def report_missing(category,id,db=None):
 if type(id) is not int or id<0:return
 root=data_root(db);marker=(str(root),category,id)
 if marker in _reported:return
 _reported.add(marker)
 try:
  root.mkdir(parents=True,exist_ok=True)
  with (root/'catalog-name-misses.jsonl').open('a',encoding='utf8') as file:file.write(json.dumps({'category':category,'id':id,'observed_at':int(time.time())})+'\n')
 except OSError:pass

class NameMap(dict):
 def __init__(self,values,category,db):super().__init__(values);self.category=category;self.db=db
 def get(self,id,default=None):
  if id in self:return super().get(id)
  report_missing(self.category,id,self.db);return default

def names(category,db=None):
 return NameMap({id:row['name'] for id,row in metadata(category,db).items() if row['name_key']>0 and row['name']},category,db)

def hero_names(db=None):
 value=dict(names('monsters',db));value.update(names('characters',db));return NameMap(value,'heroes',db)

def resolver(category,db=None):
 values=names(category,db)
 def resolve(id):return values.get(id,'이름 미확인 #'+str(id))
 return resolve
