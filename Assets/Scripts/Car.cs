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

    public float overallSpeed;

    public float speed;
    [Tooltip("Turn rate in degrees per second (scaled by overallSpeed). With 'speed' it sets the turn radius.")]
    public float rotationSpeed = 2.0f;

    [Tooltip("Not used any more: turns are planned from the road point geometry and the turn radius.")]
    public float minDistanceToTurn;

    bool isMoving;
    public bool IsMoving { get { return isMoving; } }
    bool isTurning;
    bool isExiting;
    BoxCollider exitCollider;

    Animator animator;

    // ---- route state
    // A turn is planned as an exact quarter circle that leaves the current lane and ends on the lane that
    // runs through the chosen road point, so the car always lands centred in the new lane with a clean
    // 90 degree heading, whatever the frame rate or the exact spot where the car was parked.
    bool hasTurnPlanned;
    Vector3 turnStart;
    Vector3 turnCenter;
    float turnRadius;
    float turnSign;          // +1 = right, -1 = left
    float headingBeforeTurn;
    float turnedDegrees;
    float bodyOffset;

    List<Vector3> positionHistory;
    List<Quaternion> rotationHistory;

    bool isrecording = true;
    bool isReseting;

    float timePassed;
    public float playbackSpeed = 1.0f;

    int frameIndex;

    bool didHitSomeone;

    /// <summary>Radius of the arc the car drives when turning (same as the old speed / turn rate ratio).</summary>
    float TurnRadius
    {
        get
        {
            float radius = speed / Mathf.Max(rotationSpeed * Mathf.Deg2Rad, 0.0001f);
            return Mathf.Clamp(radius, 0.1f, 1f);
        }
    }

    float TurnSign
    {
        get { return direction == Direction.Left || direction == Direction.Full_Left ? -1f : 1f; }
    }

    private void Start()
    {
        SetNumberOfTurns();

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
        Step(Time.fixedDeltaTime);
    }

    void Step(float dt)
    {
        if (didHitSomeone || isExiting)
            return;

        if (isrecording && isMoving)
        {
            positionHistory.Add(this.transform.position);
            rotationHistory.Add(this.transform.rotation);
        }

        if (isMoving)
            Drive(overallSpeed * speed * dt, true);

        if (isReseting)
        {
            timePassed += dt;

            if (timePassed >= 1.0 / playbackSpeed && frameIndex > 0)
            {
                frameIndex--;

                this.transform.position = positionHistory[frameIndex];
                this.transform.rotation = rotationHistory[frameIndex];

                timePassed = 0f;
            }

            if (frameIndex <= 0)
            {
                if (positionHistory.Count > 0)
                {
                    this.transform.position = positionHistory[0];
                    this.transform.rotation = rotationHistory[0];
                }

                isReseting = false;

                isrecording = true;

                positionHistory.Clear();
                rotationHistory.Clear();

                SetNumberOfTurns();
                ClearRoute();
            }
        }
    }

    /// <summary>Moves the car 'distance' world units along its route (straight parts and turns).</summary>
    void Drive(float distance, bool allowNewTurns)
    {
        // one step can finish a straight part and start a turn, or finish a turn and go on straight
        for (int i = 0; i < 4 && distance > 0f; i++)
        {
            if (isTurning)
                distance = AdvanceTurn(distance);
            else
                distance = AdvanceStraight(distance, allowNewTurns);
        }
    }

    /// <summary>
    /// How far the middle of the car body sits ahead of the transform pivot. Turns are steered around the
    /// body middle so the front and the rear swing out by the same small amount on narrow roads.
    /// </summary>
    float BodyOffset
    {
        get
        {
            var body = GetComponent<BoxCollider>();
            return body != null ? Vector3.Dot(transform.TransformVector(body.center), transform.forward) : 0f;
        }
    }

    Vector3 BodyMiddle { get { return transform.position + transform.forward * bodyOffset; } }

    float AdvanceStraight(float distance, bool allowNewTurns)
    {
        Vector3 forward = transform.forward;

        if (hasTurnPlanned && allowNewTurns)
        {
            float ahead = Mathf.Max(0f, Vector3.Dot(turnStart - BodyMiddle, forward));
            if (ahead <= distance)
            {
                transform.position += forward * ahead;
                BeginTurn();
                return distance - ahead;
            }
        }

        transform.position += forward * distance;
        return 0f;
    }

    void BeginTurn()
    {
        hasTurnPlanned = false;
        isTurning = true;
        turnedDegrees = 0f;
        headingBeforeTurn = transform.eulerAngles.y;
        turnCenter = BodyMiddle + transform.right * (turnSign * turnRadius);
    }

    float AdvanceTurn(float distance)
    {
        float degreesPerUnit = Mathf.Rad2Deg / turnRadius;
        float degrees = distance * degreesPerUnit;
        float leftover = 0f;

        float remaining = 90f - turnedDegrees;
        if (degrees >= remaining)
        {
            leftover = (degrees - remaining) / degreesPerUnit;
            degrees = remaining;
        }

        turnedDegrees += degrees;

        // the body middle stays on the circle: always turnRadius away from the centre, at right angles to the heading
        Quaternion rotation = Quaternion.Euler(0f, headingBeforeTurn + turnSign * turnedDegrees, 0f);
        Vector3 middle = turnCenter - (rotation * Vector3.right) * (turnSign * turnRadius);
        transform.SetPositionAndRotation(middle - (rotation * Vector3.forward) * bodyOffset, rotation);

        if (turnedDegrees >= 90f)
        {
            isTurning = false;
            NumberOfTurns--;
            PlanNextTurn();
        }

        return leftover;
    }

    /// <summary>Finds the road point where the next turn happens and works out where the arc has to begin.</summary>
    void PlanNextTurn()
    {
        hasTurnPlanned = false;

        if (NumberOfTurns <= 0)
            return;

        turnSign = TurnSign;

        Vector3 corner;
        if (!FindTurnPoint(out corner))
            return;

        bodyOffset = BodyOffset;
        Vector3 position = BodyMiddle;
        Vector3 forward = transform.forward;

        // the new lane is the line through the road point at right angles to the current heading;
        // it crosses the current lane 'along' units ahead of the car
        float along = Vector3.Dot(corner - position, forward);

        // a car parked right before its turning point turns a little tighter so it still ends on the lane
        turnRadius = Mathf.Clamp(along, 0.05f, TurnRadius);
        turnStart = position + forward * (along - turnRadius);
        hasTurnPlanned = true;
    }

    void ClearRoute()
    {
        isTurning = false;
        hasTurnPlanned = false;
        turnedDegrees = 0f;
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

        StartDriving();
    }

    void StartDriving()
    {
        isMoving = true;
        ClearRoute();
        PlanNextTurn();
    }

    // Comparison function for sorting RaycastHit array based on distance
    private int CompareRaycastHits(RaycastHit hit1, RaycastHit hit2)
    {
        float distanceToCar = Vector3.Distance(hit1.point, this.transform.position);
        float distanceToCar2 = Vector3.Distance(hit2.point, this.transform.position);

        return distanceToCar.CompareTo(distanceToCar2);
    }

    /// <summary>
    /// The first road point straight ahead from which the road is free of land all the way in the
    /// direction the car is about to turn to.
    /// </summary>
    bool FindTurnPoint(out Vector3 point)
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, Mathf.Infinity, 1 << 6);

        System.Array.Sort(hits, CompareRaycastHits);

        Vector3 turnDirection = transform.right * turnSign;

        foreach (RaycastHit hit in hits)
        {
            Vector3 candidate = hit.collider.transform.position;
            if (isRoadPointValid(candidate, turnDirection))
            {
                point = candidate;
                return true;
            }
        }

        point = Vector3.zero;
        return false;
    }

    bool isRoadPointValid(Vector3 point, Vector3 turnDirection)
    {
        Ray ray = new Ray(point, turnDirection);

        // is there a land on that direction
        return !Physics.Raycast(ray, Mathf.Infinity, 1 << 7);
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
            ClearRoute();

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
        isExiting = false;
        if (exitCollider != null)
            exitCollider.enabled = true;
        isReseting = false;
        didHitSomeone = false;
        isrecording = true;
        timePassed = 0f;
        ClearRoute();

        if (positionHistory != null) positionHistory.Clear();
        if (rotationHistory != null) rotationHistory.Clear();

        SetNumberOfTurns();

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
        float exitSpeed = Mathf.Max(overallSpeed * speed, 1f);
        exitCollider = GetComponent<BoxCollider>();
        if (exitCollider != null)
            exitCollider.enabled = false;

        var renderers = GetComponentsInChildren<Renderer>();
        var camera = Camera.main;
        var planes = new Plane[6];
        for (float elapsed = 0; camera != null && elapsed < 3f; elapsed += Time.deltaTime)
        {
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            foreach (var renderer in renderers)
            {
                if (renderer != null && renderer.enabled)
                    bounds.Encapsulate(renderer.bounds);
            }
            bounds.Expand(.10f);

            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                break;

            // keep following the road: a car that reaches the exit mid-turn finishes the turn
            // instead of sliding off diagonally across the grass
            Drive(exitSpeed * Time.deltaTime, false);
            yield return null;
        }

        isMoving = false;
        GameManager.Instance.CarEscape(gameObject, exitPosition);
    }
}
