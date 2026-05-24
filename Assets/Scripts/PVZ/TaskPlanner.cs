using System.Collections;
using UnityEngine;

public enum PvzTaskType
{
    None,
    StoreParcel,
    RetrieveParcel,
    Error
}

public class TaskPlanner : MonoBehaviour
{
    [Header("Ссылки на системы")]
    [SerializeField] private ParcelDatabase parcelDatabase;
    [SerializeField] private ShelfManager shelfManager;
    [SerializeField] private RobotController robotController;
    [SerializeField] private PVZTrainingManager trainingManager;

    [Header("Основные точки маршрута")]
    [SerializeField] private Transform wpRobotIdle;
    [SerializeField] private Transform wpReceiving;
    [SerializeField] private Transform wpSklad;
    [SerializeField] private Transform wpShelfA;
    [SerializeField] private Transform wpShelfB;
    [SerializeField] private Transform wpClientCounter;

    [Header("Точки выдачи")]
    [SerializeField] private Transform parcelDeliveryPoint;

    [Header("Ограничения робота")]
    [SerializeField] private float maxRobotCarryWeight = 2.0f;

    [Header("Режим демонстрации")]
    [SerializeField] private bool useVisualRobot = true;
    [SerializeField] private bool enablePhysicsAfterPlacement = true;

    [Header("Текущее состояние")]
    [SerializeField] private PvzTaskType currentTask = PvzTaskType.None;
    [SerializeField] private string currentStatusMessage;
    [SerializeField] private string activeOrderId;
    [SerializeField] private string activeParcelId;
    [SerializeField] private string activeCellId;

    private bool isBusy;

    public PvzTaskType CurrentTask => currentTask;
    public string CurrentStatusMessage => currentStatusMessage;
    public string ActiveOrderId => activeOrderId;
    public string ActiveParcelId => activeParcelId;
    public string ActiveCellId => activeCellId;
    public bool IsBusy => isBusy;

    private void Awake()
    {
        if (parcelDatabase == null)
        {
            parcelDatabase = FindFirstObjectByType<ParcelDatabase>();
        }

        if (shelfManager == null)
        {
            shelfManager = FindFirstObjectByType<ShelfManager>();
        }

        if (robotController == null)
        {
            robotController = FindFirstObjectByType<RobotController>();
        }

        if (trainingManager == null)
        {
            trainingManager = FindFirstObjectByType<PVZTrainingManager>();
        }
    }

    public void StartStoreParcel(ParcelData parcelData)
    {
        if (IsTrainingModeActive())
        {
            Debug.LogWarning("TaskPlanner: request ignored because Training Mode is active.");
            return;
        }

        if (isBusy)
        {
            Debug.LogWarning("TaskPlanner: система занята другой задачей.");
            return;
        }

        StartCoroutine(StoreParcelRoutine(parcelData));
    }

    public void StartRetrieveParcel(ParcelRecord record)
    {
        if (IsTrainingModeActive())
        {
            Debug.LogWarning("TaskPlanner: request ignored because Training Mode is active.");
            return;
        }

        if (isBusy)
        {
            Debug.LogWarning("TaskPlanner: система занята другой задачей.");
            return;
        }

        StartCoroutine(RetrieveParcelRoutine(record));
    }

    private IEnumerator StoreParcelRoutine(ParcelData parcelData)
    {
        isBusy = true;
        currentTask = PvzTaskType.StoreParcel;

        if (parcelData == null)
        {
            SetError("TaskPlanner: нельзя разместить пустую посылку.");
            isBusy = false;
            yield break;
        }

        activeOrderId = parcelData.orderId;
        activeParcelId = parcelData.parcelId;

        Debug.Log($"TaskPlanner: запуск приема посылки {parcelData.parcelId}, заказ {parcelData.orderId}");

        if (!CheckRequiredSystems())
        {
            isBusy = false;
            yield break;
        }

        if (parcelData.weight > maxRobotCarryWeight)
        {
            SetError($"TaskPlanner: посылка {parcelData.parcelId} слишком тяжелая. Вес: {parcelData.weight}, максимум: {maxRobotCarryWeight}");
            isBusy = false;
            yield break;
        }

        if (parcelData.status == ParcelStatus.Stored && !string.IsNullOrEmpty(parcelData.cellId))
        {
            SetError($"TaskPlanner: посылка {parcelData.parcelId} уже хранится в ячейке {parcelData.cellId}.");
            isBusy = false;
            yield break;
        }

        parcelDatabase.RegisterOrUpdateParcel(parcelData);

        StorageCell freeCell = shelfManager.FindFreeCellForParcel(parcelData);

        if (freeCell == null)
        {
            SetError($"TaskPlanner: нет свободной ячейки для посылки {parcelData.parcelId}.");
            isBusy = false;
            yield break;
        }

        activeCellId = freeCell.cellId;

        bool reserved = shelfManager.ReserveCell(freeCell.cellId);

        if (!reserved)
        {
            SetError($"TaskPlanner: не удалось зарезервировать ячейку {freeCell.cellId}.");
            isBusy = false;
            yield break;
        }

        if (freeCell.placePoint == null)
        {
            SetError($"TaskPlanner: у ячейки {freeCell.cellId} не указан PlacePoint.");
            isBusy = false;
            yield break;
        }

        Transform shelfWaypoint = GetShelfWaypoint(freeCell);

        if (shelfWaypoint == null)
        {
            SetError($"TaskPlanner: не найден waypoint для стеллажа ячейки {freeCell.cellId}.");
            isBusy = false;
            yield break;
        }

        if (useVisualRobot && robotController != null)
        {
            currentStatusMessage = $"Робот идет к зоне приема за посылкой {parcelData.parcelId}.";
            yield return robotController.MoveAlongRoute(wpRobotIdle, wpReceiving);

            currentStatusMessage = $"Робот взял посылку {parcelData.parcelId}.";
            robotController.AttachParcel(parcelData.gameObject);

            currentStatusMessage = $"Робот несет посылку {parcelData.parcelId} к стеллажу {freeCell.shelfId}.";
            yield return robotController.MoveAlongRoute(wpSklad, shelfWaypoint);

            currentStatusMessage = $"Робот размещает посылку {parcelData.parcelId} в ячейке {freeCell.cellId}.";
            robotController.ReleaseParcelToPoint(freeCell.placePoint, enablePhysicsAfterPlacement);

            currentStatusMessage = "Робот возвращается в зону ожидания.";
            yield return robotController.MoveAlongRoute(wpSklad, wpRobotIdle);
        }
        else
        {
            MoveParcelToPoint(parcelData.gameObject, freeCell.placePoint, enablePhysicsAfterPlacement);
        }

        parcelDatabase.SetParcelStored(parcelData.parcelId, freeCell.cellId);
        shelfManager.MarkCellOccupied(freeCell.cellId);

        SetStatus($"Посылка {parcelData.parcelId} размещена в ячейке {freeCell.cellId}.");

        isBusy = false;
    }

    private IEnumerator RetrieveParcelRoutine(ParcelRecord record)
    {
        isBusy = true;
        currentTask = PvzTaskType.RetrieveParcel;

        if (record == null)
        {
            SetError("TaskPlanner: нельзя выдать пустой заказ.");
            isBusy = false;
            yield break;
        }

        activeOrderId = record.orderId;
        activeParcelId = record.parcelId;
        activeCellId = record.cellId;

        Debug.Log($"TaskPlanner: запуск выдачи заказа {record.orderId}, посылка {record.parcelId}");

        if (!CheckRequiredSystems())
        {
            isBusy = false;
            yield break;
        }

        if (record.status == ParcelStatus.Delivered)
        {
            SetError($"TaskPlanner: заказ {record.orderId} уже выдан.");
            isBusy = false;
            yield break;
        }

        if (string.IsNullOrEmpty(record.cellId))
        {
            SetError($"TaskPlanner: у заказа {record.orderId} не указана ячейка хранения.");
            isBusy = false;
            yield break;
        }

        if (record.weight > maxRobotCarryWeight)
        {
            SetError($"TaskPlanner: посылка {record.parcelId} слишком тяжелая. Вес: {record.weight}, максимум: {maxRobotCarryWeight}");
            isBusy = false;
            yield break;
        }

        StorageCell storageCell = shelfManager.FindCellById(record.cellId);

        if (storageCell == null)
        {
            SetError($"TaskPlanner: ячейка {record.cellId} не найдена в ShelfManager.");
            isBusy = false;
            yield break;
        }

        Transform shelfWaypoint = GetShelfWaypoint(storageCell);

        if (shelfWaypoint == null)
        {
            SetError($"TaskPlanner: не найден waypoint для стеллажа ячейки {storageCell.cellId}.");
            isBusy = false;
            yield break;
        }

        ParcelData sceneParcel = parcelDatabase.FindSceneParcelById(record.parcelId);

        if (sceneParcel == null)
        {
            SetError($"TaskPlanner: объект посылки {record.parcelId} не найден в сцене.");
            isBusy = false;
            yield break;
        }

        if (parcelDeliveryPoint == null)
        {
            SetError("TaskPlanner: не назначен ParcelDeliveryPoint.");
            isBusy = false;
            yield break;
        }

        parcelDatabase.SetParcelPicked(record.parcelId);

        if (useVisualRobot && robotController != null)
        {
            currentStatusMessage = $"Робот идет к стеллажу за посылкой {record.parcelId}.";
            yield return robotController.MoveAlongRoute(wpRobotIdle, wpSklad, shelfWaypoint);

            currentStatusMessage = $"Робот взял посылку {record.parcelId}.";
            robotController.AttachParcel(sceneParcel.gameObject);

            currentStatusMessage = $"Робот несет посылку {record.parcelId} к зоне выдачи.";
            yield return robotController.MoveAlongRoute(wpSklad, wpClientCounter);

            currentStatusMessage = $"Робот выдает посылку {record.parcelId}.";
            robotController.ReleaseParcelToPoint(parcelDeliveryPoint, enablePhysicsAfterPlacement);

            // Исправлено:
            // раньше робот шел WP_ClientCounter -> WP_Sklad -> WP_RobotIdle,
            // теперь после выдачи он сразу идет в зону ожидания.
            currentStatusMessage = "Робот возвращается в зону ожидания.";
            yield return robotController.MoveAlongRoute(wpRobotIdle);
        }
        else
        {
            MoveParcelToPoint(sceneParcel.gameObject, parcelDeliveryPoint, enablePhysicsAfterPlacement);
        }

        parcelDatabase.SetParcelDelivered(record.parcelId);
        shelfManager.MarkCellFree(record.cellId);

        SetStatus($"Заказ {record.orderId} выдан. Посылка {record.parcelId} перемещена на стойку.");

        isBusy = false;
    }

    private Transform GetShelfWaypoint(StorageCell cell)
    {
        if (cell == null)
        {
            return null;
        }

        string shelfId = cell.shelfId;
        string cellId = cell.cellId;

        if (!string.IsNullOrEmpty(shelfId))
        {
            if (shelfId.Contains("A"))
            {
                return wpShelfA;
            }

            if (shelfId.Contains("B"))
            {
                return wpShelfB;
            }
        }

        if (!string.IsNullOrEmpty(cellId))
        {
            if (cellId.StartsWith("A"))
            {
                return wpShelfA;
            }

            if (cellId.StartsWith("B"))
            {
                return wpShelfB;
            }
        }

        return null;
    }

    private bool CheckRequiredSystems()
    {
        if (parcelDatabase == null)
        {
            SetError("TaskPlanner: ParcelDatabase не найдена.");
            return false;
        }

        if (shelfManager == null)
        {
            SetError("TaskPlanner: ShelfManager не найден.");
            return false;
        }

        if (useVisualRobot && robotController == null)
        {
            SetError("TaskPlanner: RobotController не найден.");
            return false;
        }

        return true;
    }

    public void CancelActiveTaskForTraining()
    {
        StopAllCoroutines();

        isBusy = false;
        currentTask = PvzTaskType.None;
        currentStatusMessage = "Training Mode active. TaskPlanner is paused.";
        activeOrderId = string.Empty;
        activeParcelId = string.Empty;
        activeCellId = string.Empty;
    }

    private bool IsTrainingModeActive()
    {
        if (trainingManager == null)
        {
            trainingManager = FindFirstObjectByType<PVZTrainingManager>();
        }

        return trainingManager != null && trainingManager.IsTrainingMode;
    }

    private void MoveParcelToPoint(GameObject parcelObject, Transform targetPoint, bool enablePhysicsAfterMove)
    {
        if (parcelObject == null || targetPoint == null)
        {
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

        parcelObject.transform.SetPositionAndRotation(targetPoint.position, targetPoint.rotation);

        if (rb != null)
        {
            rb.isKinematic = !enablePhysicsAfterMove;
            rb.useGravity = enablePhysicsAfterMove;

            if (enablePhysicsAfterMove)
            {
                rb.WakeUp();
            }
        }

        Debug.Log($"TaskPlanner: объект {parcelObject.name} перемещен в точку {targetPoint.name}");
    }

    private void SetStatus(string message)
    {
        currentTask = PvzTaskType.None;
        currentStatusMessage = message;
        Debug.Log($"TaskPlanner: {message}");
    }

    private void SetError(string message)
    {
        currentTask = PvzTaskType.Error;
        currentStatusMessage = message;
        Debug.LogWarning(message);
    }

    [ContextMenu("Test Store First Received Parcel")]
    private void TestStoreFirstReceivedParcel()
    {
        if (isBusy)
        {
            Debug.LogWarning("TaskPlanner Test: система занята.");
            return;
        }

        ParcelData[] sceneParcels = FindObjectsByType<ParcelData>(FindObjectsSortMode.None);

        foreach (ParcelData parcel in sceneParcels)
        {
            if (parcel.status == ParcelStatus.Received)
            {
                StartStoreParcel(parcel);
                return;
            }
        }

        Debug.LogWarning("TaskPlanner Test: в сцене нет посылки со статусом Received.");
    }

    [ContextMenu("Test Retrieve Order OZ-100002")]
    private void TestRetrieveOrderOZ100002()
    {
        if (isBusy)
        {
            Debug.LogWarning("TaskPlanner Test: система занята.");
            return;
        }

        if (parcelDatabase == null)
        {
            parcelDatabase = FindFirstObjectByType<ParcelDatabase>();
        }

        if (parcelDatabase == null)
        {
            Debug.LogWarning("TaskPlanner Test: ParcelDatabase не найдена.");
            return;
        }

        ParcelRecord record = parcelDatabase.FindByOrderId("OZ-100002");

        if (record == null)
        {
            Debug.LogWarning("TaskPlanner Test: заказ OZ-100002 не найден.");
            return;
        }

        StartRetrieveParcel(record);
    }
}
