"""Verify the camera component and model fitted body volumes are connected."""
import re
import unittest
from pathlib import Path

ASSETS = Path(__file__).resolve().parents[1] / "Assets"


class CameraCollisionAssetTests(unittest.TestCase):
    def setUp(self):
        self.prefab = (ASSETS / "Prefabs/Player.prefab").read_text()
        self.blocks = re.split(r"(?=^--- !u!)", self.prefab, flags=re.M)
        self.fx = next(block for block in self.blocks if "Assembly-CSharp::PlayerCameraCollision" in block)

    def test_component_is_on_the_actual_output_camera(self):
        ident = re.match(r"--- !u!114 &(\d+)", self.fx)[1]
        owner_id = re.search(r"m_GameObject: \{fileID: (\d+)", self.fx)[1]
        owner = next(block for block in self.blocks if block.startswith(f"--- !u!1 &{owner_id}\n"))
        self.assertIn(f"component: {{fileID: {ident}}}", owner)
        self.assertIn("m_Name: PlayerCamera\n", owner)
        camera = next(block for block in self.blocks if block.startswith("--- !u!20 "))
        self.assertIn(f"m_GameObject: {{fileID: {owner_id}}}", camera)

    def test_player_and_rig_references_resolve(self):
        player_id = re.search(r"player: \{fileID: (\d+)", self.fx)[1]
        player = next(block for block in self.blocks if block.startswith(f"--- !u!114 &{player_id}\n"))
        self.assertIn("Assembly-CSharp::PlayerBrain", player)
        rig_id = re.search(r"bodyRoot: \{fileID: (\d+)", self.fx)[1]
        rig = next(block for block in self.blocks if block.startswith(f"--- !u!4 &{rig_id} stripped\n"))
        self.assertIn("guid: a2601fb6aa97f914a8fb0f9f12bfa731", rig)

    def test_body_volumes_cover_every_main_bone_of_this_model(self):
        volumes = re.findall(r"boneName: ([^\n]+)\n    center: ([^\n]+)\n    size: ([^\n]+)", self.fx)
        expected = {"head", "neck", "spine01", "spine02"}
        for side in ["L", "R"]:
            expected.update(f"{bone}.{side}" for bone in ["shoulder", "upper_arm", "forearm", "hand", "waist", "hip", "chin", "foot"])
        self.assertEqual({name for name, _, _ in volumes}, expected)
        self.assertEqual(len(volumes), len(expected))
        model = (ASSETS / "Animations/GingerNPC/Standing W_Briefcase Idle.fbx").read_bytes()
        for name, center, size in volumes:
            self.assertIn(name.encode() + b"\x00\x01Model", model)
            values = [float(value) for value in re.findall(r"[xyz]: ([^,}]+)", size)]
            self.assertTrue(all(value > 0 for value in values), name)
            self.assertLess(max(values), 0.03, name)  # Bone local units; rig scale is 100.

    def test_mask_covers_scene_geometry_and_excludes_ui_and_helper_layer(self):
        mask = int(re.search(r"environmentMask:\n    serializedVersion: 2\n    m_Bits: (\d+)", self.fx)[1])
        for layer in [0, 6, 8, 9, 10]:
            self.assertTrue(mask & (1 << layer))
        for layer in [2, 5]:
            self.assertFalse(mask & (1 << layer))

    def test_component_ids_are_unique(self):
        ids = re.findall(r"^--- !u!\d+ &(-?\d+)", self.prefab, re.M)
        self.assertEqual(len(ids), len(set(ids)))
        self.assertTrue(all(-(2**63) <= int(value) < 2**63 for value in ids))
