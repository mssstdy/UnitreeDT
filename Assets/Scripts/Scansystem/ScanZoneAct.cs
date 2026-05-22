using UnityEngine;

public class ScanZone : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private ParcelDatabase parcelDatabase;

    private void Awake()
    {
        if (parcelDatabase == null)
        {
            parcelDatabase = FindFirstObjectByType<ParcelDatabase>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        ScannableCode code = other.GetComponent<ScannableCode>();

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

        if (code.codeType == CodeType.OrderCode)
        {
            HandleOrderCode(code);
        }
        else if (code.codeType == CodeType.ParcelCode)
        {
            HandleParcelCode(code, other.gameObject);
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

        if (record.status == ParcelStatus.Delivered)
        {
            Debug.LogWarning($"ScanZone: заказ {record.orderId} уже был выдан.");
            return;
        }

        if (string.IsNullOrEmpty(record.cellId))
        {
            Debug.LogWarning($"ScanZone: заказ {record.orderId} найден, но ячейка хранения не указана.");
            return;
        }

        Debug.Log(
            $"ScanZone: заказ найден. " +
            $"Order: {record.orderId}, Parcel: {record.parcelId}, Cell: {record.cellId}, Status: {record.status}"
        );

        // Позже здесь будет вызов TaskPlanner:
        // taskPlanner.StartRetrieveParcel(record);
    }

    private void HandleParcelCode(ScannableCode code, GameObject scannedObject)
    {
        ParcelData parcelData = scannedObject.GetComponent<ParcelData>();

        if (parcelData == null)
        {
            Debug.LogWarning("ScanZone: это ParcelCode, но на объекте нет ParcelData.");
            return;
        }

        parcelDatabase.RegisterOrUpdateParcel(parcelData);

        Debug.Log(
            $"ScanZone: новая/обновленная посылка зарегистрирована. " +
            $"Order: {parcelData.orderId}, Parcel: {parcelData.parcelId}, Status: {parcelData.status}"
        );

        // Позже здесь будет вызов TaskPlanner:
        // taskPlanner.StartStoreParcel(parcelData);
    }
}