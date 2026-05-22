using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ParcelRecord
{
    public string orderId;
    public string parcelId;
    public string cellId;
    public float weight;
    public ParcelStatus status;
}

public class ParcelDatabase : MonoBehaviour
{
    [Header("Настройки")]
    [SerializeField] private bool loadParcelsFromSceneOnStart = true;

    [Header("Список посылок в базе")]
    [SerializeField] private List<ParcelRecord> parcels = new List<ParcelRecord>();

    public IReadOnlyList<ParcelRecord> Parcels => parcels;

    private void Start()
    {
        if (loadParcelsFromSceneOnStart)
        {
            LoadParcelsFromScene();
        }

        PrintAllParcels();
    }

    public void LoadParcelsFromScene()
    {
        parcels.Clear();

        ParcelData[] sceneParcels = FindObjectsByType<ParcelData>(FindObjectsSortMode.None);

        foreach (ParcelData parcelData in sceneParcels)
        {
            RegisterOrUpdateParcel(parcelData);
        }

        Debug.Log($"ParcelDatabase: загружено посылок из сцены: {parcels.Count}");
    }

    public void RegisterOrUpdateParcel(ParcelData parcelData)
    {
        if (parcelData == null)
        {
            Debug.LogWarning("ParcelDatabase: попытка добавить пустую посылку.");
            return;
        }

        ParcelRecord existingRecord = FindByParcelId(parcelData.parcelId);

        if (existingRecord == null)
        {
            ParcelRecord newRecord = new ParcelRecord
            {
                orderId = parcelData.orderId,
                parcelId = parcelData.parcelId,
                cellId = parcelData.cellId,
                weight = parcelData.weight,
                status = parcelData.status
            };

            parcels.Add(newRecord);

            Debug.Log($"ParcelDatabase: добавлена посылка {newRecord.parcelId}, заказ {newRecord.orderId}");
        }
        else
        {
            existingRecord.orderId = parcelData.orderId;
            existingRecord.cellId = parcelData.cellId;
            existingRecord.weight = parcelData.weight;
            existingRecord.status = parcelData.status;

            Debug.Log($"ParcelDatabase: обновлена посылка {existingRecord.parcelId}");
        }
    }

    public ParcelRecord FindByOrderId(string orderId)
    {
        foreach (ParcelRecord record in parcels)
        {
            if (record.orderId == orderId)
            {
                return record;
            }
        }

        return null;
    }

    public ParcelRecord FindByParcelId(string parcelId)
    {
        foreach (ParcelRecord record in parcels)
        {
            if (record.parcelId == parcelId)
            {
                return record;
            }
        }

        return null;
    }

    public bool HasOrder(string orderId)
    {
        return FindByOrderId(orderId) != null;
    }

    public bool HasParcel(string parcelId)
    {
        return FindByParcelId(parcelId) != null;
    }

    public void SetParcelStored(string parcelId, string cellId)
    {
        ParcelRecord record = FindByParcelId(parcelId);

        if (record == null)
        {
            Debug.LogWarning($"ParcelDatabase: посылка {parcelId} не найдена.");
            return;
        }

        record.cellId = cellId;
        record.status = ParcelStatus.Stored;

        ParcelData sceneParcel = FindSceneParcelById(parcelId);

        if (sceneParcel != null)
        {
            sceneParcel.cellId = cellId;
            sceneParcel.status = ParcelStatus.Stored;
        }

        Debug.Log($"ParcelDatabase: посылка {parcelId} размещена в ячейке {cellId}");
    }

    public void SetParcelPicked(string parcelId)
    {
        ParcelRecord record = FindByParcelId(parcelId);

        if (record == null)
        {
            Debug.LogWarning($"ParcelDatabase: посылка {parcelId} не найдена.");
            return;
        }

        record.status = ParcelStatus.Picked;

        ParcelData sceneParcel = FindSceneParcelById(parcelId);

        if (sceneParcel != null)
        {
            sceneParcel.status = ParcelStatus.Picked;
        }

        Debug.Log($"ParcelDatabase: посылка {parcelId} взята роботом.");
    }

    public void SetParcelDelivered(string parcelId)
    {
        ParcelRecord record = FindByParcelId(parcelId);

        if (record == null)
        {
            Debug.LogWarning($"ParcelDatabase: посылка {parcelId} не найдена.");
            return;
        }

        record.status = ParcelStatus.Delivered;

        ParcelData sceneParcel = FindSceneParcelById(parcelId);

        if (sceneParcel != null)
        {
            sceneParcel.status = ParcelStatus.Delivered;
        }

        Debug.Log($"ParcelDatabase: посылка {parcelId} выдана клиенту.");
    }

    public ParcelData FindSceneParcelById(string parcelId)
    {
        ParcelData[] sceneParcels = FindObjectsByType<ParcelData>(FindObjectsSortMode.None);

        foreach (ParcelData parcelData in sceneParcels)
        {
            if (parcelData.parcelId == parcelId)
            {
                return parcelData;
            }
        }

        return null;
    }

    public bool CanRobotCarryParcel(string parcelId, float maxRobotCarryWeight)
    {
        ParcelRecord record = FindByParcelId(parcelId);

        if (record == null)
        {
            Debug.LogWarning($"ParcelDatabase: посылка {parcelId} не найдена.");
            return false;
        }

        return record.weight <= maxRobotCarryWeight;
    }

    [ContextMenu("Print All Parcels")]
    public void PrintAllParcels()
    {
        Debug.Log("===== PARCEL DATABASE =====");

        if (parcels.Count == 0)
        {
            Debug.Log("База посылок пуста.");
            return;
        }

        foreach (ParcelRecord record in parcels)
        {
            Debug.Log(
                $"Order: {record.orderId} | " +
                $"Parcel: {record.parcelId} | " +
                $"Cell: {record.cellId} | " +
                $"Weight: {record.weight} | " +
                $"Status: {record.status}"
            );
        }
    }

    [ContextMenu("Test Find Order OZ-100002")]
    private void TestFindOrder()
    {
        ParcelRecord record = FindByOrderId("OZ-100002");

        if (record == null)
        {
            Debug.Log("Тест: заказ OZ-100002 не найден.");
            return;
        }

        Debug.Log($"Тест: заказ OZ-100002 найден. Посылка {record.parcelId}, ячейка {record.cellId}");
    }
}