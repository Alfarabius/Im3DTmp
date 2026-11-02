import re
import unittest
from pathlib import Path

ASSETS = Path(__file__).resolve().parents[1] / "Assets"


class FirstPersonViewAssetTests(unittest.TestCase):
    def setUp(self):
        self.prefab = (ASSETS / "Prefabs/Player.prefab").read_text()

    def test_custom_collision_solver_has_been_removed(self):
        self.assertNotIn("PlayerCameraCollision", self.prefab)
        self.assertFalse((ASSETS / "Scripts/Player/PlayerCameraCollision.cs").exists())

    def test_deoccluder_uses_nonzero_tracking_target_without_aiming_at_it(self):
        self.assertIn("Unity.Cinemachine.CinemachineDeoccluder", self.prefab)
        self.assertIn("TrackingTarget: {fileID: 843000000000000103}", self.prefab)
        self.assertIn("CustomLookAtTarget: 1", self.prefab)
        self.assertIn("UseFollowTarget:\n      Enabled: 1", self.prefab)
        self.assertIn("Strategy: 0", self.prefab)
        self.assertIn("DampingWhenOccluded: 0", self.prefab)

    def test_head_layer_is_excluded_only_from_players_camera(self):
        camera = next(b for b in re.split(r"(?=^--- !u!)", self.prefab, flags=re.M) if b.startswith("--- !u!20 "))
        mask = int(re.search(r"m_CullingMask:\n    serializedVersion: 2\n    m_Bits: (\d+)", camera)[1])
        self.assertFalse(mask & (1 << 11))
        self.assertTrue(mask & (1 << 7))
        self.assertIn("  - PlayerHead\n", (ASSETS.parent / "ProjectSettings/TagManager.asset").read_text())

    def test_model_data_is_readable_for_one_time_head_separation(self):
        importer = ASSETS / "Animations/GingerNPC/Standing W_Briefcase Idle.fbx.meta"
        self.assertIn("isReadable: 1", importer.read_text())
        self.assertIn("modelRoot: {fileID: 6217357032570441250}", self.prefab)
        self.assertIn("Assembly-CSharp::PlayerFirstPersonView", self.prefab)

    def test_lens_and_noise_guard_are_configured(self):
        self.assertIn("near clip plane: 0.02", self.prefab)
        self.assertIn("NearClipPlane: 0.02", self.prefab)
        self.assertIn("Assembly-CSharp::PlayerCameraNoiseCollision", self.prefab)
        self.assertIn("cameraRadius: 0.04", self.prefab)
