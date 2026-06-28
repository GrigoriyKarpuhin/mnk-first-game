"""aiart — генерация игровых ассетов через OpenAI image API по Design/ART_STYLE.md.

Мост: текстовый промпт (по стайл-гайду) -> AI-рендер -> чистый игровой спрайт в
`Assets/Resources/Sprites/` (или концепт-арт в `Design/concept/`).

Этапы (каждый — отдельный модуль):
  prompts   — снапшот промпт-блоков из ART_STYLE §0/§2/§7;
  presets   — реестр видов ассетов (kind) + сборка финального промпта;
  client    — загрузка ключа из .token и ленивый OpenAI();
  generate  — вызов images.generate/edit, сохранение RAW в Design/ai_raw/;
  pipeline  — RAW -> spritekit (вырез фона / резка листа / холст) -> Sprites/.

CLI: `python3 -m tools.aiart --help`  (запуск из корня репозитория).
"""
from .presets import PRESETS, build_prompt
from .generate import generate_raw
from . import pipeline

__all__ = ["PRESETS", "build_prompt", "generate_raw", "pipeline"]
