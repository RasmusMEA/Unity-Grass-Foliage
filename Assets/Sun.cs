using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Light))]
public class Sun : MonoBehaviour {

    // The speed at which the sun rotates.
    [SerializeField] private float rotationSpeed = 1.0f;

    // Light Component
    private Vector3 lightDirection;
    private Light lightComponent;
    private float intensity;

    void Start() {
        lightComponent = GetComponent<Light>();
        intensity = lightComponent.intensity;
    }

    // Update is called once per frame
    void Update() {
        
        // Scale intensity based on time of day.
        lightComponent.intensity = Mathf.Clamp01(Vector3.Dot(lightDirection, Vector3.down)) * intensity;

        // Rotate the sun.
        transform.Rotate(Vector3.right, rotationSpeed * Time.deltaTime);

        // Update the light direction.
        lightDirection = transform.forward;
    }
}
