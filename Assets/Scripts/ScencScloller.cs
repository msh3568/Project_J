using UnityEngine;

public class ScencScloller : MonoBehaviour
{

    public float ScrollSpeed = 1.0f;
    Material myMaterial;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        myMaterial = GetComponent<Renderer>().material;
    }

    // Update is called once per frame
    void Update()
    {
        float offset = Time.time * ScrollSpeed;
        myMaterial.mainTextureOffset = new Vector2(offset, 0);
    }
}
