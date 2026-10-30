"""Runtime paths for development and frozen distribution builds."""
import os,pathlib,sys
ROOT=pathlib.Path(__file__).resolve().parent

def data_dir():
 value=os.environ.get('ASTRAL_STATS_DATA_DIR')
 if value:return pathlib.Path(value).expanduser().resolve()
 if getattr(sys,'frozen',False):return pathlib.Path(os.environ.get('LOCALAPPDATA',str(pathlib.Path.home()/'AppData/Local')))/'AstralPersonalStats'
 return ROOT/'astral-collector-data'

def set_data_dir(path):os.environ['ASTRAL_STATS_DATA_DIR']=str(pathlib.Path(path).expanduser().resolve())
def settings_file():
 if getattr(sys,'frozen',False) or os.environ.get('ASTRAL_STATS_DATA_DIR'):return data_dir()/'personal-settings.json'
 return ROOT/'personal-settings.json'
