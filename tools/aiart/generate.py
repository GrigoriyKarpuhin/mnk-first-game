"""Вызов OpenAI image API и сохранение СЫРОГО рендера в Design/ai_raw/.

RAW — самый дорогой артефакт (за него платим), поэтому всегда сохраняем его в
полном разрешении; повторная постобработка из RAW бесплатна (см. pipeline.py,
флаг CLI `--raw`). Ключ грузится лениво и НЕ логируется (см. client.py).
"""
import base64
import time
from pathlib import Path

from .client import REPO_ROOT, make_client, has_key
from . import presets

RAW_DIR = REPO_ROOT / "Design" / "ai_raw"
DEFAULT_MODEL = "gpt-image-2"


def _decode(b64):
    return base64.b64decode(b64)


def describe(kind, *, subject=None, extra=None, refs=None, model=DEFAULT_MODEL,
             quality="low", variants=1):
    """Текст для --dry-run: что и как будет сгенерировано, БЕЗ вызова API."""
    spec = presets.PRESETS[kind]
    prompt = presets.build_prompt(kind, subject=subject, extra=extra)
    use_edit = bool(refs)  # любой kind с --ref идёт через images.edit (стиль по референсу)
    lines = [
        "=== DRY RUN (API не вызывается) ===",
        f"kind        : {kind}",
        f"endpoint    : {'images.edit' if use_edit else 'images.generate'}",
        f"model       : {model}",
        f"size        : {spec['size']}",
        f"background  : {spec['background']}",
        f"quality     : {quality}",
        f"variants(n) : {variants}",
        f"out         : {spec['out']}  (final_size={spec['final_size']})",
        f"refs        : {', '.join(refs) if refs else '(нет)'}",
        f"api key     : {'найден' if has_key() else 'НЕ НАЙДЕН (.token / OPENAI_API_KEY)'}",
        "--- prompt ---",
        prompt,
    ]
    return "\n".join(lines)


def generate_raw(kind, *, subject=None, extra=None, refs=None, model=DEFAULT_MODEL,
                 quality="low", variants=1, name=None):
    """Сгенерировать изображение(я) и сохранить RAW в Design/ai_raw/<kind>/.

    Возвращает список путей к сохранённым RAW-PNG (по одному на variant).
    """
    spec = presets.PRESETS[kind]
    prompt = presets.build_prompt(kind, subject=subject, extra=extra)

    params = dict(
        model=model,
        prompt=prompt,
        size=spec["size"],
        n=variants,
        background=spec["background"],
        output_format="png",
    )
    if quality:
        params["quality"] = quality

    client = make_client()
    use_edit = bool(refs)  # любой kind с --ref -> images.edit
    if spec["endpoint"] == "edit" and not refs:
        print("ВНИМАНИЕ: для этого kind рекомендуются --ref (стиль/консистентность); "
              "генерю без референсов через images.generate.")

    def _call(p):
        if use_edit:
            handles = [open(r, "rb") for r in refs]
            try:
                return client.images.edit(image=handles, **p)
            finally:
                for h in handles:
                    h.close()
        return client.images.generate(**p)

    try:
        result = _call(params)
    except Exception as e:  # модель может не принимать параметр background
        if "background" in str(e).lower() and "background" in params:
            print(f"INFO: модель не приняла background={params['background']}, "
                  f"повтор без этого параметра.")
            params.pop("background", None)
            result = _call(params)
        else:
            raise

    usage = getattr(result, "usage", None)
    if usage is not None:
        print(f"usage: {usage}")

    out_dir = RAW_DIR / kind
    out_dir.mkdir(parents=True, exist_ok=True)
    stamp = time.strftime("%Y%m%d-%H%M%S")
    base = name or kind
    paths = []
    for i, item in enumerate(result.data):
        suffix = f"_{i}" if variants > 1 else ""
        dst = out_dir / f"{base}_{stamp}{suffix}.png"
        dst.write_bytes(_decode(item.b64_json))
        print(f"RAW: {dst}")
        paths.append(dst)
    return paths
