using System.Collections.Generic;
using UnityEngine;

public class ShelfManager : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private bool loadCellsFromSceneOnStart = true;
    [SerializeField] private bool syncWithParcelDatabaseOnStart = true;

    [Header("Ссылки")]
    [SerializeField] private ParcelDatabase parcelDatabase;

    [Header("Все ячейки хранения")]
    [SerializeField] private List<StorageCell> cells = new List<StorageCell>();

    public IReadOnlyList<StorageCell> Cells => cells;

    private void Awake()
    {
        if (parcelDatabase == null)
        {
            parcelDatabase = FindFirstObjectByType<ParcelDatabase>();
        }
    }

    private void Start()
    {
        if (parcelDatabase == null)
        {
            parcelDatabase = FindFirstObjectByType<ParcelDatabase>();
        }

        // Важно: сначала принудительно загружаем посылки из сцены,
        // чтобы ShelfManager синхронизировался уже с заполненной ParcelDatabase.
        if (parcelDatabase != null)
        {
            parcelDatabase.LoadParcelsFromScene();
        }
        else
        {
            Debug.LogWarning("ShelfManager: ParcelDatabase не найдена перед синхронизацией.");
        }

        if (loadCellsFromSceneOnStart)
        {
            LoadCellsFromScene();
        }

        if (syncWithParcelDatabaseOnStart)
        {
            SyncCellsWithParcelDatabase();
        }

        PrintAllCells();
    }

    [ContextMenu("Load Cells From Scene")]
    public void LoadCellsFromScene()
    {
        cells.Clear();

        StorageCell[] sceneCells = FindObjectsByType<StorageCell>(FindObjectsSortMode.None);

        foreach (StorageCell cell in sceneCells)
        {
            if (cell == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(cell.cellId))
            {
                cell.cellId = cell.gameObject.name;
            }

            if (!cells.Contains(cell))
            {
                cells.Add(cell);
            }
        }

        cells.Sort((a, b) => string.Compare(a.cellId, b.cellId, System.StringComparison.Ordinal));

        Debug.Log($"ShelfManager: загружено ячеек из сцены: {cells.Count}");
    }

    public StorageCell FindCellById(string cellId)
    {
        foreach (StorageCell cell in cells)
        {
            if (cell.cellId == cellId)
            {
                return cell;
            }
        }

        return null;
    }

    public StorageCell FindFreeCellForWeight(float parcelWeight)
    {
        foreach (StorageCell cell in cells)
        {
            if (cell.IsAvailableForWeight(parcelWeight))
            {
                return cell;
            }
        }

        return null;
    }

    public StorageCell FindFreeCellForParcel(ParcelData parcelData)
    {
        if (parcelData == null)
        {
            Debug.LogWarning("ShelfManager: нельзя найти ячейку для пустой посылки.");
            return null;
        }

        return FindFreeCellForWeight(parcelData.weight);
    }

    public bool ReserveCell(string cellId)
    {
        StorageCell cell = FindCellById(cellId);

        if (cell == null)
        {
            Debug.LogWarning($"ShelfManager: ячейка {cellId} не найдена.");
            return false;
        }

        if (cell.status != StorageCellStatus.Free)
        {
            Debug.LogWarning($"ShelfManager: ячейка {cellId} не свободна. Текущий статус: {cell.status}");
            return false;
        }

        cell.SetStatus(StorageCellStatus.Reserved);

        Debug.Log($"ShelfManager: ячейка {cellId} зарезервирована.");
        return true;
    }

    public bool MarkCellOccupied(string cellId)
    {
        StorageCell cell = FindCellById(cellId);

        if (cell == null)
        {
            Debug.LogWarning($"ShelfManager: ячейка {cellId} не найдена.");
            return false;
        }

        cell.SetStatus(StorageCellStatus.Occupied);

        Debug.Log($"ShelfManager: ячейка {cellId} отмечена как занятая.");
        return true;
    }

    public bool MarkCellFree(string cellId)
    {
        StorageCell cell = FindCellById(cellId);

        if (cell == null)
        {
            Debug.LogWarning($"ShelfManager: ячейка {cellId} не найдена.");
            return false;
        }

        cell.SetStatus(StorageCellStatus.Free);

        Debug.Log($"ShelfManager: ячейка {cellId} освобождена.");
        return true;
    }

    [ContextMenu("Sync Cells With ParcelDatabase")]
    public void SyncCellsWithParcelDatabase()
    {
        if (parcelDatabase == null)
        {
            Debug.LogWarning("ShelfManager: ParcelDatabase не назначена, синхронизация невозможна.");
            return;
        }

        // Сначала считаем все незаблокированные ячейки свободными.
        foreach (StorageCell cell in cells)
        {
            if (cell.status != StorageCellStatus.Blocked)
            {
                cell.SetStatus(StorageCellStatus.Free);
            }
        }

        // Потом смотрим, какие посылки уже лежат на хранении,
        // и помечаем соответствующие ячейки как занятые.
        foreach (ParcelRecord record in parcelDatabase.Parcels)
        {
            if (record.status == ParcelStatus.Stored && !string.IsNullOrEmpty(record.cellId))
            {
                StorageCell cell = FindCellById(record.cellId);

                if (cell == null)
                {
                    Debug.LogWarning($"ShelfManager: в базе указана ячейка {record.cellId}, но в сцене она не найдена.");
                    continue;
                }

                cell.SetStatus(StorageCellStatus.Occupied);

                Debug.Log($"ShelfManager: ячейка {record.cellId} занята посылкой {record.parcelId}");
            }
        }
    }

    [ContextMenu("Print All Cells")]
    public void PrintAllCells()
    {
        Debug.Log("===== SHELF MANAGER: CELLS =====");

        if (cells.Count == 0)
        {
            Debug.Log("ShelfManager: список ячеек пуст.");
            return;
        }

        foreach (StorageCell cell in cells)
        {
            Debug.Log(
                $"Cell: {cell.cellId} | " +
                $"Shelf: {cell.shelfId} | " +
                $"Level: {cell.level} | " +
                $"Number: {cell.cellNumber} | " +
                $"Status: {cell.status} | " +
                $"MaxWeight: {cell.maxWeight}"
            );
        }
    }

    [ContextMenu("Test Find Free Cell For 1kg")]
    private void TestFindFreeCellFor1Kg()
    {
        StorageCell cell = FindFreeCellForWeight(1.0f);

        if (cell == null)
        {
            Debug.Log("ShelfManager Test: свободная ячейка для 1 кг не найдена.");
            return;
        }

        Debug.Log($"ShelfManager Test: свободная ячейка для 1 кг найдена: {cell.cellId}");
    }

    [ContextMenu("Test Reserve Free Cell For 1kg")]
    private void TestReserveFreeCellFor1Kg()
    {
        StorageCell cell = FindFreeCellForWeight(1.0f);

        if (cell == null)
        {
            Debug.Log("ShelfManager Test: свободная ячейка для 1 кг не найдена.");
            return;
        }

        ReserveCell(cell.cellId);
    }
}