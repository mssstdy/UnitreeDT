using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody))]
public class PVZNavigationAgent : Agent
{
    [Header("PVZ References")]
    [SerializeField] private PVZTrainingManager trainingManager;
    [SerializeField] private Rigidbody robotRigidbody;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform currentTarget;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 1.8f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private float targetRadius = 0.35f;

    [Header("Rewards")]
    [SerializeField] private float successReward = 1f;
    [SerializeField] private float collisionPenalty = -1f;
    [SerializeField] private float stepPenalty = -0.001f;
    [SerializeField] private float progressRewardScale = 0.02f;
    [SerializeField] private int maxEpisodeSteps = 1000;

    [Header("Observations")]
    [SerializeField] private float distanceNormalizer = 10f;
    [SerializeField] private float velocityNormalizer = 2f;

    [Header("Training Safety")]
    [SerializeField] private bool requireTrainingMode = true;
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string[] wallNameKeywords = { "wall" };
    [SerializeField] private string[] obstacleNameKeywords = { "obstacle" };

    private float previousDistance;
    private int episodeStep;

    public Transform CurrentTarget => currentTarget;

    public void Configure(PVZTrainingManager manager, Rigidbody rigidbody, Transform spawn)
    {
        trainingManager = manager;
        robotRigidbody = rigidbody != null ? rigidbody : GetComponent<Rigidbody>();
        spawnPoint = spawn;

        ConfigureRigidbody();
    }

    public override void Initialize()
    {
        AutoFindReferences();
        ConfigureRigidbody();
    }

    public override void OnEpisodeBegin()
    {
        AutoFindReferences();
        ConfigureRigidbody();

        if (!CanUseAgent())
        {
            return;
        }

        currentTarget = PickRandomTarget();
        ResetRobotToSpawn();

        previousDistance = GetFlatDistanceToTarget();
        episodeStep = 0;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        Vector3 flatDelta = GetFlatDeltaToTarget();
        float distance = flatDelta.magnitude;
        Vector3 localDirection = distance > 0.001f
            ? transform.InverseTransformDirection(flatDelta.normalized)
            : Vector3.zero;

        sensor.AddObservation(localDirection);
        sensor.AddObservation(NormalizeDistance(distance));
        sensor.AddObservation(transform.InverseTransformDirection(GetVelocity()) / Mathf.Max(velocityNormalizer, 0.001f));
        sensor.AddObservation(transform.forward);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        if (!CanUseAgent())
        {
            return;
        }

        if (currentTarget == null)
        {
            currentTarget = PickRandomTarget();
            previousDistance = GetFlatDistanceToTarget();
        }

        float moveInput = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float turnInput = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        MoveAgent(moveInput, turnInput);
        ApplyNavigationRewards();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = GetHeuristicMoveInput();
        continuousActions[1] = GetHeuristicTurnInput();
    }

    private void MoveAgent(float moveInput, float turnInput)
    {
        float deltaTime = Time.fixedDeltaTime;

        Quaternion rotationDelta = Quaternion.Euler(0f, turnInput * rotationSpeed * deltaTime, 0f);
        Quaternion nextRotation = robotRigidbody.rotation * rotationDelta;
        Vector3 nextPosition = robotRigidbody.position + nextRotation * Vector3.forward * (moveInput * moveSpeed * deltaTime);
        nextPosition.y = GetCurrentY();

        robotRigidbody.MoveRotation(nextRotation);
        robotRigidbody.MovePosition(nextPosition);
    }

    private void ApplyNavigationRewards()
    {
        episodeStep++;

        AddReward(stepPenalty);

        float distance = GetFlatDistanceToTarget();
        float progress = previousDistance - distance;
        AddReward(progress * progressRewardScale);
        previousDistance = distance;

        if (distance <= targetRadius)
        {
            AddReward(successReward);
            EndEpisode();
            return;
        }

        if (maxEpisodeSteps > 0 && episodeStep >= maxEpisodeSteps)
        {
            EndEpisode();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Create the Wall and Obstacle tags manually in Unity if they are missing.
        // Name keywords are a fallback for newly added scene objects that are not tagged yet.
        if (IsTrainingCollision(collision.gameObject))
        {
            AddReward(collisionPenalty);
            EndEpisode();
        }
    }

    private bool IsTrainingCollision(GameObject other)
    {
        if (other == null)
        {
            return false;
        }

        if (MatchesTag(other, wallTag) || MatchesTag(other, obstacleTag))
        {
            return true;
        }

        return TransformNameContains(other.transform, wallNameKeywords) ||
            TransformNameContains(other.transform, obstacleNameKeywords);
    }

    private bool MatchesTag(GameObject other, string expectedTag)
    {
        return !string.IsNullOrEmpty(expectedTag) && other.tag == expectedTag;
    }

    private bool TransformNameContains(Transform start, string[] keywords)
    {
        if (start == null || keywords == null)
        {
            return false;
        }

        Transform current = start;

        while (current != null)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                string keyword = keywords[i];

                if (!string.IsNullOrEmpty(keyword) &&
                    current.name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            current = current.parent;
        }

        return false;
    }

    private Transform PickRandomTarget()
    {
        if (trainingManager == null || trainingManager.TrainingTargets == null || trainingManager.TrainingTargets.Length == 0)
        {
            Debug.LogWarning("PVZNavigationAgent: no training targets assigned.");
            return null;
        }

        Transform[] targets = trainingManager.TrainingTargets;
        return targets[UnityEngine.Random.Range(0, targets.Length)];
    }

    private void ResetRobotToSpawn()
    {
        Transform resolvedSpawnPoint = spawnPoint != null ? spawnPoint : trainingManager?.SpawnPoint;

        if (resolvedSpawnPoint == null)
        {
            Debug.LogWarning("PVZNavigationAgent: spawn point is not assigned.");
            return;
        }

        Vector3 spawnPosition = resolvedSpawnPoint.position;
        Quaternion spawnRotation = resolvedSpawnPoint.rotation;

        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        robotRigidbody.position = spawnPosition;
        robotRigidbody.rotation = spawnRotation;
        ResetVelocity();
    }

    private Vector3 GetFlatDeltaToTarget()
    {
        if (currentTarget == null)
        {
            return Vector3.zero;
        }

        Vector3 delta = currentTarget.position - transform.position;
        delta.y = 0f;
        return delta;
    }

    private float GetFlatDistanceToTarget()
    {
        return GetFlatDeltaToTarget().magnitude;
    }

    private float NormalizeDistance(float distance)
    {
        return distance / Mathf.Max(distanceNormalizer, 0.001f);
    }

    private float GetCurrentY()
    {
        Transform resolvedSpawnPoint = spawnPoint != null ? spawnPoint : trainingManager?.SpawnPoint;
        return resolvedSpawnPoint != null ? resolvedSpawnPoint.position.y : robotRigidbody.position.y;
    }

    private bool CanUseAgent()
    {
        if (!requireTrainingMode)
        {
            return true;
        }

        return trainingManager != null && trainingManager.IsTrainingMode;
    }

    private void AutoFindReferences()
    {
        if (trainingManager == null)
        {
            trainingManager = FindFirstObjectByType<PVZTrainingManager>();
        }

        if (robotRigidbody == null)
        {
            robotRigidbody = GetComponent<Rigidbody>();
        }

        if (spawnPoint == null && trainingManager != null)
        {
            spawnPoint = trainingManager.SpawnPoint;
        }
    }

    private void ConfigureRigidbody()
    {
        if (robotRigidbody == null)
        {
            return;
        }

        robotRigidbody.useGravity = false;
        robotRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        robotRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        robotRigidbody.constraints = RigidbodyConstraints.FreezePositionY |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;
    }

    private void ResetVelocity()
    {
        if (robotRigidbody == null || robotRigidbody.isKinematic)
        {
            return;
        }

#if UNITY_6000_0_OR_NEWER
        robotRigidbody.linearVelocity = Vector3.zero;
#else
        robotRigidbody.velocity = Vector3.zero;
#endif
        robotRigidbody.angularVelocity = Vector3.zero;
    }

    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return robotRigidbody != null ? robotRigidbody.linearVelocity : Vector3.zero;
#else
        return robotRigidbody != null ? robotRigidbody.velocity : Vector3.zero;
#endif
    }

    private float GetHeuristicMoveInput()
    {
        float input = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                input += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                input -= 1f;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            input += 1f;
        }

        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            input -= 1f;
        }
#endif

        return Mathf.Clamp(input, -1f, 1f);
    }

    private float GetHeuristicTurnInput()
    {
        float input = 0f;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                input -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                input += 1f;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            input -= 1f;
        }

        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            input += 1f;
        }
#endif

        return Mathf.Clamp(input, -1f, 1f);
    }
}
