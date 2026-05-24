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
