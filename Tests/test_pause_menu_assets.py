"""Static scene wiring and media checks for pause titles and menu music."""
import json
import re
import unittest
import wave
from pathlib import Path

import yaml


ASSETS = Path(__file__).resolve().parents[1] / "Assets"


def guid(path):
    return re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text(), re.M)[1]


def reference(block, field):
    return int(re.search(rf"^  {field}: \{{fileID: (-?\d+)", block, re.M)[1])


class PauseMenuAssetsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.scene = (ASSETS / "Scenes/GameScene.unity").read_text()
        cls.docs = {int(m[2]): (int(m[1]), m[0]) for m in re.finditer(
            r"^--- !u!(\d+) &(-?\d+)(?: stripped)?\n.*?(?=^---|\Z)", cls.scene, re.M | re.S)}

    def component(self, name):
        matches = [(ident, block) for ident, (kind, block) in self.docs.items()
                   if kind == 114 and f"guid: {guid(ASSETS / f'Scripts/{name}.cs')}" in block]
        self.assertEqual(len(matches), 1, name)
        return matches[0]

    def test_titles_text_is_editable_on_pause_menu_and_keeps_requested_credits(self):
        ident, titles = self.component("PauseTitlesMenu")
        owner = reference(titles, "m_GameObject")
        self.assertIn("  m_Name: PauseMenu\n", self.docs[owner][1])
        self.assertIn(f"component: {{fileID: {ident}}}", self.docs[owner][1])
        text = json.loads(re.search(r"^  titlesText: (.+)", titles, re.M)[1])
        self.assertEqual(text.splitlines(), [
            "Game by Turbo Laser", "ART - Anya Babushkina", "Music - Mikhail Puchkov"])
        source = (ASSETS / "Scripts/PauseTitlesMenu.cs").read_text()
        self.assertRegex(source, r'TextArea\(6, 30\), InspectorName\("Текст титров"\)\]\s+private string titlesText')
        self.assertNotIn("LocalizationService.Get(", source)
        body = self.docs[reference(titles, "body")][1]
        self.assertEqual(json.loads(re.search(r"^  m_text: (.+)", body, re.M)[1]), text)
        owner = reference(body, "m_GameObject")
        localizer = guid(ASSETS / "Scripts/Application/LocalizedText.cs")
        self.assertFalse(any(kind == 114 and f"guid: {localizer}" in block
                             and reference(block, "m_GameObject") == owner
                             for kind, block in self.docs.values()))

    def test_titles_buttons_exist_in_scene_with_localized_labels_and_click_handlers(self):
        ident, titles = self.component("PauseTitlesMenu")
        _, pause = self.component("PauseManager")
        self.assertEqual(reference(pause, "titlesMenu"), ident)
        button = self.docs[reference(titles, "openButton")][1]
        owner = self.docs[reference(button, "m_GameObject")][1]
        rect_id = int(re.search(r"component: \{fileID: (-?\d+)", owner)[1])
        rect = self.docs[rect_id][1]
        layout = self.docs[reference(rect, "m_Father")][1]
        self.assertIn("  m_Name: LeftColumn\n", self.docs[reference(layout, "m_GameObject")][1])
        self.assertIn(f"  - {{fileID: {rect_id}}}\n", layout)
        main = reference(titles, "mainMenu")
        parent = self.docs[reference(layout, "m_Father")][1]
        self.assertEqual(reference(parent, "m_GameObject"), main)
        for field, name, handler in [("openButton", "TitlesButton", "Show"),
                                     ("backButton", "BackToPauseButton", "Close")]:
            control = self.docs[reference(titles, field)][1]
            self.assertIn("UnityEngine.UI::UnityEngine.UI.Button", control)
            self.assertIn(f"m_Target: {{fileID: {ident}}}", control)
            self.assertIn(f"m_MethodName: {handler}\n", control)
            self.assertIn(f"  m_Name: {name}\n", self.docs[reference(control, "m_GameObject")][1])
        # The added button fits without resizing the existing controls.
        children = re.search(r"  m_Children:(.*?)(?=\n  m_Father:)", layout, re.S)[1]
        transforms = [self.docs[int(i)][1] for i in re.findall(r"fileID: (-?\d+)", children)]
        heights = [float(re.search(r"m_SizeDelta: \{x: [^,]+, y: ([^}]+)", d)[1]) for d in transforms]
        available = float(re.search(r"m_SizeDelta: \{x: [^,]+, y: ([^}]+)", layout)[1])
        self.assertLessEqual(sum(heights) + (len(heights) - 1) * 10, available)
        def table(name):
            data = (ASSETS / f"Resources/Localization/{name}.asset").read_text()
            return yaml.safe_load(re.sub(r"^%.*\n|^--- !u!.*\n", "", data, flags=re.M))["MonoBehaviour"]
        keys = {entry["m_Id"]: entry["m_Key"] for entry in table("UI Shared Data")["m_Entries"]}
        for locale, expected in [("ru", ["Титры", "Назад"]), ("en", ["Titles", "Back"])]:
            values = {keys[row["m_Id"]]: row["m_Localized"] for row in table(f"UI_{locale}")["m_TableData"]}
            self.assertEqual([values["menu.titles"], values["menu.back"]], expected)
        localizer = guid(ASSETS / "Scripts/Application/LocalizedText.cs")
        bindings = [re.search(r"^  key:[ \t]*(.*)$", block, re.M)[1] for kind, block in self.docs.values()
                    if kind == 114 and f"guid: {localizer}" in block]
        self.assertEqual(bindings.count("menu.titles"), 2)  # Entry button and submenu heading.
        self.assertEqual(bindings.count("menu.back"), 1)

    def test_music_has_its_own_looping_source_with_uploaded_clip_and_master_volume(self):
        _, music = self.component("PauseCutsceneMusic")
        owner = reference(music, "m_GameObject")
        sources = [d for kind, d in self.docs.values() if kind == 82 and reference(d, "m_GameObject") == owner]
        self.assertEqual(len(sources), 1)
        source = sources[0]
        clip = ASSETS / "Audio/Xmas horror Pause.wav"
        self.assertIn(f"m_Resource: {{fileID: 8300000, guid: {guid(clip)}", source)
        self.assertIn("  Loop: 1\n", source)
        self.assertIn("  m_PlayOnAwake: 0\n", source)
        self.assertIn("OutputAudioMixerGroup: {fileID: 24300002,", source)
        self.assertIn(guid(ASSETS / "Audio/AudioMixer.mixer"), source)
        settings = Path(str(clip) + ".meta").read_text()
        self.assertIn("forceToMono: 0", settings)
        self.assertIn("preloadAudioData: 1", settings)
        with wave.open(str(clip)) as audio:
            self.assertEqual(audio.getnchannels(), 2)
            self.assertEqual(audio.getframerate(), 48000)
            self.assertGreater(audio.getnframes(), 0)


if __name__ == "__main__":
    unittest.main()
