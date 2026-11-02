"""Regression checks for the prefab and scene wiring required by HuntState."""
import re
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "Assets"


def guid(path):
    return re.search(r"^guid: (\w+)", Path(str(path) + ".meta").read_text(), re.M)[1]


def blocks(path):
    return re.split(r"(?=^--- !u!)", path.read_text(), flags=re.M)


def component(name):
    script_guid = guid(ASSETS / f"Scripts/{name}.cs")
    matches = [doc for doc in blocks(ASSETS / "Prefabs/Enemy.prefab")
               if doc.startswith("--- !u!114 ") and f"guid: {script_guid}" in doc]
    assert len(matches) == 1, name
    return matches[0]


class EnemyHuntAssetTests(unittest.TestCase):
    def test_hunt_is_attached_to_same_enemy_as_brain_and_patrol(self):
        hunt = component("HuntState")
        owner = re.search(r"m_GameObject: \{fileID: (\d+)", hunt)[1]
        component_id = re.match(r"--- !u!114 &(\d+)", hunt)[1]
        self.assertLess(int(component_id), 2**63)
        enemy = next(doc for doc in blocks(ASSETS / "Prefabs/Enemy.prefab")
                     if doc.startswith(f"--- !u!1 &{owner}\n"))
        self.assertIn(f"component: {{fileID: {component_id}}}", enemy)
        for name in ["EnemyBrain", "PatrolState"]:
            self.assertIn(f"m_GameObject: {{fileID: {owner}}}", component(name))

    def test_defaults_are_ten_seconds_and_fixed_point_run(self):
        patrol, hunt = component("PatrolState"), component("HuntState")
        self.assertIn("  timeBeforeHunting: 10\n", patrol)
        self.assertNotIn("playerTransform:", patrol)
        self.assertIn("  runSpeed: 5\n", hunt)
        self.assertIn("  updatePatrolPointWhileRunning: 0\n", hunt)
        self.assertIn("  pointUpdateInterval: 0.3\n", hunt)

    def test_scene_player_reference_moved_to_hunt_and_patrol_route_is_preserved(self):
        scene = ASSETS / "Scenes/GameScene.unity"
        prefab_guid = guid(ASSETS / "Prefabs/Enemy.prefab")
        instance = next(doc for doc in blocks(scene) if doc.startswith("--- !u!1001 ")
                        and re.search(r"m_SourcePrefab:.*guid: " + prefab_guid, doc))
        hunt_id = re.match(r"--- !u!114 &(\d+)", component("HuntState"))[1]
        patrol_id = re.match(r"--- !u!114 &(\d+)", component("PatrolState"))[1]
        overrides = {}
        for modification in instance.split("    - target: ")[1:]:
            target = re.match(r"\{fileID: (\d+)", modification)[1]
            field = re.search(r"propertyPath: (.+)", modification)[1]
            reference = re.search(r"objectReference: \{fileID: (\d+)", modification)[1]
            overrides[target, field] = reference
        self.assertNotIn((patrol_id, "playerTransform"), overrides)
        player_id = overrides[hunt_id, "playerTransform"]
        self.assertNotEqual(player_id, "0")
        player_transform = next(doc for doc in blocks(scene) if doc.startswith(f"--- !u!4 &{player_id} "))
        self.assertIn(guid(ASSETS / "Prefabs/Player.prefab"), player_transform)
        route_id = overrides[patrol_id, "routeRoot"]
        self.assertNotEqual(route_id, "0")
        self.assertTrue(any(doc.startswith(f"--- !u!4 &{route_id}\n") for doc in blocks(scene)))
