using UnityEngine;

public enum CodeType
{
    OrderCode,
    ParcelCode
}

public class ScannableCode : MonoBehaviour
{
    public string codeId;
    public CodeType codeType;

    public string linkedOrderId;
    public string linkedParcelId;
}