using System.Collections.Generic;
using UnityEngine;

static public class FullscreenQuads
{
	// creates a quad for wrapped multi-tap shader
	static public Mesh CreateWrappedMultitap(int resolution)
	{
		var mesh = new Mesh();
		mesh.hideFlags = HideFlags.DontSave;

		float minC = -1.0f + 2.0f / resolution;
		float maxC = 1.0f - 2.0f / resolution;
		
		mesh.vertices = new Vector3[] {
				new Vector3(minC, minC, 0.5f), new Vector3(maxC, minC, 0.5f), new Vector3(maxC, maxC, 0.5f), new Vector3(minC, maxC, 0.5f),
				new Vector3(minC, -1.0f, 0.5f), new Vector3(maxC, -1.0f, 0.5f), new Vector3(maxC, minC, 0.5f), new Vector3(minC, minC, 0.5f),
				new Vector3(minC, maxC, 0.5f), new Vector3(maxC, maxC, 0.5f), new Vector3(maxC, 1.0f, 0.5f), new Vector3(minC, 1.0f, 0.5f),
				new Vector3(-1.0f, minC, 0.5f), new Vector3(minC, minC, 0.5f), new Vector3(minC, maxC, 0.5f), new Vector3(-1.0f, maxC, 0.5f),
				new Vector3(maxC, minC, 0.5f), new Vector3(1.0f, minC, 0.5f), new Vector3(1.0f, maxC, 0.5f), new Vector3(maxC, maxC, 0.5f),
				new Vector3(-1.0f, -1.0f, 0.5f), new Vector3(minC, -1.0f, 0.5f), new Vector3(minC, minC, 0.5f), new Vector3(-1.0f, minC, 0.5f),
				new Vector3(maxC, -1.0f, 0.5f), new Vector3(1.0f, -1.0f, 0.5f), new Vector3(1.0f, minC, 0.5f), new Vector3(maxC, minC, 0.5f),
				new Vector3(-1.0f, maxC, 0.5f), new Vector3(minC, maxC, 0.5f), new Vector3(minC, 1.0f, 0.5f), new Vector3(-1.0f, 1.0f, 0.5f),
				new Vector3(maxC, maxC, 0.5f), new Vector3(1.0f, maxC, 0.5f), new Vector3(1.0f, 1.0f, 0.5f), new Vector3(maxC, 1.0f, 0.5f)
			};

		float min = 0.0f + 1.0f / resolution;
		float max = 1.0f - 1.0f / resolution;

		mesh.uv = new Vector2[] {
				new Vector2(min, min), new Vector2(max, min), new Vector2(max, max), new Vector2(min, max),
				new Vector2(min, 0.0f), new Vector2(max, 0.0f), new Vector2(max, min), new Vector2(min, min),
				new Vector2(min, max), new Vector2(max, max), new Vector2(max, 1.0f), new Vector2(min, 1.0f),
				new Vector2(0.0f, min), new Vector2(min, min), new Vector2(min, max), new Vector2(0.0f, max),
				new Vector2(max, min), new Vector2(1.0f, min), new Vector2(1.0f, max), new Vector2(max, max),
				new Vector2(0.0f, 0.0f), new Vector2(min, 0.0f), new Vector2(min, min), new Vector2(0.0f, min),
				new Vector2(max, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(1.0f, min), new Vector2(max, min),
				new Vector2(0.0f, max), new Vector2(min, max), new Vector2(min, 1.0f), new Vector2(0.0f, 1.0f),
				new Vector2(max, max), new Vector2(1.0f, max), new Vector2(1.0f, 1.0f), new Vector2(max, 1.0f)
			};

		Vector4[] uvs = new Vector4[36];
		
		OffsetUV(mesh.uv, new Vector2(1.0f / resolution, 0.0f), new Vector2(-1.0f / resolution, 0.0f), uvs);
        mesh.SetUVs(1, new List<Vector4>(uvs));

		OffsetUV(mesh.uv, new Vector2(0.0f, 1.0f / resolution), new Vector2(0.0f , -1.0f / resolution), uvs);
		mesh.SetUVs(2, new List<Vector4>(uvs));

		int[] indices = new int[36];

		for(int i = 0; i < 36; ++i)
			indices[i] = i;

		mesh.SetIndices(indices, MeshTopology.Quads, 0);

		return mesh;
	}

	static private void OffsetUV(Vector2[] uv, Vector2 offset1, Vector2 offset2, Vector4[] output)
	{
		// offset
		for(int i=0; i<uv.Length; ++i)
		{
			Vector2 a = uv[i] + offset1;
			Vector2 b = uv[i] + offset2;

			output[i] = new Vector4(a.x, a.y, b.x, b.y);
		}

		// wrap
		for(int i=0; i<uv.Length;)
		{
			Vector4 d = GetWrapDir(output, i);

			output[i++] += d;
			output[i++] += d;
			output[i++] += d;
			output[i++] += d;
		}
	}

	static private Vector4 GetWrapDir(Vector4[] output, int startIndex)
	{
		Vector4 d = Vector4.zero;

		for(int i= startIndex; i<startIndex+4; ++i)
		{
			for(int ii = 0; ii < 4; ++ii)
			{
				if(output[i][ii] < 0.0f) d[ii] = 1.0f;
				if(output[i][ii] > 1.0f) d[ii] = -1.0f;
			}
		}

		return d;
	}
}
