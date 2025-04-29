using UnityEngine;

namespace WaffleWare.ConcaveRigidbodies
{
    public class ExactConvexDecomp : MonoBehaviour
    {
        public enum ColliderType { Prism, Tetrahedron, BoxOBB, BoxAABB };
        public ColliderType colliderType;

        [Range(.001f, .01f)]
        public float width = .005f;
        public bool invertColliders;
        public bool centered;

        private float magnitude;

        public bool addRigidBody = true;
        public bool gravity = true;

        private GameObject target;
        private Vector3[] vertices;
        private int[] triangles;

        private GameObject convexColliders;
        private MeshCollider[] unwantedColliders;

        private void Awake()
        {
            Initialize();

            AddColliders();

            init = true;
        }

        public void Initialize()
        {
            if (target == null)
                target = this.gameObject;

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null)
            {
                Debug.LogError("MeshFilter Required");
                Debug.DebugBreak();
            }

            Mesh mesh = meshFilter.sharedMesh;
            vertices = mesh.vertices;
            triangles = mesh.triangles;

            unwantedColliders = GetComponentsInChildren<MeshCollider>();

            ogWidth = width;
            ogInvert = invertColliders;
            ogCenter = centered;
            ogCT = colliderType;
        }

        public void AddColliders()
        {
            ResetMesh();

            foreach (MeshCollider meshCollider in unwantedColliders)
                meshCollider.enabled = false;

            convexColliders = new GameObject("Convex Colliders");
            convexColliders.transform.parent = target.transform;
            convexColliders.transform.position = Vector3.zero;

            magnitude = width;
            if (!invertColliders)
                magnitude *= -1f;

            for (int i = 0; i < triangles.Length / 3; i++)
            {
                Vector3 p0 = target.transform.TransformPoint(vertices[triangles[i * 3 + 0]]);
                Vector3 p1 = target.transform.TransformPoint(vertices[triangles[i * 3 + 1]]);
                Vector3 p2 = target.transform.TransformPoint(vertices[triangles[i * 3 + 2]]);

                if (!CheckSliver(p0, p1, p2))
                    TriMeshGen(p0, p1, p2, i);
            }

            AddRigidBody();
        }
        bool CheckSliver(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 AB = v1 - v0;
            Vector3 AC = v2 - v0;
            Vector3 BC = v2 - v1;
            float area = (Vector3.Cross(AB, AC).magnitude) / 2;
            float perimeter = AB.magnitude + AC.magnitude + BC.magnitude;
            float sliverParameter = ((2 * area) / perimeter);

            if (sliverParameter < 0.001f)
                return true;
            return false;
        }
        void TriMeshGen(Vector3 v0, Vector3 v1, Vector3 v2, int triangleIndex)
        {
            switch (colliderType)
            {
                case ColliderType.BoxOBB:
                    OrientedBoxCollider(v0, v1, v2, triangleIndex);
                    break;
                case ColliderType.BoxAABB:
                    AABBBoxCollider(v0, v1, v2, triangleIndex);
                    break;
                case ColliderType.Tetrahedron:
                    Tetrahedron(v0, v1, v2);
                    break;
                case ColliderType.Prism:
                    if (!centered)
                        TriangularPrism(v0, v1, v2);
                    else
                        TriangularPrismCentered(v0, v1, v2);
                    break;
            }
        }
        void OrientedBoxCollider(Vector3 v0, Vector3 v1, Vector3 v2, int triangleIndex)
        {
            Vector3 xAxis = new Vector3();
            Vector3 yAxis = new Vector3();

            Plane plane = new Plane(v0, v1, v2);
            Vector3 basisA = plane.normal;
            Vector3.OrthoNormalize(ref basisA, ref xAxis, ref yAxis);

            Vector3 centroid = (v0 + v1 + v2) / 3;
            Vector3 normal = plane.normal;

            Vector2 v20 = Vector3ToUV(v0, xAxis, yAxis, centroid);
            Vector2 v21 = Vector3ToUV(v1, xAxis, yAxis, centroid);
            Vector2 v22 = Vector3ToUV(v2, xAxis, yAxis, centroid);

            Vector2 edge = FindEdge(v20, v21, v22);

            float angle = Vector2.Angle(edge, Vector2.right);
            Quaternion rotation = Quaternion.Euler(0, 0, angle);

            Vector2 vr0 = rotation * v20;
            Vector2 vr1 = rotation * v21;
            Vector2 vr2 = rotation * v22;

            float minX = Mathf.Min(vr0.x, vr1.x, vr2.x);
            float maxX = Mathf.Max(vr0.x, vr1.x, vr2.x);
            float minY = Mathf.Min(vr0.y, vr1.y, vr2.y);
            float maxY = Mathf.Max(vr0.y, vr1.y, vr2.y);

            Vector2 p0 = new Vector2(minX, minY);
            Vector2 p1 = new Vector2(minX, maxY);
            Vector2 p2 = new Vector2(maxX, maxY);
            Vector2 p3 = new Vector2(maxX, minY);

            GameObject go = new GameObject("g" + triangleIndex.ToString());
            BoxCollider boxCollider = go.AddComponent<BoxCollider>();

            if (centered)
                go.transform.position = centroid;
            else
                go.transform.position = centroid + 0.5f * magnitude * normal;

            go.transform.rotation = Quaternion.LookRotation(normal, yAxis);
            go.transform.Rotate(0, 0, -angle);

            boxCollider.center = (p0 + p1 + p2 + p3) / 4;
            boxCollider.size = new Vector3(maxX - minX, maxY - minY, width);

            go.transform.parent = convexColliders.transform;
        }
        Vector2 FindEdge(Vector2 v0, Vector2 v1, Vector2 v2)
        {
            float d0 = Vector2.Distance(v0, v1);
            float d1 = Vector2.Distance(v1, v2);
            float d2 = Vector2.Distance(v2, v0);

            if (d0 > d1 && d0 > d2)
                return v1.y > v0.y ? v0 - v1 : v1 - v0;
            if (d1 > d2 && d1 > d0)
                return v2.y > v1.y ? v1 - v2 : v2 - v1;
            return v0.y > v2.y ? v2 - v0 : v0 - v2;
        }
        Vector2 Vector3ToUV(Vector3 point, Vector3 xAxis, Vector3 yAxis, Vector3 center)
        {
            Vector3 offset = point - center;
            return new Vector2((Vector3.Dot(offset, xAxis)), (Vector3.Dot(offset, yAxis)));
        }

        void AABBBoxCollider(Vector3 v0, Vector3 v1, Vector3 v2, int triangleIndex)
        {
            Mesh triMesh = new Mesh();

            Vector3[] triVerts = new Vector3[3] { v0, v1, v2 };
            triMesh.vertices = triVerts;

            int[] tris = new int[3] { 0, 1, 2 };
            triMesh.triangles = tris;

            GameObject triangle = new GameObject("t" + triangleIndex.ToString());
            triangle.AddComponent<MeshRenderer>();
            MeshFilter meshFilter = triangle.AddComponent<MeshFilter>();
            meshFilter.mesh = triMesh;
            triangle.AddComponent<BoxCollider>();
            triangle.transform.parent = convexColliders.transform;

            Destroy(meshFilter);
            Destroy(triangle.GetComponent<MeshRenderer>());
        }

        void Tetrahedron(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 centroid = (v0 + v1 + v2) / 3;
            Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
            Vector3 apex = (normal * magnitude) + centroid;

            Mesh triangleMesh = new Mesh();

            Vector3[] triangleVertices = new Vector3[4]
            {
            v0,
            v1,
            v2,
            apex
            };
            triangleMesh.vertices = triangleVertices;
            int[] tris = new int[9]
            {
            0,2,3,
            0,3,1,
            3,2,1,
            };
            triangleMesh.triangles = tris;
            MeshCollider meshCollider = convexColliders.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = triangleMesh;
            meshCollider.convex = true;
        }

        void TriangularPrism(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
            Vector3 v0a = (normal * magnitude) + v0;
            Vector3 v1a = (normal * magnitude) + v1;
            Vector3 v2a = (normal * magnitude) + v2;

            Mesh triangleMesh = new Mesh();

            Vector3[] triangleVertices = new Vector3[6]
            {
            v0,
            v1,
            v2,
            v0a,
            v1a,
            v2a,
            };
            triangleMesh.vertices = triangleVertices;
            int[] tris = new int[12]
            {
            0,1,2,
            3,4,5,
            0,3,2,
            2,3,4,
            };
            triangleMesh.triangles = tris;
            MeshCollider meshCollider = convexColliders.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = triangleMesh;
            meshCollider.convex = true;
        }


        void TriangularPrismCentered(Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 normal = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
            Vector3 v0a = (normal * magnitude * 0.5f) + v0;
            Vector3 v1a = (normal * magnitude * 0.5f) + v1;
            Vector3 v2a = (normal * magnitude * 0.5f) + v2;

            Vector3 v0b = (normal * magnitude * -0.5f) + v0;
            Vector3 v1b = (normal * magnitude * -0.5f) + v1;
            Vector3 v2b = (normal * magnitude * -0.5f) + v2;

            Mesh triangleMesh = new Mesh();

            Vector3[] triangleVertices = new Vector3[6]
            {
            v0a,
            v1a,
            v2a,
            v0b,
            v1b,
            v2b,
            };
            triangleMesh.vertices = triangleVertices;
            int[] tris = new int[12]
            {
            0,1,2,
            3,4,5,
            0,3,2,
            2,3,4,
            };
            triangleMesh.triangles = tris;
            MeshCollider meshCollider = convexColliders.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = triangleMesh;
            meshCollider.convex = true;
        }

        public void ResetMesh()
        {
            //if (TryGetComponent<Rigidbody>(out Rigidbody rigidbody))
            //{
            //    rigidbody.isKinematic = true;
            //    Destroy(rigidbody);
            //}

            Destroy(convexColliders);

            foreach (MeshCollider meshCollider in unwantedColliders)
                meshCollider.enabled = true;
        }
        void AddRigidBody()
        {
            if (addRigidBody)
            {
                if (target.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody rigidbody = target.AddComponent<Rigidbody>();
                    if (!gravity)
                        rigidbody.useGravity = false;
                }
            }
        }

        private float ogWidth;
        private bool init, ogInvert, ogCenter;
        private ColliderType ogCT;
        private void OnValidate()
        {
            if ((Application.isPlaying) && (init))
            {
                if ((width != ogWidth) || (ogInvert != invertColliders) || (ogCenter != centered) || (ogCT != colliderType))
                {
                    ogCT = colliderType;
                    ogWidth = width;
                    ogInvert = invertColliders;
                    ogCenter = centered;
                    AddColliders();
                }
            }
        }
    }
}