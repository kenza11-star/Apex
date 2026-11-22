"""Stage inputs from the immutable APEX V1.12 Godot archive for the Unity lab."""
from argparse import ArgumentParser
from hashlib import sha256
from io import BytesIO
from pathlib import Path
import json
import math
import struct
from zipfile import ZipFile
from PIL import Image

CARS = ("mclaren", "pagani", "lexus", "f1", "eb110", "murcielago")
ROOT = "APEX-RACING-V1.12/"
project = Path(__file__).resolve().parents[1]
resource_root = project / "Assets/Resources/APEX"


def catalog(data):
    magic, version, total, size, tag = struct.unpack_from("<4sIII4s", data)
    if (magic, version, total, tag) != (b"glTF", 2, len(data), b"JSON"):
        raise ValueError("Malformed glTF container")
    doc = json.loads(data[20:20+size])
    if any("uri" in item for label in ("buffers", "images") for item in doc.get(label, [])):
        raise ValueError("Referenced resource must be embedded")
    return doc


def write_route(source):
    row_position = lambda p: (-p[0], p[2] if len(p) > 2 else 0.0, p[1])
    dest = BytesIO()
    dest.write(struct.pack("<4sII", b"APX1", len(source["points"]), len(source["walls"])))
    for point in source["points"]:
        dest.write(struct.pack("<3f", *row_position(point)))
    for row in source["walls"]:
        if len(row) == 7:
            x1, y1, z1, x2, y2, z2, height = row
        elif len(row) == 5:
            x1, z1, x2, z2, height = row
            y1, y2 = 0, 0
        else:
            raise ValueError("Unsupported wall format")
        dest.write(struct.pack("<7f", -x1, y1 - .12, z1, -x2, y2 - .12, z2, height + .12))
    return dest.getvalue()


def unpack_pavement(packed):
    scan = Image.open(BytesIO(packed)).convert("RGB")
    normal = Image.new("RGB", scan.size)
    mask = Image.new("RGBA", scan.size)
    channels = scan.getdata()
    def decode(pixel):
        x, y, roughness = pixel
        z = math.sqrt(max(0.0, 1.0 - (x/127.5-1)**2 - (y/127.5-1)**2))
        return x, y, round(127.5*(z+1))
    normal.putdata([decode(pixel) for pixel in channels])
    mask.putdata([(0, 0, 0, 255-roughness) for _,_,roughness in channels])
    result = []
    for image in (normal, mask):
        buffer = BytesIO()
        image.save(buffer, format="PNG")
        result.append(buffer.getvalue())
    return result


def main(source_path):
    metadata = {}
    def write(path, data):
        target = (project / "SourceMeshes" if path.endswith(".glb") else resource_root) / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        metadata[path] = {"bytes": len(data), "sha256": sha256(data).hexdigest()}

    expected = (project/"GODOT_BASELINE.sha256").read_text().split()[0]
    if sha256(source_path.read_bytes()).hexdigest() != expected:
        raise ValueError("Expected the original APEX V1.12 source ZIP")
    with ZipFile(source_path) as archive:
        for destination, original in [(f"Cars/{car}.glb", f"assets/cars/{car}.glb") for car in CARS] + [
                ("Forest.glb", "assets/forest/forest.glb"), ("Marina.glb", "assets/circuit/marina.glb"),
                ("Plants.glb", "assets/forest/plants.glb")]:
            data = archive.read(ROOT+original)
            doc = catalog(data)
            write(destination, data)
            metadata[destination]["nodes"] = len(doc["nodes"])
            metadata[destination]["triangles"] = sum(
                doc["accessors"][p["indices"]]["count"]//3 for mesh in doc["meshes"]
                for p in mesh["primitives"] if p.get("mode",4)==4 and "indices" in p)
        for name, path in [("Forest", "forest"), ("Marina", "circuit")]:
            source_route = archive.read(ROOT+f"assets/{path}/route.json")
            write(name+"Route.bytes", write_route(json.loads(source_route)))
        for key in ("ground", "plants"):
            write(key+".bytes", archive.read(ROOT+f"assets/forest/{key}.bin"))
        for stem, name in [("course_color","RoadColor"),("duff_color","VergeColor"),
                           ("forest_color","FloorColor"),("pit_color","ApronColor")]:
            write(f"Scans/{name}.jpg", archive.read(ROOT+f"assets/forest/scans/{stem}.jpg"))
        normal, mask = unpack_pavement(archive.read(ROOT+"assets/forest/scans/course_nr.jpg"))
        write("Scans/RoadNormal.png", normal)
        write("Scans/RoadMask.png", mask)
        (project/"ASSET-CREDITS.txt").write_bytes(archive.read(ROOT+"legal/ASSET-CREDITS.txt"))
    (project/"Verification/assets.json").write_text(json.dumps(metadata, indent=2)+"\n",encoding="utf-8")
    print(len(metadata), "staged assets")


if __name__ == "__main__":
    parser = ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path)
    main(parser.parse_args().archive)
