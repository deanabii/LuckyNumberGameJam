using UnityEngine;

namespace LuckyNumber.Core
{
    [RequireComponent(typeof(Camera))]
    public class SmoothCameraFollow : MonoBehaviour
    {
        public static SmoothCameraFollow Instance { get; private set; }

        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.25f, -10f);

        [Header("Follow Settings")]
        [SerializeField] private float smoothTime = 0.14f;
        [SerializeField] private float targetOrthoSize = 6.2f;

        [Header("Zone Bounds Clamping")]
        [SerializeField] private bool useZoneBounds = false;
        [SerializeField] private Rect currentZoneBounds = new Rect(-12f, -10f, 24f, 20f);

        private Camera _cam;
        private Vector3 _velocity;

        private void Awake()
        {
            Instance = this;
            _cam = GetComponent<Camera>();
            if (_cam != null && _cam.orthographic)
            {
                _cam.orthographicSize = targetOrthoSize;
                _cam.backgroundColor = new Color(0.08f, 0.06f, 0.12f, 1f);
            }
        }

        public void SetTarget(Transform newTarget, bool snapImmediately = true)
        {
            target = newTarget;
            if (snapImmediately) SnapToTarget();
        }

        public void SetZoneBounds(Rect bounds, bool enableClamping)
        {
            currentZoneBounds = bounds;
            useZoneBounds = enableClamping;
        }

        public void SnapToTarget()
        {
            if (target == null) return;
            _velocity = Vector3.zero;
            transform.position = CalculateDesiredPosition();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 desired = CalculateDesiredPosition();
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _velocity, smoothTime);
        }

        private Vector3 CalculateDesiredPosition()
        {
            Vector3 desired = target.position + offset;
            desired.z = offset.z;

            if (useZoneBounds && _cam != null && _cam.orthographic)
            {
                float halfH = _cam.orthographicSize;
                float halfW = halfH * _cam.aspect;

                float minX = currentZoneBounds.xMin + halfW;
                float maxX = currentZoneBounds.xMax - halfW;
                float minY = currentZoneBounds.yMin + halfH;
                float maxY = currentZoneBounds.yMax - halfH;

                desired.x = (minX < maxX) ? Mathf.Clamp(desired.x, minX, maxX) : currentZoneBounds.center.x;
                desired.y = (minY < maxY) ? Mathf.Clamp(desired.y, minY, maxY) : currentZoneBounds.center.y;
            }

            return desired;
        }
    }
}
