---
name: generate-game-art
description: >
  Generate game-ready sprites and concept art (characters, character sheets,
  environment tiles, props, concept art) for the mnk-first-game prison game via
  OpenAI gpt-image, following Design/ART_STYLE.md, and import them into
  Assets/Resources/Sprites/ (or Design/concept/). Use when asked to create or
  regenerate an NPC, guard, inmate, floor/wall tile, prop, or concept art with
  AI. PAID API — bills per image.
version: 1.0.0
---

# Generate game art (OpenAI gpt-image → Sprites)

Инструмент: пакет [tools/aiart/](../../../tools/aiart/README.md). Он превращает
текстовый промпт (собранный по `Design/ART_STYLE.md`) в готовый игровой спрайт:
**RAW-рендер → чистка/раскладка через `tools/spritekit` → PNG в
`Assets/Resources/Sprites/`**. Unity сам ставит PPU = ширина PNG, Point-фильтр и
без компрессии (`Assets/Editor/PixelArtSpriteImporter.cs`) — пайплайн лишь отдаёт
PNG правильного размера.

> ⚠️ **ПЛАТНЫЙ API.** Каждая генерация и каждый `--variants` стоят денег.
> ВСЕГДА сначала делай `--dry-run` (бесплатно), для тестов бери `--quality low`.
> Перед батчем (несколько персонажей/тайлов) — подтверди у пользователя.

## Пути относительно корня репозитория (`<repo>/`), не папки скилла.

## Предусловия
- Ключ OpenAI в `.token` в корне репо (он в `.gitignore`) **или** env
  `OPENAI_API_KEY`. Ключ нигде не печатаем.
- Разовая установка зависимостей:
  ```bash
  python3 -m pip install -r tools/requirements.txt
  ```
- Все команды запускать из корня репозитория.

## Виды ассетов
`character` · `character_sheet` · `tile_floor` · `tile_wall` · `prop` ·
`prop_sheet` (несколько пропов одним листом) · `concept`.
Таблица размеров/эндпоинтов — в [tools/aiart/README.md](../../../tools/aiart/README.md).

## Рабочий цикл (всегда)
1. **Dry-run** — проверить промпт и параметры, ничего не тратя:
   ```bash
   python3 -m tools.aiart <kind> --dry-run [--subject "..."] [--name ...]
   ```
2. **Генерация** одного ассета на `--quality low` (или `medium` для финала).
3. **Открыть результат** в `Assets/Resources/Sprites/<name>.png` и оценить.
   RAW лежит в `Design/ai_raw/<kind>/` — можно пере-обработать без новой оплаты
   (`--raw <путь>`), подкрутив `--flip`/`--factor`/`--size`.
4. (Опц.) Проверить в игре скиллом `/run-mnk-first-game --isolated`.

## Рецепты (copy-paste)

**Тайл пола / стены (64×64):**
```bash
python3 -m tools.aiart tile_floor --name floor_concrete --quality low
python3 -m tools.aiart tile_wall  --name wall_top       --quality low
```
> Это перетрёт существующие игровые тайлы — проверь `git diff`. Для пробы дай
> другое имя (например `--name floor_ai`). Бесшовность не гарантирована — тайли
> финал 2×2 и смотри швы.

**Персонаж — один ракурс** (профиль смотрит ВЛЕВО; если рендер вправо — `--flip`):
```bash
python3 -m tools.aiart character \
  --subject "C-3075, робкий технарь: сутулый, худой, очки/самодельный визор, \
карманы с деталями, провода, серая тюремная роба с термопечатным ID на спине и груди" \
  --name c3075 --quality medium
# --ref не указан -> автоматически берётся Assets/Resources/Sprites/player.png (стиль)
```

**Персонаж — лист 3 ракурса** (front / side-влево / back):
```bash
python3 -m tools.aiart character_sheet \
  --subject "C-3075, робкий технарь, очки, сумка-сатчел, серая роба, ID C-3075" \
  --names c3075 c3075_side c3075_up \
  --ref Assets/Resources/Sprites/player.png \
  --ref Design/concept/concept_inmates_block_c.png \
  --quality medium
# если на листе профиль смотрит вправо: добавь --flip-side
```

**Охрана** (безликая, чёрная тактическая, см. §3b) — как character, с ref охраны:
```bash
python3 -m tools.aiart character_sheet \
  --subject "надзиратель ранг 02 Tactical Officer: глянцевый чёрный шлем без лица с визором, \
плитоноска с подсумками, нашивка ID, ноль индивидуальности" \
  --names guard2 guard2_side guard2_up \
  --ref Assets/Resources/Sprites/guard.png \
  --ref Design/concept/concept_guards_block_c.png --quality medium
```

**Проп** (объект по центру, прозрачный фон):
```bash
python3 -m tools.aiart prop --subject "ржавый металлический шкафчик заключённого, вид сверху" \
  --name locker_ai --quality low
```

**Лист пропов** (несколько объектов одним кадром → режется по объектам; порядок = `--names`):
```bash
python3 -m tools.aiart prop_sheet --quality high \
  --names camera toilet sink shower_head locker bed item_screwdriver item_keycard \
  --subject "1) купольная камера; 2) стальной унитаз; 3) умывальник; 4) душевая лейка; 5) металлический локер; 6) койка; 7) отвёртка-иконка; 8) карта-пропуск-иконка"
# вырезы -> Design/ai_raw/prop_sheet/crop_<idx>.png; если порядок сбился, переименуй вручную:
#   python3 -m tools.aiart prop --raw Design/ai_raw/prop_sheet/crop_N.png --name <правильное_имя>
```

**Концепт-арт сцены** (в `Design/concept/`, не в Sprites):
```bash
python3 -m tools.aiart concept \
  --subject "коридор охраны: металлический служебный проход, камеры, зелёные панели, безликие надзиратели" \
  --name concept_guard_corridor --quality high
```

**Несколько вариантов → выбрать лучший без повторной оплаты:**
```bash
python3 -m tools.aiart prop --subject "..." --name locker_ai --variants 3   # авто-импорт пропускается
python3 -m tools.aiart prop --name locker_ai --raw Design/ai_raw/prop/locker_ai_<ts>_1.png
```

## Референсы (важно для консистентности)
Для персонажей ВСЕГДА давай `--ref` (или полагайся на авто-`player.png`): это
держит единый стиль/пропорции/палитру (ART_STYLE §3c). Добавляй профильный
референс (`player_side.png`) для бок-ракурсов и нужный концепт (`Design/concept/*`).

**КРИТИЧНО — единый персонаж на всех кадрах:** генерь СНАЧАЛА idle-лист, а затем
walk_1/walk_2 **с приложенными idle-кадрами как референсом внешности** (front/side/up
этого же персонажа) + кадры для позы + `player.png` для пикселя. Иначе GPT на каждом
листе рисует «нового» человека (разное лицо/одежда). В тексте явно: «внешность СТРОГО
как на idle-эталонах, это ТОТ ЖЕ человек; со старых walk-кадров брать только позу».
Пример (walk_1 программиста):
```bash
python3 -m tools.aiart character_sheet --quality medium \
  --names npc_programmer_walk_1 npc_programmer_side_walk_1 npc_programmer_up_walk_1 \
  --ref Assets/Resources/Sprites/npc_programmer.png \
  --ref Assets/Resources/Sprites/npc_programmer_side.png \
  --ref Assets/Resources/Sprites/npc_programmer_up.png \
  --ref Assets/Resources/Sprites/npc_programmer_walk_1.png \
  --ref Assets/Resources/Sprites/npc_programmer_side_walk_1.png \
  --ref Assets/Resources/Sprites/npc_programmer_up_walk_1.png \
  --ref Assets/Resources/Sprites/player.png \
  --subject "..." --extra "Внешность СТРОГО как на idle-эталонах, тот же человек; поза — фаза шага"
```
То же для одного внешнего референса (фото персонажа): прикладывай его ко ВСЕМ листам
(idle и walk) первым `--ref`, тогда образ держится единым.

## Ключевые флаги
`--quality low|medium|high` · `--variants N` · `--model gpt-image-1` (откат) ·
`--factor 2|1` (2 = чанки как у героя, 1 = чёткие 256 как у охраны) ·
`--size N` · `--flip` / `--flip-side` · `--raw <png>` (пере-обработка) ·
`--no-import` (только RAW). Полный список: `python3 -m tools.aiart --help`.

## Грабли
- Бесшовность тайлов gpt-image не гарантирует — фолбэк `tools/wall_topdown.py` /
  `tools/generate_props.py` (процедурные).
- Пропорции персонажа могут плыть от 7.5–8 голов — ужесточай `--ref`, регенерируй.
- Замена канона (`player*`, `floor_concrete`, `wall_top`) перетирает ассеты.
- Промпты по «Ракель» — grounded, без pin-up (как требует сам гайд).
