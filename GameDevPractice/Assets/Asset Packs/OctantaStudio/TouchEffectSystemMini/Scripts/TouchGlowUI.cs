using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif


namespace TouchEffectSystem
{
    /// <summary>
    /// Manages touch glow visual effects on UI Canvas elements.
    /// Creates and animates a glowing point effect that follows user input (mouse/touch).
    /// Part of the Touch Effect System - Mini version with basic OneTouch functionality.
    /// </summary>
    public class TouchGlowUI : MonoBehaviour
    {
        [Header("References")]
        /// <summary>
        /// Target canvas where the glow effect will be rendered.
        /// Auto-detected from parent if not assigned.
        /// </summary>
        public Canvas targetCanvas;

        /// <summary>
        /// Unified input detection system for handling mouse and touch inputs.
        /// </summary>
        [SerializeField] private InputController inputController;

        /// <summary>
        /// Material containing the glow shader for the visual effect.
        /// </summary>
        [SerializeField] private Material glowMaterial;

        /// <summary>
        /// Prefab containing the RawImage component for the glow point.
        /// </summary>
        [SerializeField] private GameObject pointPrefab;

        [Header("Settings")]
        /// <summary>
        /// Duration in seconds that the glow effect remains active after touch ends.
        /// </summary>
        [SerializeField] private float glowLifetime = 1.0f;

        /// <summary>
        /// Size of the glow point in pixels (width and height).
        /// </summary>
        [SerializeField] private float pointSize = 120f;

        /// <summary>
        /// Runtime instance of the glow point GameObject.
        /// </summary>
        private GameObject pointObj;

        /// <summary>
        /// RectTransform component for positioning the glow point in UI space.
        /// </summary>
        private RectTransform rectTransform;

        /// <summary>
        /// Runtime instance of the glow material (cloned to avoid shared material issues).
        /// </summary>
        private Material pointMaterial;

        // Shader property IDs cached for performance
        private static readonly int StartTimeID = Shader.PropertyToID("_StartTime");
        private static readonly int TimeNowID = Shader.PropertyToID("_TimeNow");
        private static readonly int LifetimeID = Shader.PropertyToID("_Lifetime");

        /// <summary>
        /// Indicates whether the glow effect is currently active/visible.
        /// </summary>
        private bool isActive = false;

        /// <summary>
        /// Timestamp when the current glow effect was activated.
        /// </summary>
        private float startTime = 0f;

        /// <summary>
        /// Cached child count for hierarchy change detection.
        /// Used to optimize SetAsLastSibling calls.
        /// </summary>
        private int lastChildCount = -1;

        /// <summary>
        /// Initializes references, creates the glow point instance, and sets up materials.
        /// Disables the component if required references are missing.
        /// </summary>
        void Start()
        {
            // Auto-detect canvas if not assigned
            if (targetCanvas == null)
                targetCanvas = GetComponentInParent<Canvas>();

            // Validate required references
            if (glowMaterial == null || pointPrefab == null || targetCanvas == null)
            {
                Debug.LogError("[MiniGlow] Setup missing references!");
                enabled = false;
                return;
            }

            // Instantiate and configure the glow point
            pointObj = Instantiate(pointPrefab, transform);
            rectTransform = pointObj.GetComponent<RectTransform>();
            rectTransform.sizeDelta = new Vector2(pointSize, pointSize);

            // Create material instance to avoid shared material modification
            var rawImage = pointObj.GetComponent<RawImage>();
            pointMaterial = new Material(glowMaterial);
            rawImage.material = pointMaterial;

            // Initialize shader properties
            pointMaterial.SetFloat(LifetimeID, glowLifetime);
            pointMaterial.SetFloat(StartTimeID, 0);
            pointMaterial.SetFloat(TimeNowID, 0);

            // Start with effect disabled
            pointObj.SetActive(false);
        }

        /// <summary>
        /// Handles input detection, effect positioning, lifetime management, and rendering order.
        /// Called once per frame.
        /// </summary>
        void Update()
        {
            if (inputController == null) return; // No input controller available

            // Handle input state changes
            if (inputController.GetInputDown())
            {
                ActivatePoint(inputController.GetInputPosition());
            }
            else if (inputController.IsInputMoving() && isActive)
            {
                UpdatePoint(inputController.GetInputPosition());
            }
            else if (inputController.GetInputUp())
            {
                DeactivatePoint();
            }

            // Update active effect
            if (isActive)
            {
                // Update shader time for animation
                float currentTime = Time.time;
                pointMaterial.SetFloat(TimeNowID, currentTime);

                // Check if effect lifetime has expired
                if (currentTime - startTime > glowLifetime)
                {
                    pointObj.SetActive(false);
                    isActive = false;
                }

                // Ensure effect renders on top of other UI elements
                // Only update hierarchy position if it has changed
                int currentChildCount = transform.parent != null ? transform.parent.childCount : 0;
                int siblingIndex = transform.GetSiblingIndex(); // Get current position in hierarchy
                int lastIndex = currentChildCount - 1; // Calculate last sibling position
                if (siblingIndex != lastIndex) // Only update if not already last
                {
                    SetAslastSibling();
                }
                lastChildCount = currentChildCount; // Cache count for next frame
            }
        }

        /// <summary>
        /// Moves this GameObject to the last position in its parent's hierarchy.
        /// This ensures the glow effect renders on top of other UI elements.
        /// </summary>
        private void SetAslastSibling()
        {
            transform.SetAsLastSibling(); // Move to top of rendering order

            Debug.Log($"[TES mini] Touch effect set as last sibling");
        }

        /// <summary>
        /// Activates the glow effect at the specified screen position.
        /// Converts screen coordinates to local canvas space and initializes shader timing.
        /// </summary>
        /// <param name="screenPos">Screen position in pixels where the effect should appear.</param>
        private void ActivatePoint(Vector2 screenPos)
        {
            if (rectTransform == null) return;

            // Convert screen position to local canvas coordinates
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform,
                screenPos,
                null,
                out localPos);

            // Position the glow point
            rectTransform.anchoredPosition = localPos;

            // Initialize shader timing for animation start
            startTime = Time.time;
            pointMaterial.SetFloat(StartTimeID, startTime);
            pointMaterial.SetFloat(TimeNowID, startTime);

            // Show the effect
            pointObj.SetActive(true);
            isActive = true;
        }

        /// <summary>
        /// Updates the glow effect position while input is being dragged/moved.
        /// Converts screen coordinates to local canvas space.
        /// </summary>
        /// <param name="screenPos">Current screen position in pixels.</param>
        private void UpdatePoint(Vector2 screenPos)
        {
            if (rectTransform == null) return;

            // Convert screen position to local canvas coordinates
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                targetCanvas.transform as RectTransform,
                screenPos,
                null,
                out localPos);

            // Update position to follow input
            rectTransform.anchoredPosition = localPos;
        }

        /// <summary>
        /// Deactivates input tracking but keeps the effect visible during its lifetime fade-out.
        /// The effect will automatically hide after the glowLifetime expires.
        /// </summary>
        private void DeactivatePoint()
        {
            isActive = true;
        }

    }

#if UNITY_EDITOR
    /// <summary>
    /// Custom editor for TouchGlowUI component.
    /// Provides upgrade information and auto-detects required Canvas reference.
    /// </summary>
    [CustomEditor(typeof(TouchGlowUI))]
    public class TouchGlowUIEditor : Editor
    {
        /// <summary>
        /// Called when the editor is enabled.
        /// Auto-detects and assigns the target Canvas if not already set.
        /// </summary>
        void OnEnable()
        {
            TouchGlowUI glowUI = (TouchGlowUI)target;

            if (!glowUI) return;

            // Skip if editing a prefab in prefab stage
            if (PrefabStageUtility.GetPrefabStage(glowUI.gameObject)) return;

            // Skip if this is a prefab asset
            if (EditorUtility.IsPersistent(glowUI)) return;

            // Auto-detect Canvas reference if not assigned
            if (glowUI.targetCanvas == null)
            {
                glowUI.targetCanvas = glowUI.GetComponentInParent<Canvas>();
                EditorUtility.SetDirty(glowUI);
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

            // Upgrade button
            GUI.backgroundColor = new Color(0.2f, 0.8f, 0.2f);
            GUILayout.Space(5);

            if (GUILayout.Button("Get The Full Touch Effect System >", GUILayout.Height(35)))
            {
                Application.OpenURL("https://u3d.as/3C7x");
            }

            GUI.backgroundColor = originalColor;
            GUILayout.Space(8);

            // Info box
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var titleStyle = new GUIStyle(EditorStyles.boldLabel);
            titleStyle.fontSize = 12;
            EditorGUILayout.LabelField("Full Version Includes:", titleStyle);

            GUILayout.Space(3);

            var bulletStyle = new GUIStyle(EditorStyles.label);
            bulletStyle.fontSize = 10;
            bulletStyle.wordWrap = true;

            EditorGUILayout.LabelField("� Separate PC/mobile optimization settings, multitouch support, Canvas coordinate conversion", bulletStyle);
            EditorGUILayout.LabelField("� 25+ shader effects: heat trails, blades, expanding rings, neon shapes, lightning, etc", bulletStyle);
            EditorGUILayout.LabelField("� Advanced trail system with spacing, pooling, sharp edges", bulletStyle);
            EditorGUILayout.LabelField("� Sprite-based particle system with dispersion patterns", bulletStyle);
            EditorGUILayout.LabelField("� Preview system compatible with Automatic Tutorial Maker", bulletStyle);
            EditorGUILayout.LabelField("� Technical support and custom development services", bulletStyle);

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