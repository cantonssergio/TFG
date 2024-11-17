using System;
using System.Collections.Generic;
using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Settings")]
    public int numDroplets;
    public float gravity;
    public Vector2 spawnSize; // Tamaño del área de generación de gotas
    public Vector3 spawnCentre; // Centro del área de generación de gotas

    public Vector3 limitSize;
    public float initialVelocityMagnitude; // Magnitud de la velocidad inicial de cada gota

    public ComputeShader dropletComputeShader;
    public GameObject dropletPrefab; // Prefab para representar visualmente cada gota

    [Header("Computing")]
    private ComputeBuffer dropletPositionBuffer;
    private ComputeBuffer dropletVelocityBuffer;
    private Vector3[] dropletsPosition;
    private List<GameObject> dropletInstances;

    void Start()
    {
        // Validar la cantidad de gotas
        if (numDroplets <= 0)
        {
            Debug.LogError("El número de gotas debe ser mayor que cero.");
            return;
        }

        // Inicializar el buffer y los datos de las gotas
        dropletsPosition = new Vector3[numDroplets];
        dropletPositionBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletVelocityBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);

        // Inicializar las instancias visuales de las gotas
        dropletInstances = new List<GameObject>();
        for (int i = 0; i < numDroplets; i++)
        {
            GameObject dropletInstance = Instantiate(dropletPrefab, spawnCentre, Quaternion.identity);
            dropletInstances.Add(dropletInstance);
        }

        // Asignar valores a los parámetros del Compute Shader
        if (dropletComputeShader == null)
        {
            Debug.LogError("ComputeShader no asignado.");
            return;
        }

        InitializeDroplets();
    }

    private void InitializeDroplets()
    {
        int kernelPositionIndex = dropletComputeShader.FindKernel("GenerateSpawnData");
        //int kerneVelocitylIndex = dropletComputeShader.FindKernel("");


        dropletComputeShader.SetInt("numDroplets", numDroplets);
        dropletComputeShader.SetFloat("initialVelocityMagnitude", initialVelocityMagnitude);
        dropletComputeShader.SetFloat("gravity", gravity);
        dropletComputeShader.SetFloats("spawnSize", spawnSize.x, spawnSize.y);
        dropletComputeShader.SetFloats("spawnCentre", spawnCentre.x, spawnCentre.y, spawnCentre.z);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsVelocity", dropletVelocityBuffer);


        // Ejecutar el Compute Shader para generar los datos iniciales
        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        dropletComputeShader.Dispatch(kernelPositionIndex, threadGroupsX, 1, 1);

        // Leer los datos generados desde la GPU
        dropletPositionBuffer.GetData(dropletsPosition);

        // Actualizar las posiciones iniciales de las instancias visuales
        for (int i = 0; i < numDroplets; i++)
        {
            dropletInstances[i].transform.position = dropletsPosition[i];
        }
    }

    void OnDestroy()
    {

        dropletPositionBuffer.Release();
        dropletVelocityBuffer.Release();

    }

    void Update()
    {
        UpdateDropletPositions();
    }

    private void UpdateDropletPositions()
    {
        int kernelPositionIndex = dropletComputeShader.FindKernel("UpdateDropletPosition");

        // Actualizar las posiciones de las gotas usando el Compute Shader
        dropletComputeShader.SetFloat("gravity", gravity);
        dropletComputeShader.SetVector("limitSize", limitSize);
        dropletComputeShader.SetFloat("deltaTime", Time.deltaTime);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelPositionIndex, "dropletsVelocity", dropletVelocityBuffer);

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
}