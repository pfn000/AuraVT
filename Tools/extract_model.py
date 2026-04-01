#!/usr/bin/env python3
"""
AuraVT — extract_model.py
Extracts face_landmark.onnx from the MediaPipe FaceLandmarker .task file.

Usage:
    python extract_model.py

The .task file is a ZIP archive containing the ONNX model.
This script downloads it, extracts the model, and places it in the
correct Unity Resources folder.

Requirements:
    pip install requests
"""

import os
import sys
import zipfile
import urllib.request
import shutil

TASK_URL = (
    "https://storage.googleapis.com/mediapipe-models/"
    "face_landmarker/face_landmarker/float16/latest/face_landmarker.task"
)
OUTPUT_DIR = os.path.join(
    os.path.dirname(__file__), "..",
    "UnityProject", "Assets", "AuraVT", "Resources", "Models"
)
OUTPUT_PATH = os.path.join(OUTPUT_DIR, "face_landmark.onnx")
TASK_TEMP   = os.path.join(os.path.dirname(__file__), "_face_landmarker.task")

def download(url, dest):
    print(f"Downloading {url} …")
    def progress(count, block, total):
        pct = min(count * block / total * 100, 100)
        print(f"\r  {pct:.1f}%", end="", flush=True)
    urllib.request.urlretrieve(url, dest, reporthook=progress)
    print()

def extract_onnx(task_path, output_path):
    print("Extracting ONNX model from .task bundle …")
    with zipfile.ZipFile(task_path, 'r') as z:
        names = z.namelist()
        # Find the face landmark ONNX inside the task bundle
        onnx_candidates = [n for n in names if n.endswith(".onnx") and "face" in n.lower()]
        if not onnx_candidates:
            # Fallback: any .onnx
            onnx_candidates = [n for n in names if n.endswith(".onnx")]
        if not onnx_candidates:
            print(f"ERROR: No .onnx found in bundle. Contents: {names}")
            sys.exit(1)
        src = onnx_candidates[0]
        print(f"  Found: {src}")
        data = z.read(src)

    os.makedirs(os.path.dirname(output_path), exist_ok=True)
    with open(output_path, 'wb') as f:
        f.write(data)
    size_mb = len(data) / 1_048_576
    print(f"  Saved: {output_path} ({size_mb:.1f} MB)")

def main():
    if os.path.exists(OUTPUT_PATH):
        print(f"Model already exists: {OUTPUT_PATH}")
        print("Delete it first if you want to re-download.")
        return

    try:
        download(TASK_URL, TASK_TEMP)
        extract_onnx(TASK_TEMP, OUTPUT_PATH)
    finally:
        if os.path.exists(TASK_TEMP):
            os.remove(TASK_TEMP)

    print("\n✅ Done! face_landmark.onnx is ready.")
    print("   In Unity: Window → Package Manager → import Unity Sentis,")
    print("   then press Play — AuraVT will use your webcam automatically.")

if __name__ == "__main__":
    main()
