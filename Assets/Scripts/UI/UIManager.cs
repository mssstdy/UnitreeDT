using System.Text;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [Header("Ссылки на системы")]
    [SerializeField] private TaskPlanner taskPlanner;
    [SerializeField] private ParcelDatabase parcelDatabase;
    [SerializeField] private ShelfManager shelfManager;

    [Header("UI элементы")]
    [SerializeField] private TMP_Text statusText;

    [Header("Настройки")]
    [SerializeField] private float updateInterval = 0.25f;

    private float timer;

    private void Awake()
    {
        if (taskPlanner == null)
        {
            taskPlanner = FindFirstObjectByType<TaskPlanner>();
        }

        if (parcelDatabase == null)
        {
            parcelDatabase = FindFirstObjectByType<ParcelDatabase>();
        }

        if (shelfManager == null)
        {
            shelfManager = FindFirstObjectByType<ShelfManager>();
        }
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= updateInterval)
        {
            timer = 0f;
            UpdateStatusPanel();
        }
    }

    private void UpdateStatusPanel()
    {
        if (statusText == null)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();

        builder.AppendLine("Статус цифрового двойника");
        builder.AppendLine();

        AppendTaskPlannerInfo(builder);
        AppendParcelDatabaseInfo(builder);
        AppendShelfManagerInfo(builder);

        statusText.text = builder.ToString();
    }

    private void AppendTaskPlannerInfo(StringBuilder builder)
    {
        builder.AppendLine("=== Робот / TaskPlanner ===");

        if (taskPlanner == null)
        {
            builder.AppendLine("TaskPlanner: не найден");
            builder.AppendLine();
            return;
        }

        builder.AppendLine($"Текущая задача: {taskPlanner.CurrentTask}");
        builder.AppendLine($"Сообщение: {SafeText(taskPlanner.CurrentStatusMessage)}");
        builder.AppendLine($"Активный заказ: {SafeText(taskPlanner.ActiveOrderId)}");
        builder.AppendLine($"Активная посылка: {SafeText(taskPlanner.ActiveParcelId)}");
        builder.AppendLine($"Активная ячейка: {SafeText(taskPlanner.ActiveCellId)}");
        builder.AppendLine();
    }

    private void AppendParcelDatabaseInfo(StringBuilder builder)
    {
        builder.AppendLine("=== База посылок ===");

        if (parcelDatabase == null)
        {
            builder.AppendLine("ParcelDatabase: не найдена");
            builder.AppendLine();
            return;
        }

        int totalParcels = parcelDatabase.Parcels.Count;
        int received = 0;
        int stored = 0;
        int picked = 0;
        int delivered = 0;
        int error = 0;

        foreach (ParcelRecord record in parcelDatabase.Parcels)
        {
            switch (record.status)
            {
                case ParcelStatus.Received:
                    received++;
                    break;

                case ParcelStatus.Stored:
                    stored++;
                    break;

                case ParcelStatus.Picked:
                    picked++;
                    break;

                case ParcelStatus.Delivered:
                    delivered++;
                    break;

                case ParcelStatus.Error:
                case ParcelStatus.Missing:
                    error++;
                    break;
            }
        }

        builder.AppendLine($"Всего посылок: {totalParcels}");
        builder.AppendLine($"Принятые: {received}");
        builder.AppendLine($"На хранении: {stored}");
        builder.AppendLine($"Взяты роботом: {picked}");
        builder.AppendLine($"Выданы: {delivered}");
        builder.AppendLine($"Ошибки: {error}");
        builder.AppendLine();
    }

    private void AppendShelfManagerInfo(StringBuilder builder)
    {
        builder.AppendLine("=== Ячейки хранения ===");

        if (shelfManager == null)
        {
            builder.AppendLine("ShelfManager: не найден");
            builder.AppendLine();
            return;
        }

        int totalCells = shelfManager.Cells.Count;
        int free = 0;
        int reserved = 0;
        int occupied = 0;
        int blocked = 0;

        foreach (StorageCell cell in shelfManager.Cells)
        {
            switch (cell.status)
            {
                case StorageCellStatus.Free:
                    free++;
                    break;

                case StorageCellStatus.Reserved:
                    reserved++;
                    break;

                case StorageCellStatus.Occupied:
                    occupied++;
                    break;

                case StorageCellStatus.Blocked:
                    blocked++;
                    break;
            }
        }

        builder.AppendLine($"Всего ячеек: {totalCells}");
        builder.AppendLine($"Свободно: {free}");
        builder.AppendLine($"Зарезервировано: {reserved}");
        builder.AppendLine($"Занято: {occupied}");
        builder.AppendLine($"Заблокировано: {blocked}");
        builder.AppendLine();
    }

    private string SafeText(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "—";
        }

        return value;
    }
}