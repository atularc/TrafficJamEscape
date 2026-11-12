using System.Collections;
using System.Collections.Generic;

using UnityEngine;

public class Pedestrian : MonoBehaviour
{
    Rigidbody rb;
    Animator animator;

    public Transform positionA;
    public Transform positionB;

    public bool isWalking;
    public bool isHit;

    public float speed;

    public float hitPower;
    float t;

    [Header("Pedestrian signal")]
    [Tooltip("Seconds the pedestrian waits at the curb at each end (signal shows red-man, cars may pass).")]
    public float waitAtEnds = 1.5f;
    public bool showSignal = true;

    float waitTimer;
    Renderer signalRenderer;
    Material signalMaterial;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (showSignal)
            BuildSignal();
    }

    void BuildSignal()
    {
        GameObject s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        s.name = "PedestrianSignal";
        Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(transform, false);
        float parentScale = Mathf.Max(0.0001f, transform.lossyScale.x);
        s.transform.localScale = Vector3.one * (0.16f / parentScale);
        s.transform.localPosition = new Vector3(0f, 1.25f / parentScale, 0f);

        signalRenderer = s.GetComponent<Renderer>();
        signalRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        signalMaterial = new Material(Shader.Find("Sprites/Default"));
        signalRenderer.material = signalMaterial;
        SetSignal(true);
    }

    // green = pedestrian walking (cars beware), red = pedestrian waiting at the curb
    void SetSignal(bool walking)
    {
        if (signalMaterial != null)
            signalMaterial.color = walking ? new Color(0.2f, 0.9f, 0.3f, 1f) : new Color(1f, 0.2f, 0.2f, 1f);
        if (animator != null && !isHit)
            animator.speed = walking ? 1f : 0f;
    }

    void Update()
    {
        if (isWalking && waitTimer > 0f)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
                SetSignal(true);
            return;
        }

        if (isWalking)
        {
            t += speed * Time.deltaTime;

            // Use SmoothStep to create an ease-in motion
            float smoothStepT = Mathf.SmoothStep(0f, 1f, t);

            // Use Lerp to interpolate between positionA and positionB
            transform.position = Vector3.Lerp(positionA.position, positionB.position, smoothStepT);
            transform.LookAt(positionB.position);

            // Check if the object has reached positionB
            if (t >= 1f)
            {
                // Reset t and swap positions of positionA and positionB
                t = 0f;
                Transform temp = positionA;
                positionA = positionB;
                positionB = temp;

                if (waitAtEnds > 0f)
                {
                    waitTimer = waitAtEnds;
                    SetSignal(false);
                }
            }
        }


    }

    public void OnHit(Vector3 forceDir)
    {
        if (!isHit)
        {
            isWalking = false;

            if (animator != null)
                animator.speed = 1f;

            rb.isKinematic = false;
            rb.useGravity = true;

            rb.AddForce(forceDir * hitPower, ForceMode.Impulse);

            Invoke("PlayDeadAnimation", 1.8f);

            GameManager.Instance.LevelLose();

            isHit = true;
        }
    }

    void PlayDeadAnimation()
    {
        animator.Play("Dead_Loop");
    }

    Collider other;
    public void OnTriggerEnter(Collider c)
    {
        if (c.tag == "Car")
        {
            other = c;

            Vector3 forceDirection = (this.transform.position - c.transform.position).normalized;

            Invoke("StopCar", 0.3f);

            OnHit(forceDirection);
        }
    }

    void StopCar()
    {
        other.GetComponent<Car>().StopCar();
    }
}
