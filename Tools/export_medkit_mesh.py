"""Bake the supplied FBX mesh: blender -b --python Tools/export_medkit_mesh.py.

Keeps the original model untouched. The runtime prefab uses this native mesh so
its reference does not depend on generated FBX subasset IDs. UVs and split
normals come from the model; the prefab supplies its size and orientation.
"""
from pathlib import Path
import struct

import bpy
from mathutils import Vector


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "Assets/Models/Items/Medkit.fbx"
OUTPUT = ROOT / "Assets/Inventory/MedkitMesh.asset"


def vector(value):
    return "{x: %.9g, y: %.9g, z: %.9g}" % tuple(value)


def export(source=SOURCE, output=OUTPUT, name="Medkit", transform=None):
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(source))
    objects = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    if len(objects) != 1:
        raise ValueError("Expected the supplied model's single mesh")
    obj = objects[0]
    mesh = obj.data
    mesh.calc_loop_triangles()
    matrix = obj.matrix_world
    normal_matrix = matrix.to_3x3().inverted().transposed()
    vertices, indices, lookup = [], [], {}
    for triangle in mesh.loop_triangles:
        # Swapping Blender Y/Z converts Z-up to Unity Y-up and changes handedness.
        for loop_index in reversed(triangle.loops):
            loop = mesh.loops[loop_index]
            point = matrix @ mesh.vertices[loop.vertex_index].co
            normal = (normal_matrix @ mesh.corner_normals[loop_index].vector).normalized()
            uv = mesh.uv_layers.active.data[loop_index].uv
            point = Vector((point.x, point.z, point.y))
            normal = Vector((normal.x, normal.z, normal.y))
            if transform is not None:
                point = transform @ point
                normal = (transform.to_3x3().inverted().transposed() @ normal).normalized()
            data = struct.pack("<8f", *point, *normal, uv.x, uv.y)
            if data not in lookup:
                lookup[data] = len(vertices)
                vertices.append(data)
            indices.append(lookup[data])
    if len(vertices) > 65535:
        raise ValueError("Mesh no longer fits its 16-bit index buffer")
    points = [struct.unpack("<8f", data)[:3] for data in vertices]
    minimum = [min(p[i] for p in points) for i in range(3)]
    maximum = [max(p[i] for p in points) for i in range(3)]
    center = [(a + b) * 0.5 for a, b in zip(minimum, maximum)]
    extent = [(b - a) * 0.5 for a, b in zip(minimum, maximum)]
    channels = []
    for index in range(14):
        offset, dimension = {0: (0, 3), 1: (12, 3), 4: (24, 2)}.get(index, (0, 0))
        channels.append(f"    - stream: 0\n      offset: {offset}\n      format: 0\n      dimension: {dimension}")
    text = f"""%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!43 &4300000
Mesh:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_Name: {name}
  serializedVersion: 10
  m_SubMeshes:
  - serializedVersion: 2
    firstByte: 0
    indexCount: {len(indices)}
    topology: 0
    baseVertex: 0
    firstVertex: 0
    vertexCount: {len(vertices)}
    localAABB:
      m_Center: {vector(center)}
      m_Extent: {vector(extent)}
  m_Shapes:
    vertices: []
    shapes: []
    channels: []
    fullWeights: []
  m_BindPose: []
  m_BoneNameHashes:
  m_RootBoneNameHash: 0
  m_BonesAABB: []
  m_VariableBoneCountWeights:
    m_Data:
  m_MeshCompression: 0
  m_IsReadable: 0
  m_KeepVertices: 0
  m_KeepIndices: 0
  m_IndexFormat: 0
  m_IndexBuffer: {struct.pack('<' + 'H' * len(indices), *indices).hex()}
  m_VertexData:
    serializedVersion: 3
    m_VertexCount: {len(vertices)}
    m_Channels:
{chr(10).join(channels)}
    m_DataSize: {len(vertices) * 32}
    _typelessdata: {b''.join(vertices).hex()}
  m_CompressedMesh:
"""
    for name, floating in [("Vertices", True), ("UV", True), ("Normals", False),
                           ("Tangents", False), ("Weights", False), ("NormalSigns", False),
                           ("TangentSigns", False), ("FloatColors", True),
                           ("BoneIndices", False), ("Triangles", False)]:
        text += f"    m_{name}:\n      m_NumItems: 0\n"
        if floating:
            text += "      m_Range: 0\n      m_Start: 0\n"
        text += "      m_Data:\n      m_BitSize: 0\n"
    text += f"""    m_UVInfo: 0
  m_LocalAABB:
    m_Center: {vector(center)}
    m_Extent: {vector(extent)}
  m_MeshUsageFlags: 0
  m_CookingOptions: 30
  m_BakedConvexCollisionMesh:
  m_BakedTriangleCollisionMesh:
  m_MeshMetrics[0]: 1
  m_MeshMetrics[1]: 1
  m_MeshOptimizationFlags: 1
  m_StreamData:
    serializedVersion: 2
    offset: 0
    size: 0
    path:
"""
    output.write_text(text)
    print(f"Exported {len(vertices)} vertices / {len(indices) // 3} triangles to {output}")
    print(f"Source mesh size in metres: {[b-a for a,b in zip(minimum,maximum)]}")
    return vertices, indices


if __name__ == "__main__":
    export()
