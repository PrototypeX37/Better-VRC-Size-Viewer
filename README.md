# Better-VRC-Size-Viewer
A revised, recoded VRC-Size Viewer and optimizer.

This script does the following:

- View Build info once you have sucessfully built the project including sizes of each type of file included

- Allows you to crunch all textures quality using crunch compression.
- Adjust the quality of compression
- Adjust the processing speed of compressino
- Change the resolution of textures

- Optimize meshes



------------------------------------------------------------------------------
  The mesh optimiser does the following:

- Vertex Optimization: The method performs vertex optimization, which involves merging identical vertices that share the same position, normal, and UV coordinates. This reduces the number of unique vertices in the mesh, leading to memory savings and improved rendering performance.

- Triangle Optimization: The optimization also looks for duplicate or redundant triangles and removes them. It ensures that the mesh is represented in the most efficient way possible, reducing the number of triangles without affecting the mesh's visual appearance.

- Submesh Optimization: Meshes can have multiple submeshes, each representing a different material on the same object. The optimization ensures that submeshes are organized and stored efficiently, reducing memory overhead and rendering overhead when rendering the mesh with multiple materials.

- Vertex Cache Optimization: The method reorders the vertices and triangles in a way that maximizes the utilization of the GPU's vertex cache. This optimization can significantly improve rendering performance, as GPUs tend to work more efficiently with sequentially accessed vertex data.

- Readability and Memory Optimization: Tries to improve the memory layout and data access patterns to enhance data read/write efficiency during rendering. 
