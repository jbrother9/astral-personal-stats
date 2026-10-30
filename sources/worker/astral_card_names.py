"""Compatibility API for card names backed by the unified runtime game catalogue."""
from astral_game_catalog import data_root
import astral_game_catalog as catalog

def load(db=None):return catalog.names('cards',db)
def resolver(db=None):return catalog.resolver('cards',db)
