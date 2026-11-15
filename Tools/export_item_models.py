"""Bake Shield/Phone and the shield's two capped halves from the user's FBX files.

Run: blender -b --python Tools/export_item_models.py
The meshes are in prefab-local coordinates, including model-only alignment and
scale. Shield fragments use exactly the same coordinates as the intact mesh.
"""
from pathlib import Path
import json
import math
import struct
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
sys.dont_write_bytecode = True
from export_medkit_mesh import ROOT, export


def alignment(source, height, center_y):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source))
    obj, = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    points = [obj.matrix_world @ v.co for v in obj.data.vertices]
    # Unity's Y-up coordinates, matching export_medkit_mesh.
    points = [Vector((p.x, p.z, p.y)) for p in points]
    minimum = Vector([min(p[i] for p in points) for i in range(3)])
    maximum = Vector([max(p[i] for p in points) for i in range(3)])
    center = (minimum + maximum) * 0.5
    scale = height / (maximum.y - minimum.y)
    return (Matrix.Translation(Vector((0, center_y, 0))) @ Matrix.Scale(scale, 4)
            @ Matrix.Rotation(math.pi / 2, 4, "Y") @ Matrix.Translation(-center))


def write_halves(vertex_data, indices):
    vertices = [struct.unpack("<8f", value) for value in vertex_data]
    source = bpy.data.meshes.new("Shield aligned")
    source.from_pydata([v[:3] for v in vertices], [],
                       [indices[i:i+3] for i in range(0, len(indices), 3)])
    source.update()
    uv_layer = source.uv_layers.new()
    for loop in source.loops:
        uv_layer.data[loop.index].uv = vertices[loop.vertex_index][6:8]

    # Use a wood colour already present on this model for the fresh cut surfaces.
    image = bpy.data.images.load(str(ROOT / "Assets/Models/KayKit_Holiday_Bits_1.0_EXTRA/Assets/fbx(unity)/holiday_bits_texture.png"), check_existing=True)
    pixels = list(image.pixels)
    def wood_distance(vertex):
        u, v = vertex[6:8]
        x = min(image.size[0]-1, max(0, int(u*image.size[0])))
        y = min(image.size[1]-1, max(0, int(v*image.size[1])))
        start = (y*image.size[0]+x)*4
        rgb = pixels[start:start+3]
        return sum((a-b)**2 for a,b in zip(rgb, (0.55,0.32,0.2)))
    cap_uv = min(vertices, key=wood_distance)[6:8]

    for name, left in [("ShieldLeft", True), ("ShieldRight", False)]:
        mesh = bmesh.new()
        mesh.from_mesh(source)
        # The runtime mesh duplicates vertices at normal/UV seams; weld positions
        # for a topological cut while keeping UVs on each face corner.
        bmesh.ops.remove_doubles(mesh, verts=list(mesh.verts), dist=1e-7)
        uv = mesh.loops.layers.uv.active
        # The model's rear grip is open where it meets the shield. Close that
        # hidden attachment face before cutting so the detached pieces are solid.
        source_caps = bmesh.ops.holes_fill(mesh, edges=[e for e in mesh.edges if e.is_boundary], sides=0)["faces"]
        for face in source_caps:
            for loop in face.loops:
                loop[uv].uv = cap_uv
        geometry = list(mesh.verts) + list(mesh.edges) + list(mesh.faces)
        bmesh.ops.bisect_plane(mesh, geom=geometry, dist=1e-7,
                              plane_co=(0,0,0), plane_no=(1,0,0),
                              clear_outer=left, clear_inner=not left)
        cut_edges = [e for e in mesh.edges if e.is_boundary and
                     all(abs(v.co.x) < 1e-6 for v in e.verts)]
        caps = bmesh.ops.holes_fill(mesh, edges=cut_edges, sides=0)["faces"]
        for face in caps:
            for loop in face.loops:
                loop[uv].uv = cap_uv
        bmesh.ops.recalc_face_normals(mesh, faces=list(mesh.faces))
        # The supplied model contains overlapping disconnected surface pieces.
        # Each piece must remain closed after the cut.
        if any(edge.is_boundary for edge in mesh.edges):
            raise ValueError(name + " has an uncapped boundary")
        result = bpy.data.meshes.new(name)
        mesh.to_mesh(result)
        mesh.free()
        result.calc_loop_triangles()
        unique, lookup, triangles = [], {}, []
        for triangle in result.loop_triangles:
            for loop_index in triangle.loops:
                loop = result.loops[loop_index]
                point = result.vertices[loop.vertex_index].co
                normal = result.corner_normals[loop_index].vector
                uv = result.uv_layers.active.data[loop_index].uv
                data = struct.pack("<8f", *point, *normal, *uv)
                if data not in lookup:
                    lookup[data] = len(unique)
                    unique.append(struct.unpack("<8f", data))
                triangles.append(lookup[data])
        vector = lambda values: dict(zip("xyz", values))
        data = {"vertices": [vector(v[:3]) for v in unique],
                "normals": [vector(v[3:6]) for v in unique],
                "uvs": [dict(zip("xy",v[6:8])) for v in unique],
                "submeshes": [{"triangles": triangles}]}
        (ROOT / f"Assets/Inventory/{name}.json").write_text(json.dumps(data,separators=(",",":"))+"\n")
        print(f"{name}: {len(unique)} vertices, {len(triangles)//3} triangles, {len(caps)} cut caps")


def main():
    for name, height, center_y in [("Shield", .5, .25), ("Phone", .4, .2)]:
        source = ROOT / f"Assets/Models/Items/{name}.fbx"
        transform = alignment(source, height, center_y)
        data, indices = export(source, ROOT / f"Assets/Inventory/{name}Mesh.asset", name, transform)
        if name == "Shield":
            write_halves(data, indices)


if __name__ == "__main__":
    main()
