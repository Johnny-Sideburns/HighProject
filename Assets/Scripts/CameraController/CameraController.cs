using UnityEngine;
using UnityEngine.InputSystem;

// Based on the tutorial "Building a Camera Controller for a Strategy Game" by Game Dev Guide:
// https://www.youtube.com/watch?v=rnqF6S7PfFA
// Reworked to use the new Input System (rebindable actions) instead of the legacy Input class.
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

    [Header("Movement")]
    public Transform cameraTransform;
    
    public float normalSpeed;
    public float fastSpeed;
    
    public float movementSpeed;
    public float movementTime;

    public float rotationAmount;
    public Vector3 zoomAmount;
    
    public Vector3 newPosition;
    public Quaternion newRotation;
    public Vector3 newZoom;
    
    public Vector3 dragStartPosition;
    public Vector3 dragCurrentPosition;
    public Vector3 rotateStartPosition;
    public Vector3 rotateCurrentPosition;
    
    InputActionReference[] AllActions => new[]
    {
        moveAction, rotateAction, zoomAction, scrollZoomAction,
        sprintAction, dragAction, rotateDragAction, pointerAction
    };

    void OnEnable()
    {
        foreach (InputActionReference reference in AllActions)
        {
            reference.action.Enable();
        }
    }

    void OnDisable()
    {
        foreach (InputActionReference reference in AllActions)
        {
            reference.action.Disable();
        }
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        newPosition = transform.position;
        newRotation = transform.rotation;
        newZoom = cameraTransform.localPosition;
    }

    // Update is called once per frame
    void Update()
    {
        HandleMovementInput();
        HandleMouseInput();
    }

    void HandleMouseInput()
    {
        // The new Input System reports scroll in platform-dependent units (e.g. 120 per notch on Windows),
        // so only the direction is used to match the old one-step-per-notch behaviour.
        float scroll = scrollZoomAction.action.ReadValue<float>();
        if (scroll != 0)
        {
            newZoom += Mathf.Sign(scroll) * zoomAmount;
        }

        Vector2 pointer = pointerAction.action.ReadValue<Vector2>();

        if (dragAction.action.WasPressedThisFrame())
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            Ray ray = Camera.main.ScreenPointToRay(pointer);

            float entry;
            
            if (plane.Raycast(ray, out entry))
            {
                dragStartPosition = ray.GetPoint(entry);
            }
        }

        if (dragAction.action.IsPressed())
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            Ray ray = Camera.main.ScreenPointToRay(pointer);

            float entry;
            
            if (plane.Raycast(ray, out entry))
            {
                dragCurrentPosition = ray.GetPoint(entry);
                
                newPosition = transform.position + dragStartPosition - dragCurrentPosition;
            }
        }

        if (rotateDragAction.action.WasPressedThisFrame())
        {
            rotateStartPosition = pointer;
        }

        if (rotateDragAction.action.IsPressed())
        {
            rotateCurrentPosition = pointer;
            
            Vector3 difference = rotateStartPosition - rotateCurrentPosition;
            
            rotateStartPosition = rotateCurrentPosition;
            
            newRotation *= Quaternion.Euler(Vector3.up * (-difference.x/5f)); 
        }
    }
    
    void HandleMovementInput()
    {
        movementSpeed = sprintAction.action.IsPressed() ? fastSpeed : normalSpeed;

        Vector2 move = moveAction.action.ReadValue<Vector2>();
        newPosition += (transform.forward * move.y + transform.right * move.x) * movementSpeed;

        newRotation *= Quaternion.Euler(Vector3.up * (rotateAction.action.ReadValue<float>() * rotationAmount));

        newZoom += zoomAction.action.ReadValue<float>() * zoomAmount;
        
        transform.position = Vector3.Lerp(transform.position, newPosition, movementTime * Time.deltaTime); 
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, movementTime * Time.deltaTime);
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newZoom, movementTime * Time.deltaTime);
    }
}
