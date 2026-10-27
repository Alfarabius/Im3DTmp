"""Checks serialized integration data without Unity: python3 -m unittest discover -s Tests -v."""
import json
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"


def documents(path):
    return [part for part in re.split(r"(?=^--- !u!)", path.read_text(), flags=re.M) if part.startswith("--- !u!")]


def table_values(locale):
    text = (ASSETS / f"Resources/Localization/UI_{locale}.asset").read_text()
    return {int(entry_id): json.loads(value) for entry_id, value in
            re.findall(r"m_Id: (\d+)\n    m_Localized: (.+)", text)}


class IntegrationAssetsTests(unittest.TestCase):
    def test_table_editor_settings_are_registered_and_point_to_localization_asset(self):
        settings = ASSETS / "Localization/Localization Settings.asset"
        guid = re.search(r"guid: (\w+)", Path(str(settings) + ".meta").read_text())[1]
        build = (ROOT / "ProjectSettings/EditorBuildSettings.asset").read_text()
        self.assertIn(f"com.unity.localization.settings: {{fileID: 11400000, guid: {guid}, type: 2}}", build)
        self.assertIn("guid: a07b5cd0b1b829245bc8c4b6978793e8", settings.read_text())
        self.assertIn("m_InitializeSynchronously: 0", settings.read_text())
        collection = (ASSETS / "Localization/UI.asset").read_text()
        for locale in ["ru", "en"]:
            table_meta = ASSETS / f"Resources/Localization/UI_{locale}.asset.meta"
            self.assertIn(re.search(r"guid: (\w+)", table_meta.read_text())[1], collection)

    def setUp(self):
        shared = (ASSETS / "Resources/Localization/UI Shared Data.asset").read_text()
        self.keys = {int(entry_id): key for entry_id, key in
                     re.findall(r"m_Id: (\d+)\n    m_Key: (.+)", shared)}
        self.bindings = (ASSETS / "Scripts/Application/LocalizedText.cs.meta").read_text().split("guid: ")[1].splitlines()[0]

    def test_complete_matching_translations_and_placeholders(self):
        ru, en = table_values("ru"), table_values("en")
        self.assertGreater(len(self.keys), 10)
        self.assertEqual(set(self.keys), set(ru))
        self.assertEqual(set(self.keys), set(en))
        for entry_id in self.keys:
            self.assertTrue(ru[entry_id])
            self.assertTrue(en[entry_id])
            self.assertEqual(re.findall(r"\{\w+\}", ru[entry_id]), re.findall(r"\{\w+\}", en[entry_id]))

    def test_all_current_ui_text_has_explicit_localization_binding(self):
        for relative in ["Scenes/GameScene.unity", "Prefabs/LootBox.prefab"]:
            docs = documents(ASSETS / relative)
            text_objects = set()
            bound_objects = set()
            used_keys = []
            for doc in docs:
                if not doc.startswith("--- !u!114 "):
                    continue
                owner = int(re.search(r"m_GameObject: \{fileID: (\d+)", doc)[1])
                if "guid: f4688fdb7df04437aeb418b961361dc5" in doc:
                    text_objects.add(owner)
                if f"guid: {self.bindings}" in doc:
                    bound_objects.add(owner)
                    used_keys.append(json.loads(re.search(r'^  key: (.*)$', doc, re.M)[1]))
            self.assertTrue(text_objects, relative)
            self.assertEqual(text_objects, bound_objects, relative)
            self.assertTrue(all(not key or key in self.keys.values() for key in used_keys))

    def test_component_ids_are_unique_signed_64_bit_and_attached(self):
        for relative in ["Scenes/GameScene.unity", "Prefabs/LootBox.prefab"]:
            docs = documents(ASSETS / relative)
            ids = [int(re.match(r"--- !u!\d+ &(-?\d+)", doc)[1]) for doc in docs]
            self.assertEqual(len(ids), len(set(ids)))
            self.assertTrue(all(-(2**63) <= value < 2**63 for value in ids))
            owners = {int(re.match(r"--- !u!1 &(\d+)", doc)[1]): doc for doc in docs if doc.startswith("--- !u!1 ")}
            for doc in docs:
                if f"guid: {self.bindings}" not in doc:
                    continue
                component_id = re.match(r"--- !u!114 &(\d+)", doc)[1]
                owner = int(re.search(r"m_GameObject: \{fileID: (\d+)", doc)[1])
                self.assertIn(f"component: {{fileID: {component_id}}}", owners[owner])

    def test_resources_link_to_existing_tables_and_included_font(self):
        settings = (ASSETS / "Resources/GameApplicationSettings.asset").read_text()
        for locale, field in [("ru", "russianTable"), ("en", "englishTable")]:
            guid = re.search(r"guid: (\w+)", (ASSETS / f"Resources/Localization/UI_{locale}.asset.meta").read_text())[1]
            self.assertIn(f"{field}: {{fileID: 11400000, guid: {guid}", settings)
        font_meta = (ASSETS / "Fonts/TrueType (.ttf)/HomeVideo-Regular.ttf.meta").read_text()
        self.assertIn("includeFontData: 1", font_meta)
        self.assertIn(re.search(r"guid: (\w+)", font_meta)[1], settings)

    def test_shared_key_ids_are_unique(self):
        text = (ASSETS / "Resources/Localization/UI Shared Data.asset").read_text()
        pairs = re.findall(r"m_Id: (\d+)\n    m_Key: (.+)", text)
        self.assertEqual(len(pairs), len({pair[0] for pair in pairs}))
        self.assertEqual(len(pairs), len({pair[1] for pair in pairs}))

    def test_localization_dependency_is_locked(self):
        manifest = json.loads((ROOT / "Packages/manifest.json").read_text())["dependencies"]
        lock = json.loads((ROOT / "Packages/packages-lock.json").read_text())["dependencies"]
        self.assertEqual(manifest["com.unity.localization"], lock["com.unity.localization"]["version"])
        for name in ["com.unity.localization", "com.unity.addressables", "com.unity.scriptablebuildpipeline", "com.unity.nuget.newtonsoft-json"]:
            for dependency in lock[name]["dependencies"]:
                self.assertIn(dependency, lock)


if __name__ == "__main__":
    unittest.main()
