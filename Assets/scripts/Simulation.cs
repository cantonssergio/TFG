using System;
using System.Collections.Generic;
using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Settings")]
    public int numDroplets;

    public float timeStep;
    public float gravity;
    public float density;
    public float collisionDampling;
    public float smoothingRadius;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityMultiplier;
    public Vector2 spawnSize; // Tamaño del área de generación de gotas
    public Vector3 spawnCentre; // Centro del área de generación de gotas

    public Vector3 limitSize;
    public float initialVelocityMagnitude; // Magnitud de la velocidad inicial de cada gota

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

        dropletComputeShader.SetVector("limitSize", limitSize);
        dropletComputeShader.SetFloat("pressureMultiplier", pressureMultiplier);
        dropletComputeShader.SetFloat("viscosityMultiplier", viscosityMultiplier);

    }

    void Start()
    {

        dropletPositionBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletVelocityBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletDensityBuffer = new ComputeBuffer(numDroplets, sizeof(float));
        dropletsNearDensity = new ComputeBuffer(numDroplets, sizeof(float));


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
        /*
        int kernelPositionIndex = dropletComputeShader.FindKernel("GenerateSpawnData");
        //int kerneVelocitylIndex = dropletComputeShader.FindKernel("");


        dropletComputeShader.SetInt("numDroplets", numDroplets);
        dropletComputeShader.SetFloat("initialVelocityMagnitude", initialVelocityMagnitude);
        dropletComputeShader.SetFloat("gravity", gravity);
        dropletComputeShader.SetFloat("targetDensity", density);
        dropletComputeShader.SetFloat("smoothingRadius", smoothingRadius);
        dropletComputeShader.SetFloats("spawnSize", spawnSize.x, spawnSize.y);
        dropletComputeShader.SetFloats("spawnCentre", spawnCentre.x, spawnCentre.y, spawnCentre.z);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsVelocity", dropletVelocityBuffer);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelPositionIndex, threadGroupsX, 1, 1);

        dropletsPosition = new Vector3[numDroplets];
        dropletPositionBuffer.GetData(dropletsPosition);*/
        int kernelSpawnIndex = dropletComputeShader.FindKernel("GenerateSpawnData");


        dropletComputeShader.SetInt("numDroplets", numDroplets);
        dropletComputeShader.SetFloats("spawnSize", spawnSize.x, spawnSize.y);
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
        dropletComputeShader.SetBuffer(kernelDensityPressureIndex, "dropletsNearDensity", dropletDensityBuffer);

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

        // Ejecutar el Compute Shader para actualizar las posiciones de las gotas
        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelPositionIndex, threadGroupsX, 1, 1);

        // Leer los datos actualizados desde la GPU
        dropletPositionBuffer.GetData(dropletsPosition);


        // Actualizar las posiciones de las instancias visuales de las gotas
        for (int i = 0; i < numDroplets; i++)
        {
            dropletInstances[i].transform.position = dropletsPosition[i];
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = spawnCentre;
        Vector3 size = new Vector3(limitSize.x, limitSize.y, 0.1f);

        Gizmos.DrawWireCube(center, size);
    }
}