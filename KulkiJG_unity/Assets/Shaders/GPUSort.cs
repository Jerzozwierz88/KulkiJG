using UnityEngine;
using static UnityEngine.Mathf;

public class GPUSort
{
    const int hashKernel = 0;
    const int sortKernel = 1;
    const int startIndexesKernel = 2;
    int totalNumberOfParticles;

    readonly ComputeShader sortCompute;
    ComputeBuffer lookupTable;

    public GPUSort()
    {
        // sortCompute = ComputeHelper.LoadComputeShader("BitonicMergeSort");
        sortCompute = GameObject.FindGameObjectWithTag("Sim").GetComponent<Sim>().bitonic;
    }

    public void SetBuffers(ComputeBuffer lookupTable, ComputeBuffer startLookupIndexes)
    {
        this.lookupTable = lookupTable;
        ComputeHelper.SetBuffer(sortCompute, startLookupIndexes, "StartLookupIndexes", hashKernel, startIndexesKernel);
        ComputeHelper.SetBuffer(sortCompute, lookupTable, "LookupTable", hashKernel, sortKernel, startIndexesKernel);

        totalNumberOfParticles = lookupTable.count;
        sortCompute.SetInt("totalNumberOfParticles", totalNumberOfParticles);
        sortCompute.SetInt("hashCount", startLookupIndexes.count);
    }

    private void CalculateHashes()
    {
        sortCompute.Dispatch(hashKernel, (int)Ceil(totalNumberOfParticles / 128) + 1, 1, 1);
    }

    // Sorts given buffer of integer values using bitonic merge sort
    // Note: buffer size is not restricted to powers of 2 in this implementation
    private void Sort()
    {
        // Launch each step of the sorting algorithm (once the previous step is complete)
        // Number of steps = [log2(n) * (log2(n) + 1)] / 2
        // where n = nearest power of 2 that is greater or equal to the number of inputs
        int numStages = (int)Log(NextPowerOfTwo(lookupTable.count), 2);

        for (int stageIndex = 0; stageIndex < numStages; stageIndex++)
        {
            for (int stepIndex = 0; stepIndex < stageIndex + 1; stepIndex++)
            {
                // Calculate some pattern stuff
                int groupWidth = 1 << (stageIndex - stepIndex);
                int groupHeight = 2 * groupWidth - 1;
                sortCompute.SetInt("groupWidth", groupWidth);
                sortCompute.SetInt("groupHeight", groupHeight);
                sortCompute.SetInt("stepIndex", stepIndex);
                // Run the sorting step on the GPU
                ComputeHelper.Dispatch(sortCompute, NextPowerOfTwo(lookupTable.count) / 2, kernelIndex: sortKernel);
            }
        }
    }


    private void CalculateStartLookupIndexes()
    {
        ComputeHelper.Dispatch(sortCompute, totalNumberOfParticles, kernelIndex: startIndexesKernel);
    }

    public void PerformAllHashingSteps()
    {
        CalculateHashes();
        Sort();
        CalculateStartLookupIndexes();
    }

}