using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenCats.Interactable;

namespace HiddenCats.Core
{
    /// <summary>
    /// Scene-level manager for hidden objects and collectibles.
    /// Handles registration, progress tracking, completion checking, and event notifications.
    /// Each scene should have one instance of this component.
    /// </summary>
    [AddComponentMenu("Hidden Cats/Core/Hidden Object Manager")]
    public class HiddenObjectManager : MonoBehaviour
    {
        [Header("Scene Configuration")]
        [Tooltip("Scene name for this manager (e.g., SceneName.RoomWnd)")]
        [SerializeField] private string sceneName;

        [Header("Registration Mode")]
        [Tooltip("Auto-register: Automatically scan and register all interactables in the scene\n" +
                 "Manual: Manually assign interactables in Inspector")]
        [SerializeField] private RegistrationMode registrationMode = RegistrationMode.Auto;

        [Header("Manual Registration (Only used when Registration Mode is Manual)")]
        [Tooltip("Manually assigned NormalCat interactables")]
        [SerializeField] private List<NormalCatInteractable> manualNormalCats = new List<NormalCatInteractable>();
        
        [Tooltip("Manually assigned HiddenCat interactables")]
        [SerializeField] private List<HiddenCatInteractable> manualHiddenCats = new List<HiddenCatInteractable>();
        
        [Tooltip("Manually assigned Fish interactables")]
        [SerializeField] private List<FishInteractable> manualFish = new List<FishInteractable>();
        
        [Tooltip("Manually assigned Firework interactables")]
        [SerializeField] private List<FireworkInteractable> manualFireworks = new List<FireworkInteractable>();

        [Header("Completion Configuration")]
        [Tooltip("Types of collectibles required for level completion (empty = all types)")]
        [SerializeField] private List<CollectibleType> requiredTypesForCompletion = new List<CollectibleType>();

        [Header("Debug")]
        [Tooltip("Enable debug logging")]
        [SerializeField] private bool enableDebugLog = false;

        /// <summary>
        /// Registration mode for interactables.
        /// </summary>
        public enum RegistrationMode
        {
            Auto,    // Automatically scan and register all interactables
            Manual   // Manually assign interactables in Inspector
        }

        // Registered interactables (by type)
        private Dictionary<CollectibleType, List<MonoBehaviour>> _registeredInteractables = 
            new Dictionary<CollectibleType, List<MonoBehaviour>>();

        // Maximum counts (cached after registration)
        private Dictionary<CollectibleType, int> _maxCounts = new Dictionary<CollectibleType, int>();

        // Current progress (updated from CollectionService)
        private Dictionary<CollectibleType, int> _currentCounts = new Dictionary<CollectibleType, int>();

        // Completion state
        private bool _isLevelComplete = false;

        // Events
        /// <summary>
        /// Invoked when an objective is found (collected).
        /// Parameters: sceneName, collectibleType, currentCount, maxCount
        /// </summary>
        public event Action<string, CollectibleType, int, int> OnObjectiveFound;

        /// <summary>
        /// Invoked when the level is completed (all required objectives found).
        /// Parameter: sceneName
        /// </summary>
        public event Action<string> OnLevelComplete;

        /// <summary>
        /// Invoked when progress is updated.
        /// Parameters: sceneName, collectibleType, currentCount, maxCount
        /// </summary>
        public event Action<string, CollectibleType, int, int> OnProgressUpdated;

        private void Awake()
        {
            // Validate scene name
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError($"[HiddenObjectManager] Scene name is not set on {gameObject.name}");
                enabled = false;
                return;
            }

            // Initialize dictionaries
            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                _registeredInteractables[type] = new List<MonoBehaviour>();
                _maxCounts[type] = 0;
                _currentCounts[type] = 0;
            }
        }

        private void Start()
        {
            // Register interactables
            if (registrationMode == RegistrationMode.Auto)
            {
                RegisterInteractablesAuto();
            }
            else
            {
                RegisterInteractablesManual();
            }

            // Subscribe to CollectionService events
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnSceneCountChanged += HandleSceneCountChanged;
            }
            else
            {
                Debug.LogWarning($"[HiddenObjectManager] CollectionService.Instance is null. Will retry in Update().");
            }

            // Load current progress from CollectionService
            LoadCurrentProgress();

            // Check initial completion state
            CheckLevelCompletion();
        }

        private void Update()
        {
            // Retry subscribing if CollectionService wasn't ready in Start
            if (CollectionService.Instance != null && !IsSubscribed())
            {
                CollectionService.Instance.OnSceneCountChanged += HandleSceneCountChanged;
                LoadCurrentProgress();
                CheckLevelCompletion();
            }
        }

        private void OnDestroy()
        {
            if (CollectionService.Instance != null)
            {
                CollectionService.Instance.OnSceneCountChanged -= HandleSceneCountChanged;
            }
        }

        /// <summary>
        /// Automatically scan and register all interactables in the scene.
        /// </summary>
        private void RegisterInteractablesAuto()
        {
            if (enableDebugLog)
            {
                Debug.Log($"[HiddenObjectManager] Auto-registering interactables for scene: {sceneName}");
            }

            // Register NormalCats
            NormalCatInteractable[] normalCats = FindObjectsOfType<NormalCatInteractable>(true);
            foreach (var cat in normalCats)
            {
                if (cat != null && cat.SceneName == sceneName)
                {
                    _registeredInteractables[CollectibleType.NormalCat].Add(cat);
                }
            }
            _maxCounts[CollectibleType.NormalCat] = _registeredInteractables[CollectibleType.NormalCat].Count;

            // Register HiddenCats
            HiddenCatInteractable[] hiddenCats = FindObjectsOfType<HiddenCatInteractable>(true);
            foreach (var cat in hiddenCats)
            {
                if (cat != null && cat.SceneName == sceneName)
                {
                    _registeredInteractables[CollectibleType.HiddenCat].Add(cat);
                }
            }
            _maxCounts[CollectibleType.HiddenCat] = _registeredInteractables[CollectibleType.HiddenCat].Count;

            // Register Fish (only those initially active in prefab)
            FishInteractable[] fish = FindObjectsOfType<FishInteractable>(true);
            foreach (var f in fish)
            {
                if (f != null && f.SceneName == sceneName && f.InitiallyActiveInPrefab)
                {
                    _registeredInteractables[CollectibleType.Fish].Add(f);
                }
            }
            _maxCounts[CollectibleType.Fish] = _registeredInteractables[CollectibleType.Fish].Count;

            // Register Fireworks (only those initially active in prefab)
            FireworkInteractable[] fireworks = FindObjectsOfType<FireworkInteractable>(true);
            foreach (var firework in fireworks)
            {
                if (firework != null && firework.SceneName == sceneName && firework.InitiallyActiveInPrefab)
                {
                    _registeredInteractables[CollectibleType.Firework].Add(firework);
                }
            }
            _maxCounts[CollectibleType.Firework] = _registeredInteractables[CollectibleType.Firework].Count;

            // Log registration results
            if (enableDebugLog)
            {
                Debug.Log($"[HiddenObjectManager] Registration complete for {sceneName}:\n" +
                         $"  NormalCats: {_maxCounts[CollectibleType.NormalCat]}\n" +
                         $"  HiddenCats: {_maxCounts[CollectibleType.HiddenCat]}\n" +
                         $"  Fish: {_maxCounts[CollectibleType.Fish]}\n" +
                         $"  Fireworks: {_maxCounts[CollectibleType.Firework]}");
            }
        }

        /// <summary>
        /// Register manually assigned interactables.
        /// </summary>
        private void RegisterInteractablesManual()
        {
            if (enableDebugLog)
            {
                Debug.Log($"[HiddenObjectManager] Manual-registering interactables for scene: {sceneName}");
            }

            // Register NormalCats
            foreach (var cat in manualNormalCats)
            {
                if (cat != null && cat.SceneName == sceneName)
                {
                    _registeredInteractables[CollectibleType.NormalCat].Add(cat);
                }
            }
            _maxCounts[CollectibleType.NormalCat] = _registeredInteractables[CollectibleType.NormalCat].Count;

            // Register HiddenCats
            foreach (var cat in manualHiddenCats)
            {
                if (cat != null && cat.SceneName == sceneName)
                {
                    _registeredInteractables[CollectibleType.HiddenCat].Add(cat);
                }
            }
            _maxCounts[CollectibleType.HiddenCat] = _registeredInteractables[CollectibleType.HiddenCat].Count;

            // Register Fish
            foreach (var f in manualFish)
            {
                if (f != null && f.SceneName == sceneName)
                {
                    _registeredInteractables[CollectibleType.Fish].Add(f);
                }
            }
            _maxCounts[CollectibleType.Fish] = _registeredInteractables[CollectibleType.Fish].Count;

            // Register Fireworks
            foreach (var firework in manualFireworks)
            {
                if (firework != null && firework.SceneName == sceneName)
                {
                    _registeredInteractables[CollectibleType.Firework].Add(firework);
                }
            }
            _maxCounts[CollectibleType.Firework] = _registeredInteractables[CollectibleType.Firework].Count;

            // Log registration results
            if (enableDebugLog)
            {
                Debug.Log($"[HiddenObjectManager] Manual registration complete for {sceneName}:\n" +
                         $"  NormalCats: {_maxCounts[CollectibleType.NormalCat]}\n" +
                         $"  HiddenCats: {_maxCounts[CollectibleType.HiddenCat]}\n" +
                         $"  Fish: {_maxCounts[CollectibleType.Fish]}\n" +
                         $"  Fireworks: {_maxCounts[CollectibleType.Firework]}");
            }
        }

        /// <summary>
        /// Handle scene count changed event from CollectionService.
        /// </summary>
        private void HandleSceneCountChanged(string changedSceneName, CollectibleType type, int newCount)
        {
            if (changedSceneName != sceneName)
            {
                return; // Not for this scene
            }

            int oldCount = _currentCounts[type];
            _currentCounts[type] = newCount;

            // Notify progress update
            OnProgressUpdated?.Invoke(sceneName, type, newCount, _maxCounts[type]);

            // If count increased, notify objective found
            if (newCount > oldCount)
            {
                OnObjectiveFound?.Invoke(sceneName, type, newCount, _maxCounts[type]);

                if (enableDebugLog)
                {
                    Debug.Log($"[HiddenObjectManager] Objective found: {type} in {sceneName} ({newCount}/{_maxCounts[type]})");
                }
            }

            // Check if level is complete
            CheckLevelCompletion();
        }

        /// <summary>
        /// Load current progress from CollectionService.
        /// </summary>
        private void LoadCurrentProgress()
        {
            if (CollectionService.Instance == null)
            {
                return;
            }

            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                int count = CollectionService.Instance.GetSceneCount(sceneName, type);
                _currentCounts[type] = count;
            }

            if (enableDebugLog)
            {
                Debug.Log($"[HiddenObjectManager] Loaded current progress for {sceneName}");
            }
        }

        /// <summary>
        /// Check if the level is complete (all required objectives found).
        /// </summary>
        private void CheckLevelCompletion()
        {
            if (_isLevelComplete)
            {
                return; // Already completed
            }

            // Determine which types to check
            List<CollectibleType> typesToCheck = requiredTypesForCompletion.Count > 0
                ? requiredTypesForCompletion
                : new List<CollectibleType>(Enum.GetValues(typeof(CollectibleType)).Cast<CollectibleType>());

            // Check if all required types are complete
            bool allComplete = true;
            foreach (CollectibleType type in typesToCheck)
            {
                // Skip types that don't exist in this scene (maxCount == 0)
                if (_maxCounts[type] == 0)
                {
                    continue;
                }

                // Check if current count equals max count
                if (_currentCounts[type] < _maxCounts[type])
                {
                    allComplete = false;
                    break;
                }
            }

            if (allComplete && !_isLevelComplete)
            {
                _isLevelComplete = true;
                OnLevelComplete?.Invoke(sceneName);

                if (enableDebugLog)
                {
                    Debug.Log($"[HiddenObjectManager] Level completed: {sceneName}");
                }
            }
        }

        /// <summary>
        /// Check if we're subscribed to CollectionService events.
        /// </summary>
        private bool IsSubscribed()
        {
            // This is a simple check - in a more complex scenario, we might track subscription state
            return CollectionService.Instance != null;
        }

        // Public API

        /// <summary>
        /// Get the maximum count for a specific collectible type in this scene.
        /// </summary>
        public int GetMaxCount(CollectibleType type)
        {
            return _maxCounts.GetValueOrDefault(type, 0);
        }

        /// <summary>
        /// Get the current count for a specific collectible type in this scene.
        /// </summary>
        public int GetCurrentCount(CollectibleType type)
        {
            return _currentCounts.GetValueOrDefault(type, 0);
        }

        /// <summary>
        /// Get progress for a specific collectible type (current / max).
        /// Returns a tuple (current, max).
        /// </summary>
        public (int current, int max) GetProgress(CollectibleType type)
        {
            return (_currentCounts.GetValueOrDefault(type, 0), _maxCounts.GetValueOrDefault(type, 0));
        }

        /// <summary>
        /// Get all progress data for this scene.
        /// Returns a dictionary mapping CollectibleType to (current, max) tuple.
        /// </summary>
        public Dictionary<CollectibleType, (int current, int max)> GetAllProgress()
        {
            var result = new Dictionary<CollectibleType, (int current, int max)>();
            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                int max = _maxCounts.GetValueOrDefault(type, 0);
                if (max > 0) // Only include types that exist in this scene
                {
                    int current = _currentCounts.GetValueOrDefault(type, 0);
                    result[type] = (current, max);
                }
            }
            return result;
        }

        /// <summary>
        /// Check if the level is complete.
        /// </summary>
        public bool IsLevelComplete => _isLevelComplete;

        /// <summary>
        /// Get the scene name for this manager.
        /// </summary>
        public string SceneName => sceneName;

        /// <summary>
        /// Get all registered interactables for a specific type.
        /// </summary>
        public List<MonoBehaviour> GetRegisteredInteractables(CollectibleType type)
        {
            return new List<MonoBehaviour>(_registeredInteractables.GetValueOrDefault(type, new List<MonoBehaviour>()));
        }

        /// <summary>
        /// Force refresh registration (useful for editor or runtime changes).
        /// </summary>
        public void RefreshRegistration()
        {
            // Clear existing registrations
            foreach (var list in _registeredInteractables.Values)
            {
                list.Clear();
            }
            foreach (CollectibleType type in Enum.GetValues(typeof(CollectibleType)))
            {
                _maxCounts[type] = 0;
            }

            // Re-register
            if (registrationMode == RegistrationMode.Auto)
            {
                RegisterInteractablesAuto();
            }
            else
            {
                RegisterInteractablesManual();
            }

            // Reload progress
            LoadCurrentProgress();

            // Re-check completion
            _isLevelComplete = false;
            CheckLevelCompletion();
        }

        #if UNITY_EDITOR
        /// <summary>
        /// Editor-only: Validate configuration in Inspector.
        /// </summary>
        private void OnValidate()
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                sceneName = HiddenCats.Core.SceneName.RoomWnd;
            }

            // Remove null entries from manual lists
            if (manualNormalCats != null)
            {
                manualNormalCats.RemoveAll(cat => cat == null);
            }
            if (manualHiddenCats != null)
            {
                manualHiddenCats.RemoveAll(cat => cat == null);
            }
            if (manualFish != null)
            {
                manualFish.RemoveAll(f => f == null);
            }
            if (manualFireworks != null)
            {
                manualFireworks.RemoveAll(f => f == null);
            }
        }
        #endif
    }
}
