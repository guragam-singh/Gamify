from io import BytesIO

import pytest
from pypdf import PdfWriter
from pypdf.generic import DictionaryObject, NameObject, NumberObject, StreamObject

from document_parser import DocumentParseError, clean_text, extract_text, ocr_available, parse_document


def make_pdf(text: str) -> bytes:
    writer = PdfWriter()
    page = writer.add_blank_page(width=300, height=200)
    font = DictionaryObject(
        {
            NameObject("/Type"): NameObject("/Font"),
            NameObject("/Subtype"): NameObject("/Type1"),
            NameObject("/BaseFont"): NameObject("/Helvetica"),
        }
    )
    page[NameObject("/Resources")] = DictionaryObject(
        {NameObject("/Font"): DictionaryObject({NameObject("/F1"): font})}
    )
    stream = StreamObject()
    stream._data = f"BT /F1 12 Tf 20 100 Td ({text}) Tj ET".encode("latin-1")
    stream[NameObject("/Length")] = NumberObject(len(stream._data))
    page[NameObject("/Contents")] = writer._add_object(stream)
    output = BytesIO()
    writer.write(output)
    return output.getvalue()


def test_extracts_txt():
    assert extract_text("notes.txt", b"  Photosynthesis uses light.  ") == "Photosynthesis uses light."


def test_extracts_pdf():
    assert "gravity" in extract_text("notes.pdf", make_pdf("gravity"))


@pytest.mark.parametrize(
    ("filename", "data", "message"),
    [
        ("notes.docx", b"content", "Unsupported file type"),
        ("notes.txt", b"", "empty"),
        ("notes.pdf", b"not a PDF", "invalid"),
        ("notes.pdf", make_pdf(""), "no readable text"),
    ],
)
def test_rejects_invalid_or_empty_documents(filename, data, message):
    with pytest.raises(DocumentParseError, match=message):
        extract_text(filename, data)


def test_rejects_oversized_document():
    with pytest.raises(DocumentParseError, match="too large"):
        extract_text("notes.txt", b"12345", max_bytes=4)


def test_clean_text_joins_hyphenated_and_wrapped_lines():
    raw = "Photo-\nsynthesis happens in\nthe leaves.\n|| ~~ __ ||\nNext sentence."
    assert clean_text(raw) == "Photosynthesis happens in the leaves.\nNext sentence."


def test_image_without_ocr_is_rejected_clearly():
    with pytest.raises(DocumentParseError, match="OCR"):
        parse_document("scan.png", b"not really an image", use_ocr=False)


def _text_image(text: str):
    from PIL import Image, ImageDraw, ImageFont

    image = Image.new("RGB", (900, 160), "white")
    try:
        font = ImageFont.truetype("Arial.ttf", 40)
    except OSError:
        font = ImageFont.load_default(size=40)
    ImageDraw.Draw(image).text((20, 50), text, fill="black", font=font)
    return image


@pytest.mark.skipif(not ocr_available(), reason="Tesseract is not installed")
def test_ocr_reads_png_image():
    buffer = BytesIO()
    _text_image("Mitochondria release energy").save(buffer, format="PNG")
    parsed = parse_document("photo.png", buffer.getvalue())
    assert parsed.kind == "image"
    assert "mitochondria" in parsed.text.lower()


@pytest.mark.skipif(not ocr_available(), reason="Tesseract is not installed")
def test_ocr_reads_scanned_pdf_page():
    buffer = BytesIO()
    _text_image("Gravity pulls objects toward Earth").save(buffer, format="PDF", resolution=150)
    parsed = parse_document("scan.pdf", buffer.getvalue())
    assert parsed.ocr_pages == 1
    assert "gravity" in parsed.text.lower()
