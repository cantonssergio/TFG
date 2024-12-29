using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Fluid
{
    public Fluid(FluidConfig obj, int numDroplets, float proportion)
    {
        config = obj;
        numOwnDroplets = (int)(proportion * numDroplets);


    }


    public FluidConfig config;

    private int numOwnDroplets;
    public int getNumDroplets()
    {
        return numOwnDroplets;
    }

}

public class FluidsManager : MonoBehaviour
{
    [Header("Settings")]
    public int numDroplets;
    [Range(0, 1)] public float collisionDampling;
    [Range(0.01f, 0.001f)] public float timeStep;
    public float smoothingRadius;
    public float maxSpeed;
    public Vector3 spawnSize;
    public Vector3 spawnCentre;
    public Vector3 limitSize;
    public ComputeShader dropletComputeShader;

    [Header("Fluids")]
    public FluidConfig blueFluidConfig;
    public FluidConfig redFluidconfig;
    private Fluid blueFluid;
    private Fluid redFluid;

    [Header("Proportions")]
    [Range(0f, 1f)] public float blueFluidProportion = 0.5f;
    private float redFluidProportion => 1f - blueFluidProportion;

    private List<GameObject> dropletInstances;
    private ComputeBuffer dropletPositionBuffer;
    public ComputeBuffer dropletVelocityBuffer;
    public ComputeBuffer dropletDensityBuffer;
    public ComputeBuffer dropletsNearDensity;
    private Vector3[] dropletsPosition;
    void OnDestroy()
    {
        dropletPositionBuffer.Release();
        dropletVelocityBuffer.Release();
        dropletDensityBuffer.Release();
        dropletsNearDensity.Release();

    }
    void Update()
    {
        Simulate();


    }

    void Simulate()
    {
        CalcDensity();
        CalcPressure();
        CalcViscosity();
        UpdatePositions();
    }

    private void Start()
    {
        redFluid = new Fluid(redFluidconfig, numDroplets, redFluidProportion);
        blueFluid = new Fluid(blueFluidConfig, numDroplets, blueFluidProportion);
        dropletPositionBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletVelocityBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletDensityBuffer = new ComputeBuffer(numDroplets, sizeof(float));
        dropletsNearDensity = new ComputeBuffer(numDroplets, sizeof(float));

        dropletInstances = new List<GameObject>();
        for (int i = 0; i < blueFluid.getNumDroplets(); i++)
        {
            GameObject dropletInstance = Instantiate(blueFluid.config.dropletPrefab, spawnCentre, Quaternion.identity);
            dropletInstances.Add(dropletInstance);
        }
        for (int i = 0; i < redFluid.getNumDroplets(); i++)
        {
            GameObject dropletInstance = Instantiate(redFluid.config.dropletPrefab, spawnCentre, Quaternion.identity);
            dropletInstances.Add(dropletInstance);
        }

        InitializeDroplets();
    }

    void UpdateSettings(FluidConfig config)
    {
        dropletComputeShader.SetFloat("gravity", config.gravity);
        dropletComputeShader.SetFloat("targetDensity", config.density);
        dropletComputeShader.SetFloat("smoothingRadius", smoothingRadius);
        dropletComputeShader.SetFloat("collisionDampling", collisionDampling);
        dropletComputeShader.SetFloat("nearPressureMultiplier", config.nearPressureMultiplier);
        dropletComputeShader.SetVector("limitSize", limitSize);
        dropletComputeShader.SetFloat("pressureMultiplier", config.pressureMultiplier);
        dropletComputeShader.SetFloat("viscosityMultiplier", config.viscosityMultiplier);
        dropletComputeShader.SetFloat("maxSpeed", maxSpeed);
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


    private void CalcDensity()
    {
        int kernelDensityIndex = dropletComputeShader.FindKernel("CalcDensity");

        dropletComputeShader.SetBuffer(kernelDensityIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelDensityIndex, "dropletsVelocity", dropletVelocityBuffer);
        dropletComputeShader.SetBuffer(kernelDensityIndex, "dropletsDensity", dropletDensityBuffer);
        dropletComputeShader.SetBuffer(kernelDensityIndex, "dropletsNearDensity", dropletsNearDensity);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);

        UpdateSettings(blueFluid.config);

        dropletComputeShader.SetInt("startIndex", 0);
        dropletComputeShader.SetInt("endIndex", blueFluid.getNumDroplets());
        dropletComputeShader.Dispatch(kernelDensityIndex, threadGroupsX, 1, 1);

        UpdateSettings(redFluid.config);

        dropletComputeShader.SetInt("startIndex", blueFluid.getNumDroplets());
        dropletComputeShader.SetInt("endIndex", redFluid.getNumDroplets() + blueFluid.getNumDroplets());
        dropletComputeShader.Dispatch(kernelDensityIndex, threadGroupsX, 1, 1);

    }
    void CalcPressure()
    {
        int kernelPressureIndex = dropletComputeShader.FindKernel("CalcPressure");
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsDensity", dropletDensityBuffer);
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsVelocity", dropletVelocityBuffer);
        dropletComputeShader.SetBuffer(kernelPressureIndex, "dropletsNearDensity", dropletsNearDensity);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);

        UpdateSettings(blueFluid.config);

        dropletComputeShader.SetInt("startIndex", 0);
        dropletComputeShader.SetInt("endIndex", blueFluid.getNumDroplets());
        dropletComputeShader.Dispatch(kernelPressureIndex, threadGroupsX, 1, 1);

        UpdateSettings(redFluid.config);

        dropletComputeShader.SetInt("startIndex", blueFluid.getNumDroplets());
        dropletComputeShader.SetInt("endIndex", redFluid.getNumDroplets() + blueFluid.getNumDroplets());
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

        UpdateSettings(blueFluid.config);

        dropletComputeShader.SetInt("startIndex", 0);
        dropletComputeShader.SetInt("endIndex", blueFluid.getNumDroplets());
        dropletComputeShader.Dispatch(kernelViscosityIndex, threadGroupsX, 1, 1);

        UpdateSettings(redFluid.config);

        dropletComputeShader.SetInt("startIndex", blueFluid.getNumDroplets());
        dropletComputeShader.SetInt("endIndex", redFluid.getNumDroplets() + blueFluid.getNumDroplets());
        dropletComputeShader.Dispatch(kernelViscosityIndex, threadGroupsX, 1, 1);
    }

    private void UpdatePositions()
    {
        int kernelPositionIndex = dropletComputeShader.FindKernel("UpdateDropletPosition");

        dropletComputeShader.SetVector("limitSize", limitSize);
        dropletComputeShader.SetInt("startIndex", 0);
        dropletComputeShader.SetInt("endIndex", numDroplets);

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
