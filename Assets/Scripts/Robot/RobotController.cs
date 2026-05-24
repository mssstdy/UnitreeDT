using System.Collections;
using UnityEngine;

public class RobotController : MonoBehaviour
{
    [Header("Основные ссылки")]
    [SerializeField] private Transform robotRoot;
    [SerializeField] private Transform holdPoint;
    [SerializeField] private PVZTrainingManager trainingManager;

    [Header("Движение")]
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private float stoppingDistance = 0.05f;

    [Header("Состояние")]
    [SerializeField] private bool isMoving;
    [SerializeField] private GameObject heldParcel;

    public Transform HoldPoint => holdPoint;
    public bool IsMoving => isMoving;
    public GameObject HeldParcel => heldParcel;

    private void Awake()
    {
        if (robotRoot == null)
        {
            robotRoot = transform;
        }

        if (trainingManager == null)
        {
            trainingManager = FindFirstObjectByType<PVZTrainingManager>();
        }
    }

    public IEnumerator MoveAlongRoute(params Transform[] routePoints)
    {
        if (IsTrainingModeActive())
        {
            CancelMotionForTraining();
            yield break;
        }

        if (routePoints == null || routePoints.Length == 0)
        {
            Debug.LogWarning("RobotController: маршрут пуст.");
            yield break;
        }

        foreach (Transform point in routePoints)
        {
            if (IsTrainingModeActive())
            {
                CancelMotionForTraining();
                yield break;
            }

            if (point == null)
            {
                Debug.LogWarning("RobotController: в маршруте есть пустая точка.");
                continue;
            }

            yield return MoveTo(point);
        }
    }

    public IEnumerator MoveTo(Transform targetPoint)
    {
        if (IsTrainingModeActive())
        {
            CancelMotionForTraining();
            yield break;
        }

        if (targetPoint == null)
        {
            Debug.LogWarning("RobotController: целевая точка не назначена.");
            yield break;
        }

        isMoving = true;

        Debug.Log($"RobotController: движение к точке {targetPoint.name}");

        while (Vector3.Distance(robotRoot.position, GetFlatTargetPosition(targetPoint)) > stoppingDistance)
        {
            if (IsTrainingModeActive())
            {
                CancelMotionForTraining();
                yield break;
            }

            Vector3 targetPosition = GetFlatTargetPosition(targetPoint);
            Vector3 direction = targetPosition - robotRoot.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

                robotRoot.rotation = Quaternion.Slerp(
                    robotRoot.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }

            robotRoot.position = Vector3.MoveTowards(
                robotRoot.position,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            yield return null;
        }

        robotRoot.position = GetFlatTargetPosition(targetPoint);

        isMoving = false;

        Debug.Log($"RobotController: достигнута точка {targetPoint.name}");
    }

    private Vector3 GetFlatTargetPosition(Transform targetPoint)
    {
        Vector3 targetPosition = targetPoint.position;
        targetPosition.y = robotRoot.position.y;
        return targetPosition;
    }

    public void CancelMotionForTraining()
    {
        StopAllCoroutines();
        isMoving = false;
    }

    private bool IsTrainingModeActive()
    {
        if (trainingManager == null)
        {
            trainingManager = FindFirstObjectByType<PVZTrainingManager>();
        }

        return trainingManager != null && trainingManager.IsTrainingMode;
    }

    public void AttachParcel(GameObject parcelObject)
    {
        if (IsTrainingModeActive())
        {
            Debug.LogWarning("RobotController: parcel attach ignored because Training Mode is active.");
            return;
        }

        if (parcelObject == null)
        {
            Debug.LogWarning("RobotController: нельзя взять пустую посылку.");
            return;
        }

        if (holdPoint == null)
        {
            Debug.LogWarning("RobotController: HoldPoint не назначен.");
            return;
        }

        Rigidbody rb = parcelObject.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif

            rb.angularVelocity = Vector3.zero;
        }

        parcelObject.transform.SetParent(holdPoint);
        parcelObject.transform.localPosition = Vector3.zero;
        parcelObject.transform.localRotation = Quaternion.identity;

        heldParcel = parcelObject;

        Debug.Log($"RobotController: посылка {parcelObject.name} прикреплена к HoldPoint.");
    }

    public void ReleaseParcelToPoint(Transform targetPoint, bool enablePhysicsAfterRelease)
    {
        if (IsTrainingModeActive())
        {
            Debug.LogWarning("RobotController: parcel release ignored because Training Mode is active.");
            return;
        }

        if (heldParcel == null)
        {
            Debug.LogWarning("RobotController: нет посылки для отпускания.");
            return;
        }

        if (targetPoint == null)
        {
            Debug.LogWarning("RobotController: точка отпускания не назначена.");
            return;
        }

        GameObject parcelObject = heldParcel;

        parcelObject.transform.SetParent(null);
        parcelObject.transform.SetPositionAndRotation(targetPoint.position, targetPoint.rotation);

        Rigidbody rb = parcelObject.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = !enablePhysicsAfterRelease;
            rb.useGravity = enablePhysicsAfterRelease;

#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif

            rb.angularVelocity = Vector3.zero;

            if (enablePhysicsAfterRelease)
            {
                rb.WakeUp();
            }
        }

        Debug.Log($"RobotController: посылка {parcelObject.name} отпущена в точке {targetPoint.name}.");

        heldParcel = null;
    }
}
