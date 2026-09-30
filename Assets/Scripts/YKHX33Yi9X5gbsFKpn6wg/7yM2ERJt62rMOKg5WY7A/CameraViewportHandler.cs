using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
public class CameraViewportHandler : MonoBehaviour
{
    public enum Constraint
    {
        Landscape,
        Portrait
    }

    private static CameraViewportHandler Instance;
    private Color wireColor = Color.white;
    private float UnitsSize = 1;
    private Constraint constraint = Constraint.Portrait;
    private new Camera camera;
    //public bool executeInUpdate;
    private float Width { get; set; }
    private float Height { get; set; }
    private Vector3 BottomLeft { get; set; }
    private Vector3 BottomCenter { get; set; }
    private Vector3 BottomRight { get; set; }
    private Vector3 MiddleLeft { get; set; }
    private Vector3 MiddleCenter { get; set; }
    private Vector3 MiddleRight { get; set; }
    private Vector3 TopLeft { get; set; }
    private Vector3 TopCenter { get; set; }
    private Vector3 TopRight { get; set; }

    private void Awake()
    {
        this.camera = this.GetComponent<Camera>();
        Instance = this;
        this.ComputeResolution();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = this.wireColor;
        Matrix4x4 temp = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(this.transform.position, this.transform.rotation, Vector3.one);
        if (this.camera.orthographic)
        {
            float spread = this.camera.farClipPlane - this.camera.nearClipPlane;
            float center = (this.camera.farClipPlane + this.camera.nearClipPlane) * 0.5f;
            Gizmos.DrawWireCube(new Vector3(0, 0, center), new Vector3(this.camera.orthographicSize * 2 * this.camera.aspect, this.camera.orthographicSize * 2, spread));
        }
        else
        {
            Gizmos.DrawFrustum(Vector3.zero, this.camera.fieldOfView, this.camera.farClipPlane, this.camera.nearClipPlane, this.camera.aspect);
        }

        Gizmos.matrix = temp;
    }

    private void ComputeResolution()
    {
        float leftX, rightX, topY, bottomY;
        if (this.constraint == Constraint.Landscape)
            this.camera.orthographicSize = 1f / this.camera.aspect * this.UnitsSize / 2f;
        else
            this.camera.orthographicSize = this.UnitsSize / 2f;
        this.Height = 2f * this.camera.orthographicSize;
        this.Width = this.Height * this.camera.aspect;
        float cameraX = this.camera.transform.position.x;
        float cameraY = this.camera.transform.position.y;
        leftX = cameraX - this.Width / 2;
        rightX = cameraX + this.Width / 2;
        topY = cameraY + this.Height / 2;
        bottomY = cameraY - this.Height / 2;
        this.BottomLeft = new Vector3(leftX, bottomY, 0);
        this.BottomCenter = new Vector3(cameraX, bottomY, 0);
        this.BottomRight = new Vector3(rightX, bottomY, 0);
        this.MiddleLeft = new Vector3(leftX, cameraY, 0);
        this.MiddleCenter = new Vector3(cameraX, cameraY, 0);
        this.MiddleRight = new Vector3(rightX, cameraY, 0);
        this.TopLeft = new Vector3(leftX, topY, 0);
        this.TopCenter = new Vector3(cameraX, topY, 0);
        this.TopRight = new Vector3(rightX, topY, 0);
    }
}