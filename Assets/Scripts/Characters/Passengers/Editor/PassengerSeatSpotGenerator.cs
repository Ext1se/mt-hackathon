using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Game.Characters.Passengers.Editor
{
    /// <summary>
    /// Creates seat <see cref="PassengerSpot"/>s from seat-block meshes. The block is probed with vertical rays along
    /// its depth to find the cushion height and the backrest; the seat faces away from the backrest.
    /// Blocks are expected to be upright with seats side by side along <c>rowAxis</c>, a horizontal world direction
    /// (across the wagon for rows of airline-style seats).
    /// </summary>
    public static class PassengerSeatSpotGenerator
    {
        private const float DefaultSeatWidth = 0.52f;
        private const float ProbeStep = 0.025f;
        private const float CushionMin = 0.25f;
        private const float CushionMax = 0.7f;
        private const float BackrestMin = 0.85f;
        // The sitting clips put the hips about 0.15 m in front of the backrest and the root 0.04 m behind the hips.
        private const float RootFromBackrest = 0.12f;

        /// <summary>Creates spots for every block under <paramref name="parent"/> and returns how many were made.</summary>
        public static int CreateSeatSpots(IReadOnlyList<Renderer> seatBlocks, Transform parent, Vector3 rowAxis,
            float seatWidth = DefaultSeatWidth)
        {
            rowAxis = Vector3.ProjectOnPlane(rowAxis, Vector3.up).normalized;
            Vector3 depthAxis = Vector3.Cross(rowAxis, Vector3.up);
            int created = 0;
            List<float> cushionHeights = new List<float>();
            for (int i = 0; i < seatBlocks.Count; i++)
            {
                created += CreateBlockSpots(seatBlocks[i], parent, rowAxis, depthAxis, seatWidth, cushionHeights);
            }

            return created;
        }

        private static int CreateBlockSpots(Renderer block, Transform parent, Vector3 rowAxis, Vector3 depthAxis, float seatWidth,
            List<float> cushionHeights)
        {
            if (!block.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
            {
                Debug.LogWarning($"Seat block '{block.name}' has no mesh.", block);
                return 0;
            }

            Bounds bounds = block.bounds;
            float rowLength = Mathf.Abs(bounds.size.x * rowAxis.x) + Mathf.Abs(bounds.size.z * rowAxis.z);
            int seatCount = Mathf.Max(1, Mathf.RoundToInt(rowLength / seatWidth));
            float seatPitch = rowLength / seatCount;
            Vector3 rowStart = bounds.center - rowAxis * (rowLength * 0.5f);

            // Mesh colliders bake non-readable meshes in the editor, which is what makes probing possible.
            MeshCollider probe = block.gameObject.AddComponent<MeshCollider>();
            probe.sharedMesh = meshFilter.sharedMesh;
            Physics.SyncTransforms();

            Vector3 firstSeat = rowStart + rowAxis * (seatPitch * 0.5f);
            bool isProbed = TryProbe(probe, bounds, firstSeat, depthAxis, cushionHeights,
                out float cushionHeight, out float backrestFront, out float forwardSign);
            Object.DestroyImmediate(probe);

            if (!isProbed)
            {
                Debug.LogWarning($"Seat block '{block.name}': cannot find cushion and backrest.", block);
                return 0;
            }

            Vector3 forward = depthAxis * forwardSign;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
            for (int i = 0; i < seatCount; i++)
            {
                Vector3 seatCenter = rowStart + rowAxis * (seatPitch * (i + 0.5f));
                Vector3 position = seatCenter - depthAxis * Vector3.Dot(seatCenter, depthAxis)
                    + depthAxis * (backrestFront + forwardSign * RootFromBackrest);
                position.y = cushionHeight - PassengerSpot.SeatHeight;

                GameObject spotObject = new GameObject($"Seat_{block.name}_{i}");
                Undo.RegisterCreatedObjectUndo(spotObject, "Create Seat Spots");
                spotObject.transform.SetParent(parent, false);
                spotObject.transform.SetPositionAndRotation(position, rotation);
                spotObject.AddComponent<PassengerSpot>();
            }

            return seatCount;
        }

        private static bool TryProbe(MeshCollider probe, Bounds bounds, Vector3 seatCenter, Vector3 depthAxis, List<float> cushionHeights,
            out float cushionHeight, out float backrestFront, out float forwardSign)
        {
            cushionHeights.Clear();
            float cushionDepthSum = 0f;
            float backrestDepthSum = 0f;
            int backrestHits = 0;
            float depthCenter = Vector3.Dot(bounds.center, depthAxis);
            float depthExtent = Mathf.Abs(bounds.extents.x * depthAxis.x) + Mathf.Abs(bounds.extents.z * depthAxis.z);
            float depthMin = depthCenter - depthExtent;
            float depthMax = depthCenter + depthExtent;
            float floor = bounds.min.y;

            for (float depth = depthMin; depth <= depthMax; depth += ProbeStep)
            {
                Vector3 origin = seatCenter - depthAxis * Vector3.Dot(seatCenter, depthAxis) + depthAxis * depth;
                origin.y = bounds.max.y + 0.1f;
                if (!probe.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, bounds.size.y + 0.2f))
                {
                    continue;
                }

                float height = hit.point.y - floor;
                if (height >= CushionMin && height <= CushionMax)
                {
                    cushionHeights.Add(hit.point.y);
                    cushionDepthSum += depth;
                }
                else if (height >= BackrestMin)
                {
                    backrestDepthSum += depth;
                    backrestHits++;
                }
            }

            cushionHeight = 0f;
            backrestFront = 0f;
            forwardSign = 1f;
            if (cushionHeights.Count == 0 || backrestHits == 0)
            {
                return false;
            }

            float cushionDepth = cushionDepthSum / cushionHeights.Count;
            float backrestDepth = backrestDepthSum / backrestHits;
            forwardSign = Mathf.Sign(cushionDepth - backrestDepth);

            cushionHeights.Sort();
            cushionHeight = cushionHeights[cushionHeights.Count / 2];

            // The backrest front is the last backrest sample before the cushion starts.
            backrestFront = backrestDepth;
            for (float depth = depthMin; depth <= depthMax; depth += ProbeStep)
            {
                Vector3 origin = seatCenter - depthAxis * Vector3.Dot(seatCenter, depthAxis) + depthAxis * depth;
                origin.y = bounds.max.y + 0.1f;
                if (probe.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, bounds.size.y + 0.2f)
                    && hit.point.y - floor >= BackrestMin
                    && (depth - backrestFront) * forwardSign > 0f)
                {
                    backrestFront = depth;
                }
            }

            return true;
        }
    }
}
