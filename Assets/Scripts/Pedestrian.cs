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

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
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
            }
        }


    }

    public void OnHit(Vector3 forceDir)
    {
        if (!isHit)
        {
            isWalking = false;

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
