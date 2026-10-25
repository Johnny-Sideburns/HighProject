using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Based on the tutorial "Building a Camera Controller for a Strategy Game" by Game Dev Guide:
// https://www.youtube.com/watch?v=rnqF6S7PfFA
// Reworked to use the new Input System (rebindable actions) instead of the legacy Input class.
// Better naming conventions, encapsulation and frame-rate independent movement were used than in the tutorial.
public class CameraController : MonoBehaviour
{
    [Header("Input (assign the actions from the Camera map of InputSystem_Actions)")]
    [SerializeField] InputActionReference moveAction;
    [SerializeField] InputActionReference rotateAction;
    [SerializeField] InputActionReference zoomAction;
    [SerializeField] InputActionReference scrollZoomAction;
    [SerializeField] InputActionReference sprintAction;
    [SerializeField] InputActionReference dragAction;
    [SerializeField] InputActionReference rotateDragAction;
    [SerializeField] InputActionReference pointerAction;

    [Header("References")]
    [SerializeField] Transform cameraTransform;

    [Header("Panning (units per second)")]
    [SerializeField, Min(0f)] float panSpeed = 30f;
    [SerializeField, Min(0f)] float fastPanSpeed = 180f;

    [Header("Rotation (degrees)")]
    [Tooltip("Degrees per second while the rotate keys are held.")]
    [SerializeField, Min(0f)] float keyRotateSpeed = 60f;
    [Tooltip("Degrees per screen pixel while rotating with the mouse.")]
    [SerializeField, Min(0f)] float mouseRotateSensitivity = 0.2f;

    [Header("Zoom (offset applied to the camera's local position)")]
    [Tooltip("Applied once per mouse wheel notch.")]
    [FormerlySerializedAs("zoomAmount")]
    [SerializeField] Vector3 scrollZoomStep = new Vector3(0f, -10f, 10f);
    [Tooltip("Applied per second while the zoom keys are held.")]
    [SerializeField] Vector3 keyZoomSpeed = new Vector3(0f, -20f, 20f);

    [Header("Smoothing")]
    [Tooltip("Higher values make the camera catch up with its target faster.")]
    [SerializeField, Min(0f)] float movementTime = 5f;

    static readonly Plane GroundPlane = new Plane(Vector3.up, Vector3.zero);

    Camera mainCamera;
    InputActionReference[] allActions;

    Vector3 targetPosition;
    Quaternion targetRotation;
    Vector3 targetZoom;

    Vector3 dragStartPoint;
    Vector2 lastRotatePointer;

    void Awake()
    {
        mainCamera = Camera.main;

        allActions = new[]
        {
            moveAction, rotateAction, zoomAction, scrollZoomAction,
            sprintAction, dragAction, rotateDragAction, pointerAction
        };
    }

    void OnEnable()
    {
        foreach (InputActionReference reference in allActions)
        {
            reference.action.Enable();
        }
    }

    void OnDisable()
    {
        foreach (InputActionReference reference in allActions)
        {
            reference.action.Disable();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        targetZoom = cameraTransform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        float deltaTime = Time.deltaTime;

        ReadKeyboardInput(deltaTime);
        ReadMouseInput();
        ApplySmoothing(deltaTime);
    }

    void ReadMouseInput()
    {
        // Scroll units are platform dependent (e.g. 120 per notch on Windows), so only the direction is used.
        float scroll = scrollZoomAction.action.ReadValue<float>();
        if (scroll != 0f)
        {
            targetZoom += scrollZoomStep * Mathf.Sign(scroll);
        }

        Vector2 pointer = pointerAction.action.ReadValue<Vector2>();

        if (dragAction.action.WasPressedThisFrame())
        {
            TryGetGroundPoint(pointer, out dragStartPoint);
        }
        else if (dragAction.action.IsPressed() && TryGetGroundPoint(pointer, out Vector3 dragCurrentPoint))
        {
            targetPosition = transform.position + dragStartPoint - dragCurrentPoint;
        }

        if (rotateDragAction.action.WasPressedThisFrame())
        {
            lastRotatePointer = pointer;
        }
        else if (rotateDragAction.action.IsPressed())
        {
            float deltaX = pointer.x - lastRotatePointer.x;
            lastRotatePointer = pointer;

            targetRotation *= Quaternion.Euler(0f, deltaX * mouseRotateSensitivity, 0f);
        }
    }
    
    void ReadKeyboardInput(float deltaTime)
    {
        float speed = sprintAction.action.IsPressed() ? fastPanSpeed : panSpeed;

        Vector2 move = moveAction.action.ReadValue<Vector2>();
        targetPosition += (transform.forward * move.y + transform.right * move.x) * (speed * deltaTime);

        float rotate = rotateAction.action.ReadValue<float>();
        targetRotation *= Quaternion.Euler(0f, rotate * keyRotateSpeed * deltaTime, 0f);

        targetZoom += keyZoomSpeed * (zoomAction.action.ReadValue<float>() * deltaTime);
    }

    bool TryGetGroundPoint(Vector2 screenPoint, out Vector3 point)
    {
        Ray ray = mainCamera.ScreenPointToRay(screenPoint);

        if (GroundPlane.Raycast(ray, out float distance))
        {
            point = ray.GetPoint(distance);
            return true;
        }

        point = default;
        return false;
    }

    void ApplySmoothing(float deltaTime)
    {
        // Frame-rate independent equivalent of Lerp(current, target, rate * deltaTime).
        float t = 1f - Mathf.Exp(-movementTime * deltaTime);

        transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, t);
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, targetZoom, t);
    }
}
