"""Run with Blender background mode to validate the unchanged migration GLBs."""
import json
from pathlib import Path
import bpy

root = Path(__file__).resolve().parents[1] / "SourceMeshes"
paths = sorted(root.glob("*.glb")) + sorted((root / "Cars").glob("*.glb"))
for path in paths:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(path))
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    polygons = sum(len(obj.data.polygons) for obj in meshes)
    if result != {"FINISHED"} or len(meshes) == 0 or polygons == 0:
        raise RuntimeError(f"Empty Blender scene for {path}")
    print("APEX_IMPORT", json.dumps({"asset": path.name, "meshes": len(meshes),
                                      "polygons": polygons}), flush=True)
