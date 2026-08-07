using UnityEngine;

public class RailSpline : MonoBehaviour
{
    [Tooltip("World-space control points. A minimum of two points is required.")]
    [SerializeField] private Vector3[] controlPoints;

    [Tooltip("Number of samples used when calculating rail length and closest points.")]
    [SerializeField, Min(10)] private int sampleCount = 100;

    private float cachedLength = -1f;

    private void Awake()
    {
        RefreshLength();
    }

    public Vector3 GetPoint(float t)
    {
        if (controlPoints == null || controlPoints.Length < 2)
        {
            return transform.position;
        }

        return EvaluateCatmullRom(Mathf.Clamp01(t));
    }

    public Vector3 GetTangent(float t)
    {
        if (controlPoints == null || controlPoints.Length < 2)
        {
            return transform.forward;
        }

        const float sampleOffset = 0.001f;

        float previousT = Mathf.Clamp01(t - sampleOffset);
        float nextT = Mathf.Clamp01(t + sampleOffset);

        Vector3 tangent = GetPoint(nextT) - GetPoint(previousT);

        if (tangent.sqrMagnitude < 0.000001f)
        {
            return transform.forward;
        }

        return tangent.normalized;
    }

    public float ApproximateLength()
    {
        if (cachedLength <= 0f)
        {
            RefreshLength();
        }

        return cachedLength;
    }

    public float GetClosestT(Vector3 worldPosition)
    {
        if (controlPoints == null || controlPoints.Length < 2)
        {
            return 0f;
        }

        int samples = Mathf.Max(10, sampleCount);

        float closestT = 0f;
        float closestDistance = float.MaxValue;

        for (int i = 0; i <= samples; i++)
        {
            float t = i / (float)samples;
            float distance =
                Vector3.SqrMagnitude(GetPoint(t) - worldPosition);

            if (distance >= closestDistance)
            {
                continue;
            }

            closestDistance = distance;
            closestT = t;
        }

        return closestT;
    }

    public void RefreshLength()
    {
        cachedLength = ComputeLength();
    }

    private Vector3 EvaluateCatmullRom(float t)
    {
        int pointCount = controlPoints.Length;

        float scaledT = t * (pointCount - 1);
        int segmentIndex = Mathf.Clamp(
            Mathf.FloorToInt(scaledT),
            0,
            pointCount - 2);

        float segmentT = scaledT - segmentIndex;

        Vector3 point0 =
            controlPoints[Mathf.Max(segmentIndex - 1, 0)];

        Vector3 point1 =
            controlPoints[segmentIndex];

        Vector3 point2 =
            controlPoints[Mathf.Min(segmentIndex + 1, pointCount - 1)];

        Vector3 point3 =
            controlPoints[Mathf.Min(segmentIndex + 2, pointCount - 1)];

        return 0.5f *
        (
            2f * point1
            + (-point0 + point2) * segmentT
            + (2f * point0 - 5f * point1 + 4f * point2 - point3)
            * segmentT * segmentT
            + (-point0 + 3f * point1 - 3f * point2 + point3)
            * segmentT * segmentT * segmentT
        );
    }

    private float ComputeLength()
    {
        if (controlPoints == null || controlPoints.Length < 2)
        {
            return 0f;
        }

        int samples = Mathf.Max(10, sampleCount);

        float length = 0f;
        Vector3 previousPoint = GetPoint(0f);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 currentPoint = GetPoint(t);

            length += Vector3.Distance(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        return length;
    }

    private void OnValidate()
    {
        sampleCount = Mathf.Max(10, sampleCount);
        RefreshLength();
    }

    private void OnDrawGizmosSelected()
    {
        if (controlPoints == null || controlPoints.Length < 2)
        {
            return;
        }

        int samples = Mathf.Max(10, sampleCount);

        Vector3 previousPoint = GetPoint(0f);

        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            Vector3 currentPoint = GetPoint(t);

            Gizmos.DrawLine(previousPoint, currentPoint);
            previousPoint = currentPoint;
        }

        foreach (Vector3 point in controlPoints)
        {
            Gizmos.DrawSphere(point, 0.1f);
        }
    }
}