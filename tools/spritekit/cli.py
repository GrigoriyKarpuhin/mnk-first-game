"""CLI: чистка одной картинки или разрезка листа в набор спрайтов.

Примеры:
  # одну картинку — только вырезать фон и обрезать:
  python3 -m tools.spritekit clean hero.png player.png

  # лист «3 фигуры в ряд» -> стоячие кадры (одного роста, ступни на полу):
  python3 -m tools.spritekit sheet sheet.png \
      --names player player_side player_up --out-dir Assets/Resources/Sprites \
      --normalize 232

  # лист приседа/наклона -> НЕ нормализовать высоту, тот же масштаб + пустота сверху:
  python3 -m tools.spritekit sheet pickup.png \
      --names player_pickup_1 player_side_pickup_1 player_up_pickup_1 \
      --out-dir Assets/Resources/Sprites --scale 0.337
"""
import argparse
from pathlib import Path

from PIL import Image

from .background import remove_background
from .layout import place_on_canvas
from .sheet import find_figures


def _add_clean_opts(p):
    p.add_argument("--white-threshold", type=int, default=236)
    p.add_argument("--pocket-min-area", type=int, default=24)
    p.add_argument("--no-pockets", action="store_true", help="не закрывать карманы")
    p.add_argument("--no-defringe", action="store_true", help="не срезать кромку")
    p.add_argument("--defringe-threshold", type=int, default=205)
    p.add_argument("--defringe-iterations", type=int, default=2)


def _clean_kwargs(a):
    return dict(
        white_threshold=a.white_threshold,
        fill_pockets=not a.no_pockets,
        pocket_min_area=a.pocket_min_area,
        defringe=not a.no_defringe,
        defringe_threshold=a.defringe_threshold,
        defringe_iterations=a.defringe_iterations,
    )


def main(argv=None):
    ap = argparse.ArgumentParser(prog="spritekit", description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = ap.add_subparsers(dest="cmd", required=True)

    c = sub.add_parser("clean", help="вырезать фон у одной картинки")
    c.add_argument("src")
    c.add_argument("dst")
    _add_clean_opts(c)

    s = sub.add_parser("sheet", help="разрезать лист и собрать спрайты")
    s.add_argument("sheet")
    s.add_argument("--names", nargs="+", required=True, help="имена слева направо")
    s.add_argument("--out-dir", required=True)
    s.add_argument("--size", type=int, default=256)
    s.add_argument("--factor", type=int, default=2)
    s.add_argument("--align", choices=("feet", "head", "bbox"), default="feet")
    g = s.add_mutually_exclusive_group()
    g.add_argument("--normalize", type=int, help="нормализовать высоту фигуры к N px")
    g.add_argument("--scale", type=float, help="фикс. масштаб src->out (присед/наклон)")
    _add_clean_opts(s)

    a = ap.parse_args(argv)

    if a.cmd == "clean":
        out = remove_background(Image.open(a.src), **_clean_kwargs(a))
        out.save(a.dst)
        print(f"wrote {a.dst}  {out.size}")
        return

    sheet = Image.open(a.sheet)
    bands = find_figures(sheet, white_threshold=a.white_threshold)
    if len(bands) != len(a.names):
        raise SystemExit(f"нашёл {len(bands)} фигур, а имён {len(a.names)}: {bands}")
    out_dir = Path(a.out_dir)
    out_dir.mkdir(parents=True, exist_ok=True)
    for (x0, x1), name in zip(bands, a.names):
        fig = remove_background(sheet.crop((x0, 0, x1, sheet.height)), **_clean_kwargs(a))
        canvas = place_on_canvas(fig, size=a.size, target_fig_h=a.normalize,
                                 scale=a.scale, factor=a.factor, align=a.align)
        dst = out_dir / f"{name}.png"
        canvas.save(dst)
        print(f"wrote {dst}  band {x0}-{x1}")


if __name__ == "__main__":
    main()
