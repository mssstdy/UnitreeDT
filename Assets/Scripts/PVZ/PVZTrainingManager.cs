using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

public class PVZTrainingManager : MonoBehaviour
{
    [Header("Mode")]
    [SerializeField] private bool trainingMode;
    [SerializeField] private bool applyModeOnStart = true;

    [Header("Demo Systems")]
    [SerializeField] private TaskPlanner taskPlanner;
    [SerializeField] private RobotController robotController;
    [SerializeField] private UIManager uiManager;
    [SerializeField] private ScanZone[] scanZones;

    [Header("Training Points")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform[] trainingTargets;

    [Header("Mode Switching")]
    [SerializeField] private bool disableTaskPlannerInTraining = true;
    [SerializeField] private bool disableRobotControllerInTraining = true;
    [SerializeField] private bool disableScanZonesInTraining = true;
    [SerializeField] private bool keepUIEnabledInTraining = true;

    [Header("Optional Future Training Objects")]
    [SerializeField] private MonoBehaviour[] demoModeBehaviours;
    [SerializeField] private MonoBehaviour[] trainingModeBehaviours;
    [SerializeField] private Rigidbody robotRigidbody;
    [SerializeField] private bool makeRobotRigidbodyKinematicInDemo = true;

    [Header("ML-Agents Navigation")]
    [SerializeField] private bool setupNavigationComponents = true;
    [SerializeField] private string behaviorName = "PVZNavigation";
    [SerializeField] private BehaviorType behaviorType = BehaviorType.Default;
    [SerializeField] private int decisionPeriod = 5;
    [SerializeField] private bool takeActionsBetweenDecisions = true;
    [SerializeField] private PVZNavigationAgent navigationAgent;
    [SerializeField] private BehaviorParameters behaviorParameters;
    [SerializeField] private DecisionRequester decisionRequester;

    [Header("Training Collisions")]
    [SerializeField] private bool autoTagTrainingColliders = true;
    [SerializeField] private string wallTag = "Wall";
    [SerializeField] private string obstacleTag = "Obstacle";
    [SerializeField] private string[] wallNameKeywords = { "wall" };
    [SerializeField] private string[] obstacleNameKeywords = { "obstacle", "stoika", "counter", "platform", "table" };

    private bool appliedTrainingMode;
    private bool modeWasApplied;
    private bool initialStatesCaptured;
    private bool taskPlannerInitialEnabled;
    private bool robotControllerInitialEnabled;
    private bool uiManagerInitialEnabled;
    private bool[] scanZoneInitialEnabled;

    public bool IsTrainingMode => trainingMode;
    public Transform SpawnPoint => spawnPoint;
    public Transform[] TrainingTargets => trainingTargets;
    public TaskPlanner TaskPlanner => taskPlanner;
    public RobotController RobotController => robotController;
    public UIManager UIManager => uiManager;

    private void Awake()
    {
        AutoFindMissingReferences();
        CaptureInitialEnabledStates();
    }

    private void Start()
    {
        if (applyModeOnStart)
        {
            ApplyMode();
            return;
        }

        appliedTrainingMode = trainingMode;
        modeWasApplied = true;
    }

    private void Update()
    {
        if (appliedTrainingMode != trainingMode)
        {
            ApplyMode();
        }
    }

    public void SetTrainingMode(bool enabled)
    {
        if (trainingMode == enabled && modeWasApplied)
        {
            return;
        }

        trainingMode = enabled;
        ApplyMode();
    }

    [ContextMenu("Apply Current Mode")]
    public void ApplyMode()
    {
        AutoFindMissingReferences();
        CaptureInitialEnabledStatesIfNeeded();

        if (trainingMode)
        {
            taskPlanner?.CancelActiveTaskForTraining();
            robotController?.CancelMotionForTraining();
        }

        ApplyDemoSystemState();
        ApplyExtraBehaviourState(demoModeBehaviours, !trainingMode);
        ApplyExtraBehaviourState(trainingModeBehaviours, trainingMode);
        ApplyRobotRigidbodyState();
        ApplyNavigationAgentState();

        appliedTrainingMode = trainingMode;
        modeWasApplied = true;
    }

    private void ApplyDemoSystemState()
    {
        if (taskPlanner != null && disableTaskPlannerInTraining)
        {
            taskPlanner.enabled = trainingMode ? false : taskPlannerInitialEnabled;
        }

        if (robotController != null && disableRobotControllerInTraining)
        {
            robotController.enabled = trainingMode ? false : robotControllerInitialEnabled;
        }

        if (uiManager != null && !keepUIEnabledInTraining)
        {
            uiManager.enabled = trainingMode ? false : uiManagerInitialEnabled;
        }

        if (scanZones != null && disableScanZonesInTraining)
        {
            for (int i = 0; i < scanZones.Length; i++)
            {
                ScanZone scanZone = scanZones[i];

                if (scanZone != null)
                {
                    bool initialEnabled = scanZoneInitialEnabled != null &&
                        i < scanZoneInitialEnabled.Length &&
                        scanZoneInitialEnabled[i];

                    scanZone.enabled = trainingMode ? false : initialEnabled;
                }
            }
        }
    }

    private void ApplyExtraBehaviourState(MonoBehaviour[] behaviours, bool enabled)
    {
        if (behaviours == null)
        {
            return;
        }

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
    }

    private void ApplyRobotRigidbodyState()
    {
        if (robotRigidbody == null)
        {
            return;
        }

        if (trainingMode)
        {
            robotRigidbody.isKinematic = false;
            robotRigidbody.useGravity = false;
            ResetRobotVelocity();
            return;
        }

        if (makeRobotRigidbodyKinematicInDemo)
        {
            ResetRobotVelocity();
            robotRigidbody.isKinematic = true;
            robotRigidbody.useGravity = false;
        }
    }

    private void ResetRobotVelocity()
    {
        if (robotRigidbody == null)
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

    private void AutoFindMissingReferences()
    {
        if (taskPlanner == null)
        {
            taskPlanner = FindFirstObjectByType<TaskPlanner>(FindObjectsInactive.Include);
        }

        if (robotController == null)
        {
            robotController = FindFirstObjectByType<RobotController>(FindObjectsInactive.Include);
        }

        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManager>(FindObjectsInactive.Include);
        }

        if (scanZones == null || scanZones.Length == 0)
        {
            scanZones = FindObjectsByType<ScanZone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        if (robotRigidbody == null && robotController != null)
        {
            robotRigidbody = robotController.GetComponent<Rigidbody>();
        }

        if (setupNavigationComponents)
        {
            EnsureNavigationComponents();
        }

        if (autoTagTrainingColliders)
        {
            EnsureTrainingCollisionTags();
        }
    }

    private void EnsureNavigationComponents()
    {
        if (robotController == null)
        {
            return;
        }

        GameObject robotObject = robotController.gameObject;

        if (robotRigidbody == null)
        {
            robotRigidbody = robotObject.GetComponent<Rigidbody>();

            if (robotRigidbody == null)
            {
                robotRigidbody = robotObject.AddComponent<Rigidbody>();
            }
        }

        if (behaviorParameters == null)
        {
            behaviorParameters = robotObject.GetComponent<BehaviorParameters>();

            if (behaviorParameters == null)
            {
                behaviorParameters = robotObject.AddComponent<BehaviorParameters>();
            }
        }

        ConfigureNavigationComponents();

        if (navigationAgent == null)
        {
            navigationAgent = robotObject.GetComponent<PVZNavigationAgent>();

            if (navigationAgent == null)
            {
                navigationAgent = robotObject.AddComponent<PVZNavigationAgent>();
            }
        }

        if (decisionRequester == null)
        {
            decisionRequester = robotObject.GetComponent<DecisionRequester>();

            if (decisionRequester == null)
            {
                decisionRequester = robotObject.AddComponent<DecisionRequester>();
            }
        }

        ConfigureNavigationComponents();
    }

    private void ConfigureNavigationComponents()
    {
        if (robotRigidbody != null)
        {
            robotRigidbody.useGravity = false;
            robotRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            robotRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            robotRigidbody.constraints = RigidbodyConstraints.FreezePositionY |
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }

        if (behaviorParameters != null)
        {
            behaviorParameters.BehaviorName = behaviorName;
            behaviorParameters.BehaviorType = behaviorType;
            behaviorParameters.BrainParameters.VectorObservationSize = 10;
            behaviorParameters.BrainParameters.NumStackedVectorObservations = 1;
            behaviorParameters.BrainParameters.ActionSpec = ActionSpec.MakeContinuous(2);
        }

        if (decisionRequester != null)
        {
            decisionRequester.DecisionPeriod = Mathf.Max(1, decisionPeriod);
            decisionRequester.DecisionStep = 0;
            decisionRequester.TakeActionsBetweenDecisions = takeActionsBetweenDecisions;
        }

        if (navigationAgent != null)
        {
            navigationAgent.Configure(this, robotRigidbody, spawnPoint);
        }
    }

    private void ApplyNavigationAgentState()
    {
        if (!setupNavigationComponents)
        {
            return;
        }

        if (navigationAgent != null)
        {
            navigationAgent.enabled = trainingMode;
        }

        if (decisionRequester != null)
        {
            decisionRequester.enabled = trainingMode;
        }
    }

    private void EnsureTrainingCollisionTags()
    {
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Collider collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            GameObject colliderObject = collider.gameObject;

            if (TransformNameContains(colliderObject.transform, wallNameKeywords))
            {
                TrySetTag(colliderObject, wallTag);
            }
            else if (TransformNameContains(colliderObject.transform, obstacleNameKeywords))
            {
                TrySetTag(colliderObject, obstacleTag);
            }
        }
    }

    private void TrySetTag(GameObject targetObject, string targetTag)
    {
        if (targetObject == null || string.IsNullOrEmpty(targetTag) || targetObject.tag == targetTag)
        {
            return;
        }

        try
        {
            targetObject.tag = targetTag;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"PVZTrainingManager: tag '{targetTag}' is missing in TagManager.");
        }
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
            foreach (string keyword in keywords)
            {
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

    private void CaptureInitialEnabledStatesIfNeeded()
    {
        if (!initialStatesCaptured ||
            (scanZones != null && scanZoneInitialEnabled != null && scanZones.Length != scanZoneInitialEnabled.Length))
        {
            CaptureInitialEnabledStates();
        }
    }

    private void CaptureInitialEnabledStates()
    {
        taskPlannerInitialEnabled = taskPlanner == null || taskPlanner.enabled;
        robotControllerInitialEnabled = robotController == null || robotController.enabled;
        uiManagerInitialEnabled = uiManager == null || uiManager.enabled;

        if (scanZones == null)
        {
            scanZoneInitialEnabled = new bool[0];
        }
        else
        {
            scanZoneInitialEnabled = new bool[scanZones.Length];

            for (int i = 0; i < scanZones.Length; i++)
            {
                scanZoneInitialEnabled[i] = scanZones[i] == null || scanZones[i].enabled;
            }
        }

        initialStatesCaptured = true;
    }
}
