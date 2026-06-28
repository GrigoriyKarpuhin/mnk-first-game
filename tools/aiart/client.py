"""Загрузка ключа OpenAI и ленивое создание клиента.

ЕДИНСТВЕННЫЙ файл, который читает `.token`. Ключ НИКОГДА не печатается и не
логируется. Клиент создаётся лениво, чтобы `--dry-run`/`--help` не требовали ни
ключа, ни установленного пакета `openai`.
"""
import os
from pathlib import Path

# tools/aiart/client.py -> tools/aiart -> tools -> <repo root>
REPO_ROOT = Path(__file__).resolve().parent.parent.parent
TOKEN_FILE = REPO_ROOT / ".token"


def _from_file():
    if TOKEN_FILE.exists():
        return TOKEN_FILE.read_text(encoding="utf-8").strip()
    return ""


def has_key():
    """True, если ключ доступен — без раскрытия значения (для --dry-run проверок)."""
    return bool(_from_file() or os.environ.get("OPENAI_API_KEY", "").strip())


def load_key():
    """Ключ из .token (корень репо), иначе из env OPENAI_API_KEY. Значение не логируется."""
    key = _from_file() or os.environ.get("OPENAI_API_KEY", "").strip()
    if not key:
        raise SystemExit(
            "Не найден API-ключ OpenAI. Положи ключ в файл .token в корне "
            "репозитория (он в .gitignore) или задай переменную окружения "
            "OPENAI_API_KEY."
        )
    return key


def make_client():
    """Ленивая инициализация OpenAI(); импорт openai внутри функции."""
    try:
        from openai import OpenAI
    except ImportError as e:
        raise SystemExit(
            "Пакет openai не установлен. Выполни: "
            "python3 -m pip install -r tools/requirements.txt"
        ) from e
    return OpenAI(api_key=load_key())
