# aiart — генерация ассетов через OpenAI gpt-image

Мост из текстового промпта (по `Design/ART_STYLE.md`) в готовый игровой спрайт:
**RAW-рендер → чистка/раскладка через `tools/spritekit` → PNG в
`Assets/Resources/Sprites/`** (Unity сам выставит PPU/Point-фильтр через
`Assets/Editor/PixelArtSpriteImporter.cs`).

> ⚠️ **Платный API.** Каждая генерация и каждый `--variants` стоят денег.
> Для проверок используй `--dry-run` (бесплатно) и `--quality low`.

## Установка
```bash
python3 -m pip install -r tools/requirements.txt   # разово (нужен openai)
```
Ключ берётся из `.token` в корне репо (он в `.gitignore`), иначе из
`OPENAI_API_KEY`. Ключ никогда не печатается.

## Виды ассетов (kind)
| kind | endpoint | размер ген. | фон | результат |
|------|----------|-------------|-----|-----------|
| `character` | edit (с --ref) | 1024×1536 | белый → вырез | `Sprites/<name>.png` 256, ступни у низа |
| `character_sheet` | edit (с --ref) | 1536×1024 | белый → вырез | 3 спрайта 256 (front/side/up) |
| `tile_floor` | generate | 1024×1024 | opaque | `Sprites/<name>.png` 64×64 RGB |
| `tile_wall` | generate | 1024×1024 | opaque | `Sprites/<name>.png` 64×64 RGB |
| `prop` | generate | 1024×1024 | белый → вырез | `Sprites/<name>.png`, объект по центру |
| `prop_sheet` | generate | 1536×1024 | белый → вырез | НЕСКОЛЬКО пропов в одном кадре → режутся в отдельные спрайты по `--names` |
| `concept` | generate | 1536×1024 | opaque | `Design/concept/<name>.png` |

> Примечание: `gpt-image-2` НЕ поддерживает `background=transparent`, поэтому
> персонажи/пропы генерятся на ЧИСТОМ БЕЛОМ фоне, а прозрачность делает
> `spritekit.remove_background` (тот же путь, что и при импорте героя).

## Примеры
```bash
# Бесплатно — увидеть промпт и параметры, без вызова API:
python3 -m tools.aiart tile_floor --dry-run
python3 -m tools.aiart character --subject "..." --name guard2 --dry-run

# Тайл пола 64×64 (дёшево):
python3 -m tools.aiart tile_floor --name floor_concrete --quality low

# Персонаж (один ракурс) с авто-референсом героя для стиля:
python3 -m tools.aiart character --subject "C-3075 робкий технарь, очки, сумка-сатчел" \
    --name c3075 --quality medium
# профиль смотрит ВЛЕВО; если рендер смотрит вправо — добавь --flip

# Лист «3 фигуры в ряд» (спереди / сбоку-влево / со спины):
python3 -m tools.aiart character_sheet --subject "C-3075 робкий технарь, очки" \
    --names c3075 c3075_side c3075_up \
    --ref Assets/Resources/Sprites/player.png \
    --ref Design/concept/concept_inmates_block_c.png --quality medium

# Лист из нескольких пропов в одном кадре (режется по объектам, порядок = --names):
python3 -m tools.aiart prop_sheet --quality high \
    --names camera toilet sink shower_head locker bed item_screwdriver item_keycard \
    --subject "1) купольная камера; 2) стальной унитаз; 3) умывальник; 4) душевая лейка; 5) металлический локер; 6) койка; 7) отвёртка-иконка; 8) карта-пропуск-иконка"
# вырезы пишутся в Design/ai_raw/prop_sheet/crop_<idx>.png; если порядок сбился —
# переименуй: prop --raw Design/ai_raw/prop_sheet/crop_N.png --name <правильное_имя>

# Несколько вариантов, потом выбрать лучший и пере-обработать БЕЗ новой оплаты:
python3 -m tools.aiart prop --subject "ржавый металлический шкафчик, вид сверху" \
    --name locker_ai --variants 3
python3 -m tools.aiart prop --name locker_ai --raw Design/ai_raw/prop/locker_ai_<ts>_1.png
```

## Полезные флаги
- `--dry-run` — показать промпт/параметры, не тратя деньги.
- `--quality low|medium|high` — качество (дороже = выше). Дефолт `low`.
- `--variants N` — N вариантов (каждый платный); авто-импорт пропускается, выбираешь `--raw`.
- `--ref PATH` (можно несколько) — референс-картинки для `images.edit` (стиль/консистентность). Для персонажей по умолчанию подставляется `player.png`.
- `--model` / env `OPENAI_IMAGE_MODEL` — модель (дефолт `gpt-image-2`; откат `--model gpt-image-1`).
- `--factor 2|1` — пиксель персонажа/пропа: `2` = чанки как у героя, `1` = чёткие 256 как у охраны.
- `--size N` — переопределить финальный размер.
- `--flip` (персонаж) / `--flip-side` (лист) — отзеркалить профиль влево.
- `--raw PATH` — пере-обработать сохранённый RAW без вызова API (бесплатно).
- `--no-import` — только сохранить RAW, без постобработки.

## Как это устроено
- `client.py` — ключ из `.token`/env, ленивый `OpenAI()`. Единственный, кто читает ключ.
- `prompts.py` — снапшот блоков `ART_STYLE.md` §0/§2/§3/§7. Источник правды — сам гайд; правишь гайд → перенеси сюда.
- `presets.py` — реестр `kind` + `build_prompt()`.
- `generate.py` — вызов API, RAW → `Design/ai_raw/<kind>/` (всегда хранится; не в git).
- `pipeline.py` — RAW → `tools/spritekit` (`remove_background`/`slice_sheet`/`place_on_canvas`) → `Sprites/`.

## Грабли
- **Бесшовность тайлов** gpt-image НЕ гарантирует. Перед заменой канона тайли финал 2×2 и смотри швы; фолбэк — процедурные `tools/wall_topdown.py` / `tools/generate_props.py`.
- **Пропорции/направление персонажа** плывут — всегда давай `--ref player.png`, при нужде `--flip`, регенерируй (RAW дёшево пере-обрабатывать).
- **Замена канона** (`floor_concrete`, `wall_top`, `player*`) перетирает существующие ассеты — проверяй через `git diff` и тестовый прогон.
- **Модерация** — промпты по «Ракель» держи grounded (no pin-up, как требует сам гайд).
