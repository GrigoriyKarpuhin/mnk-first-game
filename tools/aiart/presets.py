"""Реестр видов ассетов (kind) и сборка финального промпта.

Каждый пресет описывает всё, что нужно генератору и пайплайну:
  endpoint    — 'generate' | 'edit' (edit = с референс-картинками, для стиля);
  size        — размер генерации у API (gpt-image: 1024x1024 | 1024x1536 | 1536x1024);
  background  — 'transparent' | 'opaque';
  process     — какая постобработка в pipeline.py ('character'|'sheet'|'tile'|'prop'|'concept');
  out         — куда кладём финал ('sprites' | 'concept');
  final_size  — размер финального холста по умолчанию (px);
  needs_subject — обязателен ли --subject.
"""
from . import prompts

PRESETS = {
    "character": {
        "endpoint": "edit",
        "size": "1024x1536",
        # gpt-image-2 не поддерживает transparent -> генерим на БЕЛОМ фоне,
        # его вырезает spritekit.remove_background (как импорт героя).
        "background": "opaque",
        "process": "character",
        "out": "sprites",
        "final_size": 256,
        "needs_subject": True,
    },
    "character_sheet": {
        "endpoint": "edit",
        "size": "1536x1024",
        "background": "opaque",
        "process": "sheet",
        "out": "sprites",
        "final_size": 256,
        "needs_subject": True,
    },
    "tile_floor": {
        "endpoint": "generate",
        "size": "1024x1024",
        "background": "opaque",
        "process": "tile",
        "out": "sprites",
        "final_size": 64,
        "needs_subject": False,
    },
    "tile_wall": {
        "endpoint": "generate",
        "size": "1024x1024",
        "background": "opaque",
        "process": "tile",
        "out": "sprites",
        "final_size": 64,
        "needs_subject": False,
    },
    "prop": {
        "endpoint": "generate",
        "size": "1024x1024",
        "background": "opaque",  # белый фон -> вырезается spritekit.remove_background
        "process": "prop",
        "out": "sprites",
        "final_size": 128,
        "needs_subject": True,
    },
    "prop_sheet": {
        "endpoint": "generate",
        "size": "1536x1024",
        "background": "opaque",  # белый фон, режется по объектам (connected components)
        "process": "prop_sheet",
        "out": "sprites",
        "final_size": 128,
        "needs_subject": True,
    },
    "concept": {
        "endpoint": "generate",
        "size": "1536x1024",
        "background": "opaque",
        "process": "concept",
        "out": "concept",
        "final_size": None,
        "needs_subject": True,
    },
}


def build_prompt(kind, subject=None, extra=None):
    """Собрать финальный промпт для вида `kind`.

    subject — описание объекта/персонажа/сцены (для character/prop/concept);
    extra   — необязательная добавка в конец (нюансы конкретного запроса).
    """
    if kind not in PRESETS:
        raise SystemExit(f"Неизвестный kind '{kind}'. Доступно: {', '.join(PRESETS)}")
    spec = PRESETS[kind]
    if spec["needs_subject"] and not subject:
        raise SystemExit(f"Для kind '{kind}' нужен --subject (описание).")

    if kind in ("character", "character_sheet"):
        sheet = prompts.SHEET_CLAUSE if kind == "character_sheet" else ""
        text = prompts.CHARACTER_TEMPLATE.format(
            subject=subject, sheet=sheet,
            palette=prompts.PALETTE, negative=prompts.NEGATIVE,
        )
    elif kind == "tile_floor":
        text = prompts.TILE_FLOOR
    elif kind == "tile_wall":
        text = prompts.TILE_WALL
    elif kind == "prop":
        text = prompts.PROP_TEMPLATE.format(
            subject=subject, palette=prompts.PALETTE, negative=prompts.NEGATIVE,
        )
    elif kind == "prop_sheet":
        text = prompts.PROP_SHEET_TEMPLATE.format(
            subject=subject, palette=prompts.PALETTE, negative=prompts.NEGATIVE,
        )
    elif kind == "concept":
        text = prompts.CONCEPT_TEMPLATE.format(
            base=prompts.STYLE_BASE, subject=subject,
            palette=prompts.PALETTE, negative=prompts.NEGATIVE,
        )
    else:  # pragma: no cover — на случай добавления kind без ветки
        raise SystemExit(f"build_prompt: нет ветки для kind '{kind}'")

    if extra:
        text = f"{text}\n{extra}"
    return text
