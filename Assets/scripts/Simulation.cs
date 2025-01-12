using System.Collections.Generic;
using UnityEngine;

public class Simulation : MonoBehaviour
{
    [Header("Settings")]
    [Range(4, 10)] public int dropletsBrush;
    private int numDroplets;
    const int MAXDROPLETS = 8100;
    [Range(2f, 0.1f)] public float timeStep;
    public float gravity;
    public float density;
    private float maxForce;
    [Range(0, 1)] public float collisionDampling;
    public float smoothingRadius;
    public float pressureMultiplier;
    public float nearPressureMultiplier;
    public float viscosityMultiplier;
    private float maxSpeed;
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
    private ComputeBuffer objectCentersBuffer;
    private ComputeBuffer objectSizesBuffer;
    private ComputeBuffer objectVelocitiesBuffer;
    private Vector3[] dropletsPosition;
    private Vector3[] dropletsVelocity;
    private List<GameObject> dropletInstances;

    private bool isPaused;
    private float particleSpawnInterval = 0.1f;
    private float lastSpawnTime = 0f;




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
        dropletComputeShader.SetInt("numDroplets", numDroplets);
        dropletComputeShader.SetInt("endIndex", numDroplets);
        dropletComputeShader.SetFloat("maxSpeed", maxSpeed);
        dropletComputeShader.SetFloat("maxForce", maxForce);

    }

    void Start()
    {

        Debug.Log("R: Reiniciar simulación");
        Debug.Log("Click izquierdo: generar partículas");
        Debug.Log("Click derecho: eliminar partículas");
        Debug.Log("Espacio: pausar simulación");

        dropletPositionBuffer = new ComputeBuffer(MAXDROPLETS, sizeof(float) * 3);
        dropletVelocityBuffer = new ComputeBuffer(MAXDROPLETS, sizeof(float) * 3);
        dropletDensityBuffer = new ComputeBuffer(MAXDROPLETS, sizeof(float));
        dropletsNearDensity = new ComputeBuffer(MAXDROPLETS, sizeof(float));
        objectCentersBuffer = new ComputeBuffer(10, sizeof(float) * 3);
        objectSizesBuffer = new ComputeBuffer(10, sizeof(float) * 3);
        objectVelocitiesBuffer = new ComputeBuffer(10, sizeof(float) * 3);
        isPaused = false;
        numDroplets = 1225;
        dropletsBrush = 4;
        maxForce = 10000f;
        maxSpeed = 100f;

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

        dropletsPosition = new Vector3[MAXDROPLETS];
        dropletsVelocity = new Vector3[MAXDROPLETS];
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
        objectCentersBuffer.Release();
        objectSizesBuffer.Release();
        objectVelocitiesBuffer.Release();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            InitializeDroplets();
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            isPaused = !isPaused;
        }

        if (Input.GetMouseButton(0))
        {
            if (Time.time >= lastSpawnTime + particleSpawnInterval)
            {
                Vector3 mousePosition = GetMouseWorldPosition();
                GenerateParticle(mousePosition, dropletsBrush);
                lastSpawnTime = Time.time;
            }
        }
        if (Input.GetMouseButton(1))
        {
            if (Time.time >= lastSpawnTime + particleSpawnInterval)
            {
                Vector3 mousePosition = GetMouseWorldPosition();
                RemoveParticles(mousePosition);
                lastSpawnTime = Time.time;
            }
        }

        if (!isPaused)
        {
            simulate();
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Vector3 mouseScreenPosition = Input.mousePosition;
        mouseScreenPosition.z = spawnCentre.z - Camera.main.transform.position.z;

        Vector3 worldPosition = Camera.main.ScreenToWorldPoint(mouseScreenPosition);
        worldPosition.z = spawnCentre.z;
        return worldPosition;
    }




    private void GenerateParticle(Vector3 position, int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            if (numDroplets < MAXDROPLETS)
            {
                Vector3 pos = new Vector3(
                    position.x + Random.Range(-0.1f, 0.1f),
                    position.y + Random.Range(-0.1f, 0.1f),
                    position.z
                );

                GameObject dropletInstance = Instantiate(dropletPrefab, pos, Quaternion.identity);
                dropletInstances.Add(dropletInstance);

                if (numDroplets < dropletsPosition.Length)
                {
                    dropletsPosition[numDroplets] = pos;
                    numDroplets++;
                }
                else
                {
                    break;
                }
            }
        }

        dropletPositionBuffer.SetData(dropletsPosition);
    }


    private void RemoveParticles(Vector3 position)
    {
        float removeRadius = dropletsBrush / 10f;
        for (int i = dropletInstances.Count - 1; i >= 0; i--)
        {
            if (Vector3.Distance(dropletInstances[i].transform.position, position) < removeRadius && numDroplets > 0)
            {
                Destroy(dropletInstances[i]);
                dropletInstances.RemoveAt(i);

                dropletPositionBuffer.GetData(dropletsPosition);
                dropletVelocityBuffer.GetData(dropletsVelocity);
                for (int j = i; j < numDroplets - 1; j++)
                {
                    dropletsPosition[j] = dropletsPosition[j + 1];
                    dropletsVelocity[j] = dropletsVelocity[j + 1];
                }
                dropletPositionBuffer.SetData(dropletsPosition);
                dropletVelocityBuffer.SetData(dropletsVelocity);
                numDroplets--;
            }
        }
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
        dropletComputeShader.SetFloat("deltaTime", timeStep / 100f);

        int threadGroupsX = Mathf.CeilToInt(numDroplets / 16.0f);
        UpdateInteractableObjects(dropletComputeShader);
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

    void UpdateInteractableObjects(ComputeShader computeShader)
    {
        int kernelInteractionIndex = computeShader.FindKernel("UpdateDropletPosition");
        FluidInteractable[] interactables = FindObjectsByType<FluidInteractable>(FindObjectsSortMode.None);

        List<Vector3> centers = new List<Vector3>();
        List<Vector3> sizes = new List<Vector3>();
        List<Vector3> velocities = new List<Vector3>();

        foreach (FluidInteractable obj in interactables)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider == null) continue;

            Bounds bounds = collider.bounds;
            centers.Add(bounds.center);
            sizes.Add(bounds.size + obj.SizeOffset);

            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                velocities.Add(rb.linearVelocity);
            }
            else
            {
                velocities.Add(Vector3.zero);
            }
        }


        // Actualizar los buffers de datos
        objectCentersBuffer.SetData(centers);
        objectSizesBuffer.SetData(sizes);
        objectVelocitiesBuffer.SetData(velocities); // Buffer para velocidades

        // Pasar los buffers al ComputeShader
        computeShader.SetBuffer(kernelInteractionIndex, "centers", objectCentersBuffer);
        computeShader.SetBuffer(kernelInteractionIndex, "sizes", objectSizesBuffer);
        computeShader.SetBuffer(kernelInteractionIndex, "interactableVelocities", objectVelocitiesBuffer); // Pasar velocidades
        computeShader.SetInt("numInteractables", centers.Count);
    }



    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Vector3 center = spawnCentre;
        Vector3 size = new Vector3(limitSize.x, limitSize.y, limitSize.z);

        Gizmos.DrawWireCube(center, size);
    }
}