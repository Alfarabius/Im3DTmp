"""Static input/prefab checks; these do not execute the game or its C# code."""
import json
import math
import re
import struct
import unittest
from pathlib import Path

from test_integration_assets import documents, table_values


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"


def guid(path):
    return re.search(r"^guid: (\w+)$", Path(str(path) + ".meta").read_text(), re.M)[1]


def player_map(path):
    return next(m for m in json.loads(path.read_text())["maps"] if m["name"] == "Player")


class InventoryControlsAssetsTests(unittest.TestCase):
    def test_crouch_actions_keep_their_ids_without_any_device_bindings(self):
        for relative, action_id in [
            ("Input/PlayerControls.inputactions", "25022331-7297-45d9-b315-dfb96c58c018"),
            ("InputSystem_Actions.inputactions", "27c5f898-bc57-4ee1-8800-db469aca5fe3"),
        ]:
            actions = player_map(ASSETS / relative)
            crouch = next(a for a in actions["actions"] if a["name"] == "Crouch")
            self.assertEqual(crouch["id"], action_id)
            self.assertFalse([b for b in actions["bindings"] if b["action"] == "Crouch"])

    def test_e_and_f_share_lootbox_interaction_without_changing_gamepad_actions(self):
        controls = player_map(ASSETS / "Input/PlayerControls.inputactions")
        paths = lambda action: {b["path"] for b in controls["bindings"] if b["action"] == action}
        self.assertEqual(paths("Interact"), {"<Keyboard>/e", "<Keyboard>/f", "<Gamepad>/buttonWest"})
        self.assertEqual(paths("LeanLeft"), {"<Gamepad>/leftShoulder"})
        self.assertEqual(paths("LeanRight"), {"<Gamepad>/rightShoulder"})
        self.assertEqual(paths("Action"), {"<Mouse>/leftButton", "<Gamepad>/rightTrigger"})
        self.assertEqual(paths("AltAction"), {"<Mouse>/rightButton", "<Gamepad>/leftTrigger"})
        interaction = next(a for a in controls["actions"] if a["name"] == "Interact")
        self.assertEqual(interaction["id"], "00435fb6-9314-4924-b47d-e24b9b3e6104")
        self.assertEqual(interaction["interactions"], "")

    def test_medkit_prefab_retains_pickup_and_definition_with_real_model_mesh(self):
        prefab = (ASSETS / "Prefabs/Medkit.prefab").read_text()
        self.assertIn(f"itemDefinition: {{fileID: 11400000, guid: {guid(ASSETS / 'Inventory/Medkit.asset')}", prefab)
        self.assertIn("--- !u!135 &840000000000000302", prefab)
        self.assertIn("--- !u!54 &840000000000000303", prefab)
        self.assertIn(f"m_Mesh: {{fileID: 4300000, guid: {guid(ASSETS / 'Inventory/MedkitMesh.asset')}", prefab)
        self.assertIn(f"guid: {guid(ASSETS / 'Inventory/MedkitModel.mat')}", prefab)
        self.assertNotIn("0000000000000000e000000000000000", prefab)
        docs = documents(ASSETS / "Prefabs/Medkit.prefab")
        ids = [re.match(r"--- !u!\d+ &(-?\d+)", doc)[1] for doc in docs]
        self.assertEqual(len(ids), len(set(ids)))
        self.assertEqual(sum(doc.startswith("--- !u!33 ") for doc in docs), 1)
        definition = (ASSETS / "Inventory/Medkit.asset").read_text()
        self.assertIn("  useDuration: 3\n", definition)
        self.assertIn("  consumeOnUse: 1\n", definition)
        self.assertIn(guid(ASSETS / "Inventory/MedkitIcon.png"), definition)
        material = (ASSETS / "Inventory/MedkitModel.mat").read_text()
        texture = ASSETS / "Models/KayKit_Holiday_Bits_1.0_EXTRA/Assets/fbx(unity)/holiday_bits_texture.png"
        self.assertIn(guid(texture), material)
        self.assertIn("_BaseColor: {r: 1, g: 1, b: 1, a: 1}", material)

    def test_baked_medkit_mesh_has_valid_buffers_normals_and_world_size(self):
        text = (ASSETS / "Inventory/MedkitMesh.asset").read_text()
        vertex_count = int(re.search(r"m_VertexCount: (\d+)", text)[1])
        index_count = int(re.search(r"indexCount: (\d+)", text)[1])
        vertex_data = bytes.fromhex(re.search(r"_typelessdata: ([0-9a-f]+)", text)[1])
        index_data = bytes.fromhex(re.search(r"m_IndexBuffer: ([0-9a-f]+)", text)[1])
        self.assertEqual(len(vertex_data), vertex_count * 32)
        self.assertEqual(int(re.search(r"m_DataSize: (\d+)", text)[1]), len(vertex_data))
        self.assertEqual(len(index_data), index_count * 2)
        self.assertEqual(index_count % 3, 0)
        vertices = list(struct.iter_unpack("<8f", vertex_data))
        indices = struct.unpack("<" + "H" * index_count, index_data)
        self.assertLess(max(indices), vertex_count)
        self.assertGreater(vertex_count, 100)
        for vertex in vertices:
            self.assertTrue(all(math.isfinite(value) for value in vertex))
            self.assertAlmostEqual(sum(n * n for n in vertex[3:6]), 1, places=5)
        # Model's own 0.36 child scale and Y=90 rotation: width is source Z.
        dimensions = [max(v[i] for v in vertices) - min(v[i] for v in vertices) for i in range(3)]
        self.assertAlmostEqual(dimensions[2] * 0.36, 0.312, delta=0.002)
        self.assertAlmostEqual(dimensions[1] * 0.36, 0.270, delta=0.002)
        self.assertAlmostEqual(dimensions[0] * 0.36, 0.135, delta=0.002)
        prefab = (ASSETS / "Prefabs/Medkit.prefab").read_text()
        self.assertIn("m_LocalScale: {x: 0.36, y: 0.36, z: 0.36}", prefab)
        self.assertIn("m_LocalEulerAnglesHint: {x: 0, y: 90, z: 0}", prefab)

    def test_item_tips_keep_three_lines_and_distinguish_throw_from_hold(self):
        for locale in ["ru", "en"]:
            values = table_values(locale)
            for entry, button in [(21, "ЛКМ" if locale == "ru" else "LMB"), (22, "RT")]:
                lines = values[entry].splitlines()
                self.assertEqual(len(lines), 3)
                self.assertTrue(lines[0].startswith(button + " - "))
                self.assertIn("бросок / зажать" if locale == "ru" else "throw / hold", lines[0])
                drop_button = ("ПКМ" if locale == "ru" else "RMB") if entry == 21 else "LT"
                self.assertTrue(lines[1].startswith(drop_button + " - "))


if __name__ == "__main__":
    unittest.main()
