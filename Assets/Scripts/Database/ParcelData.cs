using UnityEngine;

public enum ParcelStatus
{
    Received,
    Stored,
    Picked,
    Delivered,
    Missing,
    Error
}

public class ParcelData : MonoBehaviour
{
    public string orderId;
    public string parcelId;
    public string cellId;

    public float weight = 1.0f;

    public ParcelStatus status = ParcelStatus.Received;
}