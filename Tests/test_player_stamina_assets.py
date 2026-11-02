"""Verify the uploaded voice clips and Player's audio/camera/volume wiring."""
import re
import unittest
import wave
from pathlib import Path

ASSETS = Path(__file__).resolve().parents[1] / "Assets"
PLAYER = ASSETS / "Prefabs/Player.prefab"
CLIP = ASSETS / "Audio/516696__amekiano__respiracion-agitada-heavy-breathing.wav"
PAIN_CLIP = ASSETS / "Audio/876075__sebasteks__man-in-excruciating-pain.wav"


def guid(path):
    return re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text(), re.M)[1]


def blocks(path):
    return re.split(r"(?=^--- !u!)", path.read_text(), flags=re.M)


def effects(script_name="PlayerStaminaEffects"):
    script = guid(ASSETS / f"Scripts/Player/{script_name}.cs")
    matches = [block for block in blocks(PLAYER) if block.startswith("--- !u!114 ") and f"guid: {script}" in block]
    assert len(matches) == 1
    return matches[0]


class PlayerStaminaAssetTests(unittest.TestCase):
    def test_effects_and_audio_are_attached_to_player(self):
        fx = effects()
        fx_id = re.match(r"--- !u!114 &(\d+)", fx)[1]
        owner_id = re.search(r"m_GameObject: \{fileID: (\d+)", fx)[1]
        owner = next(block for block in blocks(PLAYER) if block.startswith(f"--- !u!1 &{owner_id}\n"))
        self.assertIn(f"component: {{fileID: {fx_id}}}", owner)
        audio_id = re.search(r"breathingAudio: \{fileID: (\d+)", fx)[1]
        self.assertIn(f"component: {{fileID: {audio_id}}}", owner)
        for path in [PLAYER, ASSETS / "Scenes/GameScene.unity"]:
            ids = [int(value) for value in re.findall(r"^--- !u!\d+ &(-?\d+)", path.read_text(), re.M)]
            self.assertEqual(len(ids), len(set(ids)))
            self.assertTrue(all(-(2**63) <= value < 2**63 for value in ids))

    def test_breathing_source_has_correct_clip_loop_and_sfx_bus(self):
        fx = effects()
        audio_id = re.search(r"breathingAudio: \{fileID: (\d+)", fx)[1]
        audio = next(block for block in blocks(PLAYER) if block.startswith(f"--- !u!82 &{audio_id}\n"))
        self.assertIn(f"guid: {guid(CLIP)}", audio)
        self.assertIn("  m_PlayOnAwake: 0\n", audio)
        self.assertIn("  Loop: 1\n", audio)
        self.assertIn("  OutputAudioMixerGroup: {fileID: -4502576610630677228, guid: ddb28457edbc91f48946fc13af951299, type: 2}", audio)
        clip_meta = Path(str(CLIP) + ".meta").read_text()
        self.assertIn("preloadAudioData: 1", clip_meta)
        with wave.open(str(CLIP)) as clip:
            self.assertEqual(clip.getnchannels(), 2)
            self.assertEqual(clip.getframerate(), 48000)
            self.assertEqual(clip.getsampwidth(), 3)
            self.assertEqual(clip.getnframes() / clip.getframerate(), 4)

    def test_existing_camera_noise_is_referenced(self):
        noise_id = re.search(r"cameraNoise: \{fileID: (\d+)", effects())[1]
        noise = next(block for block in blocks(PLAYER) if block.startswith(f"--- !u!114 &{noise_id}\n"))
        self.assertIn("CinemachineBasicMultiChannelPerlin", noise)
        self.assertNotIn("NoiseProfile: {fileID: 0}", noise)

    def test_pain_clip_is_assigned_without_adding_an_audio_source(self):
        damage = effects("PlayerDamageEffects")
        self.assertIn(f"painClip: {{fileID: 8300000, guid: {guid(PAIN_CLIP)}, type: 3}}", damage)
        self.assertNotIn("painClip:", effects())
        pain_source = re.search(r"painAudio: \{fileID: (\d+)", damage)[1]
        breathing_source = re.search(r"breathingAudio: \{fileID: (\d+)", effects())[1]
        self.assertEqual(pain_source, breathing_source)
        sources = [block for block in blocks(PLAYER) if block.startswith("--- !u!82 ")]
        self.assertEqual(len(sources), 3)  # footsteps, hit, shared breathing/pain
        with wave.open(str(PAIN_CLIP)) as clip:
            self.assertEqual(clip.getnchannels(), 2)
            self.assertEqual(clip.getframerate(), 96000)
            self.assertEqual(clip.getsampwidth(), 2)
            self.assertAlmostEqual(clip.getnframes() / clip.getframerate(), 6.886208333333333)

    def test_scene_volume_is_same_as_damage_effects(self):
        scene = (ASSETS / "Scenes/GameScene.unity").read_text()
        prefab_guid = guid(PLAYER)
        fx_id = re.match(r"--- !u!114 &(\d+)", effects())[1]
        pattern = r"target: \{fileID: %s, guid: %s, type: 3\}\n      propertyPath: postProcessVolume\n      value: *\n      objectReference: \{fileID: (\d+)\}"
        volume_id = re.search(pattern % (fx_id, prefab_guid), scene)[1]
        damage_id = "3087166897470010314"
        self.assertEqual(volume_id, re.search(pattern % (damage_id, prefab_guid), scene)[1])
        volume = next(block for block in blocks(ASSETS / "Scenes/GameScene.unity") if block.startswith(f"--- !u!114 &{volume_id}\n"))
        profile_guid = re.search(r"sharedProfile:.*guid: (\w+)", volume)[1]
        profile = next(path for path in ASSETS.rglob("*.asset") if guid(path) == profile_guid)
        self.assertIn("ChromaticAberration", profile.read_text())

    def test_smooth_envelope_defaults_are_serialized(self):
        fx = effects()
        self.assertIn("  fadeInDuration: 0.8\n", fx)
        self.assertIn("  fadeOutDuration: 1.2\n", fx)
        self.assertIn("  shakeAmplitudeMultiplier: 2\n", fx)
