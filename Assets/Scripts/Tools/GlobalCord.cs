using UnityEngine;

public class WorldTransformCoordinates : MonoBehaviour
{
    [Header("Только для удобства отображения")]
    [SerializeField] private Vector3 worldPosition;
    [SerializeField] private Vector3 localPosition;

    private void OnValidate()
    {
        UpdateCoordinatesInfo();
    }

    private void Update()
    {
        UpdateCoordinatesInfo();
    }

    private void UpdateCoordinatesInfo()
    {
        worldPosition = transform.position;
        localPosition = transform.localPosition;
    }
}