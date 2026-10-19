"""Small, in-memory document parsing helpers for the quiz MVP.

PDFs are read through their text layer first. Pages that have little or no
text (scans, photographed worksheets) are rendered and passed through
Tesseract OCR when it is installed. Images (PNG/JPG/WEBP/...) go straight to
OCR. Files stay in memory and are never written to disk.
"""

from __future__ import annotations

import os
import re
import shutil
from dataclasses import dataclass
from io import BytesIO
from pathlib import Path, PurePath

from pypdf import PdfReader

try:  # OCR is optional: the API still works for text PDFs and TXT without it.
    import pytesseract
    from PIL import Image, ImageOps
except ImportError:  # pragma: no cover - depends on the environment
    pytesseract = None
    Image = None

try:
    import pypdfium2
except ImportError:  # pragma: no cover - depends on the environment
    pypdfium2 = None


def _init_tesseract():
    if pytesseract is None:
        return
    custom_path = os.getenv("TESSERACT_PATH", "").strip()
    candidates = [
        custom_path,
        Path(__file__).resolve().parent / "tesseract_bin" / "tesseract.exe",
        Path(__file__).resolve().parent / "tesseract" / "tesseract.exe",
        r"C:\Program Files\Tesseract-OCR\tesseract.exe",
    ]
    for candidate in candidates:
        if candidate and os.path.isfile(str(candidate)):
            pytesseract.pytesseract.tesseract_cmd = str(candidate)
            tessdata_cand = Path(candidate).parent / "tessdata"
            if tessdata_cand.is_dir() and "TESSDATA_PREFIX" not in os.environ:
                os.environ["TESSDATA_PREFIX"] = str(tessdata_cand)
            break

_init_tesseract()


class DocumentParseError(ValueError):
    """Raised when an uploaded document cannot be accepted or read."""


TEXT_EXTENSIONS = {".txt", ".md"}
IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff"}
SUPPORTED_EXTENSIONS = {".pdf"} | TEXT_EXTENSIONS | IMAGE_EXTENSIONS
DEFAULT_MAX_BYTES = 10 * 1024 * 1024

# A page with fewer characters than this in its text layer is treated as scanned.
MIN_PAGE_TEXT_CHARS = 40
# OCR is slow (~1s/page); cap it so a 200-page scan cannot stall a request.
MAX_OCR_PAGES = 20
OCR_RENDER_DPI = 300
TESSERACT_CONFIG = "--oem 1 --psm 3"


@dataclass
class ParsedDocument:
    text: str
    kind: str  # "pdf", "text" or "image"
    pages: int = 1
    ocr_pages: int = 0
    ocr_skipped_pages: int = 0


def ocr_available() -> bool:
    if pytesseract is None or Image is None:
        return False
    _init_tesseract()
    cmd = pytesseract.pytesseract.tesseract_cmd
    return bool((cmd and os.path.isfile(cmd)) or shutil.which(cmd) or shutil.which("tesseract"))



def _max_size_label(max_bytes: int) -> str:
    return f"{max_bytes / (1024 * 1024):g} MB"


def _prepare_for_ocr(image):
    """Light preprocessing that measurably helps Tesseract on photos and scans."""

    image = ImageOps.exif_transpose(image)  # phone photos are often rotated via EXIF
    if image.mode in ("RGBA", "LA", "P"):
        # Flatten transparency onto white; transparent pixels otherwise read as black.
        image = image.convert("RGBA")
        background = Image.new("RGBA", image.size, (255, 255, 255, 255))
        image = Image.alpha_composite(background, image)
    image = ImageOps.grayscale(image)
    # Tesseract is most accurate when capital letters are ~30px tall; upscale small images.
    shortest = min(image.size)
    if shortest < 1000:
        scale = min(3.0, 1000 / max(shortest, 1))
        image = image.resize((int(image.width * scale), int(image.height * scale)), Image.LANCZOS)
    # Measured on noisy photos: median filtering, sharpening and hard binarisation
    # all lowered accuracy versus plain autocontrast, so they are deliberately absent.
    return ImageOps.autocontrast(image, cutoff=1)


# Lines Tesseract is this unsure about are almost always noise (stains, borders, photos).
MIN_LINE_CONFIDENCE = 45
_OCR_FIXES = [
    (re.compile(r"(?<![\w])1s(?![\w])"), "is"),
    (re.compile(r"(?<![\w])1n(?![\w])"), "in"),
    (re.compile(r"(?<![\w])0f(?![\w])"), "of"),
    (re.compile(r"(?<=[a-z])0(?=[a-z])"), "o"),
    (re.compile(r"(?<=[a-z])1(?=[a-z])"), "l"),
    (re.compile(r"[\u2018\u2019]"), "'"),
    (re.compile(r"[\u201c\u201d]"), '"'),
]


def _ocr_image(image) -> str:
    """OCR an image, keeping only lines Tesseract is reasonably confident about."""

    data = pytesseract.image_to_data(
        _prepare_for_ocr(image), lang="eng", config=TESSERACT_CONFIG, output_type=pytesseract.Output.DICT
    )
    lines: dict[tuple[int, int, int], list[tuple[str, float]]] = {}
    for i, word in enumerate(data["text"]):
        if not word.strip():
            continue
        key = (data["block_num"][i], data["par_num"][i], data["line_num"][i])
        lines.setdefault(key, []).append((word, float(data["conf"][i])))

    output: list[str] = []
    previous_block = None
    for key in sorted(lines):
        words = lines[key]
        confidence = sum(conf for _word, conf in words) / len(words)
        if confidence < MIN_LINE_CONFIDENCE:
            continue
        if previous_block is not None and key[:2] != previous_block:
            output.append("")  # paragraph break
        previous_block = key[:2]
        output.append(" ".join(word for word, _conf in words))
    text = "\n".join(output)
    for pattern, replacement in _OCR_FIXES:
        text = pattern.sub(replacement, text)
    return text


_HYPHEN_BREAK = re.compile(r"(\w)-\n(\w)")
_SINGLE_NEWLINE = re.compile(r"(?<![\n.!?:;])\n(?!\n|\s*[-•*\d]+[.)]?\s)")


def clean_text(text: str) -> str:
    """Normalise extracted text so sentences survive for question generation.

    Joins words hyphenated across line breaks, merges hard-wrapped lines inside
    paragraphs, and drops OCR noise lines that are mostly symbols.
    """

    text = text.replace("\r\n", "\n").replace("\r", "\n").replace("\x0c", "\n\n")
    text = text.replace("ﬁ", "fi").replace("ﬂ", "fl").replace("­", "")
    kept = []
    for line in text.split("\n"):
        stripped = line.strip()
        if stripped:
            letters = sum(ch.isalnum() for ch in stripped)
            if letters / len(stripped) < 0.5 and len(stripped) > 2:
                continue  # e.g. "|| ~~ _-_" from table borders or speckle
        kept.append(stripped)
    text = "\n".join(kept)
    text = _HYPHEN_BREAK.sub(r"\1\2", text)
    text = _SINGLE_NEWLINE.sub(" ", text)
    text = re.sub(r"[ \t]{2,}", " ", text)
    text = re.sub(r"\n{3,}", "\n\n", text)
    return text.strip()


def _ocr_pdf_page(pypdf_page, pdfium_page) -> str:
    """OCR one scanned page.

    A scanned page is usually a single embedded image. OCR-ing that image at its
    native resolution beats re-rendering the page (which resamples it), so try
    that first and fall back to rendering the whole page.
    """

    try:
        images = [item.image for item in pypdf_page.images]
        images = [image for image in images if image is not None and min(image.size) >= 200]
        if images:
            largest = max(images, key=lambda image: image.width * image.height)
            text = _ocr_image(largest)
            if len(text.strip()) >= MIN_PAGE_TEXT_CHARS:
                return text
    except Exception:
        pass
    if pdfium_page is None:
        return ""
    try:
        return _ocr_image(pdfium_page.render(scale=OCR_RENDER_DPI / 72).to_pil())
    except Exception:
        return ""


def _parse_pdf(data: bytes, use_ocr: bool) -> ParsedDocument:
    try:
        reader = PdfReader(BytesIO(data))
        page_texts = [page.extract_text() or "" for page in reader.pages]
    except Exception as exc:  # pypdf has several parser-specific exceptions.
        raise DocumentParseError("The PDF could not be read or is invalid.") from exc

    result = ParsedDocument(text="", kind="pdf", pages=len(page_texts))
    weak_pages = [i for i, text in enumerate(page_texts) if len(text.strip()) < MIN_PAGE_TEXT_CHARS]
    if weak_pages and use_ocr:
        pdf = None
        if pypdfium2 is not None:
            try:
                pdf = pypdfium2.PdfDocument(data)
            except Exception:
                pdf = None
        for index in weak_pages[:MAX_OCR_PAGES]:
            ocr_text = _ocr_pdf_page(reader.pages[index], pdf[index] if pdf is not None else None)
            if len(ocr_text.strip()) > len(page_texts[index].strip()):
                page_texts[index] = ocr_text
                result.ocr_pages += 1
        result.ocr_skipped_pages = max(0, len(weak_pages) - MAX_OCR_PAGES)
        if pdf is not None:
            pdf.close()

    result.text = clean_text("\n\n".join(page_texts))
    return result


def parse_document(
    filename: str,
    data: bytes,
    max_bytes: int = DEFAULT_MAX_BYTES,
    *,
    use_ocr: bool | None = None,
) -> ParsedDocument:
    """Validate *data* and return readable text plus extraction details."""

    if not filename:
        raise DocumentParseError("A file name is required.")

    extension = PurePath(filename).suffix.lower()
    if extension not in SUPPORTED_EXTENSIONS:
        raise DocumentParseError("Unsupported file type. Upload a PDF, TXT, or image (PNG/JPG).")
    if not isinstance(data, bytes):
        raise DocumentParseError("The uploaded file could not be read.")
    if len(data) > max_bytes:
        raise DocumentParseError(f"The file is too large. The maximum size is {_max_size_label(max_bytes)}.")
    if not data:
        raise DocumentParseError("The uploaded file is empty.")

    if use_ocr is None:
        use_ocr = ocr_available()

    if extension in TEXT_EXTENSIONS:
        try:
            parsed = ParsedDocument(text=clean_text(data.decode("utf-8-sig")), kind="text")
        except UnicodeDecodeError as exc:
            raise DocumentParseError("The TXT file must be valid UTF-8 text.") from exc
    elif extension in IMAGE_EXTENSIONS:
        if not use_ocr:
            raise DocumentParseError("Image uploads need OCR, which is not installed on this server.")
        try:
            image = Image.open(BytesIO(data))
            image.load()
        except Exception as exc:
            raise DocumentParseError("The image could not be read or is invalid.") from exc
        try:
            text = _ocr_image(image)
        except Exception as exc:
            raise DocumentParseError("OCR failed on this image.") from exc
        parsed = ParsedDocument(text=clean_text(text), kind="image", ocr_pages=1)
    else:
        parsed = _parse_pdf(data, use_ocr)

    if not parsed.text:
        if parsed.kind == "pdf":
            hint = "even after OCR" if use_ocr else "and OCR is not installed on this server"
            raise DocumentParseError(f"The PDF contains no readable text ({hint}).")
        if parsed.kind == "image":
            raise DocumentParseError("No readable text was found in the image. Try a sharper, well-lit photo.")
        raise DocumentParseError("The uploaded TXT file contains no readable text.")
    return parsed


def extract_text(filename: str, data: bytes, max_bytes: int = DEFAULT_MAX_BYTES, *, use_ocr: bool | None = None) -> str:
    """Backwards-compatible helper returning only the extracted text."""

    return parse_document(filename, data, max_bytes, use_ocr=use_ocr).text
