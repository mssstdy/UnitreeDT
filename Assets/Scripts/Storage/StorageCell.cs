using UnityEngine;

public enum StorageCellStatus
{
    Free,
    Reserved,
    Occupied,
    Blocked
}

public class StorageCell : MonoBehaviour
{
    [Header("ID ячейки")]
    public string cellId;      // Например: B-2-2
    public string shelfId;     // Например: Shelf_B
    public int level;          // Номер полки
    public int cellNumber;     // Номер ячейки на полке

    [Header("Состояние")]
    public StorageCellStatus status = StorageCellStatus.Free;

    [Header("Ограничения")]
    public float maxWeight = 2.0f;

    [Header("Точки для робота")]
    public Transform placePoint;
    public Transform graspPoint;
    public Transform approachPoint;

    public bool IsFree()
    {
        return status == StorageCellStatus.Free;
    }

    public bool IsAvailableForWeight(float parcelWeight)
    {
        return status == StorageCellStatus.Free && parcelWeight <= maxWeight;
    }

    public void SetStatus(StorageCellStatus newStatus)
    {
        status = newStatus;
    }

    [ContextMenu("Auto Fill From Object Name")]
    public void AutoFillFromObjectName()
    {
        if (string.IsNullOrEmpty(cellId))
        {
            cellId = gameObject.name;
        }

        string[] parts = cellId.Split('-');

        if (parts.Length == 3)
        {
            shelfId = "Shelf_" + parts[0];

            int.TryParse(parts[1], out level);
            int.TryParse(parts[2], out cellNumber);
        }

        Debug.Log($"StorageCell: заполнено {cellId}, {shelfId}, level {level}, cell {cellNumber}");
    }

    [ContextMenu("Auto Assign Child Points")]
    public void AutoAssignChildPoints()
    {
        Transform place = transform.Find("PlacePoint");
        Transform grasp = transform.Find("GraspPoint");
        Transform approach = transform.Find("ApproachPoint");

        if (place != null)
        {
            placePoint = place;
        }

        if (grasp != null)
        {
            graspPoint = grasp;
        }

        if (approach != null)
        {
            approachPoint = approach;
        }

        Debug.Log($"StorageCell: точки назначены для {gameObject.name}");
    }

    private void Reset()
    {
        cellId = gameObject.name;
        AutoFillFromObjectName();
        AutoAssignChildPoints();
    }
}