Try out the [Demo](https://atlergibby.itch.io/sdf-sculptor-demo)

# Features
* SDF based sculpting / modelling.
* Easily flip geometry across X, y, or Z axis to mirror geometry.
* Surface spawning tool for automatically scattering geometry across other geometry.
* Creating "fuzzy" geometry for things like foliage or clouds by randomizing position, rotation, and scale of each face.
* All geometry made for different LODs can be stored as a JSON and loaded at the start of a scene.
* GPU Instancing and occlusion culling data is generated at the start of a scene; no need to bake anything beforehand (Occlusion culling comes at a performance cost).
* SDF Sculptor LOD, instancing, and occlusion culling works with multiple cameras.
* Dynamic objects can also be instanced and culled.
* Shaders are built on Unity's Shader Graph so no external tools are needed for shader customization.
* C# functions for sculpting and rendering sculpts.
* Able to export meshes as FBX files.
* Able to create 3D textures from sculpts and export them as 2D texture atlases.
* Includes Demo scenes (Had to remove some to upload on GitHub) and PDF Documentation.

# Requirements
* Unity 2022.3 or later.
* Uses Render Piepeline features so URP / HDRP is required.
* Dedicated GPUs are highly recommended for sculpting performance (Many SDF brushes and sculpting with a high resolution can negatively impact performance).
