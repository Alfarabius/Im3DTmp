"""Check effect wiring and resources without simulating Unity's renderer."""
import re
import unittest
import wave
from pathlib import Path

ASSETS = Path(__file__).resolve().parents[1] / "Assets"


def guid(path):
    return re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text(), re.M)[1]


def blocks(path):
    return re.split(r"(?=^--- !u!)", path.read_text(), flags=re.M)


def effect(prefab, script):
    script_guid = guid(ASSETS / f"Scripts/Effects/{script}.cs")
    return next(block for block in blocks(prefab) if f"guid: {script_guid}" in block)


class SceneEffectAssetsTests(unittest.TestCase):
    def test_prefab_ids_are_unique_and_effects_attached(self):
        for name, script in [("Enemy", "EnemyWeaponEffects"), ("LootBox", "LootBoxEffects")]:
            prefab = ASSETS / f"Prefabs/{name}.prefab"
            ids = re.findall(r"^--- !u!\d+ &(-?\d+)", prefab.read_text(), re.M)
            self.assertEqual(len(ids), len(set(ids)))
            self.assertTrue(all(-(2**63) <= int(value) < 2**63 for value in ids))
            fx = effect(prefab, script)
            effect_id = re.match(r"--- !u!114 &(\d+)", fx)[1]
            owner_id = re.search(r"m_GameObject: \{fileID: (\d+)", fx)[1]
            owner = next(block for block in blocks(prefab) if block.startswith(f"--- !u!1 &{owner_id}\n"))
            self.assertIn(f"component: {{fileID: {effect_id}}}", owner)

    def test_muzzle_follows_the_weapon_prefab(self):
        prefab = ASSETS / "Prefabs/Enemy.prefab"
        fx = effect(prefab, "EnemyWeaponEffects")
        muzzle_id = re.search(r"muzzle: \{fileID: (\d+)", fx)[1]
        muzzle = next(block for block in blocks(prefab) if block.startswith(f"--- !u!4 &{muzzle_id}\n"))
        weapon_id = re.search(r"m_Father: \{fileID: (\d+)", muzzle)[1]
        weapon = next(block for block in blocks(prefab) if block.startswith(f"--- !u!4 &{weapon_id} stripped\n"))
        self.assertIn("guid: 37938f7089e93274ab22e5c189b9a7a8", weapon)
        self.assertIn(f"addedObject: {{fileID: {muzzle_id}}}", prefab.read_text())

    def test_confetti_origin_is_attached_above_the_box(self):
        prefab = ASSETS / "Prefabs/LootBox.prefab"
        fx = effect(prefab, "LootBoxEffects")
        origin_id = re.search(r"confettiOrigin: \{fileID: (\d+)", fx)[1]
        origin = next(block for block in blocks(prefab) if block.startswith(f"--- !u!4 &{origin_id}\n"))
        self.assertIn("m_LocalPosition: {x: 0, y: 0.68, z: 0}", origin)
        root_id = re.search(r"m_Father: \{fileID: (\d+)", origin)[1]
        root = next(block for block in blocks(prefab) if block.startswith(f"--- !u!4 &{root_id}\n"))
        self.assertIn(f"- {{fileID: {origin_id}}}", root)

    def test_opening_audio_uses_the_uploaded_clip_and_sfx_bus(self):
        prefab = ASSETS / "Prefabs/LootBox.prefab"
        fx = effect(prefab, "LootBoxEffects")
        source_id = re.search(r"openingAudio: \{fileID: (\d+)", fx)[1]
        sources = [block for block in blocks(prefab) if block.startswith("--- !u!82 ")]
        self.assertEqual(len(sources), 1)
        source = sources[0]
        self.assertTrue(source.startswith(f"--- !u!82 &{source_id}\n"))
        clip = ASSETS / "Audio/422738__grubzyy__s_tear_box_open.wav"
        self.assertIn(f"guid: {guid(clip)}", source)
        self.assertIn("m_PlayOnAwake: 0", source)
        self.assertIn("Loop: 0", source)
        self.assertIn("guid: ddb28457edbc91f48946fc13af951299", source)
        self.assertIn("forceToMono: 1", Path(str(clip) + ".meta").read_text())
        with wave.open(str(clip)) as audio:
            self.assertEqual(audio.getframerate(), 48000)
            self.assertEqual(audio.getnchannels(), 2)
            self.assertAlmostEqual(audio.getnframes() / audio.getframerate(), 3.2710416666666666)

    def test_material_references_resolve_to_the_shared_urp_shader(self):
        shader = ASSETS / "Effects/EffectUnlit.shader"
        for prefab_name, script, names in [
            ("Enemy", "EnemyWeaponEffects", ["MuzzleFlash", "MuzzleSmoke", "BulletTracer"]),
            ("LootBox", "LootBoxEffects", ["Confetti"]),
        ]:
            fx = effect(ASSETS / f"Prefabs/{prefab_name}.prefab", script)
            for name in names:
                material = ASSETS / f"Effects/Materials/{name}.mat"
                self.assertIn(f"guid: {guid(material)}", fx)
                self.assertIn(f"guid: {guid(shader)}", material.read_text())
        flash = (ASSETS / "Effects/Materials/MuzzleFlash.mat").read_text()
        texture = ASSETS / "EvolveGames/BodyCamController/WeaponEffects/Textures/FlashParticle.tif"
        self.assertIn(f"guid: {guid(texture)}", flash)
