using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace TouchEffectSystem
{
    /// <summary>
    /// Manages animated text effects on UI Canvas elements that spawn at touch/click positions.
    /// Supports multiple animation types, text pooling, and customizable appearance settings.
    /// Part of the Touch Effect System - Mini version with basic OneTouch functionality.
    /// </summary>
    public class TouchTextUI : MonoBehaviour
    {
        /// <summary>
        /// Defines the direction and pattern of text appearance animation.
        /// </summary>
        public enum AppearanceAnimationType
        {
            /// <summary>No movement animation - text appears and fades in place.</summary>
            None,
            /// <summary>Text moves outward in a random arc pattern (configurable angle range).</summary>
            RadialArc,
            /// <summary>Text moves downward.</summary>
            Down,
            /// <summary>Text moves to the left.</summary>
            Left,
            /// <summary>Text moves to the right.</summary>
            Right,
            /// <summary>Text moves upward.</summary>
            Up
        }

        [Header("References")]
        /// <summary>
        /// Target canvas where text effects will be rendered.
        /// Auto-detected from parent if not assigned.
        /// </summary>
        public Canvas targetCanvas;

        /// <summary>
        /// Unified input detection system for handling mouse and touch inputs.
        /// </summary>
        [SerializeField] private InputController inputController;

        /// <summary>
        /// Prefab containing TextMeshProUGUI component for text effects.
        /// Created automatically if not assigned.
        /// </summary>
        [SerializeField] private GameObject textPrefab;

        /// <summary>
        /// Default size for text RectTransform if creating prefab at runtime.
        /// </summary>
        private Vector2 textBasicSizeDelta = new Vector2(100, 100);

        /// <summary>
        /// Text alignment setting for runtime-created prefabs.
        /// </summary>
        private TextAlignmentOptions textAlignment = TextAlignmentOptions.Center;

        [Header("Settings")]
        /// <summary>
        /// Default font asset to apply to spawned text effects.
        /// </summary>
        [SerializeField] private TMP_FontAsset fontAsset;

        /// <summary>
        /// Default font style (Normal, Bold, Italic, etc.) for text effects.
        /// </summary>
        [SerializeField] private FontStyles fontStyle = FontStyles.Normal;

        /// <summary>
        /// Font size in pixels for spawned text.
        /// </summary>
        [SerializeField] private int fontSize = 48;

        /// <summary>
        /// Sprite asset for TextMeshPro rich text sprite support.
        /// </summary>
        [SerializeField] private TMP_SpriteAsset mainSpriteAsset;

        /// <summary>
        /// Pool of text strings to randomly display on spawn.
        /// If multiple strings provided, one is randomly selected per spawn.
        /// </summary>
        [SerializeField] private List<string> textStrings = new List<string> { "+1", "+10", "BOOM" };

        /// <summary>
        /// Pool of colors to randomly apply to text on spawn.
        /// If multiple colors provided, one is randomly selected per spawn.
        /// </summary>
        [SerializeField] private List<Color> textColors = new List<Color> { Color.white, Color.yellow };

        /// <summary>
        /// Animation type determining the direction and pattern of text movement.
        /// </summary>
        [SerializeField] private AppearanceAnimationType appearanceAnimation = AppearanceAnimationType.Up;

        /// <summary>
        /// Duration in seconds that text remains visible before fading out.
        /// </summary>
        [SerializeField] private float textLifetime = 1f;

        /// <summary>
        /// Minimum distance in pixels from spawn point where text animation starts.
        /// </summary>
        [SerializeField] private float minMoveDistance = 50f;

        /// <summary>
        /// Maximum distance in pixels from spawn point where text animation ends.
        /// </summary>
        [SerializeField] private float maxMoveDistance = 150f;

        /// <summary>
        /// Cached child count for hierarchy change detection.
        /// Used to optimize SetAsLastSibling calls.
        /// </summary>
        private int lastChildCount = -1;

        /// <summary>
        /// List of active touch IDs for multi-touch support tracking.
        /// Currently unused in mini version but reserved for full version compatibility.
        /// </summary>
        private List<int> activeTouchIDsList = new List<int>();

        /// <summary>
        /// Pool of pre-instantiated text GameObjects for performance optimization.
        /// </summary>
        private List<GameObject> textPool = new List<GameObject>();

        /// <summary>
        /// Cached TextMeshProUGUI components from pooled objects to avoid GetComponent calls.
        /// </summary>
        private List<TextMeshProUGUI> cachedTexts = new List<TextMeshProUGUI>();

        /// <summary>
        /// Cached RectTransform components from pooled objects to avoid GetComponent calls.
        /// </summary>
        private List<RectTransform> cachedRectTransforms = new List<RectTransform>();

        /// <summary>
        /// Tracks whether each pooled text object is currently active/visible.
        /// </summary>
        private List<bool> textActiveStates = new List<bool>();

        /// <summary>
        /// Starting positions for each pooled text's animation in local canvas coordinates.
        /// </summary>
        private List<Vector2> textStartPositions = new List<Vector2>();

        /// <summary>
        /// Target positions for each pooled text (middle of animation path).
        /// </summary>
        private List<Vector2> textTargetPositions = new List<Vector2>();

        /// <summary>
        /// End positions for each pooled text's animation in local canvas coordinates.
        /// </summary>
        private List<Vector2> textEndPositions = new List<Vector2>();

        /// <summary>
        /// Timestamps when each pooled text was activated, used to calculate animation age.
        /// </summary>
        private List<float> textStartTimes = new List<float>();

        /// <summary>
        /// Base colors for each pooled text before alpha/fade modifications.
        /// </summary>
        private List<Color32> textBaseColors = new List<Color32>();

        /// <summary>
        /// Base font sizes for each pooled text before scale modifications.
        /// </summary>
        private List<float> textBaseSizes = new List<float>();

        /// <summary>
        /// Rotation angles for each pooled text (currently unused but reserved).
        /// </summary>
        private List<float> textRotations = new List<float>();

        /// <summary>
        /// Initial scale values for each pooled text at spawn time.
        /// </summary>
        private List<float> textInitialScales = new List<float>();

        /// <summary>
        /// Pre-calculated movement vectors (end - start) for each pooled text.
        /// Cached to avoid repeated calculations during animation updates.
        /// </summary>
        private List<Vector2> textMovementVectors = new List<Vector2>();

        /// <summary>
        /// Alpha range (0-1) for each pooled text's fade animation.
        /// </summary>
        private List<float> textAlphaRanges = new List<float>();

        /// <summary>
        /// Scale range for each pooled text's size animation.
        /// </summary>
        private List<float> textScaleRanges = new List<float>();

        /// <summary>
        /// Pre-defined direction vector for downward movement (0, -1).
        /// </summary>
        private Vector2 downDirection = new Vector2(0f, -1f);

        /// <summary>
        /// Pre-defined direction vector for leftward movement (-1, 0).
        /// </summary>
        private Vector2 leftDirection = new Vector2(-1f, 0f);

        /// <summary>
        /// Pre-defined direction vector for rightward movement (1, 0).
        /// </summary>
        private Vector2 rightDirection = new Vector2(1f, 0f);

        /// <summary>
        /// Pre-defined direction vector for upward movement (0, 1).
        /// </summary>
        private Vector2 upDirection = new Vector2(0f, 1f);

        /// <summary>
        /// Temporary color variable to reduce garbage allocation during animation updates.
        /// </summary>
        private Color32 tempColor = Color.white;

        /// <summary>
        /// Temporary Vector2 variable to reduce garbage allocation during calculations.
        /// </summary>
        private Vector2 tempVector2 = Vector2.zero;

        /// <summary>
        /// List of indices for available (inactive) text objects in the pool.
        /// Used for fast allocation of new text effects.
        /// </summary>
        private List<int> availableTextIndices = new List<int>();

        /// <summary>
        /// List of indices for currently active text objects in the pool.
        /// Used for efficient iteration during animation updates.
        /// </summary>
        private List<int> activeTextIndices = new List<int>();

        /// <summary>
        /// Current count of active text effects, cached to avoid list count checks.
        /// </summary>
        private int activeTextCount = 0;

        /// <summary>
        /// Maximum number of text objects in the pool.
        /// When exceeded, oldest text is recycled.
        /// </summary>
        private int maxTexts = 20;

        /// <summary>
        /// Angular range in degrees for RadialArc animation randomization.
        /// Text spawns within +/- this range from the center angle.
        /// </summary>
        private float radialArcAngleRange = 35f;

        /// <summary>
        /// Center angle in degrees for RadialArc animation (90° = upward).
        /// </summary>
        private float radialArcCenterAngle = 90f;

        /// <summary>
        /// Minimum angle for RadialArc animation (centerAngle - angleRange).
        /// Pre-calculated for performance.
        /// </summary>
        private float radialArcMinAngle;

        /// <summary>
        /// Maximum angle for RadialArc animation (centerAngle + angleRange).
        /// Pre-calculated for performance.
        /// </summary>
        private float radialArcMaxAngle;

        /// <summary>
        /// Pre-calculated inverse of textLifetime (1 / lifetime) for normalization.
        /// Cached to avoid division operations during animation updates.
        /// </summary>
        private float lifetimeInverse;

        /// <summary>
        /// Determines whether text moves from start to end position.
        /// When true, uses smooth interpolation; when false, uses linear interpolation.
        /// </summary>
        private bool useEndPositionsForMovement = true;

        /// <summary>
        /// Normal scale multiplier for text appearance (1.0 = 100%).
        /// </summary>
        private float normalScale = 1f;

        /// <summary>
        /// Initializes references, validates setup, creates text prefab if needed,
        /// and pre-instantiates the object pool for optimal runtime performance.
        /// </summary>
        void Start()
        {
            // Auto-detect canvas if not assigned
            if (targetCanvas == null)
                targetCanvas = GetComponentInParent<Canvas>();

            // Validate required references
            if (textPrefab == null || targetCanvas == null)
            {
                Debug.LogError("[MiniGlow] Setup missing references!");
                enabled = false;
                return;
            }

            // Create default text prefab if none assigned
            if (textPrefab == null)
            {
                GameObject prefab = new GameObject("TextEffect");
                RectTransform rt = prefab.AddComponent<RectTransform>();
                rt.sizeDelta = textBasicSizeDelta;
                TextMeshProUGUI tmp = prefab.AddComponent<TextMeshProUGUI>();
                tmp.alignment = textAlignment;
                tmp.text = "";
                textPrefab = prefab;
                textPrefab.SetActive(false);
            }

            // Auto-detect input controller if not assigned
            if (inputController == null)
                inputController = GetComponentInChildren<InputController>();

            // Pre-calculate animation values for performance
            lifetimeInverse = 1f / textLifetime;

            // Pre-calculate radial arc angle bounds
            radialArcMinAngle = radialArcCenterAngle - radialArcAngleRange;
            radialArcMaxAngle = radialArcCenterAngle + radialArcAngleRange;

            // Pre-instantiate all pooled objects
            InitializeTextPool();
        }

        /// <summary>
        /// Handles rendering order management, input processing, and animation updates.
        /// Called once per frame.
        /// </summary>
        void Update()
        {
            // Ensure text effects render on top of other UI elements when active
            if (activeTextCount > 0)
            {
                int currentChildCount = transform.parent != null ? transform.parent.childCount : 0;
                int siblingIndex = transform.GetSiblingIndex();
                int lastIndex = currentChildCount - 1;

                // Only update hierarchy position if it has changed
                if (siblingIndex != lastIndex)
                {
                    transform.SetAsLastSibling();
                }
                lastChildCount = currentChildCount;
            }

            // Process input and spawn new text effects
            HandleMultiTouchInput();

            // Update all active text animations
            if (activeTextCount > 0)
            {
                UpdateActiveTexts();
            }
        }

        /// <summary>
        /// Pre-instantiates all text objects in the pool to avoid runtime allocation hiccups.
        /// Initializes all associated lists with default values for each pooled object.
        /// </summary>
        private void InitializeTextPool()
        {
            for (int i = 0; i < maxTexts; i++)
            {
                // Instantiate text object from prefab
                GameObject textObj = Instantiate(textPrefab, transform);
                textObj.name = $"Text_{i}";
                textObj.SetActive(false);

                // Ensure TextMeshProUGUI component exists
                TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
                if (tmp == null)
                {
                    tmp = textObj.AddComponent<TextMeshProUGUI>();
                }

                RectTransform rt = textObj.GetComponent<RectTransform>();

                // Apply sprite asset if available
                if (mainSpriteAsset != null && tmp.spriteAsset == null)
                {
                    tmp.spriteAsset = mainSpriteAsset;
                }

                // Add to pools and cache references
                textPool.Add(textObj);
                cachedTexts.Add(tmp);
                cachedRectTransforms.Add(rt);
                textActiveStates.Add(false);

                // Initialize animation data with default values
                textStartPositions.Add(Vector2.zero);
                textTargetPositions.Add(Vector2.zero);
                textEndPositions.Add(Vector2.zero);
                textStartTimes.Add(0f);
                textBaseColors.Add(Color.white);
                textBaseSizes.Add(36f);
                textRotations.Add(0f);
                textInitialScales.Add(1f);

                // Initialize calculation caches
                textMovementVectors.Add(Vector2.zero);
                textAlphaRanges.Add(1f);
                textScaleRanges.Add(1f);

                // Mark as available for use
                availableTextIndices.Add(i);
            }
        }

        /// <summary>
        /// Processes input events from the InputController and spawns text effects on touch/click.
        /// Currently supports single input in mini version.
        /// </summary>
        private void HandleMultiTouchInput()
        {
            if (inputController == null) return; // No input controller available

            // Spawn text at input position when detected
            if (inputController.GetInputDown())
            {
                SpawnText(inputController.GetInputPosition());
            }
        }

        /// <summary>
        /// Spawns a new text effect at the specified screen position.
        /// Retrieves an available text from the pool, configures its appearance and animation,
        /// and activates it. If pool is full, recycles the oldest active text.
        /// </summary>
        /// <param name="screenPosition">Screen position in pixels where text should spawn.</param>
        private void SpawnText(Vector2 screenPosition)
        {
            // Handle pool exhaustion by recycling oldest text
            if (availableTextIndices.Count == 0)
            {
                if (activeTextIndices.Count > 0)
                {
                    // Recycle oldest active text (FIFO queue behavior)
                    int oldestIndex = activeTextIndices[0];
                    DeactivateText(oldestIndex);
                    activeTextIndices.RemoveAt(0);
                }
                else
                {
                    return; // Should never happen, but safety check
                }
            }

            // Get next available text from pool
            int index = availableTextIndices[0];
            availableTextIndices.RemoveAt(0);

            // Convert screen position to local canvas coordinates
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform,
                screenPosition,
                targetCanvas.worldCamera,
                out localPos
            );

            // Set text content (random if multiple strings available)
            bool randomText = textStrings.Count > 1;
            string textContent = randomText ? textStrings[Random.Range(0, textStrings.Count)] : textStrings[0];
            cachedTexts[index].text = textContent;

            // Apply font asset if specified
            if (fontAsset != null)
            {
                cachedTexts[index].font = fontAsset;
            }

            // Apply font style if not normal
            if (fontStyle != FontStyles.Normal)
            {
                cachedTexts[index].fontStyle = fontStyle;
            }

            // Set color (random if multiple colors available)
            bool randomColor = textColors.Count > 1;
            Color32 textColor = randomColor ? textColors[Random.Range(0, textColors.Count)] : textColors[0];
            textBaseColors[index] = textColor;
            cachedTexts[index].color = textColor;

            // Set font size
            textBaseSizes[index] = fontSize;
            cachedTexts[index].fontSize = fontSize;

            // Calculate animation start/end positions based on selected animation type
            CalculateTargetPosition(index, localPos);

            // Position text at animation start point
            cachedRectTransforms[index].anchoredPosition = textStartPositions[index];

            // Set initial scale
            float initialScale = normalScale;
            textInitialScales[index] = initialScale;
            cachedRectTransforms[index].localScale = new Vector3(initialScale, initialScale, 1f);

            // Record spawn time for animation
            textStartTimes[index] = Time.time;

            // Pre-calculate movement vector for animation updates
            Vector2 movementVector = textEndPositions[index] - textStartPositions[index];
            textMovementVectors[index] = movementVector;

            // Pre-calculate alpha range for fade animation
            Color32 baseColor = textBaseColors[index];
            textAlphaRanges[index] = baseColor.a / 255;

            // Cache scale range
            textScaleRanges[index] = textBaseSizes[index];

            // Activate the text
            textActiveStates[index] = true;
            textPool[index].SetActive(true);

            // Track as active
            activeTextIndices.Add(index);
            activeTextCount++;
        }

        /// <summary>
        /// Calculates the start and end positions for text animation based on the selected animation type.
        /// For directional animations, text moves from touchCenter + (direction * minDistance) 
        /// to touchCenter + (direction * maxDistance).
        /// </summary>
        /// <param name="index">Index of the text in the pool.</param>
        /// <param name="touchCenter">Center position where touch/click occurred.</param>
        private void CalculateTargetPosition(int index, Vector2 touchCenter)
        {
            Vector2 direction = Vector2.zero;

            // Determine movement direction based on animation type
            switch (appearanceAnimation)
            {
                case AppearanceAnimationType.None:
                    direction = Vector2.zero;
                    break;

                case AppearanceAnimationType.RadialArc:
                    // Random angle within configured arc range
                    float angleArc = Random.Range(-radialArcAngleRange, radialArcAngleRange);
                    float finalAngle = (radialArcCenterAngle + angleArc) * Mathf.Deg2Rad;
                    direction = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));
                    break;

                case AppearanceAnimationType.Down:
                    direction = downDirection;
                    break;

                case AppearanceAnimationType.Left:
                    direction = leftDirection;
                    break;

                case AppearanceAnimationType.Right:
                    direction = rightDirection;
                    break;

                case AppearanceAnimationType.Up:
                    direction = upDirection;
                    break;
            }

            // Calculate start and end positions along the direction vector
            Vector2 startPosition = touchCenter + direction * minMoveDistance;
            Vector2 endPosition = touchCenter + direction * maxMoveDistance;

            // Store calculated positions
            textStartPositions[index] = startPosition;
            textTargetPositions[index] = endPosition;
            textEndPositions[index] = endPosition;
        }

        /// <summary>
        /// Updates all active text animations by checking their lifetime and applying
        /// position, scale, and fade transformations. Deactivates expired texts.
        /// Iterates backward through list to safely remove items during iteration.
        /// </summary>
        private void UpdateActiveTexts()
        {
            float currentTime = Time.time;

            // Iterate backward to safely remove expired texts
            for (int i = activeTextIndices.Count - 1; i >= 0; i--)
            {
                int index = activeTextIndices[i];

                // Skip if text was deactivated externally
                if (!textActiveStates[index])
                {
                    activeTextIndices.RemoveAt(i);
                    continue;
                }

                // Calculate animation age
                float age = currentTime - textStartTimes[index];

                // Deactivate if lifetime expired
                if (age >= textLifetime)
                {
                    DeactivateText(index);
                    activeTextIndices.RemoveAt(i);
                    continue;
                }

                // Update animation frame
                UpdateTextAnimation(index, age);
            }
        }

        /// <summary>
        /// Updates a single text's animation frame based on its age.
        /// Applies position interpolation and updates fade/scale effects.
        /// </summary>
        /// <param name="index">Index of the text in the pool.</param>
        /// <param name="age">Time elapsed since text was spawned.</param>
        private void UpdateTextAnimation(int index, float age)
        {
            // Normalize age to 0-1 range
            float normalizedAge = age * lifetimeInverse;
            normalizedAge = Mathf.Clamp01(normalizedAge);

            // Calculate position based on animation type and interpolation method
            Vector2 newPos;
            switch (appearanceAnimation)
            {
                default:
                    if (useEndPositionsForMovement)
                    {
                        // Smooth interpolation for more natural movement
                        float t = Mathf.SmoothStep(0f, 1f, normalizedAge);
                        newPos = textStartPositions[index] + textMovementVectors[index] * t;
                    }
                    else
                    {
                        // Linear interpolation
                        newPos = textStartPositions[index] + textMovementVectors[index] * normalizedAge;
                    }
                    break;
            }
            cachedRectTransforms[index].anchoredPosition = newPos;

            // Apply appearance scale and update fade/scale
            float appearanceScale = normalScale;
            UpdateFadeAndScale(index, normalizedAge, appearanceScale);
        }

        /// <summary>
        /// Updates alpha (fade) and scale for a text based on its normalized age.
        /// Text fades from full opacity to transparent and shrinks to zero scale over its lifetime.
        /// </summary>
        /// <param name="index">Index of the text in the pool.</param>
        /// <param name="normalizedAge">Age normalized to 0-1 range (0=start, 1=end).</param>
        /// <param name="appearanceScale">Base scale multiplier for appearance.</param>
        private void UpdateFadeAndScale(int index, float normalizedAge, float appearanceScale)
        {
            // Calculate fade: full opacity at start, transparent at end
            float alpha = (1f - normalizedAge) * textAlphaRanges[index];
            Color32 tempColor32 = textBaseColors[index];
            tempColor32.a = (byte)(alpha * 255);
            cachedTexts[index].color = tempColor32;

            // Calculate scale: full size at start, zero at end
            float disappearScale = 1f - normalizedAge;
            float finalScale = disappearScale * appearanceScale;
            cachedRectTransforms[index].localScale = new Vector3(finalScale, finalScale, 1f);
        }

        /// <summary>
        /// Deactivates a text object and returns it to the available pool.
        /// Hides the GameObject and decrements the active text counter.
        /// </summary>
        /// <param name="index">Index of the text to deactivate.</param>
        private void DeactivateText(int index)
        {
            // Mark as inactive
            textActiveStates[index] = false;

            // Hide GameObject
            textPool[index].SetActive(false);

            // Return to available pool
            availableTextIndices.Add(index);

            // Update counter
            activeTextCount--;
        }

    }

#if UNITY_EDITOR
    /// <summary>
    /// Custom editor for TouchTextUI component.
    /// Provides upgrade information and auto-detects required Canvas reference.
    /// </summary>
    [CustomEditor(typeof(TouchTextUI))]
    public class TouchTextUIEditor : Editor
    {
        /// <summary>
        /// Called when the editor is enabled.
        /// Auto-detects and assigns the target Canvas if not already set.
        /// </summary>
        void OnEnable()
        {
            TouchTextUI textEffect = (TouchTextUI)target;

            if (!textEffect) return;

            // Skip if editing a prefab in prefab stage
            if (PrefabStageUtility.GetPrefabStage(textEffect.gameObject)) return;

            // Skip if this is a prefab asset
            if (EditorUtility.IsPersistent(textEffect)) return;

            // Auto-detect Canvas reference if not assigned
            if (textEffect.targetCanvas == null)
            {
                textEffect.targetCanvas = textEffect.GetComponentInParent<Canvas>();
                EditorUtility.SetDirty(textEffect);
            }
        }

        /// <summary>
        /// Renders the custom inspector GUI with upgrade information and default inspector.
        /// </summary>
        public override void OnInspectorGUI()
        {
            DrawUpgradeSection();
            DrawDefaultInspector();
        }

        /// <summary>
        /// Draws the promotional section for the full Touch Effect System.
        /// Provides upgrade button and feature comparison information.
        /// </summary>
        private void DrawUpgradeSection()
        {
            var originalColor = GUI.backgroundColor;

            // Upgrade button with green background
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            GUILayout.Space(5);

            if (GUILayout.Button("Get The Full Touch Effect System >", GUILayout.Height(35)))
            {
                Application.OpenURL("https://u3d.as/3C7x");
            }

            GUI.backgroundColor = originalColor;
            GUILayout.Space(8);

            // Feature list in styled help box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 12;
            EditorGUILayout.LabelField("Full Version Includes:", titleStyle);

            GUILayout.Space(3);

            var bulletStyle = new GUIStyle(EditorStyles.label);
            bulletStyle.fontSize = 10;
            bulletStyle.wordWrap = true;

            EditorGUILayout.LabelField("• Separate PC/mobile optimization settings, multitouch support, Canvas coordinate conversion", bulletStyle);
            EditorGUILayout.LabelField("• 25+ shader effects: heat trails, blades, expanding rings, neon shapes, lightning, etc", bulletStyle);
            EditorGUILayout.LabelField("• Advanced trail system with spacing, pooling, sharp edges", bulletStyle);
            EditorGUILayout.LabelField("• Sprite-based particle system with dispersion patterns", bulletStyle);
            EditorGUILayout.LabelField("• Preview system compatible with Automatic Tutorial Maker", bulletStyle);
            EditorGUILayout.LabelField("• Technical support and custom development services", bulletStyle);

            GUILayout.Space(3);

            var linkStyle = new GUIStyle(EditorStyles.miniLabel);
            linkStyle.normal.textColor = new Color(1f, 1f, 1f);
            linkStyle.fontSize = 9;
            EditorGUILayout.LabelField("This is the free mini version with basic OneTouch effects only.", linkStyle);

            EditorGUILayout.EndVertical();
            GUILayout.Space(8);
        }
    }
#endif
}