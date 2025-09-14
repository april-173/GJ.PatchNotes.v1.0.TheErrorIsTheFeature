using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Default shake")]
    public float defaultAmp = 0.14f;
    public float defaultFreq = 22f;
    public float defaultTime = 0.12f;

    Vector3 baseLocalPos;
    Coroutine co;

    void Awake()
    {
        if (Instance == null) Instance = this;
        baseLocalPos = transform.localPosition;
    }

    public void Shake(float amp = -1f, float freq = -1f, float time = -1f)
    {
        if (amp < 0) amp = defaultAmp;
        if (freq < 0) freq = defaultFreq;
        if (time < 0) time = defaultTime;

        if (co != null) StopCoroutine(co);
        co = StartCoroutine(Co_Shake(amp, freq, time));
    }

    IEnumerator Co_Shake(float amp, float freq, float dur)
    {
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = 1f - (t / dur);
            float offX = (Mathf.PerlinNoise(0, Time.time * freq) - 0.5f) * 2f * amp * k;
            float offY = (Mathf.PerlinNoise(1, Time.time * freq) - 0.5f) * 2f * amp * k;
            transform.localPosition = baseLocalPos + new Vector3(offX, offY, 0f);
            yield return null;
        }
        transform.localPosition = baseLocalPos;
        co = null;
    }
}
