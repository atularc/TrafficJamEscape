using UnityEngine;

/// <summary>Hop while the animal is moving, gentle breathing while it waits. Only moves the visual model.</summary>
sealed class AnimalMotion : MonoBehaviour
{
    Car car;
    float hopHeight;
    float hopSpeed;
    float phase;
    float breathOffset;
    Vector3 restPosition;
    Vector3 restScale;

    public void Init(Car owner, float height, float speed)
    {
        car = owner;
        hopHeight = height;
        hopSpeed = speed;
        restPosition = transform.localPosition;
        restScale = transform.localScale;
        breathOffset = Random.value * 6f;
    }

    void LateUpdate()
    {
        if (car == null)
            return;

        if (car.IsMoving)
        {
            phase += Time.deltaTime * hopSpeed;
            float hop = Mathf.Abs(Mathf.Sin(phase));
            transform.localPosition = restPosition + Vector3.up * (hop * hopHeight * restScale.y);
            transform.localRotation = Quaternion.Euler(-7f * Mathf.Cos(phase), 0f, 6f * Mathf.Sin(phase * .5f));
            float squash = 1f - (1f - hop) * .08f;
            transform.localScale = new Vector3(restScale.x * (2f - squash), restScale.y * squash, restScale.z);
        }
        else
        {
            phase = 0f;
            float breath = Mathf.Sin(Time.time * 2.4f + breathOffset) * .025f;
            transform.localPosition = restPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = new Vector3(restScale.x * (1f - breath * .5f), restScale.y * (1f + breath), restScale.z);
        }
    }
}
