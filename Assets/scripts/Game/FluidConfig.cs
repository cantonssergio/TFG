using UnityEngine;

[CreateAssetMenu(fileName = "FluidConfig", menuName = "Scriptable Objects/FluidConfig")]
public class FluidConfig : ScriptableObject
{
    public float gravity;
    public float density;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityMultiplier;
    public float maxSpeed;
    public GameObject dropletPrefab;

}
