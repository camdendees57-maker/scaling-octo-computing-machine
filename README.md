# WallWalker

A BONELAB MelonLoader code mod that lets the player attach to solid surfaces and walk across walls and ceilings.

## Controls

- Touch a solid surface with either hand to attach.
- Touch another surface while attached to transfer the walking surface.
- F: attach using the surface you're looking at, or toggle off if already attached.
- G: detach.
- Space: jump away from the current surface.
- VR thumbstick: move across the active surface.

The mod redirects gravity for the player physics rig toward the touched surface and projects thumbstick movement onto that surface.

## Build

The project uses the public Bonelab.GameLibs.Steam NuGet package for stripped/publicized BONELAB Patch 6 assemblies. Game references are restored during CI rather than committed to this repository.

GitHub Actions builds WallWalker.dll on every push to main and also supports manual dispatch. The compiled DLL is uploaded as the WallWalker workflow artifact.

## Install

Copy the resulting WallWalker.dll into the BONELAB Mods folder with MelonLoader installed.

This repository contains source code only and does not redistribute the original game DLLs.
