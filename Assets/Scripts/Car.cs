using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Direction
{
    Forward,
    Left,
    Right,
    Full_Left,
    Full_Right,
}

public class Car : MonoBehaviour
{
    public Direction direction;
    int NumberOfTurns;

    Vector3 carTargetDirection;

    public float overallSpeed;

    public float speed;
    public float rotationSpeed = 2.0f;

    public float minDistanceToTurn;

    bool isMoving;
    public bool IsMoving { get { return isMoving; } }
    bool isTurning;
    bool isExiting;
    BoxCollider exitCollider;

    float step;

    Animator animator;

    Vector3 targetepointPosition;
    Quaternion targetRotation;

    List<Vector3> positionHistory;
    List<Quaternion> rotationHistory;

    bool isrecording = true;
    bool isReseting;

    float timePassed;
    public float playbackSpeed = 1.0f;

    int frameIndex;

    bool didHitSomeone;

    private void Start()
    {
        SetNumberOfTurns();
        SetCarTargetDir();

        positionHistory = new List<Vector3>();
        rotationHistory = new List<Quaternion>();

        animator = GetComponent<Animator>();
    }

    void SetNumberOfTurns()
    {
        if (direction == Direction.Forward) NumberOfTurns = 0;
        if (direction == Direction.Right || direction == Direction.Left) NumberOfTurns = 1;
        if (direction == Direction.Full_Left || direction == Direction.Full_Right) NumberOfTurns = 2;
    }

    void SetCarTargetDir()
    {
        if (direction == Direction.Forward) carTargetDirection = this.transform.forward;
        if(direction == Direction.Right || direction == Direction.Full_Right) carTargetDirection = this.transform.right;
        if (direction == Direction.Left || direction == Direction.Full_Left) carTargetDirection = -this.transform.right;
    }

    private void Update()
    {
        // Check for mouse click
        if (Input.GetMouseButtonDown(0) && !isMoving)
        {
            if (!GameManager.Instance.CanTapCar)
                return;

            // Cast a ray from the camera to the mouse position
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            // Check if the ray hits an object with a collider
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << 8))
            {
                // Check if the collider belongs to your desired object
                if (hit.collider.gameObject == gameObject)
                {
                    OnClick();
                }
            }
        }
    }
    void FixedUpdate()
    {
        if (didHitSomeone || isExiting)
            return;

        if (isrecording && isMoving)
        {
            positionHistory.Add(this.transform.position);
            rotationHistory.Add(this.transform.rotation);
        }

        if (isMoving)
        {
            if (isTurning)
            {
                // Calculate the rotation step
                step = overallSpeed * rotationSpeed * Time.deltaTime;

                // Rotate towards the target rotation using a linear interpolation
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, step);

                // Assuming targetRotation is the desired rotation and transform.rotation is the current rotation
                float angleDifference = Quaternion.Angle(transform.rotation, targetRotation);

                // Set a small threshold to consider the rotation as done
                float rotationThreshold = 0.1f;

                if (angleDifference < rotationThreshold)
                {
                    NumberOfTurns--;

                    isTurning = false;

                    targetepointPosition = getTargetPoint();
                }
            }

            transform.position += overallSpeed * transform.forward * speed * Time.deltaTime;

            if (Vector3.Distance(this.transform.position, targetepointPosition) <= minDistanceToTurn && targetepointPosition != Vector3.zero)
            {
                ChangeDirection();
            }
        }

        if (isReseting)
        {
            timePassed += Time.deltaTime;

            if (timePassed >= 1.0 / playbackSpeed)
            {
                frameIndex--;

                this.transform.position = positionHistory[frameIndex];
                this.transform.rotation = rotationHistory[frameIndex];

                timePassed = 0f;
            }

            if (frameIndex <= 0)
            {
                isReseting = false;

                isrecording = true;

                positionHistory.Clear();
                rotationHistory.Clear();

                SetNumberOfTurns();
            }
        }
    }

    bool AllCarStatic()
    {
        // Find all GameObjects with the specified component in the scene
        Car[] cars = FindObjectsOfType<Car>();

        // Do something with the found components
        foreach (Car c in cars)
        {
            if (c.isMoving == true)
                return false;
        }

        return true;
    }
    void OnClick()
    {
        if (AllCarStatic() == false)
            return;

        GameManager.Instance.CarClicked();

        isMoving = true;

        targetepointPosition = getTargetPoint();
    }

    void ChangeDirection()
    {
        if (NumberOfTurns == 0) return;

        targetepointPosition = Vector3.zero;

        targetRotation = getDirection();

        isTurning = true;
    }

    Quaternion getDirection()
    {
        if (this.direction == Direction.Left || this.direction == Direction.Full_Left)
        {
            return Quaternion.Euler(0f, this.transform.eulerAngles.y - 90f, 0f);
        }

        if (this.direction == Direction.Right || this.direction == Direction.Full_Right)
        {
            return Quaternion.Euler(0f, this.transform.eulerAngles.y + 90f, 0f);
        }

        return Quaternion.Euler(0f, 0f, 0f);
    }

    // Comparison function for sorting RaycastHit array based on distance
    private int CompareRaycastHits(RaycastHit hit1, RaycastHit hit2)
    {
        float distanceToCar = Vector3.Distance(hit1.point, this.transform.position);
        float distanceToCar2 = Vector3.Distance(hit2.point, this.transform.position);

        // Compare distances and return the appropriate result
        if (distanceToCar < distanceToCar2)
        {
            return -1; // hit1 comes before hit2
        }
        else if (distanceToCar > distanceToCar2)
        {
            return 1; // hit1 comes after hit2
        }
        else
        {
            return 0; // distances are equal
        }
    }

    Vector3 getTargetPoint()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, 1 << 6);

        System.Array.Sort(hits, CompareRaycastHits);

        Vector3 targetPoint = Vector3.zero;

        foreach (RaycastHit hit in hits)
        {
            if (isRoadPointValid(hit.collider.transform.position))
            {
                return hit.collider.gameObject.transform.position;
            }
        }
        return targetPoint;
    }

    bool isRoadPointValid(Vector3 point)
    {
        Ray ray = new Ray(point, carTargetDirection);
        RaycastHit hit;

        // is there a land on that direction
        if (!Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << 7))
        {
            return true;
        }

        return false;
    }

    void OnHit(Collider c)
    {
        if (!isMoving)
        {
            float dotProduct = Vector3.Dot(c.transform.forward.normalized, this.transform.forward.normalized);

            if (dotProduct > 0)
                animator.Play("OnHitBack");
            if (dotProduct < 0)
                animator.Play("OnHitFront");

        }

        if (this.isMoving)
        {
            GameManager.Instance.CarHit();

            isMoving = false;
            isTurning = false;

            isrecording = false;

            Invoke("Reset", 0.5f);

            frameIndex = positionHistory.Count - 1;
        }
    }

    public void StopCar()
    {
        didHitSomeone = true;
    }

    Vector3 startPosition;
    Quaternion startRotation;
    bool startCaptured;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startCaptured = true;
    }

    /// <summary>Puts an escaped car back where it started (used by Undo).</summary>
    public void ResetToStart()
    {
        CancelInvoke();

        if (startCaptured)
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }

        isMoving = false;
        isTurning = false;
        isExiting = false;
        if (exitCollider != null)
            exitCollider.enabled = true;
        isReseting = false;
        didHitSomeone = false;
        isrecording = true;
        timePassed = 0f;
        targetepointPosition = Vector3.zero;

        if (positionHistory != null) positionHistory.Clear();
        if (rotationHistory != null) rotationHistory.Clear();

        SetNumberOfTurns();
        SetCarTargetDir();

        gameObject.SetActive(true);
    }

    void Reset()
    {
        isReseting = true;
    }

    void OnTriggerEnter(Collider c)
    {
        if (isExiting)
            return;

        if (c.CompareTag("Car"))
        {
            OnHit(c);
        }

        if (c.CompareTag("EscapeTrigger") && isMoving && !didHitSomeone)
            StartCoroutine(DriveOffscreen());
    }

    IEnumerator DriveOffscreen()
    {
        isExiting = true;
        Vector3 exitPosition = transform.position;
        Vector3 velocity = transform.forward * Mathf.Max(overallSpeed * speed, 1f);
        exitCollider = GetComponent<BoxCollider>();
        if (exitCollider != null)
            exitCollider.enabled = false;

        Bounds bounds = new Bounds(exitPosition, Vector3.zero);
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            if (renderer.enabled)
                bounds.Encapsulate(renderer.bounds);
        }
        bounds.Expand(.10f);

        var camera = Camera.main;
        var planes = new Plane[6];
        for (float elapsed = 0; camera != null && elapsed < 3f; elapsed += Time.deltaTime)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                break;

            Vector3 travel = velocity * Time.deltaTime;
            transform.position += travel;
            bounds.center += travel;
            yield return null;
        }

        isMoving = false;
        GameManager.Instance.CarEscape(gameObject, exitPosition);
    }
}
