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

    def test_lens_and_noise_guard_are_configured(self):
        self.assertIn("near clip plane: 0.02", self.prefab)
        self.assertIn("NearClipPlane: 0.02", self.prefab)
        self.assertIn("Assembly-CSharp::PlayerCameraNoiseCollision", self.prefab)
        self.assertIn("cameraRadius: 0.04", self.prefab)
