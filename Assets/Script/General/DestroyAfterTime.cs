using System.Collections;
using System.Collections.Generic;
using ToolBox;
using ToolBox.Pools;
using UnityEngine;

public class DestroyAfterTime : MonoBehaviour, ToolBox.Pools.IPoolable
{
    [SerializeField] float destoryTime = 5f;
    [SerializeField] bool useObjectPooling = false;

    private void Start()
    {
        //Start the coroutine to destroy the object after the specified time
        StartCoroutine(Co_DestroyAfterTime());
    }

    IEnumerator Co_DestroyAfterTime()
    {
        // Wait for the specified time
        yield return new WaitForSeconds(destoryTime);

        // Check if the object is not pooled
        if (!useObjectPooling)
        {
            // Destroy the object
            Destroy(gameObject);
        }
        else
        {
            //Return back to the pool
            gameObject.Release();
        }
    }

    public void OnPool()
    {
        //Start the coroutine to destroy the object after the specified time
        StartCoroutine(Co_DestroyAfterTime());
    }

    public void OnDepool()
    {

    }
}
