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


    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();

        if (showSignal)
            BuildSignal();
    }

    void BuildSignal()
    {
        var body = GetComponentInChildren<SkinnedMeshRenderer>();
        float height = body != null ? Mathf.Max(.25f, body.bounds.max.y - transform.position.y) : .42f;
        var signal = new GameObject("Pedestrian signal").transform;
        signal.SetParent(transform, false);
        signal.position = transform.position + Vector3.up * (height + .09f);
        Vector3 scale = transform.lossyScale;
        signal.localScale = new Vector3(
            .087f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
            .087f / Mathf.Max(.001f, Mathf.Abs(scale.y)),
            .087f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
        CartoonModels.Part(signal, "Signal rim", Vector3.zero, Vector3.one,
            new Color32(39, 61, 100, 255), false);
        signalRenderer = CartoonModels.Part(signal, "Signal lamp", Vector3.up * .15f,
            Vector3.one * .82f, new Color32(137, 234, 105, 255), false, 50).GetComponent<Renderer>();
        SetSignal(true);
    }

    // green = pedestrian walking (cars beware), red = pedestrian waiting at the curb
    void SetSignal(bool walking)
    {
        if (signalRenderer != null)
            signalRenderer.sharedMaterial = CartoonModels.MaterialFor(walking
                ? new Color32(137, 234, 105, 255) : new Color32(247, 105, 110, 255), 50);
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
