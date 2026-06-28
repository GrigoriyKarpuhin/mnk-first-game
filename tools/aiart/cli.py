"""CLI: генерация игровых ассетов через OpenAI image API по ART_STYLE.

Запуск из корня репозитория:
    python3 -m tools.aiart <kind> [опции]

Примеры:
    # бесплатно — показать собранный промпт и параметры, НЕ вызывая API:
    python3 -m tools.aiart tile_floor --dry-run
    python3 -m tools.aiart character --subject "C-3075 ..." --name c3075 --dry-run

    # тайл пола 64×64 (дёшево):
    python3 -m tools.aiart tile_floor --name floor_concrete --quality low

    # лист персонажа (3 ракурса) с референсом героя для стиля:
    python3 -m tools.aiart character_sheet --subject "..." \
        --names c3075 c3075_side c3075_up \
        --ref Assets/Resources/Sprites/player.png --quality medium

    # пере-обработать уже сгенерированный RAW (без новой оплаты):
    python3 -m tools.aiart tile_wall --name wall_top --raw Design/ai_raw/tile_wall/xxx.png
"""
import argparse
from pathlib import Path

from .client import REPO_ROOT
from .generate import generate_raw, describe, DEFAULT_MODEL
from .presets import PRESETS
from . import pipeline

# дефолтные имена для тайлов (реальные игровые ассеты)
TILE_DEFAULT_NAME = {"tile_floor": "floor_concrete", "tile_wall": "wall_top"}
# референсы по умолчанию для персонажей (для консистентности стиля, §3c)
DEFAULT_CHARACTER_REFS = ["Assets/Resources/Sprites/player.png"]


def _build_parser():
    p = argparse.ArgumentParser(
        prog="python3 -m tools.aiart",
        description="Генерация спрайтов/тайлов/пропов/концептов через gpt-image по ART_STYLE.md.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__,
    )
    p.add_argument("kind", choices=list(PRESETS), help="вид ассета")
    p.add_argument("--subject", help="описание персонажа/пропа/сцены (для character/prop/concept)")
    p.add_argument("--extra", help="доп. строка в конец промпта")
    p.add_argument("--name", help="имя выходного спрайта без .png")
    p.add_argument("--names", nargs="+", help="имена для character_sheet (front side up)")
    p.add_argument("--ref", action="append", default=[],
                   help="референс-картинка для images.edit (можно несколько)")
    p.add_argument("--model", default=DEFAULT_MODEL, help=f"модель (по умолчанию {DEFAULT_MODEL})")
    p.add_argument("--quality", default="low", choices=["low", "medium", "high", "auto"],
                   help="качество генерации (дороже = выше); для тестов low")
    p.add_argument("--variants", type=int, default=1,
                   help="сколько вариантов сгенерировать (каждый платный)")
    p.add_argument("--size", type=int, help="переопределить финальный размер (px)")
    p.add_argument("--factor", type=int, default=2,
                   help="чанк-пиксель персонажа/пропа: 2=как герой, 1=чёткие 256")
    p.add_argument("--flip", action="store_true", help="отзеркалить (профиль -> влево)")
    p.add_argument("--flip-side", action="store_true",
                   help="в листе отзеркалить полосу с 'side' в имени")
    p.add_argument("--raw", help="пере-обработать существующий RAW PNG (без вызова API)")
    p.add_argument("--no-import", action="store_true",
                   help="только сгенерировать RAW, без постобработки в Sprites")
    p.add_argument("--dry-run", action="store_true",
                   help="показать промпт и параметры, НЕ вызывая API")
    return p


def _resolve_names(args, kind):
    """Проверить/выдать имена выходных файлов. Возвращает (name, names)."""
    if kind.endswith("_sheet"):
        if not args.names:
            raise SystemExit(f"Для {kind} нужен --names (имена выходных спрайтов "
                             "по порядку фигур/объектов на листе).")
        return None, args.names
    name = args.name or TILE_DEFAULT_NAME.get(kind)
    if not name:
        raise SystemExit(f"Для kind '{kind}' нужен --name (имя выходного спрайта).")
    return name, None


def _resolve_refs(args, kind):
    refs = list(args.ref)
    if PRESETS[kind]["endpoint"] == "edit" and not refs:
        refs = [r for r in DEFAULT_CHARACTER_REFS if (REPO_ROOT / r).exists()]
        if refs:
            print(f"INFO: --ref не задан, беру по умолчанию для стиля: {', '.join(refs)} "
                  f"(см. ART_STYLE §3c).")
    return refs


def main(argv=None):
    args = _build_parser().parse_args(argv)
    kind = args.kind
    refs = _resolve_refs(args, kind)

    if args.dry_run:
        print(describe(kind, subject=args.subject, extra=args.extra, refs=refs,
                       model=args.model, quality=args.quality, variants=args.variants))
        return

    name, names = _resolve_names(args, kind)

    # 1) получить RAW: либо из существующего файла (--raw), либо сгенерировать
    if args.raw:
        raws = [Path(args.raw)]
        if not raws[0].exists():
            raise SystemExit(f"RAW не найден: {raws[0]}")
        print(f"Пере-обработка RAW без вызова API: {raws[0]}")
    else:
        raws = generate_raw(kind, subject=args.subject, extra=args.extra, refs=refs,
                            model=args.model, quality=args.quality,
                            variants=args.variants, name=(name or (names[0] if names else None)))

    # 2) постобработка
    if args.no_import:
        print("--no-import: RAW сохранён, постобработка пропущена.")
        return
    if len(raws) > 1:
        print(f"Сгенерировано {len(raws)} вариантов — авто-импорт пропущен. "
              f"Выбери лучший и пере-обработай: "
              f"python3 -m tools.aiart {kind} --name {name or '<name>'} --raw <путь>")
        return

    pipeline.run(kind, raws[0], name=name, names=names, size=args.size,
                 factor=args.factor, flip=args.flip, flip_side=args.flip_side)


if __name__ == "__main__":
    main()
