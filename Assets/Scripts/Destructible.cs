using UnityEngine;
using UnityEngine.Tilemaps;

public class Destructible : MonoBehaviour
{
    public void OnDestroyed()
    {
        Destroy(gameObject);
    }
}
