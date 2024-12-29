using System.Collections.Generic;
using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Settings")]
    public int numDroplets;

    [Range(0.01f, 0.001f)] public float timeStep;
    public float gravity;
    public float density;
    [Range(0, 1)] public float collisionDampling;
    public float smoothingRadius;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityMultiplier;
    public float maxSpeed;
    public Vector3 spawnSize; // Tamaño del área de generación de gotas
    public Vector3 spawnCentre; // Centro del área de generación de gotas

    public Vector3 limitSize;

    public ComputeShader dropletComputeShader;
    public GameObject dropletPrefab; // Prefab para representar visualmente cada gota

    [Header("Computing")]
    private ComputeBuffer dropletPositionBuffer;
    private ComputeBuffer dropletVelocityBuffer;
    private ComputeBuffer dropletDensityBuffer;
    private ComputeBuffer dropletsNearDensity;
    private Vector3[] dropletsPosition;
    private List<GameObject> dropletInstances;


    void simulate()
    {
        UpdateSettings();
        CalcDensity();
        CalcViscosity();
        CalcPressure();
        UpdateDropletPositions();
    }

    void UpdateSettings()
    {
        dropletComputeShader.SetFloat("gravity", gravity);
        dropletComputeShader.SetFloat("targetDensity", density);
        dropletComputeShader.SetFloat("smoothingRadius", smoothingRadius);
        dropletComputeShader.SetFloat("collisionDampling", collisionDampling);
        dropletComputeShader.SetFloat("nearPressureMultiplier", nearPressureMultiplier);
        dropletComputeShader.SetVector("limitSize", limitSize);
        dropletComputeShader.SetFloat("pressureMultiplier", pressureMultiplier);
        dropletComputeShader.SetFloat("viscosityMultiplier", viscosityMultiplier);
        dropletComputeShader.SetFloat("maxSpeed", maxSpeed);

    }

    void Start()
    {

        dropletPositionBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletVelocityBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletDensityBuffer = new ComputeBuffer(numDroplets, sizeof(float));
        dropletsNearDensity = new ComputeBuffer(numDroplets, sizeof(float));

        dropletComputeShader.SetInt("startIndex", 0);
        dropletComputeShader.SetInt("endIndex", numDroplets);

        dropletInstances = new List<GameObject>();
        for (int i = 0; i < numDroplets; i++)
        {
            GameObject dropletInstance = Instantiate(dropletPrefab, spawnCentre, Quaternion.identity);
            dropletInstances.Add(dropletInstance);
        }

        InitializeDroplets();
    }

    private void InitializeDroplets()
    {
        int kernelSpawnIndex = dropletComputeShader.FindKernel("GenerateSpawnData");


        dropletComputeShader.SetInt("numDroplets", numDroplets);
        dropletComputeShader.SetFloats("spawnSize", spawnSize.x, spawnSize.y, spawnSize.z);
        dropletComputeShader.SetFloats("spawnCentre", spawnCentre.x, spawnCentre.y, spawnCentre.z);
        dropletComputeShader.SetBuffer(kernelSpawnIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelSpawnIndex, "dropletsVelocity", dropletVelocityBuffer);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelSpawnIndex, threadGroupsX, 1, 1);

        dropletsPosition = new Vector3[numDroplets];
        dropletPositionBuffer.GetData(dropletsPosition);

        for (int i = 0; i < numDroplets; i++)
        {
            dropletInstances[i].transform.position = dropletsPosition[i];
        }
    }

    void OnDestroy()
    {

        dropletPositionBuffer.Release();
        dropletVelocityBuffer.Release();
        dropletDensityBuffer.Release();
        dropletsNearDensity.Release();
    }

    void Update()
    {
        simulate();
    }

    private void CalcDensity()
    {
        int kernelDensityPressureIndex = dropletComputeShader.FindKernel("CalcDensity");

        dropletComputeShader.SetBuffer(kernelDensityPressureIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelDensityPressureIndex, "dropletsVelocity", dropletVelocityBuffer);
        dropletComputeShader.SetBuffer(kernelDensityPressureIndex, "dropletsDensity", dropletDensityBuffer);
        dropletComputeShader.SetBuffer(kernelDensityPressureIndex, "dropletsNearDensity", dropletsNearDensity);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelDensityPressureIndex, threadGroupsX, 1, 1);

    }

    void CalcPressure()
    {
        int kernelPressureIndex = dropletComputeShader.FindKernel("CalcPressure");
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsDensity", dropletDensityBuffer);
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsVelocity", dropletVelocityBuffer);
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsNearDensity", dropletsNearDensity);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelPressureIndex, threadGroupsX, 1, 1);
    }

    void CalcViscosity()
    {
        int kernelViscosityIndex = dropletComputeShader.FindKernel("CalcViscosity");
        dropletComputeShader.SetBuffer(kernelViscosityIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelViscosityIndex, "dropletsDensity", dropletDensityBuffer);
        dropletComputeShader.SetBuffer(kernelViscosityIndex, "dropletsVelocity", dropletVelocityBuffer);
        dropletComputeShader.SetBuffer(kernelViscosityIndex, "dropletsNearDensity", dropletsNearDensity);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelViscosityIndex, threadGroupsX, 1, 1);
    }

    private void UpdateDropletPositions()
    {
        int kernelPositionIndex = dropletComputeShader.FindKernel("UpdateDropletPosition");

        dropletComputeShader.SetVector("limitSize", limitSize);

        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsDensity", dropletDensityBuffer);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsVelocity", dropletVelocityBuffer);
        dropletComputeShader.SetFloat("deltaTime", timeStep);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelPositionIndex, threadGroupsX, 1, 1);

        dropletPositionBuffer.GetData(dropletsPosition);

        for (int i = 0; i < numDroplets; i++)
        {
            Vector3 halfSize = limitSize * 0.5f;
            Vector3 minBounds = spawnCentre - halfSize;
            Vector3 maxBounds = spawnCentre + halfSize;

            Vector3 clampedPosition = new Vector3(
                Mathf.Clamp(dropletsPosition[i].x, minBounds.x, maxBounds.x),
                Mathf.Clamp(dropletsPosition[i].y, minBounds.y, maxBounds.y),
                Mathf.Clamp(dropletsPosition[i].z, minBounds.z, maxBounds.z)
            );

            dropletInstances[i].transform.position = clampedPosition;

        }

    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = spawnCentre;
        Vector3 size = new Vector3(limitSize.x, limitSize.y, limitSize.z);

        Gizmos.DrawWireCube(center, size);
    }
}