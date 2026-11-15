"""Validate the actual cutscene assets, captions and layout configuration."""
import re
import struct
import unittest
from pathlib import Path

from test_integration_assets import table_values


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"
CAPTIONS = [
    "Наш Имбирный городок готовился к ежегодному празднику",
    "Внезапно мистер Имбирный взбесился и схватился за ружье",
    "Я вспомнил, что в одном из подарков лежит телефон, нужно найти его и вызвать полицию",
    "Полиция прибыла очень быстро",
    "Мистер Имбирный отказался бросить ружье, причины его поведения неизвестны до сих пор",
    "Многие пострадали в тот день, это событие оставило рану на нашем городке",
]


class CutsceneAssetsTests(unittest.TestCase):
    def test_each_slide_is_a_separate_full_image_sprite_in_order(self):
        scene = (ASSETS / "Scenes/GameScene.unity").read_text()
        guids = []
        for index in range(1, 7):
            image = ASSETS / "Cutscenes" / ("1.png" if index == 1 else f"{index}.PNG")
            with image.open("rb") as stream:
                header = stream.read(24)
            self.assertEqual(header[:8], b"\x89PNG\r\n\x1a\n")
            self.assertEqual(struct.unpack(">II", header[16:24]), (1920, 1080))
            meta = Path(str(image) + ".meta").read_text()
            guids.append(re.search(r"^guid: (\w+)", meta, re.M)[1])
            for setting in ["spriteMode: 1", "textureType: 8", "nPOTScale: 0", "enableMipMap: 0"]:
                self.assertIn(setting, meta)
            self.assertIn("    sprites: []", meta)
        self.assertEqual(len(set(guids)), 6)
        for field, expected in [("introFrames", guids[:3]), ("victoryFrames", guids[3:])]:
            frames = re.search(rf"  {field}:\n((?:  - .*\n)+)", scene)[1]
            self.assertEqual(re.findall(r"guid: (\w+)", frames), expected)
            self.assertEqual(re.findall(r"fileID: (-?\d+)", frames), ["21300000"] * 3)
        self.assertIn("  secondsPerFrame: 4\n", scene)
        self.assertIn("  holdToSkip: 1\n", scene)

    def test_six_captions_match_requested_text_and_have_english_translations(self):
        shared = (ASSETS / "Resources/Localization/UI Shared Data.asset").read_text()
        keys = {int(i): key for i, key in re.findall(r"m_Id: (\d+)\n    m_Key: (.+)", shared)}
        ru, en = table_values("ru"), table_values("en")
        expected_keys = [f"cutscene.intro.{i}" for i in range(1, 4)] + [f"cutscene.victory.{i}" for i in range(1, 4)]
        for index, (key, caption) in enumerate(zip(expected_keys, CAPTIONS), 24):
            self.assertEqual(keys[index], key)
            self.assertEqual(ru[index], caption)
            self.assertTrue(en[index])
            self.assertNotEqual(en[index], caption)

    def test_open_prompt_is_only_e_and_skip_hint_text_is_unchanged(self):
        ru, en = table_values("ru"), table_values("en")
        self.assertEqual(ru[14], "[E] Открыть")
        self.assertEqual(en[14], "[E] Open")
        self.assertEqual(ru[15], "Удерживайте ЛКМ, чтобы пропустить")
        self.assertEqual(en[15], "Hold LMB to skip")
        self.assertEqual(ru[23], "Удерживайте A, чтобы пропустить")
        self.assertEqual(en[23], "Hold A to skip")

    def test_picture_and_caption_have_separate_space_on_dark_background(self):
        source = (ASSETS / "Scripts/GameCutscenes.cs").read_text()
        def anchors(field, bound):
            value = re.search(rf"{re.escape(field)}\.anchor{bound} = new Vector2\(([\d.f]+), ([\d.f]+)\)", source)
            return tuple(float(v.rstrip("f")) for v in value.groups())
        image_min = anchors("_picture.rectTransform", "Min")
        image_max = anchors("_picture.rectTransform", "Max")
        caption_min = anchors("_caption.rectTransform", "Min")
        caption_max = anchors("_caption.rectTransform", "Max")
        self.assertGreater(image_min[1], caption_max[1])
        self.assertIn('_overlay.transform, Color.black', source)
        self.assertIn('_picture.preserveAspect = true', source)
        # Aspect fitting must leave natural dark margins in each supported shape.
        for width, height in [(1920, 1080), (1280, 720), (1440, 1080), (2560, 1080), (3840, 1080)]:
            box_width = width * (image_max[0] - image_min[0])
            box_height = height * (image_max[1] - image_min[1])
            scale = min(box_width / 1920, box_height / 1080)
            actual_width, actual_height = 1920 * scale, 1080 * scale
            self.assertLess(actual_width, width)
            self.assertLess(actual_height, height)
            self.assertAlmostEqual(actual_width / actual_height, 16 / 9)
            # Skip hint still ends at 88 reference pixels above the bottom.
            self.assertGreater(caption_min[1] * height, 88 * height / 1080)


if __name__ == "__main__":
    unittest.main()
