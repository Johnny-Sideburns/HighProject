using UnityEngine;
using UnityEngine.InputSystem;

// Based on the tutorial "Building a Camera Controller for a Strategy Game" by Game Dev Guide:
// https://www.youtube.com/watch?v=rnqF6S7PfFA
// Reworked to use the new Input System instead of the legacy Input class.
public class CameraController : MonoBehaviour
{
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
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        // The new Input System reports scroll in platform-dependent units (e.g. 120 per notch on Windows),
        // so only the direction is used to match the old one-step-per-notch behaviour.
        float scroll = mouse.scroll.ReadValue().y;
        if (scroll != 0)
        {
            newZoom += Mathf.Sign(scroll) * zoomAmount;
        }
        
        if (mouse.leftButton.wasPressedThisFrame)
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());

            float entry;
            
            if (plane.Raycast(ray, out entry))
            {
                dragStartPosition = ray.GetPoint(entry);
            }
        }

        if (mouse.leftButton.isPressed)
        {
            Plane plane = new Plane(Vector3.up, Vector3.zero);

            Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());

            float entry;
            
            if (plane.Raycast(ray, out entry))
            {
                dragCurrentPosition = ray.GetPoint(entry);
                
                newPosition = transform.position + dragStartPosition - dragCurrentPosition;
            }
        }

        if (mouse.middleButton.wasPressedThisFrame)
        {
            rotateStartPosition = mouse.position.ReadValue();
        }

        if (mouse.middleButton.isPressed)
        {
            rotateCurrentPosition = mouse.position.ReadValue();
            
            Vector3 difference = rotateStartPosition - rotateCurrentPosition;
            
            rotateStartPosition = rotateCurrentPosition;
            
            newRotation *= Quaternion.Euler(Vector3.up * (-difference.x/5f)); 
        }
    }
    
    void HandleMovementInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.leftShiftKey.isPressed)
        {
            movementSpeed = fastSpeed;
        }
        else
        {
            movementSpeed = normalSpeed;
        }
        
        
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            newPosition += transform.forward * movementSpeed;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            newPosition -= transform.forward * movementSpeed;
        }
        
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            newPosition += transform.right * movementSpeed;
        }
        
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            newPosition -= transform.right * movementSpeed;
        }

        if (keyboard.qKey.isPressed)
        {
            newRotation *= Quaternion.Euler(Vector3.up * rotationAmount);
        }

        if (keyboard.eKey.isPressed)
        {
            newRotation *= Quaternion.Euler(Vector3.up * -rotationAmount);
        }

        if (keyboard.rKey.isPressed)
        {
            newZoom += zoomAmount;
        }

        if (keyboard.fKey.isPressed)
        {
            newZoom -= zoomAmount;
        }
        
        transform.position = Vector3.Lerp(transform.position, newPosition, movementTime * Time.deltaTime); 
        transform.rotation = Quaternion.Lerp(transform.rotation, newRotation, movementTime * Time.deltaTime);
        cameraTransform.localPosition = Vector3.Lerp(cameraTransform.localPosition, newZoom, movementTime * Time.deltaTime);
    }
}
