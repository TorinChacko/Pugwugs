using UnityEngine;

public class DashGhost : MonoBehaviour
{
    public float lifetime = 0.3f;
    public float fadeSpeed = 5f;

    private SpriteRenderer sr;
    private Color color;

    void Start()
    {
        sr = GetComponent<SpriteRenderer>();
        color = sr.color;

        Destroy(gameObject, lifetime);
    }

    void Update()
    {
        color.a -= fadeSpeed * Time.deltaTime;
        sr.color = color;
    }
}