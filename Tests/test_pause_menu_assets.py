"""Static scene wiring and media checks for pause titles and menu music."""
import json
import re
import unittest
import wave
from pathlib import Path


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

    def test_titles_is_connected_to_existing_left_layout_and_button_template(self):
        ident, titles = self.component("PauseTitlesMenu")
        _, pause = self.component("PauseManager")
        self.assertEqual(reference(pause, "titlesMenu"), ident)
        layout = self.docs[reference(titles, "leftLayout")][1]
        self.assertIn("  m_Name: LeftColumn\n", self.docs[reference(layout, "m_GameObject")][1])
        main = reference(titles, "mainMenu")
        parent = self.docs[reference(layout, "m_Father")][1]
        self.assertEqual(reference(parent, "m_GameObject"), main)
        button = self.docs[reference(titles, "buttonTemplate")][1]
        owner = reference(button, "m_GameObject")
        self.assertIn("  m_Name: ResumeButton\n", self.docs[owner][1])
        self.assertIn("UnityEngine.UI::UnityEngine.UI.Button", button)
        # The cloned Titles button fits without resizing the existing controls.
        children = re.search(r"  m_Children:(.*?)(?=\n  m_Father:)", layout, re.S)[1]
        transforms = [self.docs[int(i)][1] for i in re.findall(r"fileID: (-?\d+)", children)]
        heights = [float(re.search(r"m_SizeDelta: \{x: [^,]+, y: ([^}]+)", d)[1]) for d in transforms]
        available = float(re.search(r"m_SizeDelta: \{x: [^,]+, y: ([^}]+)", layout)[1])
        self.assertLessEqual(sum(heights) + 140 + len(heights) * 10, available)

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
