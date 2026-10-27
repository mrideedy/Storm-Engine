"""
Builds the Google Play store art from raw in-game captures (Tools/marketing/raw):
  store/screenshots/<lang>/0N_<name>.png   1080x1920 phone screenshots with a caption banner
  store/feature_graphic.png                1024x500 feature graphic
Captions are written for every language the game ships in; Play shows the set that matches the
listing language (and falls back to English).

Raw captures come from the game at 1080x1920 (see Docs/PlayStore/CHECKLIST.md, "Store art").
Usage: python Tools/marketing/make_store_art.py
"""
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
RAW = os.path.join(HERE, 'raw')
OUT = os.path.join(HERE, 'store')
FONT_BLACK = os.path.join(HERE, 'fonts', 'Roboto-Black.ttf')
FONT_BOLD = os.path.join(HERE, 'fonts', 'Roboto-Bold.ttf')
THAI_FONTS = [r'C:\Windows\Fonts\LeelaUIb.ttf', r'C:\Windows\Fonts\leelawdb.ttf', r'C:\Windows\Fonts\tahomabd.ttf']

W, H = 1080, 1920
INK = (20, 32, 58)
TOP, BOTTOM = (86, 196, 255), (88, 52, 214)

# Order on the store page: the most exciting moment first.
SHOTS = ['2_storm', '1_machines', '3_island', '5_tournament', '4_styles', '6_daily']

CAPTIONS = {
    'en': {
        '2_storm': ('UNLEASH EPIC STORMS', 'Chain lightning into giant combos'),
        '1_machines': ('BUILD YOUR ENGINE', 'Upgrade pumps, coils, furnaces & turbines'),
        '3_island': ('DECORATE YOUR ISLAND', 'Trees, ponds, balloons and more'),
        '5_tournament': ('COMPETE EVERY WEEK', 'Climb the global leaderboards'),
        '4_styles': ('STYLE IT YOUR WAY', 'Skins, clouds, bolts & worlds with bonuses'),
        '6_daily': ('DAILY GIFTS & MISSIONS', 'Keep your streak for bigger rewards'),
    },
    'es': {
        '2_storm': ('¡DESATA TORMENTAS ÉPICAS!', 'Encadena rayos en combos gigantes'),
        '1_machines': ('CONSTRUYE TU MOTOR', 'Mejora bombas, bobinas, hornos y turbinas'),
        '3_island': ('DECORA TU ISLA', 'Árboles, estanques, globos y más'),
        '5_tournament': ('COMPITE CADA SEMANA', 'Escala las clasificaciones mundiales'),
        '4_styles': ('DALE TU ESTILO', 'Aspectos, nubes, rayos y mundos con bonos'),
        '6_daily': ('REGALOS Y MISIONES DIARIAS', 'Mantén tu racha para más premios'),
    },
    'pt': {
        '2_storm': ('LIBERE TEMPESTADES ÉPICAS', 'Encadeie raios em combos gigantes'),
        '1_machines': ('CONSTRUA SEU MOTOR', 'Melhore bombas, bobinas, fornos e turbinas'),
        '3_island': ('DECORE SUA ILHA', 'Árvores, lagos, balões e muito mais'),
        '5_tournament': ('COMPITA TODA SEMANA', 'Suba nos rankings mundiais'),
        '4_styles': ('DO SEU JEITO', 'Visuais, nuvens, raios e mundos com bônus'),
        '6_daily': ('PRESENTES E MISSÕES DIÁRIAS', 'Mantenha a sequência e ganhe mais'),
    },
    'fr': {
        '2_storm': ('DÉCHAÎNE LA TEMPÊTE !', 'Enchaîne les éclairs en combos géants'),
        '1_machines': ('CONSTRUIS TON MOTEUR', 'Améliore pompes, bobines, fours et turbines'),
        '3_island': ('DÉCORE TON ÎLE', 'Arbres, mares, montgolfières et plus'),
        '5_tournament': ('AFFRONTE LE MONDE', 'Grimpe dans les classements chaque semaine'),
        '4_styles': ('À TON STYLE', 'Skins, nuages, éclairs et mondes à bonus'),
        '6_daily': ('CADEAUX ET MISSIONS', 'Garde ta série pour plus de récompenses'),
    },
    'de': {
        '2_storm': ('ENTFESSLE EPISCHE STÜRME', 'Blitzketten für riesige Kombos'),
        '1_machines': ('BAU DEINEN MOTOR', 'Pumpen, Spulen, Öfen und Turbinen'),
        '3_island': ('GESTALTE DEINE INSEL', 'Bäume, Teiche, Ballons und mehr'),
        '5_tournament': ('JEDE WOCHE TURNIER', 'Steig in den weltweiten Ranglisten auf'),
        '4_styles': ('DEIN STIL', 'Skins, Wolken, Blitze und Welten mit Boni'),
        '6_daily': ('TÄGLICHE GESCHENKE', 'Halte deine Serie für größere Belohnungen'),
    },
    'it': {
        '2_storm': ('SCATENA TEMPESTE EPICHE', 'Concatena fulmini in combo giganti'),
        '1_machines': ('COSTRUISCI IL TUO MOTORE', 'Potenzia pompe, bobine, forni e turbine'),
        '3_island': ('DECORA LA TUA ISOLA', 'Alberi, laghetti, mongolfiere e altro'),
        '5_tournament': ('SFIDE OGNI SETTIMANA', 'Scala le classifiche mondiali'),
        '4_styles': ('IL TUO STILE', 'Skin, nuvole, fulmini e mondi con bonus'),
        '6_daily': ('REGALI E MISSIONI', 'Mantieni la serie per premi più grandi'),
    },
    'ru': {
        '2_storm': ('ЭПИЧЕСКИЕ БУРИ', 'Цепные молнии и огромные комбо'),
        '1_machines': ('СОБЕРИТЕ ДВИГАТЕЛЬ', 'Насосы, катушки, печи и турбины'),
        '3_island': ('УКРАСЬТЕ ОСТРОВ', 'Деревья, пруды, воздушные шары и не только'),
        '5_tournament': ('ТУРНИРЫ КАЖДУЮ НЕДЕЛЮ', 'Поднимайтесь в мировых рейтингах'),
        '4_styles': ('ВАШ СТИЛЬ', 'Облики, облака, молнии и миры с бонусами'),
        '6_daily': ('ЕЖЕДНЕВНЫЕ ПОДАРКИ', 'Не прерывайте серию и получайте больше'),
    },
    'tr': {
        '2_storm': ('EFSANE FIRTINALAR KOPAR', 'Şimşekleri dev kombolara bağla'),
        '1_machines': ('MOTORUNU KUR', 'Pompaları, bobinleri, fırınları geliştir'),
        '3_island': ('ADANI SÜSLE', 'Ağaçlar, göletler, balonlar ve fazlası'),
        '5_tournament': ('HER HAFTA YARIŞ', 'Dünya sıralamasında yüksel'),
        '4_styles': ('KENDİ TARZIN', 'Bonuslu görünümler, bulutlar ve dünyalar'),
        '6_daily': ('GÜNLÜK HEDİYELER', 'Serini koru, daha büyük ödüller kazan'),
    },
    'pl': {
        '2_storm': ('ROZPĘTAJ EPICKIE BURZE', 'Łącz pioruny w gigantyczne kombo'),
        '1_machines': ('ZBUDUJ SWÓJ SILNIK', 'Ulepszaj pompy, cewki, piece i turbiny'),
        '3_island': ('UDEKORUJ WYSPĘ', 'Drzewa, stawy, balony i nie tylko'),
        '5_tournament': ('RYWALIZUJ CO TYDZIEŃ', 'Wspinaj się w światowych rankingach'),
        '4_styles': ('TWÓJ STYL', 'Skórki, chmury, pioruny i światy z bonusami'),
        '6_daily': ('CODZIENNE PREZENTY', 'Utrzymaj serię, by zdobywać więcej'),
    },
    'id': {
        '2_storm': ('LEPASKAN BADAI EPIK', 'Rangkai petir jadi kombo raksasa'),
        '1_machines': ('BANGUN MESINMU', 'Tingkatkan pompa, kumparan, tungku & turbin'),
        '3_island': ('HIAS PULAUMU', 'Pohon, kolam, balon, dan lainnya'),
        '5_tournament': ('BERTANDING TIAP MINGGU', 'Naiki papan peringkat dunia'),
        '4_styles': ('GAYAMU SENDIRI', 'Skin, awan, petir & dunia dengan bonus'),
        '6_daily': ('HADIAH & MISI HARIAN', 'Jaga beruntunmu untuk hadiah lebih besar'),
    },
    'vi': {
        '2_storm': ('THẢ NHỮNG CƠN BÃO KHỦNG', 'Nối chuỗi sét thành combo khổng lồ'),
        '1_machines': ('XÂY ĐỘNG CƠ CỦA BẠN', 'Nâng cấp bơm, cuộn Tesla, lò và tua-bin'),
        '3_island': ('TRANG TRÍ HÒN ĐẢO', 'Cây, ao, khinh khí cầu và nhiều nữa'),
        '5_tournament': ('THI ĐẤU MỖI TUẦN', 'Leo bảng xếp hạng toàn cầu'),
        '4_styles': ('PHONG CÁCH RIÊNG', 'Diện mạo, mây, tia sét và thế giới có thưởng'),
        '6_daily': ('QUÀ & NHIỆM VỤ HẰNG NGÀY', 'Giữ chuỗi ngày để nhận thưởng lớn hơn'),
    },
    'th': {
        '2_storm': ('ปล่อยพายุสุดมันส์', 'สายฟ้าต่อเนื่องเป็นคอมโบยักษ์'),
        '1_machines': ('สร้างเครื่องยนต์ของคุณ', 'อัปเกรดปั๊ม ขดลวด เตา และกังหัน'),
        '3_island': ('ตกแต่งเกาะของคุณ', 'ต้นไม้ บ่อน้ำ บอลลูน และอีกมากมาย'),
        '5_tournament': ('แข่งขันทุกสัปดาห์', 'ไต่อันดับกระดานผู้นำระดับโลก'),
        '4_styles': ('สไตล์ของคุณเอง', 'สกิน เมฆ สายฟ้า และโลกพร้อมโบนัส'),
        '6_daily': ('ของขวัญและภารกิจรายวัน', 'รักษาสตรีคเพื่อรางวัลที่ใหญ่ขึ้น'),
    },
}


def gradient(w, h):
    col = Image.new('RGB', (1, 256))
    for y in range(256):
        t = (y / 255.0) ** 1.15
        col.putpixel((0, y), tuple(int(TOP[i] + (BOTTOM[i] - TOP[i]) * t) for i in range(3)))
    img = col.resize((w, h), Image.BICUBIC).convert('RGBA')
    soft = Image.new('RGBA', (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(soft)
    for (x, y, r) in [(0.1, 0.07, 0.16), (0.3, 0.04, 0.12), (0.88, 0.1, 0.15), (0.7, 0.05, 0.1), (0.05, 0.95, 0.2), (0.95, 0.9, 0.22)]:
        d.ellipse(((x - r) * w, (y - r) * w + y * h - y * w, (x + r) * w, (y + r) * w + y * h - y * w), fill=(255, 255, 255, 46))
    soft = soft.filter(ImageFilter.GaussianBlur(w * 0.02))
    return Image.alpha_composite(img, soft)


def font_for(lang, bold_black, size):
    if lang == 'th':
        for path in THAI_FONTS:
            if os.path.exists(path):
                return ImageFont.truetype(path, size)
    return ImageFont.truetype(FONT_BLACK if bold_black else FONT_BOLD, size)


def fit(lang, text, black, max_w, start, minimum):
    size = start
    while size > minimum:
        f = font_for(lang, black, size)
        box = f.getbbox(text)
        if box[2] - box[0] <= max_w:
            return f
        size -= 2
    return font_for(lang, black, minimum)


def outlined(draw, xy, text, font, fill, stroke, width, anchor='mm'):
    draw.text(xy, text, font=font, fill=fill, stroke_width=width, stroke_fill=stroke, anchor=anchor)


def rounded_mask(size, radius):
    m = Image.new('L', size, 0)
    ImageDraw.Draw(m).rounded_rectangle((0, 0, size[0] - 1, size[1] - 1), radius=radius, fill=255)
    return m


def screenshot(lang, name, raw):
    head, sub = CAPTIONS[lang][name]
    img = gradient(W, H)
    d = ImageDraw.Draw(img)

    # Headline: big, white, ink outline; two lines if it does not fit.
    words = head.split(' ')
    font = fit(lang, head, True, 980, 104, 76)
    lines = [head]
    if font.size <= 78 and len(words) > 1:
        half = (len(words) + 1) // 2
        lines = [' '.join(words[:half]), ' '.join(words[half:])]
        font = fit(lang, max(lines, key=len), True, 980, 96, 60)
    y = 118 if len(lines) == 1 else 84
    for line in lines:
        # Soft drop shadow, then the outlined text.
        shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).text((W // 2, y + 8), line, font=font, fill=(20, 32, 58, 110), anchor='mm', stroke_width=8, stroke_fill=(20, 32, 58, 110))
        img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(4)))
        d = ImageDraw.Draw(img)
        outlined(d, (W // 2, y), line, font, (255, 255, 255), INK, 8)
        y += int(font.size * 1.05)

    # Subline in a soft pill.
    sf = fit(lang, sub, False, 900, 46, 30)
    box = sf.getbbox(sub)
    tw = box[2] - box[0]
    py = y + 34
    pill = (W // 2 - tw // 2 - 34, py - 38, W // 2 + tw // 2 + 34, py + 38)
    overlay = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(overlay).rounded_rectangle(pill, radius=38, fill=(20, 32, 58, 150))
    img = Image.alpha_composite(img, overlay)
    d = ImageDraw.Draw(img)
    d.text((W // 2, py), sub, font=sf, fill=(255, 226, 120), anchor='mm')

    # The game screenshot as a rounded card with a white rim and a shadow.
    top = pill[3] + 34
    avail_h = H - top - 40
    scale = min(900 / raw.width, avail_h / raw.height)
    sw, sh = int(raw.width * scale), int(raw.height * scale)
    shot = raw.resize((sw, sh), Image.LANCZOS).convert('RGBA')
    x = (W - sw) // 2
    shadow = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    ImageDraw.Draw(shadow).rounded_rectangle((x - 6, top + 14, x + sw + 6, top + sh + 22), radius=52, fill=(12, 16, 40, 130))
    img = Image.alpha_composite(img, shadow.filter(ImageFilter.GaussianBlur(14)))
    rim = Image.new('RGBA', (sw + 16, sh + 16), (255, 255, 255, 255))
    img.paste(rim, (x - 8, top - 8), rounded_mask(rim.size, 54))
    img.paste(shot, (x, top), rounded_mask((sw, sh), 46))
    return img.convert('RGB')


def feature_graphic():
    """1024x500: the island on the right, the name and slogan on the left."""
    fw, fh = 1024, 500
    raw = Image.open(os.path.join(RAW, 'feature_island.png')).convert('RGBA')
    img = gradient(fw, fh)
    scale = fh / raw.height * 1.08
    iw, ih = int(raw.width * scale), int(raw.height * scale)
    island = raw.resize((iw, ih), Image.LANCZOS)
    img.paste(island, (fw - iw + int(iw * 0.18), (fh - ih) // 2))
    # Fade the left side into the sky so the title reads cleanly.
    fade = Image.new('RGBA', (fw, fh), (0, 0, 0, 0))
    fd = ImageDraw.Draw(fade)
    for x in range(fw):
        t = max(0.0, 1.0 - x / 560.0)
        a = int(235 * min(1.0, t * 1.25))
        fd.line([(x, 0), (x, fh)], fill=(70, 150, 245, a))
    img = Image.alpha_composite(img, fade)
    icon_path = os.path.join(OUT, 'play_icon_512.png')
    if os.path.exists(icon_path):
        icon = Image.open(icon_path).convert('RGBA').resize((150, 150), Image.LANCZOS)
        img.paste(icon, (48, 72), rounded_mask(icon.size, 34))
    d = ImageDraw.Draw(img)
    title = ImageFont.truetype(FONT_BLACK, 88)
    outlined(d, (48, 300), 'STORM', title, (255, 255, 255), INK, 7, anchor='ls')
    outlined(d, (48, 390), 'ENGINE', title, (255, 214, 92), INK, 7, anchor='ls')
    tag = ImageFont.truetype(FONT_BOLD, 34)
    outlined(d, (52, 448), 'Build smart. Storm big.', tag, (255, 255, 255), INK, 4, anchor='ls')
    return img.convert('RGB')


def main():
    raws = {n: Image.open(os.path.join(RAW, n + '.png')).convert('RGB') for n in SHOTS}
    for lang in CAPTIONS:
        folder = os.path.join(OUT, 'screenshots', lang)
        os.makedirs(folder, exist_ok=True)
        for i, name in enumerate(SHOTS):
            screenshot(lang, name, raws[name]).save(os.path.join(folder, '%02d_%s.png' % (i + 1, name.split('_', 1)[1])), optimize=True)
    feature_graphic().save(os.path.join(OUT, 'feature_graphic.png'), optimize=True)
    print('store art written to', OUT)


if __name__ == '__main__':
    main()
