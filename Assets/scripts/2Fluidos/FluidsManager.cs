using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Fluid
{
    public Fluid(FluidConfig obj, int numDroplets, float proportion)
    {
        config = obj;
        numOwnDroplets = Mathf.RoundToInt(proportion * numDroplets);
    }

    public FluidConfig config;
    private int numOwnDroplets;

    public int NumDroplets => numOwnDroplets;
}

public class FluidsManager : MonoBehaviour
{
    [Header("Settings")]
    public int numDroplets;
    [Range(0, 1)] public float collisionDampling;
    [Range(0.01f, 0.001f)] public float timeStep;
    public float smoothingRadius;
    public float maxSpeed;
    public float maxForce;
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
    private float RedFluidProportion => 1f - blueFluidProportion;

    private List<GameObject> dropletInstances;
    private ComputeBuffer dropletPositionBuffer;
    private ComputeBuffer dropletVelocityBuffer;
    private ComputeBuffer dropletDensityBuffer;
    private ComputeBuffer dropletsNearDensity;
    private Vector3[] dropletsPosition;

    private void OnDestroy()
    {
        dropletPositionBuffer?.Release();
        dropletVelocityBuffer?.Release();
        dropletDensityBuffer?.Release();
        dropletsNearDensity?.Release();
    }

    private void Update()
    {
        Simulate();
    }

    private void Simulate()
    {
        ProcessFluids(CalcDensity);
        ProcessFluids(CalcPressure);
        ProcessFluids(CalcViscosity);
        UpdatePositions();
    }

    private void Start()
    {
        blueFluid = new Fluid(blueFluidConfig, numDroplets, blueFluidProportion);
        redFluid = new Fluid(redFluidconfig, numDroplets, RedFluidProportion);

        AllocateBuffers();
        CreateDropletInstances();
        InitializeDroplets();
    }

    private void AllocateBuffers()
    {
        dropletPositionBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletVelocityBuffer = new ComputeBuffer(numDroplets, sizeof(float) * 3);
        dropletDensityBuffer = new ComputeBuffer(numDroplets, sizeof(float));
        dropletsNearDensity = new ComputeBuffer(numDroplets, sizeof(float));
        dropletsPosition = new Vector3[numDroplets];
    }

    private void CreateDropletInstances()
    {
        dropletInstances = new List<GameObject>();

        CreateInstancesForFluid(blueFluid);
        CreateInstancesForFluid(redFluid);
    }

    private void CreateInstancesForFluid(Fluid fluid)
    {
        for (int i = 0; i < fluid.NumDroplets; i++)
        {
            GameObject dropletInstance = Instantiate(fluid.config.dropletPrefab, spawnCentre, Quaternion.identity);
            dropletInstances.Add(dropletInstance);
        }
    }

    private void UpdateSettings(FluidConfig config)
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
        dropletComputeShader.SetFloat("maxForce", maxForce);
    }

    private void ProcessFluids(System.Action<int, int> kernelAction)
    {
        ProcessFluid(blueFluid, 0, blueFluid.NumDroplets, kernelAction);
        ProcessFluid(redFluid, blueFluid.NumDroplets, numDroplets, kernelAction);
    }

    private void ProcessFluid(Fluid fluid, int startIndex, int endIndex, System.Action<int, int> kernelAction)
    {
        UpdateSettings(fluid.config);
        kernelAction(startIndex, endIndex);
    }

    private void InitializeDroplets()
    {
        int kernelSpawnIndex = dropletComputeShader.FindKernel("GenerateSpawnData");

        dropletComputeShader.SetInt("numDroplets", numDroplets);
        dropletComputeShader.SetFloats("spawnSize", spawnSize.x, spawnSize.y, spawnSize.z);
        dropletComputeShader.SetFloats("spawnCentre", spawnCentre.x, spawnCentre.y, spawnCentre.z);
        dropletComputeShader.SetBuffer(kernelSpawnIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelSpawnIndex, "dropletsVelocity", dropletVelocityBuffer);

        DispatchKernel(kernelSpawnIndex, numDroplets);
        dropletPositionBuffer.GetData(dropletsPosition);

        for (int i = 0; i < numDroplets; i++)
        {
            dropletInstances[i].transform.position = dropletsPosition[i];
        }
    }

    private void CalcDensity(int startIndex, int endIndex)
    {
        DispatchComputeKernel("CalcDensity", startIndex, endIndex);
    }

    private void CalcPressure(int startIndex, int endIndex)
    {
        DispatchComputeKernel("CalcPressure", startIndex, endIndex);
    }

    private void CalcViscosity(int startIndex, int endIndex)
    {
        DispatchComputeKernel("CalcViscosity", startIndex, endIndex);
    }

    private void DispatchComputeKernel(string kernelName, int startIndex, int endIndex)
    {
        int kernelIndex = dropletComputeShader.FindKernel(kernelName);

        dropletComputeShader.SetBuffer(kernelIndex, "dropletsPosition", dropletPositionBuffer);
        dropletComputeShader.SetBuffer(kernelIndex, "dropletsVelocity", dropletVelocityBuffer);
        dropletComputeShader.SetBuffer(kernelIndex, "dropletsDensity", dropletDensityBuffer);
        dropletComputeShader.SetBuffer(kernelIndex, "dropletsNearDensity", dropletsNearDensity);

        dropletComputeShader.SetInt("startIndex", startIndex);
        dropletComputeShader.SetInt("endIndex", endIndex);

        DispatchKernel(kernelIndex, endIndex - startIndex);
    }

    private void DispatchKernel(int kernelIndex, int count)
    {
        int threadGroupsX = Mathf.CeilToInt(count / 16.0f);
        dropletComputeShader.Dispatch(kernelIndex, threadGroupsX, 1, 1);
    }

    private void UpdatePositions()
    {
        DispatchComputeKernel("UpdateDropletPosition", 0, numDroplets);
        dropletPositionBuffer.GetData(dropletsPosition);

        for (int i = 0; i < numDroplets; i++)
        {
            Vector3 clampedPosition = ClampPosition(dropletsPosition[i]);
            dropletInstances[i].transform.position = clampedPosition;
        }
    }

    private Vector3 ClampPosition(Vector3 position)
    {
        Vector3 halfSize = limitSize * 0.5f;
        Vector3 minBounds = spawnCentre - halfSize;
        Vector3 maxBounds = spawnCentre + halfSize;

        return new Vector3(
            Mathf.Clamp(position.x, minBounds.x, maxBounds.x),
            Mathf.Clamp(position.y, minBounds.y, maxBounds.y),
            Mathf.Clamp(position.z, minBounds.z, maxBounds.z)
        );
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(spawnCentre, limitSize);
    }
}
