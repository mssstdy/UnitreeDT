using UnityEngine;

public class ScanZone : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private ParcelDatabase parcelDatabase;
    [SerializeField] private TaskPlanner taskPlanner;

    private void Awake()
    {
        if (parcelDatabase == null)
        {
            parcelDatabase = FindFirstObjectByType<ParcelDatabase>();
        }

        if (taskPlanner == null)
        {
            taskPlanner = FindFirstObjectByType<TaskPlanner>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ScannableCode code = other.GetComponent<ScannableCode>();

        if (code == null)
        {
            code = other.GetComponentInParent<ScannableCode>();
        }

        if (code == null)
        {
            Debug.Log("ScanZone: объект не содержит сканируемого кода.");
            return;
        }

        Debug.Log($"ScanZone: отсканирован код {code.codeId}, тип {code.codeType}");

        if (parcelDatabase == null)
        {
            Debug.LogError("ScanZone: ParcelDatabase не найдена.");
            return;
        }

        if (taskPlanner == null)
        {
            Debug.LogError("ScanZone: TaskPlanner не найден.");
            return;
        }

        if (code.codeType == CodeType.OrderCode)
        {
            HandleOrderCode(code);
        }
        else if (code.codeType == CodeType.ParcelCode)
        {
            HandleParcelCode(code);
        }
    }

    private void HandleOrderCode(ScannableCode code)
    {
        ParcelRecord record = parcelDatabase.FindByOrderId(code.linkedOrderId);

        if (record == null)
        {
            Debug.LogWarning($"ScanZone: заказ {code.linkedOrderId} не найден в базе.");
            return;
        }

        taskPlanner.StartRetrieveParcel(record);
    }

    private void HandleParcelCode(ScannableCode code)
    {
        ParcelData parcelData = code.GetComponent<ParcelData>();

        if (parcelData == null)
        {
            parcelData = code.GetComponentInParent<ParcelData>();
        }

        if (parcelData == null)
        {
            Debug.LogWarning("ScanZone: это ParcelCode, но на объекте нет ParcelData.");
            return;
        }

        taskPlanner.StartStoreParcel(parcelData);
    }
}