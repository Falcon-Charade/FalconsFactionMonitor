# resourceTranslator.py (robust version with retries & fallbacks)
# Usage examples:
#   pip install deep-translator requests
#   python resourceTranslator.py --default StringResources.default.xaml --langs af de es fr it ja ru zh --provider auto
#   python resourceTranslator.py --default StringResources.default.xaml --langs af de es fr it ja ru zh --provider google --overwrite
#   python resourceTranslator.py --default StringResources.default.xaml --langs zh --provider auto --overwrite
#   # Azure (recommended for reliability): set env vars AZURE_TRANSLATOR_KEY, AZURE_TRANSLATOR_REGION, AZURE_TRANSLATOR_ENDPOINT

import os
import re
import json
import time
import argparse
from pathlib import Path

# Keys to skip translating (endonyms for language selection)
SKIP_KEY_REGEX = re.compile(r'^Options_LanguageOption_')

# Manual per-string overrides (use EN text as keys; values per target lang)
MANUAL_OVERRIDES = {
    "Journal Monitor": {
        "af": "Joernaalmonitering",
        "de": "Journalüberwachung",
        "es": "Monitor de registro",
        "fr": "Surveillance du journal",
        "it": "Monitor del registro",
        "ja": "ジャーナル監視",
        "ru": "Мониторинг журнала",
        "zh": "日志监视器",
    },
    "Journal Monitor Service": {
        "af": "Joernaalmoniteringsdiens",
        "de": "Journalüberwachungsdienst",
        "es": "Servicio del monitor de registro",
        "fr": "Service de surveillance du journal",
        "it": "Servizio di monitor del registro",  # keep consistent with earlier "Monitor del registro"
        "ja": "ジャーナル監視サービス",
        "ru": "Служба мониторинга журнала",
        "zh": "日志监视服务",
    }
    # Add more stubborn terms here if needed:
    # "Journal Monitor Output Log": {
    #     "af": "Joernaalmonitering Uitsetlogboek",
    #     "de": "Ausgabeprotokoll der Journalüberwachung",
    #     "es": "Registro de salida del monitor de registro",
    #     "fr": "Journal de sortie de la surveillance du journal",
    #     "it": "Registro di output del monitor del registro",
    #     "ja": "ジャーナル監視の出力ログ",
    #     "ru": "Журнал вывода мониторинга журнала",
    #     "zh": "日志监视器输出日志",
    # },
}


# EXACT value-only replace between <prefix:String ...> ... </prefix:String>
STRING_TAG_RE = re.compile(
    r'(<(?P<prefix>[A-Za-z_][\w\-.]*):String\b[^>]*>)(?P<inner>.*?)(</(?P=prefix):String>)',
    re.DOTALL
)

# Protect placeholders/entities
PLACEHOLDER_PATTERNS = [
    r'\{\{', r'\}\}',
    r'\{[0-9]+(?:[^{}]*)\}',            # {0}, {1:N2}, {Name}
    r'%\d+\$?[sdif]', r'%[sdif]',       # printf
    r'&[a-zA-Z]+;', r'&#\d+;', r'&#x[0-9A-Fa-f]+;',  # XML entities
]
PROTECTED_RE = re.compile('(' + '|'.join(PLACEHOLDER_PATTERNS) + ')')

def normalize_lang_for_provider(provider: str, lang: str) -> str:
    L = lang.lower()
    # Simplified Chinese
    if L in ("zh", "zh_cn", "zh-cn", "zh-hans"):
        return "zh-Hans" if provider == "azure" else "zh-CN"
    # Traditional Chinese
    if L in ("zh_tw", "zh-tw", "zh-hant"):
        return "zh-Hant" if provider == "azure" else "zh-TW"
    return lang

def protect_tokens(text: str):
    token_map = []
    def repl(m):
        idx = len(token_map)
        token_map.append(m.group(0))
        return f'__TKN{idx}__'
    return PROTECTED_RE.sub(repl, text), token_map

def restore_tokens(text: str, token_map):
    for idx, tok in enumerate(token_map):
        text = text.replace(f'__TKN{idx}__', tok)
    return text

# ---------------- Translation providers ----------------
def try_google(text, target_lang):
    from deep_translator import GoogleTranslator
    gt = GoogleTranslator(source='en', target=target_lang)
    return gt.translate(text)

def try_mymemory(text, target_lang):
    from deep_translator import MyMemoryTranslator
    # MyMemory uses ISO codes; 'zh' -> 'zh-CN' is OK; leave as-is
    mt = MyMemoryTranslator(source='en', target=target_lang)
    return mt.translate(text)

def try_azure_batch(lines, target_lang):
    import requests
    endpoint = os.environ.get('AZURE_TRANSLATOR_ENDPOINT')
    key = os.environ.get('AZURE_TRANSLATOR_KEY')
    region = os.environ.get('AZURE_TRANSLATOR_REGION')
    if not endpoint or not key or not region:
        raise RuntimeError("Azure env vars missing.")
    url = f"{endpoint.rstrip('/')}/translate?api-version=3.0&to={target_lang}"
    headers = {
        "Ocp-Apim-Subscription-Key": key,
        "Ocp-Apim-Subscription-Region": region,
        "Content-Type": "application/json",
    }
    body = [{"Text": s} for s in lines]
    r = requests.post(url, headers=headers, data=json.dumps(body), timeout=30)
    r.raise_for_status()
    data = r.json()
    out = []
    for i, item in enumerate(data):
        try:
            out.append(item['translations'][0]['text'])
        except Exception:
            out.append(lines[i])
    return out

def _allow_unchanged(text: str) -> bool:
    s = text.strip()

    # Common cases that are often identical across languages
    NONTRANSLATABLE_COLORS = {
        "Indigo", "Amber", "Lime", "Teal", "Cyan", "Magenta", "Fuchsia", "Aqua",
        "Navy", "Olive", "Maroon", "Silver", "Gold", "Bronze", "Khaki", "Taupe",
        "Beige", "Ivory", "Coral", "Salmon", "Lavender", "Lilac", "Turquoise",
        "Charcoal", "Sienna", "Tan", "Burgundy", "Ruby", "Sapphire", "Emerald",
        "Onyx", "Slate", "Plum", "Mint", "Peach", "Apricot", "Chestnut",
        "Mustard", "Azure", "Gray", "Grey", "Blue Grey", "Deep Purple", "Deep Orange"
    }

    # 1) Exact color names
    if s in NONTRANSLATABLE_COLORS:
        return True

    # 2) Very short tokens or pure numbers often don't translate
    if len(s) <= 3:
        return True
    if re.fullmatch(r'\d+(\.\d+)?', s):  # numbers
        return True

    # 3) Acronyms / codes / filenames (e.g., "ABN", "CSV", "v1.2.3")
    if re.fullmatch(r'[A-Z0-9._-]+', s):
        return True

    # 4) Single “word-ish” tokens (proper nouns often stay the same)
    if re.fullmatch(r'[A-Za-z][A-Za-z0-9_-]*', s):
        return True

    # Otherwise, we expect a translation
    return False


def robust_translate_one(text, target_lang, order=("azure","google","mymemory"), max_retries=4):
    if not text.strip():
        return text

    last_err = None
    for provider in order:
        norm_lang = normalize_lang_for_provider(provider, target_lang)
        for attempt in range(max_retries):
            try:
                if provider == "azure":
                    res = try_azure_batch([text], norm_lang)[0]
                elif provider == "google":
                    res = try_google(text, norm_lang)
                elif provider == "mymemory":
                    res = try_mymemory(text, norm_lang)
                else:
                    continue

                if res.strip() == text.strip():
                    if _allow_unchanged(text):
                        return res
                    raise RuntimeError(f"{provider} returned unchanged text")

                return res
            except Exception as e:
                last_err = e
                time.sleep(0.25 * (attempt + 1))
    raise RuntimeError(f"All providers failed for text: {text!r}; last error: {last_err}")


# -------------------------------------------------------
def translate_values_preserve_format(src_text: str, target_lang: str, mode: str):
    order = ("azure","google","mymemory") if mode == "auto" else ((mode,) if mode in ("azure","google") else ("google","mymemory"))

    matches = list(STRING_TAG_RE.finditer(src_text))
    out = []
    last = 0
    for m in matches:
        prefix = m.group(1)
        inner  = m.group('inner')
        suffix = m.group(4)

        # detect x:Key
        key_match = re.search(r'\bx:Key\s*=\s*"([^"]+)"', prefix)
        key = key_match.group(1) if key_match else None

        # Skip language-option endonyms entirely
        if key and SKIP_KEY_REGEX.match(key):
            out.append(src_text[last:m.start()])
            out.append(prefix + inner + suffix)
            last = m.end()
            continue

        # Preserve whitespace around core
        leading_ws = re.match(r'^\s*', inner, re.DOTALL).group(0)
        trailing_ws_match = re.search(r'\s*$', inner, re.DOTALL)
        trailing_ws = trailing_ws_match.group(0) if trailing_ws_match else ""
        core = inner[len(leading_ws):len(inner)-len(trailing_ws)] if len(inner) >= len(leading_ws)+len(trailing_ws) else inner.strip()

        # --- NEW: manual override first (compare against raw core, no token protection needed here)
        mo = MANUAL_OVERRIDES.get(core)
        if mo and target_lang in mo:
            translated_core = mo[target_lang]
            out.append(src_text[last:m.start()])
            out.append(prefix + leading_ws + translated_core + trailing_ws + suffix)
            last = m.end()
            continue
        # --- END NEW

        protected, token_map = protect_tokens(core)

        # Robust translation with fallbacks (your existing function)
        translated_core = robust_translate_one(protected, target_lang, order=order, max_retries=4)
        restored = restore_tokens(translated_core, token_map)

        out.append(src_text[last:m.start()])
        out.append(prefix + leading_ws + restored + trailing_ws + suffix)
        last = m.end()

    out.append(src_text[last:])
    return ''.join(out)

def write_bytes_like_source(out_path: Path, new_text: str, source_bytes: bytes):
    has_bom = source_bytes.startswith(b'\xef\xbb\xbf')
    out_bytes = new_text.encode("utf-8-sig" if has_bom else "utf-8")
    out_path.write_bytes(out_bytes)

def main():
    ap = argparse.ArgumentParser(description="Translate from StringResources.default.xaml -> en + other languages, preserving exact XML formatting.")
    ap.add_argument("--default", required=True, help="Path to StringResources.default.xaml")
    ap.add_argument("--langs", nargs="+", required=True, help="Target langs, e.g. af de es fr it ja ru zh")
    ap.add_argument("--provider", choices=["auto","azure","google"], default="auto", help="auto=azure(if configured)->google->mymemory")
    ap.add_argument("--overwrite", action="store_true", help="Overwrite existing outputs")
    args = ap.parse_args()

    default_path = Path(args.default)
    if not default_path.exists():
        raise FileNotFoundError(f"Default file not found: {default_path}")

    src_bytes = default_path.read_bytes()
    try:
        src_text = src_bytes.decode("utf-8")
    except UnicodeDecodeError:
        src_text = src_bytes.decode("utf-8-sig")

    # 1) Create EN as direct copy (verbatim)
    en_path = default_path.with_name("StringResources.en.xaml")
    if en_path.exists() and not args.overwrite:
        print(f"EN exists, skipping copy (use --overwrite to replace): {en_path}")
    else:
        en_path.write_bytes(src_bytes)
        print(f"Created EN copy: {en_path}")

    # 2) Translate for each requested language
    for lang in args.langs:
        out_path = default_path.with_name(f"StringResources.{lang}.xaml")
        if out_path.exists() and not args.overwrite:
            print(f"Exists, skipping: {out_path}")
            continue
        print(f"Translating -> {lang} : {out_path}")
        new_text = translate_values_preserve_format(src_text, lang, args.provider)
        write_bytes_like_source(out_path, new_text, src_bytes)
        print(f"Saved: {out_path}")

if __name__ == "__main__":
    main()
